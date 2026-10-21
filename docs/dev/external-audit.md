# ShotLink の外部の点検（プロジェクトの部分）

harness の `external-audit` スキルが、共通の雛形（`read.md`・`attack.md`）の `{{…}}` にこの節を入れて Codex に渡す。節の名前は変えない。
2026-10-14 の読むだけの指示書（`external-audit-prompt.md`）と、10-18 の攻撃の指示書（`external-attack-prompt.md`）から、共通の部分を除いて移した（2026-10-21）。
「走らせてよいコマンド」と「狙う所」は read と attack で違うので、節の中を分けてある。走らせる種類の方だけを入れる。

## アプリの種類

Windows の常駐アプリ

## アプリ名

ShotLink

## このアプリ

VRChat の写真フォルダ（と、利用者が足したフォルダ）を見張り、新しく撮られた写真に写った QR コードを読んで、
Windows のトースト通知で知らせる常駐アプリです。中身が URL なら、通知のクリック（設定によっては直接）で**既定のブラウザで開きます**。
写真に写った QR は、VRChat の中で**他人が見せた物**であることが多い。つまり QR の中身は攻撃者が自由に決められる入力です。
C# / Windows Forms / .NET 10。まだ公開していません。写真も QR の中身も、どこへも送らない（ネットにつながない）作りです。

- `ShotLink.Core/`：UI を持たない部分（QR の読み取り＝zxing-cpp、中身の振り分け `QrContent.cs`（`QrClassifier`）、`QrDispatcher`、同じ URL を開く間隔 `RecentOpenFilter`、
  ホワイトリスト `SiteAllowlist`（2026-10-17 に足した）、`ToastText`、`Settings`、`JsonFileStore`、`History`、`ImageIntegrity`、書き込みの待ち `FileReadyWaiter`、`PhotoProcessor`、見張り、ログ、URL を開く所 `UrlOpener`）
- `ShotLink.App/`：通知領域のアイコンとメニュー（`TrayContext`）、トースト通知（`Toasts.cs`）、設定の窓、一覧の窓 `QrListForm`、画像の読み込み（Windows.Graphics.Imaging）、1つしか起動しない仕組み
- `ShotLink.Core.Tests/`：xUnit の試験（2026-10-18 に 278 件。すべて通る。`Attack/` は攻撃の点検で足した試験）
- `ShotLink.Fixtures/`：試験の素材を作る部品（`QrMatrix`・`PngWriter`・`SceneBuilder`）。好きな中身の QR を PNG にできる
- `tools/`：測る道具・配布の道具
- 決め事：`CLAUDE.md`（最初に読む）、`docs/spec.md`（仕様。[決定] はユーザーが決めたこと）、`docs/spec/*.md`（`settings.md` など）

