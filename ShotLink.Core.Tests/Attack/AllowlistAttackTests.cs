// 2026-10-18 に外部の AI（Codex）が攻撃者の立場で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using ShotLink.Core;

namespace ShotLink.Core.Tests.Attack;

public class AllowlistAttackTests
{
    [Theory]
    [InlineData("https://example.com.evil.example/")]
    [InlineData("https://evil-example.example/")]
    [InlineData("https://example.com@evil.example/")]
    [InlineData("https://example.com:8080@evil.example/")]
    [InlineData("https://evil.example/?next=https://example.com")]
    [InlineData("https://exаmple.example/")]
    public void 許可ホストの見せかけで直接開かない(string input)
    {
        var s = new Settings { OpenDirectly = true, OpenOnlyAllowedSites = true, AllowedSites = ["example.com"] }.Normalized();
        var d = QrDispatcher.CreateDefault(new FakeClock()).Decide([input], s.ToDispatchOptions())[0];
        Assert.NotEqual(QrAction.Opened, d.Action);
        Assert.True(d.NotAllowed);
    }

    [Theory]
    [InlineData("*.EXAMPLE.COM.", "https://sub.example.com/")]
    [InlineData("https://example.com:8080/path", "https://example.com:9090/")]
    [InlineData("example.com", "https://EXAMPLE.COM./")]
    [InlineData("bücher.example", "https://xn--bcher-kva.example/")]
    [InlineData("xn--bcher-kva.example", "https://bücher.example/")]
    [InlineData("192.0.2.1", "http://0xc0000201/")]
    [InlineData("[2001:db8::1]", "http://[2001:db8::1]/")]
    public void 設定とQRの正規化が一致する(string entry, string input)
    {
        var site = SiteAllowlist.Normalize(entry);
        Assert.NotNull(site);
        Assert.True(SiteAllowlist.Contains([site], QrClassifier.Classify(input).Host!));
    }

    [Fact]
    public void 空や無効な一覧は全許可にならない()
    {
        var s = new Settings { OpenDirectly = true, OpenOnlyAllowedSites = true,
            AllowedSites = [null!, "", " ", "*.", ".", "https://"] }.Normalized();
        Assert.Empty(s.AllowedSites);
        Assert.True(QrDispatcher.CreateDefault(new FakeClock()).Decide(["https://evil.example/"], s.ToDispatchOptions())[0].NotAllowed);
    }

    [Fact]
    public void Unicodeの見た目が似ていても別ホストを許可しない()
    {
        var site = SiteAllowlist.Normalize("example.example")!;
        var host = QrClassifier.Classify("https://exаmple.example/").Host!;
        Assert.False(SiteAllowlist.Contains([site], host));
        Assert.False(SiteAllowlist.Contains([site], "evil-example.example"));
    }
}
