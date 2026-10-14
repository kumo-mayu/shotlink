# 外部の AI（Codex）による全域検査（2026-10-14）

- 指示書：`docs/dev/external-audit-prompt.md`。`codex exec -s read-only`（codex-cli 0.160.1・gpt-6.1-sol・medium）で、読むだけで検査させた。リポジトリは変わっていない
- 検査したコミット：`7d3a5ec`
- まとめ役（Claude）の確かめ：重い指摘の当たる所（`LogWriter` が `IOException` しか捕まえない・`Handle` が履歴を先に書く・メニューと一覧が原文を出す・`_seen` が処理より先に足されて消えない）をコードで見て、指摘どおりと確かめた。直すかはユーザーと決める

## 対応（2026-10-14、ユーザー判断）

| 指摘 | 対応 |
|---|---|
| 致命：開発用の通知から本番で起動する | 直した。開発用の起動は、終了時に通知を全部消す（`Toasts.ClearAll`）。落ちたときは残るので、CLAUDE.md の注意は残す |
| 致命：ネットワークの場所へアクセスする | 受け付ける（ユーザー判断）。設定の窓に一言書いた |
| 致命：メニューと一覧で原文を出す | 直した。メニューは「ホスト名｜原文」、一覧は「行き先」の列にホスト名（xn-- のまま） |
| 重い：壊れたフォルダの場所で起動できない | 直した。その場所だけを外して知らせる。見張りを作るのに失敗したら「無い」として 5 秒ごとに見直す |
| 重い：ログが書けないと処理が止まる | 直した。ログは投げない・処理の中のログも投げない。万一、繰り返しが止まったらアイコンと通知で知らせる |
| 重い：履歴を保存できないと通知も出ない | 直した。開く・知らせるを先に行い、保存の失敗は最初の1回だけ知らせる |
| 重い：一度読み損ねた写真を二度と読まない | 直した。読めなかった写真は「見た」の控えから外し、置き直されたら読む |
| 重い・要確認：巨大な画像 | 直した。1 億画素を超える画像は読まない |
| 中：画面が止まると上限を超えて開く | 上限そのものを外した（ユーザー判断） |
| 重い：あふれたときの読み直しの取りこぼし／中：読み直しと再開が重なる／中・要確認：通知の引数を照らさない | 記録だけ（まれな条件が重ならないと起きない。開く前に http / https かは確かめている） |

あわせて、写真の処理の失敗のログから例外の文を外した（渡した先の例外に QR の中身が入りうるため）。

以下、Codex の報告そのまま。

---

読み取りだけで検査しました。変更・ビルド・試験・アプリ起動・ネットアクセス・利用者データへのアクセスは行っていません。「確か」はコード上の条件と結果を確認した意味で、実機での再現確認ではありません。

重い順の要約は次の10件です。

1. **致命**：終了後の開発用通知から起動すると、本番の保存先・通知登録を使う。
2. **致命**：UNC パスを受け入れるため、フォルダ監視だけでも外部への通信が発生する。
3. **致命**：メニューと一覧には国際化ドメインを原文で表示し、似た偽ドメインを見分けられない。
4. **重い**：不正な追加フォルダを設定すると、起動時に例外で終了する。
5. **重い**：ログのアクセス拒否で写真処理ループが停止し、その後の写真が処理されない。
6. **重い**：履歴の保存に失敗すると、その写真の通知・直接開く処理も打ち切られる。
7. **重い**：一度受け付けたファイル名は永久に除外され、読み取り失敗や同名ファイルの再作成から回復しない。
8. **重い**：あふれ後の再走査は、古い作成日時の移動写真やアクセスできない下位フォルダで取りこぼす。
9. **中**：直接開く回数を実際に開く前に数えるため、UI が詰まると1分3件を超えてまとめて開く。
10. **中**：再走査と再開が重なると、一時停止中の写真を読んでしまう。

`file:` などを QR の中身として開く経路は、読んだ範囲では見つかりませんでした。通知クリック・メニュー・一覧からも `UrlOpener` で再分類されます。ただし、以下の表示と起動の問題は残っています。

### [致命] 開発用通知からの再起動が本番の保存先・登録に触れる

