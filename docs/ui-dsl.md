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
| `TableSetBgColor` | `Target`, `Color`, `Column` | 배경색 설정 |
| `TableSetColumnEnabled` | `Index`, `Enabled` | 컬럼 활성/비활성 |
| `TableRowBg` | `Color` | 행 배경 |
| `TableRowBgAlternating` | `EvenColor`, `OddColor` | 교대 행 배경 |
| `TableRowSeparator` | `Color` | 행 구분선 |
| `TableCellBg` | `Color` | 셀 배경 |
| `TableGetSortSpecs` | `ColumnKey`, `AscendingKey`, `ChangedKey` | 정렬 스펙 조회 |

### 팝업/툴팁

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Popup` | `Id` | 팝업 (`BeginPopup` 생략) |
| `PopupModal` | `Id`, `OpenKey` | 모달 팝업 |
| `PopupContextItem` | `Id` | 아이템 컨텍스트 팝업 |
| `PopupContextWindow` | `Id` | 윈도우 컨텍스트 팝업 |
| `PopupContextVoid` | `Id` | 빈 영역 컨텍스트 팝업 |
| `OpenPopup` | `Id` | 팝업 열기 |
| `OpenPopupOnItemClick` | `Id` | 아이템 클릭 시 팝업 열기 |
| `CloseCurrentPopup` | — | 현재 팝업 닫기 |
| `IsPopupOpen` | `Id`, `OutKey` | 팝업 열림 여부 조회 |
| `BeginTooltip` | — | 툴팁 시작 |
| `BeginItemTooltip` | — | 아이템 툴팁 시작 |
| `SetTooltip` | `Text` | 툴팁 텍스트 |
| `SetItemTooltip` | `Text` | 아이템 툴팁 |

### 이미지

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Image` | `TextureId`, `Size` | 이미지 |
| `ImageWithBg` | `TextureId`, `Size`, `BgColor` | 배경 이미지 |
| `ImageButton` | `Id`, `TextureId`, `Size` | 이미지 버튼 |

### 드래그앤드롭

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `BeginDragDropSource` | `Flags` | 드래그 소스 시작 |
| `SetDragDropPayload` | `Type`, `Data` | 페이로드 설정 |
| `BeginDragDropTarget` | — | 드롭 대상 시작 |
| `AcceptDragDropPayload` | `Type`, `Flags` | 페이로드 수락 |

### 레이아웃/유틸

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Separator` | — | 구분선 |
| `SameLine` | — | 같은 줄 |
| `NewLine` | — | 새 줄 |
| `Spacing` | — | 여백 |
| `Dummy` | `Size` | 빈 영역 |
| `Indent` | `Width` | 들여쓰기 |
| `Unindent` | `Width` | 내어쓰기 |
| `AlignTextToFramePadding` | — | 텍스트 프레임 정렬 |
| `PushID` / `PopID` | `Id` | ID 스택 |
| `PushItemWidth` / `PopItemWidth` | `Width` | 아이템 너비 |
| `SetNextItemWidth` | `Width` | 다음 아이템 너비 |
| `Columns` | `Count`, `Id`, `Border` | 구 컬럼 API |
| `NextColumn` | — | 다음 컬럼 |
| `Bullet` | — | 불릿 |
| `PlotLines` | `Id`, `Text`, `Values` | 라인 플롯 |
| `PlotHistogram` | `Id`, `Text`, `Values` | 히스토그램 |

## 변환 파이프라인

```
.ui 파일 → UiDslParser → UiDslDocument (AST)
                               ↓
                    UiDslCompiler → .g.cs (소스 생성기)
                               ↓
                    빌드 시 포함 → 런타임 Render() 호출
