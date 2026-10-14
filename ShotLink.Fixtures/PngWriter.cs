using System.Buffers.Binary;
using System.IO.Compression;
using ShotLink.Core;

namespace ShotLink.Fixtures;

/// <summary>
/// 灰色の画像を PNG に書く。見張りを手で確かめる素材のためだけなので、WPF（WIC）に依らず最小限に書く
/// </summary>
public static class PngWriter
{
    public static void Write(string path, GrayImage image)
    {
        using var file = File.Create(path);
        Write(file, image);
    }

    public static void Write(Stream output, GrayImage image)
    {
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8; // 1画素8ビット
        header[9] = 0; // 灰色
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < image.Height; y++)
            {
                z.WriteByte(0); // 行ごとの前処理なし
                z.Write(image.Pixels, y * image.Width, image.Width);
            }
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
    }

    static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc(Crc(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
        Span<byte> sum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sum, crc);
        output.Write(sum);
    }

    // PNG の CRC-32。System.IO.Hashing は別の包みなので、素材のためだけに足さず表で計算する
    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }

        return c;
    }).ToArray();

    static uint Crc(uint crc, byte[] data)
    {
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }
}
