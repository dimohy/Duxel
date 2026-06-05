# Duxel Agent Reference

> Last synced: 2026-06-06
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
| `Screen` | `UiScreen` | (required) | immediate-mode app entry |
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
- `DUXEL_VK_SOLID_UNIFIED_PIPELINE`
- `DUXEL_VK_SOLID_UNIFIED_STATIC`
- `DUXEL_VK_TRIANGLE_COLOR_PIPELINE`

When a task depends on a runtime toggle, verify the exact name and meaning in the relevant sample before documenting or using it.

For Vulkan command-recording profiling, set `DUXEL_VK_PROFILE=1`. `DUXEL_VK_PROFILE_EVERY` controls the log interval in frames, and `DUXEL_VK_PROFILE_OUT` appends profile lines to a file. Profile lines include `device(vendor=... vid=... did=... type=... name=... gfxQ=... uploadQ=... xferCandQ=... tsBits=... tsPeriodNs=... gpuTs=...)` and `policy(upload=... transferCandidate=... triColor=... solidUnified=... solidUnifiedStatic=... staticPrimTri=... staticUpdate=... staticUpdateReq=... scheduler=... schedWindow=... staticSecondaryMin=...)` so device/vendor gates remain attributable when artifacts from NVIDIA, AMD, Intel, and integrated/discrete devices are compared later. `uploadQ` is the queue family currently used for upload command buffers; `xferCandQ` is only the detected non-graphics transfer-capable candidate. `policy(upload=graphics transferCandidate=1)` therefore means a candidate exists, not that uploads are already using it. `DUXEL_VK_UPLOAD_QUEUE=transfer` opts into the split transfer upload path only when that candidate exists; `policy(upload=transfer transferCandidate=1)` is the profile proof that upload copy command buffers are using it. `staticUpdateReq` is the requested mode and `staticUpdate` is the resolved device policy. `scheduler` is the resolved command-scheduler mode: `disabled`, `static`, or `all`. Profile lines also include `stateUs(pipe=... desc=... buf=... push=... scissor=...)` to separate pipeline binds, descriptor binds, vertex/index/primitive buffer binds, push constants, and scissor sets. `clipCache(calc=... reuse=...)` reports actual scissor rectangle computation count and consecutive same clip/translation reuse count. `staticSec(cand=... cmds=... draws=...)` reports static draw lists that crossed the active `staticSecondaryMin` threshold, plus the recorded commands/draws inside those candidates; `cand=0` means secondary command buffers are not a useful next-path hypothesis for that frame even if static lists exist. `listWork(staticCmd=... dynCmd=... staticDraw=... dynDraw=... staticPipe=... dynPipe=... staticClip=... dynClip=... staticScissor=... dynScissor=... staticPush=... dynPush=... staticGeom=... dynGeom=... staticPrim=... dynPrim=...)` attributes command-recording work to static cached replay versus dynamic draw lists. Use it to decide whether the next bottleneck is static replay policy or dynamic UI ordering/channelization. `staticGeom(hit=... create=... replace=... update=... reuse=... hash=...)` reports static geometry cache reuse, buffer creation, content replacement, same-shape in-place content update, same-shape rotating-buffer reuse, and fallback content hashing in the upload phase. `staticMem(active=... activeBytes=... retired=... retiredBytes=...)` reports active static geometry entries/bytes and retired rotating-pool entries/bytes for memory-pressure checks. `staticPrim(expand=... expandPrim=... force=... autoSkip=... autoSkipPrim=... autoSkipMut=... expandBytes=... autoSkipBytes=...)` reports per-frame static primitive triangle expansion decisions, which is necessary because `policy(... staticPrimTri=1 ...)` only says the device policy allows the path. `autoSkipMut` is the subset of skipped lists suppressed because the same static geometry tag is actively changing content. `pipeClass(font=... texTri=... colorTri=... texPrim=... colorPrim=... solid=...)` reports the actual pipeline class selected after renderer policy, which is more precise for pipeline-switch analysis than the original source command kind alone. `sched(probe=... hit=... miss=... nochange=... lists=... merged=... us=...)` reports opt-in command scheduler activity and cost. `upSched(sub=... prepSub=... wait=... flush=... bytes=... texRegions=... bufCopies=... submitUs=... prepUs=... waitUs=...)` reports staging upload scheduler submissions, split-transfer graphics-prepare submissions, upload fence waits, batch flushes, staged byte volume, texture copy regions, static/buffer copy commands, and their submission/wait costs. `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)` reports image layout transition counts and separates transfer-queue-compatible stage masks from barriers that still require graphics stages. `state=` excludes scissor because scissor is also part of the `clip=` bucket, so interpret scissor separately.

