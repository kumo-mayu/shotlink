using System.Diagnostics;
using ShotLink.Core;

namespace ShotLink.App;

/// <summary>
/// 常駐の本体。通知領域のアイコンとメニュー、見張り・写真の処理・通知をつなぐ（仕様書 3・5・7章）。
/// 見張りと写真の処理は裏のスレッドで動き、画面に触る処理は必ず _ui へ Post して画面のスレッドで行う
/// </summary>
sealed class TrayContext : ApplicationContext, IQrSink
{
    /// <summary>無かったフォルダができたかを見る間隔。数秒遅れて見張り始めても、その間の写真は撮った直後でなければ困らない</summary>
    static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    readonly SynchronizationContext _ui;
    readonly AppServices _app;
    readonly NotifyIcon _icon;
    readonly ContextMenuStrip _menu = new();
    readonly System.Windows.Forms.Timer _retry = new() { Interval = (int)RetryInterval.TotalMilliseconds };
    readonly PhotoWatch _watch;
    readonly PhotoProcessor _processor;
    readonly Toasts _toasts;
    readonly UrlOpener _opener = UrlOpener.CreateDefault();
    readonly Dictionary<IconBadge, Icon> _icons = [];
    HashSet<WatchTarget> _missingNotified = [];
    Settings _settings;
    SettingsForm? _settingsForm;
    QrListForm? _listForm;
    bool _notificationsBlocked;

