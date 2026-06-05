# Duxel 에이전트 참조 문서

> Last synced: 2026-06-06
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
| `TextRendering` | `DuxelTextRenderingMode.DirectText` |

`DuxelTextRenderingMode.DirectText`가 기본 텍스트 경로다. Atlas 출력은 기본 시각 기준으로 쓰지 않는다. 명시적 `Atlas`는 atlas renderer A/B 프로파일링에만 사용한다. 명시적 `Auto`는 atlas 우선 렌더링을 유지하되 atlas가 따라오기 전 누락 글자만 DirectText 시각 fallback으로 즉시 보여야 하는 sample에서만 사용한다.

DirectWrite atlas glyph 경로를 수정할 때는 oversample 래스터라이즈 metric을 atlas에 넘기기 전에 논리 픽셀로 되돌려야 한다. Bitmap data는 `fontSize * oversample` 크기로 래스터라이즈할 수 있지만, glyph advance, text-run advance, offset, baseline은 downsample 뒤 logical em size 기준으로 정규화해야 한다. Cached placement metric이 바뀌면 font-atlas disk cache version도 올린다.

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
- retained static 드로우리스트는 `StaticGeometryKey`를 캐시 identity로 사용하므로, retained geometry 내용이 바뀌면 key도 바꾼다.
- `UiDrawList.StaticGeometryStamp`는 렌더러 캐시 검증에 쓰이는 저비용 geometry-content stamp다. 레이어 캐시는 자동으로 채우고, 커스텀 retained static 생성자는 필요 시 `UiDrawList.ComputeStaticGeometryStamp(...)`로 직접 설정할 수 있다.
- layer opacity와 append translation은 `UiDrawCommand.Opacity`/`UiDrawCommand.Translation`으로 전달되어 Vulkan vertex shader에서 적용되므로, retained static geometry key/stamp는 layer opacity나 배치 위치가 아니라 geometry content를 설명해야 한다.
- producer가 독립 draw group을 이미 알고 있고 의도한 stacking을 보존할 수 있으면 `UiDrawListBuilder.Split(count)`와 `SetCurrentChannel(index)`를 사용한다. 그 group들이 별도 draw list로 남아도 되고 focused gate에서 draw-list boundary가 이득임이 증명된 경우에만 `MergeChannelsAsDrawLists()`를 선택한다. `global_dirty_strategy_bench.cs`는 copy-free channel output이 더 느린 counterexample이다. 하나의 물리적으로 병합된 draw list가 필요하거나 focused gate가 copy-merge를 지지하면 `Merge()`를 사용한다. channel 출력 순서가 곧 명시적 draw order이므로, 겹치는 content의 stacking이 달라지면 안 되는 경우에는 channelize하지 않는다.

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
- `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait`

이유:

- 패키지 지시문을 로컬 프로젝트 참조로 바꿔 준다.
- 필요 시 Windows 엔트리 호출을 정규화한다.
- 기본적으로 NativeAOT 경로를 지원한다.
- `-Wait`로 NativeAOT GUI 샘플 종료까지 대기할 수 있어 자동 파일 로그 벤치마크 수집에 적합하다.
- 단순 패키지 모드와 달리 현재 워크스페이스 소스 변경을 반영한다.

로컬 소스 수정 검증에 `dotnet run samples/fba/<file>.cs`를 기준으로 삼지 않는다.

## 빌드와 검증

기본 저장소 빌드:

- `dotnet build Duxel.slnx -c Release`

의미 있는 변경 뒤 최소 검증 기준:

- 솔루션 빌드, 또는
- 관련 샘플 1개 이상 실행

가능한 한 가장 작은 검증으로, 그러나 충분히 증명되는 검증을 선택한다.

이 저장소에서는 작업이 .NET, 성능, Vulkan, native import, FBA sample을 건드릴 때 `.github/copilot-instructions.md`와 관련 `.github/skills/*/SKILL.md`도 확인한다. Codex도 이를 로컬 프로젝트 지침으로 활용하되, 현재 대화의 계약은 `AGENTS.md`와 명시적인 user/developer instruction을 우선한다.

## 환경 변수와 런타임 토글

샘플과 진단에서 자주 쓰는 런타임 토글:

- `DUXEL_APP_PROFILE`
- `DUXEL_PERF_PROFILE`
- `DUXEL_PERF_BENCH_OUT`
- `DUXEL_PERF_BENCH_SECONDS`
- `DUXEL_PERF_INITIAL_POLYGONS`
- `DUXEL_PERF_GLOBAL_STATIC_CACHE`
- `DUXEL_SAMPLE_AUTO_EXIT_SECONDS`
- `DUXEL_IMAGE_PATH`
- `DUXEL_LAYER_BENCH_BACKEND`
- `DUXEL_LAYER_BENCH_OPACITY`
- `DUXEL_LAYER_BENCH_PARTICLES`
- `DUXEL_LAYER_BENCH_LAYOUTS`
- `DUXEL_LAYER_BENCH_PHASE_SECONDS`
- `DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER`
- `DUXEL_LAYER_BENCH_OUT`
- `DUXEL_GLOBAL_DIRTY_BENCH_OUT`
- `DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS`
- `DUXEL_GLOBAL_DIRTY_BENCH_DENSITY`
- `DUXEL_GLOBAL_DIRTY_BENCH_COLS`
- `DUXEL_GLOBAL_DIRTY_BENCH_ROWS`
- `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS`
- `DUXEL_PIPELINE_ORDER_BENCH_OUT`
- `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`
- `DUXEL_PIPELINE_ORDER_ITEMS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`
- `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`
- `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`
- `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`
- `DUXEL_STATIC_CACHE_REBUILD_LAYERS`
- `DUXEL_STATIC_CACHE_REBUILD_DENSITY`
- `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW`
- `DUXEL_DIRECT_TEXT_PAGE`
- `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`
- `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`
- `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`
- `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`
- `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`
- `DUXEL_DTPAGE_UPLOAD_REGIONS`
- `DUXEL_DTPAGE_UPLOAD_PAGES`
- `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`
- `DUXEL_TEXT_RENDERING`
- `DUXEL_VK_COMMAND_SCHEDULER`
- `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`
- `DUXEL_UI_COMMAND_SCHEDULER`
- `DUXEL_VK_COMMAND_DIAG`
- `DUXEL_VK_COMMAND_DIAG_EVERY`
- `DUXEL_VK_COMMAND_DIAG_FRAMES`
- `DUXEL_VK_COMMAND_DIAG_OUT`
- `DUXEL_VK_FONT_DIAG`
- `DUXEL_VK_FONT_DIAG_OUT`
- `DUXEL_VK_PROFILE`
- `DUXEL_VK_PROFILE_EVERY`
- `DUXEL_VK_PROFILE_OUT`
- `DUXEL_VK_GPU_PROFILE`
- `DUXEL_VK_UPLOAD_BATCH`
- `DUXEL_VK_UPLOAD_QUEUE`
- `DUXEL_VK_STATIC_GEOMETRY_UPDATE`
- `DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE`
- `DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE`
- `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`
- `DUXEL_VK_SOLID_UNIFIED_PIPELINE`
- `DUXEL_VK_SOLID_UNIFIED_STATIC`
- `DUXEL_VK_TRIANGLE_COLOR_PIPELINE`

