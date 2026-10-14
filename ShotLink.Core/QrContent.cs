namespace ShotLink.Core;

public enum QrKind
{
    /// <summary>http / https の URL。ブラウザで開いてよいのはこれだけ（仕様書 4章）</summary>
    WebUrl,

    /// <summary>それ以外のすべて（文字列・他のスキーム）。開かず、コピーだけ</summary>
    Text,
}

/// <param name="Text">QR に入っていた文字列そのまま</param>
/// <param name="Url">WebUrl のときだけ。ブラウザへ渡す URL</param>
/// <param name="Host">WebUrl のときだけ。通知に見せるホスト名（国際化ドメインは xn-- のまま）</param>
public sealed record QrContent(string Text, QrKind Kind, Uri? Url, string? Host);

public static class QrClassifier
{
    public static QrContent Classify(string text)
    {
        var url = TryWebUrl(text);
        return url is null
            ? new QrContent(text, QrKind.Text, null, null)
            : new QrContent(text, QrKind.WebUrl, url, url.IdnHost);
    }

    static Uri? TryWebUrl(string text)
    {
        var trimmed = text.Trim();

        // 途中に空白や改行・制御文字があるものは URL として扱わない。Uri は一部の空白や制御文字を
        // 黙って取り除いて読むので、通知に見せた文字列と開く先が食い違うおそれがある
        if (trimmed.Length == 0 || trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // スキームは大文字小文字を区別しない（HTTPS://… も URL）。Uri は小文字にそろえて返す
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return string.IsNullOrEmpty(uri.Host) ? null : uri;
    }
}
