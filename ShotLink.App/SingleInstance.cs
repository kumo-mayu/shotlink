namespace ShotLink.App;

/// <summary>1つしか起動しない（仕様書 7章）。2つ目は1つ目に「設定の窓を開いて」と合図して終わる</summary>
sealed class SingleInstance : IDisposable
{
    readonly Mutex _mutex;
    readonly EventWaitHandle _signal;
    RegisteredWaitHandle? _wait;

    SingleInstance(Mutex mutex, EventWaitHandle signal)
    {
        _mutex = mutex;
        _signal = signal;
    }

    /// <summary>1つ目なら持ち主になって返す。2つ目なら1つ目に合図して null</summary>
    public static SingleInstance? TryAcquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{name}", out var created);
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show");
        if (created)
        {
            return new SingleInstance(mutex, signal);
        }

        signal.Set();
        signal.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>2つ目が起動されたときに呼ばれる（裏のスレッドから）</summary>
    public void Listen(Action onSecondLaunch)
    {
        _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onSecondLaunch(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        _signal.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
