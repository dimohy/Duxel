# UI DSL 레퍼런스

이 문서는 현재 코드(`UiDslParser`, `UiDslWidgetDispatcher`, `UiDslPipeline`) 기준으로 정리한 DSL 문법입니다.

## 핵심 요약

- 파일 형식: `.ui` (들여쓰기 기반 트리)
- 파서: `Duxel.Core.Dsl.UiDslParser`
- 런타임 렌더링: `UiDslScreen` (Managed 핫리로드 + NativeAOT 소스 생성)
- 상태 저장: `UiDslState` 또는 `IUiDslValueSource`
- 이벤트 연결: `IUiDslEventSink`

## 문법 규칙

### 1) 들여쓰기

- 기본 들여쓰기 크기는 2칸입니다.
- 기본 옵션에서 탭은 허용되지 않습니다(`AllowTabs=false`).
- 들여쓰기 폭이 2칸 단위가 아니면 파서 예외가 발생합니다.

```text
Window "Main"
  Row
    Button Id="ok" Text="OK"
```

### 2) 라인/토큰

- 한 줄은 `NodeName` + 인자들로 구성됩니다.
- 문자열은 큰따옴표(`"`)를 사용합니다.
- 공백 기준 토큰 분리이며, 문자열 내부 공백은 유지됩니다.
- 주석은 `#` 또는 `//`를 지원합니다(문자열 바깥에서만).

### 3) 인자 형식

- 명명 인자: `Key=Value`
- 위치 인자: 순서 기반
- 혼용 가능

```text
Button Id="play" Text="Play" Size="120,32"
Button "play" "Play"
```

`Button`처럼 `Id`/`Text`를 받는 노드는 다음 규칙을 따릅니다.

1. `Id`와 `Text`가 모두 없으면 위치 인자 1개는 `(Id=Text=값)`으로 해석
2. 위치 인자 2개면 `(Id, Text)` 순서로 해석
3. `Id`만 있으면 `Text=Id`, `Text`만 있으면 `Id=Text`

## Begin 별칭(normalization)

파싱 후 아래 노드는 자동으로 `Begin*` 이름으로 정규화됩니다.

- `Group` → `BeginGroup`
- `Child` → `BeginChild`
- `MenuBar` → `BeginMenuBar`
- `MainMenuBar` → `BeginMainMenuBar`
- `Menu` → `BeginMenu`
- `Popup` → `BeginPopup`
- `PopupModal` → `BeginPopupModal`
- `PopupContextItem` → `BeginPopupContextItem`
- `PopupContextWindow` → `BeginPopupContextWindow`
- `PopupContextVoid` → `BeginPopupContextVoid`
- `TabBar` → `BeginTabBar`
- `TabItem` → `BeginTabItem`
- `Table` → `BeginTable`
- `Tooltip` → `BeginTooltip`
- `ItemTooltip` → `BeginItemTooltip`
- `DragDropSource` → `BeginDragDropSource`
- `DragDropTarget` → `BeginDragDropTarget`
- `Disabled` → `BeginDisabled`
- `MultiSelect` → `BeginMultiSelect`

추가 규칙:

- `Combo`/`ListBox`는 **자식 블록이 있을 때만** `BeginCombo`/`BeginListBox`로 정규화됩니다.
- 자식이 없으면 단일 위젯 호출(`Combo`/`ListBox`)로 처리됩니다.

## 값 파싱 규칙

`UiDslArgReader` 기준:

- `bool`: `true/false` 또는 `0/1`
- `int/uint/float/double`: invariant culture 숫자
- `UiVector2/UiVector4`: `x,y` 또는 `x;y` 또는 `x|y` 형식
- `UiColor`: `#RRGGBB`, `#RRGGBBAA`, `0x...`, 정수
- 목록형(`Items`, 배열): `|`, `,`, `;` 구분자 지원
- enum: 이름 문자열 (`Enum.TryParse`, 대소문자 무시)

## 상태/이벤트 바인딩

상태성 위젯(`Checkbox`, `Slider*`, `Input*`, `Combo`, `ListBox` 등)은 `Id`를 키로 값을 읽고 씁니다.

- 기본 저장소: `UiDslState`
- 외부 바인딩: `IUiDslValueSource`
- 이벤트: `IUiDslEventSink`
- 편의 바인더: `UiDslEventBinder` (ID별 콜백 등록)

런타임 바인딩은 `IUiDslEventSink`/`IUiDslValueSource` 인터페이스를 구현하여 `UiDslScreen`에 전달합니다.

### UiDslEventBinder

`IUiDslEventSink`를 직접 구현하는 대신, `UiDslEventBinder`로 ID별 플루언트 바인딩이 가능합니다:

```csharp
var events = new UiDslEventBinder()
    .Bind("save", () => Console.WriteLine("Save clicked"))
    .Bind("file.new", () => Console.WriteLine("New file"))
    .BindCheckbox("dark_mode", value => Console.WriteLine($"Dark mode: {value}"))
    .OnAnyButton(id => Console.WriteLine($"Unhandled button: {id}"))
    .OnAnyCheckbox((id, v) => Console.WriteLine($"Unhandled checkbox: {id}={v}"));

var screen = new UiDslScreen("Ui/Main.ui", eventSink: events);
```