- 場所：[Program.cs:10](D:/work/ClaudeCode/shotlink/ShotLink.App/Program.cs:10)、[AppServices.cs:12](D:/work/ClaudeCode/shotlink/ShotLink.App/AppServices.cs:12)、[AppIdentity.cs:15](D:/work/ClaudeCode/shotlink/ShotLink.App/AppIdentity.cs:15)、[TrayContext.cs:385](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:385)
- 条件：`SHOTLINK_HOME` を指定して通知を出し、通知を残して終了する。その後、通知をクリックして Windows に exe を起動させる。
- 起きること：環境変数が引き継がれなければ、本番の `Logs` に書き、本番の設定・履歴を読み、本番の通知登録を使う。Run 登録があれば `Startup.Refresh` による更新対象にもなる。
- 根拠：`Main` は環境変数だけで開発用かを決め、`ShotLinkHome.Resolve(..., allowProduction: true)` を呼ぶ。通知起動の引数から保存先を復元する処理はない。`AppServices` は即座にログの作成・古いログの削除・履歴の読み込みを行う。通常終了では `Toasts.Dispose` によりイベント購読を外すだけで、開発用通知を消さない。
- 確かさ：**確か**。Windows が環境変数を付けずに起動する条件は `CLAUDE.md` にも明記済み。既知の注意事項だが、コードでは防止されていない。
- 試験：**穴**。`指定が無く本物の起動でもなければ止める` は `allowProduction: false` の試験であり、この入口を検査していない。
- 直し方の方向：開発用通知の起動経路に保存先を確実に引き継ぐか、復元できない通知起動は本番初期化の前に停止する。通常終了でも開発用通知を消す。

### [致命] UNC の監視先・保存先によってネットへ出る

- 場所：[VrchatPhotoFolder.cs:41](D:/work/ClaudeCode/shotlink/ShotLink.Core/VrchatPhotoFolder.cs:41)、[PhotoWatch.cs:219](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:219)、[FolderWatcher.cs:27](D:/work/ClaudeCode/shotlink/ShotLink.Core/FolderWatcher.cs:27)、[ShotLinkHome.cs:18](D:/work/ClaudeCode/shotlink/ShotLink.Core/ShotLinkHome.cs:18)
- 条件：追加フォルダ、`picture_output_folder`、または `SHOTLINK_HOME` に、到達可能な `\\server\share\...` を指定する。
- 起きること：Windows のネットワークファイルアクセスが発生する。監視先ならディレクトリ照会・画像の読み取り、保存先なら履歴の QR 全文を含む JSON の書き込みも外部共有に向かう。
- 根拠：`Path.GetFullPath` は UNC をローカルパスに変えない。その後、`Directory.Exists` → `FileSystemWatcher`、または `Directory.CreateDirectory` → `File.WriteAllText` にそのまま渡す。UNC やネットワークドライブを拒否する検査はない。通信 API がなくても、この道で通信する。
- 確かさ：**確か**。認証情報など何が送られるかは Windows と接続先の設定次第で、ここでは断定しない。
- 試験：**穴**。保存先・監視先の試験はローカルパス中心で、ネットワークパスの拒否を試していない。
- 直し方の方向：「ネットに一切つながない」を守るなら、監視先・保存先を共通の検査で制限する。UNC に加え、ネットワークドライブや別名経由の扱いも決める。

### [致命] メニューと一覧では偽ドメインを原文のまま見せて開く

- 場所：[TrayContext.cs:185](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:185)、[QrListForm.cs:44](D:/work/ClaudeCode/shotlink/ShotLink.App/QrListForm.cs:44)、[ToastText.cs:39](D:/work/ClaudeCode/shotlink/ShotLink.Core/ToastText.cs:39)、[QrContent.cs:24](D:/work/ClaudeCode/shotlink/ShotLink.Core/QrContent.cs:24)
- 条件：キリル文字の `а` を使った `https://аpple.com/` の QR を読み、通知ではなく「最近読んだ QR」または一覧から開く。
- 起きること：見た目が `apple.com` に似た原文を表示し、実際には別の国際化ドメインを開く。長いユーザー情報を持つ `https://...@evil.example/` でも、メニューの60文字省略で本当のホストが隠れる。
- 根拠：分類結果には安全表示用の `IdnHost` があるが、メニューは `MenuLabel(entry.Text)`、一覧は `Flatten(entry.Text)` を表示する。ツールチップにも別途ホストを出さない。クリックすると原文を再分類して実際のホストを開く。通知のタイトルだけにある偽装対策が、この2経路にはない。
- 確かさ：**確か**。
- 試験：**穴**。`キリル文字の偽ドメインも_xn_になる` と `偽のホストは本当のホストを一行目に出す` は分類と通知の試験。メニュー・一覧の安全表示を試していない。
- 直し方の方向：URL の項目には `IdnHost` を独立して表示し、原文を補足にする。省略してもホストが消えない表示にそろえる。

