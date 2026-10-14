namespace ShotLink.Core;

/// <summary>同じ中身の除外と、通知をまとめる件数（仕様書 3.4・3.5 [決定]。設定には出さず固定）。
/// 直接開く回数の上限は 2026-10-14 に外した（ユーザー判断）。写真は利用者が1枚ずつ撮る物で、1枚に多数ある場合はまとめて既定では開かないので、上限は続けて撮る人の邪魔になるだけだった</summary>
public static class QrPolicy
{
    /// <summary>
    /// 同じ相手を続けて撮ると、同じ QR が何枚にも写る。続けて撮るのはせいぜい数秒〜数十秒なので、60 秒あれば足りる
    /// </summary>
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 1枚からこの件数以上読めたら、通知を1件にまとめる（仕様書 3.5 [決定]）。3件までなら通知が並んでも読めるが、
    /// ポスターが並んだワールドなどで十件以上が一度に並ぶと、通知センターが埋まって目当ての物を探せない
    /// </summary>
    public const int SummarizeThreshold = 4;
}
