# Duxel Agent Reference

> Last synced: 2026-07-16
> Audience: coding agents and developers building apps, samples, and reusable UI components with Duxel
> Scope: Duxel feature map, architecture boundaries, recommended workflows, and sample anchors

Korean: [docs/duxel-agent-reference.ko.md](duxel-agent-reference.ko.md)

## Purpose

Use this document as the primary reference when writing Duxel code.

This guide is intentionally written for both:

- coding agents that need one practical reference before generating or modifying Duxel code
- developers who want a compact but high-coverage handbook for the current Duxel surface area

This guide is designed to be strong enough that an agent can generate ordinary Duxel code without inspecting the source tree first.

For normal app code, sample code, DSL examples, and reusable widgets, prefer this document first. Use source inspection only when the task is unusually deep, renderer-internal, or platform-internal.

When this document conflicts with the current source code, prefer the current source code and then update this document in the same change set.

## Freshness rule

Treat this file as a living reference.

When project structure, sample layout, public API direction, runtime workflow, or documentation priority changes, update this document together with the implementation.

Always keep the `Last synced` date at the top aligned with the most recent meaningful update to this document.

## What Duxel is

Duxel is an immediate-mode GUI framework for .NET 9 and .NET 10.

Current implementation direction:

- renderer: Vulkan
- primary platform: Windows-native backend
- runtime style: NativeAOT-friendly
- main UI API: `UiImmediateContext`
- app lifecycle entry: `UiScreen.Render(UiImmediateContext ui)`
- package surface:
  - `Duxel.App`
  - `Duxel.Windows.App`

Both packages ship `net9.0` and `net10.0` runtime assets. FBA samples remain `net10.0` because the file-based app feature requires the .NET 10 SDK.

`Duxel.Windows.App` is the single-package Windows entry and depends on `Duxel.App`. Its package dependency must allow analyzer assets to flow so the integrated `Duxel.Core.Dsl.Generator` remains available to Windows-package consumers.

## Standalone generation rule

If you are a coding agent and the user asks for Duxel code generation:

1. start from this document
2. choose one of the templates in this document
3. use the capability map and cookbook sections in this document
4. inspect source code only if the request targets internals not covered here

For most user-facing Duxel work, this document should be sufficient.

## Source-of-truth order

When you need authoritative context, read in this order:

1. this file: `docs/duxel-agent-reference.md`
2. `README.md`
3. task-specific docs:
   - `docs/fba-reference-guide.md`
   - `docs/fba-run-samples.md`
   - `docs/getting-started-fba.md`
   - `docs/custom-widgets.md`
   - `docs/ui-dsl.md`
4. design baseline:
   - `docs/design.ko.md`
5. current source code under `src/` when you need deeper verification or unsupported internals

## Repository map

Top-level areas that matter most:

- `src/`
  - `Duxel.Core` — immediate-mode core, widgets, layout, text, state, draw data, DSL runtime
  - `Duxel.App` — app-facing facade and shared runtime wiring
  - `Duxel.Windows.App` — Windows runner entry surface
  - `Duxel.Platform.Windows` — Windows platform backend
  - `Duxel.Vulkan` — Vulkan renderer backend
- `samples/`
  - `samples/Duxel.Sample` — project-style reference app
  - `samples/fba/*.cs` — file-based app samples
- `docs/` — user and contributor documentation
- `global.json` — pins repository builds and benchmarks to .NET SDK `10.0.301` with patch-only roll-forward
- `run-fba.ps1` — contributor path for local-source FBA validation

## Architecture boundaries

Preserve these boundaries when adding or modifying features:

- `Duxel.Core`
  - owns immediate-mode behavior, layout, widgets, draw list generation, state, text-facing APIs
- `Duxel.Platform.Windows`
  - owns Windows-specific behavior such as input, clipboard, IME/TSF integration, text backend integration, and native windowing
- `Duxel.Vulkan`
  - owns rendering, GPU resources, swapchain flow, and submission
- `Duxel.App` and `Duxel.Windows.App`
  - own app bootstrapping, developer-facing entry flow, option validation, and runtime wiring

Do not casually collapse these boundaries.

### Interactive Windows resize flow

During a Win32 move/size modal loop, `Duxel.App` treats `IPlatformBackend.IsInteractingResize` as continuous render work instead of waiting for one-shot invalidation messages. `Duxel.Platform.Windows` updates an atomic cached client size from `WM_SIZE` and predicts the next client size from the `WM_SIZING` drag rectangle, so the render thread consumes the newest dimensions without a cross-thread `GetClientRect` call.

Each `WM_SIZING` request has a monotonically increasing sequence. The window procedure requests a frame and does not let Windows commit that outer-window step until `IRendererBackend.TryRenderDrawData(...)` reports a successful/suboptimal present for draw data captured at that sequence. Renderer failure cancels the wait explicitly. This intentionally couples visible border movement to completed Duxel frame submission: the pointer/window may advance no faster than rendering, but the outer frame cannot visibly outrun its contents.

During the modal loop, a draw-data/swapchain extent mismatch is rendered into the full current swapchain viewport with proportionally scaled clipping instead of forcing a proactive `vkDeviceWaitIdle` recreation for every pointer sample. WSI `OUT_OF_DATE`/surface-loss results still recreate, and the first non-interactive frame recreates to the exact final extent. The resize-only recreate path passes the prior handle through `VkSwapchainCreateInfoKHR.oldSwapchain` and preserves non-swapchain renderer resources. VSync creation prefers tearing-free Mailbox when supported, otherwise the required FIFO mode; VSync-off prefers Immediate. Do not add a fixed 240 FPS cap.

## Primary public entry points

The most important downstream-facing surfaces are:

- `UiScreen`
- `UiImmediateContext`
- declarative UI API:
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
- compiled design API:
  - `UiCompiledDesign`
  - `IUiDesign`
  - `UiWindows11Design`
- `DuxelWindowsApp.Run(...)`
- custom widget API:
  - `IUiCustomWidget`
  - `MarkdownEditorWidget`
  - `MarkdownViewerWidget`

## Option reference with defaults

These defaults are safe to rely on when generating ordinary Duxel code.

### `DuxelAppOptions`

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Window` | `DuxelWindowOptions` | `new()` | window title, size, vsync |
| `Renderer` | `DuxelRendererOptions` | `new()` | validation, profile, AA, text renderer |
| `Font` | `DuxelFontOptions` | `new()` | font paths, atlas sizing, startup glyph strategy |
| `Frame` | `DuxelFrameOptions` | `new()` | line height, idle frame skip, font rebuild cadence |
| `Debug` | `DuxelDebugOptions` | `new()` | logging and frame capture |
| `Theme` | `UiTheme` | `UiCompiledDesign.Default.Theme` | fallback theme preset |
| `Design` | `UiCompiledDesign?` | `null` | optional compiled design; overrides platform default theme tracking when set |
| `FontTextureId` | `UiTextureId` | `new(1)` | font texture slot |
| `WhiteTextureId` | `UiTextureId` | `new(2)` | white texture slot |
| `Screen` | `UiScreen` | (required) | immediate-mode app entry |
| `Clipboard` | `IUiClipboard?` | `null` | direct clipboard injection |
| `ImageDecoder` | `IUiImageDecoder?` | `null` | custom image decode path |
| `KeyRepeatSettingsProvider` | `IKeyRepeatSettingsProvider?` | `null` | custom key repeat timing |
| `ClipboardFactory` | `Func<IPlatformBackend, IUiClipboard?>?` | `null` | platform-aware clipboard factory |

### Compiled design

By default, Windows apps resolve to `UiCompiledDesign.Windows11` or `UiCompiledDesign.Windows11Dark` from the current OS app theme, and the active design updates when Windows sends an app theme change. Use `DuxelWindowsApp.Run<TDesign>(...)`, `DuxelApp.Options<TDesign>(...)`, or `DuxelAppOptions.Design` when the visual shape of controls must be fixed by code or source generation instead of runtime theme parsing.

```csharp
DuxelWindowsApp.Run<UiWindows11Design>(
    new ProductScreen(),
    title: "Windows 11 styled Duxel",
    width: 980,
    height: 700);
