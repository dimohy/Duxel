# Duxel 에이전트 참조 문서

> Last synced: 2026-03-25
> 대상: Duxel로 앱, 샘플, 재사용 UI 컴포넌트를 작성하는 코딩 에이전트와 개발자
> 범위: Duxel 기능 맵, 아키텍처 경계, 권장 워크플로, 샘플 기준점, 바로 사용할 수 있는 생성 템플릿

## 목적

이 문서는 Duxel 코드를 작성할 때 가장 먼저 읽는 기준 문서다.

이 문서는 다음 두 대상을 동시에 위해 작성한다.

- Duxel 코드를 생성하거나 수정하기 전에 단일 고밀도 기준 문서가 필요한 코딩 에이전트
- 현재 Duxel 공개 표면을 빠르게 파악하고 싶은 개발자

이 문서는 에이전트가 소스 트리를 먼저 뒤지지 않고도 일반적인 Duxel 코드를 생성할 수 있을 정도의 정보 밀도를 목표로 한다.

일반적인 앱 코드, 샘플 코드, DSL 예제, 재사용 위젯은 우선 이 문서를 기준으로 작성한다. 렌더러 내부, 플랫폼 내부, 깊은 구현 검증이 필요한 경우에만 소스 읽기를 추가한다.

이 문서와 현재 소스가 충돌하면 현재 소스를 우선하고, 같은 변경 집합에서 이 문서도 함께 갱신한다.

## 최신화 규칙

이 파일은 살아 있는 기준 문서로 취급한다.

프로젝트 구조, 샘플 배치, 공개 API 방향, 실행 흐름, 문서 우선순위가 바뀌면 구현과 함께 이 문서도 갱신한다.

문서 상단의 `Last synced` 날짜는 의미 있는 갱신이 있을 때마다 반드시 함께 갱신한다.

## Duxel이 무엇인가

Duxel은 .NET 10 기반 즉시 모드 GUI 프레임워크다.

현재 구현 방향:

- 렌더러: Vulkan
- 주 플랫폼: Windows 네이티브 백엔드
- 런타임 스타일: NativeAOT 친화
- 주요 UI API: `UiImmediateContext`
- 앱 수명주기 진입점: `UiScreen.Render(UiImmediateContext ui)`
- 패키지 표면:
  - `Duxel.App`
  - `Duxel.Windows.App`

## 소스 없이 생성하는 기본 규칙

코딩 에이전트가 Duxel 코드 생성을 요청받았을 때는 다음 순서를 따른다.

1. 이 문서에서 시작한다.
2. 이 문서의 템플릿 중 가장 가까운 것을 고른다.
3. 이 문서의 기능 맵과 Cookbook 섹션을 사용한다.
4. 여기서 다루지 않는 내부 구현 작업일 때만 소스 코드를 읽는다.

대부분의 사용자-facing Duxel 작업은 이 문서만으로 충분해야 한다.

## 기준 정보 읽기 순서

권위 있는 기준이 필요할 때는 다음 순서로 읽는다.

1. 이 문서: `docs/duxel-agent-reference.ko.md`
2. 영문 대응 문서: `docs/duxel-agent-reference.md`
3. `README.ko.md`
4. 작업별 문서:
   - `docs/fba-reference-guide.ko.md`
   - `docs/fba-run-samples.ko.md`
   - `docs/getting-started-fba.ko.md`
   - `docs/custom-widgets.ko.md`
   - `docs/ui-dsl.ko.md`
5. 설계 기준:
   - `docs/design.ko.md`
6. 더 깊은 검증이 필요할 때만 `src/` 아래 현재 소스 코드

## 저장소 맵

중요한 최상위 영역:

- `src/`
  - `Duxel.Core` — 즉시 모드 코어, 위젯, 레이아웃, 텍스트, 상태, 드로우 데이터, DSL 런타임
  - `Duxel.App` — 앱 파사드와 공용 런타임 연결
  - `Duxel.Windows.App` — Windows 러너 엔트리 표면
  - `Duxel.Platform.Windows` — Windows 플랫폼 백엔드
  - `Duxel.Vulkan` — Vulkan 렌더러 백엔드
