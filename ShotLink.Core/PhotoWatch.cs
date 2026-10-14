namespace ShotLink.Core;

public enum WatchState
{
    /// <summary>見張っている</summary>
    Watching,

    /// <summary>フォルダが無い。できたら見張り始める（仕様書 7章）</summary>
    Missing,
}

/// <summary>
/// 見張る全部のフォルダをまとめ、新しく現れた写真を1枚ずつ知らせる（仕様書 3.1・3.2・7章）。
/// 起動前・一時停止の間に増えた写真は知らせない
/// </summary>
public sealed class PhotoWatch : IDisposable
{
    /// <summary>対象の拡張子（仕様書 3.1）。VRChat は .png、ほかのスクショ道具は .jpg が多い</summary>
    public static readonly IReadOnlySet<string> Extensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    readonly IFolderWatcherFactory _factory;
    readonly IClock _clock;
    readonly Lock _lock = new();
    readonly Dictionary<WatchTarget, IFolderWatcher?> _watchers = [];

    // 同じファイルを2回知らせないための控え。フォルダが重なっているとき（追加のフォルダが VRChat のフォルダの中にある）や、
    // 作られた→名前が変わったの両方が届いたとき、あふれて読み直したときに同じファイルが来る
    readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    DateTimeOffset _since;
    bool _paused;

    public PhotoWatch(IFolderWatcherFactory factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
        _since = clock.Now;
    }

    /// <summary>新しい写真が現れた。見張りのスレッドから呼ばれる</summary>
    public event Action<string>? PhotoAppeared;

    /// <summary>どれかのフォルダの状態が変わった（アイコンの見た目と通知のため）</summary>
    public event Action? StateChanged;

    public bool IsPaused
    {
        get { lock (_lock) { return _paused; } }
    }

    public IReadOnlyDictionary<WatchTarget, WatchState> States
    {
        get
        {
            lock (_lock)
            {
                return _watchers.ToDictionary(p => p.Key, p => p.Value is null ? WatchState.Missing : WatchState.Watching);
            }
        }
    }

    /// <summary>見張る場所を入れ替える。前からあるフォルダは見張りを続ける（その間の写真を取りこぼさない）</summary>
    public void SetTargets(IEnumerable<WatchTarget> targets)
    {
        var wanted = Deduplicate(targets);
        lock (_lock)
        {
            foreach (var old in _watchers.Keys.Except(wanted).ToArray())
            {
                _watchers[old]?.Dispose();
                _watchers.Remove(old);
            }

            foreach (var target in wanted.Where(t => !_watchers.ContainsKey(t)))
            {
                _watchers[target] = Attach(target);
            }
        }

        StateChanged?.Invoke();
    }

    /// <summary>無かったフォルダができていないかを見る。App が数秒ごとに呼ぶ</summary>
    public void RetryMissing()
    {
        var changed = false;
        lock (_lock)
        {
            foreach (var target in _watchers.Where(p => p.Value is null).Select(p => p.Key).ToArray())
            {
                var watcher = Attach(target);
                if (watcher is not null)
                {
                    _watchers[target] = watcher;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            _paused = true;
        }
    }

    /// <summary>止めていた間の写真は読まない（仕様書 3.1 [決定]）。ここから先に現れた物だけ</summary>
    public void Resume()
    {
        lock (_lock)
        {
            _paused = false;
            _since = _clock.Now;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var w in _watchers.Values)
            {
                w?.Dispose();
            }

            _watchers.Clear();
        }
    }

    IFolderWatcher? Attach(WatchTarget target)
    {
        var watcher = _factory.TryCreate(target);
        if (watcher is null)
        {
            return null;
        }

        watcher.FileAppeared += OnAppeared;
        watcher.Overflowed += () => Rescan(target);
        watcher.Lost += () => OnLost(target, watcher);
        return watcher;
    }

    void OnAppeared(string path)
    {
        if (!Extensions.Contains(Path.GetExtension(path)))
        {
            return;
        }

        lock (_lock)
        {
            if (_paused || !_seen.Add(Path.GetFullPath(path)))
            {
                return;
            }
        }

        PhotoAppeared?.Invoke(path);
    }

    void Rescan(WatchTarget target)
    {
        // あふれた間に現れた写真を拾う。見張り始め（再開）より前に作られた物は、起動前の写真と同じく読まない
        DateTimeOffset since;
        lock (_lock)
        {
            since = _since;
        }

        IEnumerable<string> files;
        try
        {
            var option = target.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            files = Directory.EnumerateFiles(target.Path, "*", option)
                .Where(f => File.GetCreationTime(f) >= since.LocalDateTime)
                .OrderBy(File.GetCreationTime)
                .ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var f in files)
        {
            OnAppeared(f);
        }
    }

    void OnLost(WatchTarget target, IFolderWatcher watcher)
    {
        lock (_lock)
        {
            if (!_watchers.TryGetValue(target, out var current) || current != watcher)
            {
                return;
            }

            watcher.Dispose();
            _watchers[target] = null;
        }

        StateChanged?.Invoke();
    }

    static List<WatchTarget> Deduplicate(IEnumerable<WatchTarget> targets)
    {
        // 同じフォルダが2回指定されたら1つにまとめる（サブフォルダを含める方を残す）
        return targets
            .Select(t => t with { Path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(t.Path)) })
            .GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(t => t.IncludeSubfolders).First())
            .ToList();
    }
}