```

For custom compile-time designs, implement `IUiDesign` and pass the generated/static value:

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

`UiTheme` remains the color palette. `UiStyle` remains layout sizing. `UiDesignTokens` controls widget shape such as corner radius, border width, pressed offset, focus ring thickness, and related control geometry. Existing runtime theme changes update colors only; compiled design tokens remain the active shape contract.

When `Design` is `null` and `Theme` is left at its default, Duxel follows the platform theme provider. Explicit `Theme` or `Design` values are treated as app-authored choices and are not overwritten by OS theme changes.

The next homework is a skin layer that can replace rendering strategy per control type. Today, themes, tokens, modifiers, and custom widgets can adjust much of the appearance, but there is not yet an official design-level layer for swapping the built-in rendering policy of controls such as `Button`, `TextField`, `Segmented`, and `Scrollbar`. Start the next session from the [Declarative Control Skin Roadmap](declarative-control-skin-roadmap.md).

### Declarative UI

For SwiftUI/Compose-style composition in C#, use `IUiView` nodes created through the grouped `DuxelView` factories. This keeps dot-completion useful: `DuxelView.Layout`, `DuxelView.Controls`, `DuxelView.Text`, `DuxelView.Display`, `DuxelView.Menus`, and `DuxelView.Windows` expose the common authoring surface without putting every helper into one large class. For compact app code and samples, the `Dux.*` aliases expose the same surface.

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

`UiState<T>` is the ergonomic state holder for app code. It converts to `UiBinding<T>` for controls, so `Dux.Checkbox("Running", running)` and `Dux.TextField("project", project)` stay concise. The declarative aliases `Dux.TextField(...)`, `Dux.TextArea(...)`, `Dux.NumberField(...)`, and `Dux.Slider(...)` take stable control ids and hide immediate-mode `##` labels from app code. Use `state.Set(value)` and `state.Update(current => next)` for explicit state changes. Use `Dux.Bind(get, set)` when state already lives somewhere else, and `UiBinding<T>.Map(...)` for writable derived bindings. Dynamic text and conditional views accept `Func<T>` so values are evaluated during rendering. `UiComponent` is the base class for reusable view objects that implement `Build()`.

For app-scale surfaces, prefer small `UiComponent` classes over one large render method. A component owns or receives state in its constructor and returns an `IUiView` from `Build()`. The recommended startup shape is `DuxelWindowsApp.Run<ProductDesign>(Dux.App(new ProductScreen(...)))`, which binds the compiled design and root component before the first frame. `Dux.App(...)` is the semantic alias for wrapping a declarative root view into the `UiScreen` expected by the runtime.

Use `Dux.AppShell(...)` for the common product surface shape: an optional menu, a themed sidebar, a dynamic header, meta text, command content, and the selected page body. Navigation items are authored with `Dux.NavItem(...)`. The active page can be controlled by a regular `UiState<int>`/`UiBinding<int>`, but enum-backed `UiState<T>` keeps app navigation type-safe.

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

High-level layout helpers describe product surfaces in the same shape users see on screen: `Dux.Section(...)` for titled areas, `Dux.Form(...)` and `Dux.Field(...)` for labeled settings, `Dux.Grid(...)` for repeated card/metric layouts, `Dux.List(...)` for repeated rows, `Dux.Tabs(...)` / `Dux.Tab(...)` for tabbed flows, `Dux.Tree(...)` for hierarchical content, and `Dux.Table(...)` / `Dux.TableColumn(...)` for structured data. For dynamic lists and grids, prefer keyed overloads such as `Dux.List("tasks", tasks, task => task.Id, task => Dux.StatusRow(task.Name, task.Owner, () => task.Progress))` or `Dux.Grid(tasks, task => task.Id, task => new TaskTile(task))`; `Dux.Key(...)` and `.Key(...)` expose the same stable ID scope for individual view fragments.

Declarative control flow stays inside the view tree. Use `Dux.When(...)` and `Dux.Unless(...)` for one-sided conditional content, `Dux.Switch(...)` with `Dux.Case(...)` for state-specific fragments, and `Dux.ForEachIndexed(...)` when the displayed row needs its index. These helpers evaluate dynamic values during rendering, so app code can keep state changes in `UiState<T>` while the view remains a pure description of the current screen.

Display helpers make common product UI fragments concise. Use `Dux.AppShell(...)` for sidebar-based product shells, `Dux.Header(...)` for product title plus meta rows, `Dux.Surface(...)` or `Dux.Card(...)` for padded themed panels, `Dux.SettingsGroup(...)` with `Dux.Setting(...)` for labeled settings, `Dux.Callout(...)` for semantic status/help messages, `Dux.EmptyState(...)` for empty/disabled states, `Dux.Toolbar(...)` for compact command rows, `Dux.MetaBar(...)` with `Dux.Meta(...)` for compact status rows, `Dux.PropertyList(...)` with `Dux.Property(...)` for detail key/value lists, `Dux.StatusRow(...)` for selectable progress rows, `Dux.Badge(...)` for semantic status pills (`UiBadgeTone.Neutral`, `Accent`, `Success`, `Warning`, `Danger`), and `Dux.MetricCard(...)` or `Dux.Metric(...)` for dashboard numbers. Use `Dux.PrimaryButton(...)`, `Dux.DangerButton(...)`, or `.ButtonRole(...)` to give commands a product action hierarchy without manually pushing button colors. Surface and child content keeps the regular Duxel scrollbar behavior by default. The grouped factory surface is `DuxelView.Display`.

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

For product commands, prefer `Dux.CommandBar(...)` when the action role, tooltip, and enabled state matter. It keeps command semantics in values and lets the compiled design style the buttons:

```csharp
Dux.CommandBar(
    Dux.Command("Queue", QueueRun, UiButtonRole.Primary, tooltip: () => "Queue one run"),
    Dux.Command("Reset", Reset, enabled: () => canReset.Value));
```

Settings surfaces can use `Dux.SettingsGroup(...)` so each row owns its label, description, and control:

```csharp
Dux.SettingsGroup(
    Dux.Setting("Project", Dux.TextField("project", project), "Shown in the workspace shell."),
    Dux.Setting("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)));
```

Selection controls include `Dux.Segmented(...)` for compact mode/channel pickers backed by `UiBinding<int>` or typed `UiBinding<T>`. Use `Dux.Choice(value, label)` for typed choices and `Dux.EnumSegmented<TEnum>(...)` when the state is an enum. These are available alongside `Dux.Combo(...)`, `Dux.ListBox(...)`, `Dux.RadioButton(...)`, and `Dux.Selectable(...)`.

`UiModifier` and fluent extension methods provide local, compile-time-authored visual rules. Common modifiers include `.FontSize(...)`, `.Foreground(...)`, `.Tone(...)`, `.Accent()`, `.Success()`, `.Warning()`, `.Danger()`, `.Muted()`, `.Title(...)`, `.Subtitle(...)`, `.Caption(...)`, `.ItemWidth(...)`, `.FillWidth()`, `.Padding(...)`, `.Frame(...)`, `.FillFrameWidth(...)`, `.Width(...)`, `.Height(...)`, `.Background(...)`, `.Border(...)`, `.CornerRadius(...)`, `.Tooltip(...)`, `.VisibleIf(...)`, `.Disabled(...)`, `.StyleColor(...)`, and `.StyleVar(...)`. Text hierarchy can be authored through `Dux.Title(...)`, `Dux.Subtitle(...)`, `Dux.Caption(...)`, or reusable `UiTextStyle` values. Semantic text tone uses `UiTextTone` so app code can say `.Success()` or `.Warning()` instead of carrying color constants. Use the shape modifiers when a view should own its rendered appearance instead of relying on a runtime theme slot:

