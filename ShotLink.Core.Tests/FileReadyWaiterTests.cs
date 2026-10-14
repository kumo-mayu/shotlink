using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class FileReadyWaiterTests
{
    /// <summary>呼ばれるたびに、並べた大きさを順に返す（null は開けない）</summary>
    sealed class ScriptedProbe(params long?[] lengths) : IFileProbe
    {
        int _i;

        public int Calls => _i;

        public long? TryGetReadableLength(string path) => lengths[Math.Min(_i++, lengths.Length - 1)];
    }

    static readonly Func<TimeSpan, CancellationToken, Task> NoWait = (_, _) => Task.CompletedTask;

    [Fact]
    public async Task 開けて大きさが二回続けて同じなら読んでよい()
    {
        var probe = new ScriptedProbe(1000, 1000);
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(2, probe.Calls);
    }

    [Fact]
    public async Task 書き込み中で開けない間は待つ()
    {
        var probe = new ScriptedProbe(null, null, null, 5000, 5000);
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(5, probe.Calls);
    }

    [Fact]
    public async Task 大きくなり続けている間は待つ()
    {
        var probe = new ScriptedProbe(100, 2000, 4000, 4000);
        Assert.True(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
        Assert.Equal(4, probe.Calls);
    }

    [Fact]
    public async Task 大きさ_0_のままなら読まない()
    {
        var probe = new ScriptedProbe(0);
        Assert.False(await new FileReadyWaiter(probe, NoWait).WaitAsync("x.png"));
    }

    [Fact]
    public async Task 十秒たっても開けなければあきらめる()
    {
        var probe = new ScriptedProbe([null]);
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
        await File.WriteAllBytesAsync(path, new byte[100]);
        Assert.True(await FileReadyWaiter.CreateDefault().WaitAsync(path));
    }

    [Fact]
    public async Task 書き込み中に掴まれているファイルは開けないと分かる()
    {
        using var dir = new TempDir();
        var path = dir.Combine("a.png");
        await using var writing = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        writing.Write(new byte[100]);
        Assert.Null(new FileProbe().TryGetReadableLength(path));
    }
}