保存は `%LOCALAPPDATA%\ShotLink`（`settings.json`・`history.json`・`Logs\`）。環境変数 `SHOTLINK_HOME` があればそちら。

## 触ってはいけない場所

- `%USERPROFILE%\Pictures\VRChat`（本物の写真）と、ほかの「ピクチャ」の下
- `%LOCALAPPDATA%\ShotLink`（本番の設定と履歴）、`%USERPROFILE%\AppData\LocalLow\VRChat`（VRChat の設定）
- レジストリ（通知の登録・Run）
- attack で `SHOTLINK_HOME` を設定するなら、試験の中で作る一時フォルダだけ

## 走らせてよいコマンド

- read のとき：なし。読むだけの設定で動いていて、ビルドも試験もできない。`ShotLink.exe`・`tools/` の道具・スクリプトも走らせない。コードを読んで確かめる
- attack のとき：`dotnet build ShotLink.Core.Tests`・`dotnet test ShotLink.Core.Tests` だけ。`ShotLink.exe`・`ShotLink.App` は組まない・走らせない。`tools/` の道具も、PowerShell やシェルで URL を開くコマンドも打たない
- どちらも、`tools/release/make-notices.mjs` は GitHub から取るので走らせない

## 使わない部品（attack だけ）

`System.Diagnostics.Process`、`ProcessStartInfo`、`UseShellExecute`、`UrlOpener`、`Clipboard`、`System.Windows.Forms`、
`Microsoft.Win32.Registry`、`Microsoft.Toolkit.Uwp.Notifications`、`HttpClient` などの通信。
攻撃は「ShotLink が何をすると決めたか」（`QrAction`・`QrDecision`・`ToastText`・`SiteAllowlist.Contains`・`Settings`）で止める。実際に開く・コピーする・通知するところまでは行かない。

## 試験の置き場（attack だけ）

`ShotLink.Core.Tests/Attack/`

## 狙う所

### read のとき

**A. 開いてはいけない物を開く（いちばん重い。仕様書 4章）**

- 開いてよいのは `http` / `https` だけ。それ以外のスキーム（`file:`・`javascript:`・`ms-settings:`・`vrchat:` など）や、スキームの無い文字列、
  空白・改行・制御文字を含む文字列、`https://good@evil/` のような見せかけ、国際化ドメインの見た目の似た偽物が、どこかの道で開かれないか
  （`QrClassifier`・`UrlOpener`・`TrayContext`・`QrListForm`・通知のクリックの引数 `ToastArguments`）
- 通知のクリックで渡る引数（`kind`・`text`）を、通知の中身ではない所から作れるか・偽れるか。ShotLink が止まっているときに通知をクリックされると、
  Windows が exe を起こす（`-ToastActivated`）。そのときに何が起きるか
- 同じ URL を 60 秒に1回だけ開く（既定）・4件以上でまとめたときに既定で開かない・ホワイトリストに絞ったら一覧のサイトだけ開く、を設定の外から抜ける道
- 写真フォルダを開く（エクスプローラー）に、設定に書かれた細工した場所が渡ったとき

**B. 見張りと書き込みの待ち（仕様書 3.1・3.2）**

- VRChat が書いている途中のファイルを読む・読み損ねる（`FileReadyWaiter`）。大きさが変わらない間に書き足される・0 バイトのまま・消される・名前が変わる
- `FileSystemWatcher` のあふれ・フォルダが消える／戻る・ネットワークやリムーバブルの場所・ジャンクション・とても深いフォルダ（`PhotoWatch`・`FileSystemFolderWatcher`）
- 起動前の写真・一時停止の間の写真を読まない決まり（仕様書 3.1 [決定]）を破る道（あふれたときの読み直し・時計・作成日時が古いまま移されたファイル）
- 同じフォルダを重ねて指定・大文字小文字・末尾の区切り・短い名前（8.3）・同じファイルが2回知らされる
- 写真が一度に何百枚も来たとき（行列が溜まり続ける・メモリ）

**C. 外から来る物を信じすぎる**

- **画像**：壊れた PNG・巨大な寸法（展開すると何 GB にもなる画像）・形式の偽装・WebP の部品が無い Windows（`WinRtImageLoader`。メモリ・例外の行き先）
- **QR の中身**：とても長い文字列・改行や制御文字・右から左の文字（U+202E など）で通知やメニューの見た目を偽る・`&` などメニューで特別な文字
- **VRChat の設定ファイル**：`picture_output_folder` に細工した値（相対パス・UNC・環境変数・とても長い値）（`VrchatPhotoFolder`）
- **手で直した・壊れた `settings.json`・`history.json`**（`JsonFileStore`・`Settings.Normalized`・`History`）

**D. データ・登録・外への漏れ**

- 設定と履歴の書き込み中に落ちる・ディスクが一杯・2つの経路から同時に書く（`JsonFileStore` の置き換え）
- ログに QR の中身が残らない決まり（ホスト名まで。仕様書 7章）を破る道（例外の文・ファイル名・URL のパス）
- 試験・確かめの起動（`SHOTLINK_HOME`）が、本番の保存先・本番の通知の登録（`KumoMayu.ShotLink`）・Windows の起動時の登録（Run）に触れる道
- ネットへつながる道が紛れていないか

