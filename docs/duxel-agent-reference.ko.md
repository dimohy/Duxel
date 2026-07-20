# Duxel 에이전트 참조 문서

> Last synced: 2026-07-20
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

Duxel은 .NET 9 및 .NET 10 기반 즉시 모드 GUI 프레임워크다.

현재 구현 방향:

- 렌더러: Vulkan
- 주 플랫폼: Windows 네이티브 백엔드
- 런타임 스타일: NativeAOT 친화
- 주요 UI API: `UiImmediateContext`
- 앱 수명주기 진입점: `UiScreen.Render(UiImmediateContext ui)`
- 패키지 표면:
  - `Duxel.App`
  - `Duxel.Windows.App`

두 패키지는 `net9.0`과 `net10.0` 런타임 자산을 함께 제공한다. 파일 기반 앱 기능에는 .NET 10 SDK가 필요하므로 FBA 샘플은 `net10.0`을 유지한다.

`Duxel.Windows.App`은 Windows 단일 패키지 진입점이며 `Duxel.App`에 의존한다. 패키지 의존성은 analyzer asset 전파를 허용해 Windows 패키지 사용자도 통합 `Duxel.Core.Dsl.Generator`를 사용할 수 있어야 한다.

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
   - `docs/extended-title-bar-guide.ko.md`
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
- `global.json` — 저장소 build와 benchmark SDK를 .NET `10.0.301`로 고정하고 patch roll-forward만 허용
- `run-fba.ps1` — 로컬 소스 검증용 FBA 실행 경로

## 아키텍처 경계

기능을 추가하거나 수정할 때는 다음 경계를 유지한다.

- `Duxel.Core`
  - 즉시 모드 동작, 레이아웃, 위젯, 드로우리스트 생성, 상태, 텍스트 API 소유
- `Duxel.Platform.Windows`
  - 입력, 클립보드, IME/TSF 연결, 텍스트 백엔드 연결, 네이티브 윈도우 등 Windows 전용 동작 소유
- `Duxel.Vulkan`
  - 렌더링, GPU 리소스, 스왑체인, 제출 흐름 소유
- `Duxel.App`, `Duxel.Windows.App`
  - 앱 부트스트랩, 개발자 진입 흐름, 옵션 검증, 런타임 연결 소유

이 경계는 임의로 합치지 않는다.

### Windows 대화형 리사이즈 흐름

Win32 이동/크기 조절 모달 루프 동안 `Duxel.App`은 `IPlatformBackend.IsInteractingResize`를 일회성 무효화 메시지가 아니라 연속 렌더 작업으로 취급한다. `Duxel.Platform.Windows`는 `WM_SIZE`에서 원자적 클라이언트 크기 캐시를 갱신하고 `WM_SIZING` drag rectangle에서 다음 클라이언트 크기를 예측하므로, 렌더 스레드는 교차 스레드 `GetClientRect` 호출 없이 최신 치수를 소비한다.

각 `WM_SIZING` 요청에는 단조 증가 sequence가 있다. Window procedure는 프레임을 요청하고, 해당 sequence에서 캡처한 draw data에 대해 `IRendererBackend.TryRenderDrawData(...)`가 성공/suboptimal present를 보고할 때까지 Windows가 그 외곽 창 단계를 확정하지 못하게 한다. Renderer 실패는 대기를 명시적으로 취소한다. 따라서 포인터/창 이동은 렌더보다 빨라질 수 없지만 외곽 프레임이 내부 내용보다 눈에 띄게 앞설 수도 없다.

모달 루프 동안 draw-data와 swapchain extent가 달라도 매 pointer sample마다 `vkDeviceWaitIdle` 재생성을 선제 실행하지 않는다. 최신 draw data를 현재 swapchain viewport 전체에 그리고 clip을 비례 조정한다. WSI `OUT_OF_DATE`/surface-loss 결과는 계속 재생성하고, 대화형 상태가 끝난 첫 프레임은 정확한 최종 extent로 재생성한다. 리사이즈 전용 recreate는 이전 핸들을 `VkSwapchainCreateInfoKHR.oldSwapchain`으로 전달하고 non-swapchain renderer resource를 보존한다. VSync 생성은 지원될 때 tearing 없는 Mailbox를 우선하고, 아니면 필수 FIFO를 사용한다. VSync off는 Immediate를 우선한다. 고정 240 FPS 제한을 추가하지 않는다.

## 주요 공개 진입점

다운스트림 개발자가 가장 많이 만지는 표면:

- `UiScreen`
- `UiImmediateContext`
- 선언형 UI API:
  - `IUiView`
  - `DuxelView`
  - `Dux`
  - `DuxelView.Display`
- `DuxelAppOptions`
- `DuxelWindowOptions`
- `DuxelRendererOptions`
- `DuxelFontOptions`
- `DuxelFrameOptions`
- `DuxelDebugOptions`
- 컴파일된 디자인 API:
  - `UiCompiledDesign`
  - `IUiDesign`
  - `UiWindows11Design`
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
| `Theme` | `UiTheme` | `UiCompiledDesign.Default.Theme` | fallback 테마 프리셋 |
| `Design` | `UiCompiledDesign?` | `null` | 선택적 컴파일 디자인. 지정하면 플랫폼 기본 테마 추적보다 우선 |
| `FontTextureId` | `UiTextureId` | `new(1)` | 폰트 텍스처 슬롯 |
| `WhiteTextureId` | `UiTextureId` | `new(2)` | 화이트 텍스처 슬롯 |
| `Screen` | `UiScreen` | (필수) | 즉시 모드 앱 진입점 |
| `Clipboard` | `IUiClipboard?` | `null` | 직접 클립보드 주입 |
| `ImageDecoder` | `IUiImageDecoder?` | `null` | 커스텀 이미지 디코드 경로 |
| `KeyRepeatSettingsProvider` | `IKeyRepeatSettingsProvider?` | `null` | 커스텀 키 반복 타이밍 |
| `ClipboardFactory` | `Func<IPlatformBackend, IUiClipboard?>?` | `null` | 플랫폼별 클립보드 팩토리 |

### 컴파일된 디자인

기본적으로 Windows 앱은 현재 OS 앱 테마에 따라 `UiCompiledDesign.Windows11` 또는 `UiCompiledDesign.Windows11Dark`로 해석되며, Windows가 앱 테마 변경을 알리면 활성 디자인도 갱신된다. 컨트롤의 시각적 모양을 런타임 테마 파싱이 아니라 코드 또는 소스 생성으로 고정해야 할 때는 `DuxelWindowsApp.Run<TDesign>(...)`, `DuxelApp.Options<TDesign>(...)` 또는 `DuxelAppOptions.Design`을 사용한다.

```csharp
DuxelWindowsApp.Run<UiWindows11Design>(
    new ProductScreen(),
    title: "Windows 11 styled Duxel",
    width: 980,
    height: 700);
```

커스텀 컴파일 타임 디자인은 `IUiDesign`을 구현하고 생성된/정적 값을 전달한다.

```csharp
public readonly struct ProductDesign : IUiDesign
{
    public static UiCompiledDesign Create()
        => UiCompiledDesign.Windows11 with
        {
            Theme = UiTheme.GitHubDark,
            Tokens = UiDesignTokens.Windows11 with { ControlCornerRadius = 6f }
        };
}

DuxelWindowsApp.Run<ProductDesign>(
    Dux.App(new ProductScreen()),
    title: "Product Surface");
```

