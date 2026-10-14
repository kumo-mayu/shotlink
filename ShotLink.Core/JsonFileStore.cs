using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShotLink.Core;

/// <summary>設定と履歴を、人が読める JSON で読み書きする</summary>
public sealed class JsonFileStore<T>(string path, Func<T> createDefault) where T : class
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 日本語のフォルダ名・QR の中身を \uXXXX にせず、そのまま読めるように書く
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    readonly Lock _lock = new();

    public string Path => path;

    /// <summary>無ければ既定。壊れていれば .broken を付けて残し、既定で始める（手で直せるように消さない）</summary>
    public T Load()
    {
        lock (_lock)
        {
            if (!File.Exists(path))
            {
                return createDefault();
            }

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? createDefault();
            }
            catch (JsonException)
            {
                var broken = $"{path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Move(path, broken, overwrite: true);
                return createDefault();
            }
        }
    }

    public void Save(T value)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            // 書いている途中で落ちても元のファイルが残るよう、隣に書いてから置き換える
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            File.Move(temp, path, overwrite: true);
        }
    }
}