    public TrayContext(AppServices app, SingleInstance instance)
    {
        // 画面の部品が1つも無いうちは、Windows Forms の受け渡し先が入っていない。先に入れてから捕まえる
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _ui = SynchronizationContext.Current!;
        _app = app;
        _settings = app.SettingsStore.Load().Normalized();

        _toasts = new Toasts(app.Log.Write) { PlaySound = _settings.PlaySound };
        _toasts.Activated += (kind, text) => _ui.Post(_ => OnToastClicked(kind, text), null);

        _processor = new PhotoProcessor(FileReadyWaiter.CreateDefault(), new WinRtImageLoader(app.Log.Write), new QrReader(),
            QrDispatcher.CreateDefault(SystemClock.Instance), () => Volatile.Read(ref _settings).ToDispatchOptions(), this, app.Log.Write);

        _watch = new PhotoWatch(new FileSystemFolderWatcherFactory(), SystemClock.Instance);
        _watch.PhotoAppeared += path =>
        {
            app.Log.Write($"写真：{Path.GetFileName(path)}");
            _processor.Enqueue(path);
        };
        _watch.StateChanged += () => _ui.Post(_ => UpdateState(), null);

        _icon = new NotifyIcon { ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => OpenSettings();
        _menu.Opening += (_, _) => BuildMenu();

        _retry.Tick += (_, _) => _watch.RetryMissing();
        _retry.Start();

        instance.Listen(() => _ui.Post(_ => OpenSettings(), null));
        app.Startup.Refresh();

        ApplyTargets();
        CheckNotifications();
        app.Log.Write($"起動：版 {Application.ProductVersion}・保存先 {app.Home}");
    }

    // ---- 写真の処理から（裏のスレッド） ----

    public void Handle(QrBatch batch, string photo)
    {
        _ui.Post(_ =>
        {
            // 履歴は新しい物を先頭に足すので、位置の順の最後から足す。メニューと一覧で写真の中の順に並ぶ
            var now = DateTimeOffset.Now;
            foreach (var decision in batch.Reverse())
            {
                _app.History.Add(new HistoryEntry(decision.Content.Text, now, Path.GetFileName(photo)));
            }

            if (batch.Summarized)
            {
                foreach (var decision in batch.Where(d => d.Action == QrAction.Opened))
                {
                    _opener.TryOpen(decision.Content);
                }

                _toasts.ShowBatch(batch, photo);
                return;
            }

            foreach (var decision in batch)
            {
                Show(decision);
            }
        }, null);
    }

    void Show(QrDecision decision)
    {
        switch (decision.Action)
        {
            case QrAction.Opened:
                if (!_opener.TryOpen(decision.Content))
                {
                    // 振り分けで URL と決めたのに開く直前の確かめで落ちることは起きないはず。起きたら開かずに知らせる
                    _toasts.ShowQr(decision with { Action = QrAction.NotifyToOpen });
                    return;
                }

                _toasts.ShowQr(decision);
                break;
            case QrAction.NotifyToOpen:
            case QrAction.NotifyToCopy:
                _toasts.ShowQr(decision);
                break;
        }
    }

    // ---- 画面のスレッド ----

    void OnToastClicked(ToastKind kind, string text)
    {
        switch (kind)
        {
            case ToastKind.Url:
                // 通知に書いた中身から振り分け直してから開く（http / https だけ。仕様書 4章）
                if (!_opener.TryOpen(QrClassifier.Classify(text)))
                {
                    Copy(text);
                }

                break;
            case ToastKind.Text:
                Copy(text);
                break;
            case ToastKind.Settings:
                OpenSettings();
                break;
            case ToastKind.List:
                var fromPhoto = _app.History.Recent(History.Capacity).Where(e => e.Photo == text).ToList();
                // ShotLink を起動し直したあとや履歴を消したあとは、その写真の分が無い。そのときは履歴の全部を見せる
                OpenList(fromPhoto.Count > 0 ? $"QR の一覧（{text}）" : "読んだ QR の一覧", fromPhoto.Count > 0 ? fromPhoto : _app.History.Recent(History.Capacity));
                break;
        }
    }

    void OpenRecent(string text)
    {
        var content = QrClassifier.Classify(text);
        if (content.Kind == QrKind.WebUrl)
        {
            _opener.TryOpen(content);
        }
        else
        {
            Copy(text);
        }
    }

    void Copy(string text)
    {
        try
        {
            // ほかのソフトがクリップボードを掴んでいると失敗するので、少し待って何度か試す
            Clipboard.SetDataObject(text, copy: true, retryTimes: 5, retryDelay: 100);
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.ExternalException or ThreadStateException)
        {
            _app.Log.Write($"コピーできない：{e.GetType().Name}");
        }
    }

    void BuildMenu()
    {
        _menu.Items.Clear();

        var recent = new ToolStripMenuItem("最近読んだ QR");
        var entries = _app.History.Recent(History.MenuCount);
        if (entries.Count == 0)
        {
            recent.DropDownItems.Add(new ToolStripMenuItem("（まだありません）") { Enabled = false });
        }

        foreach (var entry in entries)
        {
            var isUrl = QrClassifier.Classify(entry.Text).Kind == QrKind.WebUrl;
            var item = new ToolStripMenuItem(ToastText.MenuLabel(entry.Text))
            {
                ToolTipText = isUrl ? "クリックで開きます" : "クリックでコピーします",
            };
            var text = entry.Text;
            item.Click += (_, _) => OpenRecent(text);
            recent.DropDownItems.Add(item);
        }

        recent.DropDownItems.Add(new ToolStripSeparator());
        recent.DropDownItems.Add("すべて表示…", null, (_, _) => OpenList("読んだ QR の一覧", _app.History.Recent(History.Capacity)));
        _menu.Items.Add(recent);
        _menu.Items.Add(new ToolStripSeparator());

        var openDirectly = new ToolStripMenuItem("URL を直接開く") { Checked = _settings.OpenDirectly, CheckOnClick = true };
        openDirectly.Click += (_, _) => SaveSettings(_settings with { OpenDirectly = openDirectly.Checked });
        _menu.Items.Add(openDirectly);

        var pause = new ToolStripMenuItem(_watch.IsPaused ? "見張りを再開" : "見張りを一時停止");
        pause.Click += (_, _) =>
        {
            if (_watch.IsPaused)
            {
                _watch.Resume();
                _app.Log.Write("見張りを再開");
            }
            else
            {
                _watch.Pause();
                _app.Log.Write("見張りを一時停止");
            }

            UpdateState();
        };
        _menu.Items.Add(pause);

        var folders = _watch.States.Keys.Select(t => t.Path).ToList();
        if (folders.Count == 1)
        {
            var open = new ToolStripMenuItem("写真フォルダを開く");
            open.Click += (_, _) => OpenFolder(folders[0]);
            _menu.Items.Add(open);
        }
        else if (folders.Count > 1)
        {
            var open = new ToolStripMenuItem("写真フォルダを開く");
            foreach (var folder in folders)
            {
                var item = new ToolStripMenuItem(folder.Replace("&", "&&"));
                item.Click += (_, _) => OpenFolder(folder);
                open.DropDownItems.Add(item);
            }

            _menu.Items.Add(open);
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("設定…", null, (_, _) => OpenSettings());
        _menu.Items.Add("終了", null, (_, _) => ExitThread());
    }

    void OpenFolder(string folder)
    {
        // 開くのはフォルダだけ。設定に書かれた場所がファイルや実行ファイルだったときに、それを起動しないように
        if (!Directory.Exists(folder))
        {
            _toasts.ShowSettingsHint("フォルダが見つかりません", folder);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { folder }, UseShellExecute = false })?.Dispose();
    }

    void OpenList(string title, IReadOnlyList<HistoryEntry> entries)
    {
        _listForm?.Close();
        _listForm = new QrListForm(title, entries, text => OpenRecent(text), Copy);
        _listForm.Show();
        _listForm.Activate();
    }

    void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        CheckNotifications();
        _settingsForm = new SettingsForm(_settings, _app, VrchatFolder(), _notificationsBlocked, SaveSettings, UnregisterAndExit, ClearHistory);
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    void SaveSettings(Settings settings)
    {
        settings = settings.Normalized();
        Volatile.Write(ref _settings, settings);
        _app.SettingsStore.Save(settings);
        _toasts.PlaySound = settings.PlaySound;
        _app.Startup.Set(settings.StartWithWindows);
        ApplyTargets();
    }

    void ClearHistory()
    {
        _app.History.Clear();
        _listForm?.Close();
        _app.Log.Write("履歴を消した");
    }

    void UnregisterAndExit()
    {
        Toasts.Unregister();
        _app.Log.Write("通知の登録を消して終了");
        ExitThread();
    }

    void ApplyTargets()
    {
        var targets = new List<WatchTarget>();
        if (_settings.WatchVrchat)
        {
            // VRChat は月ごとのフォルダを作るので、サブフォルダも見る（仕様書 3.1）
            targets.Add(new WatchTarget(VrchatFolder(), IncludeSubfolders: true));
        }

        targets.AddRange(_settings.ExtraFolders.Select(f => new WatchTarget(f.Path, f.IncludeSubfolders)));
        _watch.SetTargets(targets);
        UpdateState();
    }

    string VrchatFolder() => VrchatPhotoFolder.Resolve(_app.VrchatConfigPath, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

    void CheckNotifications()
    {
        _notificationsBlocked = _toasts.IsBlocked(out var reason);
        _app.Log.Write($"Windows の通知：{reason}");
        UpdateState();
    }

    void UpdateState()
    {
        var states = _watch.States;
        var missing = states.Where(p => p.Value == WatchState.Missing).Select(p => p.Key).ToList();

        // フォルダが無くなったと知らせるのは、無くなったときに1回だけ。5 秒ごとの見直しのたびには出さない
        foreach (var target in missing.Where(t => !_missingNotified.Contains(t)))
        {
            _app.Log.Write($"フォルダが無い：{target.Path}");
            _toasts.ShowSettingsHint("見張るフォルダが見つかりません", $"{target.Path}\nできたら自動で見張り始めます");
        }

        _missingNotified = missing.ToHashSet();

        string tip;
        IconBadge badge;
        if (_watch.IsPaused)
        {
            (badge, tip) = (IconBadge.Paused, "ShotLink：一時停止中");
        }
        else if (states.Count == 0)
        {
            (badge, tip) = (IconBadge.Warning, "ShotLink：見張るフォルダがありません");
        }
        else if (missing.Count > 0)
        {
            (badge, tip) = (IconBadge.Warning, $"ShotLink：見つからないフォルダが {missing.Count} 件あります");
        }
        else if (_notificationsBlocked)
        {
            (badge, tip) = (IconBadge.Warning, "ShotLink：Windows の通知がオフです。読んだ QR はメニューの「最近読んだ QR」から開けます");
        }
        else
        {
            (badge, tip) = (IconBadge.None, "ShotLink：見張っています");
        }

        _icon.Icon = IconFor(badge);

        // ツールチップは 127 字まで（それを超えると例外になる）
        _icon.Text = tip.Length <= 127 ? tip : tip[..126] + "…";
    }

    Icon IconFor(IconBadge badge)
    {
        if (!_icons.TryGetValue(badge, out var icon))
        {
            // 通知領域の大きさは表示の拡大率で変わる（100% で 16 px、150% で 24 px）
            using var bmp = IconArt.Draw(SystemInformation.SmallIconSize.Width, badge);
            _icons[badge] = icon = IconArt.ToIcon(bmp);
        }

        return icon;
    }

    protected override void ExitThreadCore()
    {
        _retry.Stop();
        _icon.Visible = false;
        _watch.Dispose();
        _processor.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
        _toasts.Dispose();
        _settingsForm?.Close();
        _listForm?.Close();
        _app.Log.Write("終了");
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Dispose();
            _menu.Dispose();
            _retry.Dispose();
            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
