# Dynamic 3D layers

## Final verification results — 2026-11-03

The verified distribution image is **DE068949E4A92689455341057405F6BFE0BEC38C9C89DC64D50A1D43A6D60715**. Later historical sections describe their original source/images;use this section and the [current FPS record](../artifacts/perf-2026-11-03/fps-current.json) for current results.

| ERD1200tables,MSAA4 | Median of3independent run averages | Run range | Target1000 |
| --- | ---: | ---: | --- |
| Camera | 1093.47FPS | 1046.70–1180.66 | 3/3pass |
| Camera plus entity motion | 1044.80FPS | 1028.66–1171.38 | 3/3pass |

Best single-run average1180.66FPS. .NET11 RC3 NativeAOT,`OptimizationPreference=Speed`,portable default CPU instruction set,VSync off,one-second preparation after first accepted presentation,then18actual measurement seconds. The clock measures accepted Vulkan presentation requests;it does not guarantee every frame within1ms or physical1000Hz display. Multiple changes separate earlier stages,so no isolated improvement percentage is inferred.

Layers are real3D planes with MVP/depth in the common Vulkan path,without per-layer bitmaps/composition targets. Camera-only world geometry builds are0and resident GPU data remains in place;GPU rasterization of final visible pixels still costs work. Minimap and other widgets use independent viewport/camera/depth scopes in the same path. Opaque color/depth coverage agrees at1x/4x;partially translucent materials use depth-read and back-to-front blending.1x has one-sample edge precision;default4x smooth quality is preserved.

GPU validation passes438checks each on NET10/11,414direct-path checks,9real sample/status-panel checks each,and Release0warnings/errors. The panel's top,bottom and spacing were reviewed in the current renderer's owned GPU buffer;three NET10/11frames are byte-identical. This is neither desktop capture nor a three-frame FPS measurement.

| Every3D layer changes every frame | 1layer×1000 | 8layers×1000 | 8layers×4000 |
| --- | ---: | ---: | ---: |
| .NET10 Managed | 8461.55 | 2453.86 | 719.93 |
| .NET11 Managed | 8044.89 | 2600.19 | 882.55 |
| .NET10 NativeAOT Speed | 7049.16 | 1815.60 | 581.45 |
| .NET11 NativeAOT Speed | 8124.30 | 2426.84 | 806.74 |

Values are medians of3independent run averages. All12executions,72phases,72raw-frame files,72GPU phases and12visible sample windows were verified. SDK10.0.403/runtime10.0.14 and SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 were used. MSAA1,VSync off,no static layer cache,serial UI-frame-start clock,.25second preparation plus1.5second measurement per phase,with reversed phase/variant order and equivalent visible-window conditions. These values are not directly comparable to the pipelined MSAA4 ERD presentation clock. Median/p95/p99/1%low,allocations,draws,uploads,GPU time and run ranges are preserved in [runtime results](../artifacts/perf-2026-11-03/coverage-complete-runtime/summary.json). Neither.NET11nor AOT is claimed fastest for every workload.

The prior three-run-per-variant180-second pixel-ownership comparison reduced private-byte growth median116.33MiB→0.375MiB(99.68%less growth). A final-image180-second observation records growth0.383MiB,working-set change-0.297MiB and normal exit.59.99idle seconds record0additional logged frames,62.5ms CPU and normal exit. No forced GC. This does not mean99.68%less total memory,universal leak freedom or VRAM measurement. [Current resource observations](../artifacts/perf-2026-11-03/coverage-complete-resources-audit.json).

Measured GPU:NVIDIA RTX5070Ti Laptop;other GPU performance is unverified. Glyphs are vector meshes with default curve approximation tolerance.001font pixels,not infinitely precise analytic glyphs. Bitmap magnification remains limited by source resolution. The pinned SDK10.0.301baseline and public supported TFMs remain intact.

Vulkan unions command bounds and classifies each list before applying layer state. Entirely offscreen lists skip matrix, viewport and depth-state changes. Lists with known bounds wholly inside the guarded frustum skip redundant per-command checks; intersecting lists retain them. Unknown bounds and callbacks remain conservative. Homogeneous clip coordinates include a two-pixel guard, and widget normalization is shared with the GPU state calculation. Geometry preparation/residency stays independent, so returning to an invisible layer does not upload its geometry merely because the camera changed. `UiRendererFrameMetrics.LayerTransformPushes` and `CulledLayerLists` expose actual transform updates and rejected lists.

Return frames from `UiContext.GetDrawData()` with `using var frame = ...` or `ReleasePooled()`. Frame-local draw-list wrappers may be reused after the frame is released; do not retain released lists. Copy the `UiLayer3D` value for a lasting camera snapshot. Geometry retention and frame-state lifetime are separate, and outstanding frame matrices remain unchanged. `UiRetainedLayerBatcher.Frame.DrawData` is a borrowed view: dispose the `Frame`, then the original input frame, rather than independently disposing the borrowed data.

`UiLayerOptions.Scene3D` projects a layer's geometry as a real plane in a 3D scene. Geometry may change every frame. There are no per-layer images or composition passes: the vertex shader transforms and projects geometry, and a Vulkan D32 depth attachment resolves visibility and intersections.

## Coordinates and API