특정 토글에 의존하는 작업은 문서화하거나 사용하기 전에 관련 샘플에서 이름과 의미를 다시 확인한다.

Vulkan 커맨드 기록 프로파일링은 `DUXEL_VK_PROFILE=1`로 켠다. `DUXEL_VK_PROFILE_EVERY`는 로그 간격 프레임 수이고, `DUXEL_VK_PROFILE_OUT`은 프로파일 라인을 파일에 append한다. 프로파일 라인의 `device(vendor=... vid=... did=... type=... name=... gfxQ=... uploadQ=... xferCandQ=... tsBits=... tsPeriodNs=... gpuTs=...)`와 `policy(upload=... transferCandidate=... triColor=... solidUnified=... solidUnifiedStatic=... staticPrimTri=... staticUpdate=... staticUpdateReq=... scheduler=... schedWindow=... staticSecondaryMin=...)`는 나중에 NVIDIA, AMD, Intel, integrated/discrete 장치의 artifact를 비교할 때 어떤 장치와 자동 정책에서 나온 결과인지 증명한다. `uploadQ`는 upload command buffer가 실제로 사용하는 queue family이고, `xferCandQ`는 탐지된 non-graphics transfer-capable 후보일 뿐이다. 따라서 `policy(upload=graphics transferCandidate=1)`은 후보가 있다는 뜻이지 upload가 이미 transfer queue를 사용한다는 뜻이 아니다. `DUXEL_VK_UPLOAD_QUEUE=transfer`는 후보가 있을 때만 split transfer upload path를 opt-in으로 켠다. `policy(upload=transfer transferCandidate=1)`은 upload copy command buffer가 그 queue를 실제로 사용한다는 profile 증거다. `staticUpdateReq`는 요청된 mode이고 `staticUpdate`는 장치 정책으로 resolve된 mode다. `scheduler`는 resolved command-scheduler mode이며 `disabled`, `static`, `all` 중 하나다. `stateUs(pipe=... desc=... buf=... push=... scissor=...)`는 pipeline bind, descriptor bind, vertex/index/primitive buffer bind, push constant, scissor set 비용을 분리해서 보여준다. `clipCache(calc=... reuse=...)`는 실제 scissor rectangle 계산 횟수와 연속으로 같은 clip/translation을 재사용한 횟수를 기록한다. `staticSec(cand=... cmds=... draws=...)`는 현재 `staticSecondaryMin` threshold를 넘긴 static draw list 수와 그 후보 안의 기록된 command/draw 수를 기록한다. `cand=0`이면 static list가 존재해도 그 frame에서는 secondary command buffer가 다음 경로 후보라는 가설이 약하다는 뜻이다. `listWork(staticCmd=... dynCmd=... staticDraw=... dynDraw=... staticPipe=... dynPipe=... staticClip=... dynClip=... staticScissor=... dynScissor=... staticPush=... dynPush=... staticGeom=... dynGeom=... staticPrim=... dynPrim=...)`는 command-recording 작업을 static cached replay와 dynamic draw list로 나눠 기록한다. 다음 병목이 static replay policy인지 dynamic UI ordering/channelization인지 판단할 때 이 값을 우선 확인한다. `staticGeom(hit=... create=... replace=... update=... reuse=... hash=...)`는 upload phase에서 static geometry cache hit, buffer creation, content replacement, 같은 shape의 in-place content update, 같은 shape의 rotating-buffer reuse, fallback content hashing을 기록한다. `staticMem(active=... activeBytes=... retired=... retiredBytes=...)`는 memory-pressure 확인을 위해 active static geometry entry/bytes와 retired rotating-pool entry/bytes를 기록한다. `staticPrim(expand=... expandPrim=... force=... autoSkip=... autoSkipPrim=... autoSkipMut=... expandBytes=... autoSkipBytes=...)`는 frame별 static primitive triangle expansion 결정을 기록한다. `policy(... staticPrimTri=1 ...)`은 장치 정책상 경로가 허용됐다는 뜻일 뿐이므로, 실제 draw-list가 확장됐는지는 이 카운터로 확인한다. `autoSkipMut`은 같은 static geometry tag의 content가 바뀌는 중이라 확장을 억제한 skip subset이다. `pipeClass(font=... texTri=... colorTri=... texPrim=... colorPrim=... solid=...)`는 renderer policy 적용 뒤 실제로 선택된 pipeline class를 기록하므로 원본 source command kind만 보는 것보다 pipeline switch 분석에 더 정확하다. `sched(probe=... hit=... miss=... nochange=... lists=... merged=... us=...)`는 opt-in command scheduler의 동작과 비용을 기록한다. `upSched(sub=... prepSub=... wait=... flush=... bytes=... texRegions=... bufCopies=... submitUs=... prepUs=... waitUs=...)`는 staging upload scheduler submission, split-transfer graphics-prepare submission, upload fence wait, batch flush, staging byte volume, texture copy region, static/buffer copy command, 그리고 submission/wait 비용을 기록한다. `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)`는 image layout transition 횟수와 transfer-queue-compatible stage mask / graphics-stage-required barrier를 분리해서 기록한다. `state=`는 scissor를 제외한 state command 시간이고, scissor는 `clip=`에도 포함되므로 별도로 해석한다.

