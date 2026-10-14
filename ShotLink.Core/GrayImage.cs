namespace ShotLink.Core;

/// <summary>
/// 1画素1バイトの明るさの並び（左上から行ごと）。QR を探すにはこれだけで足りる。
/// 画像ファイルから作るのは App の側（WIC）で、Core は形式を知らない
/// </summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (pixels.Length != width * height)
        {
            throw new ArgumentException($"画素の数（{pixels.Length}）が {width}×{height} と合わない", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
}