- `samples/`
  - `samples/Duxel.Sample` — 프로젝트 스타일 참조 앱
  - `samples/fba/*.cs` — 파일 기반 앱 샘플
- `docs/` — 사용자 및 기여자 문서
- `run-fba.ps1` — 로컬 소스 검증용 FBA 실행 경로

## 아키텍처 경계

기능을 추가하거나 수정할 때는 다음 경계를 유지한다.

- `Duxel.Core`
  - 즉시 모드 동작, 레이아웃, 위젯, 드로우리스트 생성, 상태, 텍스트 API 소유
- `Duxel.Platform.Windows`
  - 입력, 클립보드, 텍스트 백엔드 연결, 네이티브 윈도우 등 Windows 전용 동작 소유
- `Duxel.Vulkan`
  - 렌더링, GPU 리소스, 스왑체인, 제출 흐름 소유
- `Duxel.App`, `Duxel.Windows.App`
  - 앱 부트스트랩, 개발자 진입 흐름, 옵션 검증, 런타임 연결 소유

이 경계는 임의로 합치지 않는다.

## 주요 공개 진입점

다운스트림 개발자가 가장 많이 만지는 표면:

- `UiScreen`
- `UiImmediateContext`
- `DuxelAppOptions`
- `DuxelWindowOptions`
- `DuxelRendererOptions`
- `DuxelFontOptions`
- `DuxelFrameOptions`
- `DuxelDebugOptions`
- `DuxelWindowsApp.Run(...)`
- 커스텀 위젯 API:
  - `IUiCustomWidget`
  - `MarkdownEditorWidget`
  - `MarkdownViewerWidget`

## 옵션 참조와 기본값

아래 기본값은 일반적인 Duxel 코드 생성 시 그대로 기대해도 되는 값이다.

### `DuxelAppOptions`

| 속성 | 형식 | 기본값 | 용도 |
|---|---|---|---|
| `Window` | `DuxelWindowOptions` | `new()` | 창 제목, 크기, VSync |
| `Renderer` | `DuxelRendererOptions` | `new()` | 검증 레이어, 프로필, AA, 텍스트 렌더링 |
| `Font` | `DuxelFontOptions` | `new()` | 폰트 경로, 아틀라스 크기, 시작 글리프 전략 |
| `Frame` | `DuxelFrameOptions` | `new()` | 줄 높이, idle 프레임 스킵, 폰트 재빌드 주기 |
| `Debug` | `DuxelDebugOptions` | `new()` | 로그와 프레임 캡처 |
| `Theme` | `UiTheme` | `UiTheme.ImGuiDark` | 테마 프리셋 |
| `FontTextureId` | `UiTextureId` | `new(1)` | 폰트 텍스처 슬롯 |
| `WhiteTextureId` | `UiTextureId` | `new(2)` | 화이트 텍스처 슬롯 |
| `Screen` | `UiScreen` | (필수) | 즉시 모드 앱 진입점 |
| `Clipboard` | `IUiClipboard?` | `null` | 직접 클립보드 주입 |
| `ImageDecoder` | `IUiImageDecoder?` | `null` | 커스텀 이미지 디코드 경로 |
| `KeyRepeatSettingsProvider` | `IKeyRepeatSettingsProvider?` | `null` | 커스텀 키 반복 타이밍 |
| `ClipboardFactory` | `Func<IPlatformBackend, IUiClipboard?>?` | `null` | 플랫폼별 클립보드 팩토리 |

### `DuxelWindowOptions`

| 속성 | 기본값 |
|---|---|
| `Width` | `1280` |
| `Height` | `720` |
| `Title` | `"Duxel"` |
| `VSync` | `true` |

### `DuxelRendererOptions`

| 속성 | 기본값 |
|---|---|
| `MinImageCount` | `3` |
| `EnableValidationLayers` | `Debugger.IsAttached` |
| `Profile` | `DuxelPerformanceProfile.Display` |
| `MsaaSamples` | `0` |
| `EnableGlobalStaticGeometryCache` | `true` |
| `FontLinearSampling` | `false` |

