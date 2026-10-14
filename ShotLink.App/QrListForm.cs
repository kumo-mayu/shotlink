using ShotLink.Core;

namespace ShotLink.App;

/// <summary>
/// 読んだ QR の一覧の窓。まとめた通知をクリックしたとき（その写真の分）と、
/// メニューの「すべて表示…」（履歴の全部）で開く（仕様書 3.5・5章）
/// </summary>
sealed class QrListForm : Form
{
    readonly ListView _list = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        Dock = DockStyle.Fill,
    };

    readonly Action<string> _open;
    readonly Action<string> _copy;
    readonly Button _openButton = new() { Text = "開く", AutoSize = true, Enabled = false };
    readonly Button _copyButton = new() { Text = "コピー", AutoSize = true, Enabled = false };

    public QrListForm(string title, IReadOnlyList<HistoryEntry> entries, Action<string> open, Action<string> copy)
    {
        _open = open;
        _copy = copy;

        Text = title;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Size = new Size(720, 420);
        MinimumSize = new Size(480, 260);
        Padding = new Padding(10);

        _list.Columns.Add("行き先", 160);
        _list.Columns.Add("中身", 360);
        _list.Columns.Add("読んだ時刻", 140);
        foreach (var entry in entries)
        {
            // URL は開く先のホスト名（xn-- のまま）を別の列に出す。中身の原文だけだと、見た目の似た偽のドメインに気づけない
            var host = QrClassifier.Classify(entry.Text).Host;
            var item = new ListViewItem([host ?? "（文字列）", ToastText.Flatten(entry.Text), entry.ReadAt.LocalDateTime.ToString("M/d HH:mm:ss")])
            {
                Tag = entry.Text,
                ToolTipText = entry.Text,
            };
            _list.Items.Add(item);
        }

        _list.ShowItemToolTips = true;
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.ItemActivate += (_, _) => Act(openIfUrl: true);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var close = new Button { Text = "閉じる", AutoSize = true, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();
        _openButton.Click += (_, _) => Act(openIfUrl: true);
        _copyButton.Click += (_, _) => Act(openIfUrl: false);
        buttons.Controls.Add(close);
        buttons.Controls.Add(_copyButton);
        buttons.Controls.Add(_openButton);
        CancelButton = close;

        var empty = new Label { Text = "読んだ QR はまだありません。", AutoSize = true, Dock = DockStyle.Top, Visible = entries.Count == 0 };
        Controls.Add(_list);
        Controls.Add(empty);
        Controls.Add(buttons);
    }

    void UpdateButtons()
    {
        var text = Selected();
        _copyButton.Enabled = text is not null;
        _openButton.Enabled = text is not null && QrClassifier.Classify(text).Kind == QrKind.WebUrl;
    }

    void Act(bool openIfUrl)
    {
        var text = Selected();
        if (text is null)
        {
            return;
        }

        // 開くのは http / https だけ。それ以外はダブルクリックでもコピーにする（仕様書 4章）
        if (openIfUrl && QrClassifier.Classify(text).Kind == QrKind.WebUrl)
        {
            _open(text);
        }
        else
        {
            _copy(text);
        }
    }

    string? Selected() => _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as string : null;
}