`UiTheme`은 색상 팔레트, `UiStyle`은 레이아웃 치수, `UiDesignTokens`는 코너 반경, 보더 두께, 눌림 오프셋, 포커스 링 두께 같은 위젯 모양을 담당한다. 기존 런타임 테마 변경은 색상만 갱신하고, 컴파일된 디자인 토큰은 활성 외형 계약으로 유지된다.

`Design`이 `null`이고 `Theme`을 기본값 그대로 두면 Duxel은 플랫폼 테마 공급자를 따른다. 명시적 `Theme` 또는 `Design` 값은 앱이 직접 선택한 값으로 보고 OS 테마 변경으로 덮어쓰지 않는다.

다음 단계 숙제는 컨트롤 타입별 렌더링 전략을 교체하는 skin layer다. 현재는 테마, 토큰, modifier, 커스텀 위젯으로 상당 부분 조정할 수 있지만, `Button`, `TextField`, `Segmented`, `Scrollbar` 같은 기본 컨트롤의 렌더링 정책 자체를 디자인 단위로 갈아끼우는 공식 계층은 아직 남아 있다. 다음 세션은 [Declarative Control Skin Roadmap](declarative-control-skin-roadmap.ko.md)에서 시작한다.

### 선언형 UI

C#에서 SwiftUI/Compose 스타일 조합을 사용할 때는 그룹화된 `DuxelView` 팩터리로 생성한 `IUiView` 노드를 사용한다. 점 자동완성에서 `DuxelView.Layout`, `DuxelView.Controls`, `DuxelView.Text`, `DuxelView.Display`, `DuxelView.Menus`, `DuxelView.Windows`가 먼저 드러나므로 모든 헬퍼를 한 클래스에 몰아넣지 않고도 자연스럽게 화면을 작성할 수 있다. 짧은 앱 코드와 샘플에는 같은 표면을 제공하는 `Dux.*` 별칭을 사용할 수 있다.

```csharp
var running = Dux.State(true);
var project = Dux.State("Duxel Control Surface");
var progress = Dux.State(0.62f);
var channel = Dux.State(ReleaseChannel.Preview);
var tabItems = new[] { "Layout", "Theme", "Windows" };

var screen = Dux.Screen(
    Dux.Group(
        Dux.MainMenuBar(
            Dux.Menu(
                "File",
                Dux.MenuItem("Reset", () => progress.Value = 0f),
                Dux.MenuItem("Running", () => running.Value = !running.Value, selected: () => running.Value))),
        Dux.Window(
            "Dashboard",
            Dux.VStack(
                10f,
                Dux.Header(
                    () => project.Value,
                    Dux.Meta("Preview"),
                    Dux.Meta(() => running.Value ? "Running" : "Paused", UiTextTone.Success)),
                Dux.Tabs(
                    "dashboard-tabs",
                    Dux.Tab(
                        "Overview",
                        Dux.Section(
                            "Controls",
                            Dux.Form(
                                Dux.Field("Project", Dux.TextField("project", project)),
                                Dux.Field("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)),
                                Dux.Field("Running", Dux.Toggle("running", running)),
                                Dux.Field("Progress", Dux.Slider("progress", progress, 0f, 1f).ItemWidth(360f))))),
                    Dux.Tab(
                        "Tasks",
                        Dux.List(
                            "task-list",
                            tabItems,
                            item => Dux.Text(item),
                            new UiVector2(0f, 120f),
                            border: true)))),
            new UiWindowOptions(
                Position: new UiVector2(24, 24),
                Size: new UiVector2(420, 260)))));

enum ReleaseChannel
{
    Stable,
    Preview,
    Canary
}
```

`UiState<T>`는 앱 코드용 상태 홀더다. 컨트롤에는 `UiBinding<T>`로 변환되므로 `Dux.Checkbox("Running", running)`, `Dux.TextField("project", project)`처럼 짧게 쓸 수 있다. 선언형 alias인 `Dux.TextField(...)`, `Dux.TextArea(...)`, `Dux.NumberField(...)`, `Dux.Slider(...)`는 안정적인 control id를 받고 즉시 모드 `##` label을 앱 코드에서 숨긴다. 명시적인 상태 변경은 `state.Set(value)`와 `state.Update(current => next)`를 사용한다. 상태가 이미 다른 곳에 있으면 `Dux.Bind(get, set)`을 사용하고, 쓰기 가능한 파생 바인딩은 `UiBinding<T>.Map(...)`으로 만든다. 동적 텍스트와 조건부 view는 `Func<T>`를 받아 렌더링 중 값을 다시 평가한다. 재사용 가능한 view 객체는 `Build()`를 구현하는 `UiComponent`를 기준으로 만든다.

앱 규모 화면에서는 하나의 큰 렌더 메서드보다 작은 `UiComponent` 클래스를 선호한다. 컴포넌트는 생성자로 상태를 받거나 보유하고, `Build()`에서 `IUiView`를 반환한다. 권장 시작 형태는 `DuxelWindowsApp.Run<ProductDesign>(Dux.App(new ProductScreen(...)))`이며, 첫 프레임 전에 컴파일된 디자인과 루트 컴포넌트를 함께 묶는다. `Dux.App(...)`은 선언형 root view를 런타임이 요구하는 `UiScreen`으로 감싸는 semantic alias다.

공통 제품 화면 골격에는 `Dux.AppShell(...)`을 사용한다. AppShell은 선택적인 메뉴, 테마가 적용된 sidebar, 동적 header, meta 텍스트, command 콘텐츠, 선택된 page body를 한 번에 선언한다. 내비게이션 항목은 `Dux.NavItem(...)`으로 만든다. 활성 page는 일반적인 `UiState<int>`/`UiBinding<int>`로도 제어할 수 있지만, enum 기반 `UiState<T>`를 쓰면 앱 내비게이션이 타입 안전해진다.

```csharp
Dux.AppShell(
    () => projectName.Value,
    selectedPage,
    [
        Dux.NavItem(ProjectPage.Overview, "Overview", new OverviewPage(state), "Run controls"),
        Dux.NavItem(ProjectPage.Tasks, "Tasks", new TasksPage(state), () => $"{tasks.Count} active rows"),
        Dux.NavItem(ProjectPage.Notes, "Notes", new NotesPage(state), "Composition notes")
    ],
    new UiAppShellOptions(WindowTitle: "Workspace", SidebarTitle: "Workspace"),
    Dux.Meta(() => $"Runs {runs.Value}"),
    Dux.Meta(() => channel.Value));
```

고수준 레이아웃 헬퍼는 사용자가 화면에서 보는 제품 구조 그대로 작성하게 해준다. 제목 있는 영역은 `Dux.Section(...)`, 라벨이 붙은 설정은 `Dux.Form(...)`과 `Dux.Field(...)`, 반복 카드/메트릭 배치는 `Dux.Grid(...)`, 반복 행은 `Dux.List(...)`, 탭 흐름은 `Dux.Tabs(...)` / `Dux.Tab(...)`, 계층 콘텐츠는 `Dux.Tree(...)`, 구조화 데이터는 `Dux.Table(...)` / `Dux.TableColumn(...)`을 사용한다. 동적 목록과 grid에는 `Dux.List("tasks", tasks, task => task.Id, task => Dux.StatusRow(task.Name, task.Owner, () => task.Progress))` 또는 `Dux.Grid(tasks, task => task.Id, task => new TaskTile(task))` 같은 keyed overload를 우선 사용한다. 개별 view fragment에는 `Dux.Key(...)`와 `.Key(...)`로 같은 안정적인 ID 스코프를 줄 수 있다.

