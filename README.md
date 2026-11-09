# fsharpdsldrivencrossplatform

F# 를 활용한 DSL 주도 크로스플랫폼 개발 PoC.

서로 호환되지 않는 두 UI 프레임워크 — Windows 의 **Avalonia** 와 **.NET Android 뷰** — 가 같은 부품과 같은 계약을 쓰게 한다.
부품과 계약은 F# DSL 로 한 번만 적고, 거기서 각 프레임워크용 C# 이 생성된다. 앱(소비자)은 둘 다 C# 이다.

## 스펙 주도 개발과 DSL 주도 개발

둘은 구조가 비슷하다 — 명세를 먼저 쓰고, 플랫폼마다 코드를 만든다. 다른 것은 **명세를 대하는 태도**다.

| | 스펙 주도 | DSL 주도 |
|---|---|---|
| 명세 | 자연어 문서. 사람(이나 언어 모델)이 읽고 해석한다 | 타입이 있는 코드. 컴파일러가 읽는다 |
| 플랫폼 코드 | 명세를 보고 플랫폼마다 새로 짠다(언어 모델이 짜기도 한다) | 명세에서 결정론적으로 생성된다 |
| 명세가 바뀌면 | 다시 해석해 다시 짠다 — 무엇이 바뀌었는지는 사람이 확인한다 | 다시 생성되고, 영향받는 곳이 **컴파일 오류**로 드러난다 |
| 어울리는 일 | 철마다 화면을 갈아엎는 서비스 — 어차피 새로 짤 것이라 해석이 조금 달라져도 거리낄 게 없다 | 오래 가는 업무 시스템 — 작은 변화도 하나하나 추적하고, 시스템을 보수적이고 자명하게 유지한다 |

DSL 은 범용 언어가 아니다. 이 앱에 필요한 만큼의 낱말(`dsl/Model.fs`)만 있고, 필요해지면 명세와 함께 는다.

## 구조

```
dsl/                F# — DSL 과 생성기
  Model.fs            DSL 낱말: 메시지 · 모듈 · 앱 기능 · UI 부품(Row · Dot · Label · Glyph · Pill · Show)
  Spec.fs             명세: 이 앱이 무엇을 가졌는지
  Gen*.fs             생성기: 계약(C#) · Avalonia 부품(C#) · Android 부품(C#) · 웹(JS · d.ts)
build/Dsl.targets   앱이 빌드될 때 dsl 을 실행해 obj/dsl/ 에 생성물을 만들고 컴파일에 넣는다
src/Windows/        C# — Avalonia 12 + 공식 WebView
src/Android/        C# — Activity + WebView
web/index.html      런처 페이지(모듈 카드 · 서버 주소 · 카메라 화면)
```

생성물은 저장소에 두지 않는다(`obj/dsl/`, IDE 에서는 보인다). 바뀐 것은 명세로 보고, 그 영향은 컴파일로 확인한다.

### 명세에 담는 것 — "무엇을 가졌나"

- 런처 ↔ 앱 메시지 → `ILauncherHandler`(앱이 구현), `ToLauncher.*`(앱이 보냄)
- 모듈 목록, 기본 서버 주소 → `Modules`
- 모듈 페이지가 부를 수 있는 앱 기능과 모듈별 허락 → `ICapabilities`(앱이 구현), `Capabilities.Script/Handle`, `app.g.d.ts`
- 앱 → 모듈 페이지 이벤트(스캔) → `ModuleEvents.*`
- 지원 스캐너(VID/PID) → `Scanners`
- 테마 색 · 크기 → `Tokens`
- 상단 바 부품(홈 · 탭 · 구분선 · 기호 버튼 · 스캐너 칩) → Avalonia 컨트롤 / Android 뷰

### 앱에 두는 것 — "어떻게 하나"

탭 열기 · 닫기, 서버 주소 저장, 카메라 화면 띄우기, 스캐너 연결 같은 실제 동작은 앱마다 따로 짠다.
같은 기능이 있다는 것만 명세가 보장하고, 하는 방법은 플랫폼마다 다르다(Windows: 레지스트리 · `System.IO.Ports`, Android: `UsbManager` 로 CDC-ACM).

### 명세 한 줄의 파급

```fsharp
// dsl/Spec.fs
let capabilities = [
    { Name = "TakePhoto"; Args = []; Returns = Some Str; Doc = "카메라로 한 장 찍어 JPEG base64 로" }
    { Name = "Vibrate"; Args = [ "ms" => Int ]; Returns = None; Doc = "진동" }   // ← 한 줄 더하면
]
```

```
src/Windows/MainWindow.cs   error CS0535: 'MainWindow' does not implement interface member 'ICapabilities.Vibrate(string, int)'
src/Android/MainActivity.cs error CS0535: 'MainActivity' does not implement interface member 'ICapabilities.Vibrate(string, int)'
```

모듈 페이지용 타입(`app.g.d.ts`)에는 `vibrate(ms: number): Promise<null>` 이 바로 생긴다.

## 앱 기능과 허락

```fsharp
let grants = [
    "work", [ "TakePhoto" ]
    "quality", [ "TakePhoto" ]
]
```

- 허락한 모듈 페이지에만 문서 시작 때 `window.app` 이 생긴다(`await window.app.takePhoto()` → JPEG base64 또는 `null`). 다른 모듈엔 `window.app` 자체가 없다
- 요청은 서버 출처에서 온 것만 받고, 허락 검사는 생성된 코드가 한다
- 사진은 모듈 페이지가 카메라를 직접 쓰지 않는다 — 앱이 자기 카메라 화면(런처)을 띄워 찍고 결과만 돌려준다

## 빌드

```sh
dotnet publish src/Windows -r win-x64 -c Release    # win-arm64 도 같다. 맥 · Linux 에서도 된다
dotnet publish src/Android -c Release
```

## 라이선스

MIT
