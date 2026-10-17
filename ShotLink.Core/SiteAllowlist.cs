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

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.IdnHost))
        {
            return null;
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        return host.Length == 0 ? null : host;
    }

    /// <summary>ホスト名が一覧のどれか、またはその下位のドメインか</summary>
    public static bool Contains(IEnumerable<string> sites, string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        return sites.Any(site => host == site || host.EndsWith("." + site, StringComparison.Ordinal));
    }
}