선언형 제어 흐름은 view tree 안에 유지한다. 한쪽 조건부 콘텐츠에는 `Dux.When(...)`과 `Dux.Unless(...)`, 상태별 fragment에는 `Dux.Switch(...)`와 `Dux.Case(...)`, 표시 행에 인덱스가 필요한 반복에는 `Dux.ForEachIndexed(...)`를 사용한다. 이 헬퍼들은 동적 값을 렌더링 중 평가하므로 앱 코드는 상태 변경을 `UiState<T>`에 두고, view는 현재 화면을 설명하는 형태로 유지할 수 있다.

디스플레이 헬퍼는 제품 UI에서 자주 쓰는 조각을 짧게 만든다. sidebar 기반 제품 shell에는 `Dux.AppShell(...)`, 제품 제목과 meta 행에는 `Dux.Header(...)`, padding이 있는 테마 패널에는 `Dux.Surface(...)` 또는 `Dux.Card(...)`, 라벨이 붙은 설정에는 `Dux.SettingsGroup(...)`과 `Dux.Setting(...)`, 의미 있는 상태/안내 메시지에는 `Dux.Callout(...)`, 비어 있거나 비활성화된 상태에는 `Dux.EmptyState(...)`, 조밀한 명령 행에는 `Dux.Toolbar(...)`, 조밀한 상태 행에는 `Dux.MetaBar(...)`와 `Dux.Meta(...)`, 세부 key/value 목록에는 `Dux.PropertyList(...)`와 `Dux.Property(...)`, 선택 가능한 progress 행에는 `Dux.StatusRow(...)`, 의미가 있는 상태 pill에는 `Dux.Badge(...)`와 `UiBadgeTone.Neutral`, `Accent`, `Success`, `Warning`, `Danger`, 대시보드 수치에는 `Dux.MetricCard(...)` 또는 `Dux.Metric(...)`을 사용한다. 명령의 제품 action hierarchy는 버튼 색상을 직접 push하지 않고 `Dux.PrimaryButton(...)`, `Dux.DangerButton(...)`, `.ButtonRole(...)`로 표현한다. Surface와 child 콘텐츠는 기본 Duxel 스크롤바 동작을 그대로 유지한다. 그룹화된 팩터리 표면은 `DuxelView.Display`다.

```csharp
Dux.Grid(
    3,
    Dux.MetricCard("Runs", () => runs.Value.ToString(), "Queued sessions"),
    Dux.MetricCard("Progress", () => $"{progress.Value:P0}", "Current workflow"),
    Dux.MetricCard(
        "Priority",
        () => priority.Value.ToString(),
        () => priority.Value >= 4 ? "Needs attention" : "Normal cadence",
        new UiMetricCardOptions(ValueTone: UiTextTone.Warning)));
```

제품 명령은 action role, tooltip, enabled 상태가 중요하면 `Dux.CommandBar(...)`를 우선 사용한다. 명령 의미를 값으로 유지하고 버튼의 실제 스타일은 컴파일된 design이 담당하게 한다.

```csharp
Dux.CommandBar(
    Dux.Command("Queue", QueueRun, UiButtonRole.Primary, tooltip: () => "Queue one run"),
    Dux.Command("Reset", Reset, enabled: () => canReset.Value));
```

설정 화면은 `Dux.SettingsGroup(...)`을 사용하면 각 행이 label, description, control을 함께 소유한다.

```csharp
Dux.SettingsGroup(
    Dux.Setting("Project", Dux.TextField("project", project), "Shown in the workspace shell."),
    Dux.Setting("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)));
```

선택 컨트롤은 `Dux.Combo(...)`, `Dux.ListBox(...)`, `Dux.RadioButton(...)`, `Dux.Selectable(...)`뿐 아니라 `UiBinding<int>` 또는 typed `UiBinding<T>`로 연결되는 조밀한 모드/채널 picker용 `Dux.Segmented(...)`를 제공한다. typed 선택지는 `Dux.Choice(value, label)`로 만들고, 상태가 enum이면 `Dux.EnumSegmented<TEnum>(...)`으로 바로 연결한다.

`UiModifier`와 fluent extension은 지역적인 컴파일 타임 작성 시각 규칙을 제공한다. 자주 쓰는 modifier는 `.FontSize(...)`, `.Foreground(...)`, `.Tone(...)`, `.Accent()`, `.Success()`, `.Warning()`, `.Danger()`, `.Muted()`, `.Title(...)`, `.Subtitle(...)`, `.Caption(...)`, `.ItemWidth(...)`, `.FillWidth()`, `.Padding(...)`, `.Frame(...)`, `.FillFrameWidth(...)`, `.Width(...)`, `.Height(...)`, `.Background(...)`, `.Border(...)`, `.CornerRadius(...)`, `.Tooltip(...)`, `.VisibleIf(...)`, `.Disabled(...)`, `.StyleColor(...)`, `.StyleVar(...)` 등이다. 텍스트 계층은 `Dux.Title(...)`, `Dux.Subtitle(...)`, `Dux.Caption(...)` 또는 재사용 가능한 `UiTextStyle` 값으로 작성할 수 있다. semantic 텍스트 색조는 `UiTextTone`을 사용하므로 앱 코드는 색상 상수를 들고 다니지 않고 `.Success()`나 `.Warning()`처럼 작성할 수 있다. view가 런타임 theme slot에만 의존하지 않고 자기 렌더링 모양을 직접 가져야 할 때는 shape modifier를 사용한다.

```csharp
Dux.Callout(
    "Run Status",
    () => status.Value,
    options: new UiCalloutOptions(Tone: UiTextTone.Success, Height: 88f));
```

`.Panel(...)`은 기본 제공 Windows 11 스타일 surface modifier다. 내부적으로 `UiPanelStyle`을 사용하며 기본값은 `UiStyleColor.FrameBg` / `UiStyleColor.Border`, `UiDesignToken.ControlCornerRadius`, 14 px padding, 현재 콘텐츠 폭 채우기다. fluent modifier보다 factory 호출이 더 잘 읽히면 `Dux.Panel(content, ...)`을 사용한다. 재사용 가능한 컴파일된 스타일은 일반 C# 타입이다. 같은 시각 규칙을 SwiftUI의 `ViewModifier`처럼 이름 붙이고 테스트하고 재사용하려면 `IUiViewStyle`을 구현한다. 스타일은 `UiStyleColor`와 `UiDesignToken` 슬롯을 사용할 수 있으므로, 모양은 C#에서 작성하고 색상/반경은 활성 컴파일 디자인에서 가져올 수 있다.

```csharp
readonly record struct DashboardPanelStyle(float Height = 0f) : IUiViewStyle
{
    public IUiView Apply(IUiView view)
        => view
            .Padding(14f)
            .Background(UiStyleColor.FrameBg)
            .Border(UiStyleColor.Border)
            .CornerRadius(UiDesignToken.ControlCornerRadius)
            .FillFrameWidth(Height);
}

Dux.TextWrapped(() => status.Value)
    .Style(new DashboardPanelStyle(116f));
```

