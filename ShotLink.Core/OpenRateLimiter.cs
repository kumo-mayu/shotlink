namespace ShotLink.Core;

/// <summary>直接開く回数の上限（仕様書 4章）。直近の window の中で limit 件まで</summary>
public sealed class OpenRateLimiter(IClock clock, int limit, TimeSpan window)
{
    readonly Queue<DateTimeOffset> _opened = new();
    readonly Lock _lock = new();

    /// <summary>まだ開いてよければ1件ぶん使って true</summary>
    public bool TryTake()
    {
        lock (_lock)
        {
            var now = clock.Now;
            while (_opened.Count > 0 && now - _opened.Peek() >= window)
            {
                _opened.Dequeue();
            }

            if (_opened.Count >= limit)
            {
                return false;
            }

            _opened.Enqueue(now);
            return true;
        }
    }
}