### [重い] 不正な追加フォルダを設定すると起動できない

- 場所：[Settings.cs:30](D:/work/ClaudeCode/shotlink/ShotLink.Core/Settings.cs:30)、[PhotoWatch.cs:219](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:219)、[TrayContext.cs:63](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:63)、[Program.cs:33](D:/work/ClaudeCode/shotlink/ShotLink.App/Program.cs:33)
- 条件：手で編集した `settings.json` の `extraFolders` に、JSON としては正しい `{"path":"D:\\bad\u0000path"}` を入れて起動する。
- 起きること：起動途中に例外が出て、常駐を開始できない。他の正常な監視先も使えない。
- 根拠：`Normalized` は null と空白だけを除外する。`TrayContext` のコンストラクター → `ApplyTargets` → `SetTargets` → `Deduplicate` の `Path.GetFullPath` が `ArgumentException` を出す。ここにフォルダ単位の例外処理はなく、`Application.Run` より前なので画面スレッドの例外ハンドラーでも回復できない。
- 確かさ：**確か**。
- 試験：**穴**。`空の行は捨てる` と null 配列の試験はあるが、不正な非空パスを試していない。
- 直し方の方向：パスをフォルダごとに検査し、不正な行だけ無効化して知らせる。監視作成時のアクセス拒否や消失競合も、全体を落とさず扱う。

### [重い] ログのアクセス拒否で写真処理ループが黙って停止する

- 場所：[LogWriter.cs:26](D:/work/ClaudeCode/shotlink/ShotLink.Core/LogWriter.cs:26)、[PhotoProcessor.cs:94](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoProcessor.cs:94)、[PhotoProcessor.cs:100](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoProcessor.cs:100)、[TrayContext.cs:362](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:362)
- 条件：起動後、当日のログを読み取り専用にするなどして、写真処理中のログ書き込みが `UnauthorizedAccessException` になる。
- 起きること：写真処理の Task が失敗して終了する。見張りは動いたままなので、アイコンは「見張っています」と表示し、後続写真は行列に入り続ける。
- 根拠：`LogWriter.Write` は `IOException` しか捕まえない。処理中のログ失敗は `LoopAsync` の catch に入るが、その catch も同じ `_log` を呼ぶため再び例外になる。行列が空になった際のメモリログは、その catch の外にある。`_loop` の失敗を常駐中に監視・通知する処理はない。
- 確かさ：**確か**。
- 試験：**穴**。`渡した先で落ちても次の写真へ進む` は、ログ自体が失敗しない場合だけ。アクセス拒否や例外報告中のログ失敗を試していない。
- 直し方の方向：ログ失敗が処理ループへ伝播しないようにする。ループの異常終了を監視し、アイコンと利用者への知らせに反映する。

### [重い] 履歴の保存失敗が、その写真の通知まで止める

- 場所：[History.cs:53](D:/work/ClaudeCode/shotlink/ShotLink.Core/History.cs:53)、[JsonFileStore.cs:48](D:/work/ClaudeCode/shotlink/ShotLink.Core/JsonFileStore.cs:48)、[TrayContext.cs:70](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:70)、[Program.cs:29](D:/work/ClaudeCode/shotlink/ShotLink.App/Program.cs:29)
- 条件：起動後にディスクが満杯になる、または `history.json` が置き換えできない状態で写真を撮る。
- 起きること：その写真の通知も直接開く処理も行われない。保存できない状態が続けば、後続写真も同じように通知されない。メモリ上の履歴とディスク上の履歴も食い違う。
- 根拠：`Handle` は UI に `Post` した後、先に全件の `History.Add` を行う。`Add` は錠の中でメモリを変更してから `Save` し、保存例外を戻す。例外は通知処理へ到達する前に UI コールバックを中断し、`Application.ThreadException` でログになる。非同期の `Post` 内なので、`PhotoProcessor` の「渡した先で落ちても続ける」catch は受け取れない。
- 確かさ：**確か**。
- 試験：**穴**。履歴の正常保存試験と同期 Sink の例外試験はあるが、保存失敗を伴う UI コールバックを試していない。
- 直し方の方向：通知・開く処理を履歴保存の成否から切り離す。保存失敗を利用者に知らせ、メモリの更新と保存の扱いをそろえる。

### [重い] 一度受け付けたパスは、失敗しても再作成されても二度と読まない