The Vulkan backend applies `UiRetainedLayerBatcher` automatically. Adjacent resident lists with identical 3D transform, viewport and depth state share cached vector buffers and compatible draw commands. Camera changes update their transform without repacking or uploading geometry. Lists with callbacks or frequently changing content remain separate; changing an individual model transform may split this CPU group and cause an initial repack. The subsequent Vulkan compiler also combines lists with different matrices using GPU command data. Groups retain original submission order, local clips, opacity, texture and analytic curve coverage. The cache bounds group size and entry count, evicts unused data, and releases CPU buffers on disposal. `UiRendererFrameMetrics.BatchedSourceLists` and `RebuiltBatchGroups` distinguish CPU grouping from geometry regeneration.

Existing pixel coordinates are first normalized to the full display's `[-1, 1]` plane. `Transform` maps this plane to clip space and is `model * view * projection`. Pass `System.Numerics` row-vector matrices directly: GLSL's column-major interpretation and `matrix * vector` match without an extra transpose. Vulkan depth is `0..1`.

```csharp
var camera = Matrix4x4.CreateLookAt(new(0, 0, 3), Vector3.Zero, Vector3.UnitY)
    * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, width / height, .1f, 20);
var model = Matrix4x4.CreateRotationY(angle) * Matrix4x4.CreateTranslation(x, y, z);
var options = new UiLayerOptions(false, 1f, default)
{
    Scene3D = new UiLayer3D(model * camera)
};
if (ui.BeginLayer("moving-plane", options))
{
    ui.GetWindowDrawList().AddRectFilled(rect, changingColor, ui.WhiteTextureId);
}
ui.EndLayer();
```

Positions, colors, shape counts, and matrices may change every frame. Opaque visibility is independent of submission order. Where the front plane has no geometry, rear geometry remains visible. Equal depth uses `LessOrEqual`, so later geometry wins. Occlusion does not automatically remove CPU generation or upload costs.

Zero-alpha fragments do not write depth. Partial-alpha blending is a separate material condition: draw opaque geometry first, then transparent geometry back to front with `new UiLayer3D(transform, WriteDepth: false)`. Order-independent blending of intersecting translucent planes is not provided. Empty space and partial-alpha color blending are different cases.

Local clips follow the projected plane and are tested in the fragment shader. An untransformed CPU scissor must not reject moved or rotated geometry. Optional static caching retains geometry while allowing camera changes. An explicit nested `Scene3D` is a complete MVP and overrides the parent's MVP; otherwise the parent's MVP is inherited. Nested layers require distinct ids.

Ordinary 2D draw lists retain their ordering and do not use depth tests. Submit backgrounds before the scene and HUD/editor overlays after it. This is a rendering transform API; picking on projected 3D planes is not automatic.

## Execution and lifetime

- Dynamic `EndLayer` transfers frame-owned geometry without copying vertices, indices, or primitive arrays and without rebasing indices.
- Builders are reused by layer id. Existing frame-slot/fence ownership protects in-flight GPU buffers.
- Separate pipelines handle plain 2D, 3D depth reads, and 3D depth reads/writes. Specialization constants allow the driver to remove matrix/local-clip branches from the plain 2D shaders.
- One dynamic-rendering scope is used per frame, with the existing submit/present path. No offscreen color image is allocated per layer.
- Depth attachments and 3D pipelines are created on first use. Each swapchain image owns one depth attachment shared by all layers. Depth and color MSAA counts must match.
- Resize and MSAA changes recreate depth resources; shutdown destroys them. Once allocated, depth images are retained for reuse. A pure 2D run allocates none.
- `DUXEL_VK_PROFILE=1`, `DUXEL_VK_GPU_PROFILE=1`, and `DUXEL_VK_PROFILE_OUT` expose CPU/GPU timing, draw counts, actual dynamic upload bytes, and allocated depth bytes. Account for profiling overhead separately.

## Validation and measurement

```powershell
dotnet build Duxel.slnx -c Release
dotnet run --project tests/Duxel.Rendering.Tests -c Release -p:DuxelValidationHooks=true -- artifacts/rendering-validation
dotnet run --project tests/Duxel.Performance.Bench -c Release -- artifacts/layer-cpu.json
./run-fba.ps1 samples/fba/dynamic_3d_layers_bench.cs -Managed -NoCache
```

GPU tests read actual swapchain output before presentation and compare pixels. Readback is compiled only with `DuxelValidationHooks=true`; production builds have no readback calls, allocations, or waits. Also inspect the logs for Vulkan validation errors.

The dynamic sample normally stays open. Set `DUXEL_3D_BENCH_OUT` for automatic measurement and `DUXEL_3D_BENCH_REVERSE=1` to reverse phase order. Each phase warms up for 0.25 seconds and measures for 1.5 seconds by default. Every layer's positions, colors, and matrix change every frame. Workloads use 1/8 layers, 1,000/4,000 shapes per layer, and 0.2/0.8 overlap coefficients. Preserve the actual JSON phase order and each phase's raw frame CSV.

The 2D and 3D phases have different projected coverage and visibility; they measure feature cost and scaling, not a same-image before/after optimization. Keep them separate from the identical CPU fixture against the original source and the existing layer/widget runtime comparison.

## Performance decisions

Preserve the requested 3D architecture while improving throughput, latency, and memory efficiency. Do not abandon it solely because an initial implementation is slower, or substitute static caching/offscreen 2D composition. Measure the bottleneck and improve layout, transfers, batching, parallelism, and synchronization. Higher hardware utilization alone is not proof of improvement; correct output and repeated measurements are required.

