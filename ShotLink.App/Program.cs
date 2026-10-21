using Microsoft.Toolkit.Uwp.Notifications;
using ShotLink.Core;

namespace ShotLink.App;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var homeOverride = Environment.GetEnvironmentVariable(ShotLinkHome.EnvironmentVariable);
        var development = !string.IsNullOrWhiteSpace(homeOverride);

        // 通知の部品が登録を書くより前に、決まった名前を付ける（AppIdentity.Aumid の説明）
        AppIdentity.Apply(development);

        // 本物の起動だけが本番の保存先を使ってよい（ShotLinkHome.Resolve の説明）
        var home = ShotLinkHome.Resolve(homeOverride, allowProduction: true);
        Directory.CreateDirectory(home);

        using var instance = SingleInstance.TryAcquire(AppIdentity.InstanceName(home));
        if (instance is null)
        {
            // 2つ目：1つ目に設定の窓を開いてもらって終わる（仕様書 7章）。
            // 通知のクリックで起こされたときは、Windows が1つ目にクリックを渡すので、ここへは来ない
            return 0;
        }

        var app = new AppServices(home, development);
        Application.ThreadException += (_, e) => app.Log.Write($"画面のスレッドで例外：{e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => app.Log.Write($"止まる例外：{e.ExceptionObject}");

        ApplicationConfiguration.Initialize();
        // 手で起動したときだけ「起動しました」と知らせる。通知のクリックで起こされたときは、そのクリックの用事だけを済ませる
        var announceStart = !args.Contains(StartupRegistration.Argument) && !ToastNotificationManagerCompat.WasCurrentProcessToastActivated();
        using var tray = new TrayContext(app, instance, announceStart);
        Application.Run(tray);
        return 0;
    }
}
