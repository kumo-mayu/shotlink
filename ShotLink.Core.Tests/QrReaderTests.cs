using ShotLink.Core;
using ShotLink.Fixtures;

namespace ShotLink.Core.Tests;

// 素材は作り物（仕様書 9章）。ここに置くのは、下限より十分に大きく読めて当然の場面と、読み違えてはいけない場面だけ。
// どこまで小さく・斜めでも読めるかの下限は tools/QrBench で測る
public class QrReaderTests
{
    const string Url = "https://booth.pm/ja/items/1234567";

    readonly QrReader _reader = new();

    List<string> Texts(GrayImage image) => _reader.Read(image).Select(f => f.Text).ToList();

    [Fact]
    public void 正面の_QR_を読む()
    {
        var image = new SceneBuilder(1920, 1080).Place(new QrPlacement(Url, 960, 540, 300)).Build();
        Assert.Equal([Url], Texts(image));
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(7680, 4320)]
    public void 解像度が違っても読む(int width, int height)
    {
        var size = height * 0.15;
        var image = new SceneBuilder(width, height, 2)
            .Place(new QrPlacement(Url, width * 0.3, height * 0.6, size, Roll: 10, Dark: 40, Light: 200))
            .Blur(height / 1080)
            .Noise(6)
            .Build();
        Assert.Equal([Url], Texts(image));
    }

    // ZXing.Net では読めず、zxing-cpp に替えた理由になった場面（QrReader の説明）
    [Theory]
    [InlineData(45)]
    [InlineData(55)]
    public void 斜めから撮った_QR_を読む(double yaw)
    {
        var image = new SceneBuilder(1920, 1080, 3)
            .Place(new QrPlacement(Url, 900, 500, 260, Roll: 7, Yaw: yaw, Pitch: 5))
            .Blur(1)
            .Noise(6)
            .Build();
        Assert.Equal([Url], Texts(image));
    }

    [Fact]
    public void 一枚に複数あれば全部読む()
    {
        var image = new SceneBuilder(1920, 1080, 3)
            .Place(new QrPlacement("A1", 400, 300, 200))
            .Place(new QrPlacement("https://b.example/2", 1000, 500, 180, Roll: 10, Yaw: 30))
            .Place(new QrPlacement("C3", 1500, 800, 150, Roll: -20, Pitch: 20))
            .Build();
        Assert.Equal(["A1", "C3", "https://b.example/2"], Texts(image).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void 同じ中身が二つ写っていても一つ()
    {
        var image = new SceneBuilder(1920, 1080, 4)
            .Place(new QrPlacement(Url, 500, 500, 250))
            .Place(new QrPlacement(Url, 1400, 500, 250))
            .Build();
        Assert.Equal([Url], Texts(image));
    }

    [Fact]
    public void 日本語の中身も化けない()
    {
        var image = new SceneBuilder(1920, 1080, 5).Place(new QrPlacement("くもまゆ／ShotLink テスト", 960, 540, 300)).Build();
        Assert.Equal(["くもまゆ／ShotLink テスト"], Texts(image));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void QR_が無ければ何も返さない(int seed)
    {
        var image = new SceneBuilder(3840, 2160, seed).Noise(6).Build();
        Assert.Empty(Texts(image));
    }
}

// 仕様書 3.5：通知・履歴・開く順は写真の中の位置の順
public class QrOrderTests
{
    static QrFound Q(string text, int left, int top, int size = 100) => new(text, new QrBounds(left, top, left + size, top + size));

    [Fact]
    public void 行は上から_行の中は左から()
    {
        var sorted = QrOrder.ByPosition([Q("右下", 500, 400), Q("左上", 0, 0), Q("右上", 500, 0), Q("左下", 0, 400)]);
        Assert.Equal(["左上", "右上", "左下", "右下"], sorted.Select(f => f.Text));
    }

    [Fact]
    public void 少し傾いて並んでいても同じ行として左から()
    {
        // 右の物ほど少し上にある（斜めに撮ったポスターの列）。中心の高さだけで比べると右から並んでしまう
        var sorted = QrOrder.ByPosition([Q("1", 0, 60), Q("2", 200, 30), Q("3", 400, 0)]);
        Assert.Equal(["1", "2", "3"], sorted.Select(f => f.Text));
    }

    [Fact]
    public void 写真から読んだ順も位置の順()
    {
        var image = new SceneBuilder(1920, 1080, 6)
            .Place(new QrPlacement("右下", 1500, 800, 220))
            .Place(new QrPlacement("左下", 400, 800, 220))
            .Place(new QrPlacement("真ん中上", 960, 250, 220))
            .Place(new QrPlacement("左上", 300, 260, 220))
            .Build();
        Assert.Equal(["左上", "真ん中上", "左下", "右下"], new QrReader().Read(image).Select(f => f.Text));
    }
}
