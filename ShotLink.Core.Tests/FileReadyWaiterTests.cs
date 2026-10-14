using ShotLink.Core;
using ShotLink.Fixtures;

namespace ShotLink.Core.Tests;

public class FileReadyWaiterTests
{
    /// <summary>呼ばれるたびに、並べた結果を順に返す（最後の物を繰り返す）</summary>
    sealed class ScriptedProbe(params FileCheck[] checks) : IFileProbe
    {
        int _i;

        public int Calls => _i;

        public FileCheck Check(string path) => checks[Math.Min(_i++, checks.Length - 1)];
    }

    static FileCheck Done(long length) => new(length, FileEnding.Complete);
    static FileCheck Partial(long length) => new(length, FileEnding.Incomplete);
    static FileCheck Unsure(long length) => new(length, FileEnding.Unknown);
    static readonly FileCheck Locked = new(null, FileEnding.Incomplete);

    static readonly Func<TimeSpan, CancellationToken, Task> NoWait = (_, _) => Task.CompletedTask;

    [Fact]
    public async Task 終わりの印がそろい大きさが二回続けて同じなら読んでよい()
    {
        var probe = new ScriptedProbe(Done(1000), Done(1000));
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(2, probe.Calls);
    }

    [Fact]
    public async Task 書き込み中で開けない間は待つ()
    {
        var probe = new ScriptedProbe(Locked, Locked, Locked, Done(5000), Done(5000));
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(5, probe.Calls);
    }

    [Fact]
    public async Task 大きくなり続けている間は待つ()
    {
        var probe = new ScriptedProbe(Partial(100), Partial(2000), Done(4000), Done(4000));
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(4, probe.Calls);
    }

    // 2026-10-14 の実機：VRChat が 8K を書く合間に手を止め、大きさが同じに見えた所で読んで QR を取りこぼした
    [Fact]
    public async Task 大きさが止まっても終わりの印が無い_PNG_は読まない()
    {
        var probe = new ScriptedProbe(Partial(3000), Partial(3000), Partial(3000), Partial(9000), Done(12000), Done(12000));
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(6, probe.Calls);
    }

    [Fact]
    public async Task 終わりの印が来ないままなら_十秒であきらめる()
    {
        var probe = new ScriptedProbe(Partial(3000));
        Assert.False(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
    }

    [Fact]
    public async Task 印で見分けられない形式は_大きさが一秒変わらなければ読む()
    {
        var probe = new ScriptedProbe(Unsure(800));
        var waited = TimeSpan.Zero;
        Task Delay(TimeSpan d, CancellationToken _)
        {
            waited += d;
            return Task.CompletedTask;
        }

        Assert.True(await new FileReadyWaiter(probe, Delay).WaitAsync("x.jpg"));
        Assert.True(waited >= FileReadyWaiter.QuietPeriod);
    }

    [Fact]
    public async Task 大きさ_0_のままなら読まない()
    {
        Assert.False(await new FileReadyWaiter(new ScriptedProbe(Done(0)), NoWait).WaitAsync("x.png"));
    }

    [Fact]
    public async Task 十秒たっても開けなければあきらめる()
    {
        var probe = new ScriptedProbe(Locked);
        var waited = TimeSpan.Zero;
        Task Delay(TimeSpan d, CancellationToken _)
        {
            waited += d;
            return Task.CompletedTask;
        }

        Assert.False(await new FileReadyWaiter(probe, Delay).WaitAsync("x.png"));
        Assert.Equal(FileReadyWaiter.Timeout, waited);
    }

    [Fact]
    public async Task 本物のファイルでも読める()
    {
        using var dir = new TempDir();
        var path = dir.Combine("a.png");
        PngWriter.Write(path, new SceneBuilder(64, 64).Build(), rgba: true);
        Assert.True(await FileReadyWaiter.CreateDefault().WaitAsync(path));
    }

    [Fact]
    public async Task 書き込み中に掴まれているファイルは開けないと分かる()
    {
        using var dir = new TempDir();
        var path = dir.Combine("a.png");
        await using var writing = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        writing.Write(new byte[100]);
        Assert.Null(new FileProbe().Check(path).Length);
    }
}

public class FileEndingTests
{
    static FileEnding Of(byte[] bytes, string ext = ".png") => FileProbe.Ending(new MemoryStream(bytes), ext);

    static byte[] Png()
    {
        using var ms = new MemoryStream();
        PngWriter.Write(ms, new SceneBuilder(32, 32).Build(), rgba: true);
        return ms.ToArray();
    }

    [Fact]
    public void 書き終わった_PNG_は終わり()
    {
        Assert.Equal(FileEnding.Complete, Of(Png()));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(0.99)]
    public void 途中で切れた_PNG_は途中(double ratio)
    {
        var png = Png();
        Assert.Equal(FileEnding.Incomplete, Of(png[..(int)(png.Length * ratio)]));
    }

    [Fact]
    public void 拡張子が違っても中身で見分ける()
    {
        var png = Png();
        Assert.Equal(FileEnding.Incomplete, Of(png[..(png.Length / 2)], ".jpg"));
    }

    [Fact]
    public void WebP_は頭に書かれた大きさで見分ける()
    {
        var webp = new byte[100];
        "RIFF"u8.CopyTo(webp);
        BitConverter.GetBytes(92u).CopyTo(webp, 4);
        "WEBP"u8.CopyTo(webp.AsSpan(8));
        Assert.Equal(FileEnding.Complete, Of(webp, ".webp"));
        Assert.Equal(FileEnding.Incomplete, Of(webp[..60], ".webp"));
    }

    [Fact]
    public void JPEG_は終わりの印があれば終わり_無ければ分からない()
    {
        var jpeg = new byte[64];
        jpeg[0] = 0xFF;
        jpeg[1] = 0xD8;
        jpeg[^2] = 0xFF;
        jpeg[^1] = 0xD9;
        Assert.Equal(FileEnding.Complete, Of(jpeg, ".jpg"));
        Assert.Equal(FileEnding.Unknown, Of(jpeg[..40], ".jpg"));
    }

    [Fact]
    public void 短すぎるファイルは途中()
    {
        Assert.Equal(FileEnding.Incomplete, Of(new byte[8]));
    }
}
