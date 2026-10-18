// 2026-10-18 に外部の AI（Codex）が攻撃者の立場で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class ReopenAttackTests
{
    [Theory]
    [InlineData("https://example.com/", "HTTPS://EXAMPLE.COM/")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://example.com/", " https://example.com/ ")]
    [InlineData("https://example.com/", "https://example.com:443/")]
    [InlineData("https://example.com/A", "https://example.com/%41")]
    public void 同じ絶対URLの表記違いは60秒以内に再度開かない(string first, string second)
    {
        Assert.Equal(QrClassifier.Classify(first).Url!.AbsoluteUri, QrClassifier.Classify(second).Url!.AbsoluteUri);
        var dispatcher = QrDispatcher.CreateDefault(new FakeClock());
        Assert.Equal(QrAction.Opened, dispatcher.Decide([first], new(true, true))[0].Action);
        var d = dispatcher.Decide([second], new(true, true))[0];
        Assert.True(d.Action == QrAction.NotifyToOpen && d.OpenedRecently,
            $"表記違いが再度 {d.Action}: {second}");
    }

    [Fact]
    public void 三件ずつ二枚でも同じ絶対URLを重ねて開かない()
    {
        var dispatcher = QrDispatcher.CreateDefault(new FakeClock());
        string[][] photos = [["https://example.com/", "HTTPS://example.com/", "https://EXAMPLE.com/"],
            ["https://example.COM/", "https://example.com:443/", "https://example.com"]];
        var decisions = photos.SelectMany(p => dispatcher.Decide(p, new(true, true))).ToArray();
        Assert.True(decisions.Count(d => d.Action == QrAction.Opened) == 1,
            $"同一時刻・同一絶対URLで {decisions.Count(d => d.Action == QrAction.Opened)} 件が Opened");
    }

    [Fact]
    public void 四つの異なる原文と完全一致の重複は仕様どおり数える()
    {
        var d = QrDispatcher.CreateDefault(new FakeClock());
        var batch = d.Decide(["https://example.com/", "HTTPS://EXAMPLE.COM/", "https://example.com/#x", "https://example.com/?x", "https://example.com/"], new(true, true));
        Assert.Equal(4, batch.Count);
        Assert.True(batch.Summarized);
        Assert.DoesNotContain(batch, x => x.Action == QrAction.Opened);
    }

    [Fact]
    public void 三千件でも既定ではまとめて直接開かない()
    {
        var texts = Enumerable.Range(0, 3000).Select(i => $"https://example.com/{i}");
        var batch = QrDispatcher.CreateDefault(new FakeClock()).Decide(texts, new(true, true));
        Assert.Equal(3000, batch.Count);
        Assert.True(batch.Summarized);
        Assert.DoesNotContain(batch, d => d.Action == QrAction.Opened);
    }
}
