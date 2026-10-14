using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class ToastTextTests
{
    static QrDecision D(string text, QrAction action, bool limited = false) => new(QrClassifier.Classify(text), action, limited);

    [Fact]
    public void URL_は一行目にホスト名()
    {
        var t = ToastText.For(D("https://booth.pm/ja/items/1", QrAction.NotifyToOpen));
        Assert.Equal("booth.pm", t.Title);
        Assert.Equal("https://booth.pm/ja/items/1", t.Body);
        Assert.Equal("クリックで開きます", t.Footer);
    }

    [Fact]
    public void 偽のホストは本当のホストを一行目に出す()
    {
        Assert.Equal("evil.example", ToastText.For(D("https://booth.pm@evil.example/", QrAction.NotifyToOpen)).Title);
        Assert.StartsWith("xn--", ToastText.For(D("https://аpple.com/", QrAction.NotifyToOpen)).Title);
    }

    [Fact]
    public void 開いたとき_上限で開かなかったとき_文字列のときで一言が違う()
    {
        Assert.Equal("ブラウザで開きました", ToastText.For(D("https://a.example/", QrAction.Opened)).Footer);
        Assert.Contains("1分に3件", ToastText.For(D("https://a.example/", QrAction.NotifyToOpen, limited: true)).Footer);

        var text = ToastText.For(D("こんにちは", QrAction.NotifyToCopy));
        Assert.Equal("QR を読みました", text.Title);
        Assert.Equal("クリックでコピーします", text.Footer);
    }

    [Fact]
    public void 長い中身は切る()
    {
        var t = ToastText.For(D(new string('あ', 500), QrAction.NotifyToCopy));
        Assert.Equal(ToastText.BodyLimit, t.Body.Length);
        Assert.EndsWith("…", t.Body);
    }

    [Fact]
    public void メニューの行は改行をつぶし_アンパサンドをそのまま見せる()
    {
        Assert.Equal("a b c", ToastText.MenuLabel("a\r\nb\tc"));
        Assert.Equal("https://a.example/?x=1&&y=2", ToastText.MenuLabel("https://a.example/?x=1&y=2"));
        Assert.Equal(60, ToastText.MenuLabel(new string('x', 100)).Length);
    }
}

public class LogWriterTests
{
    [Fact]
    public void 日ごとのファイルに書き_七日より古いものを消す()
    {
        using var dir = new TempDir();
        var clock = new FakeClock();
        foreach (var daysAgo in new[] { 1, 7, 8, 30 })
        {
            File.WriteAllText(Path.Combine(dir.Path, $"{clock.Now.AddDays(-daysAgo):yyyy-MM-dd}.log"), "old");
        }

        File.WriteAllText(Path.Combine(dir.Path, "keep-me.txt"), "x");

        var log = new LogWriter(dir.Path, clock);
        log.Write("こんにちは");

        var names = Directory.GetFiles(dir.Path).Select(f => Path.GetFileName(f)!).Order().ToArray();
        Assert.Equal(
            [$"{clock.Now.AddDays(-7):yyyy-MM-dd}.log", $"{clock.Now.AddDays(-1):yyyy-MM-dd}.log", $"{clock.Now:yyyy-MM-dd}.log", "keep-me.txt"],
            names);
        Assert.Contains("こんにちは", File.ReadAllText(Path.Combine(dir.Path, $"{clock.Now:yyyy-MM-dd}.log")));
    }
}