성능 데모는 확인하려는 병목에 맞아야 한다. 넓은 layer-widget scene은 최종 regression 용도로 사용하되, 질문이 좁으면 초점형 FBA 데모를 고르거나 추가한다. `samples/fba/pipeline_ordering_bench_fba.cs`는 dynamic solid/text pipeline ordering 비용을 확인하는 초점형 gate이며 alternating, grouped solids-then-text, copy-merge `channelized-solid-text`, copy-free `channel-drawlists-solid-text` phase를 포함하고 `DUXEL_PIPELINE_ORDER_BENCH_OUT`, `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`, `DUXEL_PIPELINE_ORDER_ITEMS`를 사용한다. `samples/fba/dynamic_widget_ordering_bench_fba.cs`는 widget-like dynamic producer ordering과 row-clip churn을 확인하는 초점형 gate이며 alternating widget row, grouped solids-then-text, copy-merge channelized phase, copy-free channel-drawlists phase를 포함하고 `DUXEL_DYN_WIDGET_ORDER_BENCH_OUT`, `DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_ITEMS`, `DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS`를 사용한다. `samples/fba/vector_primitives_bench_fba.cs`는 primitive-heavy geometry를 확인하는 초점형 gate이며 `DUXEL_VECTOR_BENCH_WORKLOAD=mixed`, `rect-outline`, `axis-line`과 `DUXEL_VECTOR_BENCH_OUT`, `DUXEL_VECTOR_BENCH_PHASE_SECONDS`, `DUXEL_VECTOR_BENCH_COUNTS`를 사용한다. `samples/fba/Duxel_perf_test_fba.cs`는 polygon physics/perf smoke이며 기본은 Render profile과 global static backdrop cache다. `DUXEL_PERF_PROFILE=render|display`로 시작 profile을 override할 수 있고, `DUXEL_PERF_GLOBAL_STATIC_CACHE=0`은 retained static backdrop reference를 끈다. UI의 Render Profile checkbox는 재시작 없이 MSAA를 `1x`/`4x`로 바꿔 profile을 즉시 적용한다. `samples/fba/global_dirty_strategy_bench.cs`는 global static background cache와 dynamic overlay update를 비교하는 초점형 gate이며, `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS`로 그 배경/오버레이 channel 구조에서 copy-merge와 별도 draw-list 출력을 비교한다. `samples/fba/static_layer_moving_order_bench_fba.cs`는 moving static-layer replay schedule 재사용을 확인하는 초점형 gate이며 `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`, `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`를 사용한다. `samples/fba/static_cache_rebuild_bench_fba.cs`는 cache replay, false-dirty static rebuild, mutating static geometry replacement/update 비용, Core cache-copy allocation pressure, static primitive triangle memory pressure를 분리하는 초점형 gate이며 측정 frame별 `avgAllocatedBytes`도 보고한다. 이 gate는 `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`, `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_LAYERS`, `DUXEL_STATIC_CACHE_REBUILD_DENSITY`, `DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE`(`circles`, `rects`, `mixed`), `DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS`, GPU-bound variant용 선택 토글 `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW`를 사용한다. `samples/fba/texture_upload_barrier_bench_fba.cs`는 transfer-queue policy 변경 전 texture upload copy/barrier 동작을 분리하는 초점형 gate이며 `DUXEL_TEXTURE_UPLOAD_BENCH_OUT`, `DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS`, `DUXEL_TEXTURE_UPLOAD_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGION_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGIONS`, `DUXEL_TEXTURE_UPLOAD_TEXTURES`, `DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES`를 사용한다. `samples/fba/directtext_page_upload_bench_fba.cs`는 platform glyph rasterizer 비용을 섞지 않고 DirectText page-style partial texture upload 동작을 확인하는 초점형 gate이며 `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`, `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`, `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`, `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`, `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`, `DUXEL_DTPAGE_UPLOAD_REGIONS`, `DUXEL_DTPAGE_UPLOAD_PAGES`, `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`를 사용한다.

축 정렬 `UiDrawListBuilder.AddRect(...)` 외곽선은 `rounding <= 0`일 때 triangle polyline geometry가 아니라 rect-filled primitive emission을 사용해야 한다. 수평/수직 `UiDrawListBuilder.AddLine(...)`도 rect-filled primitive emission을 사용하고, diagonal line은 기존 quad triangle path를 유지한다. 이렇게 하면 일반 사각형 border와 축 정렬 separator가 primitive path에 남아 text/triangle/primitive pipeline churn이 줄어든다. Rounded outline은 기존 polyline path를 유지한다.

`DUXEL_VK_STATIC_GEOMETRY_UPDATE`는 같은 shape의 static geometry content change 정책을 제어한다. 유효한 값은 `auto`, `replace`, `inplace`, `rotating`이다. `auto`는 검증된 NVIDIA discrete GPU에서는 `rotating`으로, AMD/Intel과 그 외 장치에서는 같은 정책을 증명하는 gate가 쌓일 때까지 `replace`로 resolve된다. `replace`는 allocation/replacement를 강제하고, `inplace`는 fence-waited existing-buffer reupload를 강제하며, `rotating`은 retired-buffer reuse를 강제한다.

`DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE=1`은 `DUXEL_VK_STATIC_GEOMETRY_UPDATE=inplace`의 backward-compatible explicit override다. static geometry cache entry의 vertex/index/primitive count와 expanded primitive triangle layout이 같으면 Vulkan은 모든 in-flight frame fence를 기다린 뒤 새 replacement buffer를 만들지 않고 기존 device-local buffer에 다시 업로드한다. 이 경로는 focused A/B용으로 유지한다. rotating update가 full in-flight fence wait를 피하므로 기본 경로 후보로 더 깔끔하다.

`DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE=1`은 `DUXEL_VK_STATIC_GEOMETRY_UPDATE=rotating`의 backward-compatible explicit override다. 같은 shape의 content change가 발생하면 현재 static geometry buffer를 frame-safe reuse pool에 retire하고, 안전해진 retired buffer가 있으면 그것을 활성화하며 없으면 새 buffer를 만들어 pool을 시드한다. Retired pool은 tag별로 frame count를 넘지 않도록 제한되고, 오래 재사용되지 않은 retired buffer는 `StaticGeometryRetiredReuseGraceFrames` 뒤에 trim된다. 이전 제출 frame이 아직 읽을 수 있는 buffer를 덮어쓰지 않으므로 in-place update보다 기본 경로 후보로 더 깔끔하다. 검증된 NVIDIA discrete GPU에서는 기본 resolve policy이고, 다른 vendor/device gate에서는 명시적으로 사용할 수 있다.

`DUXEL_VK_COMMAND_SCHEDULER=all` 또는 `1`은 모든 eligible draw list에 opt-in overlap-constrained Vulkan command scheduler를 켠다. `DUXEL_VK_COMMAND_SCHEDULER=static`은 같은 scheduler를 static-layer replay draw list에만 켜서, 측정된 static-layer replay 이득은 유지하면서 dynamic whole-list scheduling을 hot path에서 제외한다. 이 경로는 `UiDrawCommand` coverage bounds로 overlap 때문에 유지해야 하는 순서를 보존하면서 ready command를 pipeline class별로 묶고, 기록 시점에서 새로 인접해진 호환 command를 병합한다. `all` 모드에서는 dynamic draw-list 생성 단계에서 `UiDrawList.CommandScheduleStamp`도 계산해서 cache-hit scheduling이 Vulkan command recording에서 전체 command hash/compare를 반복하지 않게 한다. 기본 경로는 아니다. 초점형 프로파일링에서만 사용하고, 넓은 scene이 무제한 scheduling 비용을 내지 않도록 `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`를 제한해서 사용한다.

