// 作った素材で、QR を「読めた割合」と「1枚あたりの時間」を出す（仕様書 3.3）。
//   dotnet run -c Release --project tools/QrBench -- [--quick]
//   dotnet run -c Release --project tools/QrBench -- --dump <フォルダ>   素材の見本を PNG で書く
// 読み取り器を替えたり設定を変えたりしたら、これで前後を比べる
using System.Diagnostics;
using ShotLink.Core;
using ShotLink.Fixtures;

var quick = args.Contains("--quick");
var reader = new QrReader();

var dumpAt = Array.IndexOf(args, "--dump");
if (dumpAt >= 0)
{
    var dir = args[dumpAt + 1];
    Directory.CreateDirectory(dir);
    // 見本は人がクリックして確かめることがあるので、例示用に予約されたドメイン（RFC 2606）だけを使う。
    // 本物のサイトの URL にすると、確かめのクリックで知らないページを開いてしまう
    var samples = new (string Name, QrPlacement P, int Blur)[]
    {
        ("front-large", new("https://example.com/shotlink/front", 960, 540, 300), 0),
        ("tilt45-mid", new("https://example.org/shotlink/tilt45", 600, 400, 200, 5, 45, 10, 40, 200), 1),
        ("small-blur", new("https://example.net/shotlink/small", 1500, 800, 90, 15, 0, 0, 50, 190), 2),
        ("text", new("ShotLink のテスト（URL ではない文字列）", 960, 540, 300), 0),
        ("three", new("https://example.com/shotlink/1", 400, 540, 260), 0),
    };
    foreach (var s in samples)
    {
        var scene = new SceneBuilder(1920, 1080, 7).Place(s.P);
        if (s.Name == "three")
        {
            // 1枚に3つ：通知が3件並ぶかを見る
            scene.Place(new QrPlacement("https://example.org/shotlink/2", 960, 540, 260)).Place(new QrPlacement("ShotLink の3つ目", 1520, 540, 260));
        }

        var img = scene.Blur(s.Blur).Noise(6).Build();
        // VRChat の写真と同じ RGBA で書く（読み込みでカラーを灰色に直す道を通す）
        PngWriter.Write(Path.Combine(dir, s.Name + ".png"), img, rgba: true);
        Console.WriteLine($"{s.Name}: 読めた {string.Join(", ", reader.Read(img).Select(f => f.Text))}");
    }

    var big = new SceneBuilder(7680, 4320, 9).Place(new QrPlacement("https://example.com/shotlink/8k", 3000, 2000, 900, 10, 20, 0)).Blur(4).Noise(6).Build();
    PngWriter.Write(Path.Combine(dir, "8k-photo.png"), big, rgba: true);
    Console.WriteLine($"8k-photo: 読めた {string.Join(", ", reader.Read(big).Select(f => f.Text))}");
    return;
}

// 中身の長さで QR のマスの数が変わる（誤り訂正 M）。短い URL は Paramroom の 25×25 に近い。
// 2026-10-14 の測定は x.gd と booth.pm の同じ長さの URL で行った（例示用のドメインへ替えたが、長さは 18→19 字と 51→51 字で、マスの数は変わらない）
(string Name, string Text)[] payloads =
[
    ("短 URL", "https://example.co/"),
    ("URL", "https://example.com/items/1234567?utm_source=vrchat"),
    ("長 URL", "https://example.com/very/long/path/for/testing/qr/density?query=" + new string('a', 80)),
];

(string Name, int W, int H)[] resolutions = quick
    ? [("1080p", 1920, 1080), ("4K", 3840, 2160)]
    : [("1080p", 1920, 1080), ("4K", 3840, 2160), ("8K", 7680, 4320)];

// QR の一辺は写真の短い辺に対する割合で並べる。読めなくなる下限を探すので、読めないと見込む大きさも入れる
double[] sizeRatios = [0.03, 0.045, 0.06, 0.09, 0.14, 0.25, 0.5];

(string Name, double Roll, double Yaw, double Pitch)[] poses =
[
    ("正面", 0, 0, 0),
    ("回転20°", 20, 0, 0),
    ("斜め45°", 5, 45, 10),
    ("斜め65°", 0, 65, 0),
];