`Frame`은 안정적인 레이아웃 크기를 제공하고, `FillFrameWidth`는 현재 콘텐츠 폭을 채우며, `Background`, `Border`, `CornerRadius`는 컴파일 타임에 작성된 view shape를 설명한다. `Background`와 `Border`는 명시적 `UiColor` 값 또는 활성 디자인의 semantic `UiStyleColor` 슬롯을 받을 수 있다. `CornerRadius`는 고정 float 또는 semantic `UiDesignToken`을 받을 수 있다. shape modifier 앞에 놓인 `Padding`은 장식된 view의 내부 padding으로 흡수되므로 왼쪽과 오른쪽 padding이 모두 shape 측정에 참여한다. `IUiViewStyle`은 같은 modifier들을 컴파일 타임에 작성된 스타일 타입으로 합성한다. `.Style<TStyle>()`은 기본 struct style을 적용하고, `.Style(style)`은 설정된 style 인스턴스를 적용한다. 반복 동적 콘텐츠에서는 keyed list/grid overload를 계속 사용하거나 장식된 fragment에 `.Key(...)`를 붙여 local interaction state를 안정적으로 유지한다. 선언형 메뉴 조합은 `Dux.MainMenuBar(...)`, `Dux.Menu(...)`, `Dux.MenuItem(...)`로 작성한다. 직접 `UiImmediateContext` 접근이 필요한 커스텀 선언형 노드는 `DuxelView.Custom(ui => ...)`를 확장 지점으로 사용한다.

### `DuxelWindowOptions`

| 속성 | 기본값 |
|---|---|
| `Width` | `1280` |
| `Height` | `720` |
| `Title` | `"Duxel"` |
| `VSync` | `true` |
| `IconPath` | Duxel 기본 아이콘 |
| `IntegrateSystemChrome` | `true` |
| `TitleBarMode` | `DuxelTitleBarMode.Default` |
| `UseDuxelTitleBar` | `true` |
| `DuxelTitleBarHeight` | `48f` |

`IntegrateSystemChrome`은 활성 시작 테마/디자인에서 Windows 11 DWM 캡션 색상, 텍스트 색상, 테두리 색상, 라운드 코너, 다크 모드 속성을 적용한다. 앱이 기본 플랫폼 추적 디자인을 사용할 때는 Windows `WM_SETTINGCHANGE` 테마 알림으로 Duxel 테마와 렌더러 clear color가 런타임에 갱신된다.

`UseDuxelTitleBar`는 기본적으로 켜져 있으므로, 앱이 명시적으로 `UseDuxelTitleBar = false`를 지정하지 않는 한 Windows 앱은 네이티브 캡션을 제거하고 Vulkan surface 안에 Duxel 소유 타이틀바를 렌더링한다. 앱 런타임은 사용자 `UiScreen`을 감싸고, 상단 viewport inset을 예약하며, 앱 아이콘/제목/최소화/최대화/닫기 버튼을 그리고, 창 이동/최소화/최대화/닫기 명령은 `IWindowChromeController`를 통해 플랫폼에 위임한다.

`TitleBarMode`는 `System`, `Duxel`, `ExtendedContent`를 명시적으로 선택한다. 기본값 `Default`는 기존 소스 호환성을 위해 `UseDuxelTitleBar`에서 실제 모드를 결정하며, 명시한 비기본 모드가 우선한다. `ExtendedContent`는 Windows/DWM 캡션 버튼과 시스템 메뉴, 리사이즈, 최대화, Snap Layout 계약을 유지하면서 전체 Vulkan 클라이언트 영역을 `(0, 0)`부터 사용할 수 있게 한다.

`ExtendedContent`에서는 DWM 소유 버튼 묶음의 경계를 조회하고 현재 UI 프레임에서 플랫폼 드래그 영역 스냅샷을 교체한다.

```csharp
if (ui.TryGetCaptionButtonBounds(out var captionButtons))
{
    var dragLeft = 320f;
    var dragRight = captionButtons.X;
    ui.SetTitleBarDragRegions(dragRight > dragLeft
        ? [new UiRect(dragLeft, 0f, dragRight - dragLeft, 48f)]
        : []);
}
else
{
    ui.SetTitleBarDragRegions([]);
}
```

사각형은 Duxel 논리 클라이언트 좌표를 사용한다. 호출할 때마다 이전 영역 집합을 원자적으로 교체하며, `[]`를 전달하면 비운다. 탭, 버튼, 메뉴, 텍스트 입력 등 상호작용 요소는 해당 사각형 밖에 두어 히트 테스트가 `HTCLIENT`로 남게 한다. Windows는 숨겨지거나 최소화된 창의 DWM 경계를 유효하지 않은 값으로 정의하므로 이 상태에서는 `TryGetCaptionButtonBounds`가 `false`를 반환할 수 있다. 전체 레이아웃, Win32/DWM 계약, AI 검증 절차는 `docs/extended-title-bar-guide.ko.md`, 실행 가능한 기준 샘플은 `samples/fba/extended_title_bar_fba.cs`를 참고한다.

`IconPath`와 `IconData`를 지정하지 않으면 Duxel은 번들된 기본 `.ico`를 Win32 창/작업표시줄 아이콘으로 사용한다. `Duxel.Windows.App` 패키지도 같은 아이콘을 Windows 실행 파일의 기본 `ApplicationIcon`으로 제공하며, 앱이 자체 아이콘을 지정하거나 `DuxelUseDefaultIcon=false`를 설정하면 이를 사용하지 않는다.

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

내부 창 제목도 컨트롤과 같은 label/ID 규칙을 따른다. `표시 제목##stable-id`는 화면에 `표시 제목`만 그리되 전체 문자열은 창 상태 식별자로 유지한다.

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
| 선언형 C# 대시보드 | `samples/fba/declarative_dashboard_fba.cs` |
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
| DirectText 변경 문자열 성능 | `samples/fba/directtext_dynamic_text_bench_fba.cs` |
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

특정 토글에 의존하는 작업은 문서화하거나 사용하기 전에 관련 샘플에서 이름과 의미를 다시 확인한다.