Performance demos should match the specific bottleneck being tested. Use broad layer-widget scenes as final regressions, but add or pick a focused FBA demo when the question is narrow. `samples/fba/pipeline_ordering_bench_fba.cs` is the focused gate for dynamic solid/text pipeline ordering cost; it includes alternating, grouped solids-then-text, copy-merge `channelized-solid-text`, and copy-free `channel-drawlists-solid-text` phases and uses `DUXEL_PIPELINE_ORDER_BENCH_OUT`, `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`, and `DUXEL_PIPELINE_ORDER_ITEMS`. `samples/fba/dynamic_widget_ordering_bench_fba.cs` is the focused gate for widget-like dynamic producer ordering with row-clip churn; it includes alternating widget rows, grouped solids-then-text, copy-merge channelized, and copy-free channel-drawlists phases, and uses `DUXEL_DYN_WIDGET_ORDER_BENCH_OUT`, `DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_ITEMS`, and `DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS`. `samples/fba/vector_primitives_bench_fba.cs` is the focused gate for primitive-heavy geometry and accepts `DUXEL_VECTOR_BENCH_WORKLOAD=mixed`, `rect-outline`, or `axis-line`, plus `DUXEL_VECTOR_BENCH_OUT`, `DUXEL_VECTOR_BENCH_PHASE_SECONDS`, and `DUXEL_VECTOR_BENCH_COUNTS`. `samples/fba/Duxel_perf_test_fba.cs` is the polygon physics/perf smoke; it defaults to the Render profile and global static backdrop cache, `DUXEL_PERF_PROFILE=render|display` can override the startup profile, `DUXEL_PERF_GLOBAL_STATIC_CACHE=0` disables the retained static backdrop references, and the Render Profile checkbox applies the profile immediately by switching MSAA between `1x` and `4x`. `samples/fba/global_dirty_strategy_bench.cs` is the focused gate for global static-background caching versus dynamic overlay updates; `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS` compares channel copy-merge against separate draw-list output for that exact background/overlay channel structure. `samples/fba/static_layer_moving_order_bench_fba.cs` is the focused gate for moving static-layer replay schedule reuse; it uses `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`, `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`, and `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`. `samples/fba/static_cache_rebuild_bench_fba.cs` is the focused gate for cache replay, false-dirty static rebuilds, mutating static geometry replacement/update cost, Core cache-copy allocation pressure, and static primitive triangle memory pressure; it reports `avgAllocatedBytes` per measured frame and uses `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`, `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_LAYERS`, `DUXEL_STATIC_CACHE_REBUILD_DENSITY`, `DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE` (`circles`, `rects`, or `mixed`), `DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS`, and optional `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW` for GPU-bound variants. `samples/fba/texture_upload_barrier_bench_fba.cs` is the focused gate for texture upload copy/barrier behavior before transfer-queue policy changes; it uses `DUXEL_TEXTURE_UPLOAD_BENCH_OUT`, `DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS`, `DUXEL_TEXTURE_UPLOAD_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGION_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGIONS`, `DUXEL_TEXTURE_UPLOAD_TEXTURES`, and `DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES`. `samples/fba/directtext_page_upload_bench_fba.cs` is the focused gate for DirectText page-style partial texture upload behavior without platform glyph rasterizer cost; it uses `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`, `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`, `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`, `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`, `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`, `DUXEL_DTPAGE_UPLOAD_REGIONS`, `DUXEL_DTPAGE_UPLOAD_PAGES`, and `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`.

Axis-aligned `UiDrawListBuilder.AddRect(...)` outlines with `rounding <= 0` should use rect-filled primitive emission instead of triangle polyline geometry. Horizontal and vertical `UiDrawListBuilder.AddLine(...)` calls should also use rect-filled primitive emission; diagonal lines keep the quad triangle path. This keeps ordinary rectangular borders and axis-aligned separators on the primitive path and reduces text/triangle/primitive pipeline churn. Rounded outlines still use the polyline path.

`DUXEL_VK_STATIC_GEOMETRY_UPDATE` controls same-shape static geometry content changes. Valid values are `auto`, `replace`, `inplace`, and `rotating`. `auto` resolves to `rotating` on validated NVIDIA discrete GPUs and to `replace` elsewhere until AMD/Intel and broader NVIDIA gates prove the same policy. Use `replace` to force allocation/replacement, `inplace` to force fence-waited reupload into existing buffers, and `rotating` to force retired-buffer reuse.

`DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE=1` is a backward-compatible explicit override for `DUXEL_VK_STATIC_GEOMETRY_UPDATE=inplace`. If a static geometry cache entry has the same vertex/index/primitive counts and the same expanded primitive triangle layout, Vulkan waits for all in-flight frame fences and reuploads into the existing device-local buffers instead of allocating replacement buffers. Keep this for focused A/Bs; rotating update is the cleaner default candidate because it avoids full in-flight fence waits.

`DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE=1` is a backward-compatible explicit override for `DUXEL_VK_STATIC_GEOMETRY_UPDATE=rotating`. Same-shape content changes retire the current static geometry buffer into a frame-safe reuse pool, then activate a safe retired buffer when one is available or create a new buffer to seed the pool. The retired pool is capped per tag to the frame count and idle retired buffers are trimmed after `StaticGeometryRetiredReuseGraceFrames`. This is the default resolved policy on validated NVIDIA discrete GPUs and remains explicitly available for vendor/device gates elsewhere.

`DUXEL_VK_COMMAND_SCHEDULER=all` or `1` enables the opt-in overlap-constrained Vulkan command scheduler for every eligible draw list. `DUXEL_VK_COMMAND_SCHEDULER=static` enables the same scheduler only for static-layer replay draw lists, which keeps dynamic whole-list scheduling out of the hot path while preserving the measured static-layer replay win. The scheduler uses `UiDrawCommand` coverage bounds to preserve required overlap order while grouping ready commands by pipeline class, then merges newly adjacent compatible commands during recording. In `all` mode, dynamic draw-list construction also computes `UiDrawList.CommandScheduleStamp` so cache-hit scheduling does not repeat full command hash/compare work in Vulkan command recording. It is not a default path: use it only with focused profiling, and keep `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW` bounded so broad scenes do not pay unbounded scheduling cost.

Static cached layer replay computes a conservative command schedule shape stamp once during layer capture when `DUXEL_VK_COMMAND_SCHEDULER=static`, `all`, or `1`. The stamp is based on stable local command shape and raw local command bounds. Vulkan scheduling uses the same conservative bounds for static-layer draw lists so moving or clipping a stable layer can reuse a cached safe order instead of repeating overlap analysis or per-frame replay-shape hashing.

When a dirty static layer rebuilds to the same draw-list shape, Core should refresh the existing `UiLayerCachedList` storage in place instead of allocating new local vertex/index/command/primitive arrays. Recompute `StaticGeometryStamp` and command schedule shape stamp after the overwrite, keep the stable `duxel.layer.static:{layerId}:list:{i}` key, and invalidate replay so opacity/translation/clip commands are rebuilt under the updated content stamp.

`DUXEL_UI_COMMAND_SCHEDULER=1` enables the earlier builder-stage scheduling experiment in `UiDrawListBuilder.Flush()`. Keep this separate from `DUXEL_VK_COMMAND_SCHEDULER`: dynamic whole-list builder scheduling can cost more than it saves when draw-list content changes every frame, so it is for structural experiments only. Prefer stable layer/static schedule caching or explicit draw channels before considering it for a default path.

For GPU timestamp profiling, also set `DUXEL_VK_GPU_PROFILE=1`. It only activates when the selected graphics queue supports timestamps, and profile lines add `gpuRender=...` with render-pass GPU execution time in microseconds. Use it to separate CPU command-recording cost from shader/GPU-side cost.

Static cached rect/circle primitive expansion to triangle vertex/index geometry defaults to `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES=auto`. The automatic policy enables it only on NVIDIA/AMD discrete GPUs when the triangle color pipeline is enabled, then applies per-draw-list byte and mutation guards. `auto` skips expansion when estimated expanded vertex/index bytes exceed `32x` the primitive-instance bytes; it also suppresses expansion for a static geometry tag for `30` frames after the tag's content hash changes, so mutating static layers use the upload-cheaper primitive-instance path until stable. Force expansion with `1/true/on` or disable it with `0/false/off`. This path keeps cached static draw-list command order, clipping, opacity, and texture state while reducing static primitive buffer binds and avoiding the primitive shader path. Use profile `staticPrim(...)` counters to verify actual expand/skip decisions instead of relying only on the device policy block.

The implementation boundary for static primitive auto decisions is `VulkanRendererBackend.StaticPrimitivePolicy.cs`; `VulkanRendererBackend.StaticGeometry.cs` should call this policy boundary instead of embedding device/heuristic decisions in upload code.

Upload command pooling, staging buffer lifetime, `DUXEL_VK_UPLOAD_BATCH`, `DUXEL_VK_UPLOAD_QUEUE`, staging offset reservation, batch flush, graphics upload-prepare command submission, transfer upload-copy submission, and single-time upload submission belong in `VulkanRendererBackend.UploadScheduler.cs`. Texture updates and static geometry upload code should call this boundary instead of growing their own staging command paths. Upload batching defaults to enabled; set `DUXEL_VK_UPLOAD_BATCH=0`, `false`, or `off` only for diagnosis. The transfer upload path is opt-in only: keep the default graphics path until focused texture/page upload gates prove the split path is faster on the target vendor/device. On the local NVIDIA `10de:2f58` gate, transfer was slower by about `12-13%` on generic texture updates and `15-19%` on DirectText page updates because `upSched(prepSub=1 ...)` and higher upload waits outweighed shorter transfer-copy submissions.

