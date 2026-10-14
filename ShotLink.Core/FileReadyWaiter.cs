namespace ShotLink.Core;

/// <summary>ファイルが開けて、大きさが変わらなくなるまで待つ（仕様書 3.2）</summary>
/// <remarks>
/// 見張りの「作られた」の知らせは、VRChat がまだ書いている途中に届く。途中のファイルを読むと、
/// PNG が壊れているか、下の方が欠けた画像になる。
/// </remarks>
public sealed class FileReadyWaiter(IFileProbe probe, Func<TimeSpan, CancellationToken, Task> delay)
{
    /// <summary>
    /// 8K の PNG（数十 MB）でも書き終わるのは 1 秒前後。間隔を短くしすぎると、書き込み中に何度も開いて VRChat の邪魔になる
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    /// <summary>これを過ぎても落ち着かないファイルはあきらめる（消された・別のソフトが掴み続けている）</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static FileReadyWaiter CreateDefault() => new(new FileProbe(), Task.Delay);

    /// <summary>読んでよくなったら true。あきらめたら false</summary>
    public async Task<bool> WaitAsync(string path, CancellationToken cancel = default)
    {
        long? previous = null;
        var waited = TimeSpan.Zero;
        while (true)
        {
            var length = probe.TryGetReadableLength(path);

            // 開けて、前回と同じ大きさ（0 ではない）なら書き終わったとみなす。
            // 1回目で開けても、まだ書き足されている途中のことがあるので、もう1回見る
            if (length is > 0 && length == previous)
            {
                return true;
            }

            if (waited >= Timeout)
            {
                return false;
            }

            previous = length;
            await delay(Interval, cancel);
            waited += Interval;
        }
    }
}

public interface IFileProbe
{
    /// <summary>読むために開けたらその大きさ、開けない（書き込み中・無い）なら null</summary>
    long? TryGetReadableLength(string path);
}

public sealed class FileProbe : IFileProbe
{
    public long? TryGetReadableLength(string path)
    {
        try
        {
            // 書き込み中のファイルは、書き込みを共有しない開き方だと失敗する。それで「まだ書いている」を見分ける
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Length;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