### `DuxelFontOptions`

| 속성 | 기본값 |
|---|---|
| `PrimaryFontPath` | Windows `segoeui.ttf` |
| `SecondaryFontPath` | Windows `malgun.ttf` |
| `SecondaryScale` | `1f` |
| `FastStartup` | `true` |
| `UseBuiltInAsciiAtStartup` | `true` |
| `StartupBuiltInScale` | `2` |
| `StartupBuiltInColumns` | `16` |
| `StartupFontSize` | `16` |
| `StartupAtlasWidth` | `512` |
| `StartupAtlasHeight` | `512` |
| `StartupPadding` | `1` |
| `StartupOversample` | `1` |
| `StartupRanges` | ASCII 범위 `0x20..0x7E` |
| `StartupGlyphs` | 비어 있음 |
| `FontSize` | `16` |
| `AtlasWidth` | `1024` |
| `AtlasHeight` | `1024` |
| `Padding` | `2` |
| `Oversample` | `2` |
| `InitialRanges` | ASCII 범위 `0x20..0x7E` |

### `DuxelFrameOptions`

| 속성 | 기본값 |
|---|---|
| `LineHeightScale` | `1.2f` |
| `PixelSnap` | `true` |
| `UseBaseline` | `true` |
| `FontRebuildMinIntervalSeconds` | `0.25` |
| `FontRebuildBatchSize` | `16` |
| `EnableIdleFrameSkip` | `true` |
| `IdleSleepMilliseconds` | `2` |
| `IdleWakeCheckMilliseconds` | `1000` |
| `IdleEventWaitMilliseconds` | `0` |
| `WindowTargetFps` | 빈 사전 |
| `IsAnimationActiveProvider` | `null` |

### `DuxelDebugOptions`

| 속성 | 기본값 |
|---|---|
| `Log` | `null` |
| `LogEveryNFrames` | `60` |
| `LogStartupTimings` | `false` |
| `CaptureOutputDirectory` | `null` |
| `CaptureFrameIndices` | 비어 있음 |

## 바로 복붙 가능한 시작 템플릿

아래 템플릿은 그대로 코드 생성 출발점으로 사용할 수 있다.

### 최소 FBA 즉시 모드 앱

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions
  {
    Title = "Hello Duxel",
    Width = 1280,
    Height = 720,
    VSync = true,
  },
  Screen = new DemoScreen(),
});

public sealed class DemoScreen : UiScreen
{
  private int _count;
  private bool _enabled = true;
  private float _volume = 0.5f;
  private string _name = "Duxel";

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginWindow("Demo");
    ui.Text("Hello from Duxel");

    if (ui.Button("Increment"))
    {
      _count++;
    }

    ui.SameLine();
    ui.Text($"Count: {_count}");

    ui.Checkbox("Enabled", ref _enabled);
    ui.InputText("Name", ref _name, 128);
    ui.SliderFloat("Volume", ref _volume, 0f, 1f);

    ui.EndWindow();
  }
}
```

### 옵션을 조금 조정한 즉시 모드 앱

```csharp
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Theme = UiTheme.ImGuiDark,
  Window = new DuxelWindowOptions
  {
    Title = "Configured Duxel App",
    Width = 1440,
    Height = 900,
    VSync = true,
  },
  Renderer = new DuxelRendererOptions
  {
    Profile = DuxelPerformanceProfile.Display,
    EnableValidationLayers = true,
    MsaaSamples = 0,
    EnableGlobalStaticGeometryCache = true,
  },
  Font = new DuxelFontOptions
  {
    FontSize = 16,
    FastStartup = true,
  },
  Frame = new DuxelFrameOptions
  {
    LineHeightScale = 1.2f,
    EnableIdleFrameSkip = true,
  },
  Screen = new DemoScreen(),
});
```

### 최소 커스텀 위젯

```csharp
using Duxel.Core;

public sealed class CounterWidget : IUiCustomWidget
{
  private int _count;

  public void Render(UiImmediateContext ui)
  {
    ui.Text("Counter Widget");
    if (ui.Button("Add"))
    {
      _count++;
    }

    ui.SameLine();
    ui.Text($"Value: {_count}");
  }
}
```

### Markdown 위젯 사용 예제

```csharp
using Duxel.Core;

