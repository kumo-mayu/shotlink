# ShotLink の仕上げ（harness の wrap-up スキルが読む）

## ビルドとテスト

```powershell
dotnet build 2>&1 | Select-Object -Last 4          # 警告 0・エラー 0 を見る
dotnet test --nologo; if ($LASTEXITCODE) { throw "テスト失敗" }
```

- 出力を丸ごと出さない。失敗したときだけ `Select-String 'error|失敗|Failed'` で該当の行を見る
- 実行ファイルが掴まれて失敗したら、確かめ用に起動した ShotLink を終了する（常駐しているので、通知領域から抜けているかも見る）
- 見張り・時計に絡む直しをしたときは、テストを2回走らせて揺れないことを見る

## 直す文書

| 変えた物 | 直す文書 |
|---|---|
| 決め事・設計・動き | `docs/spec.md`（200行を超えそうなら `docs/spec/` に話題ごとに分ける） |
| 守る決め事・作業の手順 | `CLAUDE.md` |

## 点検（プロジェクトだけの物）

- 本物の VRChat の写真・ユーザーの写真が無いか（`git diff --cached --name-only` に `.png` `.jpg` が無いか。素材は道具で作り、画像は入れない）
- 本番の `settings.json`・`history.json`、実際に読んだ QR の中身が、試験のデータや文書に紛れていないか
- `bin/`・`obj/`・`publish/` が無いか

## 報告に書く「確かめていない事」の例

実際の VRChat の写真・トーストのクリック・別の PC
