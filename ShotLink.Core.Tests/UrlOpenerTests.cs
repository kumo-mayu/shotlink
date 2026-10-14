using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class UrlOpenerTests
{
    readonly List<string> _launched = [];
    readonly UrlOpener _opener;

    public UrlOpenerTests() => _opener = new UrlOpener(_launched.Add);

    [Fact]
    public void http_と_https_だけ開く()
    {
        Assert.True(_opener.TryOpen(QrClassifier.Classify("https://booth.pm/ja/items/1")));
        Assert.Equal(["https://booth.pm/ja/items/1"], _launched);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    [InlineData("calc.exe")]
    public void それ以外は開かない(string text)
    {
        Assert.False(_opener.TryOpen(QrClassifier.Classify(text)));
        Assert.Empty(_launched);
    }

    // 振り分けを通らずに作られた QrContent でも、開く直前に確かめ直す（仕様書 4章）
    [Fact]
    public void URL_と偽って作られた中身は開かない()
    {
        var forged = new QrContent("file:///C:/x", QrKind.WebUrl, new Uri("file:///C:/x"), "");
        Assert.False(_opener.TryOpen(forged));

        var mismatched = new QrContent("https://good.example/", QrKind.WebUrl, new Uri("https://evil.example/"), "good.example");
        Assert.False(_opener.TryOpen(mismatched));

        Assert.Empty(_launched);
    }
}
