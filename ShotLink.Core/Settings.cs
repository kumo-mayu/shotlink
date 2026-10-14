namespace ShotLink.Core;

/// <summary>設定（仕様書 6章）。settings.json にそのまま書く。手で開いて直せるよう、名前は意味の分かる英語にする</summary>
public sealed record Settings
{
    /// <summary>URL を直接開く（既定 OFF。仕様書 4章 [決定]）</summary>
    public bool OpenDirectly { get; init; }

    /// <summary>1枚に 4 件以上あって通知をまとめたときも直接開く（既定 OFF。仕様書 3.5）</summary>
    public bool OpenWhenSummarized { get; init; }

    /// <summary>URL 以外の QR も知らせる</summary>
    public bool NotifyNonUrl { get; init; } = true;

    /// <summary>VRChat の写真フォルダを見張る</summary>
    public bool WatchVrchat { get; init; } = true;

    /// <summary>追加で見張るフォルダ（仕様書 3.1 [決定]）</summary>
    public IReadOnlyList<ExtraFolder> ExtraFolders { get; init; } = [];

    /// <summary>Windows の起動時に起動する。実際の登録はレジストリの Run で、これは画面に出すための控え</summary>
    public bool StartWithWindows { get; init; }

    /// <summary>通知の音を鳴らす</summary>
    public bool PlaySound { get; init; } = true;

    public DispatchOptions ToDispatchOptions() => new(OpenDirectly, NotifyNonUrl, OpenWhenSummarized);

    /// <summary>手で直した JSON の null の一覧・空の行を、空として受ける</summary>
    public Settings Normalized() => this with
    {
        ExtraFolders = (ExtraFolders ?? []).Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Path)).ToArray(),
    };
}

/// <param name="IncludeSubfolders">既定 OFF。ピクチャ全体などを指定されたときに重くならないように（仕様書 3.1）</param>
public sealed record ExtraFolder(string Path, bool IncludeSubfolders = false);
