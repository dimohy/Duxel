# Duxel FBA Samples — Run Instantly

> Last synced: 2026-07-20
> Korean: [fba-run-samples.ko.md](fba-run-samples.ko.md)

> Run **Duxel** FBA samples with a **single copy-paste command**. These samples use the .NET 10 file-based app feature; Duxel packages also support regular .NET 9 projects.

> Note: To run with local source changes, use `./run-fba.ps1 samples/fba/<filename>.cs`.

## Requirements

- **.NET 10 SDK** or later ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- GPU with **Vulkan 1.0+** support (most modern GPUs)

---

## How to Run

Copy-paste the command into your terminal.

### PowerShell

```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/<filename> | dotnet run -
```

### Bash / macOS / Linux

```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/<filename> -o - | dotnet run -
```

---

## All-in-One Widget Demo ⭐

Comprehensive demo showcasing 400+ APIs. **Start here!**

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/all_features.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/all_features.cs -o - | dotnet run -
```

> MenuBar · Typography · Layout & Columns · Popup/Context/Tooltip · Input Queries · Item Status · Multi-select · Layers & Animation · Drawing Primitives · DragAndDrop · ListClipper(10K) · Markdown Studio · Time/FPS/VSync toggle

---

## Declarative Dashboard

Reusable C# declarative UI dashboard with compiled Windows 11 design tokens.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/declarative_dashboard_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/declarative_dashboard_fba.cs -o - | dotnet run -
```

> `UiState<T>` · reusable `UiComponent` classes · `DuxelWindowsApp.Run<TDesign>()` · product shell helpers

---

## Hello Duxel

Small `Hello`, big `Duxel!` — the tiniest possible greeting sample.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/hello_duxel_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/hello_duxel_fba.cs -o - | dotnet run -
```

> `PushFontSize` hierarchy · one-window minimal sample · hello-world style starting point

---

## Extended Title Bar

Places application tabs in the title-bar area while preserving Windows caption buttons, Snap Layout hit testing, resize borders, drag/double-click behavior, DPI coordinates, and the maximized monitor work area.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/extended_title_bar_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/extended_title_bar_fba.cs -o - | dotnet run -
```

> `DuxelTitleBarMode.ExtendedContent` · `TryGetCaptionButtonBounds` · `SetTitleBarDragRegions` · native DWM caption buttons

See the [Extended Title Bar Guide](extended-title-bar-guide.md) for layout rules, DPI/multi-monitor contracts, and the NativeAOT diagnostic workflow.

---

## Advanced Layout

Demonstrates PushID, ItemWidth, Cursor, ScrollControl, StyleVar, TextWrap, FontScale.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/advanced_layout.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/advanced_layout.cs -o - | dotnet run -
```

> PushID · PushItemWidth · SetNextWindowBgAlpha · Scroll · PushStyleVar · PushTextWrapPos · Font Scale

---

## Legacy Columns

Full Columns API usage showcase.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/columns_demo.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/columns_demo.cs -o - | dotnet run -
```

> 2-col/3-col · Border · ColumnWidth/Offset query · Mixed widgets

---

## Image / Popup / Advanced Widgets

Image, ImageButton, advanced Popup, Tooltip, TextLink, TreeNodeV.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/image_and_popups.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/image_and_popups.cs -o - | dotnet run -
```

> Image · ImageWithBg · ImageButton · OpenPopupOnItemClick · ContextVoid · TextLink · BeginItemTooltip · ListBoxHeader/Footer

---

## Image Effects Lab

Web PNG/JPG/GIF source switching, GIF animation playback, image effects (Zoom/Rotation/Alpha/Brightness/Contrast/Pixelate).

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/image_widget_effects_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/image_widget_effects_fba.cs -o - | dotnet run -
```

Optionally specify a local image path:

**PowerShell (Custom path)**
```powershell
$env:DUXEL_IMAGE_PATH='C:\images\sample.gif'; irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/image_widget_effects_fba.cs | dotnet run -
```

> Web PNG/JPG/GIF auto-download · GIF frame-delay playback · 3px body peek on collapse

---

## Keyboard / Mouse Input Queries