Rotating static geometry retired-buffer pool ownership belongs in `VulkanRendererBackend.StaticGeometryRetiredPool.cs`. Keep frame-safe availability checks, per-tag pool caps, idle pruning, retired memory stats, and retired-buffer teardown there; `VulkanRendererBackend.StaticGeometry.cs` should call that boundary from active cache creation/replacement/prune/teardown code.

Active static geometry cache identity and lifetime belong in `VulkanRendererBackend.StaticGeometryCache.cs`. Keep static tag recognition, active entry get/set, seen-frame tracking, LRU prune, active memory stats, and active-buffer teardown there; `VulkanRendererBackend.StaticGeometry.cs` should focus on same-shape replacement policy orchestration.

Static geometry upload/layout writing belongs in `VulkanRendererBackend.StaticGeometryUpload.cs`. Keep static vertex/index/primitive staging writes, expanded primitive triangle layout/writers, primitive instance writers, and solid sentinel reservation helpers there.

Same-shape static geometry replacement policy belongs in `VulkanRendererBackend.StaticGeometryReplacementPolicy.cs`. Keep `StaticGeometryShape`, cache-entry match checks, resource presence checks, and in-place compatibility checks there; `VulkanRendererBackend.StaticGeometry.cs` should orchestrate the selected path and call cache/upload/retired-pool boundaries.

Dynamic per-frame geometry upload belongs in `VulkanRendererBackend.DynamicGeometryUpload.cs`. Keep dynamic draw-list index ownership, mapped frame vertex/index writes, dynamic primitive instance writes, and dynamic primitive sentinel count there; `VulkanRendererBackend.StaticGeometry.cs` should only prepare static bindings and dynamic counts, while the frame loop should call dynamic upload and static cache pruning as separate steps.

Dynamic frame geometry buffer ownership belongs in `VulkanRendererBackend.GeometryBuffers.cs`. Keep dynamic frame vertex/index/primitive buffer capacity growth, host-visible memory mapping/unmapping, queued replacement destroy, and frame geometry buffer teardown there. `VulkanRendererBackend.FrameGeometry.cs` should decide capacity requirements and call this boundary; lifecycle and swapchain teardown should call `DestroyGeometryBuffers()`.

Static geometry buffer materialization belongs in `VulkanRendererBackend.StaticGeometryMaterialization.cs`. Keep static device-local buffer allocation, common static vertex/index/primitive upload fan-out, create-path materialization, and same-shape reupload materialization there; `VulkanRendererBackend.StaticGeometry.cs` should choose the high-level cache/reuse/update/replace/create policy and call this boundary.

Per-frame static/dynamic draw-list preparation belongs in `VulkanRendererBackend.StaticGeometryFramePreparation.cs`. Keep static binding dictionary ownership, dynamic vertex/index/primitive counters, static draw-list binding classification, and static-geometry profile counter resets there; the render loop should consume `FrameGeometryCounts` for frame buffer sizing.

Static geometry content/shape derivation belongs in `VulkanRendererBackend.StaticGeometryContent.cs`. Keep `StaticGeometryContent`, content hash selection, fallback hash profiling, static primitive triangle decision recording, mutation-suppression input, expanded primitive layout selection, and shape packing there; `VulkanRendererBackend.StaticGeometry.cs` should consume the prepared content and apply cache/reuse/update/replace/create policy.

Static geometry policy application belongs in `VulkanRendererBackend.StaticGeometryPolicyApplication.cs`. Keep cache-hit accounting, reusable-buffer activation, in-place update activation, replacement teardown/retire choice, creation counting, and materialize+activate calls there; `VulkanRendererBackend.StaticGeometry.cs` should stay a thin branch selector over prepared content and shape.

Primitive instance encoding belongs in `VulkanRendererBackend.PrimitiveInstanceEncoding.cs`. Keep `PrimitiveInstance` payload flags, the solid-triangle sentinel payload, dynamic/static primitive sentinel counts, primitive instance creators, and sentinel reservation predicates there; dynamic/static upload and command recording code should call this boundary instead of defining ad hoc payload or sentinel constants.

Command pipeline binding state belongs in `VulkanRendererBackend.CommandPipelineState.cs`. Keep current-pipeline caching, `vkCmdBindPipeline` timing, source command-kind bind counters, and actual pipeline-class counters there; `VulkanRendererBackend.CommandRecording.cs` should choose the desired pipeline and call the command-state boundary.

Command descriptor-set binding state belongs in `VulkanRendererBackend.CommandDescriptorState.cs`. Keep last descriptor-set caching, `vkCmdBindDescriptorSets` timing, and descriptor bind counters there; command recording should call this boundary after resolving the texture resource.

