# 外部の AI による攻撃の点検のプロンプト（作業ツリーの中で試験を書いて走らせる）

`external-audit-prompt.md`（読むだけ）と違い、攻撃の試験を書いて走らせる。2026-10-18 に作って使った（結果は `docs/feedback/review-2026-10-18-codex-attack.md`）。
直すのは、候補をもらってから、ユーザーとまとめ役（Claude）が進める。

走らせ方：

1. 本番の保存先の更新日時を控える（`ls -la --time-style=full-iso "$LOCALAPPDATA/ShotLink"`）
2. 作業用フォルダに使い捨ての作業ツリーを作り、試験の一式だけ組んでおく（Codex の中はネットが無く、部品を取りに行けない。App は組まない）
   ```bash
   git worktree add <作業用>/audit-wt HEAD
   cd <作業用>/audit-wt && dotnet test ShotLink.Core.Tests
   ```
3. 以下の `---` から下を渡す（裏で走らせる。十数分かかる）
   ```bash
   codex exec -m gpt-6-astra -c model_reasoning_effort='"medium"' -c approval_policy='"never"' -s workspace-write \
     -C <作業用>/audit-wt -o <作業用>/codex-attack.md - < <この節から下を切り出したファイル>
   ```
   `approval_policy=never` にしないと、`exec` は人がいないので、許可を求めたところで止まる
4. 終わったら：足したファイルに禁じた部品（`Process`・`UseShellExecute`・`UrlOpener`・`Clipboard`・`Windows.Forms`・`Registry`・通知・通信）が無いかを grep し、
   本番の保存先の日時を見比べ、作業ツリーで `dotnet test` を全部走らせ直す。試験は `ShotLink.Core.Tests/Attack/` に移し（`results/` は移さない）、
   作業ツリーは `git worktree remove --force` で消す

---

あなたは、Windows の常駐アプリ「ShotLink」を**攻撃者の立場で試す**点検役です。報告は**日本語**で書いてください。

## 今いる場所

作業フォルダは、ShotLink のリポジトリを写した**使い捨ての作業ツリー**（git worktree）です。本物のリポジトリではありません。
この中でなら、ファイルを作る・書き換える・`dotnet build ShotLink.Core.Tests`・`dotnet test ShotLink.Core.Tests` を走らせてかまいません。
部品（NuGet）は取得済みです。ネットにはつながりません（つなぎに行かないでください）。コミット・プッシュはしないでください。

## このアプリ

VRChat の写真フォルダ（と、利用者が足したフォルダ）を見張り、新しく撮られた写真に写った QR コードを読んで、
Windows のトースト通知で知らせる常駐アプリです。中身が URL なら、通知のクリック（設定によっては直接）で**既定のブラウザで開きます**。
写真に写った QR は、VRChat の中で**他人が見せた物**であることが多い。つまり QR の中身は攻撃者が自由に決められる入力です。
C# / Windows Forms / .NET 10。まだ公開していません。ネットにつながらない作りです。

- `ShotLink.Core/`：UI を持たない部分（QR の読み取り＝zxing-cpp、中身の振り分け `QrContent.cs`（`QrClassifier`）、`QrDispatcher`、`RecentOpenFilter`、
  `SiteAllowlist`（2026-10-17 に足したホワイトリスト）、`ToastText`、`Settings`、`JsonFileStore`、`History`、`ImageIntegrity`、`FileReadyWaiter`、`PhotoProcessor`、見張り、`UrlOpener`）
- `ShotLink.App/`：通知領域のアイコン、トースト通知（`Toasts.cs`）、`TrayContext`、設定の窓、一覧の窓 `QrListForm`、画像の読み込み
- `ShotLink.Core.Tests/`：xUnit の試験（242 件。すべて通る。`Attack/` は前回 2026-10-18 の攻撃の試験）
- `ShotLink.Fixtures/`：試験の素材を作る部品（`QrMatrix`・`PngWriter`・`SceneBuilder`）。好きな中身の QR を PNG にできる
- 決め事：`CLAUDE.md`（最初に読む）、`docs/spec.md`、`docs/spec/settings.md`

## 絶対に守ること

1. **本当に開かない・送らない・登録しない**。あなたが書く・走らせるコードは、次を**使わない・参照しない**：
   `System.Diagnostics.Process`、`ProcessStartInfo`、`UseShellExecute`、`UrlOpener`、`Clipboard`、`System.Windows.Forms`、
   `Microsoft.Win32.Registry`、`Microsoft.Toolkit.Uwp.Notifications`、`HttpClient` などの通信。
   攻撃は「ShotLink が何をすると決めたか」（`QrAction`・`QrDecision`・`ToastText`・`SiteAllowlist.Contains`・`Settings`）で止める。実際に開く・コピーする・通知するところまでは行かない
