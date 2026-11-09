/// DSL 어휘 — 명세(Spec.fs)는 이 낱말들로만 적는다. 필요한 만큼만 있고, 필요해지면 명세와 함께 는다
module Model

type Field =
    | Str
    | Int
    | Bool
    | Strs

/// 메시지 하나 — 이름과 필드
type Message = { Name: string; Fields: (string * Field) list; Doc: string }

/// 업무 모듈 — 탭 하나가 모듈 하나. 주소는 {서버}{Path}
type Module = { Key: string; Name: string; Description: string; Accent: string; Path: string }

/// 모듈 페이지가 부를 수 있는 앱 기능 — window.app.이름(인자) 가 Promise 로 결과를 돌려준다
type Capability = { Name: string; Args: (string * Field) list; Returns: Field option; Doc: string }

// ── UI 부품 ──

/// 부품 안의 값
type Value =
    | Text of string
    /// "#RRGGBB" 또는 "#AARRGGBB"
    | Color of string
    | Flag of bool
    /// 테마 색 이름
    | Token of string
    /// 부품 속성
    | Prop of string
    /// 불 속성에 따라 둘 중 하나
    | If of prop: string * whenTrue: Value * whenFalse: Value

/// 부품을 이루는 것 — 이 여섯 가지뿐이다
type Node =
    /// 가로로 늘어놓기(세로 가운데 맞춤)
    | Row of spacing: float * Node list
    /// 동그라미
    | Dot of size: float * fill: Value
    /// 글자
    | Label of text: Value * size: float * bold: Value * color: Value
    /// 누를 수 있는 기호 — 누르면 이벤트
    | Glyph of glyph: Value * size: float * color: Value * onTap: string
    /// 둥근 상자
    | Pill of padding: (float * float * float * float) * radius: float * fill: Value * child: Node
    /// 불 속성이 참일 때만
    | Show of prop: string * Node

type PropType =
    | PText
    | PColor
    | PFlag

type Component =
    { Name: string
      Doc: string
      Props: (string * PropType) list
      Events: string list
      /// 부품 전체를 누르면 나는 이벤트
      OnTap: string option
      Root: Node }
