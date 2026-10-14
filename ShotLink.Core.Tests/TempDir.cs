namespace ShotLink.Core.Tests;

/// <summary>試験ごとの作業用フォルダ。本物の写真フォルダや本番の保存先には触れない</summary>
sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shotlink-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // 見張りがまだ掴んでいることがある。一時フォルダなので残っても害は無い
        }
    }
}
