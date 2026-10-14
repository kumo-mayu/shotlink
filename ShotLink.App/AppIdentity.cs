using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ShotLink.App;

/// <summary>Windows に名乗る名前（通知の登録・1つしか起動しない仕組み）</summary>
static class AppIdentity
{
    /// <summary>
    /// 通知の登録の名前（AppUserModelID）。決めておかないと、通知の部品が exe の置き場所ごとに別の登録を作り、
    /// 版を上げて別のフォルダへ展開するたびに Windows の通知の一覧へ「ShotLink」が増える（仕様書 8章 [決定]）。
    /// 試しの起動（SHOTLINK_HOME あり）は別の名前にする。同じ名前だと、開発用の exe の場所で本物の登録が上書きされる
    /// </summary>
    public static string Aumid(bool development) => development ? "KumoMayu.ShotLink.Dev" : "KumoMayu.ShotLink";

    /// <summary>
    /// 1つしか起動しない仕組みの名前。保存先ごとに分け、試しの起動と本物が互いを「もう起動している」と見ないようにする
    /// </summary>
    public static string InstanceName(string home)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(home).ToUpperInvariant()));
        return "KumoMayu.ShotLink." + Convert.ToHexString(hash, 0, 8);
    }

    /// <summary>通知の部品を使う前に呼ぶ。部品は、このプロセスに付いた名前で登録する</summary>
    public static void Apply(bool development) => SetCurrentProcessExplicitAppUserModelID(Aumid(development));

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SetCurrentProcessExplicitAppUserModelID(string appId);
}