## Cameras, resident geometry and widget viewports

Set `ContentVersion` on `UiLayerOptions` to retain geometry independently of the camera. `BeginLayer` returns false while that version remains unchanged; always call `EndLayer`. Update `Scene3D` every frame. Increment the version when geometry, local layout, text or material data changes, or call `MarkLayerDirty`. Leave the version null for fully dynamic content. Both modes use the same renderer, without a layer image. Retained geometry owns its pooled arrays and pins referenced native text resources until replacement or removal. Do not skip interactive immediate-mode widget logic inside a versioned geometry body.

For independent widget cameras, set `UiLayer3D.Viewport` to the content rectangle. Source pixel coordinates then normalize relative to that rectangle; the GPU viewport and screen clip restrict output to it. `(DepthScopeId, Viewport)` identifies a depth scope. Lists in each scope must be contiguous. A new scope clears its depth region within the existing rendering scope, preserving color; widget panels can cover a previously drawn world scene without inheriting its depths. `DepthScopeId` distinguishes independent scenes sharing a viewport. This provides no per-widget color target or extra composition pass.

On devices with Vulkan `shaderClipDistance`, a fractional projected viewport uses an integer hardware viewport and a remapped projection. Four clip planes enforce the original fractional bounds for both ordinary and cutout geometry. Devices without the feature, and viewports whose integer envelope would exceed device limits, use the original viewport path. Depth scope identity still uses the requested viewport and scope ID.

At 4× MSAA, Vulkan uses an explicit sample mask for depth-writing rounded fills, gradients, and rounded images when the device reports standard sample positions. Fractional framebuffer viewports need `shaderClipDistance`. Integer framebuffer viewports within device limits also support the mask when that feature is unavailable. The renderer verifies that the geometry and texture are opaque. Each covered sample writes both color and depth. Partial texture uploads invalidate the opacity proof, and image draws with an independent UV edge use the ordinary coverage path.

Moving a layer with `UiLayerOptions.Translation` preserves the opacity proof for its geometry. Lowering the layer opacity uses ordinary coverage and blending.

`scene.TryMapScreenPoint(screenPoint, displayBounds, out localPoint)` inverts the same MVP and intersects the pointer ray with the local plane. It rejects points outside the viewport and singular transforms. Use the returned source pixel coordinates for content hit tests; this does not automatically transform every existing immediate-mode widget's input handling.

Run `./run-fba.ps1 samples/fba/dynamic_layer_camera_fba.cs -Managed -NoCache` for a global camera and two independent widget cameras. Wheel zooms, right drag pans. The resident mode builds five layer bodies once; the animation button switches to frame-by-frame geometry updates. Resize changes the content version. Set `DUXEL_CAMERA_DEMO_SECONDS` for an automatic exit.

Analytic circles and rounded rectangles retain their mathematical boundaries on the GPU. `fwidth` adapts antialiasing to final screen pixels, including perspective scaling; zoom does not magnify a rasterized layer. GPU tests compare an 8x camera zoom with a directly enlarged analytic vector while asserting no geometry rebuild or upload. Explicit polygon meshes retain their original tessellation. Native text in 3D layers now requires `IPlatformVectorTextBackend`: Windows provides DirectWrite outlines tessellated by Direct2D, cached as vector geometry. Its default flattening tolerance is 0.001 source font pixels and the demo uses 4x MSAA; approximation error scales with zoom. Ordinary 2D native text keeps its raster path. Raster image detail remains limited by source resolution. See the [ERD layer lab](erd-layer-lab.md) for editing and update-mode comparisons.

See `tests/runtime-net11/README.md` for the isolated .NET 11 comparison commands. The repository SDK remains pinned to .NET 10. See the [optimization session](optimization-session-2026-11-03.md) for measurements, hardware, and limits.

## Combining different matrices in one GPU draw

When only a static layer's texture changes, its command-array size and geometry may remain identical. A changed command-topology stamp still issues a new command wrapper so GPU grouping observes the new material boundary without geometry reupload. Visibility, clipping, opacity and cameras remain separate frame state.

The common CPU batcher skips command checks and content-history lookups when neighboring lists have different 3D state. Combined camera wrappers are reused within the exclusive frame-borrowing contract without changing source scene snapshots. `Frame.DrawData` is borrowed and must not be used after `Frame.Dispose()`. This optimization is independent of GPU command combining.

Layers sharing a matrix basis push the common basis once and fetch only their individual last row. Set `DUXEL_VK_SHARED_LAYER_BASIS=0` for comparison; `SharedBasisLayerDraws` counts its use. CPU visibility matrices and the matrix actually pushed to the GPU are tracked separately, allowing adjacent groups to reuse the same basis push. Different rotations or perspective matrices retain the existing full-matrix contract.

Camera motion does not regenerate, tessellate or upload retained shapes. Visible vectors still require GPU projection and rasterization because their screen pixels change. Zero geometry regeneration does not mean zero screen-rendering cost.

Set `UiLayer3D.PlaneModel` to a `UiLayerPlaneModel` to retain plane position and depth separately. `Transform` then holds the common camera. `Translation` uses source pixels before projection; `DepthOffset` is a normalized post-projection depth offset (`clip.z += offset * clip.w`). Physical world-Z translation and individual rotations use the existing complete-MVP contract. Both contracts share the renderer and depth attachment.

