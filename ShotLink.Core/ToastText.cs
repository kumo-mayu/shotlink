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
        var body = Shorten(StripInvisible(c.Text).Trim(), BodyLimit);
        return decision.Action switch
        {
            QrAction.Opened => new(c.Host!, body, "ブラウザで開きました"),
            QrAction.NotifyToOpen when decision.NotAllowed => new(c.Host!, body, "ホワイトリストに無いので開きませんでした。クリックで開きます"),
            QrAction.NotifyToOpen when decision.OpenedRecently => new(c.Host!, body, "少し前に開いたので開きませんでした。クリックで開きます"),
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

    /// <summary>改行とタブをつぶして1行にする。見えない文字も取り除く（StripInvisible）</summary>
    public static string Flatten(string text) =>
        string.Join(' ', StripInvisible(text).Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();

    /// <summary>
    /// 見せる文字列から、向きを変える文字（U+202E など）と幅の無い文字を取り除く。残すと、例えば「evil.example/」の後ろに
    /// U+202E と「moc.elpmaxe」を置いた中身が、通知やメニューで example.com のように見える（2026-10-18、Codex の攻撃の点検）。
    /// 見せる所だけに使う。開く URL（Url）とコピーする原文はそのまま
    /// </summary>
    public static string StripInvisible(string text)
    {
        if (text.EnumerateRunes().All(r => !IsInvisible(r)))
        {
            return text;
        }

        var result = new System.Text.StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsInvisible(rune))
            {
                result.Append(rune.ToString());
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// \u66F8\u5F0F\u306E\u6587\u5B57\uFF08Cf\uFF1A\u5411\u304D\u3092\u5909\u3048\u308B\u6587\u5B57\u30FB\u5E45\u306E\u7121\u3044\u6587\u5B57\u30FB\u30BD\u30D5\u30C8\u30CF\u30A4\u30D5\u30F3\u30FB\u30BF\u30B0\u306E\u6587\u5B57\u306A\u3069\uFF09\u306F\u5206\u985E\u3054\u3068\u6D88\u3059\u3002\u4E00\u3064\u305A\u3064\u6319\u3052\u3066\u3044\u305F\u3089\u3001
    /// U+00AD\u30FBU+180E\u30FBU+206A\u30FBU+E0061 \u304C\u6F0F\u308C\u305F\uFF082026-10-18\u3001Codex \u306E2\u56DE\u76EE\u306E\u653B\u6483\u306E\u70B9\u691C\uFF09\u3002
    /// Cf \u3067\u306A\u3044\u898B\u3048\u306A\u3044\u6587\u5B57\u306F\u540D\u524D\u3067\u6319\u3052\u308B\uFF1A\u7D50\u5408\u66F8\u8A18\u7D20\u30B8\u30E7\u30A4\u30CA\u30FC\u3001\u30CF\u30F3\u30B0\u30EB\u306E\u57CB\u3081\u5B57\u3001\u7570\u4F53\u5B57\u306E\u9078\u629E\u5B50
    /// </summary>
    static bool IsInvisible(System.Text.Rune r) =>
        System.Text.Rune.GetUnicodeCategory(r) == System.Globalization.UnicodeCategory.Format
        || r.Value is 0x034F or 0x115F or 0x1160 or 0x3164 or 0xFFA0 or 0x17B4 or 0x17B5
        || r.Value is >= 0x180B and <= 0x180F
        || r.Value is >= 0xFE00 and <= 0xFE0F
        || r.Value is >= 0xE0100 and <= 0xE01EF;

    static string Shorten(string text, int limit) =>
        text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit - 1), "…");
}