public sealed class MarkdownScreen : UiScreen
{
  private readonly MarkdownEditorWidget _editor = new("editor")
  {
    Label = "Markdown",
    Height = 220f,
    ShowStats = true,
    Text = "# Hello\n\n- item 1\n- item 2",
  };

  private readonly MarkdownViewerWidget _viewer = new("viewer")
  {
    Height = 260f,
    ShowBorder = true,
    ShowStats = true,
  };

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginWindow("Markdown Studio");
    _editor.Render(ui);
    _viewer.Markdown = _editor.Text;
    _viewer.Render(ui);
    ui.EndWindow();
  }
}
```

### 최소 DSL 앱 (프로젝트 기반)

```csharp
// Program.cs
using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var screen = new UiDslScreen("Ui/Main.ui", "Ui/theme.duxel-theme");

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions { Title = "DSL Demo" },
  Screen = screen,
});
```

## 핵심 앱 패턴

### 즉시 모드 화면 패턴

일반적인 앱 화면이나 샘플 화면은 `UiScreen`으로 작성한다.

- `Render(UiImmediateContext ui)`를 override
- 매 프레임 UI를 다시 구성
- 상태는 화면 인스턴스나 외부 모델에 유지

기본 권장값: 사용자가 단순히 Duxel 샘플을 요청하면, DSL을 명시적으로 요구하지 않는 한 즉시 모드 `UiScreen` 예제를 생성한다.

### DSL 패턴

선언형 UI 작성, 상태 바인딩, 생성기 기반 워크플로는 `.ui` DSL을 사용한다.

- 파서: `UiDslParser`
- 런타임 실행: `UiDslDocument.Emit(...)`
- 바인딩 경로: `IUiDslValueSource`, `IUiDslEventSink`
- 문서: `docs/ui-dsl.ko.md`

### 커스텀 위젯 패턴

내장 즉시 모드 표면에 올리기 애매한 동작은 인스턴스 기반 재사용 위젯으로 만든다.

- `IUiCustomWidget` 구현
- 위젯 전용 상태는 객체 내부에 보관 가능
- `Render(UiImmediateContext ui)`에서 렌더링

기본 제공 재사용 위젯:

- `MarkdownEditorWidget`
- `MarkdownViewerWidget`

## `UiImmediateContext` 기능 맵

이 섹션은 에이전트와 개발자가 가장 자주 쓰는 기능 인벤토리다.

### 창과 레이아웃

- `Begin(...)` / `End()`
- `BeginChild(...)` / `EndChild()`
- `SameLine()`
- `NewLine()`
- `Spacing()`
- `Separator()`
- `Dummy(...)`
- `Columns(...)`, `NextColumn()`
- 테이블 API: `TableSetupColumn(...)`, `TableNextRow()`, `TableNextCell()` 등
- 들여쓰기/너비 제어:
  - `Indent()` / `Unindent()`
  - `SetNextItemWidth(...)`
  - `PushItemWidth(...)` / `PopItemWidth()`
- 커서/영역 제어:
  - `GetCursorPos()` / `SetCursorPos(...)`
  - `GetContentRegionAvail()`

### 텍스트와 타이포그래피

- `Text(...)`
- `TextColored(...)`
- `TextDisabled(...)`
- `TextWrapped(...)`
- `TextUnformatted(...)`
- `LabelText(...)`
- 폰트 제어:
  - `PushFont(...)` / `PopFont()`
  - `PushFontSize(...)` / `PopFontSize()`

### 버튼과 기본 선택 위젯

- `Button(...)`
- `SmallButton(...)`
- `InvisibleButton(...)`
- `ArrowButton(...)`
- `Checkbox(...)`
- `CheckboxFlags(...)`
- `RadioButton(...)`

실전 시그니처:

- `bool Button(string label)`
- `bool Button(string label, UiVector2 size)`
- `bool SmallButton(string label)`
- `bool InvisibleButton(string id, UiVector2 size)`
- `bool ArrowButton(string id, UiDir dir)`
- `bool Checkbox(string label, ref bool value)`
- `bool CheckboxFlags(string label, ref int flags, int flagsValue)`
- `bool RadioButton(string label, bool active)`
- `bool RadioButton(string label, ref int value, int buttonValue)`

### 텍스트 입력과 숫자 입력

- `InputText(...)`
- `InputTextWithHint(...)`
- `InputTextMultiline(...)`
- `InputInt(...)`, `InputInt2(...)`, `InputInt3(...)`, `InputInt4(...)`
- `InputFloat(...)`, `InputFloat2(...)`, `InputFloat3(...)`, `InputFloat4(...)`
- `InputDouble(...)`, `InputDouble2(...)`, `InputDouble3(...)`, `InputDouble4(...)`

실전 시그니처:

- `bool InputText(string label, ref string value, int maxLength)`
- `bool InputTextWithHint(string label, string hint, ref string value, int maxLength)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, float height)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, int visibleLines)`
- `bool InputInt(string label, ref int value)`
- `bool InputFloat(string label, ref float value, string format = "0.###")`
- `bool InputDouble(string label, ref double value, string format = "0.###")`

### 슬라이더와 드래그 입력

- `SliderFloat(...)`, `SliderFloat2(...)`, `SliderFloat3(...)`, `SliderFloat4(...)`
- `SliderInt(...)`, `SliderInt2(...)`, `SliderInt3(...)`, `SliderInt4(...)`
- `SliderAngle(...)`
- `VSliderFloat(...)`
- `DragFloat(...)`, `DragFloat2(...)`, `DragFloat3(...)`, `DragFloat4(...)`
- `DragInt(...)`, `DragInt2(...)`, `DragInt3(...)`, `DragInt4(...)`
- `DragFloatRange2(...)`
- `DragIntRange2(...)`

실전 시그니처:

- `bool SliderFloat(string label, ref float value, float min, float max)`
- `bool SliderInt(string label, ref int value, int min, int max)`
- `bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")`
- `bool DragInt(string label, ref int value, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)`

