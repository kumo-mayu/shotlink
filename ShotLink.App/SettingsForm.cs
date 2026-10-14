using System.Reflection;
using ShotLink.Core;

namespace ShotLink.App;

/// <summary>設定の窓（仕様書 6章）。小さな窓1枚。「OK」で保存し、すぐに効かせる</summary>
sealed class SettingsForm : Form
{
    readonly Settings _original;
    readonly AppServices _app;
    readonly Action<Settings> _save;
    readonly Action _unregisterAndExit;
    readonly Action _clearHistory;

    readonly CheckBox _openDirectly = new() { Text = "URL を直接ブラウザで開く", AutoSize = true };
    readonly CheckBox _openWhenSummarized = new() { Text = "1枚に 4 件以上あって通知をまとめたときも、全部開く", AutoSize = true, Margin = new Padding(20, 0, 3, 3) };
    readonly CheckBox _notifyNonUrl = new() { Text = "URL 以外の QR も知らせる", AutoSize = true };
    readonly CheckBox _playSound = new() { Text = "通知の音を鳴らす", AutoSize = true };
    readonly CheckBox _watchVrchat = new() { Text = "VRChat の写真フォルダを見張る", AutoSize = true };
    readonly ListView _folders = new()
    {
        View = View.Details,
        CheckBoxes = true,
        FullRowSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        Height = 110,
        Dock = DockStyle.Fill,
    };
    readonly CheckBox _startWithWindows = new() { Text = "Windows の起動時に ShotLink を起動する", AutoSize = true };