### Text 바인딩

`Text` 위젯은 `Bind`를 통한 동적 텍스트 표시를 지원합니다:

```text
Text Bind="status" Default="Ready"
```

`Bind`가 지정되면 정적 문자열 대신 `IUiDslValueSource`/`UiDslState`에서 값을 읽어 표시합니다.

## 제어 흐름

Kotlin DSL과 SwiftUI ViewBuilder 패턴을 참고하여, 조건부/반복/분기 렌더링을 지원합니다.

### If / ElseIf / Else

상태 값에 기반한 조건부 렌더링입니다.

```text
# Bool 조건
If Bind="dark_mode"
  Text "다크 모드 활성화"
ElseIf Bind="high_contrast"
  Text "고대비 모드"
Else
  Text "기본 테마"

# 부정 조건
If Bind="show_advanced" Not=true
  Text "고급 옵션이 숨겨져 있음"

# 문자열 동등 비교
If "user_role" = "admin"
  Button "admin_panel" "관리자 패널"

# 문자열 부정 비교
If "status" != "ready"
  Text "시스템이 준비되지 않았습니다"
```

**규칙:**
- `If Bind="key"` — bool 상태 `key`가 true일 때 렌더링
- `If Bind="key" Not=true` — 부정 조건
- `If "key" = "value"` — 문자열 동등 비교
- `If "key" != "value"` — 문자열 부정 비교
- `ElseIf`는 같은 문법이며, 앞선 `If`/`ElseIf`가 만족됐으면 스킵
- `Else`는 모든 `If`/`ElseIf`가 false일 때만 렌더링

### Visible

`If Bind=...`의 축약형으로, `Else` 없이 표시/숨김만 제어합니다.

```text
Visible Bind="show_advanced"
  SliderFloat "gamma" "Gamma" 0.5 0.0 3.0
  SliderFloat "contrast" "Contrast" 1.0 0.0 2.0
```

### ForEach

숫자 범위로 자식 위젯을 반복 렌더링합니다. 자식은 버퍼링 후 N회 재생됩니다.

```text
# 범위: start,end (양쪽 포함)
ForEach Range=1,5
  Button "item_{_index}" "Item {_index}"

# 카운트 기반 (0부터 N-1)
ForEach Count=3
  Text "Row {_index}"

# 사용자 정의 변수명
ForEach Range=0,9 Var=i
  Text "Entry {i}"
```

**템플릿 치환:** 문자열 인자 내 `{_index}` (또는 `{변수명}`)가 현재 반복 값으로 치환됩니다.

**파라미터:**
| 파라미터 | 설명 | 예시 |
|---|---|---|
| `Range=start,end` | 포함 범위 [start, end] | `Range=1,5` → 1,2,3,4,5 |
| `Range=N` | 범위 [0, N] | `Range=4` → 0,1,2,3,4 |
| `Count=N` | 범위 [0, N-1] | `Count=5` → 0,1,2,3,4 |
| `Var=name` | 반복 변수명 (기본: `_index`) | `Var=i` |

### Switch / Case / Default

문자열 상태 값에 기반한 다중 분기 렌더링입니다.

```text
Switch Bind="theme"
  Case "Dark"
    Text "다크 테마 사용 중"
  Case "Light"
    Text "라이트 테마 사용 중"
  Default
    Text "알 수 없는 테마"
```

**규칙:**
- `Switch Bind="key"` — 상태에서 문자열 값을 읽음
- `Case "value"` — Switch 값과 같으면 렌더링 (Ordinal 비교)
- 첫 번째 매칭 `Case`만 렌더링, 이후 `Case`는 스킵
- `Default` — 어떤 `Case`도 매칭되지 않았을 때 렌더링

### Set

렌더링 중 상태 값을 설정합니다 (기본값 초기화 등에 유용).

```text
Set "greeting" "Hello World"
```

## 최소 실행 예제

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var screen = new UiDslScreen("Ui/Main.ui", "Ui/theme.duxel-theme");

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "DSL Demo" },
    Screen = screen
});
```

## 핫리로드/생성기

- 런타임 핫리로드: `UiDslPipeline.CreateHotReloadRenderer(...)`
- 생성기 렌더러: `UiDslPipeline.CreateGeneratedRenderer(...)`
- NativeAOT 빌드(`DUX_NATIVEAOT`)에서는 런타임 핫리로드가 비활성화됩니다.

## 지원 노드 확인 방법

지원 위젯/노드의 최종 기준은 `Duxel.Core.Dsl.UiDslWidgetDispatcher.BeginOrInvoke(...)`의 `switch` 케이스입니다.

실행 가능한 샘플은 `samples/Duxel.ThemeDemo`, `samples/Duxel.Sample`을 참고하세요.

