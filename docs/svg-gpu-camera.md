# SVG GPU scenes and internal camera

2026-11-15 current source update: the launch path includes shared Component scrolling and Markdown image validation. This NativeAOT build passes32 screen presentations and384 native pointer changes with normal exit. Earlier comparative performance numbers below belong to the preserved `observer-rc3-aot-publish` benchmark build; this update is a regression check, not a new speedup comparison.

The current Linux launch path is the pinned.NET11 RC3 NativeAOT Observer. Its32 presentations/clean exit and separate Linux text/vector runtime-image comparisons pass. Existing SVG browser pixel counts below retain their original managed validation scope. [Linux launch and NativeAOT comparison](../samples/Duxel.Gallery.Linux/README.md).

`UiSvg` and `UiSvgIcon` parse XML once per source revision and retain immutable vector scenes on the GPU. Zoom updates the camera and visible output without CPU widget rasterization or path reupload. Input, layout and submission still use CPU work. Explicit image decoding remains a separate bitmap path.

## Current qualification

| Comparison | Result | Remaining scope |
| --- | ---: | --- |
| Basic layer/stroke controls | CPU/GPU5/5 | Original196 fractional pixels corrected to0 |
| Additional stroke domains | CPU/GPU3/5 | Thin284 pixels; nonuniform scale CPU9/GPU11 pixels |
| Geometric clip | CPU/GPU9/9 | Whole retained nine-case corpus |
| Shadow | CPU/GPU9/9 | Whole RGBA of nine independent sources |
| Opacity groups | GPU10/11 | Filter943 pixels, maximum channel error1 |
| Small gradients | GPU8/9 | Radial1 pixel, maximum channel error1 |
| Complex originals on white | GPU0/4 | Two originals, directSVG and imageCanvas |
| Complex transparent originals | GPU0/2 | Full premultiplied RGBA |
| Separate raw color-storage corpus | 5/6 | One straight-alpha RGB discrepancy |

Tolerance stays0 and reference images are not replaced with current output. Managed SDK11.0.100/runtime11.0.0 functionality is separate from the original pinned RC3 NativeAOT performance gate. Linux32 verifies presentation/normal exit, not physical GPU performance or full accessibility. GPU command-buffer time fell 4.7–14.3% in 9/10 camera diagnostic cases; Red 400% remains inconclusive. Readback-inclusive ranges overlap; app FPS, original RC3 AOT and vendor profiling remain separate. Evidence: `artifacts/svg-work-cache-linux-ui-2026-11-15/verification.json`.

## Run Observer

```powershell
& 'L:/Cache/Duxel/linux-accessibility-2026-11-14/observer-svg-work-cache-refined-build/bin/Duxel.Gallery/release/Duxel.Gallery.exe'
```

Under **SVG**, **Browser comparison** shows two complex originals; **Clips and edges** shows nine clip controls; **Layers and strokes** shows five corrected controls; **Stroke coverage** shows five additional domains. Zoom, rotation, margin and corner controls are shared, with first-selection loading. Independent Chromium references appear on the left and GPU widgets on the right. Default raster scale follows reference pixel dimensions;64-pixel references are not compared against separate128-pixel renders. The first basic pair is also checked through actual framebuffer byte comparison.

Linux executable: `/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.Gallery.Linux`. Pass the five fonts in the order specified in the [Linux guide](../samples/Duxel.Gallery.Linux/README.md). Append `--verify-gpu-camera <fresh-output>` for finite32-scenario verification, separate from full pixel checks of the new subtabs.

## Pixel qualification

```powershell
$exe = 'L:/Cache/Duxel/linux-accessibility-2026-11-14/graphics-svg-normal-candidate-build/bin/Duxel.Graphics.Tests/release/Duxel.Graphics.Tests.exe'
& $exe 'L:/Cache/Duxel/svg-layer-fresh' gpu-transparent-layer-origin 3
& $exe 'L:/Cache/Duxel/svg-layer-camera-fresh' gpu-transparent-layer-origin-tiled 3
& $exe 'L:/Cache/Duxel/svg-domain-fresh' gpu-transparent-stroke-domains 3
```

Always use fresh directories. The first two profiles pass5/5. All five assembled17x19 camera-tile outputs match single64x64 outputs. Additional domains save all five outputs before reporting3/5 mismatch. White profiles are `gpu-layer-origin` and `gpu-stroke-domains`; other groups include `gpu-clip-storage`, `gpu-shadow-pixels`, `gpu-gradient-pixels`, `gpu-opacity-pixels`, `gpu-svg-browser` and their transparent counterparts.

