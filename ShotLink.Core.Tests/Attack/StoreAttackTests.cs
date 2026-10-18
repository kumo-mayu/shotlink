// 2026-10-18 に外部の AI（Codex）が攻撃者の立場で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class StoreAttackTests
{
    [Fact]
    public void 不正IDNの許可行でも設定の読み込みを落とさない()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, "{\"allowedSites\":[\"a\\u061cb.example\"]}");
        var error = Record.Exception(() => new JsonFileStore<Settings>(path, () => new()).Load().Normalized());
        Assert.True(error is null, $"設定の読み込みが例外: {error}");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"reopenSeconds\":999999999999999999999999}")]
    [InlineData("{\"openDirectly\":\"true\"}")]
    [InlineData("{\"allowedSites\":{},\"extraFolders\":false}")]
    [InlineData("{\"allowedSites\":[null,\"\"],\"extraFolders\":[null]}")]
    public void 壊れた設定は安全な既定に戻る(string json)
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, json);
        var s = new JsonFileStore<Settings>(path, () => new()).Load().Normalized();
        Assert.False(s.OpenDirectly);
        Assert.Empty(s.AllowedSites);
        Assert.Empty(s.ExtraFolders);
    }

    [Fact]
    public void 数値境界と重複キーを正規化する()
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, """{"reopenSeconds":-2147483648,"summarizeThreshold":2147483647,"openDirectly":true,"openDirectly":false}""");
        var s = new JsonFileStore<Settings>(path, () => new()).Load().Normalized();
        Assert.Equal(1, s.ReopenSeconds);
        Assert.Equal(99, s.SummarizeThreshold);
        Assert.False(s.OpenDirectly);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null,{}, {\"text\":null}]")]
    [InlineData("[{\"text\":123}]")]
    [InlineData("[{\"text\":\"x\",\"readAt\":999999999999999999}]")]
    public void 壊れた履歴は例外なく読み飛ばす(string json)
    {
        using var dir = new TempDir();
        var path = dir.Combine("history.json");
        File.WriteAllText(path, json);
        Assert.Empty(new History(path).Recent(1000));
    }

    [Fact]
    public void 大きい履歴も保持件数は百件に制限する()
    {
        using var dir = new TempDir();
        var path = dir.Combine("history.json");
        var entry = "{\"text\":\"" + new string('x', 4096) + "\"}";
        File.WriteAllText(path, "[" + string.Join(',', Enumerable.Repeat(entry, 2048)) + "]");
        Assert.Equal(100, new History(path).Recent(10000).Count);
    }
}
