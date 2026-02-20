# UI DSL 레퍼런스

## 목표
- `.ui` 확장자의 DSL 파일로 위젯 트리를 선언적으로 정의한다.
- 런타임 동작은 설계 문서의 호환성 기준을 유지한다.
- 빌드 시 DSL을 C# 코드로 변환해 실행파일에 포함한다 (소스 생성기).
- 개발 중에는 핫리로드로 `.ui` 파일 변경을 즉시 반영한다.

## DSL 문법

### 기본 구조
- **들여쓰기 기반** 트리 구조 (공백/탭 중 하나만 사용, 혼용 금지).
- 각 줄은 `NodeName` + 선택적 인자로 구성.
- 빈 줄 및 주석(`//` 또는 `#`) 허용.

```
Window "Main"
  Row
    Button Id="ok" Text="OK"
    Button Id="cancel" Text="Cancel"
```

### 컨테이너 위젯
컨테이너 위젯은 들여쓰기로 자식 블록을 표현하며, `Begin` 접두사는 **생략 가능**합니다.

| DSL 노드 | Begin/End 매핑 |
|---|---|
| `Window` | `BeginWindow`/`EndWindow` |
| `Row` | `BeginRow`/`EndRow` |
| `Group` | `BeginGroup`/`EndGroup` |
| `Child` | `BeginChild`/`EndChild` |
| `MenuBar` | `BeginMenuBar`/`EndMenuBar` |
| `MainMenuBar` | `BeginMainMenuBar`/`EndMainMenuBar` |
| `Menu` | `BeginMenu`/`EndMenu` |
| `TabBar` | `BeginTabBar`/`EndTabBar` |
| `TabItem` | `BeginTabItem`/`EndTabItem` |
| `Table` | `BeginTable`/`EndTable` |
| `Combo` | 자식 블록 있으면 `BeginCombo`/`EndCombo` |
| `ListBox` | 자식 블록 있으면 `BeginListBox`/`EndListBox` |
| `Popup` | `BeginPopup`/`EndPopup` |
| `PopupModal` | `BeginPopupModal`/`EndPopupModal` |
| `PopupContextItem` | `BeginPopupContextItem`/`EndPopup` |
| `PopupContextWindow` | `BeginPopupContextWindow`/`EndPopup` |
| `PopupContextVoid` | `BeginPopupContextVoid`/`EndPopup` |
| `Tooltip` | `BeginTooltip`/`EndTooltip` |
| `ItemTooltip` | `BeginItemTooltip`/`EndTooltip` |
| `DragDropSource` | `BeginDragDropSource`/`EndDragDropSource` |
| `DragDropTarget` | `BeginDragDropTarget`/`EndDragDropTarget` |
| `Disabled` | `BeginDisabled`/`EndDisabled` |
| `MultiSelect` | `BeginMultiSelect`/`EndMultiSelect` |
| `TreeNode` | `TreeNode`/`TreePop` |
| `TreeNodeEx` | `TreeNodeEx`/`TreePop` |

### 인자 규칙

**명명 인자 (권장)**
```
Button Id="play" Text="Play" Size="120,32"
SliderFloat Id="volume" Text="Volume" Min=0 Max=1 Format="%.2f"
```

**위치 기반 인자**
```
Button "play" "Play"          // Id, Text 순서
Checkbox "vsync" "VSync" true // Id, Text, Default
```

- 명명 인자와 위치 기반 인자를 **혼용 가능**.
- 모든 인자는 `Key=Value` 형태로 표기 가능, **순서 무관**.

### 값 타입 파싱

| 타입 | 형식 | 예시 |
|---|---|---|
| `string` | 큰따옴표 | `"Hello"` |
| `int` | 정수 | `42` |
| `float` | 소수점/접미사 | `0.5`, `3.14` |
| `double` | 소수점 | `2.718` |
| `bool` | true/false | `true` |
| `UiVector2` | `"x,y"` | `"120,32"` |
| `UiVector4` | `"x,y,z,w"` | `"0.1,0.2,0.3,1.0"` |
| `UiColor` | `#RRGGBB[AA]`, `0xAARRGGBB`, 정수 | `"#33AAFF"`, `"#33AAFFCC"` |
| `float[]`/`int[]` | `"1,2,3"` | `"0.1,0.5,0.8"` |
| 열거형/플래그 | 이름 문자열 | `"NoArrowButton"` |

### 상태 바인딩

상태를 가지는 위젯은 `Id`를 키로 `UiDslState` 또는 `IUiDslValueSource`에 값을 저장/조회합니다.

```
Checkbox Id="vsync" Text="VSync" Default=true
SliderFloat Id="volume" Text="Volume" Min=0 Max=1
InputText Id="name" Text="Name" MaxLength=64
```