**E. 固まる・落ちる**

- 画面のスレッドでの同期の待ち・`Send`・クリップボードの取り合い・裏のスレッドから画面の部品に触る
- 例外が捨てられて黙って止まる（写真の処理の繰り返し・通知のクリック・見張りの知らせ）
- 終了の途中で写真の処理や通知のクリックが来る・1つしか起動しない仕組みの取りこぼし

**F. 環境の違い**

- Windows の通知がオフ・集中モード・Windows 10・高い DPI・「ピクチャ」が OneDrive にある・時計が戻る・トルコ語などの大文字小文字

### attack のとき

**A. 開いてはいけない物を開く・見せた行き先と違う所を開く（いちばん重い。仕様書 4章）**

- `QrClassifier.Classify`：`http`/`https` 以外が `WebUrl` にならないか。大文字小文字、前後の空白・BOM・ゼロ幅の文字、`https:\\`、`https:/`、
  中に CR/LF/タブ/NUL、`https://good.example@evil.example/`、`javascript:` の見た目の似た文字（全角のコロンなど）、`\\server\share`、`file:`、`vrchat:`
- **見せたホスト名（`QrContent.Host`）と、実際に開く URL（`QrContent.Url`）の行き先がずれる中身**。.NET の `Uri` の正規化（`\` を `/` に、パーセント符号、IDN、末尾のドット、ポート、
  IPv6・10進や16進の IPv4 `http://0x7f.1/`）と、ブラウザの解釈が食い違う物も考える。ブラウザに渡るのが `Url` か原文か（`TrayContext`・`UrlOpener` を読んで）も確かめる
- `SiteAllowlist`：`example.com` を許したとき `example.com.evil.example`・`evil-example.com`・`example.com:8080`・
  `user@example.com` の見せかけ・末尾のドット・大文字・`xn--` と Unicode の取り違え・`Normalize` が空や null になる行・`*.`・IP アドレスで抜けられないか。
  `Normalize`（人が書いた行）と `QrContent.Host` の形がずれて、許したはずの物が開かない・許していない物が開く組み合わせ
- `QrDispatcher.Decide`・`RecentOpenFilter`：同じ行き先の URL を少しだけ変えて（ホストの大文字、末尾の `/`、`#`、`?`、`%41` と `A`）、
  60 秒に1回の決まりを抜けて同じ所をいくつも開けるか。1枚の写真の中で同じ行き先を数えてまとめる件数を水増し・水減らしできるか
  （決め事：1枚に 4 件以上ならまとめて既定では開かない。3 件までなら直接開く。**3件ずつ何枚も**のような使い方も考える）
- 通しの攻撃：`ShotLink.Fixtures` で細工した中身の QR を本物の PNG にし、`QrReader` → `Classify` → `Dispatcher` に通す（1枚に複数・重なり・反転など）

**B. 見た目を偽る**

- `ToastText.For` / `ForBatch` / `MenuLabel`：右から左の文字（U+202E など）・ゼロ幅・結合文字・とても長いホスト・改行・`&` で、通知やメニューの**ホスト名の見え方**を偽れるか

**C. 外から来る物を信じすぎる・固まる・落ちる**

- 画像：壊れた PNG、寸法だけ巨大な PNG（展開すると何 GB）、形式の偽装を `ImageIntegrity.Verify` と `QrReader` に渡す（`WinRtImageLoader` は App なので読むだけ）
- 手で直した・壊れた `settings.json` / `history.json`（null、巨大な数、負の数、型違い、重なったキー、巨大なファイル）を `JsonFileStore` + `Settings.Normalized` / `History` に
- `PhotoProcessor` に、差し替えた読み込み部品で、例外を投げる・巨大な結果を返す・中身が何千件などを渡し、繰り返しが黙って止まらないか

