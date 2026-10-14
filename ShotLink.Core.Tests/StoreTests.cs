using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class ShotLinkHomeTests
{
    [Fact]
    public void 指定があればそこ()
    {
        using var dir = new TempDir();
        Assert.Equal(dir.Path, ShotLinkHome.Resolve(dir.Path, allowProduction: false));
    }

    [Fact]
    public void 指定が無く本物の起動でもなければ止める()
    {
        Assert.Throws<InvalidOperationException>(() => ShotLinkHome.Resolve(null, allowProduction: false));
        Assert.Throws<InvalidOperationException>(() => ShotLinkHome.Resolve("  ", allowProduction: false));
    }
}

public class VrchatPhotoFolderTests
{
    [Fact]
    public void 設定ファイルが無ければピクチャの下の_VRChat()
    {
        using var dir = new TempDir();
        Assert.Equal(Path.Combine(dir.Path, "Pictures", "VRChat"),
            VrchatPhotoFolder.Resolve(dir.Combine("config.json"), dir.Combine("Pictures")));
    }

    [Fact]
    public void 設定ファイルに保存先があればそこ()
    {
        using var dir = new TempDir();
        var config = dir.Combine("config.json");
        File.WriteAllText(config, """{ "picture_output_folder": "D:\\写真\\VRC", "picture_output_split_by_date": false }""");
        Assert.Equal(@"D:\写真\VRC", VrchatPhotoFolder.Resolve(config, dir.Combine("Pictures")));
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("""{ "picture_output_folder": "" }""")]
    [InlineData("""{ "picture_output_folder": null }""")]
    [InlineData("""{ "picture_output_folder": 3 }""")]
    [InlineData("""{ "other": 1 }""")]
    [InlineData("[]")]
    [InlineData("")]
    public void 設定ファイルが壊れている_空のときは既定(string json)
    {
        using var dir = new TempDir();
        var config = dir.Combine("config.json");
        File.WriteAllText(config, json);
        Assert.Equal(Path.Combine(dir.Path, "Pictures", "VRChat"), VrchatPhotoFolder.Resolve(config, dir.Combine("Pictures")));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void 無ければ既定_直接開くは_OFF()
    {
        using var dir = new TempDir();
        var s = new JsonFileStore<Settings>(dir.Combine("settings.json"), () => new Settings()).Load();
        Assert.False(s.OpenDirectly);
        Assert.True(s.NotifyNonUrl);
        Assert.True(s.WatchVrchat);
        Assert.Empty(s.ExtraFolders);
    }

    [Fact]
    public void 書いて読み直すと同じ_日本語はそのまま読める()
    {
        using var dir = new TempDir();
        var store = new JsonFileStore<Settings>(dir.Combine("settings.json"), () => new Settings());
        var s = new Settings { OpenDirectly = true, ExtraFolders = [new(@"D:\スクショ", true)] };
        store.Save(s);

        Assert.Contains(@"D:\\スクショ", File.ReadAllText(store.Path));
        var back = store.Load();
        Assert.True(back.OpenDirectly);
        Assert.Equal(s.ExtraFolders, back.ExtraFolders);
    }

    [Fact]
    public void 手で直した_null_の一覧は空として受ける()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, """{ "openDirectly": true, "extraFolders": null }""");
        var s = new JsonFileStore<Settings>(path, () => new Settings()).Load().Normalized();
        Assert.True(s.OpenDirectly);
        Assert.Empty(s.ExtraFolders);
    }

    [Fact]
    public void 空の行は捨てる()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, """{ "extraFolders": [ null, { "path": "" }, { "path": "D:\\a" } ] }""");
        var s = new JsonFileStore<Settings>(path, () => new Settings()).Load().Normalized();
        Assert.Equal([new ExtraFolder(@"D:\a")], s.ExtraFolders);
    }

    [Fact]
    public void 壊れていれば残して既定で始める()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, "{ broken");
        var s = new JsonFileStore<Settings>(path, () => new Settings()).Load();
        Assert.False(s.OpenDirectly);
        Assert.Single(Directory.GetFiles(dir.Path, "settings.json.broken-*"));
    }
}

public class HistoryTests
{
    static HistoryEntry Entry(string text, int minute) =>
        new(text, new DateTimeOffset(2026, 10, 14, 12, minute, 0, TimeSpan.FromHours(9)), $"VRChat_{minute}.png");

    [Fact]
    public void 新しい順に並び_読み直しても同じ()
    {
        using var dir = new TempDir();
        var path = dir.Combine("history.json");
        var h = new History(path);
        h.Add(Entry("a", 1));
        h.Add(Entry("b", 2));
        Assert.Equal(["b", "a"], new History(path).Recent(10).Select(e => e.Text));
    }

    [Fact]
    public void 同じ中身は一件にまとめて先頭へ()
    {
        using var dir = new TempDir();
        var h = new History(dir.Combine("history.json"));
        h.Add(Entry("a", 1));
        h.Add(Entry("b", 2));
        h.Add(Entry("a", 3));
        Assert.Equal(["a", "b"], h.Recent(10).Select(e => e.Text));
        Assert.Equal(3, h.Recent(1)[0].ReadAt.Minute);
    }

    [Fact]
    public void 消すと空になり_読み直しても空()
    {
        using var dir = new TempDir();
        var path = dir.Combine("history.json");
        var h = new History(path);
        h.Add(Entry("a", 1));
        h.Clear();
        Assert.Empty(h.Recent(10));
        Assert.Empty(new History(path).Recent(10));
    }

    [Fact]
    public void 残すのは百件まで()
    {
        using var dir = new TempDir();
        var path = dir.Combine("history.json");
        var h = new History(path);
        for (var i = 0; i < History.Capacity + 5; i++)
        {
            h.Add(Entry($"t{i}", i % 60));
        }

        var back = new History(path).Recent(1000);
        Assert.Equal(History.Capacity, back.Count);
        Assert.Equal($"t{History.Capacity + 4}", back[0].Text);
    }
}
