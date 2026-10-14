using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShotLink.App;

public enum IconBadge
{
    None,

    /// <summary>一時停止中（灰色にして、右下に一時停止の印）</summary>
    Paused,

    /// <summary>見張るフォルダが無い・Windows の通知がオフ（右下に橙の「!」）</summary>
    Warning,
}

/// <summary>
/// アイコンの絵。QR の角の3つの印と、右下の青い点（リンク）。通知領域の状態ごとの絵はその場で描き、
/// exe のアイコン（ShotLink.ico）は tools/IconGen が同じ絵から作る
/// </summary>
public static class IconArt
{
    static readonly Color Ink = Color.FromArgb(0x1F, 0x2A, 0x44);
    static readonly Color Paper = Color.White;
    static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
    static readonly Color Muted = Color.FromArgb(0x8A, 0x8F, 0x98);
    static readonly Color Alert = Color.FromArgb(0xF5, 0x8A, 0x07);

    public static Bitmap Draw(int size, IconBadge badge = IconBadge.None)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var ink = badge == IconBadge.Paused ? Muted : Ink;
        var s = size / 32f;

        // 白い角丸の台。暗いタスクバーでも形が見えるように
        using (var paper = new SolidBrush(Paper))
        using (var path = RoundRect(new RectangleF(0.5f * s, 0.5f * s, 31 * s, 31 * s), 6 * s))
        {
            g.FillPath(paper, path);
        }

        using var inkBrush = new SolidBrush(ink);
        using var paperBrush = new SolidBrush(Paper);
        foreach (var (x, y) in new[] { (4f, 4f), (18f, 4f), (4f, 18f) })
        {
            g.FillRectangle(inkBrush, x * s, y * s, 10 * s, 10 * s);
            g.FillRectangle(paperBrush, (x + 2) * s, (y + 2) * s, 6 * s, 6 * s);
            g.FillRectangle(inkBrush, (x + 3.5f) * s, (y + 3.5f) * s, 3 * s, 3 * s);
        }

        switch (badge)
        {
            case IconBadge.None:
                using (var accent = new SolidBrush(Accent))
                {
                    g.FillEllipse(accent, 18.5f * s, 18.5f * s, 11 * s, 11 * s);
                }

                break;
            case IconBadge.Paused:
                using (var muted = new SolidBrush(Muted))
                {
                    g.FillRectangle(muted, 19.5f * s, 18 * s, 3.5f * s, 12 * s);
                    g.FillRectangle(muted, 25 * s, 18 * s, 3.5f * s, 12 * s);
                }

                break;
            case IconBadge.Warning:
                using (var alert = new SolidBrush(Alert))
                {
                    g.FillEllipse(alert, 17 * s, 17 * s, 14 * s, 14 * s);
                }

                g.FillRectangle(paperBrush, 22.8f * s, 19.5f * s, 2.4f * s, 6 * s);
                g.FillRectangle(paperBrush, 22.8f * s, 26.5f * s, 2.4f * s, 2.4f * s);
                break;
        }

        return bmp;
    }

    public static Icon ToIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            // FromHandle は持ち主にならないので、写しを作ってから元の handle を返す
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