`UiDslBindings`로 C# 코드에서 양방향 바인딩:
```csharp
var bindings = new UiDslBindings()
    .BindButton("play", () => Console.WriteLine("Play!"))
    .BindBool("vsync", () => vsync, v => vsync = v)
    .BindFloat("volume", () => volume, v => volume = v)
    .BindString("name", () => name, v => name = v);
```

## 지원 위젯 전체 목록

### 컨테이너

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Window` | `Title` | 윈도우 (`Begin` 생략 가능) |
| `Row` | — | 수평 레이아웃 (`BeginRow`/`EndRow`) |
| `BeginGroup` | — | 그룹 |
| `BeginChild` | `Id`, `Size`, `Border` | 자식 영역 |
| `BeginCombo` | `Id`, `Preview` | 콤보박스 (자식 블록 방식, 라벨은 `Text`로 별도 표시) |
| `BeginListBox` | `Id`, `Size` | 리스트박스 (자식 블록 방식, 라벨은 `Text`로 별도 표시) |
| `BeginDisabled` | `Disabled` | 비활성 영역 |
| `BeginMultiSelect` | `Flags` | 다중 선택 |

### 텍스트

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Text` | (텍스트) | 기본 텍스트 |
| `TextV` | `Format`, ... | 포맷 텍스트 |
| `TextColored` | `Color`, (텍스트) | 색상 텍스트 |
| `TextDisabled` | (텍스트) | 비활성 텍스트 |
| `TextWrapped` | (텍스트) | 줄바꿈 텍스트 |
| `TextUnformatted` | (텍스트) | 순수 텍스트 |
| `TextLink` | `Id`, `Text` | 하이퍼링크 텍스트 |
| `TextLinkOpenURL` | `Text`, `Url` | URL 링크 |
| `LabelText` | `Label`, `Text` | 라벨+텍스트 |
| `BulletText` | (텍스트) | 불릿 텍스트 |
| `SeparatorText` | (텍스트) | 구분선+텍스트 |
| `Value` | `Label`, `Value` | 값 표시 |

### 버튼

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Button` | `Id`, `Text`, `Size` | 기본 버튼 |
| `SmallButton` | `Id`, `Text` | 작은 버튼 |
| `InvisibleButton` | `Id`, `Size` | 투명 버튼 |
| `ArrowButton` | `Id`, `Direction` | 방향 화살표 버튼 |

### 체크/라디오/프로그레스

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Checkbox` | `Id`, `Text`, `Default` | 체크박스 |
| `CheckboxFlags` | `Id`, `Text`, `Flags`, `FlagValue` | 플래그 체크박스 |
| `RadioButton` | `Id`, `Text`, `Value` | 라디오 버튼 |
| `ProgressBar` | `Fraction`, `Size`, `Overlay` | 프로그레스 바 |

### 입력

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `InputText` | `Id`, `Text`, `MaxLength` | 텍스트 입력 |
| `InputTextWithHint` | `Id`, `Text`, `Hint`, `MaxLength` | 힌트 텍스트 입력 |
| `InputTextMultiline` | `Id`, `Text`, `MaxLength`, `Size` | 멀티라인 텍스트 |
| `InputInt` | `Id`, `Text`, `Value` | 정수 입력 |
| `InputInt2`/`3`/`4` | `Id`, `Text` | 정수 N-벡터 입력 |
| `InputFloat` | `Id`, `Text`, `Value`, `Format` | 실수 입력 |
| `InputFloat2`/`3`/`4` | `Id`, `Text`, `Format` | 실수 N-벡터 입력 |
| `InputDouble` | `Id`, `Text`, `Value`, `Format` | double 입력 |
| `InputDouble2`/`3`/`4` | `Id`, `Text`, `Format` | double N-벡터 입력 |
| `InputScalar` | `Id`, `Text`, `DataType` | 범용 스칼라 입력 |
| `InputScalarN` | `Id`, `Text`, `DataType`, `Components` | 범용 N-스칼라 입력 |

### 드래그

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `DragFloat` | `Id`, `Text`, `Value`, `Speed`, `Min`, `Max` | float 드래그 |
| `DragFloat2`/`3`/`4` | `Id`, `Text`, `Speed`, `Min`, `Max` | float N-벡터 드래그 |
| `DragInt` | `Id`, `Text`, `Value`, `Speed`, `Min`, `Max` | int 드래그 |
| `DragInt2`/`3`/`4` | `Id`, `Text`, `Speed`, `Min`, `Max` | int N-벡터 드래그 |
| `DragFloatRange2` | `Id`, `Text`, `Speed`, `Min`, `Max` | float 범위 드래그 |
| `DragIntRange2` | `Id`, `Text`, `Speed`, `Min`, `Max` | int 범위 드래그 |
| `DragScalar` | `Id`, `Text`, `DataType`, `Speed` | 범용 스칼라 드래그 |
| `DragScalarN` | `Id`, `Text`, `DataType`, `Components`, `Speed` | 범용 N-스칼라 드래그 |

