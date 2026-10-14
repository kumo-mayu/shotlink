// THIRD-PARTY-NOTICES.txt を組む。配る exe は .NET を同梱した単一ファイルなので、exe の中に入る部品すべての
// 許諾文と第三者の表示を添える（Chmonos で .NET の実行環境の分が抜けていたことがある。Chmonos の docs/dev/signing.md）。
//
//   node tools/release/make-notices.mjs
//
// 部品の版は、配る形で組んだときの deps.json から読む（先に dotnet publish ShotLink.App -p:PublishProfile=win-x64）。
// 本文は NuGet の包み（手元のキャッシュ）と、Windows Forms・C#/WinRT の GitHub の該当する版から取る。
// SDK・部品の版を上げたら走らせ直してコミットする。release の workflow は、告知の版と同梱する版が合わなければ止まる
import fs from "fs";
import os from "os";
import path from "path";

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "../..");
const nuget = process.env.NUGET_PACKAGES ?? path.join(os.homedir(), ".nuget", "packages");
const deps = findDeps(path.join(root, "ShotLink.App", "obj", "Release"));
const text = fs.readFileSync(deps, "utf8");

const runtime = version(/runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64\/([0-9.]+)/);
const desktop = version(/runtimepack\.Microsoft\.WindowsDesktop\.App\.Runtime\.win-x64\/([0-9.]+)/);
const toolkit = version(/"Microsoft\.Toolkit\.Uwp\.Notifications\/([0-9.]+)"/);
const zxing = version(/"ZXingCpp\/([0-9.]+)"/);
const sdkRef = version(/"Microsoft\.Windows\.SDK\.NET\.Ref\/([0-9.]+)"/, true) ?? latest("microsoft.windows.sdk.net.ref");

const winforms = await fetchText(`https://raw.githubusercontent.com/dotnet/winforms/v${desktop}/THIRD-PARTY-NOTICES.TXT`);
const cswinrt = await fetchText("https://raw.githubusercontent.com/microsoft/CsWinRT/master/LICENSE");

const line = "-".repeat(80);
const parts = [
  `ShotLink に同梱しているライブラリ
==================================

ShotLink は次のライブラリを使っています。それぞれのライセンスに従って同梱しています。
ShotLink.exe は .NET を中に含む形（自己完結・単一ファイル）で組んでいるので、.NET の実行環境と
Windows Forms の許諾文と第三者の表示も添えています。
ShotLink.exe is a self-contained single-file build and includes the .NET runtime and Windows Forms.
`,
  section(`zxing-cpp（.NET 向けの包み ZXingCpp ${zxing} と、その中のネイティブの ZXing.dll）
https://github.com/zxing-cpp/zxing-cpp
Copyright Axel Waggershauser and zxing-cpp contributors
ZXing（https://github.com/zxing/zxing。Copyright ZXing authors）を C++ へ移したもの
License: Apache License 2.0（全文はこのファイルの最後）`),
  section(`libzint（zxing-cpp のネイティブの ZXing.dll の中に含まれる）
https://github.com/zint/zint
License: BSD 3-Clause License

${read(path.join(root, "tools", "release", "libzint-license.txt"))}`),
  section(`Windows Community Toolkit - Microsoft.Toolkit.Uwp.Notifications ${toolkit}
https://github.com/CommunityToolkit/WindowsCommunityToolkit
License: MIT

${read(path.join(nuget, "microsoft.toolkit.uwp.notifications", toolkit, "License.md"))}`),
  section(`C#/WinRT - WinRT.Runtime（Windows の API を .NET から使う橋渡し）
https://github.com/microsoft/CsWinRT
License: MIT

${cswinrt}`),
  section(`Microsoft.Windows.SDK.NET ${sdkRef}（Windows の API の .NET 向けの定義）
Copyright (c) Microsoft Corporation. All rights reserved.
License: Microsoft Windows SDK License Terms
https://aka.ms/WinSDKLicenseURL`),
  section(`.NET Runtime ${runtime} (Microsoft.NETCore.App)
https://github.com/dotnet/runtime
License: MIT

${read(path.join(nuget, "microsoft.netcore.app.runtime.win-x64", runtime, "LICENSE.TXT"))}`),
  section(`.NET Runtime ${runtime} - third-party notices
https://github.com/dotnet/runtime/blob/v${runtime}/THIRD-PARTY-NOTICES.TXT
License: various (see below)

${read(path.join(nuget, "microsoft.netcore.app.runtime.win-x64", runtime, "THIRD-PARTY-NOTICES.TXT"))}`),
  section(`Windows Desktop Runtime ${desktop} (Microsoft.WindowsDesktop.App: Windows Forms)
https://github.com/dotnet/windowsdesktop
License: MIT

${read(path.join(nuget, "microsoft.windowsdesktop.app.runtime.win-x64", desktop, "LICENSE"))}`),
  section(`Windows Forms ${desktop} - third-party notices
https://github.com/dotnet/winforms/blob/v${desktop}/THIRD-PARTY-NOTICES.TXT
License: various (see below)

${winforms}`),
  `${"=".repeat(80)}
Apache License 2.0
${"=".repeat(80)}
${read(path.join(root, "tools", "release", "apache-2.0.txt"))}`,
];

const out = path.join(root, "THIRD-PARTY-NOTICES.txt");
fs.writeFileSync(out, parts.join("\n").replace(/\r\n/g, "\n"));
console.log(`${out}：.NET ${runtime}・Windows Desktop ${desktop}・ZXingCpp ${zxing}・Toolkit ${toolkit}・SDK.NET ${sdkRef}`);

function section(body) {
  return `${line}\n${body.trimEnd()}\n${line}\n`;
}

function read(file) {
  return fs.readFileSync(file, "utf8").replace(/^﻿/, "").trimEnd();
}

function version(re, optional = false) {
  const m = text.match(re);
  if (!m && !optional) throw new Error(`deps.json に ${re} が無い（配る形で組んだか）`);
  return m?.[1];
}

function latest(pkg) {
  return fs.readdirSync(path.join(nuget, pkg)).sort().at(-1);
}

function findDeps(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true, recursive: true })) {
    if (entry.isFile() && entry.name === "ShotLink.deps.json" && (entry.parentPath ?? entry.path).includes("win-x64")) {
      return path.join(entry.parentPath ?? entry.path, entry.name);
    }
  }
  throw new Error(`${dir} に win-x64 の ShotLink.deps.json が無い。先に dotnet publish ShotLink.App -p:PublishProfile=win-x64`);
}

async function fetchText(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url} を取れない：${res.status}`);
  return (await res.text()).trimEnd();
}
