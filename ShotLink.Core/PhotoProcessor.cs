using System.Threading.Channels;

namespace ShotLink.Core;

/// <summary>画像ファイルを明るさの並びにする。App の側で WIC を使う（Core は画像の形式を知らない）</summary>
public interface IImageLoader
{
    /// <summary>読めなければ null（壊れている・対応していない形式）</summary>
    GrayImage? TryLoad(string path);
}

/// <summary>決めたことを実際に行う（開く・知らせる・履歴に残す）。App が通知の部品で受ける</summary>
public interface IQrSink
{
    void Handle(QrDecision decision, string photo);
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
    }

    public void Enqueue(string path) => _queue.Writer.TryWrite(path);

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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // 1枚で落ちて見張り全体が止まらないように。何が起きたかはログで追う
                _log($"写真の処理に失敗：{Path.GetFileName(path)}：{e.GetType().Name}：{e.Message}");
            }
        }
    }

    async Task ProcessAsync(string path, CancellationToken cancel)
    {
        if (!await _waiter.WaitAsync(path, cancel))
        {
            _log($"書き込みが終わらないので読まない：{Path.GetFileName(path)}");
            return;
        }

        var image = _loader.TryLoad(path);
        if (image is null)
        {
            _log($"画像として読めない：{Path.GetFileName(path)}");
            return;
        }

        var texts = _reader.Read(image);
        foreach (var decision in _dispatcher.Decide(texts, _options()))
        {
            // ログに中身は書かない。URL ならホスト名まで（仕様書 7章）
            _log($"QR：{decision.Action}：{decision.Content.Host ?? "（URL ではない）"}：{Path.GetFileName(path)}");
            _sink.Handle(decision, path);
        }
    }
}
