namespace ShotLink.Core;

/// <summary>設定・履歴・ログの置き場（仕様書 6章）</summary>
public static class ShotLinkHome
{
    /// <summary>試験と確かめの起動で、本番の置き場の代わりに使うフォルダ</summary>
    public const string EnvironmentVariable = "SHOTLINK_HOME";

    /// <param name="environmentValue">SHOTLINK_HOME の値</param>
    /// <param name="allowProduction">
    /// 本物の起動（ShotLink.exe の入口）だけが true を渡す。それ以外で SHOTLINK_HOME が無ければ例外で止める。
    /// Chmonos で、保存先を指定し忘れた道具が本番のデータを開いたことがある（Chmonos の CLAUDE.md「本番のデータを触らない」）
    /// </param>
    public static string Resolve(string? environmentValue, bool allowProduction)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return Path.GetFullPath(environmentValue);
        }

        if (!allowProduction)
        {
            throw new InvalidOperationException($"{EnvironmentVariable} が指定されていない。試験と確かめは作業用フォルダを指定する");
        }

        // 実行ファイルの隣には書かない。zip から出して置いた場所が書き込めないこともあり、版を上げるたびに設定が消える
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShotLink");
    }
}
