namespace ShotLink.Core;

/// <summary>
/// 直接開いてよいサイトの一覧（仕様書 3.6）。ホスト名で比べ、書いたホストの下位のドメインも含める。
/// example.com と書けば www.example.com も開く。www の有る無しを書き分けさせると、書き漏らして開かないことが増えるため
/// </summary>
public static class SiteAllowlist
{
    /// <summary>
    /// 人が書いた1行をホスト名にそろえる。URL を貼っても、そのホスト名だけを使う。
    /// 国際化ドメインは xn-- の形にする（QrContent.Host と同じ形で比べるため）。ホスト名にならなければ null
    /// </summary>
    public static string? Normalize(string? entry)
    {
        // そろえた結果をもう一度そろえて変わる行は捨てる。U+037E は1回目で「a;b.example」になり、2回目で消えた。
        // 保存と読み込みのたびにそろえるので、変わる行を残すと、開くサイトが知らないうちに減る（2026-10-18、Codex の2回目の攻撃の点検）
        var host = NormalizeOnce(entry);

        // IPv6 は IdnHost でかっこが外れる。そのままでは URL として読めないので、読み直すときだけ付け直す
        return host is not null && NormalizeOnce(host.Contains(':') ? $"[{host}]" : host) == host ? host : null;
    }

    static string? NormalizeOnce(string? entry)
    {
        var text = entry?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // 「*.example.com」「.example.com」と書く人もいる。どちらも下位を含む意味なので、そのまま example.com として扱う
        text = text.TrimStart('*').TrimStart('.');
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        // 手で書いた一行が IdnHost で投げると、設定の読み込みごと止まって起動できなかった。使えない行として捨てる
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || QrClassifier.TryIdnHost(uri) is not { } idn)
        {
            return null;
        }

        var host = idn.TrimEnd('.').ToLowerInvariant();
        return host.Length == 0 ? null : host;
    }

    /// <summary>ホスト名が一覧のどれか、またはその下位のドメインか</summary>
    public static bool Contains(IEnumerable<string> sites, string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        return sites.Any(site => host == site || host.EndsWith("." + site, StringComparison.Ordinal));
    }
}
