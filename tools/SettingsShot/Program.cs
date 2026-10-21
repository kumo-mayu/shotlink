using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using ShotLink.Core;

// 使い方：SettingsShot <書き出す PNG> [倍率]
// 設定の窓は ShotLink.App の内側の型なので、作りを変えずにリフレクションで組む
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "settings.png");
var scale = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 1f;

ApplicationConfiguration.Initialize();

var appAssembly = typeof(ShotLink.App.IconArt).Assembly;
const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

// 保存先は使い捨ての作業用。本番の %LOCALAPPDATA%\ShotLink には触れない。開発用にすると「Windows の起動時に起動する」が
// 「確かめ用の起動のため、変更できません」になるので、本番の扱いにする（窓は OK を押さないので、何も書かない）
var home = Path.Combine(Path.GetTempPath(), "ShotLink-SettingsShot");
Directory.CreateDirectory(home);
var app = Activator.CreateInstance(appAssembly.GetType("ShotLink.App.AppServices")!, Any, null, [home, false], null)!;

// 例の設定。パスに本物のユーザー名が出ないよう、VRChat のフォルダも例の文字にする
var settings = new Settings
{
    WatchVrchat = true,
    ExtraFolders = [new ExtraFolder(@"D:\Screenshots\Steam", IncludeSubfolders: true)],
};
var form = (Form)Activator.CreateInstance(appAssembly.GetType("ShotLink.App.SettingsForm")!, Any, null,
    [settings, app, @"C:\Users\User\Pictures\VRChat", false, (Action<Settings>)(_ => { }), (Action)(() => { }), (Action)(() => { }), (Action)(() => { })],
    null)!;

// 画面には出さずに組む（Load で大きさが決まるので、一度 Show する）
form.Opacity = 0;
form.ShowInTaskbar = false;
form.Show();
Application.DoEvents();

if (scale != 1f)
{
    // 画面の倍率は変えられないので、倍率が変わったという知らせ（WM_DPICHANGED）を窓に送り、Windows Forms に
    // その倍率で組み直させる。字・余白・チェックボックスまで、本物の高い倍率の画面と同じ作りになる
    var dpi = (int)Math.Round(96 * scale);
    var bounds = form.Bounds;
    var suggested = new Native.Rect { Left = bounds.Left, Top = bounds.Top, Right = bounds.Left + (int)(bounds.Width * scale), Bottom = bounds.Top + (int)(bounds.Height * scale) };
    var before = All(form).ToDictionary(c => c, c => c.Font.Size);
    Native.SendMessage(form.Handle, 0x02E0, (nint)((dpi << 16) | dpi), ref suggested);
    Application.DoEvents();

    // 見出しのように自分で字を決めた部品は、知らせだけでは字が大きくならない。変わらなかった物だけ倍にする
    foreach (var (c, size) in before)
    {
        if (c.Font.Size == size)
        {
            c.Font = new Font(c.Font.FontFamily, size * scale, c.Font.Style, c.Font.Unit);
        }
    }

    Application.DoEvents();
}

static IEnumerable<Control> All(Control parent) =>
    parent.Controls.Cast<Control>().SelectMany(c => All(c).Prepend(c));

// 中身は全部見せる。窓の高さは画面に縛られるので、中身（_root）とボタン（_buttons）を別々に描いて並べる
var root = (Control)form.GetType().GetField("_root", Any)!.GetValue(form)!;
var buttons = (Control)form.GetType().GetField("_buttons", Any)!.GetValue(form)!;
var pad = form.Padding;
root.Size = root.PreferredSize with { Width = Math.Max(root.Width, root.PreferredSize.Width) };
buttons.Width = root.Width;
buttons.PerformLayout();
Application.DoEvents();

var titleHeight = (int)(32 * scale);
var width = root.Width + pad.Horizontal;
var height = titleHeight + pad.Top + root.Height + buttons.Height + pad.Bottom;
using var bmp = new Bitmap(width, height);
using (var g = Graphics.FromImage(bmp))
{
    g.Clear(form.BackColor);
    DrawTitle(g, width, titleHeight, form.Text);
    Draw(root, new Point(pad.Left, titleHeight + pad.Top));
    Draw(buttons, new Point(pad.Left, titleHeight + pad.Top + root.Height));

    void Draw(Control c, Point at)
    {
        using var part = new Bitmap(c.Width, c.Height);
        c.DrawToBitmap(part, new Rectangle(Point.Empty, c.Size));
        g.DrawImage(part, at);
    }

    using var border = new Pen(Color.FromArgb(0xBB, 0xBB, 0xBB));
    g.DrawRectangle(border, 0, 0, width - 1, height - 1);
}

bmp.Save(output, System.Drawing.Imaging.ImageFormat.Png);
Console.WriteLine($"{output} {width}x{height}");
form.Close();
return;

// 窓の題の帯は DrawToBitmap では描けない（Windows が描く部分）ので、Windows 11 の見た目に似せて描く
void DrawTitle(Graphics g, int w, int h, string text)
{
    using var back = new SolidBrush(Color.FromArgb(0xF3, 0xF3, 0xF3));
    g.FillRectangle(back, 0, 0, w, h);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

    var icon = (int)(16 * scale);
    var margin = (int)(10 * scale);
    using (var art = ShotLink.App.IconArt.Draw(icon))
    {
        g.DrawImage(art, margin, (h - icon) / 2);
    }

    using var font = new Font(SystemFonts.CaptionFont!.FontFamily, 9f * scale);
    TextRenderer.DrawText(g, text, font, new Point(margin * 2 + icon, (h - font.Height) / 2), Color.Black);

    // 閉じるボタンの「×」
    var x = w - (int)(23 * scale);
    var y = h / 2;
    var r = (int)(5 * scale);
    using var pen = new Pen(Color.Black, Math.Max(1f, scale));
    g.DrawLine(pen, x - r, y - r, x + r, y + r);
    g.DrawLine(pen, x - r, y + r, x + r, y - r);
}

static class Native
{
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern nint SendMessage(nint hWnd, int msg, nint wParam, ref Rect lParam);
}
