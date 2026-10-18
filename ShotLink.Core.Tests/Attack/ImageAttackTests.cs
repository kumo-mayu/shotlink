// 2026-10-18 に外部の AI（Codex）が攻撃者の立場で書いた試験。守れていれば通る（docs/feedback/review-2026-10-18-codex-attack.md）
using System.Buffers.Binary;
using System.IO.Compression;
using ShotLink.Core;
using ShotLink.Fixtures;

namespace ShotLink.Core.Tests.Attack;

public class ImageAttackTests
{
    [Fact]
    public void 不正IDNのQRが同じ写真の正常なQRの処理を落とさない()
    {
        var image = new SceneBuilder(1200, 600, 83)
            .Place(new("https://a\u061cb.example/", 300, 300, 250))
            .Place(new("https://example.com/", 900, 300, 250)).Build();
        var found = new QrReader().Read(RoundTrip(image));
        Assert.Equal(2, found.Count);
        Assert.Contains(found, f => f.Text == "https://a\u061cb.example/");
        var error = Record.Exception(() => QrDispatcher.CreateDefault(new FakeClock())
            .Decide(found.Select(f => f.Text), new(false, true)));
        Assert.True(error is null, $"PNGから読んだ分類で例外: {error}");
    }

    [Fact]
    public void PNGのCRC破損と切断と偽装を完全扱いしない()
    {
        using var stream = new MemoryStream();
        PngWriter.Write(stream, new GrayImage(1, 1, [255]));
        var png = stream.ToArray();
        Assert.Equal(FileEnding.Complete, ImageIntegrity.Verify(png));
        Assert.NotEqual(FileEnding.Complete, ImageIntegrity.Verify(png[..^1]));
        png[29] ^= 1;
        Assert.NotEqual(FileEnding.Complete, ImageIntegrity.Verify(png));
        Assert.NotEqual(FileEnding.Complete, ImageIntegrity.Verify("this is not a PNG file"u8));
    }

    [Fact]
    public void 巨大寸法を小さい画素配列でネイティブに渡せない()
    {
        Assert.Throws<ArgumentException>(() => new GrayImage(100000, 100000, [0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayImage(-1, 1, [0]));
    }

    [Fact]
    public void 巨大寸法PNGは完全性検査を通っても画素数検査を通らない()
    {
        using var stream = new MemoryStream();
        PngWriter.Write(stream, new GrayImage(1, 1, [255]));
        var png = stream.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), 100000);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), 100000);
        uint crc = 0xffffffff;
        foreach (var b in png.AsSpan(12, 17))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        }
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29), crc ^ 0xffffffff);
        // CRC と終端の検査は寸法の検査ではない。巨大な展開そのものは実行しない。
        Assert.Equal(FileEnding.Complete, ImageIntegrity.Verify(png));
        Assert.Throws<ArgumentException>(() => new GrayImage(
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)), [255]));
    }

    [Fact]
    public void 細工した三つのQRをPNGから戻しても再開防御を維持する()
    {
        var image = new SceneBuilder(1600, 600, 81)
            .Place(new("https://example.com/", 250, 300, 250))
            .Place(new("HTTPS://EXAMPLE.COM/", 800, 300, 250))
            .Place(new("https://example.com:443/", 1350, 300, 250)).Build();
        var decoded = RoundTrip(image);
        var found = new QrReader().Read(decoded);
        Assert.Equal(3, found.Count);
        var batch = QrDispatcher.CreateDefault(new FakeClock()).Decide(found.Select(f => f.Text), new(true, true));
        Assert.True(batch.Count(d => d.Action == QrAction.Opened) == 1,
            $"PNG内の同一絶対URLで {batch.Count(d => d.Action == QrAction.Opened)} 件が Opened");
    }

    [Fact]
    public void PNG内の四つのURLはまとめて開かない()
    {
        var b = new SceneBuilder(2000, 600, 82);
        for (var i = 0; i < 4; i++) b.Place(new($"https://example.com/{i}", 250 + i * 500, 300, 240));
        var found = new QrReader().Read(RoundTrip(b.Build()));
        Assert.Equal(4, found.Count);
        var batch = QrDispatcher.CreateDefault(new FakeClock()).Decide(found.Select(f => f.Text), new(true, true));
        Assert.True(batch.Summarized);
        Assert.DoesNotContain(batch, d => d.Action == QrAction.Opened);
    }

    // 素材生成器の Gray8・フィルタ0だけを読む。App の画像部品を使わず、本物の PNG の往復を検証する。
    static GrayImage RoundTrip(GrayImage image)
    {
        using var dir = new TempDir();
        var path = dir.Combine("attack.png");
        PngWriter.Write(path, image);
        var data = File.ReadAllBytes(path);
        Assert.Equal(FileEnding.Complete, ImageIntegrity.Verify(data));
        var width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20));
        Assert.InRange((long)width * height, 1, 2_000_000);
        using var compressed = new MemoryStream();
        for (var offset = 8; offset < data.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            if (data.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8)) compressed.Write(data, offset + 8, length);
            offset += length + 12;
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
