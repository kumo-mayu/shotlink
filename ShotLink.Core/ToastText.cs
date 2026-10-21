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

    /// <summary>
    /// 手で起動したときの通知（仕様書 7章）。窓が出ず、Windows 11 では新しいアイコンが「∧」の中に隠れることが多いので、
    /// 何も知らせないと「起動しなかった」と思って何度も押される
    /// </summary>
    public static ToastText Started(bool watchVrchat, int extraFolders) => new(
        "ShotLink を起動しました",
        watchVrchat ? "VRChat の写真フォルダを監視しています"
            : extraFolders > 0 ? "追加したフォルダを監視しています"
            : "監視するフォルダがありません",
        "クリックで設定を開きます");

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
    /// 書式の文字（Cf：向きを変える文字・幅の無い文字・ソフトハイフン・タグの文字など）は分類ごと消す。一つずつ挙げていたら、
    /// U+00AD・U+180E・U+206A・U+E0061 が漏れた（2026-10-18、Codex の2回目の攻撃の点検）。
    /// Cf でない見えない文字は名前で挙げる：結合書記素ジョイナー、ハングルの埋め字、異体字の選択子
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
