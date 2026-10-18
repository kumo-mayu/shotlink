// 2026-10-18 に外部の AI（Codex）が2回目の攻撃の点検で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class StoreFollowupTests
{
    [Theory]
    [InlineData("a\u037eb.example")]
    [InlineData("a\u1fefb.example")]
    public void 許可行の正規化を繰り返しても内容が消えない(string entry)
    {
        var first = new Settings { AllowedSites = [entry] }.Normalized();
        Assert.Equal(first.AllowedSites, first.Normalized().AllowedSites);
    }

    [Theory]
    [InlineData("{\"allowedSites\":[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]")] 
    [InlineData("{\"reopenSeconds\":1e9999}")]
    [InlineData("{\"allowedSites\":[\"\\uD800\"]}")]
    public void 追加の壊れたJSONを安全な設定に戻す(string json)
    {
        using var dir = new TempDir();
        var path = dir.Combine("settings.json");
        File.WriteAllText(path, json);
        var settings = new JsonFileStore<Settings>(path, () => new()).Load().Normalized();
        Assert.False(settings.OpenDirectly);
        Assert.Empty(settings.AllowedSites);
    }
}
