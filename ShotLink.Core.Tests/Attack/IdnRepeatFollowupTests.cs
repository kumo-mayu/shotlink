// 2026-10-18 に外部の AI（Codex）が2回目の攻撃の点検で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class IdnRepeatFollowupTests
{
    [Theory]
    [InlineData("https://bücher.example/", "https://xn--bcher-kva.example/")]
    [InlineData("https://example.com/", "https://ｅxample.com/")]
    [InlineData("https://example.com/", "https://example。com/")]
    public void 同じIDNホストとパスを二度直接開かない(string first, string second)
    {
        var a = QrClassifier.Classify(first);
        var b = QrClassifier.Classify(second);
        Assert.Equal(a.Host, b.Host);
        Assert.Equal(a.Url!.PathAndQuery, b.Url!.PathAndQuery);
        var dispatcher = QrDispatcher.CreateDefault(new FakeClock());
        var options = new DispatchOptions(true, true, AllowedSites: [a.Host!]);
        Assert.Equal(QrAction.Opened, dispatcher.Decide([first], options)[0].Action);
        var result = dispatcher.Decide([second], options)[0];
        Assert.True(result.OpenedRecently && result.Action == QrAction.NotifyToOpen,
            $"同じIDNホスト {a.Host}: {a.Url.AbsoluteUri} → {b.Url.AbsoluteUri}: {result.Action}");
    }

    [Theory]
    [InlineData("https://example.com/a/../b", "https://example.com/b")]
    [InlineData("https://example.com/%7e", "https://example.com/~")]
    [InlineData("https://example.com/a\\b", "https://example.com/a/b")]
    public void 絶対URLが同じ追加の正規化例も再開を防ぐ(string first, string second)
    {
        Assert.Equal(QrClassifier.Classify(first).Url!.AbsoluteUri, QrClassifier.Classify(second).Url!.AbsoluteUri);
        var dispatcher = QrDispatcher.CreateDefault(new FakeClock());
        dispatcher.Decide([first], new(true, true));
        Assert.True(dispatcher.Decide([second], new(true, true))[0].OpenedRecently);
    }
}
