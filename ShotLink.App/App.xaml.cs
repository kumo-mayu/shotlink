using System.Drawing;
using System.IO;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Forms = System.Windows.Forms;

namespace ShotLink.App;

/// <summary>
/// 通知の作り方を決めるための試し（仕様書 8章 [要確認]・12章）。
/// NotifyIcon の吹き出しは、出ている間のクリックは受けられたが、通知センターに残らず、一度に1件しか出せなかった。
/// トースト通知で、出ている間・通知センターに入ったあと・続けて何件か出したときのクリックを確かめる
/// </summary>
public partial class App : System.Windows.Application
{
    Forms.NotifyIcon? _icon;
    string _log = "";
    int _count;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 本番の保存先に書かないよう、SHOTLINK_HOME が無ければ一時フォルダに書く（CLAUDE.md「本番の設定と履歴に触らない」）
        var home = Environment.GetEnvironmentVariable("SHOTLINK_HOME")
                   ?? Path.Combine(Path.GetTempPath(), "ShotLink-spike");
        Directory.CreateDirectory(home);
        _log = Path.Combine(home, "notify-spike.log");
        Log($"起動（通知のクリックで起動されたか：{ToastNotificationManagerCompat.WasCurrentProcessToastActivated()}）");

        // クリックは裏のスレッドで届く
        ToastNotificationManagerCompat.OnActivated += args =>
        {
            var a = ToastArguments.Parse(args.Argument);
            Log($"クリック：{a.Get("kind")}：{a.Get("text")}");
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("テスト通知（URL）", null, (_, _) => ShowUrl("https://booth.pm/ja/items/1234567"));
        menu.Items.Add("テスト通知（文字列）", null, (_, _) => ShowText("こんにちは"));
        menu.Items.Add("テスト通知（3件続けて）", null, (_, _) =>
        {
            ShowUrl("https://a.example/1");
            ShowUrl("https://b.example/2");
            ShowText("三つ目");
        });
        menu.Items.Add("-");
        menu.Items.Add("終了", null, (_, _) => Quit());

        _icon = new Forms.NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "ShotLink（通知の試し）",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    void ShowUrl(string url)
    {
        var host = new Uri(url).IdnHost;
        Log($"出す：URL：{url}");
        new ToastContentBuilder()
            .AddArgument("kind", "url")
            .AddArgument("text", url)
            .AddText(host)
            .AddText(url)
            .AddAttributionText($"クリックで開く（試し {++_count}）")
            .Show();
    }

    void ShowText(string text)
    {
        Log($"出す：文字列：{text}");
        new ToastContentBuilder()
            .AddArgument("kind", "text")
            .AddArgument("text", text)
            .AddText("QR を読みました")
            .AddText(text)
            .AddAttributionText($"クリックでコピー（試し {++_count}）")
            .Show();
    }

    void Quit()
    {
        Log("終了");
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
        }

        Shutdown();
    }

    void Log(string message)
    {
        lock (this)
        {
            File.AppendAllText(_log, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
    }

    static Icon MakeIcon()
    {
        // 試しなので、QR の角の印に似せた絵をその場で描く
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            foreach (var (x, y) in new[] { (2, 2), (18, 2), (2, 18) })
            {
                g.FillRectangle(Brushes.Black, x, y, 12, 12);
                g.FillRectangle(Brushes.White, x + 2, y + 2, 8, 8);
                g.FillRectangle(Brushes.Black, x + 4, y + 4, 4, 4);
            }

            g.FillRectangle(Brushes.DodgerBlue, 19, 19, 11, 11);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}
