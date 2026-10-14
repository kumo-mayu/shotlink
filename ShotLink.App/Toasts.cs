using Microsoft.Toolkit.Uwp.Notifications;
using ShotLink.Core;
using Windows.UI.Notifications;

namespace ShotLink.App;

public enum ToastKind
{
    /// <summary>クリックで URL を開く</summary>
    Url,

    /// <summary>クリックで中身をコピーする</summary>
    Text,

    /// <summary>クリックで設定の窓を開く（フォルダが無いなど）</summary>
    Settings,
}

/// <summary>トースト通知（仕様書 8章 [決定]）。クリックは裏のスレッドで届くので、受け取る側が画面のスレッドへ渡す</summary>
sealed class Toasts : IDisposable
{
    readonly Action<string> _log;

    public Toasts(Action<string> log)
    {
        _log = log;

        // 登録するのはここ。AppIdentity.Apply の後でないと、exe の置き場所ごとの登録になる
        ToastNotificationManagerCompat.OnActivated += OnActivated;
    }

    /// <summary>通知がクリックされた（裏のスレッド）</summary>
    public event Action<ToastKind, string>? Activated;

    public bool PlaySound { get; set; } = true;

    /// <summary>Windows の通知がオフか、ShotLink の通知だけ切られているか（仕様書 8章 [決定]）</summary>
    public bool IsBlocked(out string reason)
    {
        try
        {
            var setting = ToastNotificationManagerCompat.CreateToastNotifier().Setting;
            reason = setting.ToString();
            return setting != NotificationSetting.Enabled;
        }
        catch (Exception e)
        {
            reason = e.GetType().Name;
            return false;
        }
    }

    public void ShowQr(QrDecision decision)
    {
        var text = ToastText.For(decision);
        var kind = decision.Content.Kind == QrKind.WebUrl ? ToastKind.Url : ToastKind.Text;
        Show(kind, decision.Content.Text, text.Title, text.Body, text.Footer);
    }

    public void ShowSettingsHint(string title, string body) => Show(ToastKind.Settings, "", title, body, "クリックで設定を開きます");

    /// <summary>Windows への通知の登録を消す（仕様書 8章。exe を消す前に押してもらう）</summary>
    public static void Unregister() => ToastNotificationManagerCompat.Uninstall();

    public void Dispose() => ToastNotificationManagerCompat.OnActivated -= OnActivated;

    void Show(ToastKind kind, string payload, string title, string body, string footer)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddArgument("kind", kind.ToString())
                .AddArgument("text", payload)
                .AddText(title, hintMaxLines: 1)
                .AddText(body)
                .AddAttributionText(footer);
            if (!PlaySound)
            {
                builder.AddAudio(new ToastAudio { Silent = true });
            }

            builder.Show();

            // 出したことも残す。席を外している間の確かめで「出たか」をログで追えるように。中身は書かずホスト名まで（仕様書 7章）
            _log($"通知：{kind}：{(kind == ToastKind.Url ? title : "（中身は書かない）")}");
        }
        catch (Exception e)
        {
            // 通知が出せなくても見張りは続ける。履歴とメニューからは開ける
            _log($"通知を出せない：{e.GetType().Name}：{e.Message}");
        }
    }

    void OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        if (!args.TryGetValue("kind", out var kindText) || !Enum.TryParse<ToastKind>(kindText, out var kind))
        {
            return;
        }

        args.TryGetValue("text", out var text);
        Activated?.Invoke(kind, text ?? "");
    }
}
