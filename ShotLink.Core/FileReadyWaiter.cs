using System.Buffers.Binary;

namespace ShotLink.Core;

/// <summary>ファイルが書き終わるまで待つ（仕様書 3.2）</summary>
/// <remarks>
/// 見張りの「作られた」の知らせは、VRChat がまだ書いている途中に届く。途中の PNG は、Windows の画像の部品が
/// エラーを出さずに読み、書かれていない下の方が黒い画像になる（2026-10-14 に実物の 8K を半分で切って確かめた。QR が消えた）。
///
/// 「開けて、大きさが2回続けて同じ」だけでは足りなかった。VRChat は 8K の PNG を約 1 秒かけて書き、その合間に
/// 手が止まるので、途中で「書き終わった」と見なして、8K の写真 4 枚とも QR を取りこぼした（2026-10-14、作者の実機）。
/// そこで、形式ごとの「終わりの印」がそろうまで待つ：PNG は最後が IEND、WebP は頭に書かれた全体の大きさ、JPEG は最後が FFD9
/// </remarks>
public sealed class FileReadyWaiter(IFileProbe probe, Func<TimeSpan, CancellationToken, Task> delay)
{
    /// <summary>
    /// 見に行く間隔。短くしすぎると、書き込み中に何度も開いて VRChat の邪魔になる
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 終わりの印で見分けられないとき（JPEG の印の後ろに余分を付ける道具・知らない形式）は、大きさがこれだけ変わらなければ読む
    /// </summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(1);

    /// <summary>これを過ぎても書き終わらないファイルはあきらめる（消された・別のソフトが掴み続けている・壊れている）</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static FileReadyWaiter CreateDefault() => new(new FileProbe(), Task.Delay);

    /// <summary>読んでよくなったら true。あきらめたら false</summary>
    public async Task<bool> WaitAsync(string path, CancellationToken cancel = default)
    {
        long? previous = null;
        var quiet = TimeSpan.Zero;
        var waited = TimeSpan.Zero;
        while (true)
        {
            var check = probe.Check(path);
            if (check.Length is > 0 && check.Length == previous)
            {
                // 終わりの印がそろっていれば、すぐ読む。印が無い形式は、しばらく大きさが変わらなければ読む。
                // 印が足りない PNG・WebP は、どれだけ待っても途中のまま読まない
                if (check.Ending == FileEnding.Complete
                    || (check.Ending == FileEnding.Unknown && quiet >= QuietPeriod))
                {
                    return true;
                }

                quiet += Interval;
            }
            else
            {
                quiet = TimeSpan.Zero;
            }

            if (waited >= Timeout)
            {
                return false;
            }

            previous = check.Length;
            await delay(Interval, cancel);
            waited += Interval;
        }
    }
}

public enum FileEnding
{
    /// <summary>形式の終わりの印がそろっている</summary>
    Complete,

    /// <summary>終わりの印が足りない（まだ書いている）</summary>
    Incomplete,

    /// <summary>印で見分けられない（知らない形式・JPEG の印の後ろに余分がある）</summary>
    Unknown,
}

/// <param name="Length">読むために開けたらその大きさ、開けない（書き込み中・無い）なら null</param>
public readonly record struct FileCheck(long? Length, FileEnding Ending);

public interface IFileProbe
{
    FileCheck Check(string path);
}

public sealed class FileProbe : IFileProbe
{
    static readonly byte[] PngEnd = [0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82];

    public FileCheck Check(string path)
    {
        try
        {
            // 書き込み中のファイルは、書き込みを共有しない開き方だと失敗する。それで「まだ書いている」を見分ける。
            // ただ、書き手が合間にファイルを閉じることがあるので、これだけには頼らない（終わりの印も見る）
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new FileCheck(stream.Length, Ending(stream, Path.GetExtension(path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new FileCheck(null, FileEnding.Incomplete);
        }
    }

    /// <summary>形式ごとの終わりの印を見る。拡張子が偽られていても、頭の印で形式を見分ける</summary>
    public static FileEnding Ending(Stream stream, string extension)
    {
        var length = stream.Length;
        if (length < 16)
        {
            return FileEnding.Incomplete;
        }

        Span<byte> head = stackalloc byte[12];
        stream.Position = 0;
        stream.ReadExactly(head);

        // PNG：最後の 12 バイトが IEND の塊
        if (head[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            Span<byte> tail = stackalloc byte[12];
            stream.Position = length - tail.Length;
            stream.ReadExactly(tail);
            return tail.SequenceEqual(PngEnd) ? FileEnding.Complete : FileEnding.Incomplete;
        }

        // WebP：RIFF の頭に書かれた大きさ ＋ 8 が、ファイルの大きさ
        if (head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            var declared = BinaryPrimitives.ReadUInt32LittleEndian(head[4..8]) + 8L;
            return length >= declared ? FileEnding.Complete : FileEnding.Incomplete;
        }

        // JPEG：最後が FFD9。ただし後ろに余分を付ける道具があるので、合わなくても「分からない」に留める
        if (head[0] == 0xFF && head[1] == 0xD8)
        {
            Span<byte> tail = stackalloc byte[2];
            stream.Position = length - 2;
            stream.ReadExactly(tail);
            return tail[0] == 0xFF && tail[1] == 0xD9 ? FileEnding.Complete : FileEnding.Unknown;
        }

        return FileEnding.Unknown;
    }
}