### 선택, 콤보, 리스트

- `Combo(...)`
- `BeginCombo(...)` / `EndCombo()`
- `ListBox(...)`
- `BeginListBox(...)` / `EndListBox()`
- `Selectable(...)`

실전 시그니처:

- `bool Combo(ref int currentIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8, string? id = null)`
- `bool Combo(ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int popupMaxHeightInItems = 8, string? id = null)`
- `bool BeginCombo(string previewValue, int popupMaxHeightInItems = 8, string? id = null)`
- `bool ListBox(ref int currentIndex, IReadOnlyList<string> items, int visibleItems = 6, string? id = null)`
- `bool Selectable(string label, ref bool selected)`
- `bool Selectable(string label, bool selected)`

### 트리, 탭, 메뉴, 팝업, 툴팁

- `TreeNode(...)`, `TreeNodeId(...)`, `TreePop()`
- `SetNextItemOpen(...)`
- `BeginTabBar(...)` / `EndTabBar()`
- `BeginTabItem(...)` / `EndTabItem()`
- `TabItemButton(...)`
- `BeginMainMenuBar()` / `EndMainMenuBar()`
- `BeginMenuBar()` / `EndMenuBar()`
- `BeginMenu(...)` / `EndMenu()`
- `MenuItem(...)`
- `OpenPopup(...)`
- `BeginPopup(...)` / `EndPopup()`
- `BeginPopupContextItem(...)` 등 컨텍스트 팝업 계열
- `BeginTooltip()` / `EndTooltip()`
- `SetTooltip(...)`

실전 시그니처:

- `bool BeginMenuBar()`
- `bool BeginMenu(string label)`
- `bool MenuItem(string label, bool selected = false, bool enabled = true)`
- `bool BeginPopup(string id)`
- `bool BeginPopupContextItem(string id)`
- `bool BeginTooltip()`
- `bool BeginTabBar(string id)`
- `bool BeginTabItem(string label)`
- `bool TreeNode(string label, bool defaultOpen = false)`
- `bool TreeNodeEx(string label, UiTreeNodeFlags flags = UiTreeNodeFlags.None)`