Command geometry/primitive buffer binding state belongs in `VulkanRendererBackend.CommandBufferBindingState.cs`. Keep geometry vertex/index buffer cache, primitive instance buffer cache, `vkCmdBindVertexBuffers`/`vkCmdBindIndexBuffer` timing, and geometry/primitive bind counters there; command recording should call this boundary for triangle, expanded static primitive, unified solid primitive, and primitive-instance draw paths.

Command frame/render-pass recording belongs in `VulkanRendererBackend.CommandFrameRecording.cs`. Keep command-buffer begin/end, main render-pass begin/end, and GPU timestamp query reset/write helpers there; `VulkanRendererBackend.CommandRecording.cs` should call this frame boundary instead of embedding raw render-pass setup.

Render-entry shell ownership belongs in `VulkanRendererBackend.RenderEntry.cs`. Keep `RenderDrawData(...)` there as the high-level frame order only: profile reset, texture update, frame target/begin, frame geometry preparation, command recording preparation, and frame completion. Do not inline texture lifetime, swapchain acquire, geometry upload, command-buffer recording, submit/present, or profile-output internals into this shell.

Frame orchestration belongs in `VulkanRendererBackend.FrameOrchestration.cs`. Keep frame target validation, swapchain image acquire/recreate retry, per-frame fence waits, image-in-flight ownership, pending destroy flush, low-level submit/present helpers, present-result handling, and frame-profile timing/output helpers there. `VulkanRendererBackend.RenderEntry.cs` should preserve only the high-level frame order and should not inline swapchain acquire, queue submit, queue present, or frame-profile output construction again.

Frame geometry preparation/upload belongs in `VulkanRendererBackend.FrameGeometry.cs`. Keep per-frame static binding preparation, dynamic geometry capacity decisions, dynamic primitive sentinel capacity, dynamic geometry upload, static cache pruning, upload timing, and the geometry-buffer tuple passed to command recording there. `RenderDrawData(...)` should call this boundary and should not inline static/dynamic geometry preparation or upload phase logic again.

Frame command recording preparation belongs in `VulkanRendererBackend.FrameCommandRecording.cs`. Keep frame command-pool reset, command recording timing, `RecordCommandBuffer(...)` invocation, GPU timestamp-issued bookkeeping, and the command buffer returned for submit there. `RenderDrawData(...)` should call this boundary and should not inline `ResetCommandPool`, record timing, or timestamp-issued logic again.

Frame completion belongs in `VulkanRendererBackend.FrameCompletion.cs`. Keep final semaphore extraction, `SubmitFrame(...)`, `PresentFrame(...)`, present-result handling call, profile emission, and frame-index advance there. `RenderDrawData(...)` should call this boundary and should not inline submit/present/profile/frame-index completion logic again.

Command frame/list recording context belongs in `VulkanRendererBackend.CommandContext.cs`. Keep `CommandFrameContext`, `CommandDrawListContext`, and `CreateCommandFrameContext(...)` there; `VulkanRendererBackend.CommandRecording.cs` should create them at frame and draw-list boundaries, while scissor, push-constant, and draw-path boundaries consume them instead of long transform, clip, buffer, and offset scalar bundles. Keep integer framebuffer extents on the frame context for render-pass setup, and do not reintroduce standalone TAA/jitter scalar parameters on `RecordCommandBuffer(...)`; restore temporal jitter through the frame context boundary only when a real TAA path exists.

Command recording state aggregation belongs in `VulkanRendererBackend.CommandRecordingState.cs`. Keep the per-pass profile, diagnostic, texture, scissor, pipeline, descriptor, buffer, push, and draw-dispatch state bundle there so command recording and draw-list recording pass one state aggregate instead of widening every boundary. Final record timing/stat output construction also belongs there through `CompleteCommandRecordingState(...)` and `BuildCommandRecordStats(...)`; `VulkanRendererBackend.CommandRecording.cs` should not rebuild output ticks or profile stats from individual state fields inline.

Command draw-list recording belongs in `VulkanRendererBackend.CommandDrawListRecording.cs`. Keep viewport setup, draw-list offset tracking, static binding lookup, scheduler setup/profile events, command iteration, per-command diagnostic/texture/font/scissor sequencing, and draw-path dispatch there. `VulkanRendererBackend.CommandRecording.cs` should call this boundary after frame begin and should not grow another local draw-list traversal loop.

Command push-constant state belongs in `VulkanRendererBackend.CommandPushConstantState.cs`. Keep transform/opacity push cache, `vkCmdPushConstants` range selection, timing, and push counters there; command recording should provide command translation/opacity and frame context data, then call this boundary.

