// 2026-10-18 に外部の AI（Codex）が2回目の攻撃の点検で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class AllowlistFollowupTests
{
    [Theory]
    [InlineData("https://example.com%2eevil.example/")]
    [InlineData("https://example.com%00.evil.example/")]
    [InlineData("https://example.com%2f@evil.example/")]
    [InlineData("https://example.com。evil.example/")]
    [InlineData("https://example.com．evil.example/")]
    [InlineData("https://user%40example.com@evil.example/")]
    [InlineData("https://example.com:443@evil.example/")]
    [InlineData("https://evil.example/#https://example.com/")]
    public void 区切り文字の偽装で許可範囲を広げない(string text)
    {
        var settings = new Settings { OpenDirectly = true, OpenOnlyAllowedSites = true, AllowedSites = ["example.com"] }.Normalized();
        var decision = QrDispatcher.CreateDefault(new FakeClock()).Decide([text], settings.ToDispatchOptions())[0];
        Assert.NotEqual(QrAction.Opened, decision.Action);
    }

    [Theory]
    [InlineData("example.com:8080", "https://example.com:9090/")]
    [InlineData("user@example.com", "https://example.com/")]
    [InlineData("*.example.com", "https://a.b.example.com/")]
    [InlineData("example。com", "https://example.com./")]
    public void 人の入力とQRは仕様のホスト単位で一致する(string entry, string text)
    {
        var normalized = SiteAllowlist.Normalize(entry)!;
        Assert.Equal(normalized, SiteAllowlist.Normalize(normalized));
        Assert.True(SiteAllowlist.Contains([normalized], QrClassifier.Classify(text).Host!));
        Assert.False(SiteAllowlist.Contains([normalized], "example.com.evil.example"));
    }

    [Fact]
    public void 非ASCIIの全BMP文字をホストに挟んでも分類と設定正規化は例外を漏らさない()
    {
        for (var code = 128; code <= 0xffff; code++)
        {
            var text = $"https://a{(char)code}b.example/";
            var error = Record.Exception(() =>
            {
                var c = QrClassifier.Classify(text);
                if (c.Kind == QrKind.WebUrl)
                {
                    _ = c.Url!.AbsoluteUri;
                    _ = ToastText.For(new(c, QrAction.NotifyToOpen));
                }
                _ = SiteAllowlist.Normalize(text);
            });
            Assert.True(error is null, $"U+{code:X4}: {error}");
        }
    }

    [Fact]
    public void 分類した絶対URLを読み直してもホストを維持する()
    {
        var failures = new List<string>();
        for (var code = 128; code <= 0xffff; code++)
        {
            var c = QrClassifier.Classify($"https://a{(char)code}b.example/");
            if (c.Kind != QrKind.WebUrl) continue;
            var again = QrClassifier.Classify(c.Url!.AbsoluteUri);
            if (again.Kind != QrKind.WebUrl || c.Host != again.Host)
                failures.Add($"U+{code:X4}: {c.Host} → {c.Url.AbsoluteUri} → {again.Kind}/{again.Host}");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