```csharp
var scene = new UiLayer3D(camera)
{
    Viewport = contentBounds,
    PlaneModel = new UiLayerPlaneModel(new UiVector2(32, 16), -.02f),
};
```

Vulkan shares the camera across adjacent compatible planes and retains a 16-byte model payload inside the existing 64-byte compatible slot. After slot warmup, camera-only motion writes zero per-plane buffer bytes; moving one plane writes its 16-byte model for that slot. Common camera push constants are separate from this buffer metric. Consecutive identical cameras also reuse viewport normalization. Mixed cameras or general 3D matrices use the existing full-matrix contract, updating each slot's stored representation on transitions. Compare using `SharedCameraLayerDraws` and `DUXEL_VK_SHARED_LAYER_CAMERA=0`. `GetEffectiveTransform(displayBounds)` and `TryMapScreenPoint` include the same model. CPU traversal for layer submission and visibility remains.

The Vulkan resident layer compiler carries different model/camera matrices, local clips, translations, opacity and analytic primitive kinds in per-command GPU data. In an index stream preserving command and triangle order, the high eight bits select command data and the low sixteen bits select the original vertex. Adjacent lists sharing viewport, depth scope, depth-write mode, texture and font output mode use one actual indexed draw. This reduces real GPU draws rather than relabeling a multi-draw API call.

A group holds at most 256 commands and 1,048,576 indices. Long source lists are partitioned internally rather than excluding large relationship lists. The index cache is bounded to 256 groups and 512MiB, with explicit domains for the compiler and existing direct submission. Mixed-texture lists, frame-generated lists, callbacks, legacy primitive triangle expansion, source vertex counts above 65,536 and individual primitive commands above 65,536 virtual vertices retain the existing submission contract. Hidden commands are excluded through cached index ranges; adjacent visible ranges merge. Portions of the same list share one visibility classification per frame. Camera changes do not rebuild indices, and vertex-only content revisions retain compiled topology and the grouping plan.

GPU descriptors belong to frame slots, are updated after the slot fence and receive only changed data. Command data and matrices are separate so commands sharing a layer do not retransmit the same matrix. Comparisons use CPU shadow copies, avoiding GPU memory reads every frame. Resize, MSAA changes and shutdown release these resources; replaced groups retire after in-flight frames. Original and compiled indices currently coexist. The default ERD adds approximately 270MB of index data, so memory optimization remains incomplete.

`UiRendererFrameMetrics.CompiledLayerDraws` counts actual combined draws; `CompiledLayerCommands` counts their source commands. `CompiledIndexUploadBytes`, `LayerStateUploadBytes` and `CompiledIndexCacheBytes` separate index transfers, state transfers and retained indices. Existing `DrawCalls` remains the total GPU draw count. Set `DUXEL_VK_COMPILED_LAYERS=0` to select direct submission for comparison. Combining is enabled by default; both routes retain the same vector quality, depth and clipping contract.

`UiDrawList.CommandStateStamp` is a nonzero identity for unchanged command descriptor state. Use `ComputeCommandStateStamp(commands)` after changing command kind, ranges, texture, clip, translation or opacity; zero means unknown and disables descriptor reuse. A copied draw list with edited commands must recompute or clear the stamp. Geometry bindings and display transforms invalidate cached descriptors independently. Resident replay and retained batching populate the stamp automatically. Warm camera-only frames skip descriptor reconstruction while retaining CPU visibility checks and GPU drawing. Hidden command descriptors are populated before caching a source; display-context changes invalidate hidden members too. `UiRendererFrameMetrics.CompiledDescriptorBuilds` counts CPU descriptor constructions, separately from GPU transfer bytes. `DUXEL_VK_DESCRIPTOR_REUSE=0` disables this optimization; `scripts/measure-erd-layers.ps1 -CompareDescriptorReuse -Executable <exe>` compares both paths in one executable.

`UiRendererFrameMetrics.PresentationSequence` advances only after a successful/suboptimal Vulkan `QueuePresent`. `PresentationTimestampTicks` uses `Stopwatch` ticks; `PresentationIntervalSeconds` is the interval between accepted requests (zero for the first). Consume an interval only when its sequence advances: skipped renders can leave the previous metrics intact. These counters measure application presentation throughput, not monitor refreshes or GPU completion. The ERD HUD/raw CSV now use this clock and export `fpsTimingBasis: accepted-presentation`; historical results without that field used UI-loop intervals. The measurement runner rejects mixed timing bases in a comparison.

Fully visible compiled members with current descriptor state use their contiguous cached index span without visiting each source command. Partial visibility and unknown/changed state keep command-level traversal. Precomputed kind-transition summaries preserve profiling counters, including boundaries between members and empty commands. `CompiledCommandVisits` counts actual source-loop visits; `CompiledLayerCommands` retains the number of submitted nonempty source commands. Set `DUXEL_VK_COMMAND_SPANS=0` or use `-CompareCommandSpans -Executable <exe>` for comparison. Camera/model state updates and real GPU draws remain necessary.