Vulkan 커맨드 기록 프로파일링은 `DUXEL_VK_PROFILE=1`로 켠다. `DUXEL_VK_PROFILE_EVERY`는 로그 간격 프레임 수이고, `DUXEL_VK_PROFILE_OUT`은 프로파일 라인을 파일에 append한다. 프로파일 라인의 `device(vendor=... vid=... did=... type=... name=... gfxQ=... uploadQ=... xferCandQ=... tsBits=... tsPeriodNs=... gpuTs=...)`와 `policy(upload=... transferCandidate=... staticPrimTri=... staticUpdate=... staticUpdateReq=... scheduler=... schedWindow=... staticSecondaryMin=...)`는 나중에 NVIDIA, AMD, Intel, integrated/discrete 장치의 artifact를 비교할 때 어떤 장치와 자동 정책에서 나온 결과인지 증명한다. `uploadQ`는 upload command buffer가 실제로 사용하는 queue family이고, `xferCandQ`는 탐지된 non-graphics transfer-capable 후보일 뿐이다. 따라서 `policy(upload=graphics transferCandidate=1)`은 후보가 있다는 뜻이지 upload가 이미 transfer queue를 사용한다는 뜻이 아니다. `DUXEL_VK_UPLOAD_QUEUE=transfer`는 후보가 있을 때만 split transfer upload path를 opt-in으로 켠다. `policy(upload=transfer transferCandidate=1)`은 upload copy command buffer가 그 queue를 실제로 사용한다는 profile 증거다. `staticUpdateReq`는 요청된 mode이고 `staticUpdate`는 장치 정책으로 resolve된 mode다. `scheduler`는 resolved command-scheduler mode이며 `disabled`, `static`, `all` 중 하나다. `stateUs(pipe=... desc=... buf=... push=... scissor=...)`는 pipeline bind, descriptor bind, vertex/index/primitive buffer bind, push constant, scissor set 비용을 분리해서 보여준다. `clipCache(calc=... reuse=...)`는 실제 scissor rectangle 계산 횟수와 연속으로 같은 clip/translation을 재사용한 횟수를 기록한다. `staticSec(cand=... cmds=... draws=...)`는 현재 `staticSecondaryMin` threshold를 넘긴 static draw list 수와 그 후보 안의 기록된 command/draw 수를 기록한다. `cand=0`이면 static list가 존재해도 그 frame에서는 secondary command buffer가 다음 경로 후보라는 가설이 약하다는 뜻이다. `listWork(staticCmd=... dynCmd=... staticDraw=... dynDraw=... staticPipe=... dynPipe=... staticClip=... dynClip=... staticScissor=... dynScissor=... staticPush=... dynPush=... staticGeom=... dynGeom=... staticPrim=... dynPrim=...)`는 command-recording 작업을 static cached replay와 dynamic draw list로 나눠 기록한다. 다음 병목이 static replay policy인지 dynamic UI ordering/channelization인지 판단할 때 이 값을 우선 확인한다. `staticGeom(hit=... create=... replace=... update=... reuse=... hash=...)`는 upload phase에서 static geometry cache hit, buffer creation, content replacement, 같은 shape의 in-place content update, 같은 shape의 rotating-buffer reuse, fallback content hashing을 기록한다. `staticMem(active=... activeBytes=... retired=... retiredBytes=...)`는 memory-pressure 확인을 위해 active static geometry entry/bytes와 retired rotating-pool entry/bytes를 기록한다. `staticPrim(expand=... expandPrim=... layout=... force=... autoSkip=... autoSkipPrim=... autoSkipMut=... expandBytes=... autoSkipBytes=...)`는 frame별 static primitive triangle expansion 결정을 기록한다. `policy(... staticPrimTri=1 ...)`은 장치 정책상 경로가 허용됐다는 뜻일 뿐이므로, 실제 draw-list가 확장됐는지는 이 카운터로 확인한다. `autoSkipMut`은 같은 static geometry tag의 content가 바뀌는 중이라 확장을 억제한 skip subset이다. `pipeClass(font=... texTri=... colorTri=... texPrim=... colorPrim=... solid=...)`는 renderer policy 적용 뒤 실제로 선택된 pipeline class를 기록하므로 원본 source command kind만 보는 것보다 pipeline switch 분석에 더 정확하다. `sched(probe=... hit=... miss=... nochange=... lists=... merged=... us=...)`는 opt-in command scheduler의 동작과 비용을 기록한다. `upSched(sub=... prepSub=... wait=... flush=... bytes=... texRegions=... bufCopies=... submitUs=... prepUs=... waitUs=...)`는 staging upload scheduler submission, split-transfer graphics-prepare submission, upload fence wait, batch flush, staging byte volume, texture copy region, static/buffer copy command, 그리고 submission/wait 비용을 기록한다. `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)`는 image layout transition 횟수와 transfer-queue-compatible stage mask / graphics-stage-required barrier를 분리해서 기록한다. `state=`는 scissor를 제외한 state command 시간이고, scissor는 `clip=`에도 포함되므로 별도로 해석한다.

성능 데모는 확인하려는 병목에 맞아야 한다. 넓은 layer-widget scene은 최종 regression 용도로 사용하되, 질문이 좁으면 초점형 FBA 데모를 고르거나 추가한다. `samples/fba/pipeline_ordering_bench_fba.cs`는 dynamic solid/text pipeline ordering 비용을 확인하는 초점형 gate이며 alternating, grouped solids-then-text, copy-merge `channelized-solid-text`, copy-free `channel-drawlists-solid-text` phase를 포함하고 `DUXEL_PIPELINE_ORDER_BENCH_OUT`, `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`, `DUXEL_PIPELINE_ORDER_ITEMS`를 사용한다. `samples/fba/dynamic_widget_ordering_bench_fba.cs`는 widget-like dynamic producer ordering과 row-clip churn을 확인하는 초점형 gate이며 alternating widget row, grouped solids-then-text, copy-merge channelized phase, copy-free channel-drawlists phase를 포함하고 `DUXEL_DYN_WIDGET_ORDER_BENCH_OUT`, `DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_ITEMS`, `DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS`를 사용한다. `samples/fba/vector_primitives_bench_fba.cs`는 primitive-heavy geometry를 확인하는 초점형 gate이며 `DUXEL_VECTOR_BENCH_WORKLOAD=mixed`, `rect-outline`, `axis-line`과 `DUXEL_VECTOR_BENCH_OUT`, `DUXEL_VECTOR_BENCH_PHASE_SECONDS`, `DUXEL_VECTOR_BENCH_COUNTS`를 사용한다. `samples/fba/Duxel_perf_test_fba.cs`는 polygon physics/perf smoke이며 기본은 Render profile과 global static backdrop cache다. `DUXEL_PERF_PROFILE=render|display`로 시작 profile을 override할 수 있고, `DUXEL_PERF_GLOBAL_STATIC_CACHE=0`은 retained static backdrop reference를 끈다. UI의 Render Profile checkbox는 재시작 없이 MSAA를 `1x`/`4x`로 바꿔 profile을 즉시 적용한다. `samples/fba/global_dirty_strategy_bench.cs`는 global static background cache와 dynamic overlay update를 비교하는 초점형 gate이며, `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS`로 그 배경/오버레이 channel 구조에서 copy-merge와 별도 draw-list 출력을 비교한다. `samples/fba/static_layer_moving_order_bench_fba.cs`는 moving static-layer replay schedule 재사용을 확인하는 초점형 gate이며 `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`, `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`를 사용한다. `samples/fba/static_cache_rebuild_bench_fba.cs`는 cache replay, false-dirty static rebuild, mutating static geometry replacement/update 비용, Core cache-copy allocation pressure, static primitive triangle memory pressure를 분리하는 초점형 gate이며 측정 frame별 `avgAllocatedBytes`도 보고한다. 이 gate는 `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`, `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_LAYERS`, `DUXEL_STATIC_CACHE_REBUILD_DENSITY`, `DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE`(`circles`, `rects`, `mixed`), `DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS`, GPU-bound variant용 선택 토글 `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW`를 사용한다. `samples/fba/texture_upload_barrier_bench_fba.cs`는 transfer-queue policy 변경 전 texture upload copy/barrier 동작을 분리하는 초점형 gate이며 `DUXEL_TEXTURE_UPLOAD_BENCH_OUT`, `DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS`, `DUXEL_TEXTURE_UPLOAD_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGION_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGIONS`, `DUXEL_TEXTURE_UPLOAD_TEXTURES`, `DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES`를 사용한다. `samples/fba/directtext_page_upload_bench_fba.cs`는 platform glyph rasterizer 비용을 섞지 않고 DirectText page-style partial texture upload 동작을 확인하는 초점형 gate이며 `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`, `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`, `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`, `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`, `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`, `DUXEL_DTPAGE_UPLOAD_REGIONS`, `DUXEL_DTPAGE_UPLOAD_PAGES`, `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`를 사용한다.

`BenchFrameRecorder`는 초점형 gate가 공유하는 raw-frame 누적기다. 통계에는 sample 수, 측정 시간, 평균 FPS, median/p95/p99 frame time, 1% low FPS가 포함된다. `layer_widget_mix_bench_fba.cs`는 이 필드를 JSON schema version 2로 기록하므로 consumer는 tail field가 있다고 가정하기 전에 `schemaVersion`을 확인해야 한다. Layer-widget과 DirectText gate는 sample slot `1,048,576`개를 할당하고 phase duration을 30초로 제한해서, 약 `10.7k` FPS로 관측된 DirectText Atlas 대조군을 포함한 유효 장기 실행이 이전 `262,144` sample 한도를 넘지 않게 한다.

