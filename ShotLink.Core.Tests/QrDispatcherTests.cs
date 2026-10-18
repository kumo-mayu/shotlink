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

    // 仕様書 3.4（2026-10-14 に変えた）：撮った QR は毎回知らせ、同じ URL を直接開くのは最後に開いてから 60 秒に1回
    [Fact]
    public void 同じ_URL_は毎回知らせ_開くのは_60_秒に一回()
    {
        const string url = "https://a.example/";
        Assert.Equal(QrAction.Opened, Assert.Single(_dispatcher.Decide([url], Open)).Action);

        _clock.Advance(TimeSpan.FromSeconds(59));
        var again = Assert.Single(_dispatcher.Decide([url], Open));
        Assert.Equal(QrAction.NotifyToOpen, again.Action);
        Assert.True(again.OpenedRecently);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(QrAction.Opened, Assert.Single(_dispatcher.Decide([url], Open)).Action);
    }

    [Fact]
    public void 最後に見てから_60_秒たてばまた扱う()
    {
        Assert.Single(_dispatcher.Decide(["x"], Open));
        _clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Single(_dispatcher.Decide(["x"], Open));
    }

    // 2026-10-14 の実機：最後に見た時刻から数えていたので、撮り続けると黙ったままになった
    [Fact]
    public void 撮り続けても毎回知らせ_最初に開いてから_60_秒たてばまた開く()
    {
        const string url = "https://a.example/";
        var actions = new List<QrAction>();
        for (var i = 0; i < 4; i++)
        {
            actions.Add(Assert.Single(_dispatcher.Decide([url], Open)).Action);
            _clock.Advance(TimeSpan.FromSeconds(20));
        }

        Assert.Equal([QrAction.Opened, QrAction.NotifyToOpen, QrAction.NotifyToOpen, QrAction.Opened], actions);
    }

    [Fact]
    public void 設定でいつも開くにすると_同じ_URL_も毎回開く()
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(QrAction.Opened, Assert.Single(_dispatcher.Decide(["https://a.example/"], Open with { ReopenSameUrl = true })).Action);
        }
    }

    [Fact]
    public void 違う中身は除外されない()
    {
        Assert.Single(_dispatcher.Decide(["x"], Open));
        Assert.Single(_dispatcher.Decide(["y"], Open));
    }

    [Fact]
    public void 直接開くに回数の上限は無い()
    {
        // 上限は 2026-10-14 に外した（ユーザー判断。QrPolicy の説明）。続けて撮った写真の URL はどれも開く
        for (var i = 0; i < 10; i++)
        {
            var d = Assert.Single(_dispatcher.Decide([$"https://a.example/{i}"], Open));
            Assert.Equal(QrAction.Opened, d.Action);
        }
    }

}

// 仕様書 3.5：1枚に 4 件以上あれば通知を1件にまとめる
public class QrDispatcherSummarizeTests
{
    static readonly DispatchOptions Open = new(OpenDirectly: true, NotifyNonUrl: true);

    readonly FakeClock _clock = new();
    readonly QrDispatcher _dispatcher;

    public QrDispatcherSummarizeTests() => _dispatcher = QrDispatcher.CreateDefault(_clock);

    static string[] Urls(int n, string prefix = "u") => Enumerable.Range(1, n).Select(i => $"https://a.example/{prefix}{i}").ToArray();

    [Fact]
    public void 三件まではまとめない()
    {
        Assert.False(_dispatcher.Decide(Urls(3), Open).Summarized);
    }

    [Fact]
    public void 四件からまとめ_既定では直接開かない()
    {
        var batch = _dispatcher.Decide(Urls(4), Open);
        Assert.True(batch.Summarized);
        Assert.All(batch, d => Assert.Equal(QrAction.NotifyToOpen, d.Action));
    }

    [Fact]
    public void まとめたときも開く設定なら全部開く()
    {
        var batch = _dispatcher.Decide(Urls(5), Open with { OpenWhenSummarized = true });
        Assert.True(batch.Summarized);
        Assert.All(batch, d => Assert.Equal(QrAction.Opened, d.Action));
    }

    [Fact]
    public void 数えるのは写真に写っている件数()
    {
        // 少し前に見た物も除かない（毎回知らせるので、まとめるかも写っている数で決める）
        _dispatcher.Decide(Urls(3), Open);
        var batch = _dispatcher.Decide([.. Urls(3), "https://a.example/new"], Open);
        Assert.True(batch.Summarized);
        Assert.Equal(4, batch.Count);
    }

