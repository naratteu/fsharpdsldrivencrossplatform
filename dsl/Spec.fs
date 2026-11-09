/// 명세 — 앱이 "무엇을 가졌는지"만 적는다. 어떻게 하는지는 앱(C#)마다 따로 짠다
/// 여기를 바꾸면 그 영향은 앱 빌드 오류로 드러난다(생성된 인터페이스를 구현하지 않은 곳)
module Spec

open Model

let msg name fields doc : Message = { Name = name; Fields = fields; Doc = doc }
let (=>) name field = name, field

// ── 런처 ↔ 앱 ──

/// 런처 → 앱 — 앱은 ILauncherHandler 로 받는다
let toApp = [
    msg "Ready" [] "런처가 준비됐다 — 앱은 Hello · Launcher 로 답한다"
    msg "OpenModule" [ "key" => Str ] "모듈을 탭으로 연다(이미 열렸으면 그 탭으로)"
    msg "SetServer" [ "url" => Str ] "서버 주소를 바꾼다 — 열린 탭은 모두 닫힌다"
    msg "Photo" [ "data" => Str ] "카메라 화면의 결과 — JPEG base64, 취소면 \"\""
]

/// 앱 → 런처 — 앱은 ToLauncher.이름(...) 으로 보낸다
let toWeb = [
    msg "Hello" [ "platform" => Str; "nativeAot" => Bool ] "앱 정보"
    msg "Launcher" [ "server" => Str; "opened" => Strs ] "서버 주소, 열려 있는 모듈 키"
    msg "Camera" [] "카메라 화면을 띄운다 — 결과는 Photo 로"
]

// ── 모듈 ──

let modules = [
    { Key = "work"; Name = "작업"; Description = "작업 지시 · 실적"; Accent = "#3B82C4"; Path = "/work/" }
    { Key = "stock"; Name = "재고"; Description = "입고 · 이동 · 출고"; Accent = "#2F9E77"; Path = "/stock/" }
    { Key = "quality"; Name = "품질"; Description = "검사 · 불량 기록"; Accent = "#E08A1E"; Path = "/quality/" }
    { Key = "ship"; Name = "출하"; Description = "포장 · 출하"; Accent = "#7A5AF8"; Path = "/ship/" }
]
let defaultServer = "http://localhost:8080"

// ── 모듈 페이지 ↔ 앱 ──

/// 모듈 페이지가 부를 수 있는 앱 기능 — 앱은 ICapabilities 로 구현한다. 취소 · 실패면 null
let capabilities = [
    { Name = "TakePhoto"; Args = []; Returns = Some Str; Doc = "카메라로 한 장 찍어 JPEG base64 로" }
]

/// 모듈마다 허락한 기능 — 여기 없는 기능은 그 모듈 페이지에 아예 생기지 않고, 요청해도 앱이 거절한다
let grants = [
    "work", [ "TakePhoto" ]
    "quality", [ "TakePhoto" ]
]

/// 앱 → 모듈 페이지 이벤트 — 페이지는 window 의 message 이벤트로 { type, ... } 를 받는다
let moduleEvents = [
    msg "scan" [ "data" => Str ] "스캐너 입력 — 보고 있는 탭에만"
]

// ── 스캐너 ──

/// (VID, PID) — COM 포트 에뮬레이션(CDC-ACM) 모드의 USB 바코드 스캐너
let scanners = [
    0x0C2E, 0x0934, "Honeywell CCB01-010BT"
    0x1C40, 0x0439, "KOAMTAC KDC280"
]
let scannerBaudRate = 9600

// ── 모양 ──

/// 테마 색
let colors = [
    "Bar", "#24527A"
    "BarDeep", "#1B3F5E"
    "Page", "#FDFDFB"
    "TextPrimary", "#1A1A1A"
    "TextSecondary", "#6B7280"
    "OnBar", "#FFFFFF"
    "OnBarDim", "#B3FFFFFF"
    "OnBarFaint", "#66FFFFFF"
    "Live", "#4ADE80"
]
/// 테마 크기(dp)
let sizes = [
    "BarHeight", 44.0
]

/// 상단 바 부품 — 배치(어떤 부품을 몇 개 어디에)는 앱이 정한다
let components = [
    { Name = "HomeMark"; Doc = "앱 표시 — 누르면 런처"
      Props = []; Events = [ "Clicked" ]; OnTap = Some "Clicked"
      Root = Pill((10.0, 10.0, 10.0, 10.0), 0.0, Color "#00000000",
                  Pill((5.0, 5.0, 5.0, 5.0), 5.0, Token "OnBar", Pill((5.0, 5.0, 5.0, 5.0), 2.0, Token "Bar", Row(0.0, [])))) }

    { Name = "TabHeader"; Doc = "모듈 탭 — 보고 있는 탭만 바탕이 있고 ✕ 로 닫는다"
      Props = [ "Title", PText; "Accent", PColor; "Active", PFlag ]; Events = [ "Clicked"; "CloseClicked" ]; OnTap = Some "Clicked"
      Root = Pill((14.0, 6.0, 8.0, 6.0), 8.0, If("Active", Token "Page", Color "#00000000"),
                  Row(8.0, [
                      Dot(8.0, If("Active", Prop "Accent", Token "OnBarDim"))
                      Label(Prop "Title", 14.0, Prop "Active", If("Active", Token "TextPrimary", Token "OnBar"))
                      Show("Active", Glyph(Text "✕", 13.0, Token "TextSecondary", "CloseClicked")) ])) }

    { Name = "TabDivider"; Doc = "탭 사이 구분선 — 보고 있는 탭 옆에서는 자리만"
      Props = [ "Visible", PFlag ]; Events = []; OnTap = None
      Root = Pill((2.0, 8.0, 2.0, 8.0), 0.0, Color "#00000000", Pill((0.5, 8.0, 0.5, 8.0), 0.0, If("Visible", Token "OnBarFaint", Color "#00000000"), Row(0.0, []))) }

    { Name = "BarButton"; Doc = "바의 기호 버튼(+ ↻) — 꺼지면 흐리게"
      Props = [ "Glyph", PText; "On", PFlag ]; Events = [ "Clicked" ]; OnTap = None
      Root = Pill((8.0, 4.0, 8.0, 4.0), 0.0, Color "#00000000", Glyph(Prop "Glyph", 20.0, If("On", Token "OnBar", Token "OnBarFaint"), "Clicked")) }

    { Name = "ScannerChip"; Doc = "스캐너 상태"
      Props = [ "Text", PText; "On", PFlag ]; Events = []; OnTap = None
      Root = Pill((10.0, 4.0, 10.0, 4.0), 13.0, Token "BarDeep",
                  Row(6.0, [ Dot(8.0, If("On", Token "Live", Token "OnBarDim")); Label(Prop "Text", 12.0, Flag false, Token "OnBar") ])) }
]
