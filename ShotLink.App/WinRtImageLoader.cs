using ShotLink.Core;
using Windows.Graphics.Imaging;

namespace ShotLink.App;

/// <summary>
/// 画像ファイルを明るさの並びにする。Windows の画像の部品（WIC。Windows.Graphics.Imaging から使う）で読むので、
/// ライブラリを足さずに PNG・JPEG と、Windows に入っていれば WebP も読める（仕様書 8章）
/// </summary>
sealed class WinRtImageLoader(Action<string> log) : IImageLoader
{
    const long MaxPixels = 100_000_000;

    public GrayImage? TryLoad(string path)
    {
        try
        {
            // VRChat の書き込みを妨げない開き方にする。ShotLink は読むだけ
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var stream = file.AsRandomAccessStream();
            var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
            // 展開すると何 GB にもなる画像（小さな PNG に巨大な寸法を書いた物）を読まない。
            // VRChat の 8K は 3300 万画素。1 億画素あれば、ほかのスクショ道具の大きな写真も収まる
            if ((long)decoder.PixelWidth * decoder.PixelHeight > MaxPixels)
            {
                log($"大きすぎるので読まない：{Path.GetFileName(path)}：{decoder.PixelWidth}×{decoder.PixelHeight}");
                return null;
            }

            var width = (int)decoder.PixelWidth;
            var height = (int)decoder.PixelHeight;

            // 灰色で受け取れれば、8K でも 33 MB で済む（BGRA だと 4 倍）
            var gray = TryPixels(decoder, BitmapPixelFormat.Gray8);
            if (gray is not null && gray.Length == width * height)
            {
                return new GrayImage(width, height, gray);
            }

            var bgra = TryPixels(decoder, BitmapPixelFormat.Bgra8)
                       ?? throw new InvalidDataException("画素を取り出せない");
            return new GrayImage(width, height, ToGray(bgra, width * height));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException
                                      || e.HResult is unchecked((int)0x88982F50) or unchecked((int)0x88982F07) or unchecked((int)0x88982F60))
        {
            // 0x88982F50：形式が分からない（WebP の部品が入っていない Windows 10 など）、0x88982F07・0x88982F60：壊れている
            log($"画像を読めない：{Path.GetFileName(path)}：{e.GetType().Name}：0x{e.HResult:X8}");
            return null;
        }
    }

    static byte[]? TryPixels(BitmapDecoder decoder, BitmapPixelFormat format)
    {
        try
        {
            var provider = decoder.GetPixelDataAsync(format, BitmapAlphaMode.Ignore, new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult();
            return provider.DetachPixelData();
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException || e.HResult == unchecked((int)0x88982F80))
        {
            // 元の形式によっては灰色へ直せない（0x88982F80：変換できない）。そのときは BGRA でもらって自分で直す
            return null;
        }
    }

    static byte[] ToGray(byte[] bgra, int pixels)
    {
        var gray = new byte[pixels];
        for (var i = 0; i < pixels; i++)
        {
            // 明るさの重み（ITU-R BT.601）。整数で計算して、8K でも 0.1 秒かからないように
            var b = bgra[i * 4];
            var g = bgra[i * 4 + 1];
            var r = bgra[i * 4 + 2];
            gray[i] = (byte)((r * 77 + g * 150 + b * 29) >> 8);
        }

        return gray;
    }
}