Static cached layer replay는 `DUXEL_VK_COMMAND_SCHEDULER=static`, `all`, 또는 `1`일 때 layer capture 시점에 보수적인 command schedule shape stamp를 한 번 계산한다. 이 stamp는 안정적인 local command shape와 raw local command bounds를 기준으로 한다. Vulkan scheduling도 static-layer draw list에는 같은 보수적 bounds를 사용하므로, 안정적인 layer가 이동하거나 clipping되어도 매 프레임 overlap analysis나 replay-shape hashing을 반복하지 않고 cached safe order를 재사용할 수 있다.

Dirty static layer가 같은 draw-list shape로 다시 build되면 Core는 새 local vertex/index/command/primitive 배열을 할당하지 말고 기존 `UiLayerCachedList` storage를 in-place refresh해야 한다. Overwrite 뒤 `StaticGeometryStamp`와 command schedule shape stamp를 다시 계산하고, 안정적인 `duxel.layer.static:{layerId}:list:{i}` key는 유지하며, replay를 invalidate해서 opacity/translation/clip command가 갱신된 content stamp 아래에서 다시 만들어지게 한다.

`DUXEL_UI_COMMAND_SCHEDULER=1`은 `UiDrawListBuilder.Flush()`에서 더 이른 builder-stage scheduling 실험을 켠다. 이 토글은 `DUXEL_VK_COMMAND_SCHEDULER`와 별도로 둔다. 매 프레임 draw-list 내용이 바뀌는 동적 whole-list builder scheduling은 절감하는 기록 비용보다 더 비쌀 수 있으므로 구조 실험 전용이다. 기본 경로 후보로 보기 전에 stable layer/static schedule caching이나 명시적 draw channel을 우선한다.

GPU timestamp 계측은 `DUXEL_VK_GPU_PROFILE=1`로 추가로 켠다. 이 값은 그래픽 queue timestamp를 지원하는 장치에서만 활성화되고, 프로파일 라인의 `gpuRender=...`에 render pass 전후 GPU 실행 시간을 microsecond 단위로 추가한다. CPU command-recording 비용과 shader/GPU-side 비용을 분리할 때 사용한다.

Static cached rect/circle primitive를 triangle vertex/index geometry로 확장하는 경로는 `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES=auto`가 기본이다. 자동 정책은 triangle color pipeline이 켜진 NVIDIA/AMD discrete GPU에서만 켠 뒤 draw-list별 byte guard와 mutation guard를 적용한다. `auto`는 예상 expanded vertex/index bytes가 primitive-instance bytes의 `32x`를 넘으면 확장하지 않아 high-segment primitive-heavy cache를 static primitive-instance 경로에 남긴다. 또한 같은 static geometry tag의 content hash가 바뀌면 해당 tag는 `30`프레임 동안 확장을 억제해서 mutating static layer가 안정될 때까지 upload가 싼 primitive-instance 경로를 사용하게 한다. `1/true/on`은 확장을 강제하고 `0/false/off`는 비활성화를 강제한다. 이 경로는 cached static draw-list의 command 순서, clip, opacity, texture state를 유지하면서 static primitive buffer bind와 primitive shader 경로를 줄인다. 실제 expand/skip 여부는 장치 정책 블록만 보지 말고 profile의 `staticPrim(...)` 카운터로 확인한다.

Static primitive auto decision의 구현 경계는 `VulkanRendererBackend.StaticPrimitivePolicy.cs`다. `VulkanRendererBackend.StaticGeometry.cs`는 upload code 안에 device/heuristic decision을 직접 넣지 말고 이 policy 경계를 호출해야 한다.

Upload command pool, staging buffer lifetime, `DUXEL_VK_UPLOAD_BATCH`, `DUXEL_VK_UPLOAD_QUEUE`, staging offset reservation, batch flush, graphics upload-prepare command submission, transfer upload-copy submission, single-time upload submission 책임은 `VulkanRendererBackend.UploadScheduler.cs`에 둔다. Texture update와 static geometry upload 코드는 자체 staging command 경로를 늘리지 말고 이 경계를 호출해야 한다. Upload batching은 기본 enabled이며, 진단이 필요할 때만 `DUXEL_VK_UPLOAD_BATCH=0`, `false`, `off`로 끈다. Transfer upload path는 opt-in 전용이다. 대상 vendor/device의 focused texture/page upload gate가 split path가 더 빠르다는 것을 증명하기 전까지 default graphics path를 유지한다. 로컬 NVIDIA `10de:2f58` gate에서는 `upSched(prepSub=1 ...)`와 더 긴 upload wait가 짧은 transfer-copy submission 이득을 상쇄해서 transfer가 generic texture update에서 약 `12-13%`, DirectText page update에서 약 `15-19%` 느렸다.

Rotating static geometry retired-buffer pool ownership은 `VulkanRendererBackend.StaticGeometryRetiredPool.cs`에 둔다. Frame-safe availability check, tag별 pool cap, idle pruning, retired memory stats, retired-buffer teardown은 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 active cache 생성/교체/prune/teardown 흐름에서 이 경계를 호출해야 한다.

Active static geometry cache identity와 lifetime은 `VulkanRendererBackend.StaticGeometryCache.cs`에 둔다. Static tag recognition, active entry get/set, seen-frame tracking, LRU prune, active memory stats, active-buffer teardown은 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 same-shape replacement policy orchestration에 집중해야 한다.

Static geometry upload/layout writing은 `VulkanRendererBackend.StaticGeometryUpload.cs`에 둔다. Static vertex/index/primitive staging write, expanded primitive triangle layout/writer, primitive instance writer, solid sentinel reservation helper는 그 파일에서 유지한다.

Same-shape static geometry replacement policy는 `VulkanRendererBackend.StaticGeometryReplacementPolicy.cs`에 둔다. `StaticGeometryShape`, cache-entry match check, resource presence check, in-place compatibility check는 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 선택된 경로를 orchestration하며 cache/upload/retired-pool 경계를 호출해야 한다.

Dynamic per-frame geometry upload는 `VulkanRendererBackend.DynamicGeometryUpload.cs`에 둔다. Dynamic draw-list index ownership, mapped frame vertex/index write, dynamic primitive instance write, dynamic primitive sentinel count는 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 static binding과 dynamic count 준비만 담당하며 frame loop는 dynamic upload와 static cache pruning을 별도 단계로 호출해야 한다.