`comparison.json` retains whole-pixel, alpha/RGB and histogram differences. New results also report `GeneratedStrokePrograms`, `CommandCount` and `SceneBytes`. Each basic control changes from1 to0 generated stroke programs and1424 to752 scene bytes. These are logical scene data, not total VRAM or timing percentages. Transparent `.gpu.rgba` is premultiplied RGBA8; only PNG previews are converted to straight alpha.

```powershell
$managed = 'L:/Cache/Duxel/linux-accessibility-2026-11-14/svg-rectangular-stroke-managed-build/bin/Duxel.Svg.Managed.Tests/release/Duxel.Svg.Managed.Tests.exe'
& $managed --browser-stroke-domains 'L:/Cache/Duxel/svg-domain-cpu-fresh'
& $managed --browser-color-storage 'L:/Cache/Duxel/svg-color-cpu-fresh'
```

No failing member of the five-domain or six raw-color cases is omitted. Premultiplied equality is not promoted to straight-alpha equality.

## Scene and coverage contracts

The camera retains virtual full extent and an integer pixel origin. Output allocation covers the visible intersection, sampling margin and filter surroundings. Matching outputs are reused; temporary work buffers retire after GPU completion. Camera-dependent curve, dash and stroke work stays on the GPU. The64MiB work limit and explicit input validation remain.

Geometric clips, opacity storage, masks and filters have separate boundaries. Eligible single-paint rectangular clips fold into intersection geometry at compile time without changing authored object/paint coordinates. Opacity storage has a separate raster origin for dithering. Axis-aligned, undashed miter rectangle strokes use outer/inner coverage without generated path work. Clip and stroke share rectangle recognition.

Path flag64 carries outer bounds plus one inner record; invalid bounds/count/flag combinations reject before submission. Six-vector commands,88-byte push constants and8x8 workgroups remain. Temporary Vulkan1.2 SPIR-V compilation, validation/disassembly and recoverability of the protected previous producer are checked. No new subgroup-width assumption, barrier or external Skia dependency is introduced.

