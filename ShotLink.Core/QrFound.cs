namespace ShotLink.Core;

/// <summary>写真の中の QR の外接する四角（画素）</summary>
public readonly record struct QrBounds(int Left, int Top, int Right, int Bottom);

/// <summary>写真の中で見つけた QR 1つ</summary>
public sealed record QrFound(string Text, QrBounds Bounds);

/// <summary>写真の中の位置の順（仕様書 3.5 [決定]）</summary>
public static class QrOrder
{
    /// <summary>
    /// 上下に重なる物を同じ行とみなし、行は上から、行の中は左から並べる。
    /// 中心の高さだけで比べると、少し傾いて並んだポスターの順が左右で入れ替わる
    /// </summary>
    public static IReadOnlyList<QrFound> ByPosition(IEnumerable<QrFound> found)
    {
        var rows = new List<(int Top, int Bottom, List<QrFound> Items)>();
        foreach (var qr in found.OrderBy(f => f.Bounds.Top).ThenBy(f => f.Bounds.Left))
        {
            var b = qr.Bounds;
            var rowIndex = rows.FindIndex(r => b.Top < r.Bottom && b.Bottom > r.Top);
            if (rowIndex < 0)
            {
                rows.Add((b.Top, b.Bottom, [qr]));
                continue;
            }

            var row = rows[rowIndex];
            row.Items.Add(qr);
            rows[rowIndex] = (Math.Min(row.Top, b.Top), Math.Max(row.Bottom, b.Bottom), row.Items);
        }

        return rows.OrderBy(r => r.Top).SelectMany(r => r.Items.OrderBy(i => i.Bounds.Left)).ToList();
    }
}