Aggregate culling bounds are cached by nonzero `CommandStateStamp` in the common Vulkan layer visibility path, including direct submission. `ComputeCommandStateStamp` now includes bounds coordinates and `HasBounds`; callers must refresh the stamp when either changes. The cache retains source-space bounds, then reclassifies them with the current camera/model/display context every frame. Unknown stamps recompute bounds; unknown/invalid bounds retain conservative visibility. Reordered lists compare state before reusing a slot. `LayerBoundsCommandVisits` counts commands scanned while aggregating layer bounds, separately from `CompiledCommandVisits`. Set `DUXEL_VK_LAYER_BOUNDS_REUSE=0`, or use `-CompareLayerBoundsReuse -Executable <exe>`, for comparison. This caches CPU bounds only; it adds no raster layer target and does not suppress GPU vector rendering.

`UiImmediateContext.TryReplayLayer(id, in options)` submits valid retained layer content without opening a layer scope. On false, use the normal `BeginLayer`/`EndLayer` path to build or refresh it. A matching content version (or static cache), clean entry and stored content are required; valid empty content returns true. A miss does not open a scope or change the stored version. Successful replay validates the current scene and preserves opacity, translation, parent text dependencies and frame-owned camera wrappers. Do not call `EndLayer` for a successful direct replay. The ERD uses this common engine API for its grid, relationship batches, selected connectors, tables and overview. `DUXEL_ERD_DIRECT_REPLAY=0` selects normal scopes; `scripts/measure-erd-layers.ps1 -CompareDirectLayerReplay -Executable <exe>` compares both in the same executable. This avoids scope setup/copies; it still performs per-layer lookup and submission and is not a bulk retained-scene API.

```csharp
if (!ui.TryReplayLayer(id, options))
{
    if (ui.BeginLayer(id, options)) DrawContent(ui);
    ui.EndLayer();
}
```

Retained geometry and command state are protected by one CPU-frame lease, acquired on the first retained submission and transferred to `UiDrawData`. Retired pooled sources drain when their last referencing frame epoch finishes; continuously overlapping newer frames do not prevent old retirement. A retained source or replay command array is copied before modification while a frame still borrows it. Camera-only frames keep sharing the unchanged geometry. This adds no GPU idle wait or layer render target.

`GetDrawData()` transfers responsibility for the returned ownership object: dispose it after consumption. Repeated calls or record copies do not create additional ownership. `UiContext` releases unclaimed output at the next frame/render, EndFrame or disposal, and also releases pending retained leases from an abandoned render. Claimed frames can retain their geometry beyond context disposal. These guarantees cover geometry/command snapshots and do not make UiContext concurrently writable.

`UiDrawData.ScaleClipRects` and `UiDrawList.ScaleClipRects` produce independently owned copies, including empty cases. Frame geometry/camera leases are not copied; texture-update containers are copied along with geometry and commands. Scaled frame texture payloads are captured into independently owned memory. A scaled copy and its original can be disposed independently.

`UiImmediateContext.GetDrawDataFrameId()` identifies the current render pass within one UiContext. The value is copied into `UiDrawData.DrawDataFrameId`, preserved by clip copies and carried into `UiRendererFrameMetrics`. It is distinct from the renderer's accepted `PresentationSequence` and from geometry content versions.

`GetPresentationReader()` returns an `IUiPresentationReader` with an atomic latest snapshot, a bounded FIFO of accepted presentation samples and an explicit overwrite count. `Drain(Span<UiRendererFrameMetrics>)` consumes samples for one reader; the reader can collect final results after UI state buffer disposal. UI convenience methods expose the same stream. A duplicate presentation sequence updates latest metrics without another sample; a regressing sequence is rejected within one renderer history. GPU timestamp values retain their existing frame-slot delay and are not reinterpreted as timing for the current source ID. The UI context itself is not concurrently writable.

ERD now uses `presentation-history-v1`, matches CPU scene telemetry by source frame ID, checks every accepted sequence, uses the actual presentation timestamp for warmup and drains final results on save. Legacy one-column frame CSV remains unchanged; an additional `.presentations.csv` links each sample to its sequence/source ID. It reuses BenchFrameRecorder.FrameSeconds instead of keeping a duplicate duration buffer; source IDs replace that buffer at the same capacity. The runner audits link rows, ordering, counts, overwrite state and warmup metadata. Mixed sampling methods cannot produce an improvement percentage. The bounded opt-in pipeline described below consumes this presentation history.

`DuxelFrameOptions.EnableRenderPipeline` opts into a bounded UI/renderer pipeline (default false). A dedicated consumer owns Vulkan calls, settings and presentation feedback; the producer remains the sole UiContext writer. One pending frame plus one executing frame applies backpressure without dropping frames. Settings travel with their source frame; resize and first-frame notifications follow actual accepted presentation. Shutdown drains and joins before UI/renderer disposal; consumer errors release queued ownership, wake shutdown and propagate the original exception.

`UiDrawData.CaptureTexturePayloads()` captures all texture-update payload slices into one pooled frame-owned buffer before exclusive thread handoff. Frames without payload bytes do not allocate a buffer; unchanged retained geometry is not copied. Call before the producer can modify source pixel memory. `ScaleClipRects` also snapshots texture payloads, so its copy survives original-frame disposal. A record copy remains shared ownership, not a second independent lifetime. Callback-containing frames require a completion barrier before the producer starts another UI frame; ordinary frames overlap. This preserves UI/callback exclusion, not callback thread identity. External callback closures must not assume the UI producer thread.

