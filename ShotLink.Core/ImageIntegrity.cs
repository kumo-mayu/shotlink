using System.Buffers.Binary;

namespace ShotLink.Core;

/// <summary>
/// 写し取った画像のバイト列が、1つの書き終わった画像として揃っているかを見る（仕様書 3.2）。
/// VRChat が保存したあと、写真に情報を書き足す道具が同じファイルを書き直して情報を足す（2026-10-14、作者の実機の写真に
/// 別の道具の iTXt が入っていた）。書き直しの途中を読むと、終わりの印はあっても中身が混ざる。
/// PNG は塊ごとの CRC を全部確かめるので、混ざった写しは通らない
/// </summary>
public static class ImageIntegrity
{
    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }

        return c;
    }).ToArray();

    public static FileEnding Verify(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
        {
            return FileEnding.Incomplete;
        }

        if (data[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return VerifyPng(data);
        }

        if (data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]) + 8L == data.Length ? FileEnding.Complete : FileEnding.Incomplete;
        }

        if (data[0] == 0xFF && data[1] == 0xD8)
        {
            return data[^2] == 0xFF && data[^1] == 0xD9 ? FileEnding.Complete : FileEnding.Unknown;
        }

        return FileEnding.Unknown;
    }

    /// <summary>塊を頭から順にたどり、どれも CRC が合い、IEND でちょうど終わるか</summary>
    static FileEnding VerifyPng(ReadOnlySpan<byte> data)
    {
        var offset = 8;
        while (offset + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (length > int.MaxValue || offset + 12L + length > data.Length)
            {
                return FileEnding.Incomplete;
            }

            var typeAndData = data.Slice(offset + 4, 4 + (int)length);
            var expected = BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 8 + (int)length)..]);
            if (Crc(typeAndData) != expected)
            {
                return FileEnding.Incomplete;
            }

            offset += 12 + (int)length;
            if (typeAndData[..4].SequenceEqual("IEND"u8))
            {
                return offset == data.Length ? FileEnding.Complete : FileEnding.Incomplete;
            }
        }

        return FileEnding.Incomplete;
    }

    static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }
}
