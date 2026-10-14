// 本物の写真フォルダ・VRChat の設定・本番の設定と履歴を守る。CLAUDE.md「絶対に破らないこと」2・3。
// ShotLink は写真を読むだけのソフトで、作業でも本物の写真を動かしたり消したりしてはいけない。
// 見張りの確かめで「素材を見張っているフォルダへ移動する」手順があり、移動先を本物の写真フォルダと
// 取り違えやすい。決め事だけでは長い会話で抜けることがあるので、書き込みの手前で止める。
// 試験と確かめの保存先は SHOTLINK_HOME で作業用フォルダを指すので、これらの形には当たらない
import fs from "fs";

const targets = [
  { re: /Pictures[\\/]+VRChat([\\/]|$|["'\s])/i, name: "本物の VRChat の写真フォルダ" },
  { re: /LocalLow[\\/]+VRChat[\\/]+VRChat[\\/]+config\.json/i, name: "VRChat の config.json" },
  { re: /(^|[\\/])ShotLink[\\/]+(settings|history)\.json/i, name: "本番の ShotLink の設定・履歴" },
];

let input;
try {
  input = JSON.parse(fs.readFileSync(0, "utf8"));
} catch {
  process.exit(0); // 読めない入力で道具を止めない
}

const tool = input.tool_name ?? "";
const args = input.tool_input ?? {};

// ファイルを書く道具は止める
const path = args.file_path ?? args.notebook_path ?? "";
const hitPath = path && targets.find((t) => t.re.test(path));
if (hitPath) {
  answer("deny", `${hitPath.name}には書き込まない（CLAUDE.md「絶対に破らないこと」）。確かめは作業用フォルダと SHOTLINK_HOME で行う。`);
}

// シェルは読むだけのこともあるので、止めずにユーザに確かめてもらう
if (tool === "Bash" || tool === "PowerShell") {
  const hitCmd = targets.find((t) => t.re.test(args.command ?? ""));
  if (hitCmd) {
    answer("ask", `${hitCmd.name}に触れるコマンドです。読むだけか確かめてください（CLAUDE.md「絶対に破らないこと」）。`);
  }
}

process.exit(0);

function answer(decision, reason) {
  // Windows の文字コードで化けないよう、日本語は \u 形式にして出す
  const json = JSON.stringify({
    hookSpecificOutput: { hookEventName: "PreToolUse", permissionDecision: decision, permissionDecisionReason: reason },
  }).replace(/[^\x00-\x7f]/g, (c) => "\\u" + c.charCodeAt(0).toString(16).padStart(4, "0"));
  process.stdout.write(json);
  process.exit(0);
}
