# ShotLink — AIへの指示

VRChat の写真フォルダ（と、手で足したフォルダ）を見張り、写真に写った QR コードの中身を知らせる・URL ならブラウザで開く常駐型の Windows アプリ（WPF / .NET 10）。
Paramroom のおまけとして同梱し、BOOTH の単独の無料商品としても配る。

- 仕様は `docs/spec.md`。**作業の前に読む。** [決定] の項目はユーザーが決めたもの。変えるときは必ず聞く
- アイデアの経緯は idea-vault（`D:\work\ClaudeCode\idea-vault\ideas\vrc-photo-qr-watcher.md`）。配布の形は Paramroom（`D:\work\ClaudeCode\avatar-image-pad`、`tools/release/make-release.sh`）、
  保存先・公開の流れ・このハーネスの元は Chmonos（`D:\work\ClaudeCode\chmonos`）が参考になる

**返答・途中経過・報告はすべて日本語で書く。**ツールの結果や裏の作業の知らせ（英語で届く）を受けた直後の一言、サブエージェントへの指示と報告も日本語。
長い会話で薄れるので、`.claude/settings.json` の `language` と、発言のたびに一言を差し込むフック（`UserPromptSubmit`）でも補っている。

---

## 絶対に破らないこと

### 1. 安全の決まり（仕様書 4章）を崩さない

- 直接開くのは `http` / `https` だけ。ほかのスキーム・スキームの無い文字列は開かない（コピーだけ）
- 開くときは既定のブラウザに URL を渡すだけ（`UseShellExecute`）。引数を組み立ててコマンドに混ぜない
- **ネットに一切つながない**（更新の確認もしない）。写真も QR の中身もどこへも送らない。通信する部品（`HttpClient` など）を足さない
- 同じ URL を開くのは 60 秒に1回（既定）と、1枚に 4 件以上ならまとめて既定では開かない決め事を、外す・緩める変更はしない（直接開く回数の上限は 2026-10-14 にユーザー判断で外し、同じ中身を知らせない決め事も「毎回知らせる」に変えた）

### 2. 本物の写真フォルダと VRChat の設定に書かない

ShotLink は写真を**読むだけ**。作業でも同じ。

- `%USERPROFILE%\Pictures\VRChat\`（本物の写真）と `...\LocalLow\VRChat\VRChat\config.json` に、書き込み・移動・削除をしない
- 見張りの確かめは、作業用フォルダを「追加のフォルダ」にして、そこへ素材を**移動**する
- `.claude/hooks/guard-paths.mjs` が、これらと本番の `settings.json`・`history.json` への書き込みを止め、シェルで触れるときは確認を求める

### 3. 本番の設定と履歴に触らない

本番の保存先は `%LOCALAPPDATA%\ShotLink`。試験・確かめの起動は必ず `SHOTLINK_HOME` で作業用フォルダを指す。
アプリの一式を組む試験は、保存先を必ず渡す（渡し忘れたら本番を開くのではなく、例外で止まる作りにする）。

---

## 作りの決め事

- **QR の読み取り・中身の振り分け（スキームの判定）・同じ URL を開く間隔・まとめる件数は、UI の無いプロジェクト（Core）に置く**（仕様書 9章）。WPF・WinForms・通知の部品に依らない
- 見張り（`FileSystemWatcher`）・時計・URL を開く処理・通知は、差し替えられる形で Core に渡す。Core の試験は本物の見張りやブラウザを使わない
- 保存する JSON は人が読める形にする。計算で出せる値は書かない。欠けた配列（`null`）は空として読む
- 推定した値を勝手に決めない。判断が要るところは選択肢と根拠を示して聞く（おすすめを1つ付ける）

---

## 書き方

- コメントは「なぜ」を書く。日本語で。何をしているかはコードが語る
- 数字には根拠を添える（「60 秒にした」ではなく「続けて撮るのはせいぜい数秒〜数十秒なので、60 秒で足りる」）
- PowerShell のスクリプトを書く前に、Chmonos の `docs/dev/powershell.md` の落とし穴を見る（`@($null).Count` が1・空の配列が null など）

---

## 確かめ方

```bash
dotnet build
dotnet test
```

- 既定の一式に、通信する試験・時計や実マシンの状態に左右される試験を入れない
- 本物の `FileSystemWatcher` を使う試験は数件までにし、待ち時間に余裕を持たせる（揺れやすい）。それ以外は差し替えた見張りと時計で確かめる
- QR の読み取りの素材は道具で作る（仕様書 9章）。作った画像はリポジトリに入れない
- 作業の途中は関係する試験だけ回してよい。**コミットの前は全部**
- 計算で決まる物（メニューに何が並ぶか・通知の文言・押したら何が起きるか）は試験で確かめる。画面で見るのは配置と見た目だけ

### アプリを起動して確かめる

1. 作業用フォルダ（scratchpad）に保存先と見張るフォルダを作り、保存先に `settings.json` を置く。
   **`"watchVrchat": false`** にして、本物の `Pictures\VRChat` を見張らない：
   `{ "watchVrchat": false, "extraFolders": [ { "path": "<作業用>\\watched", "includeSubfolders": true } ] }`
2. `SHOTLINK_HOME` にその保存先を入れて `ShotLink.App\bin\Debug\net10.0-windows10.0.17763.0\ShotLink.exe` を起動する。
   `SHOTLINK_HOME` があると、通知の登録は `KumoMayu.ShotLink.Dev` になり、Windows の起動時の登録には触れない
3. 素材は `dotnet run -c Release --project tools/QrBench -- --dump <作業用>\fixtures` で作り（RGBA の PNG と 8K）、
   別のフォルダに写してから `watched` へ**移動**する
4. 確かめは `<保存先>\Logs\<日付>.log`（写真・QR・メモリの行）と `history.json`。設定の窓は `PrintWindow` で撮って見る
5. **通知をクリックする確かめは人にしかできない。**ユーザーが席にいるときに、何を押すかを並べて頼む
6. **確かめの間は ShotLink を起動したままにする。**止まっている間に開発用の通知をクリックされると、Windows は
   `SHOTLINK_HOME` を付けずに exe を起こすので、本番の保存先と本番の登録の名前で動いてしまう。
   終えるときは、開発用の通知を消してから止める（`ToastNotificationManager.History.Clear('KumoMayu.ShotLink.Dev')`）
7. 通知の見本の中身は、例示用のドメイン（example.com など）だけにする。本体はクリックで本当にブラウザを開く

---

## 読み方（文脈を節約する）

- 読む前に `Grep` で場所を絞り、`Read` の `offset`/`limit` でその前後だけ読む
- 編集した直後に読み直さない。一度読んだ範囲は、変わっていなければ読み直さない
- ビルド・テスト・git の出力は末尾や `--stat` だけ出す（`wrap-up` スキル）

---

## 進め方

- **コミットは Claude が適切な粒度で行う**（ユーザー指示 2026-10-14）。1つのコミットに1つの理由。メッセージはなぜそうしたかを日本語で書く
- **`git push`・GitHub のリポジトリ作成・リリースの公開・BOOTH への登録は、ユーザーが自分で行う**
- 決め事・設計を変えたら `docs/spec.md` も直す。200行を超えそうなら `docs/spec/` に話題ごとに分ける
- 仕上げの手順は `.claude/skills/wrap-up/`
