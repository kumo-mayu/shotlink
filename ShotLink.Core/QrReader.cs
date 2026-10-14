using ZXingCpp;

namespace ShotLink.Core;

/// <summary>写真の中の QR を探して、中身の文字列を返す（1枚に複数あれば全部。仕様書 3.3）</summary>
/// <remarks>
/// zxing-cpp を使う。作った素材で ZXing.Net と比べ（2026-10-14、tools/QrBench）、斜め 45〜55° の QR を
/// ZXing.Net は 6 枚中 1 枚しか読めず、zxing-cpp は 6 枚とも読めた。1枚に3つ並べたときも ZXing.Net は 1 つ取りこぼした。
/// 4K で 1枚 約 20 ms と速さも上。自前で縮小や拡大を足しても読める数は増えなかったので、段を重ねない
/// </remarks>
public sealed class QrReader
{
    public IReadOnlyList<string> Read(GrayImage image)
    {
        using var options = new ReaderOptions
        {
            // 探すのは QR だけ。全種類を探すと遅くなるうえ、ゲームの UI の縞を 1次元バーコードと読み違える
            Formats = BarcodeFormat.QRCode,
            TryHarder = true,
            TryRotate = true,
            // 8K で大きくぼけた QR を1枚拾えた。時間は 1 割ほど増えるだけ
            TryDownscale = true,
            // 白黒を反転した QR は VRChat では見かけない。試すと時間が倍近くになる
            TryInvert = false,
        };

        var view = new ImageView(image.Pixels, image.Width, image.Height, ImageFormat.Lum, 0, 0);
        var found = new List<string>();
        foreach (var barcode in BarcodeReader.Read(view, options))
        {
            if (barcode.IsValid && !found.Contains(barcode.Text, StringComparer.Ordinal))
            {
                found.Add(barcode.Text);
            }
        }

        return found;
    }
}