### 색상, 이미지, 시각 효과

- `ColorButton(...)`
- `ColorEdit3(...)`, `ColorEdit4(...)`
- `ColorPicker3(...)`, `ColorPicker4(...)`
- `Image(...)`
- `ImageWithBg(...)`
- `ImageButton(...)`

실전 시그니처:

- `bool ColorEdit3(string label, ref float r, ref float g, ref float b)`
- `bool ColorEdit4(string label, ref float r, ref float g, ref float b, ref float a)`
- `bool ImageButton(string id, UiTextureId textureId, UiVector2 size, UiColor? tint = null)`

### 진행률, 그래프, 피드백

- `ProgressBar(...)`
- `PlotLines(...)`
- `PlotHistogram(...)`

실전 시그니처:

- `void ProgressBar(float fraction, UiVector2 size, string? overlay = null)`

### 스타일과 스코프 오버라이드

- `PushStyleColor(...)` / `PopStyleColor()`
- `PushStyleVar(...)` / `PopStyleVar()`
- `PushId(...)` / `PopId()`
- `BeginDisabled()` / `EndDisabled()`

동적 리스트를 만들 때는 ID 충돌을 피하기 위해 명시적으로 ID를 관리한다.

### 상태와 상호작용 질의

- `IsItemHovered()`
- `IsItemActive()`
- `IsItemClicked()`
- `GetItemRectMin()`
- `GetItemRectMax()`
- `GetItemRectSize()`

### 커스텀 드로잉과 레이어

내장 위젯만으로 부족할 때는 드로우리스트와 캔버스 패턴을 사용한다.

- 사각형, 원, 선, 베지어, 텍스트, 이미지, 클립 제어, 텍스처 전환, 콜백 등 드로우리스트 primitive 지원
- 레이어/캔버스 지향 흐름은 레이어 벤치 샘플과 혼합 렌더링 샘플 참고

## 자주 만드는 UI용 Cookbook

### 기본 폼 창

설정 페이지, 다이얼로그, 툴 패널은 이 패턴을 기본으로 쓴다.

```csharp
public sealed class SettingsScreen : UiScreen
{
  private string _userName = "guest";
  private bool _autoSave = true;
  private int _quality = 2;
  private float _scale = 1.0f;

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginWindow("Settings");

    ui.InputText("User Name", ref _userName, 128);
    ui.Checkbox("Auto Save", ref _autoSave);
    ui.SliderFloat("Scale", ref _scale, 0.5f, 2.0f);

    ui.Text("Quality");
    ui.RadioButton("Low", ref _quality, 0);
    ui.RadioButton("Medium", ref _quality, 1);
    ui.RadioButton("High", ref _quality, 2);

    if (ui.Button("Apply"))
    {
      // commit state
    }

    ui.EndWindow();
  }
}
```

### 메뉴바 패턴

```csharp
if (ui.BeginMainMenuBar())
{
  if (ui.BeginMenu("File"))
  {
    _ = ui.MenuItem("Open");
    _ = ui.MenuItem("Save");
    _ = ui.MenuItem("Exit");
    ui.EndMenu();
  }

  if (ui.BeginMenu("View"))
  {
    _ = ui.MenuItem("Show Grid", selected: true);
    ui.EndMenu();
  }

  ui.EndMainMenuBar();
}
```

### 팝업 패턴

```csharp
if (ui.Button("Open Popup"))
{
  ui.OpenPopup("demo-popup");
}

if (ui.BeginPopup("demo-popup"))
{
  ui.Text("Popup content");
  ui.EndPopup();
}
```

### 탭 패턴

```csharp
if (ui.BeginTabBar("main-tabs"))
{
  if (ui.BeginTabItem("General"))
  {
    ui.Text("General content");
    ui.EndTabItem();
  }

  if (ui.BeginTabItem("Advanced"))
  {
    ui.Text("Advanced content");
    ui.EndTabItem();
  }

  ui.EndTabBar();
}
```

### 트리 패턴