```csharp
Dux.Callout(
    "Run Status",
    () => status.Value,
    options: new UiCalloutOptions(Tone: UiTextTone.Success, Height: 88f));
```

`.Panel(...)` is the built-in Windows 11-style surface modifier. It uses `UiPanelStyle` internally with `UiStyleColor.FrameBg` / `UiStyleColor.Border`, `UiDesignToken.ControlCornerRadius`, 14 px padding, and fills the available content width by default. Use `Dux.Panel(content, ...)` when a factory call reads better than a fluent modifier. Reusable compiled styles are regular C# types; implement `IUiViewStyle` when the same visual rule should be named, tested, and reused like a SwiftUI `ViewModifier`. Styles can use `UiStyleColor` and `UiDesignToken` slots so their colors and radius come from the active compiled design while the view shape remains authored in C#:

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

`Frame` supplies stable layout dimensions, `FillFrameWidth` fills the current content width, and `Background`, `Border`, and `CornerRadius` describe the compiled view shape. `Background` and `Border` accept either explicit `UiColor` values or semantic `UiStyleColor` slots from the active design. `CornerRadius` accepts either a fixed float or a semantic `UiDesignToken`. Padding placed before a shape modifier becomes the decorated view's internal padding, so both left and right padding participate in the measured shape. `IUiViewStyle` composes those same modifiers into a compile-time-authored style type; `.Style<TStyle>()` applies a default struct style and `.Style(style)` applies a configured style instance. For repeated dynamic content, keep using keyed list/grid overloads or wrap the decorated fragment with `.Key(...)` so local interaction state stays stable. Declarative menu composition is available through `Dux.MainMenuBar(...)`, `Dux.Menu(...)`, and `Dux.MenuItem(...)`. `DuxelView.Custom(ui => ...)` is the extension point for custom declarative nodes that need direct `UiImmediateContext` access.

### `DuxelWindowOptions`

| Property | Default |
|---|---|
| `Width` | `1280` |
| `Height` | `720` |
| `Title` | `"Duxel"` |
| `VSync` | `true` |
| `IconPath` | Duxel default icon |
| `IntegrateSystemChrome` | `true` |
| `UseDuxelTitleBar` | `true` |
| `DuxelTitleBarHeight` | `48f` |

`IntegrateSystemChrome` applies Windows 11 DWM caption, text, border color, rounded corner, and dark-mode attributes from the active startup theme/design. When the app uses the default platform-following design, Windows `WM_SETTINGCHANGE` theme notifications refresh the Duxel theme and render clear color at runtime.

`UseDuxelTitleBar` is enabled by default, so Windows apps remove the native caption and render a Duxel-owned title bar inside the Vulkan surface unless the app explicitly sets `UseDuxelTitleBar = false`. The app runtime wraps the user `UiScreen`, reserves the top viewport inset, draws the app icon, title, minimize/maximize/close buttons, and delegates window move/minimize/maximize/close commands through `IWindowChromeController`.

When `IconPath` and `IconData` are not set, Duxel uses its bundled default `.ico` for the Win32 window/taskbar icon. The `Duxel.Windows.App` package also supplies the same icon as the default `ApplicationIcon` for consuming Windows executables unless the app sets its own icon or `DuxelUseDefaultIcon=false`.

### `DuxelRendererOptions`

| Property | Default |
|---|---|
| `MinImageCount` | `3` |
| `EnableValidationLayers` | `Debugger.IsAttached` |
| `Profile` | `DuxelPerformanceProfile.Display` |
| `MsaaSamples` | `0` |
| `EnableGlobalStaticGeometryCache` | `true` |
| `FontLinearSampling` | `false` |
| `TextRendering` | `DuxelTextRenderingMode.DirectText` |

`DuxelTextRenderingMode.DirectText` is the default text path because the atlas output is not the preferred visual baseline. Use explicit `Atlas` only for atlas renderer A/B profiling. Use explicit `Auto` only when a sample should stay atlas-first but needs immediate DirectText missing-glyph visual fallback before the atlas catches up.

When touching the DirectWrite atlas glyph path, keep oversampled rasterization metrics in logical pixels before they reach the atlas. Bitmap data may be rasterized at `fontSize * oversample`, but glyph advances, text-run advances, offsets, and baselines must be normalized back to the logical em size after downsampling. Bump the font-atlas disk cache version whenever cached placement metrics change.

### `DuxelFontOptions`

| Property | Default |
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
| `StartupRanges` | ASCII range `0x20..0x7E` |
| `StartupGlyphs` | empty |
| `FontSize` | `16` |
| `AtlasWidth` | `1024` |
| `AtlasHeight` | `1024` |
| `Padding` | `2` |
| `Oversample` | `2` |
| `InitialRanges` | ASCII range `0x20..0x7E` |

### `DuxelFrameOptions`

| Property | Default |
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
| `WindowTargetFps` | empty dictionary |
| `IsAnimationActiveProvider` | `null` |

### `DuxelDebugOptions`

| Property | Default |
|---|---|
| `Log` | `null` |
| `LogEveryNFrames` | `60` |
| `LogStartupTimings` | `false` |
| `CaptureOutputDirectory` | `null` |
| `CaptureFrameIndices` | empty |

## Copy-paste starter templates

Use these templates directly when generating code.

### Minimal FBA immediate-mode app

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

### Immediate-mode app with customized options

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

### Minimal custom widget

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

### Markdown widget usage

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

### Minimal DSL app (project-based)

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

## Core app patterns

### Immediate-mode screen pattern

Use `UiScreen` when building a normal app or sample screen.

- override `Render(UiImmediateContext ui)`
- rebuild the UI every frame
- keep your own state on the screen instance or external model

Internal window titles follow the same label/ID convention as controls: `Visible title##stable-id` renders only `Visible title` while the complete string remains the window state identity.

Default recommendation: if the user simply asks for a Duxel sample, generate an immediate-mode `UiScreen` example unless they explicitly ask for DSL.

### DSL pattern

Use the `.ui` DSL when the task is declarative UI authoring, state binding, or generator-based workflows.

- parser: `UiDslParser`
- runtime execution: `UiDslDocument.Emit(...)`
- binding path: `IUiDslValueSource`, `IUiDslEventSink`
- docs: `docs/ui-dsl.md`

### Custom widget pattern

Use instance-based reusable widgets when behavior does not belong on the built-in immediate-mode surface.

- implement `IUiCustomWidget`
- keep widget-local state inside the object when appropriate
- render through `Render(UiImmediateContext ui)`

Current built-in reusable widget references:

- `MarkdownEditorWidget`
- `MarkdownViewerWidget`

## UiImmediateContext capability map

This section is a practical feature inventory for agents and developers.

### Windows and layout

- `Begin(...)` / `End()`
- `BeginChild(...)` / `EndChild()`
- `SameLine()`
- `NewLine()`
- `Spacing()`
- `Separator()`
- `Dummy(...)`
- `Columns(...)`, `NextColumn()`
- table APIs such as `TableSetupColumn(...)`, `TableNextRow()`, `TableNextCell()`
- indentation and width control:
  - `Indent()` / `Unindent()`
  - `SetNextItemWidth(...)`
  - `PushItemWidth(...)` / `PopItemWidth()`
- cursor and region control:
  - `GetCursorPos()` / `SetCursorPos(...)`
  - `GetContentRegionAvail()`

### Text and typography

