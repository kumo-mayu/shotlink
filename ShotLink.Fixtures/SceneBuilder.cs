using ShotLink.Core;

namespace ShotLink.Fixtures;

/// <summary>
/// VRChat の写真に似せた灰色の画像を作る：ゆるい明るさのむら・物の形の四角・細かいざらつきの背景に、
/// 斜めにも置ける QR を貼り、全体をぼかす。乱数の種を決めれば毎回同じ画像になる
/// </summary>
public sealed class SceneBuilder
{
    // QR の1画素を何点で測って塗るか。3×3 で、描画のアンチエイリアスに近い灰色の縁が出る
    const int Supersample = 3;

    readonly int _width;
    readonly int _height;
    readonly byte[] _pixels;
    readonly Random _random;

    public SceneBuilder(int width, int height, int seed = 1)
    {
        _width = width;
        _height = height;
        _pixels = new byte[width * height];
        _random = new Random(seed);
        PaintBackground();
    }

    public SceneBuilder Place(QrPlacement placement)
    {
        var matrix = QrMatrix.Create(placement.Text);
        var corners = ProjectCorners(placement);
        var toQr = Homography.From(corners, [(0, 0), (1, 0), (1, 1), (0, 1)]);

        var minX = Math.Max(0, (int)Math.Floor(corners.Min(c => c.X)));
        var maxX = Math.Min(_width - 1, (int)Math.Ceiling(corners.Max(c => c.X)));
        var minY = Math.Max(0, (int)Math.Floor(corners.Min(c => c.Y)));
        var maxY = Math.Min(_height - 1, (int)Math.Ceiling(corners.Max(c => c.Y)));
        var n = matrix.Modules;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                int inside = 0, dark = 0;
                for (var sy = 0; sy < Supersample; sy++)
                {
                    for (var sx = 0; sx < Supersample; sx++)
                    {
                        var (u, v) = toQr.Map(x + (sx + 0.5) / Supersample, y + (sy + 0.5) / Supersample);
                        if (u < 0 || u >= 1 || v < 0 || v >= 1)
                        {
                            continue;
                        }

                        inside++;
                        if (matrix[(int)(u * n), (int)(v * n)])
                        {
                            dark++;
                        }
                    }
                }

                if (inside == 0)
                {
                    continue;
                }

                var total = Supersample * Supersample;
                var qr = (dark * placement.Dark + (inside - dark) * placement.Light) / (double)inside;
                var i = y * _width + x;
                _pixels[i] = (byte)Math.Round((qr * inside + _pixels[i] * (total - inside)) / total);
            }
        }

        return this;
    }

    /// <summary>半径 radius の箱でぼかす（ピントの甘さ・描画のにじみ）。0 なら何もしない</summary>
    public SceneBuilder Blur(int radius)
    {
        if (radius <= 0)
        {
            return this;
        }

        var tmp = new byte[_pixels.Length];
        BoxPass(_pixels, tmp, horizontal: true, radius);
        BoxPass(tmp, _pixels, horizontal: false, radius);
        return this;
    }

    /// <summary>±amplitude のざらつきを足す（暗い場所の写真のノイズ）</summary>
    public SceneBuilder Noise(int amplitude)
    {
        for (var i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = Clamp(_pixels[i] + _random.Next(-amplitude, amplitude + 1));
        }

        return this;
    }

    public GrayImage Build() => new(_width, _height, (byte[])_pixels.Clone());

    void PaintBackground()
    {
        // 斜めのゆるい明るさのむら（空・床・照明）
        var a = _random.NextDouble() * 80 + 60;
        var gx = (_random.NextDouble() - 0.5) * 100 / _width;
        var gy = (_random.NextDouble() - 0.5) * 100 / _height;
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                _pixels[y * _width + x] = Clamp((int)(a + gx * x + gy * y));
            }
        }

        // 物の形（壁・家具・アバター）。大きさは写真の短い辺に合わせ、解像度が違っても同じ見た目にする
        var unit = Math.Min(_width, _height);
        for (var k = 0; k < 40; k++)
        {
            var w = (int)(unit * (0.02 + _random.NextDouble() * 0.3));
            var h = (int)(unit * (0.02 + _random.NextDouble() * 0.3));
            var x0 = _random.Next(0, Math.Max(1, _width - w));
            var y0 = _random.Next(0, Math.Max(1, _height - h));
            var shade = (byte)_random.Next(10, 246);
            for (var y = y0; y < y0 + h; y++)
            {
                Array.Fill(_pixels, shade, y * _width + x0, w);
            }
        }

        Noise(4);
    }

    static (double X, double Y)[] ProjectCorners(QrPlacement p)
    {
        // 正方形を 3次元で振り（左右→上下→画面の中の回転）、一辺の4倍の距離から見たとして投影する。
        // 近いほど遠近の歪みが強い。写真の QR は数 m 先なので、極端に近い距離にはしない
        var half = p.Size / 2;
        var distance = p.Size * 4;
        var yaw = p.Yaw * Math.PI / 180;
        var pitch = p.Pitch * Math.PI / 180;
        var roll = p.Roll * Math.PI / 180;

        (double, double, double)[] square = [(-half, -half, 0), (half, -half, 0), (half, half, 0), (-half, half, 0)];
        return square.Select(c =>
        {
            var (x, y, z) = c;
            (x, z) = (x * Math.Cos(yaw) + z * Math.Sin(yaw), -x * Math.Sin(yaw) + z * Math.Cos(yaw));
            (y, z) = (y * Math.Cos(pitch) - z * Math.Sin(pitch), y * Math.Sin(pitch) + z * Math.Cos(pitch));
            (x, y) = (x * Math.Cos(roll) - y * Math.Sin(roll), x * Math.Sin(roll) + y * Math.Cos(roll));
            var s = distance / (distance + z);
            return (p.CenterX + x * s, p.CenterY + y * s);
        }).ToArray();
    }

    void BoxPass(byte[] src, byte[] dst, bool horizontal, int r)
    {
        var lines = horizontal ? _height : _width;
        var length = horizontal ? _width : _height;
        for (var line = 0; line < lines; line++)
        {
            int At(int i) => horizontal ? line * _width + i : i * _width + line;
            var sum = 0;
            var count = 0;
            for (var i = 0; i <= Math.Min(r, length - 1); i++)
            {
                sum += src[At(i)];
                count++;
            }

            for (var i = 0; i < length; i++)
            {
                dst[At(i)] = (byte)(sum / count);
                var add = i + r + 1;
                var remove = i - r;
                if (add < length)
                {
                    sum += src[At(add)];
                    count++;
                }

                if (remove >= 0)
                {
                    sum -= src[At(remove)];
                    count--;
                }
            }
        }
    }

    static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
}