Keyboard/mouse state queries, shortcuts, clipboard.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/input_queries.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/input_queries.cs -o - | dotnet run -
```

> IsKeyDown · IsKeyPressed · IsMouseDragging · Shortcut(Ctrl+S) · GetClipboardText

---

## Item Status Queries

Real-time tracking of widget Hovered/Active/Focused/Clicked/Edited lifecycle.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/item_status.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/item_status.cs -o - | dotnet run -
```

> IsItemActive · Activated · Deactivated · DeactivatedAfterEdit · GetItemRectMin/Max/Size · IsRectVisible

---

## Layer Cache / GPU Buffer Validation (idle_layer_validation)

Benchmark for layer static cache and GPU-resident buffer performance/correctness.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/idle_layer_validation.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/idle_layer_validation.cs -o - | dotnet run -
```

Control backend/opacity/layout/particle count via environment variables:

**PowerShell (env var example)**
```powershell
$env:DUXEL_LAYER_BENCH_BACKEND='texture'
$env:DUXEL_LAYER_BENCH_OPACITY='0.5'
$env:DUXEL_LAYER_BENCH_PARTICLES='3000,9000'
$env:DUXEL_LAYER_BENCH_LAYOUTS='baseline,frontheavy'
$env:DUXEL_LAYER_BENCH_PHASE_SECONDS='2'
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/idle_layer_validation.cs | dotnet run -
```

| Env Variable | Default | Description |
| --- | --- | --- |
| `DUXEL_LAYER_BENCH_BACKEND` | `drawlist` | Layer cache backend (`drawlist` / `texture`) |
| `DUXEL_LAYER_BENCH_OPACITY` | `1.0` | Layer opacity (0.2–1.0) |
| `DUXEL_LAYER_BENCH_PARTICLES` | `3000,9000,18000` | Particle counts (comma-separated) |
| `DUXEL_LAYER_BENCH_LAYOUTS` | `baseline` | Layout presets (`baseline`, `frontheavy`, `uniform`, `dense`) |
| `DUXEL_LAYER_BENCH_PHASE_SECONDS` | `2.5` | Measurement time per phase (seconds) |
| `DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER` | `false` | Disable fast render path |
| `DUXEL_LAYER_BENCH_OUT` | _(none)_ | JSON output path for bench results |

> Layer cache ON/OFF comparison · drawlist/texture backend · opacity regression · particle/layout matrix bench

---

## Performance Benchmark (PerfTest)

High-polygon physics simulation for DrawList rendering performance testing.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/Duxel_perf_test_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/Duxel_perf_test_fba.cs -o - | dotnet run -
```

> Add/remove polygons · Speed/size/sides/rotation sliders · FPS display · Bounding collision

---

## Windows Calculator

Windows-style calculator with cyber backdrop, ripple effects, FX buttons, translucent UI.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/windows_calculator_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/windows_calculator_fba.cs -o - | dotnet run -
```

> Cyber grid background · Button ripple effects · Neon glow FX buttons · AnimateFloat real-time transitions

---

## Calculator Showcase (RPN Trace)

RPN token trace, multi-base simultaneous display, 32-bit toggle grid.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/windows_calculator_duxel_showcase_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/windows_calculator_duxel_showcase_fba.cs -o - | dotnet run -
```

> Token→RPN→Eval conversion display · HEX/OCT/BIN simultaneous display · 32-bit bit toggle grid

---

## Text Render Validation

Validates text alignment, font size, and clip behavior.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/text_render_validation_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/text_render_validation_fba.cs -o - | dotnet run -
```

> DrawTextAligned Left/Center/Right · PushFontSize · clipToContainer ON/OFF

---

## Font Style / Size Rendering Validation

Validates Regular/Bold/Italic font styles and Korean/English rendering at various sizes.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/font_style_validation_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/font_style_validation_fba.cs -o - | dotnet run -
```

> Size Ladder (10–64px) · Live Preview · Direct Text toggle · Font style selection

---

## Layer Dirty Strategy Bench

Benchmark comparing layer dirty strategies: `all` vs `single`.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/layer_dirty_strategy_bench.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/layer_dirty_strategy_bench.cs -o - | dotnet run -
```

> all vs single dirty comparison · Cache rebuild count · FPS difference measurement

---

## Layer + Widget Mix Bench

Dynamic benchmark mixing layers and widgets.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/layer_widget_mix_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/layer_widget_mix_bench_fba.cs -o - | dotnet run -
```