- `Text(...)`
- `TextColored(...)`
- `TextDisabled(...)`
- `TextWrapped(...)`
- `TextUnformatted(...)`
- `LabelText(...)`
- font control:
  - `PushFont(...)` / `PopFont()`
  - `PushFontSize(...)` / `PopFontSize()`
- alignment helpers and wrapped text behavior are demonstrated in `samples/fba/all_features.cs` and `samples/fba/text_render_validation_fba.cs`

### Buttons and basic choice widgets

- `Button(...)`
- `SmallButton(...)`
- `InvisibleButton(...)`
- `ArrowButton(...)`
- `Checkbox(...)`
- `CheckboxFlags(...)`
- `RadioButton(...)`

Practical signatures:

- `bool Button(string label)`
- `bool Button(string label, UiVector2 size)`
- `bool SmallButton(string label)`
- `bool InvisibleButton(string id, UiVector2 size)`
- `bool ArrowButton(string id, UiDir dir)`
- `bool Checkbox(string label, ref bool value)`
- `bool CheckboxFlags(string label, ref int flags, int flagsValue)`
- `bool RadioButton(string label, bool active)`
- `bool RadioButton(string label, ref int value, int buttonValue)`

### Text input and numeric input

- `InputText(...)`
- `InputTextWithHint(...)`
- `InputTextMultiline(...)`
- `InputInt(...)`, `InputInt2(...)`, `InputInt3(...)`, `InputInt4(...)`
- `InputFloat(...)`, `InputFloat2(...)`, `InputFloat3(...)`, `InputFloat4(...)`
- `InputDouble(...)`, `InputDouble2(...)`, `InputDouble3(...)`, `InputDouble4(...)`
- scalar-array editing APIs where supported by the current surface

Practical signatures:

- `bool InputText(string label, ref string value, int maxLength)`
- `bool InputTextWithHint(string label, string hint, ref string value, int maxLength)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, float height)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, int visibleLines)`
- `bool InputInt(string label, ref int value)`
- `bool InputFloat(string label, ref float value, string format = "0.###")`
- `bool InputDouble(string label, ref double value, string format = "0.###")`

### Sliders and drags

- `SliderFloat(...)`, `SliderFloat2(...)`, `SliderFloat3(...)`, `SliderFloat4(...)`
- `SliderInt(...)`, `SliderInt2(...)`, `SliderInt3(...)`, `SliderInt4(...)`
- `SliderAngle(...)`
- `VSliderFloat(...)`
- `DragFloat(...)`, `DragFloat2(...)`, `DragFloat3(...)`, `DragFloat4(...)`
- `DragInt(...)`, `DragInt2(...)`, `DragInt3(...)`, `DragInt4(...)`
- `DragFloatRange2(...)`
- `DragIntRange2(...)`

Practical signatures:

- `bool SliderFloat(string label, ref float value, float min, float max)`
- `bool SliderInt(string label, ref int value, int min, int max)`
- `bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")`
- `bool DragInt(string label, ref int value, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)`

### Selection, combos, lists

- `Combo(...)`
- `BeginCombo(...)` / `EndCombo()`
- `ListBox(...)`
- `BeginListBox(...)` / `EndListBox()`
- `Selectable(...)`
- list and selection behavior examples live in `samples/fba/all_features.cs`, `samples/fba/item_status.cs`, and related sample sections

Practical signatures:

- `bool Combo(ref int currentIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8, string? id = null)`
- `bool Combo(ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int popupMaxHeightInItems = 8, string? id = null)`
- `bool BeginCombo(string previewValue, int popupMaxHeightInItems = 8, string? id = null)`
- `bool ListBox(ref int currentIndex, IReadOnlyList<string> items, int visibleItems = 6, string? id = null)`
- `bool Selectable(string label, ref bool selected)`
- `bool Selectable(string label, bool selected)`

### Trees, tabs, menus, popups, tooltips

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
- `BeginPopupContextItem(...)` and related context-popup helpers
- `BeginTooltip()` / `EndTooltip()`
- `SetTooltip(...)`

Practical signatures:

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

### Color, images, and rich visuals

- `ColorButton(...)`
- `ColorEdit3(...)`, `ColorEdit4(...)`
- `ColorPicker3(...)`, `ColorPicker4(...)`
- `Image(...)`
- `ImageWithBg(...)`
- `ImageButton(...)`

For image-centric workflows, start with `samples/fba/image_and_popups.cs` and `samples/fba/image_widget_effects_fba.cs`.

Practical signatures:

- `bool ColorEdit3(string label, ref float r, ref float g, ref float b)`
- `bool ColorEdit4(string label, ref float r, ref float g, ref float b, ref float a)`
- `bool ImageButton(string id, UiTextureId textureId, UiVector2 size, UiColor? tint = null)`

### Progress, plots, and feedback

- `ProgressBar(...)`
- `PlotLines(...)`
- `PlotHistogram(...)`

Practical signature:

- `void ProgressBar(float fraction, UiVector2 size, string? overlay = null)`

### Styling and scoped overrides

- `PushStyleColor(...)` / `PopStyleColor()`
- `PushStyleVar(...)` / `PopStyleVar()`
- `PushId(...)` / `PopId()`
- `BeginDisabled()` / `EndDisabled()`

When generating dynamic lists, manage IDs explicitly to avoid collisions.

### State and interaction queries

- `IsItemHovered()`
- `IsItemActive()`
- `IsItemClicked()`
- `GetItemRectMin()`
- `GetItemRectMax()`
- `GetItemRectSize()`
- mouse and cursor queries through the current immediate context surface

For live behavior inspection, see `samples/fba/input_queries.cs` and `samples/fba/item_status.cs`.

### Custom drawing and layers

Use custom drawing when the built-in widget set is not enough.

- draw-list builder primitives include rectangles, circles, lines, bezier curves, text, images, clip control, texture switching, and callbacks
- layer and canvas-oriented flows are demonstrated by layer bench samples and mixed rendering samples
- retained static draw lists use `StaticGeometryKey` as their cache identity; change the key when retained geometry content changes
- `UiDrawList.StaticGeometryStamp` is the cheap geometry-content stamp used by renderer caches; layer caches populate it automatically, and custom retained static producers may set it with `UiDrawList.ComputeStaticGeometryStamp(...)`
- Layer opacity and append translation are carried through `UiDrawCommand.Opacity`/`UiDrawCommand.Translation` and applied in the Vulkan vertex shaders, so retained static geometry keys/stamps should describe geometry content, not layer opacity or placement
- Use `UiDrawListBuilder.Split(count)` and `SetCurrentChannel(index)` when the producer already knows independent draw groups and can preserve the intended stacking. Choose `MergeChannelsAsDrawLists()` only when those groups can remain separate draw lists and a focused gate proves the extra draw-list boundary helps; `global_dirty_strategy_bench.cs` is a counterexample where copy-free channel output is slower. Use `Merge()` when one physically merged draw list is needed or when the focused gate favors copy-merge. Channel output order is explicit draw order, so do not channelize overlapping content if that changes the desired stacking.

Start here for custom drawing and caching references:

- `samples/fba/all_features.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`
- `samples/fba/global_dirty_strategy_bench.cs`
- `samples/fba/vector_primitives_bench_fba.cs`

## Cookbook for common generation tasks

### Basic form window

Use this pattern for settings pages, dialogs, and tool panels.

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

### Menu bar pattern

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

### Popup pattern

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

### Tab pattern

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

### Tree pattern

```csharp
if (ui.TreeNode("Assets", defaultOpen: true))
{
  ui.BulletText("Textures");
  ui.BulletText("Shaders");
  ui.BulletText("Fonts");
  ui.TreePop();
}
```

### List and combo pattern

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

### Two-column settings pattern