    public SettingsForm(Settings settings, AppServices app, string vrchatFolder, bool notificationsBlocked,
        Action<Settings> save, Action unregisterAndExit, Action clearHistory)
    {
        _original = settings;
        _app = app;
        _save = save;
        _unregisterAndExit = unregisterAndExit;
        _clearHistory = clearHistory;

        Text = "ShotLink の設定";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont;

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        Controls.Add(root);

        if (notificationsBlocked)
        {
            root.Controls.Add(Note("Windows の通知がオフのため、QR を読んでも通知は出ません。読んだ QR は、アイコンのメニューの「最近読んだ QR」から開けます。", warning: true));
        }

        // ---- QR を読んだとき ----
        var read = Group("QR を読んだとき");
        read.Controls.Add(_openDirectly);
        read.Controls.Add(Note("ON にすると、知らない人が見せた QR の URL も確かめずに開きます。"));
        read.Controls.Add(_openWhenSummarized);
        _openDirectly.CheckedChanged += (_, _) => _openWhenSummarized.Enabled = _openDirectly.Checked;
        read.Controls.Add(_notifyNonUrl);
        read.Controls.Add(_playSound);
        root.Controls.Add(read.Parent!);

        // ---- 見張る場所 ----
        var watch = Group("見張る場所");
        watch.Controls.Add(_watchVrchat);
        watch.Controls.Add(Note(vrchatFolder));

        watch.Controls.Add(new Label { Text = "追加で見張るフォルダ（チェックを付けるとサブフォルダも見張ります）", AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
        _folders.Columns.Add("フォルダ", 420);
        _folders.Width = 440;
        watch.Controls.Add(_folders);

        var folderButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        var add = new Button { Text = "追加…", AutoSize = true };
        var remove = new Button { Text = "削除", AutoSize = true, Enabled = false };
        add.Click += (_, _) => AddFolder();
        remove.Click += (_, _) =>
        {
            foreach (ListViewItem item in _folders.SelectedItems)
            {
                _folders.Items.Remove(item);
            }
        };
        _folders.SelectedIndexChanged += (_, _) => remove.Enabled = _folders.SelectedItems.Count > 0;
        folderButtons.Controls.Add(add);
        folderButtons.Controls.Add(remove);
        watch.Controls.Add(folderButtons);
        watch.Controls.Add(Note("ダウンロードなど、ほかの人の画像が入るフォルダを選ぶと、その画像の QR も読みます。"));
        watch.Controls.Add(Note("ネットワーク上のフォルダを選ぶと、ShotLink はそこへアクセスします。"));
        root.Controls.Add(watch.Parent!);

        // ---- そのほか ----
        var other = Group("そのほか");
        other.Controls.Add(_startWithWindows);
        if (!app.Startup.Allowed)
        {
            _startWithWindows.Enabled = false;
            other.Controls.Add(Note("確かめ用の起動のため、変更できません。"));
        }

        var clearHistoryButton = new Button { Text = "履歴を消す", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        clearHistoryButton.Click += (_, _) => ClearHistory();
        other.Controls.Add(clearHistoryButton);
        other.Controls.Add(Note("読んだ QR の中身は、このパソコンの中に 100 件まで残ります。"));

        var unregister = new Button { Text = "Windows への通知の登録を消して終了", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        unregister.Click += (_, _) => Unregister();
        other.Controls.Add(unregister);
        other.Controls.Add(Note("ShotLink を消す前に押してください。次に起動すると、また登録されます。"));
        root.Controls.Add(other.Parent!);

        // ---- このアプリについて ----
        var about = Group("このアプリについて");
        about.Controls.Add(new Label { Text = $"ShotLink {Version}", AutoSize = true });
        about.Controls.Add(Note("VRChat Inc. とは関係のない、非公式のツールです。"));
        about.Controls.Add(Note("ShotLink はネットにつながりません。写真も QR の中身も、どこへも送りません。"));
        var notices = new LinkLabel { Text = "同梱しているライブラリのライセンス", AutoSize = true };
        notices.LinkClicked += (_, _) => ShowNotices();
        about.Controls.Add(notices);
        root.Controls.Add(about.Parent!);

        // ---- OK・キャンセル ----
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "キャンセル", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "OK", AutoSize = true };
        ok.Click += (_, _) =>
        {
            _save(Collect());
            Close();
        };
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Fill(settings);
    }

    static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Application.ProductVersion;

    void Fill(Settings s)
    {
        _openDirectly.Checked = s.OpenDirectly;
        _openWhenSummarized.Checked = s.OpenWhenSummarized;
        _openWhenSummarized.Enabled = s.OpenDirectly;
        _notifyNonUrl.Checked = s.NotifyNonUrl;
        _playSound.Checked = s.PlaySound;
        _watchVrchat.Checked = s.WatchVrchat;
        _startWithWindows.Checked = _app.Startup.Allowed ? _app.Startup.IsEnabled() : s.StartWithWindows;
        foreach (var f in s.ExtraFolders)
        {
            _folders.Items.Add(new ListViewItem(f.Path) { Checked = f.IncludeSubfolders });
        }
    }

    Settings Collect() => _original with
    {
        OpenDirectly = _openDirectly.Checked,
        OpenWhenSummarized = _openWhenSummarized.Checked,
        NotifyNonUrl = _notifyNonUrl.Checked,
        PlaySound = _playSound.Checked,
        WatchVrchat = _watchVrchat.Checked,
        StartWithWindows = _startWithWindows.Checked,
        ExtraFolders = _folders.Items.Cast<ListViewItem>().Select(i => new ExtraFolder(i.Text, i.Checked)).ToArray(),
    };

    void AddFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "見張るフォルダを選んでください", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var path = dialog.SelectedPath;
        if (_folders.Items.Cast<ListViewItem>().Any(i => string.Equals(i.Text, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _folders.Items.Add(new ListViewItem(path));
    }

    void ClearHistory()
    {
        var answer = MessageBox.Show(this, "読んだ QR の履歴を全部消します。元に戻せません。", "ShotLink", MessageBoxButtons.OKCancel, MessageBoxIcon.None);
        if (answer == DialogResult.OK)
        {
            _clearHistory();
        }
    }

    void Unregister()
    {
        var answer = MessageBox.Show(this,
            "Windows の通知の一覧から ShotLink を消して、ShotLink を終了します。",
            "ShotLink", MessageBoxButtons.OKCancel, MessageBoxIcon.None);
        if (answer == DialogResult.OK)
        {
            Close();
            _unregisterAndExit();
        }
    }

    void ShowNotices()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("THIRD-PARTY-NOTICES.txt");
        var text = stream is null ? "" : new StreamReader(stream).ReadToEnd();
        using var form = new Form
        {
            Text = "同梱しているライブラリのライセンス",
            Width = 720,
            Height = 560,
            StartPosition = FormStartPosition.CenterParent,
            Font = Font,
        };
        form.Controls.Add(new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, Font.Size),
            Text = text.ReplaceLineEndings("\r\n"),
        });
        form.ShowDialog(this);
    }

    static FlowLayoutPanel Group(string title)
    {
        var box = new GroupBox { Text = title, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(0, 0, 0, 8) };
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        box.Controls.Add(flow);
        return flow;
    }

    static Label Note(string text, bool warning = false) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(440, 0),
        ForeColor = warning ? Color.FromArgb(0xB0, 0x50, 0x00) : SystemColors.GrayText,
        Margin = new Padding(warning ? 3 : 20, 0, 3, 6),
    };
}
