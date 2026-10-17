namespace ShotLink.Core;

/// <summary>
/// 同じ URL を、最後に開いてから一定時間はもう一度開かない（仕様書 3.4）。通知は毎回出すので、ここで抑えるのはブラウザで開くことだけ
/// </summary>
public sealed class RecentOpenFilter(IClock clock)
{
    readonly Dictionary<string, DateTimeOffset> _lastOpened = new(StringComparer.Ordinal);
    readonly Lock _lock = new();

    /// <summary>開いてよければ（初めて・最後に開いてから window を過ぎた）、開いたと控えて true</summary>
    /// <param name="window">設定の窓の「OK」ですぐ効くよう、作るときに決めずに毎回受け取る</param>
    public bool TryOpen(string text, TimeSpan window)
    {
        lock (_lock)
        {
            var now = clock.Now;
            Purge(now, window);
            if (_lastOpened.ContainsKey(text))
            {
                // 開かなかったときは時刻を延ばさない。最初に開いてから数えるので、撮り続けても 60 秒たてばまた開く
                return false;
            }

            _lastOpened[text] = now;
            return true;
        }
    }

    void Purge(DateTimeOffset now, TimeSpan window)
    {
        // 文字列を溜め続けないよう、期限の切れたものを捨てる。件数は開いた URL の数なので、毎回なめても軽い
        foreach (var (text, opened) in _lastOpened.ToArray())
        {
            if (now - opened >= window)
            {
                _lastOpened.Remove(text);
            }
        }
    }
}