Dynamic frame geometry buffer ownership은 `VulkanRendererBackend.GeometryBuffers.cs`에 둔다. Dynamic frame vertex/index/primitive buffer capacity growth, host-visible memory mapping/unmapping, queued replacement destroy, frame geometry buffer teardown은 그 파일에서 유지한다. `VulkanRendererBackend.FrameGeometry.cs`는 필요한 capacity를 결정하고 이 경계를 호출해야 하며, lifecycle과 swapchain teardown은 `DestroyGeometryBuffers()`를 호출해야 한다.

Static geometry buffer materialization은 `VulkanRendererBackend.StaticGeometryMaterialization.cs`에 둔다. Static device-local buffer allocation, common static vertex/index/primitive upload fan-out, create-path materialization, same-shape reupload materialization은 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 high-level cache/reuse/update/replace/create 정책을 선택한 뒤 이 경계를 호출해야 한다.

Per-frame static/dynamic draw-list preparation은 `VulkanRendererBackend.StaticGeometryFramePreparation.cs`에 둔다. Static binding dictionary ownership, dynamic vertex/index/primitive counter, static draw-list binding classification, static-geometry profile counter reset은 그 파일에서 유지하고, render loop는 frame buffer sizing에 `FrameGeometryCounts`를 사용해야 한다.

Static geometry content/shape derivation은 `VulkanRendererBackend.StaticGeometryContent.cs`에 둔다. `StaticGeometryContent`, content hash selection, fallback hash profiling, static primitive triangle decision recording, mutation-suppression input, expanded primitive layout selection, shape packing은 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 준비된 content를 받아 cache/reuse/update/replace/create 정책을 적용해야 한다.

Static geometry policy application은 `VulkanRendererBackend.StaticGeometryPolicyApplication.cs`에 둔다. Cache-hit accounting, reusable-buffer activation, in-place update activation, replacement teardown/retire choice, creation counting, materialize+activate 호출은 그 파일에서 유지하고, `VulkanRendererBackend.StaticGeometry.cs`는 준비된 content와 shape 위의 얇은 branch selector로 남겨야 한다.

Primitive instance encoding은 `VulkanRendererBackend.PrimitiveInstanceEncoding.cs`에 둔다. `PrimitiveInstance` payload flag, solid-triangle sentinel payload, dynamic/static primitive sentinel count, primitive instance creator, sentinel reservation predicate는 그 파일에서 유지하고, dynamic/static upload와 command recording 코드는 임의 payload/sentinel 상수를 새로 두지 말고 이 경계를 호출해야 한다.

Command pipeline binding state는 `VulkanRendererBackend.CommandPipelineState.cs`에 둔다. Current-pipeline cache, `vkCmdBindPipeline` timing, source command-kind bind counter, actual pipeline-class counter는 그 파일에서 유지하고, `VulkanRendererBackend.CommandRecording.cs`는 desired pipeline을 고른 뒤 command-state 경계를 호출해야 한다.

Command descriptor-set binding state는 `VulkanRendererBackend.CommandDescriptorState.cs`에 둔다. Last descriptor-set cache, `vkCmdBindDescriptorSets` timing, descriptor bind counter는 그 파일에서 유지하고, command recording은 texture resource를 resolve한 뒤 이 경계를 호출해야 한다.

Command geometry/primitive buffer binding state는 `VulkanRendererBackend.CommandBufferBindingState.cs`에 둔다. Geometry vertex/index buffer cache, primitive instance buffer cache, `vkCmdBindVertexBuffers`/`vkCmdBindIndexBuffer` timing, geometry/primitive bind counter는 그 파일에서 유지하고, command recording은 triangle, expanded static primitive, unified solid primitive, primitive-instance draw path에서 이 경계를 호출해야 한다.

Command frame/render-pass recording은 `VulkanRendererBackend.CommandFrameRecording.cs`에 둔다. Command-buffer begin/end, main render-pass begin/end, GPU timestamp query reset/write helper는 그 파일에서 유지하고, `VulkanRendererBackend.CommandRecording.cs`는 raw render-pass setup을 직접 포함하지 말고 이 frame 경계를 호출해야 한다.

Render-entry shell ownership은 `VulkanRendererBackend.RenderEntry.cs`에 둔다. `RenderDrawData(...)`는 그 파일에서 profile reset, texture update, frame target/begin, frame geometry preparation, command recording preparation, frame completion으로 이어지는 high-level frame order만 보존한다. Texture lifetime, swapchain acquire, geometry upload, command-buffer recording, submit/present, profile-output internals를 이 shell로 inline하지 않는다.

Frame orchestration은 `VulkanRendererBackend.FrameOrchestration.cs`에 둔다. Frame target validation, swapchain image acquire/recreate retry, per-frame fence wait, image-in-flight ownership, pending destroy flush, low-level submit/present helper, present-result handling, frame-profile timing/output helper는 그 파일에서 유지한다. `VulkanRendererBackend.RenderEntry.cs`는 high-level frame order만 보존하고, swapchain acquire, queue submit, queue present, frame-profile output 구성을 다시 inline으로 키우지 않아야 한다.

Frame geometry preparation/upload는 `VulkanRendererBackend.FrameGeometry.cs`에 둔다. Per-frame static binding preparation, dynamic geometry capacity decision, dynamic primitive sentinel capacity, dynamic geometry upload, static cache pruning, upload timing, command recording에 넘길 geometry-buffer tuple은 그 파일에서 유지한다. `RenderDrawData(...)`는 이 경계를 호출해야 하며 static/dynamic geometry preparation 또는 upload phase logic을 다시 inline으로 키우지 않아야 한다.

Frame command recording preparation은 `VulkanRendererBackend.FrameCommandRecording.cs`에 둔다. Frame command-pool reset, command recording timing, `RecordCommandBuffer(...)` invocation, GPU timestamp-issued bookkeeping, submit에 넘길 command buffer 반환은 그 파일에서 유지한다. `RenderDrawData(...)`는 이 경계를 호출해야 하며 `ResetCommandPool`, record timing, timestamp-issued logic을 다시 inline으로 키우지 않아야 한다.

Frame completion은 `VulkanRendererBackend.FrameCompletion.cs`에 둔다. Final semaphore extraction, `SubmitFrame(...)`, `PresentFrame(...)`, present-result handling call, profile emission, frame-index advance는 그 파일에서 유지한다. `RenderDrawData(...)`는 이 경계를 호출해야 하며 submit/present/profile/frame-index completion logic을 다시 inline으로 키우지 않아야 한다.