`samples/fba/directtext_dynamic_text_bench_fba.cs`는 안정 DirectText cache hit와 변경 문자열 cache miss를 비교하는 초점형 gate다. `DUXEL_DIRECTTEXT_BENCH_OUT`은 필수이며 제어값은 `DUXEL_DIRECTTEXT_BENCH_PHASE_SECONDS`, `DUXEL_DIRECTTEXT_BENCH_WARMUP_FRAMES`, `DUXEL_DIRECTTEXT_BENCH_ROWS`, `DUXEL_DIRECTTEXT_BENCH_CORPUS_FRAMES`다. JSON에는 frame과 text-work의 median/p95/p99 tail, 1% low FPS, frame당 평균 할당량, generation별 collection 횟수가 포함된다.

축 정렬 `UiDrawListBuilder.AddRect(...)` 외곽선은 `rounding <= 0`일 때 triangle polyline geometry가 아니라 rect-filled primitive emission을 사용해야 한다. 수평/수직 `UiDrawListBuilder.AddLine(...)`도 rect-filled primitive emission을 사용하고, diagonal line은 기존 quad triangle path를 유지한다. 이렇게 하면 일반 사각형 border와 축 정렬 separator가 primitive path에 남아 text/triangle/primitive pipeline churn이 줄어든다. Rounded outline은 기존 polyline path를 유지한다.

`DUXEL_VK_STATIC_GEOMETRY_UPDATE`는 같은 shape의 static geometry content change 정책을 제어한다. 유효한 값은 `auto`, `replace`, `inplace`, `rotating`이다. `auto`는 검증된 NVIDIA discrete GPU에서는 `rotating`으로, AMD/Intel과 그 외 장치에서는 같은 정책을 증명하는 gate가 쌓일 때까지 `replace`로 resolve된다. `replace`는 allocation/replacement를 강제하고, `inplace`는 fence-waited existing-buffer reupload를 강제하며, `rotating`은 retired-buffer reuse를 강제한다.

`DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE=1`은 `DUXEL_VK_STATIC_GEOMETRY_UPDATE=inplace`의 backward-compatible explicit override다. static geometry cache entry의 vertex/index/primitive count와 expanded primitive triangle layout이 같으면 Vulkan은 모든 in-flight frame fence를 기다린 뒤 새 replacement buffer를 만들지 않고 기존 device-local buffer에 다시 업로드한다. 이 경로는 focused A/B용으로 유지한다. rotating update가 full in-flight fence wait를 피하므로 기본 경로 후보로 더 깔끔하다.

`DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE=1`은 `DUXEL_VK_STATIC_GEOMETRY_UPDATE=rotating`의 backward-compatible explicit override다. 같은 shape의 content change가 발생하면 현재 static geometry buffer를 frame-safe reuse pool에 retire하고, 안전해진 retired buffer가 있으면 그것을 활성화하며 없으면 새 buffer를 만들어 pool을 시드한다. Retired pool은 tag별로 frame count를 넘지 않도록 제한되고, 오래 재사용되지 않은 retired buffer는 `StaticGeometryRetiredReuseGraceFrames` 뒤에 trim된다. 이전 제출 frame이 아직 읽을 수 있는 buffer를 덮어쓰지 않으므로 in-place update보다 기본 경로 후보로 더 깔끔하다. 검증된 NVIDIA discrete GPU에서는 기본 resolve policy이고, 다른 vendor/device gate에서는 명시적으로 사용할 수 있다.

`DUXEL_VK_COMMAND_SCHEDULER=all` 또는 `1`은 모든 eligible draw list에 opt-in overlap-constrained Vulkan command scheduler를 켠다. `DUXEL_VK_COMMAND_SCHEDULER=static`은 같은 scheduler를 static-layer replay draw list에만 켜서, 측정된 static-layer replay 이득은 유지하면서 dynamic whole-list scheduling을 hot path에서 제외한다. 이 경로는 `UiDrawCommand` coverage bounds로 overlap 때문에 유지해야 하는 순서를 보존하면서 ready command를 pipeline class별로 묶고, 기록 시점에서 새로 인접해진 호환 command를 병합한다. `all` 모드에서는 dynamic draw-list 생성 단계에서 `UiDrawList.CommandScheduleStamp`도 계산해서 cache-hit scheduling이 Vulkan command recording에서 전체 command hash/compare를 반복하지 않게 한다. 기본 경로는 아니다. 초점형 프로파일링에서만 사용하고, 넓은 scene이 무제한 scheduling 비용을 내지 않도록 `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`를 제한해서 사용한다.

Static cached layer replay는 `DUXEL_VK_COMMAND_SCHEDULER=static`, `all`, 또는 `1`일 때 layer capture 시점에 보수적인 command schedule shape stamp를 한 번 계산한다. 이 stamp는 안정적인 local command shape와 raw local command bounds를 기준으로 한다. Vulkan scheduling도 static-layer draw list에는 같은 보수적 bounds를 사용하므로, 안정적인 layer가 이동하거나 clipping되어도 매 프레임 overlap analysis나 replay-shape hashing을 반복하지 않고 cached safe order를 재사용할 수 있다.

Dirty static layer가 같은 draw-list shape로 다시 build되면 Core는 새 local vertex/index/command/primitive 배열을 할당하지 말고 기존 `UiLayerCachedList` storage를 in-place refresh해야 한다. Overwrite 뒤 `StaticGeometryStamp`와 command schedule shape stamp를 다시 계산하고, 안정적인 `duxel.layer.static:{layerId}:list:{i}` key는 유지하며, replay를 invalidate해서 opacity/translation/clip command가 갱신된 content stamp 아래에서 다시 만들어지게 한다.

`DUXEL_UI_COMMAND_SCHEDULER=1`은 `UiDrawListBuilder.Flush()`에서 더 이른 builder-stage scheduling 실험을 켠다. 이 토글은 `DUXEL_VK_COMMAND_SCHEDULER`와 별도로 둔다. 매 프레임 draw-list 내용이 바뀌는 동적 whole-list builder scheduling은 절감하는 기록 비용보다 더 비쌀 수 있으므로 구조 실험 전용이다. 기본 경로 후보로 보기 전에 stable layer/static schedule caching이나 명시적 draw channel을 우선한다.

GPU timestamp 계측은 `DUXEL_VK_GPU_PROFILE=1`로 추가로 켠다. 이 값은 그래픽 queue timestamp를 지원하는 장치에서만 활성화되고, 프로파일 라인의 `gpuRender=...`에 render pass 전후 GPU 실행 시간을 microsecond 단위로 추가한다. CPU command-recording 비용과 shader/GPU-side 비용을 분리할 때 사용한다.

Static cached rect/circle primitive를 triangle vertex/index geometry로 확장하는 경로는 `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES=auto`가 기본이다. 자동 정책은 NVIDIA/AMD discrete GPU에서만 켠 뒤 draw-list별 byte guard와 mutation guard를 적용한다. `auto`는 예상 expanded vertex/index bytes가 primitive-instance bytes의 `32x`를 넘으면 확장하지 않아 high-segment primitive-heavy cache를 static primitive-instance 경로에 남긴다. 또한 같은 static geometry tag의 content hash가 바뀌면 해당 tag는 `30`프레임 동안 확장을 억제해서 mutating static layer가 안정될 때까지 upload가 싼 primitive-instance 경로를 사용하게 한다. `1/true/on`은 확장을 강제하고 `0/false/off`는 비활성화를 강제한다. 이 경로는 cached static draw-list의 command 순서, clip, opacity, texture state를 유지하면서 확장된 primitive를 indexed triangle draw mode로 태운다. 실제 expand/skip 여부는 장치 정책 블록만 보지 말고 profile의 `staticPrim(...)` 카운터로 확인한다.

