namespace ShotLink.Fixtures;

/// <param name="Text">QR に入れる中身</param>
/// <param name="CenterX">写真の中の QR の中心（画素）</param>
/// <param name="CenterY">写真の中の QR の中心（画素）</param>
/// <param name="Size">正面から見たときの QR の一辺（余白込み・画素）</param>
/// <param name="Roll">画面の中での回転（度）</param>
/// <param name="Yaw">左右に振った角度（度）。斜めから撮った QR</param>
/// <param name="Pitch">上下に振った角度（度）</param>
/// <param name="Dark">黒いマスの明るさ。アバターの上の QR は光の当たり方で真っ黒・真っ白にならない</param>
/// <param name="Light">白いマスの明るさ</param>
public sealed record QrPlacement(
    string Text,
    double CenterX,
    double CenterY,
    double Size,
    double Roll = 0,
    double Yaw = 0,
    double Pitch = 0,
    byte Dark = 30,
    byte Light = 220);
