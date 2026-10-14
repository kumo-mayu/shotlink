using System.Collections;

namespace ShotLink.Core;

public enum QrAction
{
    /// <summary>ブラウザで開いた。通知も出す</summary>
    Opened,

    /// <summary>URL だが開かなかった（直接開くが OFF か、まとめたとき、少し前に開いた）。通知のクリックで開く</summary>
    NotifyToOpen,

    /// <summary>URL ではない。通知のクリックでコピーする</summary>
    NotifyToCopy,

    /// <summary>URL ではなく、URL 以外は知らせない設定。履歴にだけ残す</summary>
    HistoryOnly,
}

/// <param name="OpenedRecently">直接開くつもりだったが、同じ URL を少し前に開いたので開かなかった</param>
public sealed record QrDecision(QrContent Content, QrAction Action, bool OpenedRecently = false);

/// <summary>1枚の写真から決めたこと。位置の順に並ぶ</summary>
/// <param name="Summarized">4件以上あったので、通知を1件にまとめる（仕様書 3.5）</param>
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
/// <param name="ReopenSameUrl">同じ URL を 60 秒以内にもう一度撮ったときも開く（既定 OFF。仕様書 3.4）</param>
public sealed record DispatchOptions(bool OpenDirectly, bool NotifyNonUrl, bool OpenWhenSummarized = false, bool ReopenSameUrl = false);

/// <summary>1枚の写真から読めた中身ごとに、開く・知らせる・黙るを決める（仕様書 3章の流れ）</summary>
public sealed class QrDispatcher(RecentOpenFilter recentOpen)
{
    public static QrDispatcher CreateDefault(IClock clock) => new(
        new RecentOpenFilter(clock, QrPolicy.ReopenWindow));

    /// <summary>
    /// 中身は写真の中の位置の順に渡す（直接開く順・通知の順になる）。
    /// 撮った QR は毎回知らせる。少し前に見た中身でも結果から外さない（仕様書 3.4。撮ったのに出たり出なかったりしないように）
    /// </summary>
    public QrBatch Decide(IEnumerable<string> texts, DispatchOptions options)
    {
        // 1枚に同じ中身の QR が複数あっても1回だけ扱う（仕様書 3.4）
        var fresh = texts.Distinct(StringComparer.Ordinal).Select(QrClassifier.Classify).ToList();
        var summarized = fresh.Count >= QrPolicy.SummarizeThreshold;
        var mayOpen = options.OpenDirectly && (!summarized || options.OpenWhenSummarized);

        var decisions = fresh.Select(content => content.Kind switch
        {
            QrKind.WebUrl when !mayOpen => new QrDecision(content, QrAction.NotifyToOpen),
            QrKind.WebUrl when options.ReopenSameUrl || recentOpen.TryOpen(content.Text) => new QrDecision(content, QrAction.Opened),
            QrKind.WebUrl => new QrDecision(content, QrAction.NotifyToOpen, OpenedRecently: true),
            _ when options.NotifyNonUrl => new QrDecision(content, QrAction.NotifyToCopy),
            _ => new QrDecision(content, QrAction.HistoryOnly),
        }).ToList();

        return new QrBatch(decisions, summarized);
    }
}
