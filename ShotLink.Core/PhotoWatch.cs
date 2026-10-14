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
    /// <returns>場所として読めないので外した物（手で直した settings.json の壊れた行など）</returns>
    public IReadOnlyList<WatchTarget> SetTargets(IEnumerable<WatchTarget> targets)
    {
        var wanted = Deduplicate(targets, out var rejected);
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
        return rejected;
    }

    /// <summary>
    /// 読めなかった写真を「見た」の控えから外す。同じ場所に置き直されたら読み直せるように。
    /// 外さないと、書き込みが 10 秒で終わらなかった写真や壊れていた写真は、ShotLink を起動し直すまで二度と読まない
    /// </summary>
    public void Forget(string path)
    {
        lock (_lock)
        {
            _seen.Remove(Path.GetFullPath(path));
        }
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
        IFolderWatcher? watcher;
        try
        {
            watcher = _factory.TryCreate(target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // 見られないフォルダ（権限が無い・在るかを確かめた直後に消えた）は「無い」として扱い、5 秒ごとに見直す。
            // ここで投げると、ほかの正しいフォルダの見張りまで始まらない
            return null;
        }

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

    static List<WatchTarget> Deduplicate(IEnumerable<WatchTarget> targets, out List<WatchTarget> rejected)
    {
        var valid = new List<WatchTarget>();
        rejected = [];
        foreach (var t in targets)
        {
            try
            {
                valid.Add(t with { Path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(t.Path)) });
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // 使えない文字の入った場所など。1つのために全部の見張りを止めない
                rejected.Add(t);
            }
        }

        // 同じフォルダが2回指定されたら1つにまとめる（サブフォルダを含める方を残す）
        return valid
            .GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(t => t.IncludeSubfolders).First())
            .ToList();
    }
}
