# ShotLink

VRChat で撮った写真に QR コードが写っていたら、その中身を Windows の通知で知らせるアプリです。
中身が URL なら、通知をクリックするとブラウザで開けます。
[Paramroom](https://github.com/kumo-mayu/Paramroom) で相手のアバターに出た QR コードも、カメラで撮るだけで PC に受け取れます。

- 通知領域に常駐します（ウィンドウは設定の窓だけ）
- VRChat の写真フォルダを自動で見張ります。ほかのスクリーンショットのフォルダも追加できます
- 開くのは `http` と `https` の URL だけです。「URL を直接開く」ははじめはオフで、続けて開くのは 1 分に 3 件までです
- ネットにはつながりません。写真も QR コードの中身も、どこへも送りません

VRChat Inc. とは関係のない、非公式のツールです。

## ダウンロード

[Releases](https://github.com/kumo-mayu/shotlink/releases) から zip を取り、展開した `ShotLink.exe` を起動します（インストール不要）。
使い方・消し方は zip の中の `README.txt` にあります。

- Windows 10（1809 以降）または Windows 11 の 64 ビット版
- exe に電子署名をしていないので、初回は「Windows によって PC が保護されました」と出ます。「詳細情報」→「実行」で起動できます

## 開発

- 仕様：[`docs/spec.md`](docs/spec.md)
- C# / Windows Forms / .NET 10。QR の読み取りは [zxing-cpp](https://github.com/zxing-cpp/zxing-cpp)
- `dotnet build` / `dotnet test`。配る形は `dotnet publish ShotLink.App -p:PublishProfile=win-x64`
- 配る zip は、tag（`v1.0.0` など）を push すると GitHub Actions が組んで Releases に下書きを置きます（`.github/workflows/release.yml`）

## ライセンス

[MIT](LICENSE)。同梱しているライブラリのライセンスは [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt)。
exe に含まれる Microsoft の部品（`Microsoft.Windows.SDK.NET.dll`・`WinRT.Runtime.dll`）は Windows SDK のライセンス条項に従い、MIT ライセンスは及びません。

QRコードは株式会社デンソーウェーブの登録商標です。
