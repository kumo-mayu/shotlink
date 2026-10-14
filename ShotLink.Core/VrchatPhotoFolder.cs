using System.Text.Json;

namespace ShotLink.Core;

/// <summary>VRChat が写真を保存する場所を見つける（仕様書 3.1）</summary>
public static class VrchatPhotoFolder
{
    /// <summary>VRChat の設定ファイルの場所</summary>
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "VRChat", "VRChat", "config.json");

    /// <summary>
    /// 設定ファイルに picture_output_folder があればそこ、無ければ「ピクチャ」の下の VRChat。
    /// 「ピクチャ」は %USERPROFILE%\Pictures と決め打ちしない。OneDrive などへ移されていることがある
    /// </summary>
    /// <param name="configPath">試験では作業用フォルダの config.json を渡す（本物を読まない）</param>
    public static string Resolve(string configPath, string picturesFolder)
    {
        return ReadOutputFolder(configPath) ?? Path.Combine(picturesFolder, "VRChat");
    }

    static string? ReadOutputFolder(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return null;
            }

            // VRChat が書いている途中でも読めるよう、書き込みを妨げない開き方にする。ShotLink は読むだけ
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

            // キー名は VRChat の文書の「Configuration File」による（仕様書 3.1 [要確認]：実機で確かめていない）
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("picture_output_folder", out var folder)
                && folder.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(folder.GetString()))
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder.GetString()!.Trim()));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            // 壊れた・読めない設定ファイルでは、既定の場所を見る。VRChat も同じく既定に戻るはず
        }

        return null;
    }
}
