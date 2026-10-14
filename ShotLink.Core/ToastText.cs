namespace ShotLink.Core;

/// <param name="Title">1行目。URL ならホスト名（仕様書 4章：ホスト名をはっきり見せる）</param>
/// <param name="Body">2行目。中身そのもの（長ければ切る）</param>
/// <param name="Footer">小さい字の一言。クリックで何が起きるか</param>
public sealed record ToastText(string Title, string Body, string Footer)
{
    /// <summary>
    /// 本文は 200 字まで。トーストは数行しか見せず、長い中身は途中で切れて読めない。
    /// クリックすれば全文が開く（URL）か、コピーされる（文字列）
    /// </summary>
    public const int BodyLimit = 200;

    public static ToastText For(QrDecision decision)
    {
        var c = decision.Content;
        var body = Shorten(c.Text.Trim(), BodyLimit);
        return decision.Action switch
        {
            QrAction.Opened => new(c.Host!, body, "ブラウザで開きました"),
            QrAction.NotifyToOpen when decision.RateLimited => new(c.Host!, body, "続けて開くのは1分に3件までです。クリックで開きます"),
            QrAction.NotifyToOpen => new(c.Host!, body, "クリックで開きます"),
            _ => new("QR を読みました", body, "クリックでコピーします"),
        };
    }

    /// <summary>メニューの「最近読んだ QR」の1行。改行をつぶし、長ければ切る</summary>
    public static string MenuLabel(string text, int limit = 60) =>
        Shorten(string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim(), limit)
            // メニューの & は次の文字の下線（アクセスキー）になる
            .Replace("&", "&&");

    static string Shorten(string text, int limit) =>
        text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit - 1), "…");
}
