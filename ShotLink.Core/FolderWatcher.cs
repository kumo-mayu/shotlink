namespace ShotLink.Core;

/// <summary>1つのフォルダの見張り。試験では差し替え、本物の FileSystemWatcher に触れない（CLAUDE.md「確かめ方」）</summary>
public interface IFolderWatcher : IDisposable
{
    /// <summary>ファイルが現れた（作られた・移されてきた・名前が変わった）。中身はフルパス</summary>
    event Action<string>? FileAppeared;

    /// <summary>知らせを取りこぼした（バッファのあふれ）。フォルダを読み直す合図</summary>
    event Action? Overflowed;

    /// <summary>見張っているフォルダそのものが消えた・見えなくなった</summary>
    event Action? Lost;
}

public interface IFolderWatcherFactory
{
    /// <summary>フォルダがまだ無ければ null</summary>
    IFolderWatcher? TryCreate(WatchTarget target);
}

/// <param name="Path">フルパス</param>
public sealed record WatchTarget(string Path, bool IncludeSubfolders);

public sealed class FileSystemFolderWatcherFactory : IFolderWatcherFactory
{
    public IFolderWatcher? TryCreate(WatchTarget target) =>
        Directory.Exists(target.Path) ? new FileSystemFolderWatcher(target) : null;
}

sealed class FileSystemFolderWatcher : IFolderWatcher
{
    readonly FileSystemWatcher _watcher;

    public FileSystemFolderWatcher(WatchTarget target)
    {
        _watcher = new FileSystemWatcher(target.Path)
        {
            IncludeSubdirectories = target.IncludeSubfolders,
            NotifyFilter = NotifyFilters.FileName,
            // 既定の 8KB だと、別のソフトが写真をまとめて移したときにあふれやすい。64KB は上限
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += (_, e) => FileAppeared?.Invoke(e.FullPath);
        // 一時ファイルに書いてから名前を変えて仕上げるスクショ道具がある
        _watcher.Renamed += (_, e) => FileAppeared?.Invoke(e.FullPath);
        _watcher.Error += (_, e) =>
        {
            if (e.GetException() is InternalBufferOverflowException)
            {
                Overflowed?.Invoke();
            }
            else
            {
                Lost?.Invoke();
            }
        };
        _watcher.EnableRaisingEvents = true;
    }

    public event Action<string>? FileAppeared;
    public event Action? Overflowed;
    public event Action? Lost;

    public void Dispose() => _watcher.Dispose();
}