`staticPrim(...)` profile block의 `layout=...`은 해당 frame에서 materialize한 expanded primitive layout 수다. 안정 static circle cache는 생성/rebuild에서만 layout을 materialize해 `layout>0`을 기록하고 cache-hit frame에서는 `layout=0`이어야 한다. steady-hit에서 0이 아니면 CPU layout work가 반복되고 있다는 뜻이다.

Static primitive auto decision의 구현 경계는 `VulkanRendererBackend.StaticPrimitivePolicy.cs`다. Static geometry 코드는 upload code 안에 device/heuristic decision을 직접 넣지 말고 이 policy 경계를 호출해야 한다.

### GPU-driven 렌더러 아키텍처 (2026-07-03)

Vulkan backend는 **graphics pipeline 1개**, **bindless texture descriptor set 1개**, **셰이더 2개**(`Shaders/imgui.vert`, `Shaders/imgui.frag`)로 구성된 GPU-driven 렌더러다:

- **Bindless texture**: 전역 `sampler2D[]` combined-image-sampler 배열(용량 4096, UPDATE_AFTER_BIND + PARTIALLY_BOUND) 하나를 프레임당 1회 bind한다. Texture는 `TextureResources`의 free-list allocator에서 slot index를 받고, slot은 deferred texture-destroy 시점에 재활용된다. Per-texture descriptor set은 존재하지 않는다.
- **통합 dual-source blending**: 모든 draw가 premultiplied color와 per-channel blend factor를 출력한다(`One/OneMinusSrc1Color`, `One/OneMinusSrc1Alpha`). 일반 draw는 `blendFactor = vec4(alpha)`를 출력해 `SrcAlpha/OneMinusSrcAlpha`와 수학적으로 동일하고, ClearType subpixel 텍스트는 per-channel coverage를 출력하며 fragment texture-index push constant의 최상위 비트로 선택된다.
- **Vertex pulling**: pipeline에 vertex input state가 없다. Vertex shader가 buffer device address(`GL_EXT_buffer_reference`)로 `UiVertex` 스트림과 packed `PrimitiveInstance` 레코드(둘 다 20바이트/5-dword)를 직접 읽는다.
- **Push constant 레이아웃**: vertex range `[0,40)` = `scale`(8) + `translate`(8) + `opacity`(4) + `drawMode`(4) + vertex-buffer address(8) + primitive-buffer address(8); fragment range `[40,44)` = packed texture index + subpixel mode bit. `drawMode` 0 = indexed triangle pulling, 1 = primitive instance expansion(셰이더 내 rect corner/circle fan 전개).
- **Per-draw state**: `vkCmdBindPipeline`은 프레임당 1회다. Draw별 변화는 push constant, index-buffer bind, dynamic scissor뿐이다. `vkCmdBindVertexBuffers`는 backend에 더 이상 존재하지 않는다.
- **Dynamic rendering**: render pass/framebuffer 객체가 없다. 프레임은 `RenderingAttachmentInfo`와 함께 `vkCmdBeginRenderingKHR`/`vkCmdEndRenderingKHR`로 기록하며, MSAA는 inline resolve(`resolveMode = AVERAGE`, MSAA target → swapchain image)로 처리한다. 암묵적 render-pass 전환 대신 명시적 image barrier를 사용한다: 렌더링 전 `UNDEFINED→COLOR_ATTACHMENT`, 후 `COLOR_ATTACHMENT→PRESENT_SRC`. Pipeline은 swapchain format을 담은 `PipelineRenderingCreateInfo`를 체인한다.
- **필수 device feature**(없으면 명시적 실패, fallback 없음): `shaderSampledImageArrayDynamicIndexing`, `descriptorBindingSampledImageUpdateAfterBind`, `descriptorBindingUpdateUnusedWhilePending`, `descriptorBindingPartiallyBound`, `runtimeDescriptorArray`, `bufferDeviceAddress`, `dualSrcBlend`, `dynamicRendering`(`VK_KHR_dynamic_rendering`).
- **Dynamic geometry 메모리**: 프레임별 vertex/primitive buffer는 BAR 메모리(`DEVICE_LOCAL|HOST_VISIBLE|HOST_COHERENT`)를 우선 선택하고, 장치에 없으면 명시된 host-visible 요구 집합으로 할당한다. Vertex pulling 성능에 필수적인 선택이다: 일반 host 메모리에 대한 셰이더 BDA 읽기는 정점마다 PCIe를 건너서 dynamic 장면이 측정 가능하게 느려진다.

### 렌더러 모듈 맵

`VulkanRendererBackend`는 19개 partial-class 모듈로 구성된다. 새 단일 목적 파일을 추가하지 말고 대응하는 모듈을 확장한다:

- `VulkanRendererBackend.cs` — `IRendererBackend` shell, constructor/bootstrap 순서, lifecycle/teardown, render-entry frame 순서, settings/API mutation.
- `VulkanRendererBackend.Device.cs` — instance/surface/physical-device/queue/device 셋업, 필수 feature 게이트, vendor/device policy state와 env parsing, pipeline cache identity.
- `VulkanRendererBackend.Swapchain.cs` — swapchain selection policy, 리사이즈 전용/전체 recreate 흐름, image view/MSAA target, semaphore capacity, live resize 중 non-swapchain renderer resource 보존.
- `VulkanRendererBackend.PipelineResources.cs` — bindless descriptor layout/pool/set, pipeline layout/push range, 단일 graphics pipeline, sampler, pipeline cache load/save, embedded shader loading.
- `VulkanRendererBackend.Resources.cs` — generic buffer/image/view/memory helper(preferred-memory 선택을 포함한 `CreateBuffer`, `GetBufferDeviceAddress`), image layout transition policy와 `imgTrans(...)` counter.
- `VulkanRendererBackend.TextureResources.cs` — texture create/update/destroy, bindless slot allocator와 descriptor write, upload batching, font/white texture id classification, deferred destroy.
- `VulkanRendererBackend.UploadScheduler.cs` — staging buffer lifetime, upload batching(`DUXEL_VK_UPLOAD_BATCH`), upload queue policy(`DUXEL_VK_UPLOAD_QUEUE`), submission/wait accounting.
- `VulkanRendererBackend.DynamicGeometry.cs` — 프레임별 vertex/index/primitive buffer capacity/mapping(BAR 우선), dynamic geometry upload, frame geometry preparation.
- `VulkanRendererBackend.StaticGeometry.cs` — static cache identity/lifetime/LRU, content/shape derivation, frame preparation, policy application, replacement policy.
- `VulkanRendererBackend.StaticGeometryStorage.cs` — static device-local buffer materialization(device address 포함), static upload/layout writing, retired-buffer pool.
- `VulkanRendererBackend.StaticPrimitivePolicy.cs` — static primitive triangle-expansion auto policy(`DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`), byte/mutation guard, decision profiling.
- `VulkanRendererBackend.Frame.cs` — frame orchestration(acquire/submit/present/fence), shared frame-fence wait, frame command-recording 준비, frame completion.
- `VulkanRendererBackend.CommandRecording.cs` — `RecordCommandBuffer`(bindless set과 단일 pipeline을 1회 bind), frame/render-pass begin/end, recording state aggregation, frame/draw-list context, draw-list traversal.
- `VulkanRendererBackend.CommandState.cs` — draw-mode push state, texture-index push state, vertex/primitive address push + index-buffer bind state, transform/opacity push state, scissor state.
- `VulkanRendererBackend.CommandDraw.cs` — draw-path orchestration, draw dispatch(indexed/instanced), command classification, texture lookup cache, primitive instance encoding.
- `VulkanRendererBackend.CommandScheduling.cs` — opt-in overlap-constrained command scheduler(`DUXEL_VK_COMMAND_SCHEDULER`), schedule cache, merge expansion.
- `VulkanRendererBackend.CommandDiagnostics.cs` — command diag gating/label(`DUXEL_VK_COMMAND_DIAG`), font diagnostics(`DUXEL_VK_FONT_DIAG`), record profile counter와 `CommandRecordStats` 구성.
- `VulkanRendererBackend.Diagnostics.cs` — profile line 출력, device/policy 텍스트, GPU timestamp 해석, 실패 추적.
- `VulkanRendererBackend.Types.cs` — 공용 renderer record(device address를 포함한 `StaticGeometryBuffer`, slot index를 갖는 `TextureResource`, frame resource).