```

### 핵심 타입

| 타입 | 역할 |
|---|---|
| `UiDslParser` | `.ui` 텍스트를 `UiDslDocument` AST로 파싱 |
| `UiDslDocument` / `UiDslNode` | DSL AST (트리 구조) |
| `IUiDslEmitter` | `BeginNode`/`EndNode` 인터페이스 |
| `UiDslImmediateEmitter` | `IUiDslEmitter` → `UiImmediateContext` 호출로 변환 |
| `UiDslWidgetDispatcher` | 노드 이름 → 실제 위젯 API 디스패치 |
| `UiDslCompiler` | AST → C# 소스 코드 생성 |
| `UiDslSourceGenerator` | Roslyn 소스 생성기 (`.ui` → `.g.cs`) |
| `UiDslPipeline` | 핫리로드/생성 렌더러 팩토리 |
| `UiDslAuto` | 자동 리졸버 (NativeAOT: 생성기, 개발: 핫리로드) |
| `UiDslState` | 위젯 상태 저장소 (`IUiDslValueSource` 기본 구현) |
| `UiDslBindings` | C# 코드 ↔ DSL 양방향 바인딩 |

## 사용 패턴

### 1. 인라인 DSL (FBA/프로토타이핑)

`Duxel.Windows.App` 패키지를 사용하는 경우 엔트리포인트는 `DuxelWindowsApp.Run`을 권장합니다.

```csharp
var dslText = """
Window "Demo"
  Text "Hello"
  Button Id="ok" Text="OK"
""";
var doc = UiDslParser.Parse(dslText);
var state = new UiDslState();

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "Demo" },
    Dsl = new DuxelDslOptions
    {
        State = state,
        Render = emitter => doc.Emit(emitter)
    }
});
```

### 2. .ui 파일 + 소스 생성기 (프로덕션)

**Ui/Main.ui:**
```
Window "My App"
  Button Id="play" Text="Play"
  Checkbox Id="vsync" Text="VSync" Default=true
```

**Program.cs:**
```csharp
var bindings = new UiDslBindings()
    .BindButton("play", () => Console.WriteLine("Play!"))
    .BindBool("vsync", () => vsync, v => vsync = v);

var render = UiDslAuto.Render("Main.ui");

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Dsl = new DuxelDslOptions
    {
        Render = render,
        Bindings = bindings
    }
});
```

**.csproj에 .ui 파일 포함:**
```xml
<ItemGroup>
  <AdditionalFiles Include="Ui\*.ui" />
</ItemGroup>
```

### 3. NativeAOT 배포
- 소스 생성기가 `UiDslGeneratedRegistry`를 생성하여 AOT 호환.
- `UiDslAuto`가 빌드 타입에 따라 자동으로 AOT 렌더러 또는 핫리로드 렌더러 선택.
- 런타임 파서는 NativeAOT 빌드에서 사용하지 않음.

## 핫리로드 (개발 모드)

개발 모드(`DUX_NATIVEAOT` 미정의)에서는 `UiDslPipeline.CreateHotReloadRenderer`가 파일 감시를 수행하여, `.ui` 파일 저장 시 자동으로 AST를 재파싱하고 렌더러를 교체합니다.

## 예시 — 종합 데모

```
Window "DSL Showcase"
  MenuBar
    Menu "File"
      MenuItem Id="new" Text="New"
      MenuItem Id="exit" Text="Exit"
  SeparatorText "Inputs"
  InputText Id="name" Text="Name" MaxLength=64
  InputInt Id="age" Text="Age" Value=0
  InputFloat Id="speed" Text="Speed" Format="0.00"
  Checkbox Id="vsync" Text="VSync" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
  Text "Preset"
  Combo Id="preset" Items="Low|Medium|High"
  SeparatorText "Tabs"
  TabBar "tabs"
    TabItem "Tab A"
      Text "Tab A content"
    TabItem "Tab B"
      Text "Tab B content"
  SeparatorText "Table"
  Table "table" 3
    TableSetupColumn "Name"
    TableSetupColumn "Value"
    TableSetupColumn "Notes"
    TableHeadersRow
    TableNextRow
    TableNextColumn
    Text "Row1"
    TableNextColumn
    Text "42"
    TableNextColumn
    Text "Hello"
  SeparatorText "Tree"
  TreeNode "Node 1"
    Text "Child 1"
    TreeNode "Node 1.1"
      Text "Leaf"
  TreeNode "Node 2"
    Text "Child 2"
```

## 구현 현황

- DSL 파서/AST/컴파일러/이미터/디스패처 구현 완료.
- 소스 생성기(`UiDslSourceGenerator`) 구현 완료.
- `UiDslAuto` 자동 리졸버 (AOT/핫리로드) 구현 완료.
- `UiDslBindings` 양방향 바인딩 구현 완료.
- 지원 위젯: 100+ 노드 (호환성 목표는 설계 문서 기준).
- 핫리로드 지원 완료.
- `.ui` 샘플: `samples/Duxel.Sample/Ui/Main.ui`
- FBA 샘플: `samples/fba/dsl_showcase.cs`, `samples/fba/dsl_interaction.cs`