ERD selects this engine option with `DUXEL_ERD_PIPELINE=1`; per-run JSON records `renderPipeline`. The performance runner's `-CompareRenderPipeline` alternates Off/On in the same executable and checks the returned mode. Current GPU tests include bounded queue backpressure, FIFO shutdown, failure propagation, texture ownership and three retained geometry revisions produced before Vulkan consumption; the public path remains opt-in pending full performance and interaction verification. In pipelined APP_PROFILE output, `getDraw` includes queue residence and `total` is source-to-render completion latency, not serial CPU work. SubmitTimeMs is the latest completed render duration.
Pipeline warmup uses `fpsWarmupMethod: first-accepted-presentation-v1`: the deadline is exactly one Stopwatch second after the first accepted presentation, never after the second UI call. JSON includes the start tick and clock frequency; the comparison runner checks this duration, the first measured timestamp and exact samples/framesAfterWarmup equality. Different warmup methods cannot produce an improvement percentage. The earlier UI-anchored twelve-run set is retained at render-queue-ab with INVALID_FOR_FINAL_COMPARISON qualification; its FPS is not the current result. The source inspection and corrected rerun budget are preserved. ERD now enables the engine pipeline by default; `DUXEL_ERD_PIPELINE=0` explicitly selects serial execution. The library option itself remains opt-in for consumers with callback thread-affinity requirements.
Producer admission waits for the pending slot before sampling input and constructing UI. The application therefore does not build a third CPU frame behind one active and one pending frame. Renderer and UI work still overlap. Callback commands are executable work even with zero elements/geometry: builder flush, static append and retained replay preserve them; adjacent identical hooks never merge; Vulkan invokes each hook in command order. Callback-bearing frames wait for completion before the next UI. Hooks run on the consumer thread in pipeline mode and must not mutate submitted geometry or recursively render/change Vulkan recording state.
`DuxelFrameOptions.UseEventDrivenPlatformLoop` lets the platform owner wait while UI and renderer threads continue running. It requires the explicit `IPlatformEventWakeBackend` capability and fails before GPU setup if unavailable; the library option defaults false. Windows implements a latched thread-safe `WakeEvents()` and waits on that event plus queued input using MsgWaitForMultipleObjectsEx/MWMO_INPUTAVAILABLE. A SafeHandle reference protects the event during the native wait. Previously observed unread messages still dispatch. Wake/result failures are explicit; no polling timeout substitutes for a failed wake.

Exit interrupts UI and platform waits and cancels resize acknowledgement waits. Cursor changes and worker errors wake the platform owner. Session wake callbacks are detached under the same lock used by concurrent callers before backend/handle disposal. RequestFrame can advance UI/render work while the owner sleeps. The existing present-based first-frame and resize notifications are unchanged. Tests now reference the actual App assembly; real application tests use separate owner threads because WM_QUIT belongs to a thread queue. Test watchdogs fail the case rather than claiming successful production wake.

ERD enables this option by default (`DUXEL_ERD_EVENT_LOOP=0` selects polling). `-ComparePlatformEventLoop` alternates both modes in one executable with the render pipeline enabled. JSON records the mode, application-run CPU milliseconds and monotonic wall milliseconds, their ratio `averageBusyCpuCores` (one full logical core = 1), and process-lifetime peak working set. CPU scope includes initialization, warmup and shutdown; peak working set is not GPU VRAM or a steady-state heap measurement. Camera timing/geometry/quality are otherwise unchanged.
Detailed CPU profiling now samples the frames that `DUXEL_VK_PROFILE_EVERY` will log. `DUXEL_VK_PROFILE_TIMING_SAMPLING=0` retains every-frame private CPU durations;1 is the default. Invalid boolean values fail explicitly. The single renderer timing clock returns0 for an uncollected start and its elapsed helper returns0 for that sentinel, avoiding fabricated elapsed values. Draw/upload/residency/command counters, allocation measurements, GPU timestamp queries and accepted-presentation timing remain independent and run as before. No geometry, shader, MSAA or camera behavior changes.

`UiRendererFrameMetrics.CpuTimingCollected` identifies frames with detailed CPU durations; it does not mean counters/GPU timing were skipped on other frames. ERD JSON reports actual `cpuTimingCollectedFrames` and `gpuTimingSamples` after warmup. `-CompareCpuTimingSampling` compares both modes in the same native image with a1,000,000-frame log interval, both engine pipeline/event loop enabled. It checks baseline CPU timing on every measured frame, sampled timing on no short-run frame, positive GPU timing/draws and unchanged warmup/process contracts. Idle is rejected before a child starts because its benchmark collector intentionally records no timing samples.
Automatic transient CPU builder reservation now follows the largest owned draw-list chunk. Borrowed resident layer vertices/indices no longer inflate temporary buffer reservation. Public scene totals,GPU geometry and explicit Reserve are unchanged. NET10/NET11 GPU366 checks each and direct submission342 pass. NativeAOT performance/memory comparison is pending; this is not yet a claim of improved FPS or reduced working set.

Per-frame static geometry bindings use a dense array indexed directly by draw-list number. Preparation,dynamic upload,compiled plans and command recording share the same frame binding. Reset clears previous references and sets the new list range,so shrinking/empty frames and dynamic lists cannot inherit stale bindings. GPU ownership,fences and retirement remain unchanged. Removing hash lookup is a performance hypothesis until a separate NativeAOT comparison completes.