Command frame/list recording context는 `VulkanRendererBackend.CommandContext.cs`에 둔다. `CommandFrameContext`, `CommandDrawListContext`, `CreateCommandFrameContext(...)`는 그 파일에서 유지하고, `VulkanRendererBackend.CommandRecording.cs`는 frame과 draw-list 경계에서 이를 만들며, scissor, push-constant, draw-path 경계는 긴 transform, clip, buffer, offset scalar 묶음 대신 이 context를 소비해야 한다. Render-pass setup용 정수 framebuffer extent는 frame context에 유지하고, `RecordCommandBuffer(...)`에 별도 TAA/jitter scalar parameter를 다시 추가하지 않는다. 실제 TAA 경로가 생기면 temporal jitter는 frame context 경계를 통해 복원한다.

Command recording state aggregation은 `VulkanRendererBackend.CommandRecordingState.cs`에 둔다. Per-pass profile, diagnostic, texture, scissor, pipeline, descriptor, buffer, push, draw-dispatch state 묶음은 그 파일에서 유지해서 command recording과 draw-list recording이 모든 경계를 넓히지 않고 하나의 state aggregate를 넘기게 한다. 최종 record timing/stat output 구성도 `CompleteCommandRecordingState(...)`와 `BuildCommandRecordStats(...)`를 통해 이 파일에 둔다. `VulkanRendererBackend.CommandRecording.cs`는 개별 state field에서 output tick이나 profile stat을 다시 inline으로 조립하지 않아야 한다.

Command draw-list recording은 `VulkanRendererBackend.CommandDrawListRecording.cs`에 둔다. Viewport setup, draw-list offset tracking, static binding lookup, scheduler setup/profile event, command iteration, command별 diagnostic/texture/font/scissor sequencing, draw-path dispatch는 그 파일에서 유지한다. `VulkanRendererBackend.CommandRecording.cs`는 frame begin 이후 이 경계를 호출하고, 새 local draw-list traversal loop를 다시 키우지 않아야 한다.

Command push-constant state는 `VulkanRendererBackend.CommandPushConstantState.cs`에 둔다. Transform/opacity push cache, `vkCmdPushConstants` range selection, timing, push counter는 그 파일에서 유지하고, command recording은 command translation/opacity와 frame context 데이터를 넘긴 뒤 이 경계를 호출해야 한다.

Command scissor state는 `VulkanRendererBackend.CommandScissorState.cs`에 둔다. Computed scissor rectangle helper, scissor reuse, current scissor cache, visibility rejection, clipping timing, `vkCmdSetScissor` timing, scissor counter는 그 파일에서 유지하고, command recording은 pipeline/draw dispatch 전에 이 경계를 호출해야 한다. `TryComputeScissorRect(...)`는 float clip-bound 단계에서 한 번 clamp하고, focused profile이 추가 boundary check 필요성을 증명하지 않는 한 두 번째 integer framebuffer clamp를 다시 넣지 않는다.

Command draw dispatch는 `VulkanRendererBackend.CommandDrawDispatch.cs`에 둔다. Triangle indexed draw dispatch, expanded static primitive indexed draw dispatch, primitive-instance draw dispatch, index/instance calculation, draw-call timing/counting은 그 파일에서 유지하고, command recording은 필요한 state를 bind한 뒤 선택된 draw path에 대해 이 경계를 호출해야 한다.

Command classification은 `VulkanRendererBackend.CommandClassification.cs`에 둔다. Command별 triangle/primitive 분류, white/font texture 분류, texture 필요 여부, static expanded primitive geometry flag는 그 파일에서 유지하고, command recording은 이 hot-path boolean들을 inline으로 다시 계산하지 말고 classification 값을 사용해야 한다.

Command texture lookup state는 `VulkanRendererBackend.CommandTextureState.cs`에 둔다. Last-texture cache, texture dictionary lookup, texture lookup timing은 그 파일에서 유지하고, command recording은 `CommandClassification.CommandNeedsTexture`가 true일 때만 descriptor binding 전에 이 경계를 통해 `TextureResource`를 resolve해야 한다. Non-texture solid/color command는 이 경계를 완전히 건너뛴다. 필요한 texture가 없을 때 font 관련 보고가 필요하면 font diagnostic 경계로 보고해야 한다.

Texture resource/update ownership은 `VulkanRendererBackend.TextureResources.cs`에 둔다. `ApplyTextureUpdates(...)`, texture create/update/destroy, pending texture-destroy flush, texture data upload/batching, font/white texture id classification, texture dictionary/font-white id state, texture descriptor allocation은 그 파일에서 유지한다. `RenderDrawData(...)`는 `ApplyTextureUpdates(...)`만 호출해야 하며, command recording은 이 경계의 texture id classification을 사용하고 texture lifetime 또는 upload code를 다시 키우지 않아야 한다.

Generic Vulkan resource helper는 `VulkanRendererBackend.ResourceHelpers.cs`에 둔다. Swapchain image-view 생성, frame-safe buffer destroy helper, `CreateBuffer(...)`, `CreateImage(...)`, `CreateImageView(...)`, `FindMemoryType(...)`, `ToVkFormat(...)`, MSAA color image create/destroy는 그 파일에서 유지한다. Texture, static geometry, dynamic geometry, staging upload, swapchain, MSAA 경로는 이 경계를 소비해야 하며 `VulkanRendererBackend.cs`에 allocation 또는 memory-type code를 다시 키우지 않아야 한다.

Image layout transition policy는 `VulkanRendererBackend.ImageTransitions.cs`에 둔다. `TransitionImageLayout(...)`, texture upload prepare/finalize layout helper, pending texture shader-read finalization state, layout pair resolution, access mask 선택, pipeline stage mask 선택, `imgTrans(...)` profile counter는 그 파일에서 유지한다. Shader/color-attachment stage dependency를 upload 또는 generic allocation code 안에 숨기지 않는다. Transfer queue 작업은 active upload queue(`uploadQ`)와 후보 queue(`xferCandQ`)를 분리해서 다루고, 기본 upload queue policy를 바꾸기 전에 queue ownership과 stage mask를 transition별로 명시적으로 모델링해야 한다. Texture/page upload가 barrier-heavy인지 copy-heavy인지는 `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)`로 증명한다. `texture_upload_barrier_bench_fba.cs`에서는 같은 texture의 non-overlapping region batch가 `total=2`, many-texture update가 `total=2 * textureCount`로 나와야 한다. `xferStage`는 stage mask가 transfer queue와 호환된다는 뜻이고, `gfxStage`는 transfer-only upload recording 전에 graphics/fragment/color stage mask를 split하거나 옮겨야 한다는 뜻이다.

