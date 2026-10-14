namespace ShotLink.Core;

/// <summary>同じ中身の除外と、直接開く回数の上限の値（仕様書 3.4・4章 [決定]。設定には出さず固定）</summary>
public static class QrPolicy
{
    /// <summary>
    /// 同じ相手を続けて撮ると、同じ QR が何枚にも写る。続けて撮るのはせいぜい数秒〜数十秒なので、60 秒あれば足りる
    /// </summary>
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 直接開くのは1分に3件まで。ワールドのポスターなどで別々の QR が並んでいても、1度に何十もタブが開かないように。
    /// 超えた分は通知だけになり、クリックすれば開けるので困ることは少ない
    /// </summary>
    public const int OpenLimit = 3;

    public static readonly TimeSpan OpenLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 1枚からこの件数以上読めたら、通知を1件にまとめる（仕様書 3.5 [決定]）。3件までなら通知が並んでも読めるが、
    /// ポスターが並んだワールドなどで十件以上が一度に並ぶと、通知センターが埋まって目当ての物を探せない
    /// </summary>
    public const int SummarizeThreshold = 4;
}