```csharp
if (ui.TreeNode("Assets", defaultOpen: true))
{
  ui.BulletText("Textures");
  ui.BulletText("Shaders");
  ui.BulletText("Fonts");
  ui.TreePop();
}
```

### 리스트와 콤보 패턴

```csharp
private readonly string[] _modes = ["Display", "Render", "Debug"];
private int _modeIndex;

public override void Render(UiImmediateContext ui)
{
  ui.BeginWindow("Selection");
  ui.Combo(ref _modeIndex, _modes, 8, "mode");
  ui.Text($"Current: {_modes[_modeIndex]}");
  ui.EndWindow();
}
```

### 두 열 설정 패턴

레거시 두 열 설정 레이아웃을 맞춰야 할 때만 `Columns(...)`를 사용한다. 새로운 구조화 데이터 뷰는 테이블 API를 우선한다.

### 커스텀 드로우 캔버스 패턴

```csharp
var canvas = ui.BeginWindowCanvas(new UiColor(0xFF101820));
var draw = ui.GetWindowDrawList();

draw.AddRect(canvas, new UiColor(0xFFFFFFFF));
draw.AddLine(
  new UiVector2(canvas.X, canvas.Y),
  new UiVector2(canvas.X + canvas.Width, canvas.Y + canvas.Height),
  new UiColor(0xFF39AFFF),
  2f);

ui.EndWindowCanvas();
```

## 생성 규칙 요약

- 빠른 데모는 `UiScreen` 하나와 `BeginWindow(...)` / `EndWindow()` 쌍 하나로 시작
- 상태는 화면 또는 위젯 객체의 private 필드에 유지
- 사용자가 렌더러 작업을 명시하지 않는 한 즉시 모드 예제를 우선
- 기존 샘플의 이름과 패턴을 우선 재사용
- 새로운 구조화 데이터 뷰는 테이블 API 우선
- 이 저장소에서 로컬 소스 검증은 `run-fba.ps1` 우선
- 마크다운 편집/뷰어 요구는 내장 Markdown 위젯 우선

## 샘플 우선 탐색표

| 필요 항목 | 시작점 |
|---|---|
| 전체 기능 쇼케이스 | `samples/fba/all_features.cs` |
| 선언형 DSL + 테마 | `samples/Duxel.ThemeDemo` |
| 레이아웃과 스타일 제어 | `samples/fba/advanced_layout.cs` |
| 레거시 컨럼 | `samples/fba/columns_demo.cs` |
| 이미지, 팝업, 툴팁 패턴 | `samples/fba/image_and_popups.cs` |
| 이미지 효과와 애니메이션 이미지 | `samples/fba/image_widget_effects_fba.cs` |
| 키보드와 마우스 질의 API | `samples/fba/input_queries.cs` |
| 아이템 생명주기와 상태 질의 | `samples/fba/item_status.cs` |
| 텍스트 정렬과 렌더 검증 | `samples/fba/text_render_validation_fba.cs` |
| 폰트 스타일과 크기 검증 | `samples/fba/font_style_validation_fba.cs` |
| 레이어 캐시와 dirty 전략 | `samples/fba/idle_layer_validation.cs`, `samples/fba/layer_dirty_strategy_bench.cs`, `samples/fba/global_dirty_strategy_bench.cs` |
| 레이어와 위젯 혼합 | `samples/fba/layer_widget_mix_bench_fba.cs` |
| 렌더링 스트레스 | `samples/fba/ui_mixed_stress.cs` |
| 물리/성능 벤치마크 | `samples/fba/Duxel_perf_test_fba.cs` |
| 앱 규모 참조 | `samples/Duxel.Sample` |

## 권장 실행 워크플로

### 최종 사용자 패키지 워크플로

Duxel을 라이브러리처럼 소비할 때는 패키지 모드를 사용한다.

- `dotnet run hello.cs`
- `dotnet run samples/fba/all_features.cs`

