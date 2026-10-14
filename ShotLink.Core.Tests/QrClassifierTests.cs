using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class QrClassifierTests
{
    [Theory]
    [InlineData("https://booth.pm/ja/items/1", "booth.pm")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("HTTPS://EXAMPLE.COM/a", "example.com")]
    [InlineData("  https://example.com/  ", "example.com")]
    [InlineData("https://example.com:8443/x?y=1#z", "example.com")]
    public void http_と_https_は_URL(string text, string host)
    {
        var c = QrClassifier.Classify(text);
        Assert.Equal(QrKind.WebUrl, c.Kind);
        Assert.Equal(host, c.Host);
        Assert.Equal(text, c.Text);
    }

    // 仕様書 4章：http / https 以外は絶対に開かない
    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    [InlineData("ftp://example.com/")]
    [InlineData("mailto:a@example.com")]
    [InlineData("vrchat://launch?id=wrld_x")]
    [InlineData("example.com")]
    [InlineData("www.example.com/path")]
    [InlineData("こんにちは")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    [InlineData("https:example.com")]
    [InlineData("\\\\server\\share")]
    public void それ以外は文字列(string text)
    {
        var c = QrClassifier.Classify(text);
        Assert.Equal(QrKind.Text, c.Kind);
        Assert.Null(c.Url);
        Assert.Null(c.Host);
    }

    // Uri は一部の空白・制御文字を黙って取り除くので、見せた文字列と開く先が食い違わないよう URL にしない
    [Theory]
    [InlineData("https://example.com/\nfile:///C:/x")]
    [InlineData("https://exa mple.com/")]
    [InlineData("https://example.com/\tx")]
    [InlineData("https://exa\u0000mple.com/")]
    [InlineData("https://example.com/a b")]
    public void 途中に空白や制御文字があれば文字列(string text)
    {
        Assert.Equal(QrKind.Text, QrClassifier.Classify(text).Kind);
    }

    [Fact]
    public void ユーザー情報で偽ったときは本当のホストを見せる()
    {
        var c = QrClassifier.Classify("https://booth.pm@evil.example/");
        Assert.Equal(QrKind.WebUrl, c.Kind);
        Assert.Equal("evil.example", c.Host);
    }

    // 仕様書 4章：国際化ドメインは xn-- のまま見せ、見た目の似た偽のドメインに気づけるようにする
    [Fact]
    public void 国際化ドメインは_xn_のまま見せる()
    {
        var c = QrClassifier.Classify("https://例え.jp/");
        Assert.Equal(QrKind.WebUrl, c.Kind);
        Assert.StartsWith("xn--", c.Host);
    }

    [Fact]
    public void キリル文字の偽ドメインも_xn_になる()
    {
        // а はキリル文字（U+0430）。見た目は apple.com
        var c = QrClassifier.Classify("https://\u0430pple.com/");
        Assert.StartsWith("xn--", c.Host);
    }
}
