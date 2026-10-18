using System.Collections;

namespace ShotLink.Core;

public enum QrAction
{
    /// <summary>ブラウザで開いた。通知も出す</summary>
    Opened,

    /// <summary>URL だが開かなかった（直接開くが OFF か、まとめたとき、少し前に開いた、開くサイトの一覧に無い）。通知のクリックで開く</summary>
    NotifyToOpen,

    /// <summary>URL ではない。通知のクリックでコピーする</summary>
    NotifyToCopy,

    /// <summary>URL ではなく、URL 以外は知らせない設定。履歴にだけ残す</summary>
    HistoryOnly,
}

/// <param name="OpenedRecently">直接開くつもりだったが、同じ URL を少し前に開いたので開かなかった</param>
/// <param name="NotAllowed">直接開くつもりだったが、開くサイトの一覧に無いので開かなかった（仕様書 3.6）</param>
public sealed record QrDecision(QrContent Content, QrAction Action, bool OpenedRecently = false, bool NotAllowed = false);

/// <summary>1枚の写真から決めたこと。位置の順に並ぶ</summary>
/// <param name="Summarized">まとめる件数以上あったので、通知を1件にまとめる（仕様書 3.5）</param>
public sealed class QrBatch(IReadOnlyList<QrDecision> decisions, bool summarized) : IReadOnlyList<QrDecision>
{
    public bool Summarized => summarized;

    public QrDecision this[int index] => decisions[index];

    public int Count => decisions.Count;

    public IEnumerator<QrDecision> GetEnumerator() => decisions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <param name="OpenDirectly">URL を直接開く（既定 OFF。仕様書 4章）</param>
/// <param name="NotifyNonUrl">URL 以外の QR も知らせる（既定 ON）</param>
/// <param name="OpenWhenSummarized">まとめたときも直接開く（既定 OFF。仕様書 3.5）</param>
/// <param name="ReopenSameUrl">同じ URL を続けて撮ったときも毎回開く（既定 OFF。仕様書 3.4）</param>
/// <param name="ReopenSeconds">同じ URL をもう一度開くまでの秒数（既定 60。仕様書 3.4）</param>
/// <param name="SummarizeThreshold">この件数以上でまとめる。null ならまとめない（既定 4。仕様書 3.5）</param>
/// <param name="AllowedSites">直接開いてよいサイト（そろえたホスト名）。null なら絞らない（既定。仕様書 3.6）</param>
public sealed record DispatchOptions(bool OpenDirectly, bool NotifyNonUrl, bool OpenWhenSummarized = false, bool ReopenSameUrl = false,
    int ReopenSeconds = QrPolicy.DefaultReopenSeconds, int? SummarizeThreshold = QrPolicy.DefaultSummarizeThreshold,
    IReadOnlyList<string>? AllowedSites = null);

/// <summary>1枚の写真から読めた中身ごとに、開く・知らせる・黙るを決める（仕様書 3章の流れ）</summary>
public sealed class QrDispatcher(RecentOpenFilter recentOpen)
{
    public static QrDispatcher CreateDefault(IClock clock) => new(new RecentOpenFilter(clock));

    /// <summary>
    /// 中身は写真の中の位置の順に渡す（直接開く順・通知の順になる）。
    /// 撮った QR は毎回知らせる。少し前に見た中身でも結果から外さない（仕様書 3.4。撮ったのに出たり出なかったりしないように）
    /// </summary>
    public QrBatch Decide(IEnumerable<string> texts, DispatchOptions options)
    {
        // 1枚に同じ中身の QR が複数あっても1回だけ扱う（仕様書 3.4）
        var fresh = texts.Distinct(StringComparer.Ordinal).Select(QrClassifier.Classify).ToList();
        var summarized = options.SummarizeThreshold is { } threshold && fresh.Count >= threshold;
        var mayOpen = options.OpenDirectly && (!summarized || options.OpenWhenSummarized);

        var decisions = fresh.Select(content => content.Kind switch
        {
            QrKind.WebUrl when !mayOpen => new QrDecision(content, QrAction.NotifyToOpen),
            // 一覧に無い物は、開いたと控える前に外す。控えると、あとで一覧に足しても少しの間開かない
            QrKind.WebUrl when options.AllowedSites is { } sites && !SiteAllowlist.Contains(sites, content.Host!) =>
                new QrDecision(content, QrAction.NotifyToOpen, NotAllowed: true),
            // 比べるのはブラウザに渡す形（UrlOpener が渡す AbsoluteUri）。原文で比べると、HTTPS://EXAMPLE.COM/ と https://example.com のような
            // 表記違いの QR を並べるだけで、同じ所を何度も開けた（2026-10-18、Codex の攻撃の点検）
            // さらにホストは見せている形（xn--）にそろえる。AbsoluteUri だけでは、全角の「ｅ」・句点の「。」・xn-- と Unicode の書き分けで
            // 同じホストでも別の鍵になり、同じ所を何度も開けた（2026-10-18、Codex の2回目の攻撃の点検）
            QrKind.WebUrl when options.ReopenSameUrl || recentOpen.TryOpen(ReopenKey(content), TimeSpan.FromSeconds(options.ReopenSeconds)) =>
                new QrDecision(content, QrAction.Opened),
            QrKind.WebUrl => new QrDecision(content, QrAction.NotifyToOpen, OpenedRecently: true),
            _ when options.NotifyNonUrl => new QrDecision(content, QrAction.NotifyToCopy),
            _ => new QrDecision(content, QrAction.HistoryOnly),
        }).ToList();

        return new QrBatch(decisions, summarized);
    }

    /// <summary>同じ URL かを比べる鍵。ホストは xn-- の形、ほかはブラウザに渡す形（既定のポート・パスの正規化は Uri がそろえる）</summary>
    static string ReopenKey(QrContent content)
    {
        var url = content.Url!;
        return $"{url.Scheme}://{content.Host}:{url.Port}{url.PathAndQuery}{url.Fragment}";
    }
}
