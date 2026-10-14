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
            QrAction.NotifyToOpen => new(c.Host!, body, "クリックで開きます"),
            _ => new("QR を読みました", body, "クリックでコピーします"),
        };
    }

    /// <summary>まとめた通知（仕様書 3.5）。中身の頭の3件を並べ、残りは件数だけ</summary>
    public static ToastText ForBatch(QrBatch batch)
    {
        const int shown = 3;
        var labels = batch.Take(shown).Select(d => d.Content.Host ?? Shorten(Flatten(d.Content.Text), 20));
        var body = string.Join("、", labels) + (batch.Count > shown ? $"、ほか {batch.Count - shown} 件" : "");
        var opened = batch.Count(d => d.Action == QrAction.Opened);
        var footer = opened > 0 ? $"{opened} 件をブラウザで開きました。クリックで一覧を開きます" : "クリックで一覧を開きます";
        return new($"QR を {batch.Count} 件読みました", body, footer);
    }

    /// <summary>メニューの「最近読んだ QR」の1行。改行をつぶし、長ければ切る</summary>
    public static string MenuLabel(string text, int limit = 60)
    {
        // URL はホスト名（xn-- のまま）を先に出す。原文だけだと、見た目の似た偽のドメインや、
        // https://長い文字@本当のホスト/ の本当のホストが、省略で隠れる（Codex の点検 2026-10-14）
        var content = QrClassifier.Classify(text);
        var label = content.Host is { } host
            ? $"{host}｜{Shorten(Flatten(text), Math.Max(10, limit - host.Length - 1))}"
            : Shorten(Flatten(text), limit);

        // メニューの & は次の文字の下線（アクセスキー）になる
        return label.Replace("&", "&&");
    }

    /// <summary>改行とタブをつぶして1行にする</summary>
    public static string Flatten(string text) =>
        string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();

    static string Shorten(string text, int limit) =>
        text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit - 1), "…");
}