## Current-source .NET10/11 comparison(2026-11-03)

SDK10.0.301/runtime10.0.9 and SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148;11 is RC. Four same-source Managed/NativeAOT combinations ran three times each:12executions/72phases. Source hashes,execution mode,raw sample counts and process termination were verified. Each phase uses0.25s warmup+1.5s measurement;run2 reverses variant and phase order. Every layer changes every frame without static layer caching. MSAA1,VSync off,serial rendering,UI frame-start interval clock. These numbers are not directly comparable to the ERD MSAA4,pipelined,accepted-presentation1000FPS target.

Below are medians of three independent run averages/distributions. Disjoint .NET10/11 FPS ranges establish Managed8×1000(+16.52%),NativeAOT1×1000(+23.37%),8×1000(+21.37%),8×4000(+38.35%). Other FPS ranges overlap,so no improvement percentage is claimed. NativeAOT is not assumed to be fastest. Raw records and all2D/3D phases/ranges are preserved in artifacts/perf-2026-11-03/runtime-current/summary.json.
| Variant | Layers × shapes | FPS | median ms | p95 ms | p99 ms | 1% low FPS | Scene bytes/frame |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| net10-aot | 1 × 1000 | 6462.2 | 0.1279 | 0.2423 | 0.3639 | 1621.93 | 432 |
| net10-aot | 8 × 1000 | 1930.36 | 0.4921 | 0.6273 | 1.0268 | 685.18 | 3456 |
| net10-aot | 8 × 4000 | 570.06 | 1.6264 | 2.4658 | 3.2484 | 244.62 | 3456 |
| net10-managed | 1 × 1000 | 8121.65 | 0.1031 | 0.2039 | 0.32 | 1988.81 | 400 |
| net10-managed | 8 × 1000 | 2280.42 | 0.3734 | 0.704 | 1.126 | 685.64 | 3200 |
| net10-managed | 8 × 4000 | 699.23 | 1.1496 | 2.2268 | 2.767 | 276.41 | 3200 |
| net11-aot | 1 × 1000 | 7972.37 | 0.1121 | 0.1856 | 0.2874 | 2083.27 | 432 |
| net11-aot | 8 × 1000 | 2342.79 | 0.3998 | 0.5634 | 0.9127 | 836.02 | 3456 |
| net11-aot | 8 × 4000 | 788.66 | 1.2213 | 1.4647 | 2.0485 | 387.47 | 3456 |
| net11-managed | 1 × 1000 | 8302.55 | 0.1017 | 0.1974 | 0.32 | 1984.42 | 401.323375893359 |
| net11-managed | 8 × 1000 | 2657.16 | 0.3535 | 0.4946 | 0.7595 | 942.81 | 3200 |
| net11-managed | 8 × 4000 | 867.37 | 1.1118 | 1.354 | 1.9715 | 417.73 | 3200 |
GPU profiling is a separate logged-frame diagnostic at interval64. The first log of each phase is excluded to avoid cross-phase delayed-query data;warmup remains included. It is not the FPS sample set. gpu-profile-summary.json preserves GPU time,draw calls,dynamic upload and depth-attachment bytes for72phases;it is not total VRAM. audit.json confirms12runs/72raw frame files/72GPU phases and0remaining sample children.

Compiled groups default to2,097,152 index entries. DUXEL_VK_COMPILED_INDEX_LIMIT strictly accepts1,048,576/2,097,152/4,194,304/8,388,608;other values fail explicitly. The256-descriptor and512MiB total index-cache limits remain. UiRendererFrameMetrics.CompiledIndexLimit reports the actual setting. ERD JSON compiledIndexLimit/compiledIndexCachePeakBytes report actual capacity and maximum observed compiled-index cache bytes over measured frames,not total VRAM. scripts/measure-erd-layers.ps1 -CompareCompiledIndexCapacity alternates1Mi/2Mi in one executable and verifies capacity/cache/draw/GPU/presentation data. A1.2M-index GPU boundary case confirms2→1draws,identical pixels and zero camera-only reupload. NET10/11pass390checks each,direct366,runner10/10. ERD FPS impact remains unproven until repeated comparison.

Completed same-image1Mi/2Mi index-capacity NativeAOT comparison(2026-11-03):1200tables,dense/resident,MSAA4,VSync off,three nominal18-second executions per mode/scenario. Current camera988.05 FPS(98.80%,range899.53–1044.10),motion966.90 FPS(96.69%,828.92–1074.02);best single-run average1074.02. Neither meets1000 across runs.1Mi→2Mi FPS923.78→988.05 and1058.46→966.90 has overlapping ranges;no improvement/regression percentage is established. Mean draws97.59→91.91 and80.69→74.65;compiled index-cache peak270330504/270330552bytes is unchanged between modes. Distinguish the deterministic2→1draw/exact-pixel boundary proof from real-run averages. Per-frame means include camera-state sample weighting,not pure same-view work reduction. Raw frames,presentation links,actual limits/cache,warmup,process metrics and0/1scene-builds audits pass12/12. Default EXE/PDB matches measured image60FE419D. See fps-current.json and index-capacity-ab.

