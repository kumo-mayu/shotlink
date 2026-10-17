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
    readonly Action _openHistory;

    // 字下げは、親の項目が ON のときだけ意味を持つ項目（20 で1段、40 で2段）
    readonly CheckBox _openDirectly = new() { Text = "URL をブラウザで開く", AutoSize = true };
    readonly CheckBox _allowedOnly = new() { Text = "ホワイトリストに絞る（自分で指定したサイトのみそのまま開きます）", AutoSize = true, Margin = new Padding(20, 0, 3, 3) };
    readonly Button _editSites = new() { Text = "サイトを編集…", AutoSize = true, Margin = new Padding(40, 0, 3, 6) };
    IReadOnlyList<string> _allowedSites = [];
    readonly CheckBox _preventReopen = new() { Text = "同じ URL は連続して開かない", AutoSize = true, Margin = new Padding(20, 0, 3, 3) };
    readonly NumericUpDown _reopenSeconds = new() { Minimum = QrPolicy.MinReopenSeconds, Maximum = QrPolicy.MaxReopenSeconds, Width = 64 };
    readonly CheckBox _summarize = new() { Text = "一枚に複数ある場合に通知をまとめる", AutoSize = true };
    readonly NumericUpDown _summarizeThreshold = new() { Minimum = QrPolicy.MinSummarizeThreshold, Maximum = QrPolicy.MaxSummarizeThreshold, Width = 48 };
    readonly CheckBox _openWhenSummarized = new() { Text = "まとめた通知の URL もすべて開く", AutoSize = true, Margin = new Padding(20, 0, 3, 3) };
    readonly CheckBox _urlOnly = new() { Text = "URL のみ通知する", AutoSize = true };
    readonly CheckBox _silent = new() { Text = "サイレント通知にする", AutoSize = true };
    readonly CheckBox _watchVrchat = new() { Text = "VRChat の写真フォルダを監視する", AutoSize = true };
    readonly ListView _folders = new()
    {
        View = View.Details,
        CheckBoxes = true,
        FullRowSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        Height = 110,
    };
    readonly CheckBox _startWithWindows = new() { Text = "Windows の起動時に ShotLink を起動する", AutoSize = true };

    public SettingsForm(Settings settings, AppServices app, string vrchatFolder, bool notificationsBlocked,
        Action<Settings> save, Action unregisterAndExit, Action clearHistory, Action openHistory)
    {
        _original = settings;
        _app = app;
        _save = save;
        _unregisterAndExit = unregisterAndExit;
        _clearHistory = clearHistory;
        _openHistory = openHistory;

        Text = "ShotLink の設定";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        // 中身に合わせて伸び縮みさせると、組を畳むたびに横幅まで変わって落ち着かなかった（ユーザー 2026-10-17）。
        // 大きさは人がドラッグで決め、収まらない分はスクロールする
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont;

        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };
        _root = root;
        _scroll.Controls.Add(root);

        if (notificationsBlocked)
        {
            root.Controls.Add(Note("Windows の通知がオフのため、QR を読んでも通知は出ません。読んだ QR は、アイコンのメニューの「最近読んだ QR」から開けます。", warning: true));
        }

        // ---- QR を読んだとき ----
        var read = Group("QR を読んだとき");
        read.Controls.Add(_openDirectly);
        read.Controls.Add(Note("ON にすると、知らない人が見せた QR の URL も確かめずに開きます。"));
        read.Controls.Add(_allowedOnly);
        // 入力欄を窓の中に置くと、窓が 1080p の画面に収まらない高さになった（1074 px）。別の小さな窓で直す
        _editSites.Click += (_, _) => EditSites();
        read.Controls.Add(_editSites);
        read.Controls.Add(Row(_preventReopen, _reopenSeconds, "秒"));
        // 開き方の設定なので「開く」の下に置き、「開く」と「まとめる」が両方 ON のときだけ触れる。
        // 「まとめる」の子として下に置くと、通知の設定の下に開き方が来て意味の親子がずれた（ユーザー 2026-10-17）
        read.Controls.Add(_openWhenSummarized);
        // まとめるのは通知の話なので、ブラウザで開かないときも効く。「URL のみ通知する」と同じ段に置き、すぐ上の子と隣り合わせる
        read.Controls.Add(Row(_summarize, _summarizeThreshold, "件以上"));
        read.Controls.Add(_urlOnly);
        read.Controls.Add(_silent);
        foreach (var box in new[] { _openDirectly, _allowedOnly, _preventReopen, _summarize })
        {
            box.CheckedChanged += (_, _) => UpdateEnabled();
        }

        root.Controls.Add(read.Parent!);

        // ---- 見張る場所 ----
        var watch = Group("監視する場所");
        watch.Controls.Add(_watchVrchat);
        watch.Controls.Add(Note(vrchatFolder));

        watch.Controls.Add(new Label { Text = "追加で監視するフォルダ（チェックを付けるとサブフォルダも監視します）", AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
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

        var historyButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
        var openHistoryButton = new Button { Text = "履歴を開く", AutoSize = true };
        openHistoryButton.Click += (_, _) => _openHistory();
        // 消すと戻せないので、赤い字で「開く」と見分けやすくする
        var clearHistoryButton = new Button { Text = "履歴を消す", AutoSize = true, ForeColor = Color.FromArgb(0xC4, 0x2B, 0x1C) };
        clearHistoryButton.Click += (_, _) => ClearHistory();
        historyButtons.Controls.Add(openHistoryButton);
        historyButtons.Controls.Add(clearHistoryButton);
        other.Controls.Add(historyButtons);
        other.Controls.Add(Note("読んだ QR の中身は、このパソコンの中に 100 件まで残ります。"));

        var unregister = new Button { Text = "通知の登録を消して終了する", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        unregister.Click += (_, _) => Unregister();
        other.Controls.Add(unregister);
        other.Controls.Add(Note("ShotLink を消す前に押してください。次に起動すると、また登録されます。"));
        root.Controls.Add(other.Parent!);

        // ---- このアプリについて ----
        var about = Group("このアプリについて");
        about.Controls.Add(new Label { Text = $"ShotLink {Version}", AutoSize = true });
        about.Controls.Add(Note("VRChat Inc. とは関係のない、非公式のツールです。"));
        about.Controls.Add(Note("ShotLink はネットにつながりません。写真も QR の中身も、どこへも送りません。"));
        // 名称を使う画面ごとに載せる（デンソーウェーブの FAQ「登録商標文の掲載単位」）。文はその FAQ の和文の例のまま
        about.Controls.Add(Note("QRコードは株式会社デンソーウェーブの登録商標です。"));
        var notices = new LinkLabel { Text = "同梱しているライブラリのライセンス", AutoSize = true };
        notices.LinkClicked += (_, _) => ShowNotices();
        about.Controls.Add(notices);
        root.Controls.Add(about.Parent!);

        // ---- OK・キャンセル ----
        // スクロールしても OK・キャンセルはいつも見えるよう、スクロールの外の下に置く
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
        _buttons = buttons;
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
        Controls.Add(_scroll);
        Controls.Add(buttons);
        _scroll.BringToFront();
        AcceptButton = ok;
        CancelButton = cancel;

        Fill(settings);
        Load += (_, _) => FitToContent();
        _scroll.Resize += (_, _) => StretchFolders();
    }

    readonly Panel _scroll;
    readonly TableLayoutPanel _root;
    readonly FlowLayoutPanel _buttons;
    int _folderSlack = -1;

    /// <summary>
    /// 最初は、全部の組を開いた中身がちょうど入る大きさにする（画面より高ければ画面に収める）。
    /// それより狭くすると字が切れるので、横はそこまでしか縮めさせない
    /// </summary>
    void FitToContent()
    {
        var content = _root.PreferredSize;
        var width = content.Width + SystemInformation.VerticalScrollBarWidth + Padding.Horizontal;
        var height = content.Height + _buttons.PreferredSize.Height + Padding.Vertical;
        var chrome = Size - ClientSize;
        var area = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(width, Math.Min(height, area.Height - chrome.Height));
        MinimumSize = new Size(Width, Math.Min(Height, LogicalToDeviceUnits(320)));
        CenterToScreen();
        _folderSlack = _scroll.ClientSize.Width - _folders.Width;
    }

    /// <summary>横に広げたら、フォルダの一覧も広げて長いパスを読みやすくする</summary>
    void StretchFolders()
    {
        if (_folderSlack >= 0)
        {
            _folders.Width = Math.Max(LogicalToDeviceUnits(440), _scroll.ClientSize.Width - _folderSlack);
            _folders.Columns[0].Width = _folders.ClientSize.Width - LogicalToDeviceUnits(4);
        }
    }

    static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Application.ProductVersion;

    void Fill(Settings s)
    {
        _openDirectly.Checked = s.OpenDirectly;
        _allowedOnly.Checked = s.OpenOnlyAllowedSites;
        _allowedSites = s.AllowedSites;
        ShowSiteCount();
        // 設定の名前は前の版のまま、画面では裏返して見せる（Settings の説明）
        _preventReopen.Checked = !s.ReopenSameUrl;
        _reopenSeconds.Value = Math.Clamp(s.ReopenSeconds, QrPolicy.MinReopenSeconds, QrPolicy.MaxReopenSeconds);
        _summarize.Checked = s.Summarize;
        _summarizeThreshold.Value = Math.Clamp(s.SummarizeThreshold, QrPolicy.MinSummarizeThreshold, QrPolicy.MaxSummarizeThreshold);
        _openWhenSummarized.Checked = s.OpenWhenSummarized;
        _urlOnly.Checked = !s.NotifyNonUrl;
        _silent.Checked = !s.PlaySound;
        UpdateEnabled();
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
        OpenOnlyAllowedSites = _allowedOnly.Checked,
        AllowedSites = _allowedSites,
        ReopenSameUrl = !_preventReopen.Checked,
        ReopenSeconds = (int)_reopenSeconds.Value,
        Summarize = _summarize.Checked,
        SummarizeThreshold = (int)_summarizeThreshold.Value,
        OpenWhenSummarized = _openWhenSummarized.Checked,
        NotifyNonUrl = !_urlOnly.Checked,
        PlaySound = !_silent.Checked,
        WatchVrchat = _watchVrchat.Checked,
        StartWithWindows = _startWithWindows.Checked,
        ExtraFolders = _folders.Items.Cast<ListViewItem>().Select(i => new ExtraFolder(i.Text, i.Checked)).ToArray(),
    };

    void UpdateEnabled()
    {
        var open = _openDirectly.Checked;
        _allowedOnly.Enabled = _preventReopen.Enabled = open;
        _editSites.Enabled = open && _allowedOnly.Checked;
        _reopenSeconds.Enabled = open && _preventReopen.Checked;
        _summarizeThreshold.Enabled = _summarize.Checked;
        _openWhenSummarized.Enabled = open && _summarize.Checked;
    }

    void ShowSiteCount() => _editSites.Text = $"サイトを編集…（{_allowedSites.Count} 件）";

    void EditSites()
    {
        using var form = new Form
        {
            Text = "ホワイトリスト",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            AutoScaleMode = AutoScaleMode.Dpi,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Font = Font,
        };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "そのまま開くサイトを、1行に1つ書きます。", AutoSize = true });
        var box = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            Width = 400,
            Height = 160,
            Text = string.Join("\r\n", _allowedSites),
        };
        layout.Controls.Add(box);
        layout.Controls.Add(Note("example.com と書くと、www.example.com なども開きます。一覧に無いサイトは、通知のクリックで開けます。", indent: 3));
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "キャンセル", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons);
        form.Controls.Add(layout);
        form.CancelButton = cancel;
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        // 書いたとおりではなく、比べる形（ホスト名）にそろえて見せる。何が効くのかを次に開いたときに分かるように
        _allowedSites = (new Settings { AllowedSites = box.Lines }).Normalized().AllowedSites;
        ShowSiteCount();
    }

    /// <summary>チェックと数の欄と単位を1行に並べる</summary>
    static FlowLayoutPanel Row(CheckBox box, NumericUpDown number, string unit)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = box.Margin };
        box.Margin = new Padding(0, 3, 0, 0);
        number.Margin = new Padding(6, 0, 3, 0);
        row.Controls.Add(box);
        row.Controls.Add(number);
        row.Controls.Add(new Label { Text = unit, AutoSize = true, Margin = new Padding(0, 4, 3, 0) });
        return row;
    }

    void AddFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "監視するフォルダを選んでください", UseDescriptionForTitle = true };
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

    /// <summary>
    /// 見出しを押すと中身を畳める組（ユーザーの案 2026-10-17）。項目が増えて窓が 1000 px を超えたので、
    /// 使わない組を畳んで窓を縮められるようにする。窓は中身に合わせて伸び縮みする（AutoSize）
    /// </summary>
    static FlowLayoutPanel Group(string title)
    {
        var outer = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        var header = new Button
        {
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Margin = Padding.Empty,
        };
        header.FlatAppearance.BorderSize = 0;
        header.Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold);
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(8, 0, 0, 0) };
        void Show() => header.Text = $"{(flow.Visible ? "▼" : "▶")} {title}";
        header.Click += (_, _) =>
        {
            flow.Visible = !flow.Visible;
            Show();
        };
        Show();
        outer.Controls.Add(header);
        outer.Controls.Add(flow);
        return flow;
    }

    static Label Note(string text, bool warning = false, int indent = 20) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(460 - indent, 0),
        ForeColor = warning ? Color.FromArgb(0xB0, 0x50, 0x00) : SystemColors.GrayText,
        Margin = new Padding(warning ? 3 : indent, 0, 3, 6),
    };
}
