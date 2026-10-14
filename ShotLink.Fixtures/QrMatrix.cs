using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace ShotLink.Fixtures;

/// <summary>QR のマス目（true が黒）。周りの余白（4マス）込み</summary>
public sealed class QrMatrix
{
    readonly BitMatrix _bits;

    QrMatrix(BitMatrix bits) => _bits = bits;

    public int Modules => _bits.Width;

    public bool this[int x, int y] => _bits[x, y];

    public static QrMatrix Create(string text, ErrorCorrectionLevel? level = null)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            // 誤り訂正 M は、スマホの QR 作成アプリや Paramroom の既定に近い
            [EncodeHintType.ERROR_CORRECTION] = level ?? ErrorCorrectionLevel.M,
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
            [EncodeHintType.MARGIN] = 4,
        };

        // 大きさに 0 を渡すと、1マス1画素の最小の大きさで返る
        return new QrMatrix(new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 0, 0, hints));
    }
}
