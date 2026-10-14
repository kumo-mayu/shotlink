namespace ShotLink.Core;

public enum QrAction
{
    /// <summary>ブラウザで開いた。通知も出す</summary>
    Opened,

    /// <summary>URL だが開かなかった（直接開くが OFF か、回数の上限）。通知のクリックで開く</summary>
    NotifyToOpen,

    /// <summary>URL ではない。通知のクリックでコピーする</summary>
    NotifyToCopy,

    /// <summary>URL ではなく、URL 以外は知らせない設定。履歴にだけ残す</summary>
    HistoryOnly,
}

/// <param name="RateLimited">直接開くが ON なのに、回数の上限で開かなかった</param>
public sealed record QrDecision(QrContent Content, QrAction Action, bool RateLimited = false);

/// <param name="OpenDirectly">URL を直接開く（既定 OFF。仕様書 4章）</param>
/// <param name="NotifyNonUrl">URL 以外の QR も知らせる（既定 ON）</param>
public sealed record DispatchOptions(bool OpenDirectly, bool NotifyNonUrl);

/// <summary>1枚の写真から読めた中身ごとに、開く・知らせる・黙るを決める（仕様書 3章の流れ）</summary>
public sealed class QrDispatcher(RecentContentFilter recent, OpenRateLimiter limiter)
{
    public static QrDispatcher CreateDefault(IClock clock) => new(
        new RecentContentFilter(clock, QrPolicy.DuplicateWindow),
        new OpenRateLimiter(clock, QrPolicy.OpenLimit, QrPolicy.OpenLimitWindow));

    /// <summary>同じ中身が 60 秒以内に来ていたものは結果に入れない（知らせず、履歴にも残さない）</summary>
    public IReadOnlyList<QrDecision> Decide(IEnumerable<string> texts, DispatchOptions options)
    {
        var decisions = new List<QrDecision>();

        // 1枚に同じ中身の QR が複数あっても1回だけ扱う（仕様書 3.4）
        foreach (var text in texts.Distinct(StringComparer.Ordinal))
        {
            if (!recent.TryAccept(text))
            {
                continue;
            }

            var content = QrClassifier.Classify(text);
            decisions.Add(content.Kind switch
            {
                QrKind.WebUrl when !options.OpenDirectly => new QrDecision(content, QrAction.NotifyToOpen),
                QrKind.WebUrl when limiter.TryTake() => new QrDecision(content, QrAction.Opened),
                QrKind.WebUrl => new QrDecision(content, QrAction.NotifyToOpen, RateLimited: true),
                _ when options.NotifyNonUrl => new QrDecision(content, QrAction.NotifyToCopy),
                _ => new QrDecision(content, QrAction.HistoryOnly),
            });
        }

        return decisions;
    }
}
