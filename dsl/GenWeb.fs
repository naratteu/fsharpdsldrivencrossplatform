/// 웹 쪽 — 런처용 다리(bridge.g.js · d.ts), 모듈 페이지 개발자용 타입(app.g.d.ts)
module GenWeb

open Model
open Emit
open Spec

let private tsObj (m: Message) = $"""{{ type: "{m.Name}"{m.Fields |> List.map (fun (n, t) -> $"; {n}: {tsType t}") |> String.concat ""} }}"""

/// 일반 스크립트(모듈 아님) — Android 의 asset 에서도 그대로 읽힌다
let bridgeJs () =
    lines [
        header
        "(() => {"
        "  // 런처 → 앱: Windows 는 WebView2, Android 는 addWebMessageListener 가 넣어 준 객체"
        "  const post = (m) => {"
        "    const s = JSON.stringify(m);"
        "    if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(s);"
        "    else if (window.appBridge) window.appBridge.postMessage(s);"
        "    else console.warn('앱 밖에서 열림 — 보낼 곳이 없다', s);"
        "  };"
        "  const handlers = [];"
        "  window.bridge = {"
        "    modules: ["
        for m in modules do
            $"      {{ key: {lit m.Key}, name: {lit m.Name}, description: {lit m.Description}, accent: {lit m.Accent}, path: {lit m.Path} }},"
        "    ],"
        for m in toApp do
            let args = m.Fields |> List.map fst |> String.concat ", "
            $"""    {camel m.Name}: ({args}) => post({{ type: "{m.Name}"{m.Fields |> List.map (fun (n, _) -> ", " + n) |> String.concat ""} }}),"""
        "    onMessage: (h) => handlers.push(h),"
        "    // 앱 → 런처: 앱이 이 함수를 스크립트로 부른다(JSON 문자열)"
        "    receive: (s) => { const m = JSON.parse(s); handlers.forEach((h) => h(m)); },"
        "  };"
        "})();" ]

let bridgeDts () =
    lines [
        header
        $"""export type ToApp = {toApp |> List.map tsObj |> String.concat " | "};"""
        $"""export type ToWeb = {toWeb |> List.map tsObj |> String.concat " | "};"""
        "export interface Module { key: string; name: string; description: string; accent: string; path: string }"
        "export interface Bridge {"
        "  modules: Module[];"
        for m in toApp do
            $"""  {camel m.Name}({m.Fields |> List.map (fun (n, t) -> $"{n}: {tsType t}") |> String.concat ", "}): void;"""
        "  onMessage(handler: (message: ToWeb) => void): void;"
        "}"
        "declare global { interface Window { bridge: Bridge } }" ]

let appDts () =
    lines [
        header
        "// 모듈 페이지용 — 앱이 넣어 주는 window.app. 모듈마다 허락한 기능만 생긴다(앱 밖에서 열면 없다)"
        "export interface App {"
        for c in capabilities do
            let who = grants |> List.filter (snd >> List.contains c.Name) |> List.map fst |> String.concat ", "
            let ret = match c.Returns with Some t -> tsType t + " | null" | None -> "null"
            $"  /** {c.Doc}. 허락: {who} */"
            $"""  {camel c.Name}({c.Args |> List.map (fun (n, t) -> $"{n}: {tsType t}") |> String.concat ", "}): Promise<{ret}>;"""
        "}"
        "/** 앱 → 모듈 페이지 이벤트 — window 의 message 이벤트로 온다 */"
        $"""export type AppEvent = {moduleEvents |> List.map tsObj |> String.concat " | "};"""
        "declare global { interface Window { app?: App } }" ]
