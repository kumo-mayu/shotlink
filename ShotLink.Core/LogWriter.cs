namespace ShotLink.Core;

/// <summary>日ごとのログ（仕様書 7章）。QR の中身は書かない（書くのは呼ぶ側の責任。PhotoProcessor はホスト名まで）</summary>
public sealed class LogWriter
{
    /// <summary>
    /// 7日で消す。不具合の相談は起きてから数日のうちに来ることが多く、それより古いログは読まない。
    /// 写真の名前と URL のホスト名が残るので、長く溜めない
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    readonly string _folder;
    readonly IClock _clock;
    readonly Lock _lock = new();

    public LogWriter(string folder, IClock clock)
    {
        _folder = folder;
        _clock = clock;
        Directory.CreateDirectory(folder);
        DeleteOld();
    }

    public string Folder => _folder;

    public void Write(string message)
    {
        var now = _clock.Now;
        var line = $"{now:HH:mm:ss.fff} {message}{Environment.NewLine}";
        lock (_lock)
        {
            try
            {
                File.AppendAllText(Path.Combine(_folder, $"{now:yyyy-MM-dd}.log"), line);
            }
            catch (IOException)
            {
                // ログが書けないことで見張りを止めない
            }
        }
    }

    void DeleteOld()
    {
        var limit = _clock.Now.Date - Retention;
        foreach (var file in Directory.EnumerateFiles(_folder, "????-??-??.log"))
        {
            if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var day) && day < limit)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