Command scissor state belongs in `VulkanRendererBackend.CommandScissorState.cs`. Keep computed scissor rectangle helpers, scissor reuse, current scissor cache, visibility rejection, clipping timing, `vkCmdSetScissor` timing, and scissor counters there; command recording should call this boundary before pipeline and draw dispatch. `TryComputeScissorRect(...)` should clamp once at the float clip-bound stage and avoid a second integer framebuffer clamp unless a focused profile proves the extra boundary check is needed.

Command draw dispatch belongs in `VulkanRendererBackend.CommandDrawDispatch.cs`. Keep triangle indexed draw dispatch, expanded static primitive indexed draw dispatch, primitive-instance draw dispatch, index/instance calculations, and draw-call timing/counting there; command recording should bind required state first, then call this boundary for the selected draw path.

Command classification belongs in `VulkanRendererBackend.CommandClassification.cs`. Keep per-command triangle/primitive classification, white/font texture classification, texture-need flags, and static-expanded-primitive geometry flags there; command recording should consume the classification value instead of recomputing these hot-path booleans inline.

Command texture lookup state belongs in `VulkanRendererBackend.CommandTextureState.cs`. Keep the last-texture cache, texture dictionary lookup, and texture lookup timing there; command recording should resolve a `TextureResource` through this boundary before descriptor binding only when `CommandClassification.CommandNeedsTexture` is true. Non-texture solid/color commands should skip this boundary entirely. Missing required textures should report through the font diagnostic boundary when relevant.

Texture resource/update ownership belongs in `VulkanRendererBackend.TextureResources.cs`. Keep `ApplyTextureUpdates(...)`, texture create/update/destroy, pending texture-destroy flushes, texture data upload/batching, font/white texture id classification, texture dictionary/font-white id state, and texture descriptor allocation there. `RenderDrawData(...)` should only call `ApplyTextureUpdates(...)`; command recording should use texture id classification from this boundary and should not grow texture lifetime or upload code.

Generic Vulkan resource helpers belong in `VulkanRendererBackend.ResourceHelpers.cs`. Keep swapchain image-view creation, frame-safe buffer destroy helpers, `CreateBuffer(...)`, `CreateImage(...)`, `CreateImageView(...)`, `FindMemoryType(...)`, `ToVkFormat(...)`, and MSAA color image create/destroy there. Texture, static geometry, dynamic geometry, staging upload, swapchain, and MSAA paths should consume this boundary instead of growing allocation or memory-type code in `VulkanRendererBackend.cs`.

Image layout transition policy belongs in `VulkanRendererBackend.ImageTransitions.cs`. Keep `TransitionImageLayout(...)`, texture upload prepare/finalize layout helpers, pending texture shader-read finalization state, layout-pair resolution, access mask selection, pipeline stage mask selection, and `imgTrans(...)` profile counters there. Do not hide shader/color-attachment stage dependencies in upload or generic allocation code; transfer-queue work must keep the active upload queue (`uploadQ`) separate from the candidate queue (`xferCandQ`) and must model queue ownership and stage masks explicitly before changing the default upload queue policy. Use `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)` to prove whether texture/page uploads are barrier-heavy or copy-heavy; `texture_upload_barrier_bench_fba.cs` should show same-texture non-overlapping region batches at `total=2` and many-texture updates at `total=2 * textureCount`. `xferStage` means the stage masks are transfer-queue-compatible; `gfxStage` means graphics/fragment/color stage masks must be split or moved before transfer-only upload recording.

Render-target setup belongs in `VulkanRendererBackend.RenderTargets.cs`. Keep render-pass creation, swapchain framebuffer creation, render-pass/framebuffer state, MSAA sample-count state, and MSAA color image/view state there, including the single-sample path, MSAA color attachment, resolve attachment, subpass dependency, and framebuffer attachment list rules. Do not grow raw render-pass/framebuffer setup back into `VulkanRendererBackend.cs` or command-recording files.

Pipeline resource setup belongs in `VulkanRendererBackend.PipelineResources.cs`. Keep descriptor pool creation, descriptor/pipeline layout creation, graphics pipeline assembly, sampler creation/state, pipeline/cache/shader/descriptor state, pipeline cache load/save/destroy, shader module creation, and embedded shader loading there. Do not mix pipeline object creation, descriptor pool setup, pipeline cache policy, or shader-loader policy back into frame orchestration or command recording.

Sync/query setup belongs in `VulkanRendererBackend.SyncResources.cs`. Keep frame command pool/buffer allocation, per-frame fence creation/state, image-in-flight state, semaphore ring creation/state, upload command resource entry, GPU timestamp query constants, and GPU timestamp query-pool creation there. Frame orchestration should consume these initialized resources and should not grow frame sync allocation or query-pool creation code. GPU profiling request/resolved state belongs with diagnostics because profile output and query-result interpretation consume it.