- 場所：[PhotoWatch.cs:29](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:29)、[PhotoWatch.cs:159](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:159)、[PhotoProcessor.cs:133](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoProcessor.cs:133)、[FolderWatcher.cs:40](D:/work/ClaudeCode/shotlink/ShotLink.Core/FolderWatcher.cs:40)
- 条件：追加フォルダで `capture.png` を作り、10秒以上書き込みを保持して読み取りを失敗させる。その後、同名の完成画像を置き直す。または同名ファイルを削除・再作成する撮影道具を使う。
- 起きること：同じパスの写真は、アプリを再起動するまで処理されない。あふれ後の再走査でも救済されない。
- 根拠：`OnAppeared` は処理成功より前に `_seen.Add` する。`_seen` から削除する処理はなく、処理失敗も PhotoWatch に返さない。変更通知は購読しておらず、再作成の Created 通知も `_seen` に弾かれる。保存しているのはファイルの世代ではなくパスだけ。
- 確かさ：**確か**。
- 試験：**穴**。`同じファイルは一回だけ` は重複通知の除外だけで、失敗後の再試行・同名の別ファイルを区別していない。
- 直し方の方向：処理待ち・処理済み・失敗を区別する。同じパスでも再作成を識別し、失敗後に完成した画像を再試行できるようにする。

### [重い] あふれ後の再走査に、救済されない取りこぼしがある

- 場所：[PhotoWatch.cs:170](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:170)、[PhotoWatch.cs:183](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:183)、[PhotoWatch.cs:188](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:188)
- 条件：作成日時の古い画像を起動後に大量移動し、Created 通知があふれる。またはサブフォルダ監視中、再走査の途中にアクセス拒否の下位フォルダがある。
- 起きること：前者では新しく入った画像が古い作成日時を理由に除外される。後者では列挙全体が中断し、読める場所の写真まで救済されない。どちらも知らせや再試行がない。
- 根拠：再走査は「フォルダに入った時刻」ではなく `File.GetCreationTime >= _since` で選ぶ。さらに全件を `OrderBy(...).ToArray()` にしてから処理するため、列挙途中の例外でも一件も `OnAppeared` に渡さず catch から戻る。失敗を記録せず、次のあふれ以外に再走査の再試行がない。
- 確かさ：**確か**。
- 試験：**穴**。`FakeWatcher.Overflow` は定義されているが、`PhotoWatchTests` にあふれの試験はない。実際の移動試験も、作成日時とあふれを組み合わせていない。
- 直し方の方向：起動前の写真を除外する基準と、起動後の移動を拾う基準を分けて設計する。下位フォルダ単位で失敗を扱い、救済失敗を知らせて再試行する。

### [中] 回数制限を決定時に使うため、遅れた UI がまとめて開く

- 場所：[QrDispatcher.cs:64](D:/work/ClaudeCode/shotlink/ShotLink.Core/QrDispatcher.cs:64)、[OpenRateLimiter.cs:14](D:/work/ClaudeCode/shotlink/ShotLink.Core/OpenRateLimiter.cs:14)、[TrayContext.cs:72](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:72)、[TrayContext.cs:104](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:104)
- 条件：保存やシェル起動などで UI スレッドが60秒以上詰まる。その間に裏の処理が最初の3件と、60秒後の別の3件について直接開くと決定する。
- 起きること：UI が戻った直後、6件を短時間に開く。待機中に直接開くを OFF にしても、既に作られた決定には適用されない。
- 根拠：回数を使う `limiter.TryTake` は裏の `Decide` で実行する。実際の `Process.Start` は後で UI の `Post` 内から行い、その時点で回数・現在の設定を確認しない。
- 確かさ：**確か**。長い UI 停滞が起きた場合の結果がコード上で確定する。
- 試験：**穴**。`直接開くのは一分に三件まで_超えた分は通知だけ` は決定の間隔だけを検査し、決定と実行の間の遅延を扱わない。
- 直し方の方向：実際に開く直前に回数制限を適用する。遅れて実行する決定には、現在の設定も反映する。

### [中] 再走査と再開が重なると、一時停止中の写真を読む