> DrawLayerCardInteractive · Widget mix load · Card drag interaction

---

## Global Static Cache Bench

Measures performance impact of global static cache strategy (`duxel.global.static:*`).

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/global_dirty_strategy_bench.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/global_dirty_strategy_bench.cs -o - | dotnet run -
```

> Static cache vs all-dynamic performance comparison · BeginWindowCanvas API demo

---

## Scrolling Static Layer Bench

Benchmarks static layer behavior while window scroll and parent clip rectangles change.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/scrolling_static_layer_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/scrolling_static_layer_bench_fba.cs -o - | dotnet run -
```

> Dynamic scroll vs static scroll · Sliding clip probe · Visual regression validation for blank/black static layer bodies

---

## UI Mixed Stress

Stress test rendering multiple windows/text/tables/lists/inputs/draws simultaneously.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/ui_mixed_stress.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/ui_mixed_stress.cs -o - | dotnet run -
```

> Multi-window · Text · Table · List · Input · Drawing primitives combined

---

## Vector Primitives Bench

Line/rect/circle vector primitives benchmark with focused workload selection for primitive-heavy gates.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/vector_primitives_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/vector_primitives_bench_fba.cs -o - | dotnet run -
```

> Bulk line/rect/circle rendering · `DUXEL_VECTOR_BENCH_WORKLOAD=mixed|rect-outline|axis-line`

---

## Pipeline Ordering Bench

Focused gate for dynamic solid/text pipeline ordering cost.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/pipeline_ordering_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/pipeline_ordering_bench_fba.cs -o - | dotnet run -
```

> Alternating · grouped solids-then-text · channelized · copy-free channel draw-list phases

---

## Dynamic Widget Ordering Bench

Focused gate for widget-like dynamic producer ordering and row clip churn.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/dynamic_widget_ordering_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/dynamic_widget_ordering_bench_fba.cs -o - | dotnet run -
```

> Alternating rows · grouped solids/text · channelized phases · row clip churn

---

## Static Cache Rebuild Bench

Focused gate for static cache replay, false-dirty rebuilds, mutating geometry updates, and allocation pressure.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/static_cache_rebuild_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/static_cache_rebuild_bench_fba.cs -o - | dotnet run -
```

> Static cache replay · replacement/update/reuse attribution · `avgAllocatedBytes`

---

## Moving Static Layer Ordering Bench

Focused gate for moving static-layer replay scheduling reuse.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/static_layer_moving_order_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/static_layer_moving_order_bench_fba.cs -o - | dotnet run -
```

> Stable static content · moving replay translation/clip · static scheduler A/B gate

---

## Texture Upload Barrier Bench

Focused gate for texture upload copy/barrier behavior before changing upload queue policy.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/texture_upload_barrier_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/texture_upload_barrier_bench_fba.cs -o - | dotnet run -
```

> Full texture updates · same-texture region batches · many-texture region updates

---

## DirectText Page Upload Bench

Focused gate for DirectText page-style partial texture uploads without platform glyph rasterizer cost.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/directtext_page_upload_bench_fba.cs | dotnet run -
```

**Bash**
```bash
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/directtext_page_upload_bench_fba.cs -o - | dotnet run -
```

> Page texture creates · same-page region appends · multi-page region updates

---

## DirectText Dynamic Text Benchmark

Focused gate for DirectText stable-cache hits versus changing-string cache misses. It writes JSON with average FPS, median/p95/p99 frame time, 1% low FPS, text-work tail latency, per-frame allocation, and GC counts.

Set `DUXEL_DIRECTTEXT_BENCH_OUT` to a writable JSON path before running. Optional controls are `DUXEL_DIRECTTEXT_BENCH_PHASE_SECONDS` (default `3`, maximum `30`), `DUXEL_DIRECTTEXT_BENCH_WARMUP_FRAMES` (default `96`), `DUXEL_DIRECTTEXT_BENCH_ROWS` (default `8`), and `DUXEL_DIRECTTEXT_BENCH_CORPUS_FRAMES` (default `256`). `DUXEL_DIRECT_TEXT_PAGE=1` is diagnostic-only because the page path currently produces a mostly black capture with the UI missing.

