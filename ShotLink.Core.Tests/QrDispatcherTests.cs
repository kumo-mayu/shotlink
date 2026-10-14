using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class QrDispatcherTests
{
    static readonly DispatchOptions Open = new(OpenDirectly: true, NotifyNonUrl: true);
    static readonly DispatchOptions NoOpen = new(OpenDirectly: false, NotifyNonUrl: true);

    readonly FakeClock _clock = new();
    readonly QrDispatcher _dispatcher;

    public QrDispatcherTests() => _dispatcher = QrDispatcher.CreateDefault(_clock);

    [Fact]
    public void 直接開くが_OFF_なら_URL_は通知だけ()
    {
        var d = Assert.Single(_dispatcher.Decide(["https://a.example/"], NoOpen));
        Assert.Equal(QrAction.NotifyToOpen, d.Action);
        Assert.False(d.RateLimited);
    }

    [Fact]
    public void 直接開くが_ON_なら_URL_を開く()
    {
        var d = Assert.Single(_dispatcher.Decide(["https://a.example/"], Open));
        Assert.Equal(QrAction.Opened, d.Action);
    }

    [Fact]
    public void 直接開くが_ON_でも_URL_でなければ開かない()
    {
        var d = Assert.Single(_dispatcher.Decide(["javascript:alert(1)"], Open));
        Assert.Equal(QrAction.NotifyToCopy, d.Action);
    }

    [Fact]
    public void URL_以外を知らせない設定なら履歴だけ()
    {
        var d = Assert.Single(_dispatcher.Decide(["hello"], Open with { NotifyNonUrl = false }));
        Assert.Equal(QrAction.HistoryOnly, d.Action);
    }

    [Fact]
    public void URL_以外を知らせない設定でも_URL_は知らせる()
    {
        var d = Assert.Single(_dispatcher.Decide(["https://a.example/"], NoOpen with { NotifyNonUrl = false }));
        Assert.Equal(QrAction.NotifyToOpen, d.Action);
    }

    [Fact]
    public void 一枚に同じ中身が複数あっても一回だけ()
    {
        var ds = _dispatcher.Decide(["https://a.example/", "https://a.example/", "b"], Open);
        Assert.Equal(["https://a.example/", "b"], ds.Select(d => d.Content.Text));
    }

    [Fact]
    public void 同じ中身は_60_秒のあいだ二回目以降を扱わない()
    {
        Assert.Single(_dispatcher.Decide(["x"], Open));
        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Empty(_dispatcher.Decide(["x"], Open));
    }

    [Fact]
    public void 最後に見てから_60_秒たてばまた扱う()
    {
        Assert.Single(_dispatcher.Decide(["x"], Open));
        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Single(_dispatcher.Decide(["x"], Open));
    }

    [Fact]
    public void 撮り続けているあいだは黙ったまま()
    {
        Assert.Single(_dispatcher.Decide(["x"], Open));
        for (var i = 0; i < 10; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(30));
            Assert.Empty(_dispatcher.Decide(["x"], Open));
        }
    }

    [Fact]
    public void 違う中身は除外されない()
    {
        Assert.Single(_dispatcher.Decide(["x"], Open));
        Assert.Single(_dispatcher.Decide(["y"], Open));
    }

    [Fact]
    public void 直接開くのは一分に三件まで_超えた分は通知だけ()
    {
        var urls = Enumerable.Range(1, 5).Select(i => $"https://a.example/{i}").ToArray();
        var ds = _dispatcher.Decide(urls, Open);

        Assert.Equal(
            [QrAction.Opened, QrAction.Opened, QrAction.Opened, QrAction.NotifyToOpen, QrAction.NotifyToOpen],
            ds.Select(d => d.Action));
        Assert.Equal([false, false, false, true, true], ds.Select(d => d.RateLimited));
    }

    [Fact]
    public void 一分たてばまた開ける()
    {
        _dispatcher.Decide(["https://a.example/1", "https://a.example/2", "https://a.example/3"], Open);
        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(QrAction.NotifyToOpen, Assert.Single(_dispatcher.Decide(["https://a.example/4"], Open)).Action);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(QrAction.Opened, Assert.Single(_dispatcher.Decide(["https://a.example/5"], Open)).Action);
    }

    [Fact]
    public void 直接開くが_OFF_のあいだは上限を使わない()
    {
        _dispatcher.Decide(["https://a.example/1", "https://a.example/2", "https://a.example/3"], NoOpen);
        var ds = _dispatcher.Decide(["https://a.example/4", "https://a.example/5", "https://a.example/6"], Open);
        Assert.All(ds, d => Assert.Equal(QrAction.Opened, d.Action));
    }
}