For legacy two-column settings layouts, prefer `Columns(...)` only when matching existing samples. For new structured data, prefer table APIs.

### Custom draw canvas pattern

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

## Generation rules of thumb

- for quick demos, generate one `UiScreen` with a single `BeginWindow(...)` / `EndWindow()` pair
- keep state in private fields on the screen or widget object
- prefer immediate-mode examples over renderer internals unless the user explicitly asks for renderer work
- prefer existing sample naming and patterns over inventing new abstractions
- prefer table APIs for new structured data views
- use `run-fba.ps1` for validating local source behavior in this repository
- if the request is about markdown editing or viewing, prefer the built-in markdown widgets before inventing a custom parser

## Sample-first feature lookup

Prefer an existing sample before inventing a new pattern.

| Need | Start here |
|---|---|
| Broad feature showcase | `samples/fba/all_features.cs` |
| Declarative C# dashboard | `samples/fba/declarative_dashboard_fba.cs` |
| Declarative DSL + Theme | `samples/Duxel.ThemeDemo` |
| Layout and style control | `samples/fba/advanced_layout.cs` |
| Legacy columns | `samples/fba/columns_demo.cs` |
| Images, popups, tooltip patterns | `samples/fba/image_and_popups.cs` |
| Image effects and animated images | `samples/fba/image_widget_effects_fba.cs` |
| Keyboard and mouse query APIs | `samples/fba/input_queries.cs` |
| Item lifecycle and status queries | `samples/fba/item_status.cs` |
| Text alignment and render validation | `samples/fba/text_render_validation_fba.cs` |
| Font style and size validation | `samples/fba/font_style_validation_fba.cs` |
| Layer cache and dirty strategy behavior | `samples/fba/idle_layer_validation.cs`, `samples/fba/layer_dirty_strategy_bench.cs`, `samples/fba/global_dirty_strategy_bench.cs` |
| Draw-layer and widget mixing | `samples/fba/layer_widget_mix_bench_fba.cs` |
| DirectText changing-string performance | `samples/fba/directtext_dynamic_text_bench_fba.cs` |
| Rendering stress | `samples/fba/ui_mixed_stress.cs` |
| Physics/perf benchmark | `samples/fba/Duxel_perf_test_fba.cs` |
| App-scale reference | `samples/Duxel.Sample` |

## Preferred execution workflows

### End-user package workflow

Use package mode when consuming Duxel as a library.

- `dotnet run hello.cs`
- `dotnet run samples/fba/all_features.cs`

