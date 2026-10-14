# ShotLink — AIへの指示

VRChat の写真フォルダを見張り、写真に写った QR コードの中身を知らせる・URL ならブラウザで開く常駐型の Windows アプリ。Paramroom のおまけとして同梱し、BOOTH の単独の無料商品としても配る。

- 仕様は `docs/spec.md`。**作業の前に読む。** [決定] の項目はユーザーが決めたもの。変えるときは必ず聞く
- 返答・途中経過・報告は日本語で書く
- **安全の決まり（仕様書 4章）を崩さない**：直接開くのは `http` / `https` だけ。写真も QR の中身もどこへも送らない
- `git push`・GitHub のリポジトリ作成・リリースの公開・BOOTH への登録は、ユーザーが自分で行う。コミットは頼まれたら行ってよい
- アイデアの経緯は idea-vault（`D:\work\ClaudeCode\idea-vault\ideas\vrc-photo-qr-watcher.md`）。配布の形は Paramroom（`D:\work\ClaudeCode\avatar-image-pad`、`tools/release/make-release.sh`）、保存先や公開の流れは Chmonos（`D:\work\ClaudeCode\chmonos`）が参考になる