FBA 샘플은 다음과 같은 패키지 지시문을 사용한다.

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*
```

### 기여자 로컬 소스 워크플로

로컬 소스 변경을 검증할 때는 `run-fba.ps1`를 사용한다.

권장 예시:

- `./run-fba.ps1 samples/fba/all_features.cs -NoCache`
- `./run-fba.ps1 samples/fba/all_features.cs -Managed`

이유:

- 패키지 지시문을 로컬 프로젝트 참조로 바꿔 준다.
- 필요 시 Windows 엔트리 호출을 정규화한다.
- 기본적으로 NativeAOT 경로를 지원한다.
- 단순 패키지 모드와 달리 현재 워크스페이스 소스 변경을 반영한다.

로컬 소스 수정 검증에 `dotnet run samples/fba/<file>.cs`를 기준으로 삼지 않는다.

## 빌드와 검증

기본 저장소 빌드:

- `dotnet build Duxel.slnx -c Release`

의미 있는 변경 뒤 최소 검증 기준:

- 솔루션 빌드, 또는
- 관련 샘플 1개 이상 실행

가능한 한 가장 작은 검증으로, 그러나 충분히 증명되는 검증을 선택한다.

## 환경 변수와 런타임 토글

샘플과 진단에서 자주 쓰는 런타임 토글:

- `DUXEL_APP_PROFILE`
- `DUXEL_SAMPLE_AUTO_EXIT_SECONDS`
- `DUXEL_IMAGE_PATH`
- `DUXEL_LAYER_BENCH_BACKEND`
- `DUXEL_LAYER_BENCH_OPACITY`
- `DUXEL_LAYER_BENCH_PARTICLES`
- `DUXEL_LAYER_BENCH_LAYOUTS`
- `DUXEL_LAYER_BENCH_PHASE_SECONDS`
- `DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER`
- `DUXEL_LAYER_BENCH_OUT`

특정 토글에 의존하는 작업은 문서화하거나 사용하기 전에 관련 샘플에서 이름과 의미를 다시 확인한다.

## 제약과 비목표

아래 저장소 규칙은 작업이 명시적으로 바꾸지 않는 한 유지되는 전제로 본다.

- .NET 10 타깃
- Windows 우선 플랫폼 현실
- Vulkan 렌더러 전제
- NativeAOT 친화 방향
- fallback 경로를 가볍게 늘리지 않음
- 방어적 코드 남발로 근본 원인 수정을 대체하지 않음

무언가가 미지원이면 숨은 fallback 대신 명시적 실패나 명시적 범위 제한을 선호한다.

## 에이전트 작업 절차

권장 순서:

1. 작업이 코어 UI, 앱 부트스트랩, Windows 플랫폼, Vulkan 렌더링, DSL, 문서 중 무엇인지 식별한다.
2. 이 문서의 가장 가까운 섹션을 먼저 읽고, 예제가 더 필요할 때만 가장 가까운 샘플을 읽는다.
3. 아키텍처 경계를 지킨다.
4. 실제 문제를 해결하는 가장 작은 변경을 한다.
5. 빌드나 관련 샘플 실행으로 검증한다.
6. 공개 동작, 워크플로, 확장 지점이 바뀌면 문서를 갱신한다.
7. 안정적인 새 규칙이 생기면 이 문서와 영문 대응 문서를 함께 갱신한다.

## 함께 읽을 문서

- `README.ko.md` — 패키지 개요와 빠른 시작
- `docs/duxel-agent-reference.md` — 영문 대응 기준 문서
- `docs/getting-started-fba.ko.md` — FBA 첫 시작 가이드
- `docs/fba-reference-guide.ko.md` — 패키지/프로젝트 전환과 `run-fba.ps1`
- `docs/fba-run-samples.ko.md` — 샘플 카탈로그와 원클릭 실행
- `docs/custom-widgets.ko.md` — 재사용 위젯 확장 경로
- `docs/ui-dsl.ko.md` — 선언형 UI 문서
- `docs/version-history.ko.md` — 그룹화된 버전 이력
- `docs/design.ko.md` — 현재 아키텍처와 설계 기준

## 유지보수 메모

미래의 에이전트나 개발자가 Duxel을 올바르게 쓰기 위해 꼭 알아야 하는 안정적인 워크플로 규칙을 새로 발견하면 여기에 반영하고 `Last synced` 날짜를 함께 갱신한다.