// ぼかしの半径（1080p での画素。解像度に合わせて広げる）
int[] blurs = [0, 1, 2];

Console.WriteLine($"素材：解像度 {resolutions.Length} × 中身 {payloads.Length} × 大きさ {sizeRatios.Length} × 向き {poses.Length} × ぼかし {blurs.Length}");

var rows = new List<Row>();
var seed = 0;
foreach (var res in resolutions)
{
    var unit = Math.Min(res.W, res.H);
    foreach (var payload in payloads)
    foreach (var ratio in sizeRatios)
    foreach (var pose in poses)
    foreach (var blur in blurs)
    {
        seed++;
        var rnd = new Random(seed);
        var size = unit * ratio;
        // 回して斜めにしても写真からはみ出さないよう、一辺の 0.75 倍ぶん内側に置く
        var margin = size * 0.75;
        var cx = margin + rnd.NextDouble() * Math.Max(0, res.W - margin * 2);
        var cy = margin + rnd.NextDouble() * Math.Max(0, res.H - margin * 2);
        var image = new SceneBuilder(res.W, res.H, seed)
            .Place(new QrPlacement(payload.Text, cx, cy, size, pose.Roll, pose.Yaw, pose.Pitch,
                Dark: (byte)rnd.Next(10, 60), Light: (byte)rnd.Next(170, 240)))
            .Blur(blur * unit / 1080)
            .Noise(6)
            .Build();

        var sw = Stopwatch.StartNew();
        var texts = reader.Read(image);
        sw.Stop();
        rows.Add(new Row(res.Name, payload.Name, ratio, pose.Name, blur, texts.Any(f => f.Text == payload.Text), sw.Elapsed.TotalMilliseconds));
    }

    Console.Error.WriteLine($"  {res.Name} 済み");
}

// QR なし：読み違え（何もないのに何か読む）と、何も無いときの時間
var falsePositives = 0;
var emptyMs = new Dictionary<string, List<double>>();
foreach (var res in resolutions)
{
    emptyMs[res.Name] = [];
    for (var i = 0; i < 5; i++)
    {
        var image = new SceneBuilder(res.W, res.H, 10_000 + i).Noise(6).Build();
        var sw = Stopwatch.StartNew();
        falsePositives += reader.Read(image).Count;
        emptyMs[res.Name].Add(sw.Elapsed.TotalMilliseconds);
    }
}

Console.WriteLine();
Console.WriteLine($"QR なしの写真で読み違えた数：{falsePositives}");
Console.WriteLine();
Console.WriteLine("## 解像度ごと（読めた割合・1枚あたりの時間）");
foreach (var g in rows.GroupBy(r => r.Resolution))
{
    Console.WriteLine($"  {g.Key,-6} 読めた {g.Count(r => r.Ok),4}/{g.Count(),-4} ({Pct(g)})  時間 平均 {g.Average(r => r.Ms),4:F0} ms・最大 {g.Max(r => r.Ms),4:F0} ms  QR なし {emptyMs[g.Key].Average(),4:F0} ms");
}

Console.WriteLine();
Console.WriteLine("## 大きさ（一辺 / 写真の短い辺）× 向き。ぼかし 0 / 1 / 2 の順");
foreach (var g in rows.GroupBy(r => r.Resolution))
{
    Console.WriteLine($"### {g.Key}");
    Console.WriteLine("  一辺/短い辺 | " + string.Join(" | ", poses.Select(p => $"{p.Name,-14}")));
    foreach (var ratio in sizeRatios)
    {
        var cells = poses.Select(p => string.Join(" ", blurs.Select(b =>
            Pct(g.Where(r => r.Ratio == ratio && r.Pose == p.Name && r.Blur == b)))));
        Console.WriteLine($"  {ratio,10:P1} | " + string.Join(" | ", cells));
    }
}

static string Pct(IEnumerable<Row> rows)
{
    var list = rows.ToList();
    return $"{100.0 * list.Count(r => r.Ok) / list.Count,3:F0}%";
}

record Row(string Resolution, string Payload, double Ratio, string Pose, int Blur, bool Ok, double Ms);
