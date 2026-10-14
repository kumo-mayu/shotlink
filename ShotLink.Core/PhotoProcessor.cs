using System.Threading.Channels;

namespace ShotLink.Core;

/// <summary>画像ファイルを明るさの並びにする。App の側で WIC を使う（Core は画像の形式を知らない）</summary>
public interface IImageLoader
{
    /// <summary>写し取ったバイト列から読む。読めなければ null（壊れている・対応していない形式）</summary>
    /// <param name="name">ログに出す名前</param>
    GrayImage? TryLoad(byte[] data, string name);
}

/// <summary>決めたことを実際に行う（開く・知らせる・履歴に残す）。App が通知の部品で受ける</summary>
public interface IQrSink
{
    /// <summary>1枚の写真から決めたこと（位置の順）。空のときは呼ばれない</summary>
    void Handle(QrBatch batch, string photo);
}

/// <summary>
/// 現れた写真を1枚ずつ順に処理する（仕様書 3.3「裏のスレッドで、1枚ずつ順に」）。
/// 続けて撮ると写真が次々に来るが、8K を並べて読むとメモリが数百 MB 単位で膨らむので、並べない
/// </summary>
public sealed class PhotoProcessor : IAsyncDisposable
{
    readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    readonly FileReadyWaiter _waiter;
    readonly IImageLoader _loader;
    readonly QrReader _reader;
    readonly QrDispatcher _dispatcher;
    readonly Func<DispatchOptions> _options;
    readonly IQrSink _sink;
    readonly Action<string> _log;
    readonly CancellationTokenSource _stop = new();
    readonly Task _loop;
    int _pending;

    public PhotoProcessor(FileReadyWaiter waiter, IImageLoader loader, QrReader reader, QrDispatcher dispatcher,
        Func<DispatchOptions> options, IQrSink sink, Action<string> log)
    {
        _waiter = waiter;
        _loader = loader;
        _reader = reader;
        _dispatcher = dispatcher;
        _options = options;
        _sink = sink;
        _log = log;
        _loop = Task.Run(LoopAsync);
        _loop.ContinueWith(t => Stopped?.Invoke(t.Exception!.GetBaseException()), TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// 写真を読めなかった（書き込みが終わらない・画像として読めない・途中で失敗した）。裏のスレッドから呼ばれる。
    /// 見張りの「見た」の控えから外し、同じ場所に置き直されたら読み直せるようにするため
    /// </summary>
    public event Action<string>? PhotoFailed;

    /// <summary>
    /// 処理の繰り返しそのものが止まった。ここに来たら、以後の写真は読まれない。
    /// 起きないはずだが、起きたときに「見張っています」のまま黙らないよう、知らせる口を残す
    /// </summary>
    public event Action<Exception>? Stopped;

    public void Enqueue(string path)
    {
        // 読み手が1人の行列は残りの数を数えられない（Reader.Count が NotSupportedException）。自分で数える
        Interlocked.Increment(ref _pending);
        if (!_queue.Writer.TryWrite(path))
        {
            Interlocked.Decrement(ref _pending);
        }
    }

    /// <summary>試験のため：今ある分を全部処理し終えたら終わる</summary>
    public async Task CompleteAsync()
    {
        _queue.Writer.TryComplete();
        await _loop;
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _stop.CancelAsync();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
    }

    async Task LoopAsync()
    {
        await foreach (var path in _queue.Reader.ReadAllAsync(_stop.Token))
        {
            try
            {
                await ProcessAsync(path, _stop.Token);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                // 1枚で落ちて見張り全体が止まらないように。何が起きたかはログで追う。
                // 例外の文は書かない：渡した先の例外には QR の中身が入りうる（ログはホスト名まで。仕様書 7章）
                Log($"写真の処理に失敗：{Path.GetFileName(path)}：{e.GetType().Name}：{e.TargetSite?.Name}");
                Fail(path);
            }

            if (Interlocked.Decrement(ref _pending) == 0)
            {
                ReturnMemory();
                using var self = System.Diagnostics.Process.GetCurrentProcess();
                Log($"メモリ：.NET {GC.GetTotalMemory(false) / 1_048_576} MB・全体 {self.PrivateMemorySize64 / 1_048_576} MB");
            }
        }
    }

    /// <summary>
    /// ログが書けない（ログのファイルが読み取り専用・権限が無い）ことで、写真の処理を止めない。
    /// 失敗を書くための catch の中でまた投げると、繰り返しごと止まり、以後の写真が黙って読まれなくなる
    /// </summary>
    void Log(string message)
    {
        try
        {
            _log(message);
        }
        catch (Exception)
        {
        }
    }

    void Fail(string path)
    {
        try
        {
            PhotoFailed?.Invoke(path);
        }
        catch (Exception)
        {
            // 知らせる先の失敗で繰り返しを止めない
        }
    }

    /// <summary>
    /// 画像に使ったメモリを Windows に返す（仕様書 3.3 [決定]）。8K を1枚読むと、.NET は画像の分（約 33 MB）を
    /// 「また使うかもしれない」と持ち続け、待ち受け中の ShotLink だけの分が 19 MB から 51 MB のままになった。
    /// 大きな配列は圧縮しない領域に置かれるので、圧縮を1回だけ指定して回収する。
    /// 続けて撮っている間は次の写真ですぐ使うので、行列が空になったときだけ行う
    /// </summary>
    static void ReturnMemory()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    /// <summary>
    /// 画像を読んで QR を探す。非同期の関数の中に画像を置かない：置くと、終わった処理が次の写真まで画像を掴み続け、
    /// 8K を読んだあと 36 MB が戻らなかった（2026-10-14 に測った）
    /// </summary>
    IReadOnlyList<string>? Read(byte[] data, string path)
    {
        var image = _loader.TryLoad(data, Path.GetFileName(path));
        return image is null ? null : _reader.Read(image).Select(f => f.Text).ToList();
    }

    async Task ProcessAsync(string path, CancellationToken cancel)
    {
        var data = await _waiter.WaitAsync(path, cancel);
        if (data is null)
        {
            Log($"書き込みが終わらないので読まない：{Path.GetFileName(path)}");
            Fail(path);
            return;
        }

        var texts = Read(data, path);
        data = null;
        if (texts is null)
        {
            Log($"画像として読めない：{Path.GetFileName(path)}");
            Fail(path);
            return;
        }

        var batch = _dispatcher.Decide(texts, _options());
        foreach (var decision in batch)
        {
            // ログに中身は書かない。URL ならホスト名まで（仕様書 7章）
            Log($"QR：{decision.Action}：{decision.Content.Host ?? "（URL ではない）"}：{Path.GetFileName(path)}");
        }

        if (batch.Count > 0)
        {
            if (batch.Summarized)
            {
                Log($"QR が {batch.Count} 件あったので通知をまとめる：{Path.GetFileName(path)}");
            }

            _sink.Handle(batch, path);
        }
    }
}