**PowerShell**
```powershell
$env:DUXEL_DIRECTTEXT_BENCH_OUT = "$PWD/directtext-dynamic-text.json"
irm https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/directtext_dynamic_text_bench_fba.cs | dotnet run -
```

**Bash**
```bash
export DUXEL_DIRECTTEXT_BENCH_OUT="$PWD/directtext-dynamic-text.json"
curl -sL https://raw.githubusercontent.com/dimohy/Duxel/refs/heads/main/samples/fba/directtext_dynamic_text_bench_fba.cs -o - | dotnet run -
```

> Stable cache hits · changing-string misses · frame/text tail latency · allocation and GC

---

## Download All at Once

To download all FBA samples at once:

**PowerShell**
```powershell
$base = "https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba"
$files = @(
    "all_features.cs",
    "declarative_dashboard_fba.cs",
    "hello_duxel_fba.cs",
    "extended_title_bar_fba.cs",
    "advanced_layout.cs", "columns_demo.cs",
    "image_and_popups.cs", "image_widget_effects_fba.cs",
    "input_queries.cs", "item_status.cs",
    "windows_calculator_fba.cs", "windows_calculator_duxel_showcase_fba.cs",
    "text_render_validation_fba.cs",
    "font_style_validation_fba.cs",
    "idle_layer_validation.cs",
    "layer_dirty_strategy_bench.cs", "layer_widget_mix_bench_fba.cs",
    "scrolling_static_layer_bench_fba.cs",
    "global_dirty_strategy_bench.cs", "vector_primitives_bench_fba.cs",
    "pipeline_ordering_bench_fba.cs", "dynamic_widget_ordering_bench_fba.cs",
    "static_cache_rebuild_bench_fba.cs", "static_layer_moving_order_bench_fba.cs",
    "texture_upload_barrier_bench_fba.cs", "directtext_page_upload_bench_fba.cs",
    "directtext_dynamic_text_bench_fba.cs",
    "Duxel_perf_test_fba.cs", "ui_mixed_stress.cs"
)
New-Item -ItemType Directory -Force -Path fba | Out-Null
$files | ForEach-Object { irm "$base/$_" -OutFile "fba/$_"; Write-Host "Downloaded $_" }
Write-Host "`nRun: dotnet run fba/all_features.cs"
```

**Bash**
```bash
BASE="https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba"
FILES=(all_features.cs declarative_dashboard_fba.cs hello_duxel_fba.cs extended_title_bar_fba.cs \
    advanced_layout.cs columns_demo.cs image_and_popups.cs image_widget_effects_fba.cs \
    input_queries.cs item_status.cs \
    windows_calculator_fba.cs windows_calculator_duxel_showcase_fba.cs \
    text_render_validation_fba.cs \
    font_style_validation_fba.cs \
    idle_layer_validation.cs \
    layer_dirty_strategy_bench.cs layer_widget_mix_bench_fba.cs scrolling_static_layer_bench_fba.cs \
    global_dirty_strategy_bench.cs vector_primitives_bench_fba.cs \
    pipeline_ordering_bench_fba.cs dynamic_widget_ordering_bench_fba.cs \
    static_cache_rebuild_bench_fba.cs static_layer_moving_order_bench_fba.cs \
    texture_upload_barrier_bench_fba.cs directtext_page_upload_bench_fba.cs \
    directtext_dynamic_text_bench_fba.cs \
    Duxel_perf_test_fba.cs ui_mixed_stress.cs)
mkdir -p fba
for f in "${FILES[@]}"; do curl -sL "$BASE/$f" -o "fba/$f" && echo "Downloaded $f"; done
echo -e "\nRun: dotnet run fba/all_features.cs"
```

---

## Links

| | |
|---|---|
| **GitHub** | https://github.com/dimohy/Duxel |
| **NuGet** | https://www.nuget.org/packages/Duxel.App |
| **DSL Docs** | [docs/ui-dsl.md](https://github.com/dimohy/Duxel/blob/main/docs/ui-dsl.md) |
| **FBA Guide** | [docs/getting-started-fba.md](https://github.com/dimohy/Duxel/blob/main/docs/getting-started-fba.md) |
