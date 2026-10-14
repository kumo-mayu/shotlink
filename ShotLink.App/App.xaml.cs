using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ShotLink.App;

/// <summary>
/// 通知の作り方を決めるための試し（仕様書 8章 [要確認]・12章）。
/// Windows Forms の NotifyIcon の吹き出し（Windows 10/11 ではトーストとして出る）で、
/// 「通知をクリックしたら動く」が満たせるかを、出ている間と、通知センターへ入ったあとの両方で確かめる。
/// 満たせれば部品を足さずに済み、exe も大きくならない
/// </summary>
public partial class App : System.Windows.Application
{
    Forms.NotifyIcon? _icon;
    string _log = "";
    string _lastShown = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 本番の保存先に書かないよう、SHOTLINK_HOME が無ければ一時フォルダに書く（CLAUDE.md「本番の設定と履歴に触らない」）
        var home = Environment.GetEnvironmentVariable("SHOTLINK_HOME")
                   ?? Path.Combine(Path.GetTempPath(), "ShotLink-spike");
        Directory.CreateDirectory(home);
        _log = Path.Combine(home, "notify-spike.log");
        Log("起動");

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("テスト通知（URL）", null, (_, _) => Show("booth.pm", "https://booth.pm/ja/items/1234567\nクリックで開く"));
        menu.Items.Add("テスト通知（文字列）", null, (_, _) => Show("QR を読みました", "こんにちは\nクリックでコピー"));
        menu.Items.Add("-");
        menu.Items.Add("終了", null, (_, _) => Quit());

        _icon = new Forms.NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "ShotLink（通知の試し）",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.BalloonTipClicked += (_, _) => Log($"クリック：{_lastShown}");
        _icon.BalloonTipClosed += (_, _) => Log($"閉じた：{_lastShown}");
        _icon.BalloonTipShown += (_, _) => Log($"出た：{_lastShown}");

        Show("booth.pm", "https://booth.pm/ja/items/1234567\nクリックで開く");
    }

    void Show(string title, string text)
    {
        _lastShown = title;
        _icon!.ShowBalloonTip(10_000, title, text, Forms.ToolTipIcon.None);
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

    void Log(string message) =>
        File.AppendAllText(_log, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");

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
