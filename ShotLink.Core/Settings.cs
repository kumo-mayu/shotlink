namespace ShotLink.Core;

/// <summary>設定（仕様書 6章）。settings.json にそのまま書く。手で開いて直せるよう、名前は意味の分かる英語にする</summary>
public sealed record Settings
{
    /// <summary>URL を直接開く（既定 OFF。仕様書 4章 [決定]）</summary>
    public bool OpenDirectly { get; init; }

    /// <summary>直接開くのを、一覧のサイトだけに絞る（既定 OFF。仕様書 3.6）</summary>
    public bool OpenOnlyAllowedSites { get; init; }

    /// <summary>直接開いてよいサイトのホスト名。下位のドメインも含む（仕様書 3.6）</summary>
    public IReadOnlyList<string> AllowedSites { get; init; } = [];

    /// <summary>
    /// 同じ URL を続けて撮ったときも毎回開く（既定 OFF。仕様書 3.4）。画面では裏返して「同じ URL は連続して開かない」と見せる。
    /// 名前は前の版の settings.json をそのまま読むために変えない
    /// </summary>
    public bool ReopenSameUrl { get; init; }

    /// <summary>同じ URL をもう一度開くまでの秒数（既定 60。仕様書 3.4）</summary>
    public int ReopenSeconds { get; init; } = QrPolicy.DefaultReopenSeconds;

    /// <summary>1枚に複数あるとき、通知を1件にまとめる（既定 ON。仕様書 3.5）</summary>
    public bool Summarize { get; init; } = true;

    /// <summary>この件数以上でまとめる（既定 4。仕様書 3.5）</summary>
    public int SummarizeThreshold { get; init; } = QrPolicy.DefaultSummarizeThreshold;

    /// <summary>まとめたときも直接開く（既定 OFF。仕様書 3.5）</summary>
    public bool OpenWhenSummarized { get; init; }

    /// <summary>URL 以外の QR も知らせる。画面では裏返して「URL のみ通知する」と見せる</summary>
    public bool NotifyNonUrl { get; init; } = true;

    /// <summary>VRChat の写真フォルダを見張る</summary>
    public bool WatchVrchat { get; init; } = true;

    /// <summary>追加で見張るフォルダ（仕様書 3.1 [決定]）</summary>
    public IReadOnlyList<ExtraFolder> ExtraFolders { get; init; } = [];

    /// <summary>Windows の起動時に起動する。実際の登録はレジストリの Run で、これは画面に出すための控え</summary>
    public bool StartWithWindows { get; init; }

    /// <summary>通知の音を鳴らす。画面では裏返して「サイレント通知にする」と見せる</summary>
    public bool PlaySound { get; init; } = true;

    public DispatchOptions ToDispatchOptions() => new(OpenDirectly, NotifyNonUrl, OpenWhenSummarized, ReopenSameUrl,
        ReopenSeconds, Summarize ? SummarizeThreshold : null, OpenOnlyAllowedSites ? AllowedSites : null);

    /// <summary>手で直した JSON の null の一覧・空の行を空として受け、幅の外の数を幅に収める</summary>
    public Settings Normalized() => this with
    {
        ExtraFolders = (ExtraFolders ?? []).Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Path)).ToArray(),
        AllowedSites = (AllowedSites ?? []).Select(SiteAllowlist.Normalize).OfType<string>().Distinct(StringComparer.Ordinal).ToArray(),
        ReopenSeconds = Math.Clamp(ReopenSeconds, QrPolicy.MinReopenSeconds, QrPolicy.MaxReopenSeconds),
        SummarizeThreshold = Math.Clamp(SummarizeThreshold, QrPolicy.MinSummarizeThreshold, QrPolicy.MaxSummarizeThreshold),
    };
}

/// <param name="IncludeSubfolders">既定 OFF。ピクチャ全体などを指定されたときに重くならないように（仕様書 3.1）</param>
public sealed record ExtraFolder(string Path, bool IncludeSubfolders = false);
