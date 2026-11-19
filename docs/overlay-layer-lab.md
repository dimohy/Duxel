# Menu and popup dynamic layer lab

For the latest common-engine follow-up, see [retained rendering](retained-rendering.md). The40C51851 and earlier measurements below are historical stage results.

`samples/fba/overlay_layer_lab.cs` is a dedicated consumer of the common engine path. Its default workload has 48 popups, five items and 64 vector color swatches per popup, and 1,800 background shapes. Several popups intentionally overlap at each screen position. `Animate one palette` moves only the first popup. Right-click opens a context menu with a submenu; the tooltip follows the pointer.

```powershell
./run-fba.ps1 samples/fba/overlay_layer_lab.cs -NoCache
```

The common engine automatically routes existing popup, context-menu, menu, combo, tooltip and foreground drawing APIs through presentation layers. The sample has no custom rendering cache and does not skip widget input. Input and layout run each UI frame; identical geometry and commands reuse an immutable snapshot. Actual buffers are compared, rather than relying only on hashes. Changing one popup replaces only that popup's snapshot.

Screen-anchored layers have 3D transforms and depth ranges. Presentation order is foreground followed by popup depth; peers preserve submission order. These already ordered screen planes need neither depth writes nor dedicated depth images. Existing alpha blending and hardware scissors preserve translucency, text edges and clipping. For planes exactly aligned with display coordinates and without depth writes, the renderer eliminates transformations that cannot affect coverage and shares the existing graphics pipeline. Ordinary camera and 3D-scene depth testing remains available. `UiLayer3D.PresentationDepthScopeId` is reserved for engine presentation layers.

New APIs:

```csharp
ui.SetNextPopupPosition(new UiVector2(100, 150));
if (ui.BeginPopup("properties"))
{
    ui.MenuItem("Inspect");
    ui.EndPopup();
}
var counters = ui.GetOverlayLayerMetrics(); // Current UI frame, completed scopes.
// counters.ReusedLists / counters.RebuiltLists
```

`SetNextPopupPosition` affects only the next `BeginPopup`, using display coordinates without inheriting a widget camera. Opening still uses `OpenPopup`. A position or content change regenerates that popup's geometry. This API does not skip arbitrary widget input or convert every move into a matrix-only update.

Replaced buffers remain alive while queued frames read them. Closing a popup or disposing its context retires snapshots once readers finish. Callbacks and commands carrying execution state retain their per-frame execution path.

## Measurement

`DUXEL_OVERLAY_COUNT` accepts 1–512 and `DUXEL_OVERLAY_SWATCHES` accepts 0–512 color swatches per popup; `DUXEL_OVERLAY_SCENARIO` accepts `idle` or `motion`. Setting `DUXEL_OVERLAY_OUT` and `DUXEL_OVERLAY_SECONDS` measures after at least 96 frames and one second of warmup, then exits. This throughput sample disables idle-frame skipping without changing general application defaults. Palettes remain open so incidental outside clicks do not permanently reduce the workload.

`scripts/measure-overlay-layers.ps1` alternates before/after executables built from the same sample. It requires at least three runs and retains raw accepted-presentation frame intervals, executable and managed payload hashes, independent stdout/stderr and results. FPS means accepted presentation throughput, not monitor refresh rate. A changed framebuffer, incomplete duration or render-thread error rejects the result. Memory uses median private/working-set observations sampled every 250ms while the window is active; post-shutdown memory is not treated as active memory.

The CPU-only comparison is `tests/Duxel.Performance.Bench --overlays`. It excludes GPU and presentation time. Per-frame allocation, post-GC managed memory and process private bytes are separate measurements; the managed-memory observation occurs outside the timed loop.

Measurements and intermediate regressions are recorded in [the 2026-11-04 optimization log](optimization-session-2026-11-04.md). `OverlayPresentationTests` covers pixels and snapshot lifetimes.

Popup hit testing resolves the immutable previous-frame rectangles and current pointer once per UI frame. Widgets share this result, reducing popup-count times widget-count work to popup-count plus widget-count while preserving higher-tier input blocking. Continuously changing popups use the shared dynamic upload stream rather than allocating resident GPU buffers for every move; stable content returns to residency.

The optimization also requires the engine-owned `UiLayer3D.IsScreenSpacePresentation` marker. A caller's ordinary scene retains depth testing even when its numeric scope ID matches `PresentationDepthScopeId`; the numeric ID alone never changes rendering behavior.

Nested widget IDs and popup state keys share a bounded, per-context string cache: at most 4,096 entries and 1,048,576 charged UTF-16 characters, with stale-key eviction. Small geometry buffers start with less capacity. Screen presentation rectangles avoid triangle expansion when that would increase transfer size. These policies reside in the common engine.

