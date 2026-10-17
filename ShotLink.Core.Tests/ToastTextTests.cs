using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class ToastTextTests
{
    static QrDecision D(string text, QrAction action) => new(QrClassifier.Classify(text), action);

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
    public void 開いたとき_開かなかったとき_文字列のときで一言が違う()
    {
        Assert.Equal("ブラウザで開きました", ToastText.For(D("https://a.example/", QrAction.Opened)).Footer);
        Assert.Equal("クリックで開きます", ToastText.For(D("https://a.example/", QrAction.NotifyToOpen)).Footer);
        Assert.Contains("少し前に開いた", ToastText.For(new QrDecision(QrClassifier.Classify("https://a.example/"), QrAction.NotifyToOpen, OpenedRecently: true)).Footer);
        Assert.Contains("ホワイトリストに無い", ToastText.For(new QrDecision(QrClassifier.Classify("https://a.example/"), QrAction.NotifyToOpen, NotAllowed: true)).Footer);

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
    public void メニューの行は_URL_ならホスト名を先に出す()
    {
        // Codex の点検 2026-10-14：原文だけだと、偽のドメインや、ユーザー情報の後ろの本当のホストが見えない
        Assert.StartsWith("xn--", ToastText.MenuLabel("https://аpple.com/"));
        var label = ToastText.MenuLabel("https://" + new string('a', 80) + "@evil.example/");
        Assert.StartsWith("evil.example｜", label);
        Assert.True(label.Length <= 60);
    }

    [Fact]
    public void メニューの行は改行をつぶし_アンパサンドをそのまま見せる()
    {
        Assert.Equal("a b c", ToastText.MenuLabel("a\r\nb\tc"));
        Assert.Equal("a.example｜https://a.example/?x=1&&y=2", ToastText.MenuLabel("https://a.example/?x=1&y=2"));
        Assert.Equal(60, ToastText.MenuLabel(new string('x', 100)).Length);
    }
}

public class LogWriterTests
{
    [Fact]
    public void 書けないログでも投げない()
    {
        using var dir = new TempDir();
        var clock = new FakeClock();
        var log = new LogWriter(dir.Path, clock);
        var file = Path.Combine(dir.Path, $"{clock.Now:yyyy-MM-dd}.log");
        File.WriteAllText(file, "");
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            log.Write("x");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

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

public class ToastTextBatchTests
{
    [Fact]
    public void まとめた通知は件数と頭の三件()
    {
        var dispatcher = QrDispatcher.CreateDefault(new FakeClock());
        var batch = dispatcher.Decide(["https://a.example/", "こんにちは\nせかい", "https://b.example/x", "https://c.example/", "https://d.example/"],
            new DispatchOptions(OpenDirectly: false, NotifyNonUrl: true));
        var t = ToastText.ForBatch(batch);
        Assert.Equal("QR を 5 件読みました", t.Title);
        Assert.Equal("a.example、こんにちは せかい、b.example、ほか 2 件", t.Body);
        Assert.Equal("クリックで一覧を開きます", t.Footer);
    }

    [Fact]
    public void 開いた件数を書く()
    {
        var dispatcher = QrDispatcher.CreateDefault(new FakeClock());
        var batch = dispatcher.Decide(Enumerable.Range(1, 6).Select(i => $"https://a.example/{i}"),
            new DispatchOptions(OpenDirectly: true, NotifyNonUrl: true, OpenWhenSummarized: true));
        Assert.Equal("6 件をブラウザで開きました。クリックで一覧を開きます", ToastText.ForBatch(batch).Footer);
    }
}
