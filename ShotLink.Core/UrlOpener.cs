using System.Diagnostics;

namespace ShotLink.Core;

/// <summary>
/// ブラウザで開く。開いてよいのは http / https だけ（仕様書 4章 [決定]）。
/// 振り分け（QrClassifier）で決めた後でも、開く直前にもう一度確かめる。開く道はここ1か所に集める
/// </summary>
public sealed class UrlOpener(Action<string> launch)
{
    public static UrlOpener CreateDefault() => new(url =>
        // 既定のブラウザに URL を渡すだけ。引数を組み立ててコマンドに混ぜない（仕様書 4章）
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose());

    /// <summary>開いたら true。http / https の URL でなければ何もせず false</summary>
    public bool TryOpen(QrContent content)
    {
        if (content.Kind != QrKind.WebUrl || content.Url is null)
        {
            return false;
        }

        // QrContent は record なので、作り方によっては Kind・Url と中身が食い違いうる。
        // 中身から振り分け直し、同じ URL になるときだけ開く
        var again = QrClassifier.Classify(content.Text);
        if (again.Kind != QrKind.WebUrl || again.Url != content.Url)
        {
            return false;
        }

        launch(content.Url.AbsoluteUri);
        return true;
    }
}