Render-target setup은 `VulkanRendererBackend.RenderTargets.cs`에 둔다. Render-pass 생성, swapchain framebuffer 생성, render-pass/framebuffer state, MSAA sample-count state, MSAA color image/view state는 그 파일에서 유지하며, single-sample path, MSAA color attachment, resolve attachment, subpass dependency, framebuffer attachment list rule도 이 경계에서 관리한다. Raw render-pass/framebuffer setup을 `VulkanRendererBackend.cs`나 command-recording 파일로 다시 키우지 않는다.

Pipeline resource setup은 `VulkanRendererBackend.PipelineResources.cs`에 둔다. Descriptor pool 생성, descriptor/pipeline layout 생성, graphics pipeline assembly, sampler 생성/state, pipeline/cache/shader/descriptor state, pipeline cache load/save/destroy, shader module 생성, embedded shader loading은 그 파일에서 유지한다. Pipeline object 생성, descriptor pool setup, pipeline cache policy, shader-loader policy를 frame orchestration 또는 command recording으로 다시 섞지 않는다.

Sync/query setup은 `VulkanRendererBackend.SyncResources.cs`에 둔다. Frame command pool/buffer allocation, per-frame fence 생성/state, image-in-flight state, semaphore ring 생성/state, upload command resource entry, GPU timestamp query constant, GPU timestamp query-pool 생성은 그 파일에서 유지한다. Frame orchestration은 초기화된 resource를 소비해야 하며, frame sync allocation 또는 query-pool creation code를 다시 키우지 않는다. GPU profiling request/resolved state는 profile output과 query-result 해석이 소비하므로 diagnostics 경계에 둔다.

Shared frame-fence wait helper는 `VulkanRendererBackend.FrameSync.cs`에 둔다. Resource replacement, texture teardown, static geometry update, 기타 cross-frame hazard avoidance 경로가 공유하는 all-in-flight frame fence wait는 그 파일에서 유지한다. Per-frame acquire/present wait는 `VulkanRendererBackend.FrameOrchestration.cs`에 남기고, sync allocation은 `VulkanRendererBackend.SyncResources.cs`에 남겨야 한다.

Swapchain selection policy는 `VulkanRendererBackend.SwapchainPolicy.cs`에 둔다. Surface format selection, present mode selection, platform framebuffer state, framebuffer extent selection은 그 파일에서 유지한다. `CreateSwapchain(...)`은 그 결정을 소비할 수 있지만, preference list, VSync fallback message, platform framebuffer clamp를 다시 inline으로 키우지 않는다.

Swapchain resource creation/lifecycle은 `VulkanRendererBackend.SwapchainResources.cs`에 둔다. `CreateSwapchain(...)`, surface capability/mode enumeration, desired image-count clamp/state, composite-alpha fallback, `SwapchainCreateInfoKHR` construction, swapchain handle 생성/state, swapchain image/view format/extent state, swapchain image retrieval, `CreateSwapchainResources(...)`, `RecreateSwapchain(...)`, `TryRecreateSwapchain(...)`, `DestroySwapchainDependentResources(...)`는 그 파일에서 유지한다. Resource creation, recreate flow, swapchain-dependent destruction을 selection policy나 main backend lifecycle method로 다시 합치지 않는다.

Device/backend lifecycle은 `VulkanRendererBackend.Lifecycle.cs`에 둔다. `Dispose()`와 `DestroyDeviceResources(...)`는 그 파일에서 유지해서 pipeline-cache 저장, swapchain-dependent destruction, device-level resource destruction, instance/surface/device handle cleanup이 하나의 teardown 경계에 남도록 한다. Device-level destruction을 `VulkanRendererBackend.cs`로 다시 inline하지 않는다.

Device resource setup은 `VulkanRendererBackend.DeviceResources.cs`에 둔다. Instance 생성/state, instance extension loading, surface 생성/state, physical-device 선택/state, graphics/present queue-family 선택/state, dedicated transfer candidate 탐지/state, logical device 생성/state, queue retrieval/state, device extension loading, device policy state, MSAA sample-count resolution은 그 파일에서 유지한다. Device/queue setup을 bootstrap constructor로 다시 키우지 말고, constructor는 초기화 순서를 보존하며 이 경계를 소비해야 한다.

Device/vendor renderer policy는 `VulkanRendererBackend.DevicePolicy.cs`에 둔다. GPU vendor classification, upload queue policy parsing/resolution, triangle color pipeline policy, solid unified pipeline policy, static primitive triangle policy, static geometry update policy, requested/resolved policy state, pipeline cache identity는 그 파일에서 유지한다. Diagnostics는 resolved policy를 보고할 수 있지만 renderer-policy env parsing을 소유하지 않는다.

Renderer bootstrap ownership은 `VulkanRendererBackend.Bootstrap.cs`에 둔다. Constructor initialization order, surface-source validation, texture id default, initial swapchain creation, selected-device policy resolution은 그 파일에서 유지한다. Device/queue creation detail은 `DeviceResources`, pipeline resource는 `PipelineResources`, settings mutation은 `Settings`, frame rendering은 `RenderEntry`에 남겨야 한다.

Renderer state declaration은 해당 lifecycle 또는 behavior를 이미 소유한 owner 파일 옆에 둔다. `VulkanRendererBackend.State.cs`는 좁은 owner가 없는 진짜 shared primitive 전용이며, 현재는 shared `Vk` API handle만 둔다. `VulkanRendererBackend.cs`는 `IRendererBackend` partial-class shell로만 남겨야 하며 field, shader blob, setup code, frame code를 다시 키우지 않는다.

Renderer settings/API ownership은 `VulkanRendererBackend.Settings.cs`에 둔다. Public device-object invalidation/recreation entry point, clear-color conversion, minimum image count, VSync, MSAA sample setting, settings-triggered swapchain recreation, settings-related environment parsing은 그 파일에서 유지한다. Runtime render-entry나 constructor code에 settings mutation branch를 다시 키우지 않는다.

Command pipeline selection은 `VulkanRendererBackend.CommandPipelineSelection.cs`에 둔다. Target vertex/index/primitive buffer 선택, solid-unified availability check, desired graphics pipeline 선택, solid-unified 사용 여부 판정은 그 파일에서 유지하고, command recording은 pipeline binding, buffer binding, draw-path branch에 `CommandPipelineSelection`을 사용해야 한다.

Command scheduling은 `VulkanRendererBackend.CommandScheduling.cs`에 둔다. Overlap-constrained scheduled-order lookup/cache, scheduling compatibility check, scheduling bounds merge, adjacent scheduled-command run merge expansion, command-iteration cursor/step selection은 그 파일에서 유지한다. Command recording은 `CommandIterationStep`을 소비해서 step의 command index와 next order index를 사용하고, merged-count event만 `CommandRecordProfileState`에 보고해야 한다.

