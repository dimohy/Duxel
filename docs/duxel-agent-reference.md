# Duxel Agent Reference

> Last synced: 2026-03-14
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

Duxel is an immediate-mode GUI framework for .NET 10.

Current implementation direction:

- renderer: Vulkan
- primary platform: Windows-native backend
- runtime style: NativeAOT-friendly
- main UI API: `UiImmediateContext`
- app lifecycle entry: `UiScreen.Render(UiImmediateContext ui)`
- package surface:
  - `Duxel.App`
  - `Duxel.Windows.App`

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
- `run-fba.ps1` — contributor path for local-source FBA validation

## Architecture boundaries

Preserve these boundaries when adding or modifying features:

- `Duxel.Core`
  - owns immediate-mode behavior, layout, widgets, draw list generation, state, text-facing APIs
- `Duxel.Platform.Windows`
  - owns Windows-specific behavior such as input, clipboard, text backend integration, and native windowing
- `Duxel.Vulkan`
  - owns rendering, GPU resources, swapchain flow, and submission
- `Duxel.App` and `Duxel.Windows.App`
  - own app bootstrapping, developer-facing entry flow, option validation, and runtime wiring

Do not casually collapse these boundaries.

## Primary public entry points

The most important downstream-facing surfaces are:

- `UiScreen`
- `UiImmediateContext`
- `DuxelAppOptions`
- `DuxelWindowOptions`
- `DuxelRendererOptions`
- `DuxelFontOptions`
- `DuxelFrameOptions`
- `DuxelDebugOptions`
- `DuxelDslOptions` (legacy compatibility surface; app runtime entry unsupported)
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
| `Theme` | `UiTheme` | `UiTheme.ImGuiDark` | theme preset |
| `FontTextureId` | `UiTextureId` | `new(1)` | font texture slot |
| `WhiteTextureId` | `UiTextureId` | `new(2)` | white texture slot |
| `Screen` | `UiScreen?` | `null` | immediate-mode app entry |
| `Dsl` | `DuxelDslOptions?` | `null` | legacy compatibility field; `DuxelApp` runtime entry is unsupported |
| `Clipboard` | `IUiClipboard?` | `null` | direct clipboard injection |
| `ImageDecoder` | `IUiImageDecoder?` | `null` | custom image decode path |
| `KeyRepeatSettingsProvider` | `IKeyRepeatSettingsProvider?` | `null` | custom key repeat timing |
| `ClipboardFactory` | `Func<IPlatformBackend, IUiClipboard?>?` | `null` | platform-aware clipboard factory |

### `DuxelWindowOptions`

| Property | Default |
|---|---|
| `Width` | `1280` |
| `Height` | `720` |
| `Title` | `"Duxel"` |
| `VSync` | `true` |

### `DuxelRendererOptions`

| Property | Default |
|---|---|
| `MinImageCount` | `3` |
| `EnableValidationLayers` | `Debugger.IsAttached` |
| `Profile` | `DuxelPerformanceProfile.Display` |
| `MsaaSamples` | `0` |
| `EnableGlobalStaticGeometryCache` | `true` |
| `FontLinearSampling` | `false` |

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
| `InitialGlyphs` | empty |

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

### `DuxelDslOptions`

`DuxelDslOptions` remains only as a compatibility surface. Passing it to `DuxelApp` now fails explicitly.

| Property | Required | Purpose |
|---|---|---|
| `Render` | yes | DSL emitter callback |
| `Bindings` | no | convenient binding object |
| `EventSink` | no | event sink |
| `ValueSource` | no | external value source |
| `State` | no | built-in DSL state store |

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

### Minimal DSL app

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var text = """
Window "DSL Demo"
  Text "Hello DSL"
  Checkbox Id="enabled" Text="Enabled" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
""";

var document = UiDslParser.Parse(text);

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions { Title = "DSL Demo" },
  Dsl = new DuxelDslOptions
  {
    State = new UiDslState(),
    Render = emitter => document.Emit(emitter),
  },
});
```

## Core app patterns

### Immediate-mode screen pattern

Use `UiScreen` when building a normal app or sample screen.

- override `Render(UiImmediateContext ui)`
- rebuild the UI every frame
- keep your own state on the screen instance or external model

Default recommendation: if the user simply asks for a Duxel sample, generate an immediate-mode `UiScreen` example unless they explicitly ask for DSL.

### DSL pattern

Use the `.ui` DSL when the task is declarative UI authoring, state binding, or generator-based workflows.

- parser: `UiDslParser`
- runtime execution: `UiDslDocument.Emit(...)`
- binding path: `UiDslBindings`, `IUiDslValueSource`, `IUiDslEventSink`
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
| Declarative DSL | `samples/fba/dsl_showcase.cs`, `samples/fba/dsl_interaction.cs` |
| Layout and style control | `samples/fba/advanced_layout.cs` |
| Legacy columns | `samples/fba/columns_demo.cs` |
| Menus and submenu overlap behavior | `samples/fba/menu_submenu_zorder.cs` |
| Images, popups, tooltip patterns | `samples/fba/image_and_popups.cs` |
| Image effects and animated images | `samples/fba/image_widget_effects_fba.cs` |
| Keyboard and mouse query APIs | `samples/fba/input_queries.cs` |
| Item lifecycle and status queries | `samples/fba/item_status.cs` |
| Text alignment and render validation | `samples/fba/text_render_validation_fba.cs` |
| Font style and size validation | `samples/fba/font_style_validation_fba.cs` |
| Layer cache and dirty strategy behavior | `samples/fba/idle_layer_validation.cs`, `samples/fba/layer_dirty_strategy_bench.cs`, `samples/fba/global_dirty_strategy_bench.cs` |
| Draw-layer and widget mixing | `samples/fba/layer_widget_mix_bench_fba.cs` |
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

Why:

- it rewrites package directives to local project references
- it normalizes Windows entry calls when needed
- it supports NativeAOT by default
- it reflects workspace source changes, unlike plain package-mode execution

Do not rely on `dotnet run samples/fba/<file>.cs` to validate local source edits.

## Build and validation

Default repository build:

- `dotnet build Duxel.slnx -c Release`

Minimum validation expectation after meaningful code changes:

- build the solution, or
- run at least one relevant sample path

Prefer the smallest relevant validation that still proves the change.

## Environment variables and runtime toggles

Common runtime toggles used across samples and diagnostics include:

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

When a task depends on a runtime toggle, verify the exact name and meaning in the relevant sample before documenting or using it.

## Constraints and non-goals

Assume these repository rules are intentional unless the task explicitly changes them:

- .NET 10 target
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