Measurement inspection:App auto-exit starts at appStartUtc before font setup;ERD tour time advances from rendering start;presentation sampling begins after first accepted present+1second. Actual sampled intervals span14.84–15.21seconds inside nominal18-second runs,and mean visible command counts differ. This does not prove every cause of FPS variability. Before more full comparisons,separate preparation from measured camera/motion workload start. Preserve these results as observations under their original nominal-run contract. runtime-current.NET10/11 results use matching source hashes before this capacity change;do not promote them to final performance measurements of the new image.

The real dynamic3D sample and window chrome were rendered by the current renderer and reviewed through its owned GPU buffer: panel title,bottom line,compact line spacing and content-sized panel are visible. NET10/11 each pass8/8,including colored geometry;final pixels are identical. Evidence:artifacts/perf-2026-11-03/status-panel-gpu-review.json. This is neither desktop/DWM capture nor an FPS measurement. The initial texture-order hypothesis was rejected by exact pixel equality;no engine defect is claimed.

Optional `IMemoryOwner<byte>` properties `PlatformTextRasterizeResult.PixelOwner` and `UiTextureUpdate.PixelOwner` transfer exclusive pixel ownership. Pixels remain immutable while consumed. `UiDrawData.CaptureTexturePayloads()` copies existing borrowed updates;an exclusively owned update stays with its frame without another copy. Frame consumption/discard,superseded queued updates,measurement-only text eviction and context disposal release ownership. GPU textures retain the existing fence/retirement lifetime.

Windows multiline bitmap composition uses returnable pooled storage,clears its pixels and performs the same composition. Measurement-only backend calls also return storage.3D vector layers,shaders,font outlines and camera quality remain unchanged. Direct rasterization callers dispose PixelOwner after use or transfer it once to a texture update;they must not return or mutate storage after transfer.

Validation:.NET11 GPU422,.NET10 text-atlas422,direct398;all119before/after GPU images are byte-identical. Lifetime checks cover mixed owned/borrowed frames,consumer-thread return,frames outliving their context,unconsumed frames,cache eviction and actual raster byte reuse including transparency. Candidate52E300FA's first180-second observation has private/working-set median changes-10559488/-11585536bytes;this is one run. Quantitative claims await three independent runs per variant. Default1000FPS image09584FF4remains protected.

2026-11-03 MSAA depth-boundary fix: 3D depth-write pipelines send analytic AA alpha to hardware alpha-to-coverage at every sample count,including1x. Only covered samples write opaque RGB and depth,so rounded rectangles,circles and borders produce identical full images in either submission order over a back plane. No per-layer texture,composition pass,sample-rate shading or alpha-to-one feature is added.2D and depth-read material alpha blending retain their existing paths.1x has one-sample edge precision;default4x quality is unchanged. A finite sample mask does not replace continuous material translucency or arbitrary-order transparency. The reproduced389-pixel order difference at1x opaque boundaries is now0 and covered by a regression check. Sample-mask mapping is driver-dependent;this is not proof of testing both GPU vendors.

Before GPU initialization,the actual loaded fragment shader is checked against the declared artifact SHA256. A copied temporary SPIR-V retained an older timestamp and caused an incremental build to retain its previous embedded resource;the final artifact timestamp and rebuild were verified. Final GPU validation passes435checks each on NET10/NET11,411direct-path checks,9actual status-panel checks each,and119/119unchanged existing images;Release has0warnings/errors. A separate2048-shape/16-plane/320×240/MSAA4 Managed probe uses2seconds preparation plus6seconds observation,three runs per variant,with audited contiguous completion intervals. Median FPS7458.46→7306.80(observed-2.03%);ranges overlap,so neither gain nor regression is established. This is separate from ERD1000FPS acceptance. Evidence:`coverage-embedded-net10/`,`coverage-embedded-net11/`,`coverage-embedded-direct/`,`coverage-probe-v2-summary.json`,`coverage-existing-pixel-audit.json` underartifacts/perf-2026-11-03.

## Reproducible current verification commands

Use the latest source. SDK10.0.403/runtime10.0.14 and .NET11 RC3 have isolated selection directories;the repository baselineSDK10.0.301 is preserved. NativeAOT throughput runs explicitly select `OptimizationPreference=Speed` and retain the portable default instruction set. [Official optimization setting](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing).

```powershell
# .NET11 RC3 NativeAOT ERD,1200tables,MSAA4,tour/animation enabled by default
Push-Location tests/runtime-net11
../../run-fba.ps1 ../../samples/fba/erd_layer_lab.cs -TargetFramework net11.0-windows7.0 -NativeAot -NoCache -ExtraArgs @('-p:OptimizationPreference=Speed')
Pop-Location

# Recheck raw FPS,percentiles and1%low without relaunching the app
./scripts/measure-erd-layers.ps1 -SummarizeOnly -OutputDirectory artifacts/perf-2026-11-03/coverage-complete-erd

# Common FBA extra-argument regression
./tests/RunFbaArgumentTests.ps1
```

`scripts/measure-dynamic-3d-runtimes.ps1` provides the reusable NET10/11 Managed/NativeAOT comparison. Every mode uses a normal visible sample window;an independent read-only observer verifies visibility per execution. Earlier hidden-Native/visible-Managed results are excluded from direct mode comparisons. Raw frames independently verify averages,linear percentiles and1%low;GPU timestamps,actual draw/upload counters,source hashes and child exits are checked before recording each run. Specify `-SourceManifestPath` and a fresh `-OutputDirectory`. Prebuild native images from the same source and match their build preference to recorded `NativeOptimization`.
