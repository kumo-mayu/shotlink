// 2026-10-18 に外部の AI（Codex）が攻撃者の立場で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class DisplayAttackTests
{
    [Theory]
    [InlineData("\u202E")]
    [InlineData("\u2066")]
    [InlineData("\u200B")]
    public void メニューに表示制御文字を残さない(string control)
    {
        var label = ToastText.MenuLabel("https://evil.example/" + control + "moc.elpmaxe");
        Assert.DoesNotContain(control, label, StringComparison.Ordinal);
    }

    [Fact]
    public void 通知本文とまとめ表示に方向制御文字を残さない()
    {
        var text = "\u202Eexample.com\nhttps://evil.example/";
        var d = QrDispatcher.CreateDefault(new FakeClock()).Decide([text], new(false, true));
        Assert.DoesNotContain("\u202E", ToastText.For(d[0]).Body + ToastText.ForBatch(d).Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ホスト先頭表示とアクセスキーと改行の防御を維持する()
    {
        Assert.StartsWith("evil.example｜", ToastText.MenuLabel("https://example.com@evil.example/?a&b"));
        Assert.Contains("&&", ToastText.MenuLabel("a&b\r\nc\td"));
        Assert.DoesNotContain("\n", ToastText.MenuLabel("a&b\r\nc\td"));
        var c = QrClassifier.Classify("https://e\u0301.example/");
        Assert.StartsWith("xn--", c.Host);
    }
}