FBA samples use package directives such as:

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*
```

### Contributor local-source workflow

Use `run-fba.ps1` when validating local source changes.

Preferred examples:

- `./run-fba.ps1 samples/fba/all_features.cs -NoCache`
- `./run-fba.ps1 samples/fba/all_features.cs -Managed`
- `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait`

Why:

- it rewrites package directives to local project references
- it normalizes Windows entry calls when needed
- it supports NativeAOT by default
- it can wait for NativeAOT GUI samples to exit with `-Wait`, which is required for automated file-log benchmark collection
- it reflects workspace source changes, unlike plain package-mode execution

Do not rely on `dotnet run samples/fba/<file>.cs` to validate local source edits.

## Build and validation

Default repository build:

- `dotnet build Duxel.slnx -c Release`

Minimum validation expectation after meaningful code changes:

- build the solution, or
- run at least one relevant sample path

Prefer the smallest relevant validation that still proves the change.

In this repository, also inspect `.github/copilot-instructions.md` and the relevant `.github/skills/*/SKILL.md` files when the task touches .NET, performance, Vulkan, native imports, or FBA samples. Treat them as local project guidance for Codex too, while keeping `AGENTS.md` and explicit user/developer instructions as the active conversation contract.

## Environment variables and runtime toggles

Common runtime toggles used across samples and diagnostics include:

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

When a task depends on a runtime toggle, verify the exact name and meaning in the relevant sample before documenting or using it.

For Vulkan command-recording profiling, set `DUXEL_VK_PROFILE=1`. `DUXEL_VK_PROFILE_EVERY` controls the log interval in frames, and `DUXEL_VK_PROFILE_OUT` appends profile lines to a file. Profile lines include `device(vendor=... vid=... did=... type=... name=... gfxQ=... uploadQ=... xferCandQ=... tsBits=... tsPeriodNs=... gpuTs=...)` and `policy(upload=... transferCandidate=... staticPrimTri=... staticUpdate=... staticUpdateReq=... scheduler=... schedWindow=... staticSecondaryMin=...)` so device/vendor gates remain attributable when artifacts from NVIDIA, AMD, Intel, and integrated/discrete devices are compared later. `uploadQ` is the queue family currently used for upload command buffers; `xferCandQ` is only the detected non-graphics transfer-capable candidate. `policy(upload=graphics transferCandidate=1)` therefore means a candidate exists, not that uploads are already using it. `DUXEL_VK_UPLOAD_QUEUE=transfer` opts into the split transfer upload path only when that candidate exists; `policy(upload=transfer transferCandidate=1)` is the profile proof that upload copy command buffers are using it. `staticUpdateReq` is the requested mode and `staticUpdate` is the resolved device policy. `scheduler` is the resolved command-scheduler mode: `disabled`, `static`, or `all`. Profile lines also include `stateUs(pipe=... desc=... buf=... push=... scissor=...)` to separate pipeline binds, descriptor binds, vertex/index/primitive buffer binds, push constants, and scissor sets. `clipCache(calc=... reuse=...)` reports actual scissor rectangle computation count and consecutive same clip/translation reuse count. `staticSec(cand=... cmds=... draws=...)` reports static draw lists that crossed the active `staticSecondaryMin` threshold, plus the recorded commands/draws inside those candidates; `cand=0` means secondary command buffers are not a useful next-path hypothesis for that frame even if static lists exist. `listWork(staticCmd=... dynCmd=... staticDraw=... dynDraw=... staticPipe=... dynPipe=... staticClip=... dynClip=... staticScissor=... dynScissor=... staticPush=... dynPush=... staticGeom=... dynGeom=... staticPrim=... dynPrim=...)` attributes command-recording work to static cached replay versus dynamic draw lists. Use it to decide whether the next bottleneck is static replay policy or dynamic UI ordering/channelization. `staticGeom(hit=... create=... replace=... update=... reuse=... hash=...)` reports static geometry cache reuse, buffer creation, content replacement, same-shape in-place content update, same-shape rotating-buffer reuse, and fallback content hashing in the upload phase. `staticMem(active=... activeBytes=... retired=... retiredBytes=...)` reports active static geometry entries/bytes and retired rotating-pool entries/bytes for memory-pressure checks. `staticPrim(expand=... expandPrim=... layout=... force=... autoSkip=... autoSkipPrim=... autoSkipMut=... expandBytes=... autoSkipBytes=...)` reports per-frame static primitive triangle expansion decisions, which is necessary because `policy(... staticPrimTri=1 ...)` only says the device policy allows the path. `autoSkipMut` is the subset of skipped lists suppressed because the same static geometry tag is actively changing content. `pipeClass(font=... texTri=... colorTri=... texPrim=... colorPrim=... solid=...)` reports the actual pipeline class selected after renderer policy, which is more precise for pipeline-switch analysis than the original source command kind alone. `sched(probe=... hit=... miss=... nochange=... lists=... merged=... us=...)` reports opt-in command scheduler activity and cost. `upSched(sub=... prepSub=... wait=... flush=... bytes=... texRegions=... bufCopies=... submitUs=... prepUs=... waitUs=...)` reports staging upload scheduler submissions, split-transfer graphics-prepare submissions, upload fence waits, batch flushes, staged byte volume, texture copy regions, static/buffer copy commands, and their submission/wait costs. `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)` reports image layout transition counts and separates transfer-queue-compatible stage masks from barriers that still require graphics stages. `state=` excludes scissor because scissor is also part of the `clip=` bucket, so interpret scissor separately.

Performance demos should match the specific bottleneck being tested. Use broad layer-widget scenes as final regressions, but add or pick a focused FBA demo when the question is narrow. `samples/fba/pipeline_ordering_bench_fba.cs` is the focused gate for dynamic solid/text pipeline ordering cost; it includes alternating, grouped solids-then-text, copy-merge `channelized-solid-text`, and copy-free `channel-drawlists-solid-text` phases and uses `DUXEL_PIPELINE_ORDER_BENCH_OUT`, `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`, and `DUXEL_PIPELINE_ORDER_ITEMS`. `samples/fba/dynamic_widget_ordering_bench_fba.cs` is the focused gate for widget-like dynamic producer ordering with row-clip churn; it includes alternating widget rows, grouped solids-then-text, copy-merge channelized, and copy-free channel-drawlists phases, and uses `DUXEL_DYN_WIDGET_ORDER_BENCH_OUT`, `DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_ITEMS`, and `DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS`. `samples/fba/vector_primitives_bench_fba.cs` is the focused gate for primitive-heavy geometry and accepts `DUXEL_VECTOR_BENCH_WORKLOAD=mixed`, `rect-outline`, or `axis-line`, plus `DUXEL_VECTOR_BENCH_OUT`, `DUXEL_VECTOR_BENCH_PHASE_SECONDS`, and `DUXEL_VECTOR_BENCH_COUNTS`. `samples/fba/Duxel_perf_test_fba.cs` is the polygon physics/perf smoke; it defaults to the Render profile and global static backdrop cache, `DUXEL_PERF_PROFILE=render|display` can override the startup profile, `DUXEL_PERF_GLOBAL_STATIC_CACHE=0` disables the retained static backdrop references, and the Render Profile checkbox applies the profile immediately by switching MSAA between `1x` and `4x`. `samples/fba/global_dirty_strategy_bench.cs` is the focused gate for global static-background caching versus dynamic overlay updates; `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS` compares channel copy-merge against separate draw-list output for that exact background/overlay channel structure. `samples/fba/static_layer_moving_order_bench_fba.cs` is the focused gate for moving static-layer replay schedule reuse; it uses `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`, `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`, and `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`. `samples/fba/static_cache_rebuild_bench_fba.cs` is the focused gate for cache replay, false-dirty static rebuilds, mutating static geometry replacement/update cost, Core cache-copy allocation pressure, and static primitive triangle memory pressure; it reports `avgAllocatedBytes` per measured frame and uses `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`, `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_LAYERS`, `DUXEL_STATIC_CACHE_REBUILD_DENSITY`, `DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE` (`circles`, `rects`, or `mixed`), `DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS`, and optional `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW` for GPU-bound variants. `samples/fba/texture_upload_barrier_bench_fba.cs` is the focused gate for texture upload copy/barrier behavior before transfer-queue policy changes; it uses `DUXEL_TEXTURE_UPLOAD_BENCH_OUT`, `DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS`, `DUXEL_TEXTURE_UPLOAD_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGION_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGIONS`, `DUXEL_TEXTURE_UPLOAD_TEXTURES`, and `DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES`. `samples/fba/directtext_page_upload_bench_fba.cs` is the focused gate for DirectText page-style partial texture upload behavior without platform glyph rasterizer cost; it uses `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`, `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`, `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`, `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`, `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`, `DUXEL_DTPAGE_UPLOAD_REGIONS`, `DUXEL_DTPAGE_UPLOAD_PAGES`, and `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`.

`BenchFrameRecorder` is the shared raw-frame accumulator for focused gates. Its statistics include samples, measured seconds, average FPS, median/p95/p99 frame time, and 1% low FPS. `layer_widget_mix_bench_fba.cs` writes these fields in JSON schema version 2; consumers must check `schemaVersion` before assuming the tail fields exist. The layer-widget and DirectText gates allocate `1,048,576` sample slots and cap phase duration at 30 seconds so valid long runs, including the DirectText Atlas control observed near `10.7k` FPS, do not overflow the earlier `262,144`-sample bound.

`samples/fba/directtext_dynamic_text_bench_fba.cs` is the focused gate for stable DirectText cache hits versus changing-string cache misses. It requires `DUXEL_DIRECTTEXT_BENCH_OUT`; controls are `DUXEL_DIRECTTEXT_BENCH_PHASE_SECONDS`, `DUXEL_DIRECTTEXT_BENCH_WARMUP_FRAMES`, `DUXEL_DIRECTTEXT_BENCH_ROWS`, and `DUXEL_DIRECTTEXT_BENCH_CORPUS_FRAMES`. Its JSON includes frame and text-work median/p95/p99 tails, 1% low FPS, average allocated bytes per frame, and generation collection counts.

Axis-aligned `UiDrawListBuilder.AddRect(...)` outlines with `rounding <= 0` should use rect-filled primitive emission instead of triangle polyline geometry. Horizontal and vertical `UiDrawListBuilder.AddLine(...)` calls should also use rect-filled primitive emission; diagonal lines keep the quad triangle path. This keeps ordinary rectangular borders and axis-aligned separators on the primitive path and reduces text/triangle/primitive pipeline churn. Rounded outlines still use the polyline path.

`DUXEL_VK_STATIC_GEOMETRY_UPDATE` controls same-shape static geometry content changes. Valid values are `auto`, `replace`, `inplace`, and `rotating`. `auto` resolves to `rotating` on validated NVIDIA discrete GPUs and to `replace` elsewhere until AMD/Intel and broader NVIDIA gates prove the same policy. Use `replace` to force allocation/replacement, `inplace` to force fence-waited reupload into existing buffers, and `rotating` to force retired-buffer reuse.

`DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE=1` is a backward-compatible explicit override for `DUXEL_VK_STATIC_GEOMETRY_UPDATE=inplace`. If a static geometry cache entry has the same vertex/index/primitive counts and the same expanded primitive triangle layout, Vulkan waits for all in-flight frame fences and reuploads into the existing device-local buffers instead of allocating replacement buffers. Keep this for focused A/Bs; rotating update is the cleaner default candidate because it avoids full in-flight fence waits.

`DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE=1` is a backward-compatible explicit override for `DUXEL_VK_STATIC_GEOMETRY_UPDATE=rotating`. Same-shape content changes retire the current static geometry buffer into a frame-safe reuse pool, then activate a safe retired buffer when one is available or create a new buffer to seed the pool. The retired pool is capped per tag to the frame count and idle retired buffers are trimmed after `StaticGeometryRetiredReuseGraceFrames`. This is the default resolved policy on validated NVIDIA discrete GPUs and remains explicitly available for vendor/device gates elsewhere.

`DUXEL_VK_COMMAND_SCHEDULER=all` or `1` enables the opt-in overlap-constrained Vulkan command scheduler for every eligible draw list. `DUXEL_VK_COMMAND_SCHEDULER=static` enables the same scheduler only for static-layer replay draw lists, which keeps dynamic whole-list scheduling out of the hot path while preserving the measured static-layer replay win. The scheduler uses `UiDrawCommand` coverage bounds to preserve required overlap order while grouping ready commands by pipeline class, then merges newly adjacent compatible commands during recording. In `all` mode, dynamic draw-list construction also computes `UiDrawList.CommandScheduleStamp` so cache-hit scheduling does not repeat full command hash/compare work in Vulkan command recording. It is not a default path: use it only with focused profiling, and keep `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW` bounded so broad scenes do not pay unbounded scheduling cost.

Static cached layer replay computes a conservative command schedule shape stamp once during layer capture when `DUXEL_VK_COMMAND_SCHEDULER=static`, `all`, or `1`. The stamp is based on stable local command shape and raw local command bounds. Vulkan scheduling uses the same conservative bounds for static-layer draw lists so moving or clipping a stable layer can reuse a cached safe order instead of repeating overlap analysis or per-frame replay-shape hashing.

When a dirty static layer rebuilds to the same draw-list shape, Core should refresh the existing `UiLayerCachedList` storage in place instead of allocating new local vertex/index/command/primitive arrays. Recompute `StaticGeometryStamp` and command schedule shape stamp after the overwrite, keep the stable `duxel.layer.static:{layerId}:list:{i}` key, and invalidate replay so opacity/translation/clip commands are rebuilt under the updated content stamp.

`DUXEL_UI_COMMAND_SCHEDULER=1` enables the earlier builder-stage scheduling experiment in `UiDrawListBuilder.Flush()`. Keep this separate from `DUXEL_VK_COMMAND_SCHEDULER`: dynamic whole-list builder scheduling can cost more than it saves when draw-list content changes every frame, so it is for structural experiments only. Prefer stable layer/static schedule caching or explicit draw channels before considering it for a default path.

For GPU timestamp profiling, also set `DUXEL_VK_GPU_PROFILE=1`. It only activates when the selected graphics queue supports timestamps, and profile lines add `gpuRender=...` with render-pass GPU execution time in microseconds. Use it to separate CPU command-recording cost from shader/GPU-side cost.

Static cached rect/circle primitive expansion to triangle vertex/index geometry defaults to `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES=auto`. The automatic policy enables it only on NVIDIA/AMD discrete GPUs, then applies per-draw-list byte and mutation guards. `auto` skips expansion when estimated expanded vertex/index bytes exceed `32x` the primitive-instance bytes; it also suppresses expansion for a static geometry tag for `30` frames after the tag's content hash changes, so mutating static layers use the upload-cheaper primitive-instance path until stable. Force expansion with `1/true/on` or disable it with `0/false/off`. This path keeps cached static draw-list command order, clipping, opacity, and texture state while letting expanded primitives ride the indexed triangle draw mode. Use profile `staticPrim(...)` counters to verify actual expand/skip decisions instead of relying only on the device policy block.

The `staticPrim(...)` profile block includes `layout=...`, the number of expanded primitive layouts materialized during that frame. A stable static circle cache should materialize layouts only on creation/rebuild (`layout>0`) and report `layout=0` on cache-hit frames; a nonzero steady-hit value indicates repeated CPU layout work.

The implementation boundary for static primitive auto decisions is `VulkanRendererBackend.StaticPrimitivePolicy.cs`; static geometry code should call this policy boundary instead of embedding device/heuristic decisions in upload code.

### GPU-driven renderer architecture (2026-07-03)

The Vulkan backend is a GPU-driven renderer with exactly **one graphics pipeline**, **one bindless texture descriptor set**, and **two shaders** (`Shaders/imgui.vert`, `Shaders/imgui.frag`):

- **Bindless textures**: one global `sampler2D[]` combined-image-sampler array (capacity 4096, UPDATE_AFTER_BIND + PARTIALLY_BOUND), bound once per frame. Textures get slot indices from a free-list allocator in `TextureResources`; slots are recycled at the deferred texture-destroy point. There are no per-texture descriptor sets.
- **Unified dual-source blending**: every draw outputs premultiplied color plus a per-channel blend factor (`One/OneMinusSrc1Color`, `One/OneMinusSrc1Alpha`). Standard draws emit `blendFactor = vec4(alpha)` (exactly equivalent to `SrcAlpha/OneMinusSrcAlpha`); ClearType subpixel text emits per-channel coverage, selected by the high bit of the fragment texture-index push constant.
- **Vertex pulling**: the pipeline has no vertex input state. The vertex shader reads `UiVertex` streams and packed `PrimitiveInstance` records (both 20-byte/5-dword layouts) through buffer device addresses (`GL_EXT_buffer_reference`).
- **Push constant layout**: vertex range `[0,40)` = `scale`(8) + `translate`(8) + `opacity`(4) + `drawMode`(4) + vertex-buffer address(8) + primitive-buffer address(8); fragment range `[40,44)` = packed texture index + subpixel mode bit. `drawMode` 0 = indexed triangle pulling, 1 = primitive instance expansion (rect corners / circle fans in-shader).
- **Per-draw state**: `vkCmdBindPipeline` happens once per frame; per-draw variation is only push constants, index-buffer binds, and dynamic scissor. `vkCmdBindVertexBuffers` no longer exists in the backend.
- **Dynamic rendering**: there are no render pass or framebuffer objects. Frames record `vkCmdBeginRenderingKHR`/`vkCmdEndRenderingKHR` with `RenderingAttachmentInfo`; MSAA resolves inline (`resolveMode = AVERAGE`, MSAA target → swapchain image). Explicit image barriers replace implicit render-pass transitions: `UNDEFINED→COLOR_ATTACHMENT` before rendering and `COLOR_ATTACHMENT→PRESENT_SRC` after. The pipeline chains `PipelineRenderingCreateInfo` with the swapchain format.
- **Required device features** (explicit failure when missing, no fallback): `shaderSampledImageArrayDynamicIndexing`, `descriptorBindingSampledImageUpdateAfterBind`, `descriptorBindingUpdateUnusedWhilePending`, `descriptorBindingPartiallyBound`, `runtimeDescriptorArray`, `bufferDeviceAddress`, `dualSrcBlend`, `dynamicRendering` (`VK_KHR_dynamic_rendering`).
- **Dynamic geometry memory**: per-frame vertex/primitive buffers prefer BAR memory (`DEVICE_LOCAL|HOST_VISIBLE|HOST_COHERENT`) and fall back to the stated host-visible requirement when the device lacks it. This preference is required for vertex-pulling performance: shader BDA reads from plain host memory cross PCIe per vertex and measurably regress dynamic scenes.

### Renderer module map

`VulkanRendererBackend` is organized into 19 partial-class modules. Extend the matching module instead of adding new single-purpose files:

- `VulkanRendererBackend.cs` — `IRendererBackend` shell, constructor/bootstrap order, lifecycle/teardown, render-entry frame order, settings/API mutation.
- `VulkanRendererBackend.Device.cs` — instance/surface/physical-device/queue/device setup, required-feature gating, vendor/device policy state and env parsing, pipeline cache identity.
- `VulkanRendererBackend.Swapchain.cs` — swapchain selection policy, resize-only versus full recreate flow, image views/MSAA targets, semaphore capacity, and preservation of non-swapchain renderer resources during live resize.
- `VulkanRendererBackend.PipelineResources.cs` — bindless descriptor layout/pool/set, pipeline layout/push ranges, the single graphics pipeline, samplers, pipeline cache load/save, embedded shader loading.
- `VulkanRendererBackend.Resources.cs` — generic buffer/image/view/memory helpers (`CreateBuffer` with preferred-memory selection, `GetBufferDeviceAddress`), image layout transition policy and `imgTrans(...)` counters.
- `VulkanRendererBackend.TextureResources.cs` — texture create/update/destroy, bindless slot allocator and descriptor writes, upload batching, font/white texture id classification, deferred destroys.
- `VulkanRendererBackend.UploadScheduler.cs` — staging buffer lifetime, upload batching (`DUXEL_VK_UPLOAD_BATCH`), upload queue policy (`DUXEL_VK_UPLOAD_QUEUE`), submission/wait accounting.
- `VulkanRendererBackend.DynamicGeometry.cs` — per-frame vertex/index/primitive buffer capacity/mapping (BAR-preferred), dynamic geometry upload, frame geometry preparation.
- `VulkanRendererBackend.StaticGeometry.cs` — static cache identity/lifetime/LRU, content/shape derivation, frame preparation, policy application, replacement policy.
- `VulkanRendererBackend.StaticGeometryStorage.cs` — static device-local buffer materialization (with device addresses), static upload/layout writing, retired-buffer pool.
- `VulkanRendererBackend.StaticPrimitivePolicy.cs` — static primitive triangle-expansion auto policy (`DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`), byte/mutation guards, decision profiling.
- `VulkanRendererBackend.Frame.cs` — frame orchestration (acquire/submit/present/fences), shared frame-fence waits, frame command-recording preparation, frame completion.
- `VulkanRendererBackend.CommandRecording.cs` — `RecordCommandBuffer` (binds the bindless set and the single pipeline once), frame/render-pass begin/end, recording state aggregation, frame/draw-list contexts, draw-list traversal.
- `VulkanRendererBackend.CommandState.cs` — draw-mode push state, texture-index push state, vertex/primitive address push + index-buffer bind state, transform/opacity push state, scissor state.
- `VulkanRendererBackend.CommandDraw.cs` — draw-path orchestration, draw dispatch (indexed/instanced), command classification, texture lookup cache, primitive instance encoding.
- `VulkanRendererBackend.CommandScheduling.cs` — opt-in overlap-constrained command scheduler (`DUXEL_VK_COMMAND_SCHEDULER`), schedule cache, merge expansion.
- `VulkanRendererBackend.CommandDiagnostics.cs` — command diag gating/labels (`DUXEL_VK_COMMAND_DIAG`), font diagnostics (`DUXEL_VK_FONT_DIAG`), record profile counters and `CommandRecordStats` construction.
- `VulkanRendererBackend.Diagnostics.cs` — profile line output, device/policy text, GPU timestamp interpretation, failure tracing.
- `VulkanRendererBackend.Types.cs` — shared renderer records (`StaticGeometryBuffer` with device addresses, `TextureResource` with slot index, frame resources).

Upload command pooling, staging buffer lifetime, `DUXEL_VK_UPLOAD_BATCH`, `DUXEL_VK_UPLOAD_QUEUE`, staging offset reservation, batch flush, graphics upload-prepare command submission, transfer upload-copy submission, and single-time upload submission belong in `VulkanRendererBackend.UploadScheduler.cs`. Texture updates and static geometry upload code should call this boundary instead of growing their own staging command paths. Upload batching defaults to enabled; set `DUXEL_VK_UPLOAD_BATCH=0`, `false`, or `off` only for diagnosis. The transfer upload path is opt-in only: keep the default graphics path until focused texture/page upload gates prove the split path is faster on the target vendor/device. On the local NVIDIA `10de:2f58` gate, transfer was slower by about `12-13%` on generic texture updates and `15-19%` on DirectText page updates because `upSched(prepSub=1 ...)` and higher upload waits outweighed shorter transfer-copy submissions.

Image layout transition policy stays in the `Resources` module. Use `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)` to prove whether texture/page uploads are barrier-heavy or copy-heavy; `texture_upload_barrier_bench_fba.cs` should show same-texture non-overlapping region batches at `total=2` and many-texture updates at `total=2 * textureCount`. `xferStage` means the stage masks are transfer-queue-compatible; `gfxStage` means graphics/fragment/color stage masks must be split or moved before transfer-only upload recording.

For font command diagnostics, set `DUXEL_VK_FONT_DIAG=1`. Use `DUXEL_VK_FONT_DIAG_OUT` to redirect the verbose command log to a file.

For general Vulkan command sequence diagnostics, set `DUXEL_VK_COMMAND_DIAG=1`. Use `DUXEL_VK_COMMAND_DIAG_OUT` to log each draw command's `pipe`, texture id, clip, and static-binding status for pipeline-switch analysis. Logging is limited by default with `DUXEL_VK_COMMAND_DIAG_EVERY=120` and `DUXEL_VK_COMMAND_DIAG_FRAMES=1`.

Draw-list command merging must treat callback-free zero-element commands as state placeholders, not draw-order barriers. When adding a real draw, the builder removes a trailing empty placeholder before testing contiguous command merge eligibility; keep opacity, texture, clip, translation, kind, vertex offset, callback, and user data in the merge predicate. Prefer clipped draw helpers such as clipped `AddImage(...)` when the caller already knows the effective clip, instead of pushing/popping the builder clip stack around one draw.

Sampler-free pipeline variants no longer exist: since the GPU-driven renderer migration every draw samples the bindless texture array, solid draws sample the app 1x1 white texture, and pipeline selection cost is gone because there is only one pipeline. `DUXEL_VK_TRIANGLE_COLOR_PIPELINE`, `DUXEL_VK_SOLID_UNIFIED_PIPELINE`, and `DUXEL_VK_SOLID_UNIFIED_STATIC` were removed together with the split pipelines and the solid-triangle sentinel; do not reintroduce them.

For text renderer A/B profiling, set `DUXEL_TEXT_RENDERING=direct`, `atlas`, or `auto`. `DirectText` is the default. Explicit `atlas` forces the atlas renderer for comparison, and explicit `auto` keeps atlas-first rendering but uses DirectText as an immediate visual fallback for missing atlas glyphs; use it only when that fallback behavior is intentional.

DirectText page-texture packing is enabled only with `DUXEL_DIRECT_TEXT_PAGE=1`. It packs DirectText bitmaps into 1024x1024 page textures, reserves a 1px border around each region, and extrudes edge pixels into that border to reduce atlas sampling bleed. Page creation uploads only the border-inclusive packed region, and consecutive non-overlapping partial updates for the same texture can be batched into one Vulkan upload/copy submission. Use `samples/fba/directtext_page_upload_bench_fba.cs` as the focused upload-policy gate for this path before changing upload batching or transfer-queue defaults. DirectText page quads should use the clipped image helper rather than a `PushClipRect -> AddImage -> PopClipRect` sequence. It is off by default to preserve text appearance and must not become a default path without rendered visual validation.

As of 2026-07-10, a page-enabled performance run reached `1819.709` FPS, but the rendered all-features capture was mostly black with the UI missing. Treat that result as a rejected diagnostic candidate, keep page mode off by default, and require a passing visual comparison before reconsidering it.

## Constraints and non-goals

Assume these repository rules are intentional unless the task explicitly changes them:

- .NET 9 and .NET 10 package targets
- Windows-first platform reality
- Vulkan renderer requirement
- NativeAOT-friendly direction
- no casual fallback-path expansion
- no defensive-code sprawl used as a substitute for root-cause fixes

When something is unsupported, prefer explicit failure or explicit scope boundaries over hidden fallback behavior.

## How an agent should approach Duxel work

Recommended sequence:

1. identify whether the task is about core UI behavior, app bootstrapping, Windows platform behavior, Vulkan rendering, DSL, or docs
2. read the nearest relevant section of this document, then the nearest relevant sample if an example is needed
3. preserve architectural boundaries
4. make the smallest change that solves the actual problem
5. validate with a build or a relevant sample run
6. update docs if the change affects public behavior, workflow, or extension points
7. update this document when the stable guidance changes

## Documentation links

Use these documents together with this reference:

- `README.md` — package-facing overview and quick start
- `docs/getting-started-fba.md` — first-time FBA walkthrough
- `docs/fba-reference-guide.md` — package/project switching and `run-fba.ps1`
- `docs/fba-run-samples.md` — sample catalog and one-command runs
- `docs/custom-widgets.md` — reusable widget extension path
- `docs/ui-dsl.md` — declarative UI reference
- `docs/version-history.md` — grouped release history
- `docs/design.ko.md` — current architecture and design baseline

## Maintenance note

If you learn a stable workflow rule that future agents or developers need in order to use Duxel correctly, add it here and refresh the `Last synced` date.
