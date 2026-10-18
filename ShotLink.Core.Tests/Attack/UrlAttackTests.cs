// 2026-10-18 に外部の AI（Codex）が攻撃者の立場で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class UrlAttackTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("javascript：alert(1)")]
    [InlineData("file:///example.txt")]
    [InlineData("vrchat://example.com")]
    [InlineData(@"\\evil.example\share")]
    [InlineData(@"https://example.com\@evil.example/")]
    [InlineData("https:/evil.example")]
    [InlineData("\uFEFFhttps://evil.example/")]
    [InlineData("\u200Bhttps://evil.example/")]
    [InlineData("https://evil.example/a\rb")]
    [InlineData("https://evil.example/a\nb")]
    [InlineData("https://evil.example/a\tb")]
    [InlineData("https://evil.example/a\0b")]
    public void 非Web入力は開く判定にならない(string input)
    {
        var c = QrClassifier.Classify(input);
        Assert.Equal(QrKind.Text, c.Kind);
        Assert.Null(c.Url);
        Assert.Equal(QrAction.NotifyToCopy, QrDispatcher.CreateDefault(new FakeClock())
            .Decide([input], new(true, true))[0].Action);
    }

    [Theory]
    [InlineData(" HTTPS://EXAMPLE.COM/ ", "example.com")]
    [InlineData("https://good.example@evil.example/", "evil.example")]
    [InlineData(@"https:\\evil.example", "evil.example")]
    [InlineData("https://example.com:8080/", "example.com")]
    [InlineData("https://example.com./", "example.com.")]
    [InlineData("https://bücher.example/", "xn--bcher-kva.example")]
    [InlineData("https://example。com/", "example.com")]
    [InlineData("http://0xc0000201/", "192.0.2.1")]
    [InlineData("http://3221225985/", "192.0.2.1")]
    [InlineData("http://[2001:db8::1]/", "2001:db8::1")]
    public void 表示先は渡す絶対URLのホストと一致する(string input, string expected)
    {
        var c = QrClassifier.Classify(input);
        Assert.Equal(QrKind.WebUrl, c.Kind);
        Assert.Equal(expected, c.Host);
        Assert.Equal(c.Host, new Uri(c.Url!.AbsoluteUri).IdnHost);
        Assert.Equal(c.Host, ToastText.For(new(c, QrAction.NotifyToOpen)).Title);
    }

    [Fact]
    public void 特殊文字を含むホストの分類は例外を漏らさない()
    {
        int[] chars = [0, 9, 10, 13, 32, 0x7f, 0xad, 0x300, 0x34f, 0x61c, 0x200b,
            0x200c, 0x200d, 0x202a, 0x202e, 0x2066, 0x2069, 0xd800, 0xff0e, 0xff1a, 0xff20];
        var errors = new List<string>();
        foreach (var code in chars)
        {
            var input = $"https://a{(char)code}b.example/";
            var error = Record.Exception(() => QrClassifier.Classify(input));
            if (error is not null) errors.Add($"U+{code:X4}: {error.GetType().Name}");
        }
        Assert.True(errors.Count == 0, string.Join("; ", errors));
    }
}
