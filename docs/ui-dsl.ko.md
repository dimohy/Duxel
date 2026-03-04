# UI DSL 레퍼런스

이 문서는 현재 코드(`UiDslParser`, `UiDslWidgetDispatcher`, `UiDslPipeline`) 기준으로 정리한 DSL 문법입니다.

## 핵심 요약

- 파일 형식: `.ui` (들여쓰기 기반 트리)
- 파서: `Duxel.Core.Dsl.UiDslParser`
- 런타임 실행: `UiDslDocument.Emit(...)` → `UiDslImmediateEmitter`
- 상태 저장: `UiDslState` 또는 `IUiDslValueSource`
- 이벤트 연결: `IUiDslEventSink` / `UiDslBindings`

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

`Duxel.App` 사용 시 `UiDslBindings`로 쉽게 연결할 수 있습니다.

```csharp
var bindings = new UiDslBindings()
    .BindButton("apply", () => Console.WriteLine("apply"))
    .BindBool("vsync", () => vsync, v => vsync = v)
    .BindFloat("volume", () => volume, v => volume = v)
    .BindString("name", () => name, v => name = v);
```

## 최소 실행 예제

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var text = """
Window "DSL"
  Text "Hello"
  Checkbox Id="dark" Text="Dark" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
""";

var doc = UiDslParser.Parse(text);

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "DSL Demo" },
    Dsl = new DuxelDslOptions
    {
        State = new UiDslState(),
        Render = emitter => doc.Emit(emitter)
    }
});
```

## 핫리로드/생성기

- 런타임 핫리로드: `UiDslPipeline.CreateHotReloadRenderer(...)`
- 생성기 렌더러: `UiDslPipeline.CreateGeneratedRenderer(...)`
- NativeAOT 빌드(`DUX_NATIVEAOT`)에서는 런타임 핫리로드가 비활성화됩니다.

## 지원 노드 확인 방법

지원 위젯/노드의 최종 기준은 `Duxel.Core.Dsl.UiDslWidgetDispatcher.BeginOrInvoke(...)`의 `switch` 케이스입니다.

실행 가능한 샘플은 `samples/fba/dsl_showcase.cs`, `samples/fba/dsl_interaction.cs`, `samples/Duxel.Sample`를 참고하세요.

