// 2026-10-18 に外部の AI（Codex）が2回目の攻撃の点検で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class InvisibleFollowupTests
{
    [Theory]
    [InlineData("\u00ad")]
    [InlineData("\u034f")]
    [InlineData("\u180e")]
    [InlineData("\u206a")]
    [InlineData("\U000e0061")]
    public void 追加の不可視文字を全表示経路から除去する(string invisible)
    {
        var text = "ex" + invisible + "ample.com";
        var batch = QrDispatcher.CreateDefault(new FakeClock()).Decide([text], new(false, true));
        var outputs = new[] { ToastText.For(batch[0]).Body, ToastText.ForBatch(batch).Body, ToastText.MenuLabel(text) };
        Assert.All(outputs, output => Assert.DoesNotContain(invisible, output, StringComparison.Ordinal));
        Assert.Equal(text, batch[0].Content.Text);
    }

    [Fact]
    public void 結合文字とアクセスキーでもホストの先頭を保つ()
    {
        const string input = "https://e\u0301.example/a&b";
        var content = QrClassifier.Classify(input);
        Assert.StartsWith("xn--", content.Host);
        Assert.StartsWith(content.Host + "｜", ToastText.MenuLabel(input));
        Assert.Contains("&&", ToastText.MenuLabel(input), StringComparison.Ordinal);
        Assert.Equal(content.Host, ToastText.For(new(content, QrAction.NotifyToOpen)).Title);
    }
}
