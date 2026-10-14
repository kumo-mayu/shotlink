namespace ShotLink.Fixtures;

/// <summary>平面から平面への射影（斜めから見た四角形）。4組の点の対応から求める</summary>
readonly struct Homography
{
    readonly double[] _h; // 3×3 を行ごとに。h[8] = 1

    Homography(double[] h) => _h = h;

    public (double X, double Y) Map(double u, double v)
    {
        var w = _h[6] * u + _h[7] * v + _h[8];
        return ((_h[0] * u + _h[1] * v + _h[2]) / w, (_h[3] * u + _h[4] * v + _h[5]) / w);
    }

    /// <summary>src[i] を dst[i] へ移す射影</summary>
    public static Homography From((double X, double Y)[] src, (double X, double Y)[] dst)
    {
        // 未知数 8 個の連立一次方程式 A h = b
        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            var (u, v) = src[i];
            var (x, y) = dst[i];
            double[] r1 = [u, v, 1, 0, 0, 0, -u * x, -v * x, x];
            double[] r2 = [0, 0, 0, u, v, 1, -u * y, -v * y, y];
            for (var j = 0; j < 9; j++)
            {
                a[i * 2, j] = r1[j];
                a[i * 2 + 1, j] = r2[j];
            }
        }

        // 部分ピボットのガウスの消去法
        for (var col = 0; col < 8; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < 8; row++)
            {
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                {
                    pivot = row;
                }
            }

            for (var j = 0; j < 9; j++)
            {
                (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);
            }

            for (var row = 0; row < 8; row++)
            {
                if (row == col)
                {
                    continue;
                }

                var f = a[row, col] / a[col, col];
                for (var j = col; j < 9; j++)
                {
                    a[row, j] -= f * a[col, j];
                }
            }
        }

        var h = new double[9];
        for (var i = 0; i < 8; i++)
        {
            h[i] = a[i, 8] / a[i, i];
        }

        h[8] = 1;
        return new Homography(h);
    }
}