### 슬라이더

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `SliderFloat` | `Id`, `Text`, `Min`, `Max`, `Format` | float 슬라이더 |
| `SliderFloat2`/`3`/`4` | `Id`, `Text`, `Min`, `Max` | float N-벡터 슬라이더 |
| `SliderInt` | `Id`, `Text`, `Min`, `Max` | int 슬라이더 |
| `SliderInt2`/`3`/`4` | `Id`, `Text`, `Min`, `Max` | int N-벡터 슬라이더 |
| `SliderAngle` | `Id`, `Text`, `Min`, `Max` | 각도 슬라이더 |
| `SliderScalar` | `Id`, `Text`, `DataType`, `Min`, `Max` | 범용 스칼라 슬라이더 |
| `SliderScalarN` | `Id`, `Text`, `DataType`, `Components` | 범용 N-스칼라 슬라이더 |
| `VSliderFloat` | `Id`, `Text`, `Size`, `Min`, `Max` | 수직 float 슬라이더 |
| `VSliderInt` | `Id`, `Text`, `Size`, `Min`, `Max` | 수직 int 슬라이더 |
| `VSliderScalar` | `Id`, `Text`, `Size`, `DataType` | 수직 범용 스칼라 슬라이더 |

### 색상

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `ColorEdit3` | `Id`, `Text` | RGB 색상 편집 |
| `ColorEdit4` | `Id`, `Text` | RGBA 색상 편집 |
| `ColorPicker3` | `Id`, `Text` | RGB 색상 선택기 |
| `ColorPicker4` | `Id`, `Text` | RGBA 색상 선택기 |
| `ColorButton` | `Id`, `Color`, `Size` | 색상 버튼 |
| `SetColorEditOptions` | `Flags` | 색상 편집 옵션 |

### 콤보/리스트박스

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Combo` | `Id`, `Items` | 콤보박스 (items를 `\|`로 구분, 라벨은 `Text`로 별도 표시) |
| `ListBox` | `Id`, `Items`, `HeightItems` | 리스트박스 (라벨은 `Text`로 별도 표시) |
| `Selectable` | `Id`, `Text`, `Selected`, `Flags`, `Size` | 선택 가능 항목 |

### 메뉴

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `MenuBar` | — | 메뉴바 (`BeginMenuBar` 생략) |
| `MainMenuBar` | — | 메인 메뉴바 |
| `Menu` | `Label` | 메뉴 항목 (`BeginMenu` 생략) |
| `MenuItem` | `Id`, `Text`, `Shortcut`, `Enabled` | 메뉴 아이템 |

### 트리/헤더

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `TreeNode` | `Label`, `DefaultOpen` | 트리 노드 |
| `TreeNodeEx` | `Label`, `Flags` | 확장 트리 노드 |
| `TreeNodeV` | `Format`, ... | 포맷 트리 노드 |
| `TreeNodeExV` | `Format`, `Flags`, ... | 포맷 확장 트리 노드 |
| `TreeNodePush` | `Label` | 트리 노드 (Push 방식) |
| `CollapsingHeader` | `Id`, `Text`, `DefaultOpen`, `Flags` | 접히는 헤더 |
| `SetNextItemOpen` | `IsOpen` | 다음 트리 항목 열기 설정 |
| `SetNextItemStorageID` | `Id` | 다음 트리 항목 저장 ID |

### 탭

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `TabBar` | `Id`, `Flags` | 탭 바 (`BeginTabBar` 생략) |
| `TabItem` | `Label`, `Flags` | 탭 항목 (`BeginTabItem` 생략) |
| `TabItemButton` | `Label`, `Flags` | 탭 버튼 |
| `SetTabItemClosed` | `Label` | 탭 닫기 |

### 테이블

| 위젯 | 주요 인자 | 설명 |
|---|---|---|
| `Table` | `Id`, `Columns`, `Flags`, `Size` | 테이블 (`BeginTable` 생략) |
| `TableSetupColumn` | `Label`, `Flags`, `InitWidth` | 컬럼 설정 |
| `TableSetupScrollFreeze` | `Cols`, `Rows` | 스크롤 고정 |
| `TableHeadersRow` | — | 헤더 행 |
| `TableAngledHeadersRow` | — | 기울어진 헤더 행 |
| `TableNextRow` | `Flags`, `MinHeight` | 다음 행 |
| `TableNextColumn` | — | 다음 컬럼 |
| `TableSetColumnIndex` | `Index` | 컬럼 인덱스 이동 |
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

