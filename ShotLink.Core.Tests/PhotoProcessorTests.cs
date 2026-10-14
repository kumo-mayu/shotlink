using ShotLink.Core;
using ShotLink.Fixtures;

namespace ShotLink.Core.Tests;

public class PhotoProcessorTests
{
    sealed class MapLoader(Dictionary<string, GrayImage?> images) : IImageLoader
    {
        public GrayImage? TryLoad(string path) => images.GetValueOrDefault(path);
    }

    sealed class RecordingSink : IQrSink
    {
        public List<(QrDecision Decision, string Photo)> Handled { get; } = [];

        public List<QrBatch> Batches { get; } = [];

        public void Handle(QrBatch batch, string photo)
        {
            Batches.Add(batch);
            Handled.AddRange(batch.Select(d => (d, photo)));
        }
    }

    sealed class ReadyProbe : IFileProbe
    {
        public FileCheck Check(string path) => path.Contains("never") ? new(null, FileEnding.Incomplete) : new(1000, FileEnding.Complete);
    }

    static GrayImage Photo(params string[] texts)
    {
        var b = new SceneBuilder(1920, 1080, texts.Length + 1);
        for (var i = 0; i < texts.Length; i++)
        {
            b.Place(new QrPlacement(texts[i], 300 + i * 500, 540, 250));
        }

        return b.Build();
    }

    readonly RecordingSink _sink = new();
    readonly List<string> _log = [];
    readonly FakeClock _clock = new();
    DispatchOptions _options = new(OpenDirectly: false, NotifyNonUrl: true);

    PhotoProcessor Create(Dictionary<string, GrayImage?> images) => new(
        new FileReadyWaiter(new ReadyProbe(), (_, _) => Task.CompletedTask),
        new MapLoader(images),
        new QrReader(),
        QrDispatcher.CreateDefault(_clock),
        () => _options,
        _sink,
        _log.Add);

    [Fact]
    public async Task 写真の_QR_を読んで順に渡す()
    {
        var p = Create(new()
        {
            ["1.png"] = Photo("https://a.example/"),
            ["2.png"] = Photo("hello", "https://b.example/"),
        });
        p.Enqueue("1.png");
        p.Enqueue("2.png");
        await p.CompleteAsync();

        // 写真の順は守る。1枚の中で読む順は読み取り器しだいなので、写真の中では並べ替えて比べる
        Assert.Equal(["1.png", "2.png", "2.png"], _sink.Handled.Select(h => h.Photo));
        Assert.Equal(
            [("1.png", "https://a.example/", QrAction.NotifyToOpen), ("2.png", "hello", QrAction.NotifyToCopy), ("2.png", "https://b.example/", QrAction.NotifyToOpen)],
            _sink.Handled.Select(h => (h.Photo, h.Decision.Content.Text, h.Decision.Action)).OrderBy(t => t.Photo).ThenBy(t => t.Text, StringComparer.Ordinal));
    }

    [Fact]
    public async Task 続けて撮った同じ_QR_は一回だけ()
    {
        var p = Create(new() { ["1.png"] = Photo("https://a.example/"), ["2.png"] = Photo("https://a.example/") });
        p.Enqueue("1.png");
        p.Enqueue("2.png");
        await p.CompleteAsync();
        Assert.Single(_sink.Handled);
    }

    [Fact]
    public async Task 設定の変更は次の写真から効く()
    {
        var p = Create(new() { ["1.png"] = Photo("https://a.example/") });
        _options = _options with { OpenDirectly = true };
        p.Enqueue("1.png");
        await p.CompleteAsync();
        Assert.Equal(QrAction.Opened, _sink.Handled.Single().Decision.Action);
    }

    [Fact]
    public async Task 読めない写真と書き終わらない写真は飛ばして続ける()
    {
        var p = Create(new() { ["broken.png"] = null, ["ok.png"] = Photo("x") });
        p.Enqueue("broken.png");
        p.Enqueue("never.png");
        p.Enqueue("ok.png");
        await p.CompleteAsync();

        Assert.Equal(["x"], _sink.Handled.Select(h => h.Decision.Content.Text));
        Assert.True(2 == _log.Count(l => l.Contains("broken.png") || l.Contains("never.png")), string.Join(" / ", _log));
    }

    [Fact]
    public async Task 渡した先で落ちても次の写真へ進む()
    {
        var calls = 0;
        var p = new PhotoProcessor(
            new FileReadyWaiter(new ReadyProbe(), (_, _) => Task.CompletedTask),
            new MapLoader(new() { ["1.png"] = Photo("a"), ["2.png"] = Photo("b") }),
            new QrReader(),
            QrDispatcher.CreateDefault(_clock),
            () => _options,
            new ThrowOnceSink(() => calls++),
            _log.Add);
        p.Enqueue("1.png");
        p.Enqueue("2.png");
        await p.CompleteAsync();
        Assert.Equal(2, calls);
    }

    // ログに中身を書かない（仕様書 7章：ホスト名まで）
    [Fact]
    public async Task ログに_QR_の中身を書かない()
    {
        var p = Create(new() { ["1.png"] = Photo("https://a.example/secret-token-123", "ひみつの合言葉") });
        p.Enqueue("1.png");
        await p.CompleteAsync();

        Assert.True(2 == _log.Count(l => l.StartsWith("QR：")), string.Join(" / ", _log));
        Assert.Contains(_log, l => l.Contains("a.example"));
        Assert.DoesNotContain(_log, l => l.Contains("secret-token-123") || l.Contains("ひみつ"));
    }

    [Fact]
    public async Task 読めなかった写真を知らせる()
    {
        var p = Create(new() { ["broken.png"] = null, ["ok.png"] = Photo("x") });
        var failed = new List<string>();
        p.PhotoFailed += failed.Add;
        p.Enqueue("broken.png");
        p.Enqueue("never.png");
        p.Enqueue("ok.png");
        await p.CompleteAsync();
        Assert.Equal(["broken.png", "never.png"], failed);
    }

    // Codex の点検 2026-10-14：ログが書けないと、失敗を書く catch の中でまた投げて、処理の繰り返しごと止まっていた
    [Fact]
    public async Task ログが投げても次の写真へ進む()
    {
        var p = new PhotoProcessor(
            new FileReadyWaiter(new ReadyProbe(), (_, _) => Task.CompletedTask),
            new MapLoader(new() { ["broken.png"] = null, ["1.png"] = Photo("a") }),
            new QrReader(),
            QrDispatcher.CreateDefault(_clock),
            () => _options,
            _sink,
            _ => throw new UnauthorizedAccessException("試験のための失敗"));
        Exception? stopped = null;
        p.Stopped += e => stopped = e;
        p.Enqueue("broken.png");
        p.Enqueue("1.png");
        await p.CompleteAsync();
        Assert.Equal(["a"], _sink.Handled.Select(h => h.Decision.Content.Text));
        Assert.Null(stopped);
    }

    sealed class ThrowOnceSink(Action onCall) : IQrSink
    {
        bool _thrown;

        public void Handle(QrBatch batch, string photo)
        {
            onCall();
            if (!_thrown)
            {
                _thrown = true;
                throw new InvalidOperationException("試験のための失敗");
            }
        }
    }
}