Upload command pool, staging buffer lifetime, `DUXEL_VK_UPLOAD_BATCH`, `DUXEL_VK_UPLOAD_QUEUE`, staging offset reservation, batch flush, graphics upload-prepare command submission, transfer upload-copy submission, single-time upload submission 책임은 `VulkanRendererBackend.UploadScheduler.cs`에 둔다. Texture update와 static geometry upload 코드는 자체 staging command 경로를 늘리지 말고 이 경계를 호출해야 한다. Upload batching은 기본 enabled이며, 진단이 필요할 때만 `DUXEL_VK_UPLOAD_BATCH=0`, `false`, `off`로 끈다. Transfer upload path는 opt-in 전용이다. 대상 vendor/device의 focused texture/page upload gate가 split path가 더 빠르다는 것을 증명하기 전까지 default graphics path를 유지한다. 로컬 NVIDIA `10de:2f58` gate에서는 `upSched(prepSub=1 ...)`와 더 긴 upload wait가 짧은 transfer-copy submission 이득을 상쇄해서 transfer가 generic texture update에서 약 `12-13%`, DirectText page update에서 약 `15-19%` 느렸다.

Image layout transition policy는 `Resources` 모듈에 둔다. Texture/page upload가 barrier-heavy인지 copy-heavy인지는 `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)`로 증명한다. `texture_upload_barrier_bench_fba.cs`에서는 같은 texture의 non-overlapping region batch가 `total=2`, many-texture update가 `total=2 * textureCount`로 나와야 한다. `xferStage`는 stage mask가 transfer queue와 호환된다는 뜻이고, `gfxStage`는 transfer-only upload recording 전에 graphics/fragment/color stage mask를 split하거나 옮겨야 한다는 뜻이다.

폰트 command 진단은 `DUXEL_VK_FONT_DIAG=1`로 켠다. 매우 자세한 command 로그는 `DUXEL_VK_FONT_DIAG_OUT`으로 파일에 기록할 수 있다.

일반 Vulkan command sequence 진단은 `DUXEL_VK_COMMAND_DIAG=1`로 켠다. `DUXEL_VK_COMMAND_DIAG_OUT`을 지정하면 draw command의 `pipe`, texture id, clip, static 여부를 파일에 기록해서 pipeline 전환 원인을 추적할 수 있다. 기본은 `DUXEL_VK_COMMAND_DIAG_EVERY=120`, `DUXEL_VK_COMMAND_DIAG_FRAMES=1`로 제한된다.

Draw-list command 병합에서 callback 없는 zero-element command는 draw-order barrier가 아니라 state placeholder로 취급한다. 실제 draw를 추가할 때 builder는 trailing empty placeholder를 제거한 뒤 contiguous command merge 가능성을 검사한다. 병합 조건에는 opacity, texture, clip, translation, kind, vertex offset, callback, user data를 계속 포함해야 한다. caller가 effective clip을 이미 알고 있으면 draw 하나를 위해 builder clip stack을 push/pop하지 말고 clipped `AddImage(...)` 같은 clipped draw helper를 우선 사용한다.

Sampler-free pipeline variant는 더 이상 존재하지 않는다. GPU-driven 렌더러 전환 이후 모든 draw가 bindless texture 배열을 샘플링하고, solid draw는 앱의 1x1 흰색 텍스처를 샘플링하며, pipeline이 하나뿐이므로 pipeline 선택 비용 자체가 사라졌다. `DUXEL_VK_TRIANGLE_COLOR_PIPELINE`, `DUXEL_VK_SOLID_UNIFIED_PIPELINE`, `DUXEL_VK_SOLID_UNIFIED_STATIC`은 분리 pipeline 및 solid-triangle sentinel과 함께 제거되었으므로 다시 도입하지 않는다.

텍스트 렌더러 A/B 프로파일링은 `DUXEL_TEXT_RENDERING=direct`, `atlas`, `auto`로 수행한다. 기본값은 `DirectText`다. 명시적 `atlas`는 atlas renderer 비교용이고, 명시적 `auto`는 atlas 우선 렌더링을 유지하되 atlas에 없는 글자만 DirectText로 즉시 시각 fallback하므로 그 fallback 동작이 의도된 경우에만 사용한다.

DirectText 페이지 텍스처 패킹은 `DUXEL_DIRECT_TEXT_PAGE=1`로만 켠다. DirectText bitmap을 1024x1024 page texture에 넣고 각 region 둘레에 1px border를 예약한 뒤 edge pixel을 border로 복제해서 atlas sampling bleed를 줄인다. Page 생성은 전체 1024x1024 pixel buffer가 아니라 border-inclusive packed region만 업로드하고, 같은 texture의 연속 non-overlapping partial update는 하나의 Vulkan upload/copy submission으로 batch될 수 있다. Upload batching이나 transfer-queue default를 바꾸기 전에는 `samples/fba/directtext_page_upload_bench_fba.cs`를 이 경로의 초점형 upload-policy gate로 사용한다. DirectText page quad는 `PushClipRect -> AddImage -> PopClipRect` sequence 대신 clipped image helper를 사용해야 한다. 기본값은 시각 품질 보존을 위해 off이며, 렌더된 글자 외형 검증 없이 기본 경로로 승격하지 않는다.

2026-07-10 기준 page-enabled 성능 실행은 `1819.709` FPS를 기록했지만, 렌더된 all-features capture는 UI가 누락된 대부분 검은 화면이었다. 이 결과는 기각된 진단 후보로 취급하고 page mode는 기본 off를 유지하며, 재검토 전에 시각 비교를 통과해야 한다.

## 제약과 비목표

아래 저장소 규칙은 작업이 명시적으로 바꾸지 않는 한 유지되는 전제로 본다.

- .NET 9 및 .NET 10 패키지 타깃
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
- `docs/extended-title-bar-guide.ko.md` — ExtendedContent 타이틀바의 앱 레이아웃, 플랫폼 계약, AI 검증 절차
- `docs/getting-started-fba.ko.md` — FBA 첫 시작 가이드
- `docs/fba-reference-guide.ko.md` — 패키지/프로젝트 전환과 `run-fba.ps1`
- `docs/fba-run-samples.ko.md` — 샘플 카탈로그와 원클릭 실행
- `docs/custom-widgets.ko.md` — 재사용 위젯 확장 경로
- `docs/ui-dsl.ko.md` — 선언형 UI 문서
- `docs/version-history.ko.md` — 그룹화된 버전 이력
- `docs/design.ko.md` — 현재 아키텍처와 설계 기준

## 유지보수 메모

미래의 에이전트나 개발자가 Duxel을 올바르게 쓰기 위해 꼭 알아야 하는 안정적인 워크플로 규칙을 새로 발견하면 여기에 반영하고 `Last synced` 날짜를 함께 갱신한다.