- 場所：[PhotoWatch.cs:116](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:116)、[PhotoWatch.cs:152](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:152)、[PhotoWatch.cs:170](D:/work/ClaudeCode/shotlink/ShotLink.Core/PhotoWatch.cs:170)
- 条件：一時停止中に大量の写真が入り、あふれによる再走査が始まる。列挙中に利用者が再開する。
- 起きること：一時停止中の写真が通知され、直接開くが ON なら URL を開く。
- 根拠：再走査は一時停止中でも開始し、古い `_since` を錠の中でコピーしてから錠を解放して列挙する。`Resume` は `_since` を更新するが、進行中の再走査は古い値を使い続ける。その後の `OnAppeared` は現在の `_paused` と `_seen` だけを検査し、新しい `_since` や走査の世代を確認しない。
- 確かさ：**確か**。
- 試験：**穴**。`一時停止の間に現れた写真は再開しても知らせない` はイベントを順番に呼ぶ試験で、進行中の再走査を再開と交差させていない。
- 直し方の方向：停止・再開に世代番号を持たせ、古い世代の再走査結果を破棄する。列挙を途中で止められる差し替えを使い、この順序の試験を追加する。

以下は、危険な入口は確認できたものの、結果を断定できない項目です。

### [重い・要確認] 巨大な画像を展開する前の上限がない

- 場所：[WinRtImageLoader.cs:19](D:/work/ClaudeCode/shotlink/ShotLink.App/WinRtImageLoader.cs:19)、[WinRtImageLoader.cs:47](D:/work/ClaudeCode/shotlink/ShotLink.App/WinRtImageLoader.cs:47)、[GrayImage.cs:13](D:/work/ClaudeCode/shotlink/ShotLink.Core/GrayImage.cs:13)
- 条件：圧縮ファイルは小さいが、展開後は数 GB になる寸法の画像を監視先に置く。
- 起きること：全画素を展開する要求が行われる。メモリ圧迫や長時間停止の程度、WIC が先に拒否するかは未確認。
- 根拠：寸法を取得した後、上限検査なしで `GetPixelDataAsync` → `DetachPixelData` を呼ぶ。BGRA への代替では追加の配列も作る。`width * height` は checked 演算ではなく、画像処理そのものには時間上限・キャンセルがない。通常の例外なら `PhotoProcessor` が捕まえるため、必ず落ちるとは断定できない。
- 確かさ：**要確認**。上限の欠如は確か、具体的な停止・終了結果は未確認。
- 試験：**穴**。試験プロジェクトは App を参照しておらず、`WinRtImageLoader` を検査していない。
- 直し方の方向：画素取得前に checked 演算で寸法・画素数・必要メモリを検査する。巨大画像への耐性は、通常の WebP 対応確認とは別に検査する。

### [中・要確認] 通知引数を履歴と照合せず、そのまま操作に使う

- 場所：[Toasts.cs:103](D:/work/ClaudeCode/shotlink/ShotLink.App/Toasts.cs:103)、[TrayContext.cs:122](D:/work/ClaudeCode/shotlink/ShotLink.App/TrayContext.cs:122)、[Program.cs:20](D:/work/ClaudeCode/shotlink/ShotLink.App/Program.cs:20)
- 条件：別のプロセスが通知の COM 起動経路に任意の `kind`・`text` を渡せる場合。または停止中の通知クリックと単一起動の判定が競合する場合。
- 起きること：前者では、ShotLink が発行した通知か確認せず、渡された http/https URL を開く。後者でクリックが既存プロセスへ確実に届くかは未確認。
- 根拠：`OnActivated` は引数を解析し、enum に変換できれば `Activated` に渡す。発行済み通知の識別子・履歴との照合はない。ただし URL は再分類されるため、非 http/https を開ける根拠にはならない。二つ目の起動は通知部品の初期化前に終了し、クリック転送はコメントで Windows 側に依存している。
- 確かさ：**要確認**。引数を信頼するコードは確かだが、外部から呼べる範囲と通知部品の転送保証は未確認。
- 試験：**穴**。通知起動・偽造引数・単一起動と通知クリックの競合を扱う試験はない。
- 直し方の方向：通知には識別子を渡し、保存した内容から操作を復元する。部品の実装を読んで外部起動と転送の保証を確認し、失敗時の動作を決める。

確かめていない範囲：

- 通知ライブラリ、C#/WinRT、WIC、zxing-cpp の依存先内部実装。壊れた画像や通知起動の OS 側の結果は確認していません。
- 実際の VRChat 写真・書き込み途中・WebP は、仕様書で既に挙げられた要確認として扱い、追加の不具合件数には入れていません。
- ジャンクション・8.3名・監視ルートの実際の削除復帰・高 DPI・Windows の通知設定ごとの実機動作。
- 電源断時の JSON 置き換えの耐久性、通知例外のメッセージに QR 全文が含まれるか。
- 配布道具・素材生成器・CI 全体、および既存試験の全件精読。関連する試験は読みましたが、122件の成功は実行して確認していません。