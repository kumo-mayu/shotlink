using ShotLink.Core;

namespace ShotLink.App;

/// <summary>保存先とそこに置く物（仕様書 6章）。試しの起動では、全部が SHOTLINK_HOME の下になる</summary>
sealed class AppServices
{
    public AppServices(string home, bool development)
    {
        Home = home;
        Development = development;
        Log = new LogWriter(Path.Combine(home, "Logs"), SystemClock.Instance);
        SettingsStore = new JsonFileStore<Settings>(Path.Combine(home, "settings.json"), () => new Settings());
        History = new History(Path.Combine(home, "history.json"));
        Startup = new StartupRegistration(allowed: !development);

        // 試しの起動では、本物の VRChat の設定を読まず、保存先に置いた config.json を見る。
        // 無ければ既定の場所（ピクチャの下）になるので、確かめでは settings.json の watchVrchat を false にしておく
        VrchatConfigPath = development ? Path.Combine(home, "vrchat-config.json") : VrchatPhotoFolder.DefaultConfigPath;
    }

    public string Home { get; }

    /// <summary>SHOTLINK_HOME で起動した（試し・確かめ）。本物の登録（通知・Windows の起動時）に触らない</summary>
    public bool Development { get; }

    public LogWriter Log { get; }
    public JsonFileStore<Settings> SettingsStore { get; }
    public History History { get; }
    public StartupRegistration Startup { get; }
    public string VrchatConfigPath { get; }
}
