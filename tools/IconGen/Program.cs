// IconArt の絵を、大きさ違いの PNG を束ねた .ico にする（Windows Vista 以降は .ico の中の PNG を読める）
// 走らせ方：dotnet run --project tools/IconGen -- ShotLink.App/ShotLink.ico
using System.Buffers.Binary;
using System.Drawing.Imaging;
using ShotLink.App;

var output = args.Length > 0 ? args[0] : "ShotLink.ico";

// エクスプローラーと通知・タスクマネージャーが使う大きさ。表示の拡大率 100〜200% をまかなう
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];

var images = sizes.Select(size =>
{
    using var bmp = IconArt.Draw(size);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return (Size: size, Png: ms.ToArray());
}).ToList();

using var file = File.Create(output);
var header = new byte[6];
BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), 1); // 1 は .ico
BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), (ushort)images.Count);
file.Write(header);

var offset = 6 + 16 * images.Count;
foreach (var (size, png) in images)
{
    var entry = new byte[16];
    entry[0] = (byte)(size >= 256 ? 0 : size); // 0 は 256
    entry[1] = (byte)(size >= 256 ? 0 : size);
    BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(4), 1); // 面の数
    BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(6), 32); // 1画素のビット数
    BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(8), png.Length);
    BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(12), offset);
    file.Write(entry);
    offset += png.Length;
}

foreach (var (_, png) in images)
{
    file.Write(png);
}

Console.WriteLine($"{output}：{images.Count} 個の大きさ・{file.Length:N0} バイト");