Shared frame-fence wait helpers belong in `VulkanRendererBackend.FrameSync.cs`. Keep the all-in-flight frame fence wait used by resource replacement, texture teardown, static geometry updates, and other cross-frame hazard-avoidance paths there. Per-frame acquire/present waits should stay in `VulkanRendererBackend.FrameOrchestration.cs`; sync allocation should stay in `VulkanRendererBackend.SyncResources.cs`.

Swapchain selection policy belongs in `VulkanRendererBackend.SwapchainPolicy.cs`. Keep surface format selection, present mode selection, platform framebuffer state, and framebuffer extent selection there. `CreateSwapchain(...)` may consume those decisions, but should not inline preference lists, VSync fallback messaging, or platform framebuffer clamping again.

Swapchain resource creation and lifecycle belongs in `VulkanRendererBackend.SwapchainResources.cs`. Keep `CreateSwapchain(...)`, surface capability/mode enumeration, desired image-count clamping/state, composite-alpha fallback, `SwapchainCreateInfoKHR` construction, swapchain handle creation/state, swapchain image/view format/extent state, swapchain image retrieval, `CreateSwapchainResources(...)`, `RecreateSwapchain(...)`, `TryRecreateSwapchain(...)`, and `DestroySwapchainDependentResources(...)` there. Do not merge resource creation, recreate flow, or swapchain-dependent destruction back into selection policy or the main backend lifecycle method.

Device/backend lifecycle belongs in `VulkanRendererBackend.Lifecycle.cs`. Keep `Dispose()` and `DestroyDeviceResources(...)` there so pipeline-cache save, swapchain-dependent destruction, device-level resource destruction, and instance/surface/device handle cleanup stay in one teardown boundary. Do not re-inline device-level destruction into `VulkanRendererBackend.cs`.

Device resource setup belongs in `VulkanRendererBackend.DeviceResources.cs`. Keep instance creation/state, instance extension loading, surface creation/state, physical-device selection/state, graphics/present queue-family selection/state, dedicated transfer candidate discovery/state, logical device creation/state, queue retrieval/state, device extension loading, device policy state, and MSAA sample-count resolution there. Do not grow device/queue setup back into the bootstrap constructor; it should preserve initialization order and consume this boundary.

Device/vendor renderer policy belongs in `VulkanRendererBackend.DevicePolicy.cs`. Keep GPU vendor classification, upload queue policy parsing/resolution, triangle color pipeline policy, solid unified pipeline policy, static primitive triangle policy, static geometry update policy, requested/resolved policy state, and pipeline cache identity there. Diagnostics should report resolved policy but should not own renderer-policy env parsing.

Renderer bootstrap ownership belongs in `VulkanRendererBackend.Bootstrap.cs`. Keep the constructor initialization order, surface-source validation, texture id defaults, initial swapchain creation, and selected-device policy resolution there. Device/queue creation details should stay in `DeviceResources`, pipeline resources in `PipelineResources`, settings mutation in `Settings`, and frame rendering in `RenderEntry`.

Renderer state declarations belong beside the owner file that already owns the matching lifecycle or behavior. `VulkanRendererBackend.State.cs` is only for truly shared primitives without a narrower owner; currently that means the shared `Vk` API handle. `VulkanRendererBackend.cs` should remain only the `IRendererBackend` partial-class shell and should not regain fields, shader blobs, setup code, or frame code.

Renderer settings/API ownership belongs in `VulkanRendererBackend.Settings.cs`. Keep public device-object invalidation/recreation entry points, clear-color conversion, minimum image count, VSync, MSAA sample setting, settings-triggered swapchain recreation, and settings-related environment parsing there. Do not grow runtime render-entry or constructor code with settings mutation branches.

Command pipeline selection belongs in `VulkanRendererBackend.CommandPipelineSelection.cs`. Keep target vertex/index/primitive buffer selection, solid-unified availability checks, desired graphics pipeline selection, and solid-unified use detection there; command recording should consume `CommandPipelineSelection` for pipeline binding, buffer binding, and draw-path branching.

Command scheduling belongs in `VulkanRendererBackend.CommandScheduling.cs`. Keep overlap-constrained scheduled-order lookup/cache, scheduling compatibility checks, scheduling bounds merge, adjacent scheduled-command run merge expansion, and command-iteration cursor/step selection there. Command recording should consume `CommandIterationStep`, use the step's command index and next order index, and report only the merged-count event to `CommandRecordProfileState`.

Command scheduler policy state also belongs in `VulkanRendererBackend.CommandScheduling.cs`. Keep `DUXEL_VK_COMMAND_SCHEDULER` parsing, `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`, resolved scheduler mode/window state, schedule cache ownership, and scheduling algorithms together.

