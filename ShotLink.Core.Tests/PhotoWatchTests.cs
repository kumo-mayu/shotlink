using ShotLink.Core;

namespace ShotLink.Core.Tests;

public class PhotoWatchTests
{
    sealed class FakeWatcher : IFolderWatcher
    {
        public event Action<string>? FileAppeared;
        public event Action? Overflowed;
        public event Action? Lost;

        public bool Disposed { get; private set; }

        public void Appear(string path) => FileAppeared?.Invoke(path);
        public void Overflow() => Overflowed?.Invoke();
        public void Lose() => Lost?.Invoke();
        public void Dispose() => Disposed = true;
    }

    sealed class FakeFactory : IFolderWatcherFactory
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, FakeWatcher> Created { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IFolderWatcher? TryCreate(WatchTarget target)
        {
            if (!Existing.Contains(target.Path))
            {
                return null;
            }

            var w = new FakeWatcher();
            Created[target.Path] = w;
            return w;
        }
    }

    const string Vrc = @"C:\Users\u\Pictures\VRChat";
    const string Extra = @"D:\Screenshots";

    readonly FakeFactory _factory = new();
    readonly FakeClock _clock = new();
    readonly List<string> _photos = [];
    readonly PhotoWatch _watch;

    public PhotoWatchTests()
    {
        _factory.Existing.Add(Vrc);
        _factory.Existing.Add(Extra);
        _watch = new PhotoWatch(_factory, _clock);
        _watch.PhotoAppeared += _photos.Add;
        _watch.SetTargets([new(Vrc, true), new(Extra, false)]);
    }

    [Fact]
    public void 両方のフォルダの写真を知らせる()
    {
        _factory.Created[Vrc].Appear(Vrc + @"\2026-10\VRChat_a.png");
        _factory.Created[Extra].Appear(Extra + @"\shot.JPG");
        Assert.Equal([Vrc + @"\2026-10\VRChat_a.png", Extra + @"\shot.JPG"], _photos);
    }

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a.png.tmp")]
    [InlineData("a.gif")]
    [InlineData("a")]
    public void 対象の拡張子でなければ知らせない(string name)
    {
        _factory.Created[Extra].Appear(Extra + @"\" + name);
        Assert.Empty(_photos);
    }

    [Fact]
    public void 同じファイルは一回だけ()
    {
        // 作られた → 名前が変わった、や、重なったフォルダの両方から届く
        _factory.Created[Vrc].Appear(Vrc + @"\a.png");
        _factory.Created[Vrc].Appear(Vrc + @"\A.PNG");
        Assert.Single(_photos);
    }

    [Fact]
    public void 一時停止の間に現れた写真は再開しても知らせない()
    {
        _watch.Pause();
        _factory.Created[Vrc].Appear(Vrc + @"\paused.png");
        _watch.Resume();
        _factory.Created[Vrc].Appear(Vrc + @"\after.png");
        Assert.Equal([Vrc + @"\after.png"], _photos);
    }

    [Fact]
    public void 同じフォルダを二回指定したら一つにまとめ_サブフォルダを含める方を残す()
    {
        _watch.SetTargets([new(Extra, false), new(Extra + @"\", true)]);
        var target = Assert.Single(_watch.States.Keys);
        Assert.True(target.IncludeSubfolders);
    }

    [Fact]
    public void 無いフォルダは無いと示し_できたら見張り始める()
    {
        const string later = @"E:\Later";
        _watch.SetTargets([new(later, false)]);
        Assert.Equal(WatchState.Missing, _watch.States.Single().Value);

        _factory.Existing.Add(later);
        _watch.RetryMissing();
        Assert.Equal(WatchState.Watching, _watch.States.Single().Value);

        _factory.Created[later].Appear(later + @"\x.png");
        Assert.Equal([later + @"\x.png"], _photos);
    }

    [Fact]
    public void フォルダが消えたら無いになり_ほかは見張り続ける()
    {
        var states = 0;
        _watch.StateChanged += () => states++;
        _factory.Created[Extra].Lose();

        Assert.True(_factory.Created[Extra].Disposed);
        Assert.Equal(WatchState.Missing, _watch.States.Single(p => p.Key.Path == Extra).Value);
        Assert.Equal(WatchState.Watching, _watch.States.Single(p => p.Key.Path == Vrc).Value);
        Assert.Equal(1, states);
    }

    [Fact]
    public void 見張る場所を入れ替えても続くフォルダの見張りは作り直さない()
    {
        var before = _factory.Created[Vrc];
        _watch.SetTargets([new(Vrc, true)]);
        Assert.Same(before, _factory.Created[Vrc]);
        Assert.False(before.Disposed);
        Assert.True(_factory.Created[Extra].Disposed);
    }
}