Command scheduler policy state도 `VulkanRendererBackend.CommandScheduling.cs`에 둔다. `DUXEL_VK_COMMAND_SCHEDULER` parsing, `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`, resolved scheduler mode/window state, schedule cache ownership, scheduling algorithm은 함께 유지한다.

Command draw-path orchestration은 `VulkanRendererBackend.CommandDrawPath.cs`에 둔다. Triangle, expanded static primitive, primitive-instance draw-path branch orchestration과 pipeline-selection 소비, pipeline/push/descriptor binding 순서, geometry/primitive buffer binding 요구사항, primitive instance buffer validation, `CommandDrawDispatch` 호출은 그 파일에서 유지한다. `VulkanRendererBackend.CommandRecording.cs`는 command classification, diagnostic, texture resolution, scissor visibility를 준비한 뒤 inline draw-path branch를 직접 소유하지 말고 이 draw-path 경계를 호출해야 한다.

Command record profile state는 `VulkanRendererBackend.CommandRecordProfileState.cs`에 둔다. Command-record profile counter, draw-list static/dynamic count, scheduler hit/miss/timing count, scheduled merge count, transition counter, 최종 `CommandRecordStats` 구성은 그 파일에서 유지하고, command recording은 profile event를 이 경계로 보고해야 한다.

일반 command diagnostic state는 `VulkanRendererBackend.CommandDiagnosticState.cs`에 둔다. Command diagnostic frame gating, pass별 emission count, pipeline label 선택, `DUXEL_VK_COMMAND_DIAG` 로그 출력은 그 파일에서 유지한다.

Command font diagnostic state는 `VulkanRendererBackend.CommandFontDiagnosticState.cs`에 둔다. `DUXEL_VK_FONT_DIAG` 로그 출력, pass별 font diagnostic count, missing texture font diagnostic, normal font command diagnostic, index/vertex/UV bounds validation은 그 파일에서 유지하고, command recording과 texture lookup은 font-specific counter나 log formatting을 직접 소유하지 말고 이 경계를 호출해야 한다.

폰트 command 진단은 `DUXEL_VK_FONT_DIAG=1`로 켠다. 매우 자세한 command 로그는 `DUXEL_VK_FONT_DIAG_OUT`으로 파일에 기록할 수 있다.

일반 Vulkan command sequence 진단은 `DUXEL_VK_COMMAND_DIAG=1`로 켠다. `DUXEL_VK_COMMAND_DIAG_OUT`을 지정하면 draw command의 `pipe`, texture id, clip, static 여부를 파일에 기록해서 pipeline 전환 원인을 추적할 수 있다. 기본은 `DUXEL_VK_COMMAND_DIAG_EVERY=120`, `DUXEL_VK_COMMAND_DIAG_FRAMES=1`로 제한된다.

Draw-list command 병합에서 callback 없는 zero-element command는 draw-order barrier가 아니라 state placeholder로 취급한다. 실제 draw를 추가할 때 builder는 trailing empty placeholder를 제거한 뒤 contiguous command merge 가능성을 검사한다. 병합 조건에는 opacity, texture, clip, translation, kind, vertex offset, callback, user data를 계속 포함해야 한다. caller가 effective clip을 이미 알고 있으면 draw 하나를 위해 builder clip stack을 push/pop하지 말고 clipped `AddImage(...)` 같은 clipped draw helper를 우선 사용한다.

solid triangle을 sampler 없이 그리는 pipeline은 `DUXEL_VK_TRIANGLE_COLOR_PIPELINE=auto`가 기본이다. 자동 정책은 NVIDIA/AMD discrete GPU에서 켜고, 그 외 장치는 보수적으로 끈다. 장면별 A/B는 `1/true/on`으로 강제 활성화하거나 `0/false/off`로 강제 비활성화해서 수행한다.

solid triangle과 solid rect/circle primitive를 하나의 Vulkan pipeline에서 그리는 dynamic 경로는 `DUXEL_VK_SOLID_UNIFIED_PIPELINE=auto`가 기본이지만, 현재 자동 정책은 vendor/device gate에서 일관된 FPS 이득이 증명될 때까지 모든 장치에서 비활성으로 resolve한다. `1/true/on`으로 강제 활성화하거나 `0/false/off`로 강제 비활성화할 수 있다. Dynamic primitive buffer의 instance `0`은 solid triangle sentinel로 예약하고 실제 dynamic rect/circle primitive는 `firstInstance + 1`로 그려서 triangle과 primitive가 같은 binding `1` buffer를 유지한다. Static cached draw-list의 unified 실험은 `DUXEL_VK_SOLID_UNIFIED_STATIC=1`을 추가로 켠다. 이 경우 static primitive buffer도 instance `0`에 solid triangle sentinel을 예약한다. 확장하지 않은 static primitive instance는 base offset 뒤에 배치하고, triangle geometry로 확장된 static primitive는 sentinel을 binding해서 solid unified draw로 그릴 수 있다. static full unified는 command-state 모양은 좋지만 mixed scene FPS가 아직 약해서 기본 경로가 아니다.

텍스트 렌더러 A/B 프로파일링은 `DUXEL_TEXT_RENDERING=direct`, `atlas`, `auto`로 수행한다. 기본값은 `DirectText`다. 명시적 `atlas`는 atlas renderer 비교용이고, 명시적 `auto`는 atlas 우선 렌더링을 유지하되 atlas에 없는 글자만 DirectText로 즉시 시각 fallback하므로 그 fallback 동작이 의도된 경우에만 사용한다.

DirectText 페이지 텍스처 패킹은 `DUXEL_DIRECT_TEXT_PAGE=1`로만 켠다. DirectText bitmap을 1024x1024 page texture에 넣고 각 region 둘레에 1px border를 예약한 뒤 edge pixel을 border로 복제해서 atlas sampling bleed를 줄인다. Page 생성은 전체 1024x1024 pixel buffer가 아니라 border-inclusive packed region만 업로드하고, 같은 texture의 연속 non-overlapping partial update는 하나의 Vulkan upload/copy submission으로 batch될 수 있다. Upload batching이나 transfer-queue default를 바꾸기 전에는 `samples/fba/directtext_page_upload_bench_fba.cs`를 이 경로의 초점형 upload-policy gate로 사용한다. DirectText page quad는 `PushClipRect -> AddImage -> PopClipRect` sequence 대신 clipped image helper를 사용해야 한다. 기본값은 시각 품질 보존을 위해 off이며, 렌더된 글자 외형 검증 없이 기본 경로로 승격하지 않는다.

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