**D. App の側は読むだけで調べる**（組まない・走らせない）

- `ShotLink.App/Toasts.cs` の通知のクリックの引数（`ToastArguments` の `kind`・`text`）：引数は照らし合わせずに使っている（既知）。
  **再指摘ではなく、攻撃者がその道に届くか**を評価してほしい。通知の中身でない所から、ShotLink の通知のクリックとして任意の `text` を渡せるか
  （ほかのアプリ・同じユーザーの別のプロセス・`-ToastActivated` での起動・ShotLink が止まっているときのクリック）。届くなら、何を開けるか（`http`/`https` の判定を通るか）
- `TrayContext`・`QrListForm` の開く道が、Core の判定を必ず通るか

## 既に分かっていること

- 決め事どおりの振る舞い：起動前の写真は読まない、1枚に 4 件以上なら通知をまとめる、直接開くは既定 OFF、設定でまとめないにしたら全部開く、
  同じ中身は毎回知らせる（開くのだけ 60 秒に1回）、直接開く回数に上限は無い（2026-10-14 に外した）、ホワイトリストは下位のドメインも含む、国際化ドメインは `xn--` のまま見せる
- 仕様書で [要確認] の物（実際の VRChat の写真・書き込みの途中・WebP）
- 2026-10-14 の読むだけの点検（`docs/feedback/review-2026-10-14-codex.md`）で記録だけにした物：見張りがあふれたときの読み直し、読み直しと再開の競合、通知の引数を照らさない（attack では評価だけ頼む）
- 2026-10-18 の攻撃の点検（`docs/feedback/review-2026-10-18-codex-attack.md`）で見つかり、直した物。**再指摘ではなく、直しそのものを攻めてほしい**：
  1. 60 秒に1回の決まりは、原文ではなく `Uri.AbsoluteUri` で比べるようにした（`QrDispatcher`）。それでも同じ行き先を何度も開ける表記違いは残っていないか
  2. `IdnHost` が投げるホストは、QR なら文字列、ホワイトリストの行なら捨てる（`QrClassifier.TryIdnHost`）。ほかに投げる所・同じ写真のほかの QR を止める所は残っていないか
  3. 見せる文字列から向きを変える文字と幅の無い文字を取り除く（`ToastText.StripInvisible`）。取り除く範囲から漏れていて、見え方を偽れる文字は無いか
  　（注意：xUnit の `Assert.Contains`/`DoesNotContain` の文字列版は文化圏に合わせて比べるので、無視される文字の確かめは `StringComparison.Ordinal` を付ける）
  2回目（同じ日）に、直しの抜けとして次も直した：60 秒の鍵のホストは `xn--` の形にそろえる（全角・句点・`xn--` の書き分け）、
  見えない文字は Cf の分類ごと取り除く、読み直してホストが変わる URL とそろえ直すと変わるホワイトリストの行は受け付けない
- 前回までの攻撃の試験（`ShotLink.Core.Tests/Attack/` のファイル）で試した角度

## 重大度

- **致命**：http / https 以外を開く・見せた行き先と違う所を開く・ホワイトリストを抜ける・本番の保存先や登録に触れる・ネットへ出る
- **重い**：固まる・落ちる・見張りが黙って止まる・写真を取りこぼし続ける・履歴や設定が消える・60 秒やまとめる決まりを外から抜ける
- **中**：見た目を偽れる・食い違い・取り違え・回復できるが手間のかかる壊れ方
- **軽い**：まれな条件での見た目・文言・効率

## Codex の設定（任意）

- read：既定（2026-10-14 は codex-cli 0.160.1・gpt-6.1-sol・medium）
- attack：`-m gpt-6-astra -c model_reasoning_effort='"medium"'`（2026-10-18 に使った）
- attack の前に、本番の保存先の更新日時を控える：`ls -la --time-style=full-iso "$LOCALAPPDATA/ShotLink"`
