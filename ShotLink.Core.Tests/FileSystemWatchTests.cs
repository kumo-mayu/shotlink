using ShotLink.Core;

namespace ShotLink.Core.Tests;

// 本物の FileSystemWatcher を使う試験はここだけ（CLAUDE.md「確かめ方」：揺れやすいので数件に絞り、待ちに余裕を持たせる）。
// 見張るのは作業用フォルダで、本物の写真フォルダには触れない
public class FileSystemWatchTests
{
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task 移されてきた写真と_サブフォルダの写真を知らせる()
    {
        using var dir = new TempDir();
        var watched = Directory.CreateDirectory(dir.Combine("watched")).FullName;
        var outside = Directory.CreateDirectory(dir.Combine("outside")).FullName;

        using var watch = new PhotoWatch(new FileSystemFolderWatcherFactory(), SystemClock.Instance);
        var seen = new TaskCompletionSource();
        var photos = new List<string>();
        watch.PhotoAppeared += p =>
        {
            lock (photos)
            {
                photos.Add(Path.GetFileName(p));
                if (photos.Count == 2)
                {
                    seen.TrySetResult();
                }
            }
        };
        watch.SetTargets([new(watched, IncludeSubfolders: true)]);

        // 仕様書 9章：見張りの確かめは、素材を見張っているフォルダへ移動する
        var src = Path.Combine(outside, "moved.png");
        await File.WriteAllBytesAsync(src, new byte[10]);
        File.Move(src, Path.Combine(watched, "moved.png"));

        var month = Directory.CreateDirectory(Path.Combine(watched, "2026-10")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(month, "VRChat_x.png"), new byte[10]);
        await File.WriteAllTextAsync(Path.Combine(month, "note.txt"), "x");

        await seen.Task.WaitAsync(Patience);
        lock (photos)
        {
            Assert.Equal(["VRChat_x.png", "moved.png"], photos.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void まだ無いフォルダは無いと示す()
    {
        using var dir = new TempDir();
        using var watch = new PhotoWatch(new FileSystemFolderWatcherFactory(), SystemClock.Instance);
        watch.SetTargets([new(dir.Combine("not-yet"), false)]);
        Assert.Equal(WatchState.Missing, watch.States.Single().Value);

        Directory.CreateDirectory(dir.Combine("not-yet"));
        watch.RetryMissing();
        Assert.Equal(WatchState.Watching, watch.States.Single().Value);
    }
}