Comparison windows start hidden by default to reduce resize interference; pass `-VisibleWindow` for a visible comparison. Memory observations run outside the UI task. `-SummarizeOnly -OutDir <results>` audits raw frames against summaries without relaunching binaries.

```powershell
$measure = @{
    BeforeExecutable = '<previous executable built from the same sample>'
    AfterExecutable = './samples/fba/artifacts/overlay_layer_lab/net11.0-windows7.0/overlay_layer_lab.exe'
    OutDir = './artifacts/overlay-comparison'
    Counts = @(128); Swatches = 256
    Scenarios = @('idle', 'motion'); Runs = 3; Seconds = 3
}
./scripts/measure-overlay-layers.ps1 @measure
```

## Engine-completion results on 2026-11-04

.NET 11 NativeAOT Speed, 1600×1000, VSync off, MSAA1. Each process warmed up for at least one second and 96 frames, then measured three seconds. Each variant ran three times in alternating order. Values are medians of independent-run average FPS.

The table below and `7B1A3575` describe earlier stages. The current distribution is `40C51851`; also see the subsequent memory optimization results.

| Popups/swatches | State | Before FPS | After FPS | Change |
| --- | --- | ---: | ---: | ---: |
| 48/64 | Idle | 2007.95 | 2023.03 | +0.75% |
| 48/64 | One moving | 1970.11 | 2004.83 | +1.76% |
| 128/256 | Idle | 359.30 | 387.50 | +7.85% |
| 128/256 | One moving | 361.48 | 382.84 | +5.91% |

Working set decreased by about 9.6–12.9MiB across these cases. Private bytes also decreased at the default load, but increased by about 12.3–14.4MiB at 128/256. At the larger load, allocations inside the UI Render body changed from 72,720B to 0B idle or 608B moving; dynamic uploads fell about 94–95%. Draw calls were unchanged. The CPU-only 32-tier test recorded per-frame allocations of 16,152→264B and rooted post-GC managed memory of about 10.18→0.61MiB. These scopes do not represent whole-frame allocations or GPU presentation FPS.

Validation passed 507 checks on each of .NET 10 and 11, preserved 142 prior GPU images byte-for-byte, and built Release with zero warnings or errors. The optimization log retains raw evidence and limitations. Submission throughput differs from monitor refresh rate; these short runs on one device do not establish performance on all devices or long-running behavior.

Distribution (`7B1A3575`) also fixes the interactive tooltip covering the context menu. A fresh comparison under the same settings recorded 1901.21→2007.09FPS idle and 1961.84→1962.87FPS moving at 48 popups; overlapping ranges make both inconclusive. At 128 popups, idle recorded 365.05→380.03FPS (+4.10%) and motion 375.58→382.72FPS (+1.90%), with nonoverlapping ranges. Working set fell about 9.6–13.2MiB, while large-load private bytes increased about 10.1–14.9MiB. `native-ui-final-{default,stress}` contains evidence for that executable. One baseline default-idle run recorded 578.34FPS; its cause remains unknown and the unfiltered result is retained. The latest moving workload did not establish a 1% low improvement.

## Additional memory optimization — current distribution 40C51851

After transferring geometry ownership, the common batcher no longer rents a second full-size working array that it would immediately release. Only the transient batch builder keeps a small replacement; regular frame builders retain their prior working capacity. This is a core change shared with other samples.

Against distribution 7B1A3575, the same 128-popup/256-swatch workload ran three alternating processes per variant and scenario. Median private bytes decreased from 197.94→196.09MiB idle (-1.84MiB) and 200.95→197.63MiB moving (-3.32MiB), with nonoverlapping ranges. FPS recorded 380.90→394.97 idle and 382.40→393.96 moving, but overlapping ranges make this additional change's throughput effect inconclusive. Large-load private bytes remain higher than the original implementation. Evidence: `scratch-memory-comparison/summary.json` and `scratch-memory-results.json`.

Validation passed 510 checks on each of .NET10 and 11, preserved 155 prior images byte-for-byte, and built Release with zero warnings or errors. Current EXE SHA256: `40C51851C9469653BA40DBCD9B8FE36ABD2D3411538E1A5FC8090AB4DD786B92`. Rejected temporary artifacts have explicit targets and [manual cleanup instructions](rejected-build-cleanup.md); deletion was not performed.

A fresh direct comparison between current 40C51851 and the original baseline is retained in `completion-native-original/summary.json`. At 128 popups, idle recorded 366.35→374.07FPS(+2.11%) and motion 363.44→381.36FPS(+4.93%), with nonoverlapping ranges. Working set fell from 135.71→122.69MiB idle and 135.79→123.42MiB moving. Median private bytes were 185.93→196.02MiB idle and 186.67→198.04MiB moving, so not every memory metric is lower than the original. The final raw evidence also retains p95, p99, slowest 1% and all independent-run ranges.

