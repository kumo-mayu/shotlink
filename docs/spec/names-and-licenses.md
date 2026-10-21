# 名前とライセンスの調べ

仕様書（`docs/spec.md`）11章の [要確認] を Claude が調べた記録。判断はユーザー（必要なら専門家）。

## 「ShotLink」の商標（2026-10-14）

- **アメリカ**：PGA ツアー（ゴルフ）が、ショットを記録する仕組みの名前に使っている。ソフトウェア（区分 9）の登録「PGA TOUR SHOTLINK」は 2009 年に失効し、今あるのはゴルフの実況・成績の配信（区分 41）の「SHOTLINK」。分野は違うが、同じ名前の有名な物がある（Justia の商標の記録）
- **日本**（J-PlatPat の商標検索。Claude がブラウザで引いた）
  - 文字の「ShotLink」（商標（検索用））：0 件
  - 読みの類似（称呼（類似検索）で「ショットリンク」）：53 件。うち区分 09（ソフトウェアなど）は 20 件で、同じ読みの物は無い
  - 区分 09 で読みが近い物：「ShortLinks（ショートリンクス）」（さつき株式会社、2026-02 出願・審査中）、「SHOWLINK（ショーリンク）」（Shure、登録）、「ほっとリンク／HOT Link」（帝人、登録）、「ThothLink（トートリンク）」（OSSTech、登録）

## 「QRコード」の商標の書き方（2026-10-17）

- デンソーウェーブの FAQ「登録商標文の例を知りたい」（denso-wave.com/ja/adcd/support/faq/trademark/comment/hy25050115.html）の和文の例：「QRコードは株式会社デンソーウェーブの登録商標です」。英文の例は「QR Code is a registered trademark of DENSO WAVE INCORPORATED in Japan and in other countries.」
- 同じ FAQ の「掲載単位」「掲載場所」：名称を使う画面・印刷物ごとに載せる。場所はデザインに合わせて決めてよい
- ShotLink は README・zip の README.txt・設定の窓の「このアプリについて」・読んだ QR の一覧の窓（2026-10-21 に足した）に和文の例のまま載せている（Claude が FAQ を引いた）

## VRChat の名前の使い方（2026-10-14）

- VRChat のブランドガイドラインはロゴの決まりで、ツールの名前の決まりは無い
- ask.vrchat.com の「Branding guideline for tool or service names」（2024-02）への VRChat の返答：VRChat が提供・承認していると見せようとしない限り、用途を示すために「VRChat」を使ってよい
- ShotLink は名前に「VRChat」を入れていない。説明では「VRChat で撮った写真」と用途として書き、README と設定の窓に「VRChat Inc. とは関係のない、非公式のツール」と書いている

## Windows SDK の .NET 向けの定義（2026-10-14）

- exe に入る `Microsoft.Windows.SDK.NET.dll`（トースト通知のための Windows の API の橋渡し）は、NuGet の包み `Microsoft.Windows.SDK.NET.Ref` から来る。包みのライセンスは **Windows SDK のライセンス条項**（https://aka.ms/WinSDKLicenseURL）で、今ある版はどれも同じ
- .NET で Windows の API を使うアプリ（TFM が `net*-windows10.0.*`）には必ず入る物で、ShotLink だけの事情ではない
- 条項の再配布の項（Distributable Code）には、次のような条件が並んでいる：配るプログラムに主な機能を足すこと・著作権表示を出すこと・配った先の人にもこの条項と同程度に守る条件へ同意させること・Microsoft を免責すること・Microsoft の OS 以外で動かさないこと
- Distributable Code は「REDIST.TXT の一覧に載っているファイル」。一覧（https://learn.microsoft.com/en-us/legal/windows-sdk/redist）に `Microsoft.Windows.SDK.NET.Ref` の包みのファイル（`Microsoft.Windows.SDK.NET.dll`・`WinRT.Runtime.dll`）が名指しで載っていて、「WinRT の API を呼べるように、アプリの一部として改変せずに配ってよい」とある
- 条件のうち「配った先の人にも、この条項と同程度に守る条件へ同意させる」に合わせるため、zip の README と THIRD-PARTY-NOTICES.txt に「これらの部品は Microsoft の条項に従い、MIT は及ばない」と書く（ユーザー判断：案 A）
- 「Microsoft を免責する」は配る人が負う形式上の条件で、書いて済ませるものではない。同じ部品を使う .NET のアプリを配る人は皆この条件の下にある。気になるなら専門家に相談する