Command draw-path orchestration belongs in `VulkanRendererBackend.CommandDrawPath.cs`. Keep the triangle, expanded static primitive, and primitive-instance draw-path branch orchestration there, including pipeline-selection consumption, pipeline/push/descriptor binding order, geometry/primitive buffer binding requirements, primitive instance buffer validation, and calls into `CommandDrawDispatch`. `VulkanRendererBackend.CommandRecording.cs` should prepare command classification, diagnostics, texture resolution, and scissor visibility, then call this draw-path boundary instead of owning draw-path branches inline.

Command record profile state belongs in `VulkanRendererBackend.CommandRecordProfileState.cs`. Keep command-record profile counters, draw-list static/dynamic counts, scheduler hit/miss/timing counts, scheduled merge counts, transition counters, and final `CommandRecordStats` construction there; command recording should report profile events through this boundary.

General command diagnostic state belongs in `VulkanRendererBackend.CommandDiagnosticState.cs`. Keep command diagnostic frame gating, per-pass emission counts, pipeline label selection, and `DUXEL_VK_COMMAND_DIAG` log emission there.

Command font diagnostic state belongs in `VulkanRendererBackend.CommandFontDiagnosticState.cs`. Keep `DUXEL_VK_FONT_DIAG` log writing, per-pass font diagnostic counts, missing texture font diagnostics, normal font command diagnostics, and index/vertex/UV bounds validation there; command recording and texture lookup should call this boundary instead of owning font-specific counters or log formatting.

For font command diagnostics, set `DUXEL_VK_FONT_DIAG=1`. Use `DUXEL_VK_FONT_DIAG_OUT` to redirect the verbose command log to a file.

For general Vulkan command sequence diagnostics, set `DUXEL_VK_COMMAND_DIAG=1`. Use `DUXEL_VK_COMMAND_DIAG_OUT` to log each draw command's `pipe`, texture id, clip, and static-binding status for pipeline-switch analysis. Logging is limited by default with `DUXEL_VK_COMMAND_DIAG_EVERY=120` and `DUXEL_VK_COMMAND_DIAG_FRAMES=1`.

Draw-list command merging must treat callback-free zero-element commands as state placeholders, not draw-order barriers. When adding a real draw, the builder removes a trailing empty placeholder before testing contiguous command merge eligibility; keep opacity, texture, clip, translation, kind, vertex offset, callback, and user data in the merge predicate. Prefer clipped draw helpers such as clipped `AddImage(...)` when the caller already knows the effective clip, instead of pushing/popping the builder clip stack around one draw.

Sampler-free solid triangle rendering defaults to `DUXEL_VK_TRIANGLE_COLOR_PIPELINE=auto`. The automatic policy enables it for NVIDIA/AMD discrete GPUs and keeps it off for other devices. For scene-specific A/B profiling, force it with `1/true/on` or disable it with `0/false/off`.

The dynamic path that draws solid triangles and solid rect/circle primitives through one Vulkan pipeline defaults to `DUXEL_VK_SOLID_UNIFIED_PIPELINE=auto`, but the current automatic policy resolves it to disabled on all devices until a vendor/device gate proves a consistent FPS win. Force it with `1/true/on` or disable it with `0/false/off`. Dynamic primitive buffer instance `0` is reserved for the solid-triangle sentinel, and real dynamic rect/circle primitives draw at `firstInstance + 1`, so triangles and primitives keep the same binding `1` buffer. For static-cached draw-list experiments, also set `DUXEL_VK_SOLID_UNIFIED_STATIC=1`. In that mode, static primitive buffers reserve instance `0` for the solid-triangle sentinel; non-expanded static primitive instances are placed after that base offset, and expanded static primitive geometry can bind the sentinel for solid unified draws. Static full unified has the desired command-state shape, but it is not a default path yet because FPS still regresses in mixed scenes.

For text renderer A/B profiling, set `DUXEL_TEXT_RENDERING=direct`, `atlas`, or `auto`. `DirectText` is the default. Explicit `atlas` forces the atlas renderer for comparison, and explicit `auto` keeps atlas-first rendering but uses DirectText as an immediate visual fallback for missing atlas glyphs; use it only when that fallback behavior is intentional.

DirectText page-texture packing is enabled only with `DUXEL_DIRECT_TEXT_PAGE=1`. It packs DirectText bitmaps into 1024x1024 page textures, reserves a 1px border around each region, and extrudes edge pixels into that border to reduce atlas sampling bleed. Page creation uploads only the border-inclusive packed region, and consecutive non-overlapping partial updates for the same texture can be batched into one Vulkan upload/copy submission. Use `samples/fba/directtext_page_upload_bench_fba.cs` as the focused upload-policy gate for this path before changing upload batching or transfer-queue defaults. DirectText page quads should use the clipped image helper rather than a `PushClipRect -> AddImage -> PopClipRect` sequence. It is off by default to preserve text appearance and must not become a default path without rendered visual validation.

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
