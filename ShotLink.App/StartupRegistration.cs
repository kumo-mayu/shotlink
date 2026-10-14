using Microsoft.Win32;

namespace ShotLink.App;

/// <summary>Windows の起動時に起動する（仕様書 6章。HKCU の Run に登録）</summary>
sealed class StartupRegistration(bool allowed)
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "ShotLink";

    /// <summary>試しの起動（SHOTLINK_HOME あり）では触らない。本物の起動の一覧を書き換えてしまう</summary>
    public bool Allowed => allowed;

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public void Set(bool enabled)
    {
        if (!allowed)
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>exe を別の場所へ移したあと、登録が古い場所を指していたら直す</summary>
    public void Refresh()
    {
        if (!allowed)
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string current && current != Command)
        {
            key.SetValue(ValueName, Command);
        }
    }

    static string Command => $"\"{Environment.ProcessPath}\"";
}