    [Fact]
    public void 文字列も件数に入る()
    {
        Assert.True(_dispatcher.Decide(["a", "b", "c", "https://a.example/"], Open).Summarized);
    }

    [Fact]
    public void 渡した順を保つ()
    {
        var batch = _dispatcher.Decide(["z", "https://a.example/", "a", "m"], Open);
        Assert.Equal(["z", "https://a.example/", "a", "m"], batch.Select(d => d.Content.Text));
    }
}

// 2026-10-17：秒数・まとめる件数・開くサイトの一覧を設定で変えられるようにした
public class QrDispatcherSettingsTests
{
    static readonly DispatchOptions Open = new(OpenDirectly: true, NotifyNonUrl: true);

    readonly FakeClock _clock = new();
    readonly QrDispatcher _dispatcher;

    public QrDispatcherSettingsTests() => _dispatcher = QrDispatcher.CreateDefault(_clock);

    static string[] Urls(int n) => Enumerable.Range(1, n).Select(i => $"https://a.example/u{i}").ToArray();

    [Fact]
    public void 秒数を変えると_その秒数でまた開く()
    {
        var options = Open with { ReopenSeconds = 10 };
        Assert.Equal(QrAction.Opened, Assert.Single(_dispatcher.Decide(["https://a.example/"], options)).Action);
        _clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(QrAction.NotifyToOpen, Assert.Single(_dispatcher.Decide(["https://a.example/"], options)).Action);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(QrAction.Opened, Assert.Single(_dispatcher.Decide(["https://a.example/"], options)).Action);
    }

    [Fact]
    public void まとめないにすると_何件あっても_1件ずつ全部開く()
    {
        var batch = _dispatcher.Decide(Urls(10), Open with { SummarizeThreshold = null });
        Assert.False(batch.Summarized);
        Assert.All(batch, d => Assert.Equal(QrAction.Opened, d.Action));
    }

    [Fact]
    public void まとめる件数を_2_にすると_2件でまとめる()
    {
        Assert.True(_dispatcher.Decide(Urls(2), Open with { SummarizeThreshold = 2 }).Summarized);
        Assert.False(_dispatcher.Decide(["https://b.example/"], Open with { SummarizeThreshold = 2 }).Summarized);
    }

    [Theory]
    [InlineData("https://example.com/a", true)]
    [InlineData("https://www.example.com/a", true)]
    [InlineData("https://WWW.Example.COM/a", true)]
    [InlineData("https://example.com.evil.example/a", false)]
    [InlineData("https://badexample.com/a", false)]
    [InlineData("https://other.example/a", false)]
    public void 一覧に絞ると_一覧のサイトと下位のドメインだけ開く(string url, bool opened)
    {
        var d = Assert.Single(_dispatcher.Decide([url], Open with { AllowedSites = ["example.com"] }));
        Assert.Equal(opened ? QrAction.Opened : QrAction.NotifyToOpen, d.Action);
        Assert.Equal(!opened, d.NotAllowed);
    }

    [Fact]
    public void 国際化ドメインは_xn_の形で比べる()
    {
        var site = SiteAllowlist.Normalize("例え.jp")!;
        Assert.StartsWith("xn--", site);
        var d = Assert.Single(_dispatcher.Decide(["https://www.例え.jp/"], Open with { AllowedSites = [site] }));
        Assert.Equal(QrAction.Opened, d.Action);
    }

    [Fact]
    public void 一覧に無くて開かなかった物は_開いたと控えない()
    {
        _dispatcher.Decide(["https://a.example/"], Open with { AllowedSites = [] });
        var d = Assert.Single(_dispatcher.Decide(["https://a.example/"], Open with { AllowedSites = ["a.example"] }));
        Assert.Equal(QrAction.Opened, d.Action);
    }

    [Fact]
    public void 一覧が空なら_何も開かない()
    {
        Assert.True(Assert.Single(_dispatcher.Decide(["https://a.example/"], Open with { AllowedSites = [] })).NotAllowed);
    }
}

public class SiteAllowlistTests
{
    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("  Example.COM.  ", "example.com")]
    [InlineData("https://www.example.com/path?q=1", "www.example.com")]
    [InlineData("*.example.com", "example.com")]
    [InlineData("example.com/path", "example.com")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("a b", null)]
    [InlineData("a\u061Cb.example", null)]
    [InlineData("a\u200Cb.example", null)]
    public void 書いた行をホスト名にそろえる(string entry, string? expected)
    {
        Assert.Equal(expected, SiteAllowlist.Normalize(entry));
    }
}
