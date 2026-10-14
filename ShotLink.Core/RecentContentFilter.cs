namespace ShotLink.Core;

/// <summary>同じ中身を一定時間、2回目以降は通さない（仕様書 3.4）</summary>
public sealed class RecentContentFilter(IClock clock, TimeSpan window)
{
    readonly Dictionary<string, DateTimeOffset> _lastSeen = new(StringComparer.Ordinal);
    readonly Lock _lock = new();

    /// <summary>初めて見た中身、または最後に見てから window を過ぎた中身なら true</summary>
    public bool TryAccept(string text)
    {
        lock (_lock)
        {
            var now = clock.Now;
            Purge(now);

            var fresh = !_lastSeen.ContainsKey(text);

            // 弾いたときも最後に見た時刻を更新する。同じ相手を 30 秒おきに撮り続けているあいだは、
            // 最初の1回から数えると 60 秒ごとにまた開いてしまうので、撮り続けている間はずっと黙らせる
            _lastSeen[text] = now;
            return fresh;
        }
    }

    void Purge(DateTimeOffset now)
    {
        // 文字列を溜め続けないよう、期限の切れたものを捨てる。件数は撮った QR の数なので、毎回なめても軽い
        foreach (var (text, seen) in _lastSeen.ToArray())
        {
            if (now - seen >= window)
            {
                _lastSeen.Remove(text);
            }
        }
    }
}
