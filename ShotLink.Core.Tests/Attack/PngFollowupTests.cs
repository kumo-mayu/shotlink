// 2026-10-18 に外部の AI（Codex）が2回目の攻撃の点検で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using System.Buffers.Binary;
using System.IO.Compression;
using ShotLink.Core;
using ShotLink.Fixtures;

namespace ShotLink.Core.Tests.Attack;

public class PngFollowupTests
{
    [Fact]
    public void PNG内のIDN表記違いでも同じ許可先を一度だけ開く()
    {
        string[] texts = ["https://example.com/", "https://ｅxample.com/", "https://example。com/"];
        var scene = new SceneBuilder(1500, 500, 923);
        for (var i = 0; i < texts.Length; i++) scene.Place(new(texts[i], 250 + i * 500, 250, 260));
        var found = new QrReader().Read(RoundTrip(scene.Build()));
        Assert.Equal(3, found.Count);
        Assert.All(texts, text => Assert.Contains(found, f => f.Text == text));
        var batch = QrDispatcher.CreateDefault(new FakeClock()).Decide(found.Select(f => f.Text), new(true, true, AllowedSites: ["example.com"]));
        Assert.False(batch.Summarized);
        Assert.True(batch.Count(d => d.Action == QrAction.Opened) == 1,
            $"同じ許可ホストで {batch.Count(d => d.Action == QrAction.Opened)} 件が Opened");
    }

    [Fact]
    public void 反転QRを混ぜても正常QRの分類は安全なまま()
    {
        var scene = new SceneBuilder(1000, 500, 924)
            .Place(new("https://example.com/", 250, 250, 260))
            .Place(new("javascript:alert(1)", 750, 250, 260, Dark: 240, Light: 10));
        var found = new QrReader().Read(RoundTrip(scene.Build()));
        Assert.Contains(found, f => f.Text == "https://example.com/");
        var batch = QrDispatcher.CreateDefault(new FakeClock()).Decide(found.Select(f => f.Text), new(true, true));
        Assert.All(batch.Where(d => d.Action == QrAction.Opened), d => Assert.Equal("example.com", d.Content.Host));
    }

    [Theory]
    [InlineData(0x7fffffffu)]
    [InlineData(0x80000000u)]
    [InlineData(0xffffffffu)]
    public void PNGチャンク長の整数境界で例外や完全判定を出さない(uint length)
    {
        using var stream = new MemoryStream();
        PngWriter.Write(stream, new GrayImage(1, 1, [255]));
        var png = stream.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(8), length);
        Assert.Equal(FileEnding.Incomplete, ImageIntegrity.Verify(png));
    }

    // 素材生成器が出す Gray8・フィルタ0だけを読む。巨大な展開やOSの画像部品は使わない。
    static GrayImage RoundTrip(GrayImage image)
    {
        using var output = new MemoryStream();
        PngWriter.Write(output, image);
        var png = output.ToArray();
        Assert.Equal(FileEnding.Complete, ImageIntegrity.Verify(png));
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        Assert.InRange((long)width * height, 1, 1_000_000);
        using var compressed = new MemoryStream();
        for (var offset = 8; offset < png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            if (png.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8)) compressed.Write(png, offset + 8, length);
            offset += 12 + length;
        }
        compressed.Position = 0;
        using var z = new ZLibStream(compressed, CompressionMode.Decompress);
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            Assert.Equal(0, z.ReadByte());
            z.ReadExactly(pixels.AsSpan(y * width, width));
        }
        Assert.Equal(-1, z.ReadByte());
        return new(width, height, pixels);
    }
}