2. **アプリを起動しない**：`ShotLink.exe`・`ShotLink.App` を組まない・走らせない。`tools/` の道具も走らせない。PowerShell やシェルで URL を開くコマンドも打たない
3. **利用者のデータに触らない**：次の場所は、開かない・読まない・一覧も取らない
   - `%USERPROFILE%\Pictures`（本物の写真）、`%LOCALAPPDATA%\ShotLink`（本番の設定と履歴）、`%USERPROFILE%\AppData\LocalLow\VRChat`、レジストリ
   - 一時ファイルは、作業ツリーの中か、試験の中で作る一時フォルダ（`Path.GetTempPath()` の下）だけ。`SHOTLINK_HOME` を設定するなら一時フォルダだけ
4. **攻撃の中身のホスト名は、例示用だけ**：`example.com`・`example.org`・`example.net`・`*.example`（`evil.example` など）。本物のサイト名を使わない
5. 作業ツリーの外のファイルは書かない

迷ったら、走らせずに「確かめていない」と書いてください。

## やること

**攻撃の試験を書いて走らせ、守りが破れるかを実際に確かめる**。新しい試験は `ShotLink.Core.Tests/Attack/` の下に、話題ごとの新しいファイルで作ってください（前回の物は直さない）。
試験は「守れているなら通る」形で書く（破れたら失敗する）。**失敗した試験＝見つけた穴**です。通った試験は守れている証拠として残します。
既存の試験は直さないでください。

### 狙う所（重い順）

**A. 開いてはいけない物を開く・見せた行き先と違う所を開く（いちばん重い。仕様書 4章）**
- `QrClassifier.Classify`：`http`/`https` 以外が `WebUrl` にならないか。大文字小文字、前後の空白・BOM・ゼロ幅の文字、`https:\\`、`https:/`、
  中に CR/LF/タブ/NUL、`https://good.example@evil.example/`、`javascript:` の見た目の似た文字（全角のコロンなど）、`\\server\share`、`file:`、`vrchat:`
- **見せたホスト名（`QrContent.Host`）と、実際に開く URL（`QrContent.Url`）の行き先がずれる中身**。.NET の `Uri` の正規化（`\` を `/` に、パーセント符号、IDN、末尾のドット、ポート、
  IPv6・10進や16進の IPv4 `http://0x7f.1/`）と、ブラウザの解釈が食い違う物も考える。ブラウザに渡るのが `Url` か原文か（`TrayContext`・`UrlOpener` を読んで）も確かめる
- `SiteAllowlist`（新しく、まだ誰も攻めていない）：`example.com` を許したとき `example.com.evil.example`・`evil-example.com`・`example.com:8080`・
  `user@example.com` の見せかけ・末尾のドット・大文字・`xn--` と Unicode の取り違え・`Normalize` が空や null になる行・`*.`・IP アドレスで抜けられないか。
  `Normalize`（人が書いた行）と `QrContent.Host` の形がずれて、許したはずの物が開かない・許していない物が開く組み合わせ
- `QrDispatcher.Decide`・`RecentOpenFilter`：比べるのは原文の `Ordinal`。同じ行き先の URL を少しだけ変えて（ホストの大文字、末尾の `/`、`#`、`?`、`%41` と `A`）、
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

## 既に分かっていること（指摘にしない）

- 決め事どおりの振る舞い：起動前の写真は読まない、1枚に 4 件以上なら通知をまとめる、直接開くは既定 OFF、設定でまとめないにしたら全部開く、
  同じ中身は毎回知らせる（開くのだけ 60 秒に1回）、直接開く回数に上限は無い、ホワイトリストは下位のドメインも含む、国際化ドメインは `xn--` のまま見せる
- 前回（2026-10-14）の点検で記録だけにした物：見張りがあふれたときの読み直し、読み直しと再開の競合、通知の引数を照らさない（上の D で評価だけ頼む）
- 仕様書で [要確認] の物（実際の VRChat の写真・書き込みの途中・WebP）

## 報告の形

最初に、重い順の要約（10件まで）。その後に全部の指摘を次の形で：

```
### [重大度] 件名（1行）
- 場所：ファイル:行
- 攻撃：どんな中身・どんな手順か（例示用ドメインで具体的に）
- 起きること：何を開く・偽る・固まるか
- 根拠：たどったコードの道／失敗した試験の名前と出力の要点
- 確かさ：試験で確かめた／コードを読んで確か／要確認
- 直し方の方向：1〜3行
```

重大度：**致命**（http/https 以外を開く・見せた行き先と違う所を開く・ホワイトリストを抜ける・本番の保存先や登録に触れる・ネットへ出る）、
**重い**（固まる・落ちる・見張りが黙って止まる・60 秒やまとめる決まりを外から抜ける）、**中**（見た目を偽れる・食い違い）、**軽い**。

最後に次の2つを書く：
1. **足した試験の一覧**：ファイルごとに、試験の名前と、通った／失敗した（失敗したものは出力の要点）。`dotnet test ShotLink.Core.Tests` の最後の合計の行もそのまま
2. **確かめていない範囲**

件数を増やすより、確かな物を優先してください。同じ原因の指摘は1件にまとめてください。