The browser reports GraphiteDawnD3D11/Skia `ea1e651d7a105f5ca327dfd65d28957957279e2e`; this is not a per-draw trace. The remaining thin-stroke discrepancy is being compared with the matching [analytic rectangle render step](https://raw.githubusercontent.com/google/skia/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/gpu/graphite/render/AnalyticRRectRenderStep.cpp) and coverage calculation. Passing five basic controls does not certify all stroke domains.

## Independent capture and prior evidence

`CaptureImageReferences.mjs` and `CaptureSvgImage.mjs` retain independent native SVG Image/Canvas bytes. Source/reference hashes are checked before device work. `BrowserReferenceContract.mjs` validates directSVG DPR, actual dimensions and position. Changed repeat-capture bytes never automatically replace a reference.

Previous artifacts remain under `artifacts/svg-layer-dither-2026-11-15`, `artifacts/svg-layer-origin-2026-11-15`, `artifacts/svg-clip-composite-2026-11-15` and `artifacts/svg-graphite-layout-2026-11-15`.

## Equal-output camera performance

```powershell
$exe = 'L:/Cache/Duxel/linux-accessibility-2026-11-14/graphics-svg-normal-candidate-build/bin/Duxel.Graphics.Tests/release/Duxel.Graphics.Tests.exe'
& $exe 'L:/Cache/Duxel/svg-camera-benchmark-fresh' gpu-svg-camera-bench 3
```

Two originals and five zooms each use 16 warmup and 32 measured frames, alternating a one-pixel pan to force new GPU output. Full premultiplied RGBA readback is bounded to 512×256. `frames.csv` separates UI build time, serialized render/readback time, prior-frame-slot GPU command-buffer time, calling-thread allocations, XML parses, scene uploads and CPU rasters. Pixel hashing and file writes are outside timers. GPU timestamps include UI rendering, not isolated SVG compute. Readback pixel-array allocations do not indicate production CPU SVG rasterization.

Three alternating baseline/candidate process runs preserve all 960 measured output hashes. All 107 raw outputs from 15 existing browser-comparison profiles are also unchanged. The common shader preserves structural layer/mask commands, then rejects drawing commands outside a conservative one-device-pixel fringe before paint/gradient calculation. Analytic fringes, sample positions, source order, scene data and 88-byte push constants remain unchanged. No CPU visibility lists or per-zoom XML parsing are introduced.

This is a limited GPU command-buffer improvement. Red 400% and readback-inclusive latency have no confirmed improvement. Full command traversal, output resource creation and synchronous readback costs need separate investigation. Whole-original browser equality also remains incomplete.

## Native GPU workspace and normal presentation

Camera updates reuse completed native work buffers instead of allocating and destroying them each time. Upload and device memory are separate classes; both the slot fence and its `q + N` retirement deadline must complete before reuse. Immutable scene data retains separate ownership. The cache holds at most 64MiB/256 completed buffers; this does not bound in-flight buffers or total VRAM. Renderer destruction frees the cache.

`gpu-svg-camera-present-bench 3` uses the same two originals and five zooms without CPU readback during measured frames. Two full RGBA camera outputs are checked after each block. The final three alternating pairs contain 1920 presentations and 60 equal output pairs, with 33.4–65.9% lower render-call time. That includes waits/submission, not app FPS or input-to-display latency. GPU timestamps belong to prior frame slots.

The logical completed-buffer cache peaked near 63.3MiB. Calling-thread allocations increased by 352–832 bytes/frame in some cases due to metadata, so this is not a memory-reduction claim. Host private/working-set samples and logical buffer bytes are separate; total VRAM is unmeasured. Existing work-budget failures remain explicit.

Use `tests/Duxel.Graphics.Tests/RunSvgCameraComparison.ps1` with named BaselineExecutable, CandidateExecutable and OutputDirectory parameters. It checks executable/DLL/source hashes before and after runs, alternates three process pairs, and records independent stdout/stderr, host memory, process IDs and termination state. It never builds automatically.

## Gallery browser comparison during zoom

The November 17 Gallery update added live C# examples to all 27 browser comparison pairs. On the default comparison, changing zoom rebuilt and reparsed both displayed examples. In two matched Windows Vulkan runs with .NET 11.0.0 managed, 40 measured zoom frames per run, and V-Sync disabled, this caused 78 additional C# parses per run. SVG XML parses, GPU scene compilations and CPU rasterizations did not increase during the measured zoom sequence.

The Gallery applies zoom, rotation, margin and corner changes to the preview immediately and refreshes the associated source when the interaction settles. The diagnostic mode `gallery-svg-profile` in the graphics test executable uses the actual Gallery page. Across two paired runs, frame composition medians dropped from 26.20–27.82 ms to 3.22–3.60 ms (86–88%). Median calling-thread allocations dropped from 10,343,740 to 327,552 bytes per frame. Renderer call medians were 6.01–6.52 ms before and 6.37–6.93 ms after. The final-frame check confirmed that both displayed sources and both vector previews agreed on the final zoom. These are managed diagnostics for one Gallery scenario. Production FPS and NativeAOT results remain unmeasured.

## Rapid transform preview

`UiSvg.UseFixedResolutionPreview` is opt-in. During a gesture, the widget uses a full GPU texture at its base resolution and applies zoom, rotation, clipping, tint and rounding to the image in the usual widget path. The Gallery uses it during interaction. When the idle timer expires, the Gallery disables it and uses the normal visible camera at the final resolution. Interpolating the preview can soften it during enlargement. XML, retained scene data and layout geometry are preserved.

On the RTX 5070 Ti Laptop GPU, two paired Windows managed .NET 11 Vulkan runs used 1280×900, Immediate presentation, three frame slots and 40 measured frames per sequence. Before the change, zoom generated a median of seven SVG compute dispatches per frame and zoom with rotation generated nine. After preview warmup both sequences generated zero. Median renderer call time went from 6.04–6.13 ms to 0.83–0.86 ms for zoom, and from 5.99–6.14 ms to 0.86–0.89 ms for zoom with rotation. Rotation alone at a fixed 2.5× zoom already reused vector output in this workload. The renderer call includes presentation. These results do not measure input-to-display latency or NativeAOT performance.

A pointer-held transform slider keeps the gesture preview active while its value stays still. The normal quiet deadline applies once capture ends. On the exact SVG path, a rotated widget intersects its local content with the inherited clip polygons before sizing the GPU output. The camera retains the same virtual extent and a one-pixel sampling border.

Two WSLg lavapipe Linux x64 NativeAOT comparisons with four software workers measured 1,029.9–1,053.0 ms before and 284.8–291.9 ms after for the final 2× 37° QueuePresent call. Unrotated 2× and 4× results overlap and remain slow. After both captures settled, nine visible rotated pixels differed by at most one 8-bit channel unit. The default transform matched below the diagnostic header. This timing does not include input-to-display latency.

## Blur cost at high zoom

With `DUXEL_VK_PROFILE=1` and `DUXEL_VK_GPU_PROFILE=1`, the log associates vector and first-blur device timings with the completed frame slot's submission sequence. The blur shader normalizes shared weights once and limits axis-aligned taps to valid source coordinates while preserving their order. In two WSLg lavapipe Linux x64 .NET 11 NativeAOT runs per build, 4× exact zoom QueuePresent decreased from 698.3–705.1 ms to 542.6–544.1 ms. Time in the first blur decreased from 409.0–411.5 ms to 263.1–266.8 ms. Twenty Windows GPU RGBA outputs matched before and after. The complete Chromium equality and hardware latency checks remain separate.

Before searching a curve's camera-dependent polyline, the shader rejects samples that cannot contribute to its winding using a conservative control-point hull. At fixed quality, two Linux x64 .NET 11 NativeAOT WSLg lavapipe pairs reduced 2× final QueuePresent from 547.0–555.5 ms to 420.4–428.2 ms and 4× from 536.8–545.0 ms to 417.4–418.7 ms. All 35 Windows GPU output pairs remained identical. This compares renderer builds on the same inputs and does not establish Chromium conformance or hardware latency.

The main browser comparison keeps the full GPU image at the browser reference resolution by default. It reuses that image throughout zoom, rotation and scrolling, including after input stops. For a pixel comparison at the final zoom, turn on high-resolution drawing. With that option enabled, a 120 ms pause in input requests the exact visible resolution. The C# example updates on its existing transform timer. See the [paired measurements](archive/performance/optimization-session-2026-11-19.md).

The Vulkan shader tests the four coverage samples before reading the material for sampled paths. If all four miss, it skips the color calculation and compositing. The RTX 5070 Ti Laptop test showed a small, repeatable drop in GPU time in nine of ten camera cases. Software Vulkan runs did not show a repeatable speedup. See the [coverage test and timings](archive/performance/optimization-session-2026-11-19.md#svg-material-evaluation-after-coverage).

In high-resolution mode, the 120 ms idle interval starts when the captured slider is released. The `gallery-svg-pointer` diagnostic records actual Gallery slider handling in `pointer.tsv` for either mode. [Slider verification](archive/performance/optimization-session-2026-11-19.md#svg-slider-release-and-consecutive-gestures)

For axis-aligned Gaussian blur, the renderer limits input reads to a conservative rectangle that contains the source drawing. After the first pass, the rectangle expands by the radius along that axis. The baseline and updated renderer produce the same tested output. [Blur pixel and timing comparison](archive/performance/optimization-session-2026-11-19.md#svg-blur-skips-transparent-input)

SVG path coverage reuses the camera values already calculated for the pixel. Compare timing only after checking the renderer's device type. On the tested Ubuntu WSL installation, the Gallery selected CPU-backed `llvmpipe` with the default driver and automatically selected fonts. [Device and timing evidence](archive/performance/optimization-session-2026-11-19.md#svg-path-coverage-reuses-camera-parameters)

With `DUXEL_VK_PROFILE=1`, `DUXEL_VK_GPU_PROFILE=1` and `DUXEL_VK_PROFILE_EVERY=1`, join completed GPU results by `gpuSubmission`. Alongside the first blur duration, the log reports `vectorBlurFilters`, `vectorBlurGpuUs` for all blur work, and `vectorOutsideBlurGpuUs` for the rest of the vector scope, including path work and copies. A submission with more than 16 blur filters keeps its count and first-filter timing and reports both aggregate timings as unavailable. [Gallery filter breakdown](archive/performance/optimization-session-2026-11-19.md#svg-filter-timing-by-submission)

For scroll and zoom response, first replay the input with detailed Vulkan profiling disabled. Collect GPU breakdowns in a separate run. Set `DUXEL_VK_PROFILE_EVERY=30` with the default `DUXEL_VK_PROFILE_TIMING_SAMPLING=1` for a sampled breakdown. Use an interval of 1 for individual submission analysis. Per-frame timestamp collection and log output can add measurable CPU time.

`vectorPathGpuUs` measures vector image rendering dispatches. `vectorCopyGpuUs` measures scene data, dependency table and final texture copies. Other stroke preparation and barriers can remain outside these measurements. Each of these two counts is recorded even if it exceeds 64. Its timing is unavailable above that limit. [Gallery path and copy measurements](archive/performance/optimization-session-2026-11-19.md#svg-path-dispatch-and-transfer-timing)
