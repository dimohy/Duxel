# Rendering and dynamic 3D layers — 2026-11-03

Status: implementation and focused GPU validation are available; final comparison, interactive review and completion audit remain open. This is not a whole-goal completion report.

## Environment and inputs

- Windows, NVIDIA GeForce RTX 5070 Ti Laptop GPU; graphics/upload queue 0. Hardware-specific results do not establish AMD or Intel performance.
- Repository baseline SDK 10.0.301, runtime 10.0.9. Isolated comparison SDK 11.0.100-rc.3.26522.148, runtime 11.0.0-rc.3.26522.148. .NET 11 is an RC.
- Original source: commit `016486667e183acd4e39a52ff3ee15fb4c09788e`, archived core under `artifacts/perf-2026-11-03/baseline/src/Duxel.Core`.
- Raw JSON, frame CSV, shader binaries and stdout/stderr evidence: `artifacts/perf-2026-11-03/` (local ignored artifacts). Results below are fixture-specific.

## Hypothesis → changes → validation

1. Dynamic layer creation copied geometry and repeatedly allocated builders. Reuse builders and transfer frame-owned vertex/index/primitive lists without copying or index rebasing. Borrowed data still obeys its ownership contract. Bulk-copy the sequential 20-byte vertex ABI to mapped GPU memory. The CPU fixture and GPU ownership tests exercise these paths.
2. Per-layer images are unnecessary for planar 3D geometry. Add per-list MVP, specialized 2D/depth-read/depth-write pipelines, local fragment clipping and a shared D32 attachment per swapchain image. Keep one rendering scope; lazily allocate depth and recreate it on resize/MSAA changes. Real swapchain readback validates depth, intersections, gaps, alpha and 2D overlays. Correct the preexisting Vulkan `ImageMemoryBarrier` structure tag to 45, exposed by validation.
3. Camera-only updates need no content regeneration. `ContentVersion` retains original geometry with explicit ownership, while matrices and widget viewports remain dynamic. Track exact native text dependencies to prevent eviction while retained geometry still references them. Distinct viewport/depth scopes share the same rendering path. Tests inspect actual upload counters and inverse pointer projection.
4. Zoom must preserve vector boundaries. Analytic rounded rectangles and circles evaluate their distance and screen-space derivatives in the fragment shader. The 8x camera zoom test matches a directly enlarged analytic vector without rebuilding or uploading geometry. Raster text and images are explicitly not claimed to become vector assets.
5. Position the status panel relative to the viewport work area and size it from measured multiline text plus title/padding. Use tight line spacing. `status-panel-tight.png` records the compact, unclipped result; final camera-demo review is pending.

## CPU fixture (three independent runs)

Release, 0.25 s warmup and 1 s measurement per case. The same geometry changes every frame, without static caching. Each reported metric is the median across the three independent runs. These runs are not an interleaved final A-B-A-B confirmation; timing improvement percentages remain provisional until that check. Allocation reduction is directly observed.

| Layers × shapes/layer | Original median ms | Final .NET 10 median ms | Original bytes/frame | Final .NET 10 bytes/frame | Final .NET 11 median ms | Final .NET 11 bytes/frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 × 100 | 0.0147 | 0.0044 | 169589 | 662 | 0.0045 | 655 |
| 1 × 2000 | 0.1090 | 0.0501 | 169576 | 656 | 0.0504 | 648 |
| 8 × 100 | 0.0921 | 0.0395 | 1352926 | 3288 | 0.0371 | 3280 |
| 8 × 2000 | 0.9919 | 0.55305 | 1352802 | 3288 | 0.5124 | 3280 |

| Case | .NET 10 FPS | median/p95/p99 ms | 1% low FPS | .NET 11 FPS | median/p95/p99 ms | 1% low FPS |
| --- | ---: | --- | ---: | ---: | --- | ---: |
| 1 × 100 | 176052 | 0.0044 / 0.0100 / 0.0123 | 40486 | 185603 | 0.0045 / 0.0082 / 0.0098 | 64007 |
| 1 × 2000 | 19720 | 0.0501 / 0.0531 / 0.0575 | 11803 | 19623 | 0.0504 / 0.05259 / 0.0581 | 11233 |
| 8 × 100 | 23188 | 0.0395 / 0.0569 / 0.067688 | 8303 | 25806 | 0.0371 / 0.0413 / 0.0767 | 7468 |
| 8 × 2000 | 1532 | 0.55305 / 0.84824 / 1.011619 | 678 | 1818 | 0.5124 / 0.74551 / 1.201576 | 656 |

Do not infer that .NET 11 is uniformly faster: throughput and tail metrics disagree in some cases. Preserve run-level distributions. These CPU measurements exclude Vulkan/presentation.

The final interleaved sequence A1-B1-A2-B2-A3-B3 (`cpu-ab-before-*`, `cpu-ab-after-*`, `cpu-ab-summary.json`) confirms the reduction on the same pinned .NET 10 SDK. Run-level median-frame ranges do not overlap for any of the four cases:

| Case | Before median ms (range) | After median ms (range) | Median time reduction | Bytes/frame before → after |
| --- | --- | --- | ---: | --- |
| 1 × 100 | 0.0136 (0.0122–0.0145) | 0.0045 (0.0044–0.0047) | 66.91% | 169589 → 662 |
| 1 × 2000 | 0.1121 (0.1080–0.1167) | 0.0511 (0.0499–0.0547) | 54.42% | 169574 → 656 |
| 8 × 100 | 0.0970 (0.0878–0.1120) | 0.0398 (0.0393–0.0411) | 58.97% | 1352930 → 3288 |
| 8 × 2000 | 1.0054 (0.9999–1.0886) | 0.5554 (0.5459–0.5850) | 44.76% | 1352811 → 3288 |

This confirms the measured CPU fixture improvement, not an equivalent whole-application FPS percentage. Tail distributions remain in every run's JSON. Compare the paired 2D/3D feature sample separately.

## GPU evidence and runtime comparison

`vector-gpu-net11/result.json` and `vector-tests.stdout.log`: 121 checks passed on .NET 11; `vector-tests.stderr.log` is empty with `VK_LAYER_KHRONOS_validation` enabled. Coverage includes opaque submission reversal, crossing planes, geometry gaps, texture alpha, partial-alpha blending, local clips, independent widget depths/cameras, inverse picking, no-upload camera updates, retained native text after 650 idle frames, 8x analytic vector zoom, dynamic frames, MSAA, resize and buffer ownership.

`native-final-net11.json`: real NativeAOT compilation and execution succeeded (`Generating native code` in its stdout). It is one smoke run, not a three-run performance conclusion. Dynamic stress 8 × 4000 reports about 796.5 FPS, 1.2271 / 1.4476 / 1.6791 ms median/p95/p99 and 517.2 FPS 1% low in that run.

Final Managed runtime repetitions use `jit-final-net10-*` and `jit-final-net11-*`, each three independent runs. `nativeAot=false` is confirmed in all six results. `final-summary.json` preserves median aggregation. Reversed phase order is used on run 2; this exposes substantial first-phase warmup sensitivity. Preserve raw phase CSV and actual order. Do not combine the 2D and projected 3D phases as a same-image optimization comparison.

| Dynamic 3D workload | .NET 10 FPS | median/p95/p99 ms | 1% low FPS | .NET 11 RC FPS | median/p95/p99 ms | 1% low FPS |
| --- | ---: | --- | ---: | ---: | --- | ---: |
| 1 × 1000 | 8085 | 0.1067 / 0.2092 / 0.311422 | 1812 | 9853 | 0.0878 / 0.1798 / 0.279063 | 1912 |
| 8 × 1000 | 2573 | 0.3739 / 0.4973 / 0.638624 | 950 | 4658 | 0.1972 / 0.29588 / 0.393918 | 1492 |
| 8 × 4000 | 838 | 1.16645 / 1.40936 / 1.650896 | 478 | 1920 | 0.5017 / 0.644225 / 0.7884 | 979 |

The 8 × 4000 case, when submitted first in reverse order, is much slower: .NET 10 run 2 is 545 FPS and .NET 11 run 2 is 967 FPS. Other runs are 838–840 and 1920–1923 respectively. This is a warmup/order caveat, not proof that all workloads gain the same runtime improvement. The measured scene-building allocation median is 376 bytes/frame for one layer and 3008 for eight, on both runtimes.

Final NativeAOT repetitions use `aot-final-net10-*` and `aot-final-net11-*`, three runs per runtime with reversed phase order on run 2. `aot-final-summary.json` preserves their median aggregation. Both publish logs contain native code generation; all six results report `nativeAot=true`.

| Dynamic 3D workload | .NET 10 AOT FPS | median/p95/p99 ms | 1% low FPS | .NET 11 RC AOT FPS | median/p95/p99 ms | 1% low FPS |
| --- | ---: | --- | ---: | ---: | --- | ---: |
| 1 × 1000 | 5947 | 0.1423 / 0.2853 / 0.398013 | 1410 | 8051 | 0.1106 / 0.1969 / 0.254925 | 2390 |
| 8 × 1000 | 1823 | 0.5111 / 0.7090 / 0.881502 | 905 | 2423 | 0.3967 / 0.50817 / 0.648668 | 917 |
| 8 × 4000 | 573 | 1.6972 / 2.00481 / 2.255195 | 388 | 789 | 1.2353 / 1.43272 / 1.65245 | 489 |

AOT scene-building allocation medians are 408 bytes/frame for one layer and 3264 for eight. In these measurements .NET 11 Managed is faster than either AOT configuration. This is a measured workload result, not a general claim about NativeAOT or final .NET 11 performance.

The status-panel change alters HUD coverage; earlier runtime measurements cannot be mixed into final same-source comparisons. The heavy case's dynamic geometry remains approximately 1.17 MB/frame; depth memory is 13,762,560 bytes for three 1280 × 800 attachments on this device. Draw counts and GPU time are in the per-run profile logs, and include HUD work.

## Invalid or superseded evidence

- `final-3d-1` was contaminated by concurrent GPU testing; excluded from performance claims.
- Early NativeAOT failures were toolchain discovery/quoting failures, not successful runs. Resolve the installed linker and SDK libraries before publishing; do not retry with an unverified linker.
- `managed-final-net10-*` used FBA's default AOT feature switches while still running under coreclr/clrjit. Its `nativeAot` flag was therefore misleading. Exclude this set from Managed/NativeAOT comparisons. The runner now explicitly passes `PublishAot=false` and `SelfContained=false` for Managed execution; `jit-final-*` must report `nativeAot=false`.
- The first vector no-upload assertion compared frame-local upload counters against the initial upload frame. Corrected the assertion to require zero bytes in the camera-only frame; the subsequent 121-check run passed.
- The runner previously accepted the CLR unhandled-exception exit code as benign. All nonzero Managed exits now fail. A stale successful-run exit code is reset after verified completion.

## Reproduction and remaining work

Use the pinned SDK on PATH, then:

```powershell
dotnet build Duxel.slnx -c Release
dotnet run --project tests/Duxel.Performance.Bench -c Release -- artifacts/cpu.json
dotnet run --project tests/Duxel.Rendering.Tests -c Release -p:DuxelValidationHooks=true -- artifacts/gpu
./run-fba.ps1 samples/fba/dynamic_layer_camera_fba.cs -Managed -NoCache
$env:DUXEL_3D_BENCH_OUT = 'artifacts/dynamic.json'
./run-fba.ps1 samples/fba/dynamic_3d_layers_bench.cs -Managed -NoCache
```

Use an absolute benchmark output path when running a relocated FBA. .NET 11 commands are in `tests/runtime-net11/README.md`. NativeAOT uses `-NativeAot -NoCache -Wait` with its selected framework.

`camera-final-before.png` and `camera-final-widget-zoom.png` show the actual .NET 11 demo before and after a wheel message in widget A. Its vector shapes enlarge while widget B and the world remain unchanged; the HUD stays at five geometry builds and projected text remains visible. The title and compact HUD are fully visible. This does not establish drag behavior or arbitrary raster-text magnification quality.

Final current-source .NET 10 validation also passed 121 GPU checks with an empty Vulkan validation error log. `dotnet build Duxel.slnx -c Release` completed with zero warnings and errors (`final-build.stdout.log`).

`runner-failure-result.json` confirms that a real unhandled Managed exception (exit -532462766) is rejected. The runner no longer treats that exit as success.

The later `camera-final-widget-pan.png` is not an isolated pan proof: the observed frame also has live geometry enabled and a changed world camera. The interaction source was not isolated, so do not infer independent pan correctness from that capture or overwrite the earlier resident/zoom proof with it.

Remaining: isolated interactive pan review and scoped temporary-artifact cleanup. Raster glyph magnification is an unresolved quality limitation when vector-quality text zoom is required. Explicit mesh tessellation, sorted translucent surfaces, contiguous depth-scope submission and untested vendors remain documented limits. No performance regression is hidden by reverting the requested 3D architecture.

## Follow-up: vector text, ERD editing and update measurements

The earlier raster-glyph limitation is now addressed for the default native-text path in 3D layers. `IPlatformVectorTextBackend` supplies triangle meshes; Windows extracts DirectWrite outlines and tessellates them through Direct2D with a default 0.001 font-pixel tolerance. Native COM callback ownership and failure transfer use explicit unmanaged vtables and `UnmanagedCallersOnly`. Glyph geometry is bounded by a 2048-entry cache; text meshes by 512 entries. Ordinary 2D native text retains its raster path. This is finite-tolerance tessellation, not analytic infinite-zoom glyph rendering; complex script shaping and color glyphs are not implemented by this path.

Official ABI and geometry contracts were checked against installed Windows SDK headers (`dwrite.h`, `d2d1.h`) and [GetGlyphRunOutline](https://learn.microsoft.com/windows/win32/api/dwrite/nf-dwrite-idwritefontface-getglyphrunoutline) / [Tessellate](https://learn.microsoft.com/windows/win32/api/d2d1/nf-d2d1-id2d1geometry-tessellate(constd2d1_matrix_3x2_f_float_id2d1tessellationsink)).

`entity-front-gpu` passed 124 GPU checks on .NET 10 with an empty Vulkan validation error log. New cases require multiple draw-list flushes for long vector text, constrain text to both widget and entity-local clips, and raise an overlapping retained entity through real depth changes with zero geometry rebuild/upload. The ERD sample now maintains matching visual Z ranks and reverse hit-test order, and pushes each entity's local clip around both its background and text.

`erd-overview.png` and `erd-vector-zoom.png` are actual ERD captures, including projected outline text around 365% zoom and an independent overview. Those captures show the earlier 12-table UI and are not large-document performance evidence. The sample now generates 120 tables/240 relationships by default, with configurable 12–5,000 tables and dense/explore views. Mode labels were clarified to `Update: REBUILD ALL` and `Update: CHANGED ONLY`; both use the same 3D path. Their difference measures CPU regeneration and geometry transfers, not depth-test acceleration.

The first 120-table Managed smoke set (`erd-120-smoke/results.json`) covered six scenario/mode combinations once. Camera/full observed 13.85 FPS, 71.49/83.75/97.87 ms median/p95/p99, about 159 MB upload/frame and 242 geometry builds/frame. Camera/changed-only observed 1877.44 FPS, 0.4701/0.8765/1.0403 ms, zero scene builds and about 4.9 KB whole-frame upload (HUD included). Changed-only idle rendered zero frames after warmup. These are initial single-run observations; later warmup and sample changes require new final repetitions.

The .NET 11 ERD NativeAOT publish completed real native code generation. Its live process had no coreclr module (`erd-native-process.json`) and completed a 120-table camera smoke run (`erd-native-validation.json`). This validates the outline extraction and native callbacks under AOT, not a repeated AOT-vs-Managed performance conclusion.

`scripts/measure-erd-layers.ps1` preserves independent child stdout/stderr, raw frame CSV, JSON results, SDK, sample hash and actual scenario order. It alternates modes across repeated runs and rejects missing results or changed sample input. A discovered SDK-selection defect was fixed in `run-fba.ps1`: `ProcessStartInfo.WorkingDirectory` now follows PowerShell's filesystem location. Previously `Push-Location` selected .NET 11 for the parent probe while the child inherited the repository OS working directory and searched for SDK 10.0.301. The failed `erd-net11` and diagnostic runs are not valid measurements.

Current remaining work includes repeated 120/1,200-table measurements, current-source .NET 11 GPU/full-solution checks, isolated interaction review, further geometry/recording efficiency and scoped artifact cleanup. `erd-1200-smoke` is a current execution series; inspect its terminal result rather than treating its start as success.

## Follow-up: changed-only FPS, visibility and relationship dependencies

Hypothesis: retaining geometry removes regeneration but still submits invisible tables/text and rebuilds every relationship whenever one table moves. The latter allocates about 1.07 MB of scene data and uploads about 356 KB each frame in the default animated document. Changed-only rendering is not a guarantee of a particular FPS.

Changes:

- `VulkanRendererBackend.LayerVisibility.cs` rejects known command bounds only when all four transformed corners are outside the same homogeneous clip plane. It uses the actual layer matrix, command translation and widget viewport, retains a two-pixel guard band, and preserves unknown bounds and callbacks. Residency and upload preparation remain independent of visibility.
- `erd_layer_lab.cs` retains relationships in ordered batches of 32. Dependencies include both endpoints. Moving the default selected table invalidates two batches (64 of 2,400 relationships). Layout changes and full-rebuild mode still regenerate all batches. Camera-only changes retain them all.
- Current defaults are 1,200 tables, camera tour and entity motion On, quintic acceleration/braking, maximum 120% zoom and wide zoom at 85% of fit. FPS and a translucent overview viewport are visible. Input is middle drag for pan, Ctrl+middle drag or wheel for zoom, right click for the context menu.

Validation: .NET 10.0.9 and .NET 11.0.0-rc.3.26522.148 each pass 142 real GPU checks (`final-visible-dirty-gpu`, `final-visible-dirty-gpu-net11`), including offscreen dynamic/resident geometry, 200 invisible frames followed by identical pixels and zero uploads, perspective geometry behind the camera and crossing the near plane. Both stderr logs are empty. Thirty prior image captures remain byte-identical (`frustum-pixel-equivalence.json`). The pinned SDK 10.0.301 Release solution build has zero warnings/errors (`final-visible-dirty-build.stdout.log`). The .NET 11 RC NativeAOT publish performed native code generation without warnings (`edge-batches-native.stdout.log`).

The intermediate visibility-only FPS comparison is **INVALID** for quantitative improvement: before runs 2–4 ranged 164.72–202.75 FPS; after runs 2–4 ranged 164.80–245.78 FPS. Their ranges overlap and one pair reverses direction. `frustum-ab/after-1` is separately excluded because the .NET 11 GPU correctness suite overlapped it; that raw result is preserved. The requested 3D architecture and visibility change were retained while addressing the remaining CPU bottleneck.

Final combined A/B: `visible-dirty-ab/inputs.json` pins both executable hashes, sample hash, scenario and order. SDK 11.0.100-rc.3.26522.148, NativeAOT, Release win-x64, MSAA 4, VSync off, 1,200 tables/2,400 relationships, dense view, motion-tour/resident. Each process runs 18 seconds, excludes the initial frame and warms up at least another second. Three independent executions per candidate in order B1/A1/A2/B2/B3/A3; no correctness suite or build overlapped these runs. Tour extrema remain approximately 6.347% and 120%, with speed 0.45. Results are medians across independent runs, not pooled-frame percentiles:

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Average FPS | 211.28 | 360.83 | +70.79% |
| Frame median | 4.469 ms | 2.541 ms | -43.14% |
| Frame p95 | 6.868 ms | 4.275 ms | INVALID percentage: run ranges overlap |
| Frame p99 | 9.861 ms | 6.485 ms | -34.24% |
| 1% low FPS | 93.40 | 127.64 | +36.65% |
| Scene allocation/frame | 1,067,984 B | 260,480 B | -75.61% |
| Whole-frame geometry upload | 355,894 B | 16,743 B | -95.30% |
| Draw calls/frame | 8,430 | 2,846 | -66.24% |
| GPU render time | 1,586 us | 534 us | -66.35% |

FPS ranges are disjoint (before 202.38–213.59; after 314.90–381.32); median frame-time ranges are also disjoint (before 4.255–4.519 ms; after 2.293–2.669 ms). Only the p95 percentage is withheld because its ranges overlap. Raw frame CSV, per-process stdout/stderr and all six terminal JSON results are preserved next to `summary.json`. This is a workload-specific result; it does not establish a universal FPS or vendor-wide speedup. Camera phases produce different visible geometry counts; the animated trajectory is numerically integrated per frame. Upload includes the HUD, and GPU timestamps lag by frame slots.

The latest six-second idle smoke (`visible-dirty-idle.json`) observed 92 frames after its nominal warmup boundary, so it does **not** prove zero additional idle frames. Distinguishing startup/font/UI animation settling from persistent invalidation remains open. No scene content was rebuilt after initialization. `visible-dirty-native-process.json` confirms zero coreclr modules in the current executable's live process.

Remaining risks: every resident list is still visited; visibility testing adds CPU work; large views still draw many visible tables and overview items. Batching regenerates up to 32 relationships for one dirty relationship and adds 75 layer entries at 1,200 tables. Actual Ctrl+middle interaction needs isolated GUI review. Prior deployment directories remain intact; recursive cleanup of the verification extraction directory was rejected by automatic approval review with only `blocked by policy`, so it was preserved without a fallback deletion route. The full long goal remains open. The current improved NativeAOT executable is `samples/fba/artifacts/erd_layer_lab/net11.0-windows7.0/erd_layer_lab.exe`; earlier release EXE/ZIP copies retain their earlier behavior.

## Engine-owned retained batching and alternating camera tour

The prototype overview batching was removed from the sample. `UiRetainedLayerBatcher` now lives in Duxel.Core and is applied automatically by Vulkan before geometry preparation/recording. It does not inspect ERD names or coordinates. Adjacent resident lists sharing the exact transform, viewport, depth scope and write state are packed once into cached vector buffers. Source order, command clips, materials and analytic primitive coverage are preserved. Callbacks and frequently changing sources stay separate. Cache limits are 512 source lists and approximately 4 MiB of geometry per group, 32 entries, and 180 unused frames; mutation exclusion is eight frames. Frame rentals return only their reference array, while cached geometry and original producer buffers have independent ownership.

A first GPU check found that the new batch key was outside Vulkan's recognized resident-key namespace: pixel output matched, but 480 primitive bytes uploaded on a camera-only frame. The key now uses the existing resident namespace. This was corrected rather than weakening the zero-upload assertion. The initial overlapping AA reference also drew the foreground before the rear surface; its reference order was corrected to back-to-front for fractional AA coverage. These failed diagnostic runs remain separate from passed results.

`engine-batch-final-gpu` and `engine-batch-final-gpu-net11` each passed 153 checks, with empty stderr. New checks compare automatic engine batching against independent vector layers pixel-for-pixel, verify zero geometry repacking/upload for both object and common-camera transforms, reject overlapping borrowed frames, and expire/rebuild cached data without invalidating original source ownership. The final pinned SDK Release solution build has zero warnings/errors (`engine-batch-final-build.stdout.log`).

`engine-batch-ab` compares the exact same sample source (SHA256 9265155257EA397BA9435E5DC1974293F9385D5AE82E5DD15C7479A60CF0B2DE), restored byte-for-byte from the earlier recorded hash for the baseline. Both candidates use .NET 11 RC NativeAOT and the previous tour: 1,200 tables, 2,400 links, motion-tour/resident, dense view, MSAA 4, VSync off, 18 seconds/process, initial frame plus at least one second warmup. Order B1/A1/A2/B2/B3/A3. No build or correctness suite overlapped these six processes. The baseline executable is preserved under `overview-baseline/artifacts/erd_layer_lab/net11.0-windows7.0`.

| Metric (median across 3 independent runs) | Before | Engine batching | Quantitative status |
| --- | ---: | ---: | --- |
| Average FPS | 413.79 | 487.25 | +17.75%; disjoint ranges 262.97–427.92 / 479.77–490.73 |
| Frame median | 2.164 ms | 1.770 ms | -18.20%; disjoint ranges |
| Frame p95 | 3.487 ms | 3.078 ms | INVALID percentage: overlapping ranges |
| Frame p99 | 4.145 ms | 4.311 ms | INVALID percentage: overlapping ranges; median regressed |
| 1% low FPS | 212.11 | 187.73 | INVALID percentage: overlapping ranges; median regressed |
| Scene allocation/frame | 260,480 B | 260,480 B | unchanged |
| Whole-frame geometry upload | 16,403 B | 16,051 B | -2.14%; includes HUD and animation phase weighting |
| Draw calls/frame | 2,847 | 1,569 | -44.88%; disjoint ranges |
| GPU time | 534 us | 488 us | -8.52%; disjoint ranges |

The approximately 1,000 FPS target has not been achieved. Different per-object matrices are not yet represented by one GPU instance draw. Individual transform changes can split a combined group, requiring an initial repack, while uniform camera changes reuse it. CPU list visitation, frame-local wrappers (260 KB/frame), recording and synchronization remain optimization targets. The connection graph's 32-edge dependency groups are still declared by the ERD sample; general dependency declaration is a further engine integration item, rather than something inferred from application data.

The latest sample alternates tracking the animated entity with exploring another pseudo-random entity. Selection is reproducible, keeps the default speed 0.45, C2 easing and maximum 120% zoom, and records a bounded 32-visit trace. `alternating-tour-native-validation/result.json` and actual screenshots at 8/21/36/49 seconds prove cycles `1 (tracking) -> 283 (exploration) -> 1 (tracking) -> 323 (exploration)` at default speed with 1,200 tables. These images were inspected; the title, compact status panel and overview remain visible. This 52-second functional run overlapped a solution build and includes capture/foreground work, so its FPS statistics are not a replacement for the isolated A/B measurement.

The runner now resolves one explicit `-DotnetPath`, probes its SDK before temporary inputs or linker setup, and uses that same executable for Managed and NativeAOT. A .NET 10-only CLI under the .NET 11 selection directory is rejected with `DUXEL-FBA-SDK-UNAVAILABLE` (`sdk-failfast-result.json`). The benchmark script passes its resolved CLI through. The runner no longer terminates a pre-existing sample process before publishing; its existing separate-output behavior protects a running executable. Earlier SDK-selection, string-escaping and overlapping-log-path failures are preserved as failed runs, not validation evidence. The corrected Managed run and real NativeAOT publish both passed (`alternating-tour-managed-validated*`, `alternating-tour-native-validated*`).

## Frame-owned camera-state pooling and renderer-stage diagnosis

Hypothesis: camera-only retained geometry still allocated a new `UiDrawList` wrapper per transformed layer. The animated 1,200-table workload allocated 260,480 scene bytes/frame even after relationship batching. Geometry itself was already reused.

The first candidate pooled wrappers using reference-identity lookup in a weak table. `wrapper-pool-ab` completed 6/6 runs (three independent baseline/current pairs). Scene allocation fell to 25,896 B/frame (-90.06%), but FPS medians were 489.97 -> 459.75 with overlapping run ranges, so the FPS percentage is INVALID. The 1% low regression was measurable: 212.29 -> 172.92 FPS (-18.55%, disjoint ranges). This intermediate regression is preserved rather than hidden or used to abandon the requested architecture.

The lookup was replaced by explicit frame ownership. `UiImmediateContext` collects scene-wrapper leases, `UiContext` transfers them to the returned `UiDrawData`, and frame disposal returns them directly to bounded per-template pools. A weak-table lookup is no longer added to every non-owning draw list's release path; ordinary 2D list release retains its original behavior. Geometry arrays and cached templates remain immutable, and outstanding frame wrappers do not change. Public `record` copies do not own a lease. Pools keep at most eight returned wrappers per template and synchronize cross-thread return. Abandoned leases from a failed render are returned before the next frame or during context disposal. `UiDrawData` now implements `IDisposable`, and the app session uses a scope so renderer/diagnostic exceptions also release frame resources.

`frame-owned-pool-ab` completed 6/6 independent executions. FPS medians 473.53 -> 489.95 remained INVALID for a percentage claim because ranges overlap. Allocation remained 260,480 -> 25,896 B/frame (-90.06%). Its 1% low improved 188.73 -> 214.96 FPS (+13.90%, disjoint ranges); other timing/upload/draw ranges overlap. These are separate comparison series and cannot be chained into one speedup against the first candidate.

Additional common-engine changes remove redundant Begin/End dictionary lookup by keeping the layer entry in the active frame, skip text-parent dependency traversal when no parent exists, and remove four redundant flush calls around the existing retained-reference flush. Removal of a cached layer during a skipped Begin/End is preserved through an explicit removed-entry flag and a GPU regression case. These changes do not depend on ERD identifiers.

Final timed candidate: `layer-hotpath-final-ab`, three baseline/current executions each, .NET SDK 11.0.100-rc.3.26522.148 / runtime 11.0.0-rc.3.26522.148, NativeAOT Release win-x64, same alternating-tour sample, 1,200 tables, 2,400 relationships, motion-tour/resident, dense view, MSAA 4, VSync off. Each process runs 18 seconds, excluding the first frame and warming up at least another second. Order baseline/current/current/baseline/baseline/current; no builds or correctness runs overlapped the comparisons. Both executable hashes and exact result order are in `results.json`; raw CSV and separate child logs are preserved.

| Median across independent runs | Baseline | Final timed candidate | Percentage status |
| --- | ---: | ---: | --- |
| Average FPS | 493.77 | 468.27 | INVALID: overlapping ranges; median regressed |
| Frame median | 1.80 ms | 1.79 ms | INVALID: overlapping ranges |
| Frame p95 | 3.00 ms | 3.47 ms | INVALID: overlapping ranges; median regressed |
| Frame p99 | 4.23 ms | 4.54 ms | INVALID: overlapping ranges; median regressed |
| 1% low FPS | 204.05 | 188.51 | INVALID: overlapping ranges; median regressed |
| Scene allocation/frame | 260,480 B | 25,896 B | -90.06%; disjoint ranges |
| Geometry upload/frame, HUD included | 16,046 B | 16,121 B | INVALID: overlapping ranges |
| Draw calls/frame | 1,694 | 1,631 | INVALID: overlapping ranges |
| GPU render time | 525 us | 508 us | INVALID: overlapping ranges |

The allocation result is valid; a throughput improvement is not established. The approximately 1,000 FPS target remains open. The later one-line depth-scope correction described below does not affect the ERD sample's global-first/widget-after order and was verified separately, rather than silently treating its executable as the exact timed candidate.

The measurement tool now accepts `-Scenarios`, `-Modes`, `-BaselineExecutable` and `-RequireExclusive`. It alternates candidate order, fingerprints both inputs, preserves partial progress, and refuses to terminate an already running comparison window. Completed comparisons produce `summary.json` with exact medians, ranges, valid repetition counts, and INVALID/unchanged/insufficient classifications. `-SummarizeOnly -OutputDirectory <completed-run>` derives the same report without restarting any process. Incomplete runs are rejected. Missing/unsupported metric values are not substituted with zero. This replaces repeated one-off comparison scripts.

Current correctness: `frame-scope-final-gpu` and `frame-scope-final-gpu-net11` each pass 162 checks with empty stderr. Tests cover independent outstanding camera snapshots, public record-copy release, cross-thread return and actual wrapper reuse, recovery of abandoned leases, retained-parent snapshots after child-wrapper reuse, cached-layer removal, and independent global depth when submitted after a widget. The last case identified residual widget depth in the first later global scope; scope transitions now clear depth even when the destination is the default global scope. No offscreen target or extra composition pass is introduced. Forty-five prior captures remain byte-identical (`frame-scope-pixel-equivalence.json`). The pinned SDK 10.0.301 Release solution build has zero warnings/errors (`frame-scope-final-build.stdout.log`). The final .NET 11 RC NativeAOT publish succeeded (`frame-scope-final-native.stdout.log`).

A separate 12-second camera-only diagnostic (`camera-stage-diagnostic`, one run, app profiling every 120 frames) gathered 44 post-warmup stage samples. Median UI rendering was 527.5 us, renderer call 1,098.2 us, and total measured app stages 1,635.3 us. The renderer call includes preparation, driver submission and waits; this is not proof that all 1.10 ms is CPU computation. Geometry builds/frame were zero, measured scene allocation 688 B/frame, and whole-frame geometry upload about 6.5 KB (HUD included). GPU render time averaged about 263 us. This diagnostic is not a new comparative FPS claim. Detailed renderer preparation/state/synchronization profiling is the next action; offscreen per-layer state updates still occur before command-level culling.

Reproduce an isolated current/baseline comparison with the existing tool:

```powershell
./scripts/measure-erd-layers.ps1 -Executable <current.exe> -BaselineExecutable <baseline.exe> -TableCounts @(1200) -Views @('dense') -Scenarios @('motion-tour') -Modes @('resident') -Runs 3 -Seconds 18 -RequireExclusive -OutputDirectory <new-directory>
```

The preserved pre-pooling executable is `artifacts/perf-2026-11-03/frame-wrapper-before.exe` (SHA256 8887129E7D51C6879256912071984B04D70127CA425B04A8A9115D1DAFF6F044). Current SDK 10/11 correctness is verified; the latest-source Managed/NativeAOT performance matrix across both SDKs, independent interaction/idle review, different-model instancing, and cleanup remain unfinished. The goal remains active.

## Whole-list visibility before GPU state

Hypothesis: invisible resident layers still incurred matrix pushes before command-level culling. Vulkan now unions valid command bounds, including translation, and classifies homogeneous clip bounds before touching viewport, depth or transform state. Fully interior lists skip redundant command tests. Empty commands do not poison the union; unknown bounds and callbacks remain conservative. Source geometry residency remains independent of visibility. `LayerTransformPushes` and `CulledLayerLists` expose the work. SDK 10/11 GPU tests passed 166 checks each at this stage.

`layer-visibility-ab/results.json` completed 12/12 executions: same .NET 11 RC NativeAOT, 1,200 tables/2,400 links, dense view, resident mode, MSAA 4, VSync off, 18-second processes, first-frame exclusion and at least one second warmup, three independent runs per candidate/scenario with ABBAAB ordering. No builds or GPU correctness runs overlapped measurement. Raw CSV and separate child logs are retained.

| Scenario / median across runs | Before | Whole-list visibility | Percentage status |
| --- | ---: | ---: | --- |
| Camera FPS | 478.80 | 532.50 | +11.21%, disjoint ranges |
| Camera median frame | 1.8146 ms | 1.5771 ms | -13.09%, disjoint ranges |
| Camera p95 / p99 | 3.26 / 3.90 ms | 3.05 / 3.47 ms | INVALID: overlapping ranges |
| Camera 1% low | 216.83 FPS | 249.74 FPS | INVALID: overlapping ranges |
| Motion-tour FPS | 477.08 | 537.40 | +12.65%, disjoint ranges |
| Motion-tour median | 1.8677 ms | 1.6165 ms | -13.45%, disjoint ranges |
| Motion-tour p95 / p99 | 3.24 / 3.85 ms | 3.03 / 3.49 ms | -6.69% / -9.43%, disjoint ranges |
| Motion-tour 1% low | 229.39 FPS | 252.88 FPS | INVALID: overlapping ranges |

Camera scene allocation remained 688 B/frame; motion-tour remained 25,896 B/frame. Draw/upload/GPU aggregates also depend on how many frames each continuous camera phase contributes, so they do not by themselves prove same-pose draw elimination. Separate `renderer-stage-before` / `renderer-stage-after` 12-second diagnostics measured record medians 503.45 -> 381.1 us, upload 175.15 -> 174.95 us, frame fence 2.9 -> 2.85 us and GPU medians 103.57 -> 101.26 us. These single diagnostic runs are not independent comparative throughput proof. Close views were about 0.1 ms GPU, while wide views reached approximately 2.3 ms and thousands of draws. Resident source geometry was 723,493,952 logical bytes; driver allocation overhead is additional.

`layer-visibility-sdk-native` also completed 6/6 runs using identical source/options: SDK 10.0.301 / NativeAOT runtime 10.0.9 versus SDK 11.0.100-rc.3.26522.148 / NativeAOT runtime 11.0.0-rc.3.26522.148. Frame median was 1.6861 -> 1.57435 ms (-6.63%, disjoint ranges). FPS 520.12 -> 533.94 and tail metrics have overlapping ranges, so their percentage claims are INVALID. This is a comparison of the visibility-stage source, not the later compiled renderer.

## Real GPU batching across different matrices

Hypothesis: retaining geometry removed regeneration but still left thousands of separate commands for different matrices and alternating analytic rectangles/circles. A Vulkan compiler now generates an ordered index stream whose high byte selects a GPU command descriptor and whose low sixteen bits retain original indexed vertex reuse. Different matrices, local clips, opacity, translations and analytic primitive kinds share actual `vkCmdDrawIndexed` calls. This is not multi-draw API-call relabeling. The index range stays within Vulkan's guaranteed 24-bit bound and adds no shader-int64, draw-parameters or indirect-draw feature requirement.

Groups are bounded to 256 descriptors / 1,048,576 indices, and retained index caches to 256 entries / 512MiB. Original and compiled index buffers currently coexist: the ERD adds approximately 270MB of compiled indices. Source glyph geometry remains duplicated across text placements despite CPU outline reuse. This is a documented intermediate memory cost, not a memory-efficiency completion claim. Vertex-only revisions reuse topology; frame-slot-owned descriptors use CPU shadow copies and fence-protected coherent writes. Command data and matrices use separate 64-byte records, avoiding a matrix copy for every command. Replaced buffers retire behind in-flight fences; resize/MSAA changes/shutdown release the resources.

The first candidate only accepted whole lists with at most 256 commands. `compiled-layers-ab` completed 12/12 independent runs with the same 18-second ABBAAB method and one executable SHA256, toggling `DUXEL_VK_COMPILED_LAYERS=0/1`. Camera draws fell 1,912.52 -> 1,440.41 (-24.69%), motion-tour 1,577.92 -> 1,162.05 (-26.36%), both disjoint ranges. GPU time regressed 609.25 -> 691.48 us (+13.50%) and 494.32 -> 569.81 us (+15.27%). FPS/tail percentages were INVALID due to overlapping ranges. This failed throughput candidate is retained in the evidence rather than hidden or used to abandon the architecture.

`compiled-direct-domain.commands.log` identified the remaining direct commands as long relationship lists alternating rectangle and circle primitives. The compiler now partitions inside a source list, shares its visibility result across portions, and coalesces visible index ranges. The subsequent `compiled-layers-final-ab` also completed 12/12 runs with identical hardware/options and per-run raw CSV:

| Median across three independent runs | Direct camera | Compiled camera | Direct motion-tour | Compiled motion-tour |
| --- | ---: | ---: | ---: | ---: |
| FPS | 520.16 | 586.34 | 546.22 | 532.53 |
| Frame median, ms | 1.62 | 1.49 | 1.57 | 1.69 |
| p95, ms | 2.99 | 2.60 | 3.05 | 2.64 |
| p99, ms | 3.77 | 3.46 | 3.39 | 4.41 |
| 1% low FPS | 221.28 | 194.51 | 259.52 | 173.51 |
| Actual draws/frame | 1,916.11 | 101.15 | 1,542.54 | 95.93 |
| GPU time, us | 605.66 | 716.78 | 488.25 | 631.60 |
| Compiled index uploads/frame | 0 B | 0 B | 0 B | 0 B |
| Compiled state uploads/frame | 0 B | 24,209.56 B | 0 B | 25,219.71 B |

Draw reduction is -94.72% / -93.78% with disjoint ranges. GPU regressions are +18.35% / +29.36%, also disjoint. Every FPS and tail percentage is INVALID because run ranges overlap; the motion-tour FPS median regressed. Reducing calls is therefore proven, while higher overall throughput is not. The requested structure remains enabled and the added GPU vertex-pulling/descriptor work remains an optimization target. These results predate the following selection changes and command-topology plan reuse.

Current common-engine correctness: `compiled-layers-current-gpu/result.json` and `compiled-layers-current-gpu-net11/result.json` pass 175 checks each. Added cases prove exact mixed-primitive/clip/opacity pixels across different matrices, one true draw for eight such planes, zero stationary descriptor/index uploads after slot warmup, camera/object motion without geometry/index upload, topology reuse on vertex-only content changes, a 257-plane group boundary, and internal partitioning of one 400-command mixed list into two GPU draws. `compiled-layers-pixels.json` compares 53 prior captures byte-for-byte. Khronos validation was loaded explicitly (`compiled-layers-final-gpu-net11.stderr.log` records the loaded DLL); no VUID/errors were reported. Shader temporary compilation, spirv-val/disassembly and source/artifact consistency checks passed. Cross-vendor runtime/ISA measurements remain unverified. The Release solution build passes with zero warnings/errors (`compiled-layers-solution.stdout.log`).

The measurement tool adds `-CompareCompiledLayers`, which toggles the two submission modes in one fingerprinted executable, records the setting on every result, alternates order and restores the caller's environment. Missing older metrics remain unknown rather than being treated as zero.

## Selected connectors and header corners

The ERD now retains selected-table connectors in their own 3D foreground layer. Their 5px stroke and larger endpoints use the selected table's accent. Their physical depth is between the selected table and every other table, with read-only depth testing after table rendering so its text remains protected and analytic edge coverage blends over the existing scene. Ordinary batches exclude these connectors; selection changes invalidate only old/new dependent batches. Endpoint adjacency is built once. Moving the selected entity now rebuilds only its four incident connectors, while background batches remain unchanged. This replaces the previous two-batch/64-relationship update for the default selected table. Moving another entity still updates its dependent background batches and any affected selected connector.

Header fills retain analytic rounded upper corners. Extending the rounded fill below the header and clipping to the header rectangle makes both lower corners square with one primitive, without introducing a polygon approximation or an extra composition image.

The .NET 11 RC NativeAOT publish is `selected-relations-native.stdout.log`. Actual 12-table interactive evidence is `selection-ui-initial.png`, `selection-ui-selected.png`, and `selection-ui.json`: UI buttons stopped camera/motion, fit the diagram, and an actual window-message click selected inventory/entity 3. Four bold foreground connectors pass visibly over other cards, while the selected title/fields remain clear and header lower corners are square. Runtime JSON confirms entity 3, four relationships, width 5, table depth 0.2 and connector depth 0.2066666667; the child exited normally with empty stderr. A later attempt to capture a second selection was rejected because the owned auto-exit process had already closed; no second-selection image is claimed.

The current compiler grouping plan uses retained index and command topology instead of command-object identity. Vertex/primitive value changes therefore update GPU addresses/data without rebuilding the whole plan. Inactive caches also age out during 2D-only frames. The latest-source repeated comparison is recorded separately in `selected-relations-ab`; do not substitute earlier-stage numbers for it.

`selected-relations-ab/results.json` is now complete: 12/12 terminal runs, same .NET 11 RC NativeAOT executable SHA256 `BC22E2FFEABAF83FB3F2065239AE5A9171F422C3C17C40017ADBED2AF26D7650`, 1,200 tables/2,400 links, resident/dense, default tour and the new selection behavior, MSAA 4, VSync off. Three independent runs per mode/scenario, 18 seconds each, same warmup and ABBAAB method, no overlapping builds or correctness runs. These comparisons toggle only GPU command compilation; both sides already include the selected-connector optimization and title changes.

| Median across independent runs | Direct camera | Compiled camera | Direct motion-tour | Compiled motion-tour |
| --- | ---: | ---: | ---: | ---: |
| FPS | 495.21 | 621.51 | 538.67 | 606.03 |
| Frame median, ms | 1.56 | 1.40 | 1.59 | 1.51 |
| p95, ms | 3.52 | 2.50 | 3.07 | 2.41 |
| p99, ms | 5.28 | 2.83 | 3.45 | 2.75 |
| 1% low FPS | 152.29 | 216.29 | 247.04 | 321.50 |
| Actual draws/frame, HUD included | 2,063.60 | 103.70 | 1,743.49 | 93.72 |
| GPU time, us | 573.99 | 700.86 | 469.84 | 604.41 |
| Scene allocation/frame | 688 B | 688 B | 3,448 B | 3,448 B |
| Scene builds/frame | 0 | 0 | 1 | 1 |
| Compiled index uploads/frame | 0 B | 0 B | 0 B | 0 B |
| Compiled state uploads/frame | 0 B | 23,455.08 B | 0 B | 21,562.72 B |

Valid disjoint-range improvements: camera FPS +25.51%, motion-tour FPS +12.50%; draw calls -94.97% / -94.62%; p95 -28.95% / -21.38%; motion-tour p99 -20.33%. Camera FPS ranges are 476.12–530.16 versus 571.29–625.45; motion-tour ranges are 523.54–570.86 versus 582.47–642.03. Frame medians, camera p99 and both 1% low percentages remain INVALID because their run ranges overlap. Scene allocation is unchanged between submission modes. The 1,000 FPS goal is not achieved across this camera workload.

GPU cost still regresses +22.10% / +28.64%, with disjoint ranges. Higher app FPS and lower draw count do not establish lower GPU work. Next optimization should separate shared camera/basis data from per-layer model data and reduce repeated vertex descriptor/matrix reads, while retaining the general 3D transform contract. The additional compiled index memory also remains unresolved. Whole-frame upload ratios are NONCOMPARABLE between these submission modes: compiled descriptor/matrix buffer writes are counted, while direct push-constant bytes were never included in the upload metric. The summary tool explicitly suppresses that ratio and can regenerate existing completed summaries with `-SummarizeOnly` without launching benchmarks.

An additional owned 12-second interactive run (`selection-change-ui.json`) confirms inventory/entity 3 -> products/entity 2 via actual window messages, four relationships, 5px width, table/line depths 0.2/0.20625 and normal exit. Its screenshots were occluded by unrelated desktop windows and are not visual proof. Automatic approval review rejected deletion of those two generated occluded captures with `blocked by policy`; they remain preserved and no alternate deletion route was attempted. `selection-change-ui-evidence.json` distinguishes the successful runtime readback from unverified pixels. The earlier unobstructed `selection-ui-selected.png` remains the visual evidence for bold foreground lines and square lower header corners.

Reproduce the same-executable submission comparison:

```powershell
./scripts/measure-erd-layers.ps1 -Executable samples/fba/artifacts/erd_layer_lab/net11.0-windows7.0/erd_layer_lab.exe -CompareCompiledLayers -TableCounts @(1200) -Views @('dense') -Scenarios @('camera', 'motion-tour') -Modes @('resident') -Runs 3 -Seconds 18 -RequireExclusive -OutputDirectory <new-directory>
```

The current .NET 11 executable is updated in place at that path. Existing release ZIPs and the .NET 10 executable have not been rebuilt with these latest changes. No commit or publication was performed. At this stage SDK 10/11 GPU tests passed 175 checks each; the following sections record subsequent changes rather than replacing these earlier measurements.

## Shared matrix basis, stable topology and drag startup

Hypothesis: repeated full-matrix fetches and uploads remain after draw compilation. When visible members have identical first three matrix rows, specialized pipelines push those rows once and fetch only the member-specific last row. Matrix reconstruction precedes the original multiply to preserve arithmetic order. Different rotations retain the full-matrix pipeline. Per-slot representation flags force correct writes when switching modes. `SharedBasisLayerDraws` and `DUXEL_VK_SHARED_LAYER_BASIS=0` expose the optimization and its comparison mode. Shader temporary compilation, spirv-val and disassembly are preserved under `shared-basis-shaders`; this does not remove the additional compiled index memory.

The zoom/select/drag report exposed a separate cache instability. In `drag-start-baseline2`, changing the selected relationship topology shifted subsequent group boundaries and rebuilt/uploaded approximately 270MB of unchanged indices twice. Instrumented renderer record times reached 171.6/182.4ms; the first-two-second total maximum was 185.9ms. The compiler now preserves previous source anchors, keeps small lists whole and separates primitive-only from mesh groups. A regression fixture changes a preceding relationship list while retaining 257 mesh planes and checks both exact pixels and at most 64 bytes of new indices.

Per-frame retired buffers/textures are released after their owning slot fence rather than waiting for all in-flight frame fences. The retirement deadline spans the slot count; the slot fence and ordered queue cover last use. Shutdown/all-resource flushing still waits for all frames. No asynchronous destruction worker was introduced.

`scripts/measure-erd-drag-start.ps1` reuses an owned-process scenario, queues window input, records sample-consumed drag frame IDs, separates first-two-second/later stage timings and validates the intended entity and held movement. It neither changes global pointer position nor forces foreground focus. `drag-start-queued-after-2` and `-3` validate entity 635, approximately six seconds held and over 120 motion frames. Their first-two-second compiled index maximum is 319,992 bytes and total 608,328 bytes, replacing the large rebuild. Residual instrumented total maxima of approximately 40.6/37.9ms remain; these traces are not isolated FPS evidence and do not establish complete elimination of every stall. The user subsequently manually verified the launched latest build and reported very smooth dragging.

Invalid attempts remain preserved: the first log reader denied write sharing and caused a render-thread exception; focus-gated attempts could not proceed; one long scenario included a later different gesture; `drag-retire-stage-profile` selected the wrong entity/zoom. None is successful timing or input evidence. The log reader now shares writes, render-thread errors are checked separately from exit code, source counters validate actual input and the owned window closes promptly after the gesture. An overlapping build file-lock failure is also preserved.

## Reuse pushed camera basis across compiled groups

Hypothesis: CPU per-member culling overwrote the state used to track the pushed GPU matrix, requiring an invalidation at every compiled group end. Track `PushedLayerTransform` independently from the CPU `LayerTransform`. Direct drawing restores its actual transform while adjacent compiled groups retain a common basis push. This is a common Vulkan engine change, not sample-only logic.

The 257-plane/two-group fixture changes from two basis pushes by construction to one measured push. A following direct layer is compared against the direct reference pixel-for-pixel. This is a state-command reduction, not a measured FPS improvement. Camera motion still requires visible-vector projection/rasterization; the complete-MVP API still transmits per-layer last rows when those change. Explicit camera/model separation and shared GPU glyph geometry remain optimization opportunities.

Verification: `camera-push-gpu/result.json` (.NET 10.0.9) and `camera-push-gpu-net11/result.json` (.NET 11.0.0-rc.3.26522.148) each pass 190 checks with `VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation`. The Release solution build has zero warnings/errors (`camera-push-solution.stdout.log`). The initial SDK10 attempt failed CS2012 because its compiler server held a reference assembly; after a normal `dotnet build-server shutdown`, the nonshared compilation retry passed. Both attempt logs are retained. The .NET11 RC NativeAOT publish completed (`camera-push-native.*.log`), updating the default NET11 executable; NET10 and release ZIPs remain older.

Reproduce the current shared-basis comparison (same executable, both variants compiled, ABBAAB, three independent 18-second runs per variant/scenario):

```powershell
./scripts/measure-erd-layers.ps1 -Executable samples/fba/artifacts/erd_layer_lab/net11.0-windows7.0/erd_layer_lab.exe -CompareSharedLayerBasis -TableCounts @(1200) -Views @('dense') -Scenarios @('camera', 'motion-tour') -Modes @('resident') -Runs 3 -Seconds 18 -RequireExclusive -OutputDirectory <new-directory>
```

`shared-basis-current-ab` completed 12/12 terminal runs, executable SHA256 `F669F5352048859D36E6C05C26AF1B42CF578E5FE86D7D288033B4CA2B035ACE`, with separate raw frame CSV and child logs. No builds or GPU correctness tests overlapped these runs. All camera cases have zero scene builds/frame and all motion-tour cases have one; compiled index uploads remain zero in every measured case.

| Median across three independent runs | Full-matrix camera | Shared-basis camera | Full-matrix motion-tour | Shared-basis motion-tour |
| --- | ---: | ---: | ---: | ---: |
| FPS | 489.71 | 554.47 | 509.35 | 587.16 |
| Frame median, ms | 1.84165 | 1.53170 | 1.77745 | 1.50525 |
| p95, ms | 3.23298 | 2.82565 | 3.05178 | 2.63712 |
| p99, ms | 5.02866 | 4.11254 | 4.01696 | 3.60423 |
| 1% low FPS | 152.68 | 183.91 | 185.77 | 226.34 |
| GPU time, us | 825.49 | 726.44 | 618.66 | 620.40 |
| Actual draws/frame, HUD included | 106.30 | 104.72 | 93.33 | 94.07 |
| Layer-state writes/frame, bytes | 24,831.65 | 5,903.72 | 20,500.66 | 5,511.63 |
| Accounted uploads/frame, bytes | 31,727.89 | 12,559.04 | 27,757.24 | 12,679.99 |

Layer-state writes decrease 76.23% / 73.11% and accounted buffer uploads decrease 60.42% / 54.32%, all disjoint ranges. Both sides use compiled submission, so this upload comparison does not mix unaccounted direct push constants with compiled writes. FPS, all tail metrics, GPU times and draw-count percentages are INVALID due to overlapping run ranges. The motion-tour GPU median slightly regresses. Do not describe this as a proven FPS or GPU-time improvement, or compare its medians against an earlier executable as a controlled throughput result. The 1,000 FPS target remains unmet; the requested architecture and quality remain enabled while further optimization continues.

Remaining structural costs include immediate traversal of every table/overview layer, per-layer completed-MVP validation/composition and descriptor visibility checks. A camera-only update does not need shape regeneration but still traverses layer metadata and updates changing matrix last rows. Explicit shared camera/model state and GPU glyph geometry reuse are next investigation targets. These measurements establish reduced state-buffer traffic, not maximum hardware utilization or completed memory/idle/runtime verification.

## Explicit shared camera and independent plane models

Hypothesis: sharing only the matrix basis still updates every visible plane's last row when the common camera pans. The new optional `UiLayer3D.PlaneModel` separates source-pixel translation before projection and normalized clip-depth bias after projection from the shared camera in `Transform`. This compact plane specialization does not replace the complete-MVP contract for arbitrary rotations, world-Z translations, intersections and general 3D transforms. Physical world-Z and post-projection depth bias are explicitly different coordinate contracts.

Vulkan normalizes consecutive identical cameras once per viewport and uses a specialized vertex pipeline with a common camera push matrix. Each plane has a 16-byte payload in the existing 64-byte compatible matrix slot. Shader position uses the shared camera and source-pixel displacement; clip depth adds `offset * w`. Local clips remain in source coordinates. CPU frustum culling and pointer inverse projection include the same model. Mode flags distinguish full matrices, shared basis and shared cameras, forcing appropriate writes when a frame slot changes representation. The default ERD uses this engine API for board, overview, grid, table stacking and selected connectors. No layer image or extra composition pass is introduced.

`SharedCameraLayerDraws`, `DUXEL_VK_SHARED_LAYER_CAMERA=0` and `-CompareSharedLayerCamera` expose the real draw path and same-executable comparison. The earlier basis comparison now explicitly disables shared cameras so its intended comparison is preserved. Caller environment is restored and result records contain both flags.

Temporary shader compilation, spirv-val and disassembly are under `shared-camera-shaders/candidate`; the previous deployed vertex binary is protected under `shared-camera-shaders/before`. GPU correctness and NativeAOT measurements use `DuxelShaderDirectory` to consume candidate shaders before source-binary replacement. No subgroup width, shader-int64 feature or vendor-specific synchronization assumption was added. NVIDIA runtime checks use the existing RTX5070 Ti Laptop; AMD ISA/register/occupancy and runtime evidence remain unavailable, not inferred from SPIR-V validation.

`shared-camera-expanded-gpu/result.json` and `shared-camera-gpu-net11/result.json` each pass 209 GPU checks on runtimes 10.0.9 and 11.0.0-rc.3.26522.148. New cases check zero per-plane state/index/geometry writes for camera-only changes, one 16-byte model write for one moving plane, exact pixels against complete-MVP references, representation changes, independent widget normalization/clipping/picking, homogeneous depth rejection and perspective projection. A subsequent fixture revision renders transition candidates consecutively rather than interleaving reference captures, ensuring all frame slots are exercised; its post-measurement verification is recorded below when completed. Slot compatibility still reserves 64 bytes, so 16-byte writes do not establish a fourfold buffer allocation reduction. Layer submission and visibility CPU traversal remain.

Build diagnosis corrected an earlier attribution: shutting down a compiler server made prior file-lock attempts pass, but did not prove the underlying cause fixed. A later SDK11 CS2012 recurred without a live stale compiler. `shared-camera-build-graph.diag.log` shows two CoreCompile instances writing the same Core output: an implicit test-to-Core reference lacked the validation global property present on the Vulkan path. All three test project references now explicitly carry the same property, including an explicit Core reference. A forced rebuild (`shared-camera-build-graph-fixed.log`) shows one CoreCompile, zero warnings/errors, and SDK11 GPU execution passes. Initial failures are retained, not converted to successes.

`shared-camera-solution.stdout.log` is a clean Release build using candidate shaders. `shared-camera-native.*.log` publishes NET11 RC NativeAOT with candidate shaders. The repeated comparison is `shared-camera-ab`, same executable SHA256 `58494E80646C3A68D7DD7C946A9914F50CF7E5E0CF2C247B31DA4700C12BDA58`, 1,200 tables, MSAA4, VSync off, resident/dense, camera and motion-tour, ABBAAB, three independent 18-second runs per variant/scenario. Both sides use the new public coordinate contract; the disabled side materializes complete matrices in the backend. This comparison isolates backend sharing rather than all API migration costs. Earlier scene allocations of 688/3,448 bytes versus this executable's 720/3,544 bytes are different-source observations, not a controlled allocation improvement; the larger nullable scene value also changes wrapper sizes.

The comparison completed 12/12 terminal runs with no stderr errors or orphan demo process. No builds or GPU correctness checks overlapped it.

| Median across three independent runs | Materialized camera | Shared camera | Materialized motion-tour | Shared motion-tour |
| --- | ---: | ---: | ---: | ---: |
| FPS | 497.01 | 507.57 | 559.12 | 486.61 |
| Frame median, ms | 1.73820 | 1.87090 | 1.60860 | 1.77420 |
| p95, ms | 3.04077 | 2.73056 | 2.60934 | 3.47288 |
| p99, ms | 5.43537 | 4.22159 | 3.62288 | 5.31026 |
| 1% low FPS | 145.78 | 186.49 | 208.84 | 152.43 |
| GPU time, us | 781.42 | 694.04 | 617.03 | 502.29 |
| Actual draws/frame, HUD included | 105.60 | 108.38 | 95.17 | 93.90 |
| Layer-state writes/frame, bytes | 6,264.14 | 0 | 5,749.03 | 511.41 |
| Accounted uploads/frame, bytes | 13,046.02 | 6,825.34 | 12,989.95 | 8,061.80 |
| Scene allocations/frame, bytes | 720 | 720 | 3,544 | 3,544 |
| Scene builds/frame | 0 | 0 | 1 | 1 |
| Compiled index writes/frame, bytes | 0 | 0 | 0 | 0 |

Disjoint-range results: GPU time -11.18% / -18.59%, layer-state writes -100% / -91.10%, accounted buffer uploads -47.68% / -37.94%; camera p99 -22.33% and camera 1% low FPS +27.93%. Both sides use compiled submission. FPS, frame median, p95, motion-tour p99/1% low and draw-count percentages are INVALID due to overlapping run ranges. Motion-tour FPS and tail medians regress; these are retained rather than hidden or used to remove the requested structure. The result proves lower GPU time/state traffic in this comparison, not 1,000 FPS, lower CPU time or maximum hardware utilization. Shared camera draws replace basis draws; a falling basis counter is a mode change, not a draw reduction.

A bounded four-second follow-up (`shared-camera-cpu-profile`) logs each CPU stage to locate remaining work; it is a single instrumented diagnostic, not isolated throughput or A/B evidence. App/Vulkan record counts both equal 1,060 with consecutive app frame IDs. After the recorded warmup frame 450, 610 paired records remain. `stage-summary.json` records medians: UI render 624.95us, renderer 1,067.25us, combined app work 1,697.75us; renderer target preparation 257.30us, geometry/cache preparation 193.95us, command recording 344.70us, submit 27.65us, present 89.80us. GPU time median is 128.784us in this short camera segment. Frame-fence wait median/max is 3.4/12.9us and begin median is 5.7us, so GPU fence waiting is not the dominant observed cost in this segment. These stage medians are independent summaries and must not be added as if they describe one particular frame. Per-frame file logging also adds overhead.

Source inspection maps target preparation to depth-scope validation and `UiRetainedLayerBatcher.Prepare`, and upload preparation to resident geometry lookup/validation despite zero scene/index uploads. The batcher calls `CanBatch` on every first list even when the immediate successor has a different scene and cannot join. UI submission still visits board and overview layers. Next CPU work should remove these unnecessary scans/metadata checks while preserving changing content, visibility and cache lifetimes; no new CPU performance percentage is claimed yet.

After candidate correctness and the completed A/B measurement, the validated vertex SPIR-V was copied to the normal runtime shader path. Both runtime entries match temporary recompilation in `shared-camera-spirv-consistency.log`. `shared-camera-final-solution.stdout.log` builds the normal shader path with zero warnings/errors. Final `shared-camera-complete-gpu/result.json` and `shared-camera-complete-gpu-net11/result.json` pass 211 checks each, including consecutive per-slot transitions, changing only the left widget camera while every right-widget pixel remains unchanged, and moving a previously hidden retained plane into view without geometry upload. Khronos validation was explicitly enabled and stderr is empty. NativeAOT embeds the identical candidate binary, so no further executable rebuild was needed for the source-binary copy. NET10 NativeAOT and release ZIPs remain older; whole-goal runtime/idle/memory/lifetime checks remain incomplete.

## CPU batch eligibility and exclusive replay reuse

Hypothesis: CPU target preparation scans command callbacks and content history even when different neighboring scene states make batching impossible. Check the next scene first and calculate geometry bytes/history only for eligible pairs. Combined replay wrappers now update their camera only within the batcher's existing exclusive borrowed-frame lifetime, reusing the wrapper rather than cloning it every frame. Source snapshots, public record-copy camera values and owned geometry remain independent. No retained geometry is rebuilt for a camera change.

The initial six focused runs (`batcher-cpu-before` / `batcher-cpu-after`) were all-baseline then all-current and did not archive raw samples. They are preliminary diagnostics, not the final quantitative comparison. The existing `Duxel.Performance.Bench --retained` path was extended to archive raw CSV via `BenchFrameRecorder.FrameSeconds`. Its iteration rate measures only the batcher, never application rendering FPS.

`batcher-controlled-ab` completes six terminal independent Managed runs in ABBAAB order, SDK10.0.301/runtime10.0.9, with .25-second warmup and one-second measurement per workload. Two isolated builds use the identical benchmark and Core sources except the two batcher changes, reconstructed in `batcher-reference-source/UiRetainedLayerBatcher.cs`. Binary hashes, source hashes and run order are recorded before execution. Thirty raw CSV files were checked for sample counts and recomputed average/median statistics; Core inputs stayed unchanged. Source inputs are preserved alongside results. An initial reference-project path was one directory too high and failed before timing; the corrected path probe/build logs are retained.

| CPU batcher workload | Before median, us | After median, us | Valid median change | Before allocation, B/iteration | After allocation, B/iteration |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,200 independent scenes, 4 commands | 59.4 | 11.1 | -81.31% | approximately 0 | approximately 0 |
| 1,200 independent scenes, 32 commands | 117.7 | 10.5 | -91.08% | approximately 0 | approximately 0 |
| 5,000 independent scenes, 4 commands | 413.7 | 63.8 | -84.58% | approximately 0 | approximately 0 |
| 1,200 scenes in runs of 8 | 187.1 | 186.4 | INVALID: overlapping ranges | 6,528 | 128 |
| 1,200 scenes sharing one transform | 53.1 | 54.3 | INVALID: overlapping ranges | 728 | 128 |

`summary.json` preserves p95/p99/1% low and run ranges for all five workloads. Independent-scene p95/p99 reductions have disjoint ranges. Grouped cases do not prove a median-time improvement; groups of eight regress in p99/1% low medians with overlapping ranges. Allocation reductions are -98.04% / -82.42% in the grouped cases. Sub-byte amortized benchmark overhead is not reported as an engine allocation improvement. A 128-byte `UiDrawData` view header remains when batching occurs: an initial test incorrectly expected zero total allocation, exposed this class allocation, and was corrected to test the intended source/public-copy snapshot lifetime rather than claim zero allocation. `batcher-verified-gpu/result.json` and `batcher-current-gpu-net11/result.json` pass 213 GPU checks each, including the added lifetime cases. No test result is claimed for the initial failed allocation assumption.

Automatic approval review rejected recursive deletion of the two verified isolated CPU build directories with `blocked by policy`. The whole deletion command was rejected before execution. `cleanup-result.json` records zero reclaimed bytes and no alternate deletion attempt. Those two temporary builds remain preserved; their cleanup is incomplete. Result JSON/CSV/logs, input hashes and source snapshots remain protected. This is not a completed general artifact-lifetime audit.

## Explicit 1,000 FPS target reporting

The user's performance target is **1,000 application FPS**. The ERD status panel shows `FPS: current / 1000` without adding a line or arbitrary height. Run JSON records `fpsTarget: 1000`. Measurement `summary.json` adds per-scenario `fpsTargets`, and `fps-target.md` plus console output show current median, target attainment, independent repetitions and target status. Instantaneous peaks and CPU-only iteration rates never establish achievement. Fewer than three runs are preliminary; `MET_ACROSS_RUNS` requires all of at least three independent run averages to meet the target. This FPS condition is separate from whole-goal functional/quality completion.

Existing completed `shared-camera-ab` summaries were regenerated without launching the sample: camera 507.57/1,000 FPS (50.76%), motion-tour 486.61/1,000 (48.66%), three current runs each, both NOT_MET. These remain version-specific historical snapshots. `batcher-fps-native.*.log` builds the new status display and CPU changes into NET11 RC NativeAOT. Latest application results are recorded below after `batcher-fps-current` finishes all six runs; do not substitute CPU-only improvements for an application FPS claim.

`batcher-fps-current` completed 6/6 terminal runs, three independent 18-second runs per scenario, latest NativeAOT SHA256 `3E93FA6F28860A3E4FDEAB6477D4C8AA2A172117F0472D65014641D1DE3AB8A1`. All six run JSON files record `fpsTarget: 1000`; no demo process remains. This is a current-source attainment measurement, not a paired comparison with the older executable. No CPU benchmark, build or GPU test overlapped these timed runs.

| Latest independent-run median | Camera | Camera + entity motion |
| --- | ---: | ---: |
| Application FPS | 588.23 | 598.33 |
| Target FPS | 1000 | 1000 |
| Target attainment | 58.82% | 59.83% |
| Individual run-average FPS range | 566.70–604.94 | 548.66–601.78 |
| Frame median, ms | 1.46850 | 1.49135 |
| p95, ms | 2.59231 | 2.52120 |
| p99, ms | 3.54249 | 3.45248 |
| 1% low FPS | 218.22 | 215.23 |
| GPU time, us | 703.05 | 604.59 |
| Actual draws/frame | 104.24 | 95.61 |
| Layer-state writes/frame, bytes | 0 | 502.66 |
| Accounted uploads/frame, bytes | 6,547.11 | 7,661.01 |
| Scene allocation/frame, bytes | 720 | 3,544 |

The highest run-average FPS in this set is 604.94. Neither repeated scenario reaches 1,000 FPS. CPU-only iteration rates are excluded from this goal. Application scene-allocation counters cover UI generation and do not establish render-thread batcher allocation changes; the isolated CPU allocation evidence remains scoped separately. Previous versus current application FPS percentages are not claimed because these are different-source snapshots without a paired comparison.

`fps-target-contract/result.json` passes four synthetic report checks through the real summary command: a median above target with one run below remains NOT_MET; two high runs remain preliminary; three runs all above pass the FPS condition; missing FPS values remain null rather than fabricated zero. Synthetic fixtures are not measurements. The final Release solution build (`batcher-final-solution.stdout.log`) has zero warnings/errors. Paired API/sample docs explain raw recorder views, borrowed replay lifetimes and target reporting. The two isolated CPU binary directories remain undeleted after automatic approval rejection; no bypass was attempted. Latest SDK10/11 Managed/NativeAOT full-matrix, idle/input/memory/lifetime work and the 1,000 FPS target remain incomplete.

## GPU resource binding references and command-material invalidation

Hypothesis: the resident binding record contains many handles/counts/cache fields and is copied through dictionaries and preparation/classification/recording. `StaticGeometryBuffer` is now a sealed reference record. The normal absent-binding value has an explicit empty resource identity; it does not select a different renderer or suppress errors. Resource retirement and explicit GPU destruction still own native lifetimes. Collecting a metadata record never destroys its GPU handles. Active and retired containers hold references rather than repeated large value copies. Content revisions may allocate new records, so lower managed allocation is not assumed.

Profiling adds `UiRendererFrameMetrics.RendererAllocatedBytes`, measured on the calling thread inside `TryRenderDrawData`, and sample `rendererAllocatedBytesPerFrame`. This excludes outer UI generation and is not process/GPU memory. Disabled profiling returns null. Older executable results lacking this field remain unknown, not zero; no before/after allocation percentage is computed from missing data. Source mutation/promotion boundaries preserve the baseline executable throughout timed comparison.

During validation a same-size static material update reproduced wrong output: two initially matching image commands remained combined after changing only the first texture, causing the second to use that texture. `binding-material-repro.*.log` retains the failing GPU assertion. Source inspection identifies reused command-array wrapper identity as the stale topology-cache key; this code pattern predates the binding representation change, while the recorded reproduction uses the candidate. `UiDrawList.ComputeCommandTopologyStamp` now centrally defines index ranges, vertex offsets, kind, texture and callback presence. Static cache refresh compares this stamp and issues a new command wrapper when topology/material changes, while preserving vertex/index arrays. The added GPU test verifies both resulting colors and zero geometry staging upload. General visibility, clipping, opacity and camera state remain separate; a topology stamp alone is not a complete draw-state version.

Initial class builds pass 213 existing checks. After the material fix, `binding-material-fixed-gpu/result.json` and `binding-material-fixed-gpu-net11/result.json` pass 214 checks each. Candidate source snapshots are preserved in `binding-reference-source` and `binding-inref-source`. No shaders or GPU geometry formats changed.

`binding-record-ab` completes 12/12 independent 18-second ABBAAB runs, 1,200 tables/dense/resident/MSAA4/VSync off, camera and motion-tour. Baseline NativeAOT SHA256 is `3E93FA6F28860A3E4FDEAB6477D4C8AA2A172117F0472D65014641D1DE3AB8A1`; first class candidate is `351CFCD52A13A0852E433ACCE803A7B9E387710D195A22206933595DA382D937`. The candidate also includes material invalidation and allocation instrumentation. It is not a causal test of representation alone.

| First class candidate, medians across three runs | Baseline camera | Candidate camera | Baseline motion-tour | Candidate motion-tour |
| --- | ---: | ---: | ---: | ---: |
| FPS | 550.15 | 602.82 | 586.96 | 608.23 |
| Frame median, ms | 1.56680 | 1.43730 | 1.54590 | 1.48660 |
| p95, ms | 2.74916 | 2.47700 | 2.47312 | 2.43680 |
| p99, ms | 3.69820 | 3.25611 | 3.11908 | 2.94070 |
| 1% low FPS | 210.85 | 232.87 | 249.63 | 270.20 |
| GPU time, us | 724.37 | 706.88 | 602.35 | 586.05 |
| Actual draws/frame | 103.79 | 104.23 | 93.22 | 93.58 |
| UI scene allocation/frame, bytes | 720 | 720 | 3,544 | 3,552 |
| Renderer allocation/frame, bytes | unknown | 226.07 | unknown | 560.05 |

Every FPS, frame-time/tail and GPU-time improvement percentage is INVALID due to overlapping run ranges. One candidate camera run falls to 363.26 FPS and is retained; no cause is asserted. Camera draw averages increase 0.43% (compiled draws +0.59%) with disjoint ranges; this is a weighted-run observation, not fewer draws. The new cached topology field increases the UI allocation median by 8 bytes in motion-tour (+0.23%). These costs are not hidden. No build or GPU test overlaps the timed runs.

One bounded four-second follow-up (`binding-cpu-profile`) records CPU stage timing to identify remaining work. Its 668 completed app/Vulkan records have consecutive app IDs; the sample records 669 UI frames because shutdown can occur after UI building and before renderer submission. The initial exact-count assertion failed, then source inspection of the shutdown check justified excluding this single terminal UI-only frame. There are no interior gaps. After warmup frame 262, 406 paired records remain. Medians are UI render 926.95us, renderer 1,402.25us, combined app work 2,296.10us; target preparation 233.60us, upload/cache preparation 317.20us, command recording 453.55us, submit 34.80us, present 133.05us. GPU median is 167.664us in this short segment. This is instrumented diagnosis, not comparable throughput or a controlled stage improvement. A later idle hardware snapshot cannot establish the cause of the slow run.

The reference-type migration initially retained `in` parameters, introducing unnecessary references to references. Private binding parameters/calls now pass the object reference directly. Value-type frame contexts/commands retain their existing `in` contracts. `binding-reference-final-gpu/result.json` and `binding-reference-final-gpu-net11/result.json` pass 214 checks each; the final Release solution build (`binding-reference-solution.stdout.log`) has zero warnings/errors.

The final path has a separate attainment measurement, `binding-reference-fps-current`, six terminal 18-second runs, three per scenario, NativeAOT SHA256 `9A08DE1CE1367111EDD6A363E15EA6003F074F50938D13F19A590DD940DA6D68`. This is current-source verification; it does not supply a new paired speedup percentage for removing `in`.

| Latest current-source median | Camera | Camera + entity motion |
| --- | ---: | ---: |
| FPS | 604.62 | 620.66 |
| Target FPS | 1000 | 1000 |
| Attainment | 60.46% | 62.07% |
| Individual run-average range | 571.07–608.99 | 616.65–628.62 |
| Frame median, ms | 1.43200 | 1.44650 |
| p95, ms | 2.50312 | 2.42697 |
| p99, ms | 3.35581 | 2.87724 |
| 1% low FPS | 226.66 | 283.97 |
| GPU time, us | 703.11 | 587.34 |
| Actual draws/frame | 104.11 | 94.20 |
| UI allocation/frame, bytes | 720 | 3,552 |
| Renderer allocation/frame, bytes | 226.06 | 559.16 |
| Layer-state writes/frame, bytes | 0 | 499.51 |

The highest run-average FPS is 628.62. Neither scenario meets 1,000 FPS. After all timed handles ended and process checks found no running demo, candidate executable/PDB files were copied to the normal NET11 output path; readback hashes match (`binding-native-promotion.json`). The original baseline was checked against its recorded hash before replacement. Earlier comparison hashes remain historical inputs; do not rerun against the replaced default path and call it the old baseline. NET10 NativeAOT/release ZIPs remain older. No prior blocked cleanup was retried.

Next CPU work should give complete command state an explicit version before skipping descriptor reconstruction; pointer identity/topology alone is insufficient. When a source becomes partially visible, descriptors skipped while hidden must be populated before marking a cached state complete. Source inspection also shows UI building and renderer recording serialized on the same render thread in `DuxelAppSession`; any future pipeline requires geometry/cache lifetime isolation and authoritative submitted-frame timing first. These are remaining work, not implemented parallelism or proof of maximum hardware use.

## Command-state descriptor reuse (2026-11-03)

Hypothesis: after camera/model separation, unchanged compiled resident sources still reconstruct 64-byte command descriptors on the CPU, compare their shadow values and then discover that no GPU write is needed. Explicit command-state identity can avoid this CPU work without rasterizing or reducing vector quality.

Changes: `UiDrawList.CommandStateStamp` and `ComputeCommandStateStamp` cover complete descriptor state; zero preserves the unknown-state path. Resident replay/batching supply stamps, command mutation clears or recomputes them. Per-slot cache keys include immutable geometry binding, command state and display scale/translation. Hidden commands are initialized before marking a source current; display changes invalidate all members including hidden ones. `CompiledDescriptorBuilds` counts construction work. `DUXEL_VK_DESCRIPTOR_REUSE=0` retains the comparison path. GPU drawing and CPU visibility traversal still execute.

Correctness: .NET10 SDK10.0.301/runtime10.0.9 and .NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, Vulkan validation enabled. `descriptor-context-gpu/result.json` and `descriptor-reuse-gpu-net11/result.json` pass 223 checks each. Disabled reuse passes 217 checks (`descriptor-reference-gpu/result.json`); six additional reuse-specific checks are conditional. Tests include warmed camera-only descriptor count0, unknown-stamp reconstruction, hidden opacity changes followed by reveal, and display-context changes while a member is hidden. No polygonal quality fallback or shader change is involved.

NativeAOT build: existing isolated `binding-candidate` output reused; SHA256 `99BB49B272BC7CF36CDDA44BBB69E8FE733E112D0D32682D2CF17E0FE72A13FA`. Same-executable alternating On/Off measurement uses `scripts/measure-erd-layers.ps1 -CompareDescriptorReuse`, 1200 tables, dense view, resident mode, camera/motion-tour, 3 independent 18-second runs per variant/scenario. Warmup is one second after the second frame. Timed runs are exclusive of builds/tests/profiling. Raw data and execution ledger live in `artifacts/perf-2026-11-03/descriptor-reuse-ab`. Final FPS and timing results follow after all runs terminate.

Risks: callers that opt into a nonzero stamp must update it when descriptor state changes; arbitrary external lists default to safe unknown state. Stamp computation uses a 64-bit hash, so collisions are theoretically possible. Extra per-slot arrays retain managed binding metadata; resource destruction still follows explicit GPU fence ownership. This change removes descriptor construction, not all per-command CPU traversal, GPU draws or present costs. Target remains 1000 FPS.

Final same-executable A/B: 12/12 terminal runs, reuse disabled -> enabled. All values are independent-run medians; FPS and timing ranges overlap, so their improvement percentages are INVALID. Keep the structural change without claiming a throughput gain.

| Metric | Camera before | Camera after | Motion before | Motion after |
| --- | ---: | ---: | ---: | ---: |
| averageFps | 605.87155 | 606.28937 | 596.65938 | 611.03184 |
| medianMs | 1.43170 | 1.42510 | 1.47160 | 1.46190 |
| p95Ms | 2.49350 | 2.49471 | 2.52650 | 2.48053 |
| p99Ms | 3.43611 | 3.23956 | 3.39668 | 3.16769 |
| low1PctFps | 217.97322 | 224.27346 | 223.48520 | 243.05058 |
| compiledDescriptorBuilds | 2446.54911 | 0.00000 | 2117.89654 | 7.39868 |
| drawCalls | 104.28493 | 104.40819 | 93.05793 | 94.51743 |
| gpuMicroseconds | 709.46692 | 706.05677 | 589.35309 | 594.28532 |
| sceneAllocatedBytesPerFrame | 736.00000 | 736.00000 | 3584.00000 | 3584.00000 |
| rendererAllocatedBytesPerFrame | 226.05485 | 226.05350 | 560.55361 | 560.92747 |
| frameUploadBytes | 6497.36873 | 6496.26728 | 7655.79129 | 7625.17428 |
| layerStateUploadBytes | 0.00000 | 0.00000 | 499.58618 | 503.27603 |

Current target attainment: camera606.29/1000 (60.63%), motion611.03/1000 (61.10%). Current run-average ranges599.62–612.57 and604.79–612.08. Highest current run-average612.57. Previous-source results604.62/620.66 and historical peak run-average628.62 remain historical, not current. Neither scenario meets1000. Descriptor construction falls2446.55 ->0 (100%) and2117.90 ->7.40 (99.65%). Motion UI allocation rises from the prior-source3552B to3584B/frame; the same-executable On/Off test is unchanged3584B. Motion renderer allocation560.55 ->560.93B/frame and draws93.06 ->94.52 are recorded increases; differing wall-time camera phases prevent attributing draw-count differences to descriptor caching. No extra timed runs were discarded.

After all twelve timed children terminated and the Release solution build passed with zero warnings/errors, process audit found no running demo. The default NET11 executable/PDB were updated only after verifying the old expected hash; readback matches candidate99BB49B272BC7CF36CDDA44BBB69E8FE733E112D0D32682D2CF17E0FE72A13FA (`descriptor-native-promotion.json`). Paired ERD docs now show606.29/611.03 FPS against1000. Source/public API docs describe stamp invalidation and metric semantics. No blocked cleanup was retried. Next work remains submitted-frame timing and remaining CPU submission/traversal work; the complete objective is active.

## Accepted presentation timing and compiled command spans (2026-11-03)

Hypotheses: UI-loop timestamps can include UI iterations whose draw data never reaches presentation (for example the auto-exit iteration), whereas accepted QueuePresent timestamps identify actual application presentation requests. Separately, descriptor reuse still traverses each command in entirely visible static members. Cache a contiguous source index span and its profile transition summary to remove that traversal without changing GPU work.

Changes: renderer metrics add successful-presentation sequence, Stopwatch timestamp and interval, independent of profiling. False/zero-size renders do not advance sequence. ERD consumes each advancing sequence once, changes HUD/CSV to accepted-presentation intervals, records timing basis, and separates UI frame counts from observed accepted presentation counts. This is not monitor-refresh or GPU-completion timing. Summaries reject mixed UI-loop/presentation clocks. Compiled groups precompute nonempty command counts and kind transitions. Fully inside, descriptor-current members append cached index ranges; partial/unknown/changed members retain the original loop. Profiling merges first/last boundaries and all internal transitions exactly. The compiler still submits real Vulkan indexed draws and GPU vector geometry; no texture raster cache or shader quality change.

Correctness: NET10 command-spans-gpu/result.json and NET11 command-spans-gpu-net11/result.json pass261 checks, including warmed camera planes with zero command visits,256 base checks with presentation sequences/timestamps and zero-size skips, and profile partition equivalence. The final NET11 test covers729 kind patterns, every split and empty commands. NET10 disabled spans passes257 checks (`command-spans-reference-gpu/result.json`). The four enabled-only checks assert zero CPU visits. NET10 SDK10.0.301/runtime10.0.9; NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148. Vulkan validation enabled.

Measurement contract: same NET11 NativeAOT executable, spans Off/On,1200 tables,dense/resident,MSAA4,VSyncoff,camera and motion-tour,three18-second independent runs per variant/scenario,one-second warmup after frame2, alternating execution order. No builds/tests/profiling runs overlap timed children. `command-spans-ab` retains raw CSV, per-run JSON, ledger and summary. These FPS values cannot supply an improvement percentage against prior UI-loop606.29/611.03. Final results follow.

Risks: source profile summaries assume group topology identity; descriptor state still has its separate stamp. Only wholly inside members skip per-command culling. Accepted presentation timestamps do not prove that every request reaches a physical scanout; asynchronous GPU work remains. Original+compiled index memory remains a separate outstanding cost. No previously rejected cleanup was retried.

Final:12/12 terminal runs; same accepted-presentation timing basis. Target1000 remains NOT_MET. Current camera603.96 (60.40%),motion621.75 (62.17%), ranges593.02–613.48 and620.39–624.58; highest current run-average624.58. NativeAOT SHA256826B6F4DD79BE5179ECC9EA84A87351DAF85CC4D4EEFF20A66602A57086569E2.

| Metric | Camera before | Camera after | Motion before | Motion after |
| --- | ---: | ---: | ---: | ---: |
| averageFps | 591.65853 | 603.95678 | 607.40612 | 621.74515 |
| medianMs | 1.43330 | 1.42385 | 1.45765 | 1.44120 |
| p95Ms | 2.50771 | 2.48821 | 2.50919 | 2.44407 |
| p99Ms | 3.37939 | 3.31647 | 3.23183 | 2.84677 |
| low1PctFps | 217.02267 | 213.82897 | 264.47453 | 271.25458 |
| compiledCommandVisits | 6184.16339 | 4042.30983 | 4676.29014 | 3158.33842 |
| compiledDescriptorBuilds | 0.00000 | 0.00000 | 7.40405 | 7.37982 |
| drawCalls | 104.38386 | 104.35529 | 93.62380 | 93.81487 |
| gpuMicroseconds | 709.22351 | 707.40892 | 587.28608 | 587.47341 |
| sceneAllocatedBytesPerFrame | 736.00000 | 736.00000 | 3584.00000 | 3584.00000 |
| rendererAllocatedBytesPerFrame | 226.10545 | 226.06273 | 561.31549 | 561.28794 |
| frameUploadBytes | 6538.77712 | 6503.82277 | 7633.89208 | 7591.46972 |
| layerStateUploadBytes | 0.00000 | 0.00000 | 503.65709 | 502.05318 |

camera/medianMs: -0.65932% (disjoint-run-ranges).
camera/compiledCommandVisits: -34.63449% (disjoint-run-ranges).
motion-tour/compiledCommandVisits: -32.46060% (disjoint-run-ranges).
FPS percentage claims are INVALID because run ranges overlap. Camera frame median is disjoint; do not turn that one metric into average-FPS achievement. All tail/FPS/GPU comparisons and allocation details remain in summary.json. Accepted presentation metrics do not remove CPU traversal for partial sources or GPU raster work.
Final correctness adds an explicit first-presentation sequence1/interval0 check: command-spans-final-gpu/result.json and command-spans-final-gpu-net11/result.json pass262 each. Timing guard accepts same-clock results and rejects mixed clocks before calculating a summary (`command-spans-timing-guard/result.json`). Release solution build has zero warnings/errors. No timed process remained live before build/test validation. Next bottleneck candidate is command-level frustum work in partial members (approximately4042/3158 loop visits/frame remain).
Default NET11 executable/PDB promotion verified against old/new hashes after zero-demo audit; receipt command-spans-native-promotion.json. Final hash826B6F4DD79BE5179ECC9EA84A87351DAF85CC4D4EEFF20A66602A57086569E2. A trailing blank line found by diff-check was removed and recheck passed; no semantic source change. NET10 NativeAOT/release ZIPs remain older. Complete goal remains active.

## Common layer-bound aggregation reuse (2026-11-03)

Hypothesis: the compiled command span path still starts with `GetLayerVisibility` scanning every source command to aggregate bounds each frame. Cache source-space aggregate bounds when command state is known, then transform/classify the cached rectangle for each current camera/model/display context. Apply the same path to direct and compiled layers.

Changes: `CommandStateStamp` now fingerprints Bounds and HasBounds in addition to descriptor state. Frame-indexed visibility entries retain aggregate bounds by stamp; unknown stamps recompute, and invalid/unknown bounds remain conservative. The common visibility cache lives in LayerVisibility.cs, is prepared even when compiled submission is disabled, compares stamps after source reordering, and releases with renderer caches. Direct submission reuses the same per-frame viewport result and no longer double-counts culled lists. `LayerBoundsCommandVisits` distinguishes aggregation scans from command recording visits. `DUXEL_VK_LAYER_BOUNDS_REUSE=0` is the comparison path. No pixel cache, layer image or shader-quality change is involved.

Correctness: NET10 SDK10.0.301/runtime10.0.9, NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148; Vulkan validation enabled. layer-bounds-gpu/result.json and layer-bounds-gpu-net11/result.json pass271 checks each. Direct submission passes247 (layer-bounds-direct-gpu); bounds reuse disabled passes266 (layer-bounds-reference-gpu). Tests cover warmed camera0 aggregation visits, hidden-source reuse, outside-to-inside geometry revision, reordered resident sources, unknown stamp rescans, and stamp sensitivity to bounds/HasBounds. Five enabled-only checks are skipped when reuse is disabled.

Measurement contract: one NET11 NativeAOT SHA2566BF9DC67EAA3F8FEACA924A13122893B30A4C15271C22F5137853922C0AB1FDD, same executable On/Off,1200 tables,dense/resident,MSAA4,VSyncoff,camera and motion-tour,3 independent18-second runs per variant/scenario,1-second warmup after frame2. Accepted-presentation timing basis, alternating order, exclusive timed children. Results live in layer-bounds-ab; final results follow after all12 children end. Current-source hash and source sample remain fixed during measurement.

Risks: a caller opting into a nonzero command state must now update it for bounds changes too. Default zero remains conservative. Cache metadata grows per draw-list position and resets when list count changes. The64-bit hash has theoretical collision risk. Partial members still perform command-level frustum tests; GPU rendering/presentation and original+compiled index memory remain costs. No denied cleanup was retried.

Final12/12 terminal runs. Current accepted-presentation FPS619.91/620.71, target1000 attainment61.99%/62.07%. Current ranges610.61–625.12 and602.74–653.80; highest current run-average653.80. Camera comparison497.28 ->619.91 (+24.66%) has disjoint ranges484.10–497.65 versus610.61–625.12. Motion comparison592.53 ->620.71 overlaps; its FPS improvement percentage is INVALID. Do not extrapolate the camera result to every workload.

| Metric | Camera before | Camera after | Motion before | Motion after |
| --- | ---: | ---: | ---: | ---: |
| averageFps | 497.27602 | 619.91042 | 592.52719 | 620.71148 |
| medianMs | 1.83355 | 1.35500 | 1.50185 | 1.41680 |
| p95Ms | 3.13782 | 2.49523 | 2.60359 | 2.48299 |
| p99Ms | 4.48895 | 3.23604 | 3.27331 | 2.92824 |
| low1PctFps | 172.33080 | 216.03699 | 257.18563 | 280.74675 |
| layerBoundsCommandVisits | 9606.00000 | 0.00000 | 9610.00000 | 9.00000 |
| compiledCommandVisits | 3940.51885 | 4060.57666 | 3146.16158 | 3152.72876 |
| compiledDescriptorBuilds | 0.00000 | 0.00000 | 7.48132 | 7.32371 |
| drawCalls | 105.62442 | 103.90798 | 93.32209 | 92.99980 |
| gpuMicroseconds | 801.27408 | 689.92003 | 596.58756 | 598.54881 |
| sceneAllocatedBytesPerFrame | 736.00000 | 736.00000 | 3584.00000 | 3584.00000 |
| rendererAllocatedBytesPerFrame | 226.53409 | 226.00256 | 563.72164 | 561.14273 |
| frameUploadBytes | 6855.51496 | 6454.58262 | 7686.68248 | 7588.04431 |
| layerStateUploadBytes | 0.00000 | 0.00000 | 508.86910 | 498.21837 |

camera/averageFps: 24.66123% (disjoint-run-ranges).
camera/medianMs: -26.09964% (disjoint-run-ranges).
camera/p95Ms: -20.47887% (disjoint-run-ranges).
camera/p99Ms: -27.91095% (disjoint-run-ranges).
camera/low1PctFps: 25.36180% (disjoint-run-ranges).
camera/rendererAllocatedBytesPerFrame: -0.23464% (disjoint-run-ranges).
camera/frameUploadBytes: -5.84832% (disjoint-run-ranges).
camera/compiledLayerDraws: -2.24007% (disjoint-run-ranges).
camera/sharedCameraLayerDraws: -2.24007% (disjoint-run-ranges).
camera/compiledLayerCommands: -10.80999% (disjoint-run-ranges).
camera/compiledCommandVisits: 3.04675% (disjoint-run-ranges).
camera/layerBoundsCommandVisits: -100.00000% (disjoint-run-ranges).
camera/drawCalls: -1.62504% (disjoint-run-ranges).
camera/gpuMicroseconds: -13.89712% (disjoint-run-ranges).
motion-tour/low1PctFps: 9.16113% (disjoint-run-ranges).
motion-tour/layerStateUploadBytes: -2.09302% (disjoint-run-ranges).
motion-tour/compiledDescriptorBuilds: -2.10661% (disjoint-run-ranges).
motion-tour/layerBoundsCommandVisits: -99.90635% (disjoint-run-ranges).
The camera compiled-command visit count rises3940.52 ->4060.58 (+3.05%) and is retained above; wall-time camera trajectory samples differ across frame rates. All raw runs, including the slow baseline and lower current motion run, are preserved. GPU timing changes are measurements, not evidence that a CPU-bound cache alone caused a GPU-clock improvement. Target1000 is NOT_MET in both scenarios.
A budgeted single6-second camera diagnostic follows timed-run termination, existing APP_PROFILE every16 frames, first96 frames excluded:168 samples(frame112..2784). Mean UI626.81us, renderer727.67us,total1354.91us; medians587.80/657.25/1246.80us. Source submission2481lists,22651552vertices,67481268indices (source totals, not visible GPU draw counts). Evidence layer-bounds-cpu-profile/summary.json and app-profile.log. Its709.58FPS single phase is diagnostic only, fewer than3 runs and a different duration; never replace current619.91/620.71 target measurements with it. Initial preflight rejected an already-created output directory before any child ran; the input contract stays at its root and the actual single costful run used a fresh run child directory. Original failed logs are preserved. No diagnostic reruns or timed-run overlap.
Release solution build passes0 warnings/errors (layer-bounds-solution.stdout.log). Next substantial candidate is the per-camera UI re-submission of unchanged resident layer lists: the profile shows material CPU time on both UI and renderer, so optimize scene submission ownership/shared views rather than assuming command traversal is the sole remaining bottleneck. This is a next-step hypothesis, not implemented retained-scene API or promised FPS.
Default NET11 executable/PDB promotion follows all measured/diagnostic child exits and zero-demo audit. Expected old826B6F4D... hash checked before replacement; destination readback matches6BF9DC67EAA3F8FEACA924A13122893B30A4C15271C22F5137853922C0AB1FDD. Receipt layer-bounds-native-promotion.json. NET10 NativeAOT and release archives remain older. No blocked cleanup retried. Goal remains active.

## Direct retained-layer replay submission (2026-11-03)

Hypothesis: the prior diagnostic measured UI composition mean626.81us alongside renderer727.67us. Each retained hit still pushes/pops a large UiLayerFrame through BeginLayer/EndLayer, copying options and reconstructing scope state. Provide a common direct replay API for valid retained entries while preserving normal construction for misses, revisions and dirty content.

Changes: `TryReplayLayer(string, in UiLayerOptions)` checks retention/version/cache validity, validates the scene on hits, derives the current local clip and calls shared ReplayLayerCache logic without opening a scope. Existing skipped Begin/End scopes call that same helper. Empty retained content is a hit; dynamic/no-version, removed, dirty, active-builder or mismatched-version content misses without mutating cache versions. The current camera wrapper and geometry lease paths remain shared. ERD grid/relationships/tables/overview use this API, with DUXEL_ERD_DIRECT_REPLAY0 selecting normal scopes for comparison. Per-layer lookup, validation and submission still run; no bulk retained-scene or parallel pipeline is claimed.

Correctness: NET10 SDK10.0.301/runtime10.0.9 and NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148. Vulkan validation enabled. direct-replay-final-gpu/result.json and direct-replay-gpu-net11/result.json each pass280 checks. Added tests cover exact camera/opacity pixels against scoped submission, overlapping frame camera isolation with shared immutable geometry, content revision misses, dirty state, empty retained content, dynamic/no-version misses, removed entries, invalid cameras on empty hits, and nested parent/child opacity and translation.

Runner comparison selection is now validated in one list rather than growing pairwise switch conditions. More than one mode is rejected before output/child initialization; direct-replay-validation-conflict/result.json confirms the intended rejection. The first attempt to write that test receipt failed because the guard rejects before the directory exists; a subsequent check creates the evidence directory after rejection and verifies readback. No child ran for the negative case.

Measurement contract: same NET11 NativeAOT executable, direct replay Off/On,1200 tables,dense/resident,MSAA4,VSyncoff,camera/motion-tour,three independent18-second runs per variant/scenario,one-second warmup after frame2,alternating order,accepted-presentation clock. All earlier GPU optimizations explicitly enabled and held constant. No builds/tests or extra profiled runs overlap timed children. Raw evidence is direct-replay-ab. Final results follow.

Risks: a successful TryReplayLayer must not be followed by EndLayer because it did not open a scope. Callers remain responsible for content versions/dirty notifications, as with existing retained scopes. Current options are passed by readonly reference to avoid another large copy. This reduces scope work, not all UI resubmission, camera validation, renderer traversal or final GPU rasterization. The full objective and1000FPS target remain active.

Final12/12 terminal runs, NativeAOT SHA256F497CA641D7894B3ABB3C46716CDE4AD42FEE722E7C5A71CDDEFA28E33C851E6. Current FPS674.06(camera)/686.40(motion), target1000 attainment67.41%/68.64%, NOT_MET. Current run-average ranges673.63–680.98 and613.52–689.84; highest current run-average689.84. Camera scoped608.95 ->direct674.06 (+10.69%), ranges603.07–619.27 versus673.63–680.98 disjoint. Motion scoped594.62 ->direct686.40 overlaps; no FPS improvement percentage is established. The613.52 current motion run is preserved and included.

| Metric | Camera before | Camera after | Motion before | Motion after |
| --- | ---: | ---: | ---: | ---: |
| averageFps | 608.95061 | 674.06474 | 594.62186 | 686.40110 |
| medianMs | 1.38985 | 1.21860 | 1.48670 | 1.25660 |
| p95Ms | 2.50573 | 2.45210 | 2.49578 | 2.40613 |
| p99Ms | 3.37317 | 2.93850 | 3.10060 | 2.78411 |
| low1PctFps | 218.27973 | 243.67821 | 266.75062 | 282.37436 |
| layerBoundsCommandVisits | 0.00000 | 0.00000 | 9.00000 | 9.00000 |
| compiledCommandVisits | 4041.60026 | 4116.89310 | 3174.63244 | 3132.19887 |
| compiledDescriptorBuilds | 0.00000 | 0.00000 | 7.41978 | 7.33446 |
| drawCalls | 104.36148 | 102.32168 | 93.65085 | 91.25699 |
| gpuMicroseconds | 698.16799 | 640.13383 | 586.98020 | 549.70438 |
| sceneAllocatedBytesPerFrame | 736.00000 | 736.00000 | 3584.00000 | 3584.00000 |
| rendererAllocatedBytesPerFrame | 226.03859 | 225.84031 | 561.34681 | 559.06803 |
| frameUploadBytes | 6484.06503 | 6321.82857 | 7660.43556 | 7437.83850 |
| layerStateUploadBytes | 0.00000 | 0.00000 | 504.74147 | 498.93544 |

camera/averageFps: 10.69284% (disjoint-run-ranges).
camera/medianMs: -12.32147% (disjoint-run-ranges).
camera/p95Ms: -2.14010% (disjoint-run-ranges).
camera/p99Ms: -12.88623% (disjoint-run-ranges).
camera/low1PctFps: 11.63575% (disjoint-run-ranges).
camera/rendererAllocatedBytesPerFrame: -0.08772% (disjoint-run-ranges).
camera/frameUploadBytes: -2.50208% (disjoint-run-ranges).
camera/compiledLayerDraws: -2.70668% (disjoint-run-ranges).
camera/sharedCameraLayerDraws: -2.70668% (disjoint-run-ranges).
camera/compiledLayerCommands: -7.75793% (disjoint-run-ranges).
camera/compiledCommandVisits: 1.86295% (disjoint-run-ranges).
camera/drawCalls: -1.95455% (disjoint-run-ranges).
camera/gpuMicroseconds: -8.31235% (disjoint-run-ranges).
Wall-time camera trajectories can differ slightly with frame rates; physical draw counts102.32/91.26 remain actual GPU draws, not proof that bypassing UI scopes itself changed batching. Do not attribute GPU-time changes solely to this CPU API. Allocations remain736/3584B per UI frame in both routes.
Separate budgeted CPU diagnostic: direct-replay-cpu-profile/input-contract.json, two6-second camera runs, same binary On/Off, APP_PROFILE every16 frames, first96 excluded. Warm samples144/158, source lists2481 per frame. UI mean718.05 ->583.23us, medians693.85 ->545.70us; renderer mean804.64 ->834.54us (increase preserved), total1523.14 ->1418.21us. Each route has one diagnostic phase only and different logging/trajectory conditions; diagnostic FPS650.37 is not substituted for repeated18-second current FPS. Summary.json preserves phases and raw append log is segmented by process frame-counter resets. No builds/tests overlapped timed or diagnostic children.
Final Release solution build passes0 warnings/errors (direct-replay-solution.stdout.log). API/reference/layer/sample docs updated in both languages. Geometry is shared safely in the tested overlapping camera-only/same-content case. Before any future UI/renderer pipeline, content-version overlap requires a separate lifetime check: CaptureLayerLists currently calls ReleaseOwnedLayerSources before replacement and static-cache refresh can overwrite same-sized buffers. Source inspection is not proof of concurrent-content correctness, and no concurrent pipeline is claimed. Next work includes geometry ownership verification and reducing remaining per-layer submission/validation/lease costs. No rejected cleanup was retried.
Default NET11 executable/PDB updated after all timed/diagnostic children and Release build terminated, zero-demo audit, expected old6BF9DC67... hash checked. Readback matches F497CA641D7894B3ABB3C46716CDE4AD42FEE722E7C5A71CDDEFA28E33C851E6; receipt direct-replay-native-promotion.json. NET10 NativeAOT/release archives remain older. No denied cleanup retried. Goal remains active.

## Retained CPU-frame lifetime and independently owned clip copies (2026-11-03)

Reproduction: frame-lifetime-before-gpu.stderr.log records the failing retained-content-revision assertion. CaptureLayerLists returned the previous OwnedSource buffers while an older UiDrawData still referenced them. Static-cache refresh and replay-opacity changes could also overwrite shared arrays. After adding frame ownership, frame-lifetime-scaled-before-gpu.stderr.log separately records scaled-copy disposal releasing original ownership/container state; ScaleClipRects copied frame lease/update-container references despite deep-copying geometry.

Implementation: one UiLayerFrameLifetime epoch lease per CPU frame, acquired before its first retained append, covers all retained sources. Retired pooled sources are queued with the latest referencing epoch and drain when older readers complete, even while newer readers stay active. Static geometry and replay commands copy before modification when readers exist. UiDrawData owns the transferred lease; original caller ownership ends at ReleasePooled/Dispose. UiContext discards unclaimed outputs and pending abandoned-render leases at the next frame/render, EndFrame or disposal; claimed data can outlive the context. This per-frame mechanism avoids per-layer reference-count operations and introduces no GPU wait, layer texture or new rendering pass.
ScaleClipRects now constructs an independent UiDrawData: copied geometry/commands/update containers, no copied source/camera leases, including empty frames and command-empty draw lists. ReadOnlyMemory payload ownership remains the existing immutable-data contract. Test access to internal lifetime state is enabled only by DuxelValidationHooks; production builds do not expose a new public diagnostic API.

Validation: NET10 frame-lifetime-verified-gpu/result.json and NET11 frame-lifetime-complete-gpu-net11/result.json pass294 checks each; direct submission passes270 (frame-lifetime-complete-direct-gpu/result.json). SDK10.0.301/runtime10.0.9; SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148; Vulkan validation enabled. Exact GPU old/new version pixels, retirement while a newer frame remains borrowed, static refresh, opacity state, scaled-copy/original independent disposal, empty copies, unclaimed output, abandoned pending leases and context disposal are covered. One intermediate fixture build failed with CS0121 because a collection expression matched List and ReadOnlySpan overloads; the span type was made explicit, and the failed log prefix is preserved (frame-lifetime-final-gpu).

Performance: frame-lifetime-ab contains12 terminal18-second runs,1200 tables,dense/resident,MSAA4,VSyncoff,camera/motion-tour,3 independent runs per scenario/variant,1-second warmup after frame2,alternating order,accepted-presentation timing. Baseline exe SHA256F497CA641D7894B3ABB3C46716CDE4AD42FEE722E7C5A71CDDEFA28E33C851E6; current9EA8D1F479ABA25F6EC6BF220B909060E96D65A5EB37967A29A44E1FF0040135. The old baseline is used only for this sequential workload; it does not provide the corrected overlapping-frame guarantee. No builds/tests/profiles overlap timed children.

| Metric | Camera before | Camera after | Motion before | Motion after |
| --- | ---: | ---: | ---: | ---: |
| averageFps | 670.18048 | 667.19833 | 677.95348 | 674.19373 |
| medianMs | 1.23370 | 1.24100 | 1.27470 | 1.28210 |
| p95Ms | 2.45962 | 2.45171 | 2.39799 | 2.41198 |
| p99Ms | 3.22306 | 2.93252 | 2.78770 | 2.86943 |
| low1PctFps | 232.76324 | 243.14162 | 280.50659 | 288.69382 |
| layerBoundsCommandVisits | 0.00000 | 0.00000 | 9.00000 | 9.00000 |
| compiledCommandVisits | 4106.54135 | 4108.38269 | 3129.86973 | 3137.77632 |
| compiledDescriptorBuilds | 0.00000 | 0.00000 | 7.34386 | 7.30774 |
| drawCalls | 103.00374 | 103.26127 | 91.48134 | 91.92880 |
| gpuMicroseconds | 647.50991 | 648.01905 | 553.86339 | 560.45134 |
| sceneAllocatedBytesPerFrame | 736.00000 | 736.00000 | 3584.00000 | 3592.00000 |
| rendererAllocatedBytesPerFrame | 225.85155 | 249.85721 | 559.68139 | 582.89101 |
| frameUploadBytes | 6331.01949 | 6335.65715 | 7455.07395 | 7466.27302 |
| layerStateUploadBytes | 0.00000 | 0.00000 | 499.55452 | 497.19298 |

camera/rendererAllocatedBytesPerFrame: 10.62896% (disjoint-run-ranges).
motion-tour/sceneAllocatedBytesPerFrame: 0.22321% (disjoint-run-ranges).
motion-tour/rendererAllocatedBytesPerFrame: 4.14694% (disjoint-run-ranges).
motion-tour/layerStateUploadBytes: -0.47273% (disjoint-run-ranges).
motion-tour/compiledDescriptorBuilds: -0.49174% (disjoint-run-ranges).
Current FPS667.20/674.19, target1000 attainment66.72%/67.42%, current ranges665.66–673.65 and663.55–686.91. Highest current run-average686.91. Baseline670.18/677.95 is slightly higher, but FPS ranges overlap; regression/improvement percentage claims are INVALID. Increased motion UI allocation3584 ->3592B/frame and renderer225.85 ->249.86(camera),559.68 ->582.89B/frame remain measured costs rather than being hidden. UI and renderer allocation scopes differ, and output-object fields can also allocate outside the screen-body sample. Correct lifetime semantics are retained despite these costs. Target1000 remains NOT_MET.

Release solution build passes0 warnings/errors (frame-lifetime-solution.stdout.log). Paired layer/API/sample documentation now explains ownership and current FPS. Geometry lifetime is one prerequisite for future overlapped UI/renderer execution, not proof of UiContext thread safety or a completed pipeline: renderer-feedback atomicity, accepted-frame timing delivery, texture payload ownership and callback ordering still need review before changing execution order. Remaining per-layer UI submission/validation/lease costs are optimization candidates. No prior denied cleanup was retried; old NET10 NativeAOT/release archives are not relabeled current.
Default NET11 executable/PDB promotion after all timed/build handles terminated and zero-demo audit: oldF497CA64... verified before copying, readback9EA8D1F479ABA25F6EC6BF220B909060E96D65A5EB37967A29A44E1FF0040135. Receipt frame-lifetime-native-promotion.json. The baseline path now holds the new binary; historical before measurements must use their recorded hash, not a rerun of this replaced path. Full goal remains active.

## Atomic presentation history and UI source-frame correlation (2026-11-03)

Hypothesis/requirement: UI/renderer overlap needs coherent feedback and every accepted presentation interval, not just whichever last metrics the UI happens to read. Otherwise a fast renderer can advance twice while UI work is running, lose a timing sample and associate scene counters with the wrong source frame. This step prepares the transport/measurement contract; it does not enable a parallel app loop or claim a throughput optimization.

Implementation: each UiContext.Render pass assigns a checked64-bit DrawDataFrameId, exposed during UI construction, retained by UiDrawData and its independent clip copies, and carried by Vulkan metrics. UiState renderer feedback publishes through an internal locked64-entry history with an atomic latest record, FIFO drains, explicit overwrite count and regression rejection. Duplicate sequences update latest stats without another timing row. IUiPresentationReader exposes a single-consumer drain and can outlive UI state buffers; UiContext itself is still single-writer. GPU timestamp values retain their older frame-slot origin/delay, not current-source timing identity.
ERD correlates scene-build/allocation telemetry to source IDs, drains all accepted frames and final results at SaveMeasurement, gates warmup on presentation timestamps and fails on sequence gaps, missing source telemetry, overwritten history or invalid intervals. Legacy one-column frame CSV is unchanged; a new presentationSequence/drawDataFrameId sidecar has one row per raw frame. Source IDs replace the old duplicate duration array, using BenchFrameRecorder.FrameSeconds instead, so the1,048,576-element capacity does not gain another raw duration buffer. The runner audits CSV headers, monotonic source IDs, contiguous presentation sequences, row counts, overflow and actual warmup metadata. Different sampling methods are marked NONCOMPARABLE for percentage claims.

Verification: presentation-history-final-gpu/result.json and presentation-history-gpu-net11/result.json pass302 checks each; direct path278 (presentation-history-direct-gpu). NET10 SDK10.0.301/runtime10.0.9; NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148. Vulkan validation enabled. History tests cover queued-before-consume samples, partial reads, duplicates, overwrite, rejected regression,500 concurrent publications/drains without lost/torn source identity, final reader access after state buffer disposal, GPU source IDs and clip-copy identity. presentation-history-audit-cases/result.json passes6 valid/invalid CSV cases; presentation-history-method-guard/result.json rejects all mixed-method quantitative percentages.
Development corrections: one shell invocation used the NET11 fixture cwd for root-relative source paths and stopped before running tests. A PowerShell multiline condition failed parsing and was fixed by ending the preceding line with its operator. Native builds completed before final warmup timestamp metadata was copied; no timed result from those provisional images is used. Root/candidate source hashes were verified before presentation-history-ready-native-build; only that fixed binary was used for timed runs. Intermediate logs remain preserved.

Measurement: current-only6 terminal18-second runs,3 per camera/motion-tour,1200 tables,dense/resident,MSAA4,VSyncoff,1-second warmup after UI frame2,accepted-presentation clock,presentation-history-v1. NativeAOT SHA25638D40B534B4DB56F9DFC1414A503E342B81B05336A0B1DC76D4FE30ED1600727. No builds/tests/profiles overlap these six timed children.

| Metric | Camera current | Motion current |
| --- | ---: | ---: |
| averageFps | 629.64665 | 483.74480 |
| medianMs | 1.32870 | 1.95620 |
| p95Ms | 2.51381 | 3.52735 |
| p99Ms | 3.17513 | 4.41034 |
| low1PctFps | 235.43220 | 189.52880 |
| layerBoundsCommandVisits | 0.00000 | 9.00000 |
| compiledCommandVisits | 4093.35803 | 3083.81823 |
| compiledDescriptorBuilds | 0.00000 | 7.56088 |
| drawCalls | 103.45565 | 93.26860 |
| gpuMicroseconds | 672.20312 | 673.69525 |
| sceneAllocatedBytesPerFrame | 736.00000 | 3592.00000 |
| rendererAllocatedBytesPerFrame | 257.98250 | 603.16972 |
| frameUploadBytes | 6438.17434 | 8079.12176 |
| layerStateUploadBytes | 0.00000 | 514.27807 |

Current FPS629.65(camera)/483.74(motion), target1000 attainment62.96%/48.37%, NOT_MET. Ranges488.67–651.64 and463.05–692.46; highest repeated-set run-average692.46. All lower runs are retained. These are lower and more variable than prior latest-snapshot results; the variation cause is unknown. Different collectors/warmup association are not mixed to manufacture a speedup/regression percentage.
Each of the6 runs has exactly one audited link per sample (7364,7178,9487,6976,9854,10505),0 overwritten samples and0 produced-source gaps in the recorded interval. Scene builds remain0(camera)/1(motion) per accepted frame. Current UI/accepted totals are7927/7927,7642/7642,10068/10068,7411/7411,10429/10428,11215/11215; a final UI invocation can be intentionally unsubmitted during shutdown.
A separate budgeted6-second camera diagnostic (presentation-history-cpu-profile/input-contract.json,maximum1 run,every16 app frames,first96 excluded) has mean UI535.70us/renderer740.71us,total1276.92us; medians490.85/659.90/1154.50us. It does not explain wide18-second variation or prove causality. Its751.10FPS is insufficient repetitions and a different camera phase, never substituted for629.65/483.74. Raw log/summary preserved.

Release solution build passes0 warnings/errors (presentation-history-solution.stdout.log). Paired API/layer/sample docs include source identity, single-reader history, timestamp warmup, sidecar and sampling-method compatibility. Remaining work is the bounded UI-producer/renderer-consumer implementation with texture-payload/callback/settings ownership and shutdown verification. Geometry lifetime and coherent feedback now exist, but actual app overlap is not enabled. No denied cleanup was retried; older NET10 NativeAOT/release archives remain older.

## Bounded UI and renderer execution pipeline (2026-11-03)

Hypothesis: previous APP_PROFILE UI535.70us and renderer740.71us execute serially. A bounded producer/consumer path can overlap independent CPU work while preserving retained vector geometry, source-frame identity and one Vulkan render path. No promised FPS gain is inferred from the diagnostic sum.

Implementation: DuxelFrameOptions.EnableRenderPipeline (opt-in, defaultfalse) connects the actual app loop to DuxelRenderQueue. One executing frame plus one pending frame; the UI may own the next frame under construction. Submit applies backpressure, transfers exclusive ownership, never replaces/drops queued work and allocates no task/delegate per frame. Vulkan operations and renderer settings have one consumer; settings, resize sequence and profiling timestamps are immutable per-frame packets. UiContext stays on one producer, feedback is the previously validated atomic history, SubmitTimeMs is a volatile completed-duration snapshot. First-frame/resize notifications follow accepted presentation. Complete drains/joins before context/backend disposal. Consumer exceptions dispose active/pending frames, wake stop/resize waits and propagate the original exception through the existing app error path.

Texture transfer: UiDrawData.CaptureTexturePayloads copies all create/update/destroy payload slices into one pooled byte buffer owned by the frame; no payload means no buffer. Call before source mutation; no retained geometry copy is introduced. ScaleClipRects now also captures independent texture bytes, fixing a scaled-copy lifetime hole when its original owned a captured payload. Record copies do not create ownership. Callback commands require producer completion waiting before the next UI frame; this preserves UI/callback exclusion but not producer-thread identity. This is explicit opt-in API behavior; external callback thread-affinity assumptions are not silently supported. App profiling getDraw includes queue residence and total is generation-to-completion latency, not the sum of serial CPU stages.

Tests: render-queue-overlap-gpu/result.json (.NET10.0.9) and render-queue-gpu-net11/result.json (.NET11.0.0-rc.3.26522.148) pass314 each under VK_LAYER_KHRONOS_validation. Ten queue/texture tests cover backpressure, FIFO draining and ownership release, failure notification/original exception, rejected completed submissions, callback-barrier identification, captured texture slices and scaled-copy independence. Two additional actual Vulkan checks produce retained red/green/blue revisions while the first frame is blocked and verify all three GPU colors plus source identities. The earlier302 checks remain included. render-queue-solution.stdout.log builds the Release solution with0 warnings/errors. Tests compile the production queue source by link rather than introducing a substitute implementation.

NativeAOT: render-queue-native-build stdout confirms SDK11.0.100-rc.3.26522.148 and actual native code generation, reusing the existing binding-candidate build directory. No new baseline binary is created. render-queue-smoke is one6-second motion-tour run with pipelineenabled:1132.96FPS, explicitly INSUFFICIENT_REPETITIONS; it is a smoke observation, not target completion or the final current FPS. A bounded12-run alternating Off/On18-second contract is recorded in render-queue-ab-input-contract.json; raw child logs, accepted-frame CSV and source links are retained. No builds/tests overlap timed children. Performance numbers and risks below will use the completed ledger, not this preliminary run.
Warmup correction before final performance claim: the initial twelve-run render-queue-ab set yielded882.88/864.61FPS and numerical Off/On separation, but direct source inspection found that second-UI-frame-based startup deadlines are execution-dependent. In pipeline mode the producer may create UI2 before the first renderer startup finishes; a deadline measured from that call cannot establish the same post-presentation preparation interval as the serial mode. Therefore raw runs remain unchanged, qualification.json marks INVALID_FOR_FINAL_COMPARISON, derived summary percentages are null/invalid and the target markdown carries an invalid banner. These observations are not the final current FPS. No hardware clock/scheduler cause is inferred.

The corrected sample anchors one second to the first accepted presentation timestamp, records start ticks/frequency/method and fixes warmupLastFrame to the last excluded sequence rather than the first included sequence. Non-idle framesAfterWarmup must now exactly equal recorded sample count. The runner rejects missing/short/early/mismatched warmup and different warmup methods in summaries. Production function audit passes8/8 cases plus a synthetic mixed-method fixture where every improvement percentage is rejected; fixture values are not new measurements. The additional twelve18-second runs are justified by this concrete invalid contract, not by a desired FPS outcome; render-queue-warm-ab-input-contract.json fixes cumulative timed budget24. NativeAOT was rebuilt in the same candidate directory. ERD explicitly enables the engine pipeline by default; the library API stays defaultfalse. The sample's accepted-frame timing and target1000 remain unchanged.
The corrected pre-callback/admission twelve-run set render-queue-warm-ab is complete:822.23 camera and816.01 motion FPS, target82.22%/81.60%. It remains a historical phase after the subsequent correctness/admission source changes; the first invalid UI-anchor set is not substituted. Separate six-second camera diagnostic, one phase/variant, APP_PROFILE every16 and first96 excluded: Off177samples, UI535.638us, renderer724.190us, handoff0.226us,total1260.055us; On257samples, UI575.054us, renderer762.002us,handoff1192.644us,total2529.700us. The On1137.81FPS diagnostic is not repeated target evidence. Increased individual stage times and roughly doubled source-to-completion latency are preserved costs, not hidden behind throughput. Profile total is latency with queue residence, not aggregate CPU time.

The initial queue allowed active+pending+producer construction. Admission now waits for the pending slot before sampling input and building UI, retaining overlap while preventing a third stale CPU frame. Tests cover blocking/resuming this admission. This is a source-backed response to measured latency, not proof of final latency improvement before the new diagnostic. The pre-admission idle probe recorded4 boundary drain frames after the fixed warmup deadline and no further per-frame APP_PROFILE log for4.548seconds, with368UI/accepted frames. Do not claim zero warmup-boundary frames or relabel it final-admission idle proof.

Additional actual Vulkan callback tests exposed two existing defects: render-queue-final-gpu fails because trailing zero-element callbacks were removed; render-queue-callback-fixed-gpu then fails because callback-only retained append was rejected as geometry-empty. Root fixes preserve callbacks during pop/merge/flush, channel flush, span append, static/retained append and layer-content classification. Vulkan now invokes callback commands before geometry/texture classification rather than silently ignoring them. Actual tests cover a trailing hook, adjacent identical hooks, a callback-only frame and a callback-only retained layer replaying twice without rebuilding. Render-queue-admission-gpu and render-queue-admission-gpu-net11 pass322 checks each; render-queue-admission-direct-gpu passes298. Release solution build0 warnings/errors. Prior failed stderr/stdout remain intact.

Final same-executable Off/On measurement includes those hot-path correctness changes and pre-input admission, with12 additional18-second runs. The contract fixes cumulative comparison budget36 and total costful benchmark budget43 (one initial smoke,36 comparison runs,six diagnostic/idle runs). No additional native variant directory was created; provisional native rebuilds reused the same candidate, and only the final admission image is used for final measurement. The original full objective and1000FPS target remain active; no stage diagnostic is promoted to full completion.
Final admission image measurements: render-queue-admission-ab,12/12 terminal runs, same NativeAOT executable, SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, MSAA4,VSyncoff,1200tables,dense/resident. Each variant/scenario has3 independent18-second processes; order alternates. First accepted presentation +1sec warmup; every source row, warmup duration/boundary/count and overwrite state audited. Image SHA25680D0B9D34C255238CD87CABC2EF67ED7FAF8C68510446DAB80EFEBD574908C5E.

| Scenario | Metric | Serial median | Pipeline median | Change % | Qualification |
| --- | --- | ---: | ---: | ---: | --- |
| camera | averageFps | 667.9089 | 908.6058 | 36.04 | disjoint-run-ranges |
| camera | medianMs | 1.2399 | 0.7992 | -35.54 | disjoint-run-ranges |
| camera | p95Ms | 2.4746 | 2.4189 | unproven | INVALID: overlapping run ranges |
| camera | p99Ms | 3.0600 | 2.8186 | unproven | INVALID: overlapping run ranges |
| camera | low1PctFps | 243.5940 | 279.9506 | 14.93 | disjoint-run-ranges |
| camera | sceneAllocatedBytesPerFrame | 736.0000 | 736.0000 | 0.00 | UNCHANGED |
| camera | rendererAllocatedBytesPerFrame | 265.8570 | 265.1876 | -0.25 | disjoint-run-ranges |
| camera | frameUploadBytes | 6335.5071 | 5935.8929 | -6.31 | disjoint-run-ranges |
| camera | layerStateUploadBytes | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | compiledIndexUploadBytes | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | compiledLayerDraws | 73.7754 | 68.8241 | -6.71 | disjoint-run-ranges |
| camera | sharedBasisLayerDraws | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | sharedCameraLayerDraws | 73.7754 | 68.8241 | -6.71 | disjoint-run-ranges |
| camera | compiledLayerCommands | 2255.4559 | 1683.4184 | -25.36 | disjoint-run-ranges |
| camera | compiledDescriptorBuilds | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | compiledCommandVisits | 4104.8804 | 4336.9103 | 5.65 | disjoint-run-ranges |
| camera | layerBoundsCommandVisits | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | drawCalls | 102.7754 | 97.8241 | -4.82 | disjoint-run-ranges |
| camera | gpuMicroseconds | 650.1851 | 525.3258 | -19.20 | disjoint-run-ranges |
| motion-tour | averageFps | 666.7099 | 957.2954 | 43.58 | disjoint-run-ranges |
| motion-tour | medianMs | 1.2980 | 0.8133 | -37.34 | disjoint-run-ranges |
| motion-tour | p95Ms | 2.4423 | 2.2780 | -6.73 | disjoint-run-ranges |
| motion-tour | p99Ms | 2.9986 | 2.6011 | -13.26 | disjoint-run-ranges |
| motion-tour | low1PctFps | 259.4758 | 350.5908 | 35.12 | disjoint-run-ranges |
| motion-tour | sceneAllocatedBytesPerFrame | 3592.0000 | 3592.0000 | 0.00 | UNCHANGED |
| motion-tour | rendererAllocatedBytesPerFrame | 601.5221 | 590.9266 | -1.76 | disjoint-run-ranges |
| motion-tour | frameUploadBytes | 7482.9328 | 7007.8542 | -6.35 | disjoint-run-ranges |
| motion-tour | layerStateUploadBytes | 497.1547 | 489.6989 | -1.50 | disjoint-run-ranges |
| motion-tour | compiledIndexUploadBytes | 0.0000 | 0.0000 | unproven | UNCHANGED |
| motion-tour | compiledLayerDraws | 62.8761 | 52.8188 | -16.00 | disjoint-run-ranges |
| motion-tour | sharedBasisLayerDraws | 0.0000 | 0.0000 | unproven | UNCHANGED |
| motion-tour | sharedCameraLayerDraws | 62.8761 | 52.8188 | -16.00 | disjoint-run-ranges |
| motion-tour | compiledLayerCommands | 2017.6835 | 1498.2066 | -25.75 | disjoint-run-ranges |
| motion-tour | compiledDescriptorBuilds | 7.3062 | 7.1985 | -1.47 | disjoint-run-ranges |
| motion-tour | compiledCommandVisits | 3122.7152 | 3054.1877 | -2.19 | disjoint-run-ranges |
| motion-tour | layerBoundsCommandVisits | 9.0000 | 9.0000 | 0.00 | UNCHANGED |
| motion-tour | drawCalls | 91.8761 | 81.8188 | -10.95 | disjoint-run-ranges |
| motion-tour | gpuMicroseconds | 568.1572 | 436.2405 | -23.22 | disjoint-run-ranges |

Current FPS908.61/957.30, target1000 attainment90.86%/95.73%, NOT_MET. Camera run range893.10–980.47, motion953.48–960.71; highest repeated-set run-average980.47. FPS gain36.04%/43.58% has disjoint ranges. Camera p95/p99 ranges overlap and percentage claims are invalid. All raw lower runs remain included. Scene builds0/1 per frame and source-link gaps/overwrites0 across12runs. Per-frame draw/upload/GPU means are frame-count weighted along the camera trajectory, not proof of reduced GPU work at an identical view. No shader/geometry quality was reduced.
Final diagnostics are budgeted separately: render-queue-admission-diagnostics, six-second phases, one each Off/On, APP_PROFILE every16, first96frames excluded. Off173samples/UI529.492us/handoff0.243us/renderer707.873us/total1237.608us; On253samples/UI596.095us/handoff304.336us/renderer777.577us/total1678.008us. Pipeline throughput gain does not eliminate its latency cost or increased individual stage time. Earlier2.530ms was a different image/phase; do not claim isolated admission speedup percentages. Diagnostic1114.79FPS remains insufficient repetitions/different camera duration and is excluded from current attainment.
Final idle probe:404UI/accepted frames,3boundary drain frames after first-present+1sec, then4.0836seconds with no further every-frame log. This is not zero frames at the warmup boundary and not a repeated idle CPU/memory audit. Production admission precedes input sampling; startup/resize notifications remain tied to accepted presentation and geometry-empty queued clear/present is tested. No background builds/tests overlap timed or diagnostic children.
Default EXE/PDB promoted only after all handles are terminal and process audit is empty; readback SHA matches all12final timed runs. Receipt: render-queue-admission-native-promotion.json. Previous default9EA8D1F4 image is historical by hash and no longer lives at the overwritten default path. No manual interaction claim for this image. Remaining work includes1000FPS, latest manual/UI/resize interaction, comprehensive memory/runtime/full-objective audit and remaining event-loop/renderer costs. The goal stays active.

## Event-driven platform owner and explicit wake (2026-11-03)

Hypothesis: the active app main loop repeatedly PollEvents/UpdateSnapshot/SetMouseCursor while UI and GPU submission already run independently. Remove that busy work using an explicit wake capability, preserving input/resize/exit semantics rather than inserting an arbitrary sleep. Authoritative API: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-msgwaitformultipleobjectsex (consulted current Microsoft Learn docs). Handles must remain alive during wait, MWMO_INPUTAVAILABLE includes previously observed unread input, events and input share one wait, and failed/unknown results must not be silently accepted.

Implementation: optional IPlatformEventWakeBackend capability + DuxelFrameOptions.UseEventDrivenPlatformLoop, with unsupported-mode fail-fast before platform state/GPU initialization. Windows waits on a latched AutoResetEvent plus native input, retaining the SafeHandle across the unmanaged call. Nonpositive WaitEvents timeouts retain infinite-wait semantics with explicit wake, replacing the old WaitMessage path. Session.Exit signals both UI and platform waits and cancels interactive resize acknowledgement waits. Cursor changes and render/UI failures signal the platform. RequestFrame/Exit handle access and callback detachment are serialized with lifecycle cleanup, avoiding stale disposed-event references. Ordinary frame requests do not generate platform wake traffic. First-frame and resize presentation acceptance semantics are preserved; live modal resize interaction of this new image is still a separate verification item.

Tests: event-loop-owned-gpu and event-loop-gpu-net11 pass332 each, including latched wake across polling, worker wake, previously observed unread targeted mouse message, unsupported-backend fail-fast and actual App sessions proving zero-geometry first presentation, on-demand rendering while owner waits, cursor delivery, explicit Exit and renderer callback error propagation without watchdog intervention. No global mouse/keyboard motion is synthesized. App assembly is referenced directly instead of linking a queue substitute, with test-only friend visibility. The first event-loop-gpu attempt failed because consecutive application windows shared a thread queue: direct inspection confirms WM_DESTROY posts thread WM_QUIT. Actual application tests now own separate native window threads; failed logs remain. Test-only watchdog cleanup fails the case and is not production fallback.

One direct test execution overlapped the solution build after the tool returned a still-running handle; that execution is not the authoritative direct proof. After both handles terminated, event-loop-direct-verified-gpu was rerun without overlap and passed308. event-loop-solution build has0 warnings/errors. No performance children overlap builds/tests. The native image reuses the existing candidate directory and build logs record actual native code generation with SDK11.0.100-rc.3.26522.148.

Measurement adds process CPU time over a monotonic application-run interval (including setup/warmup/shutdown), average logical-core equivalents and process-lifetime peak working set. They are not warm-frame-only CPU time or GPU VRAM. Production process-metric validator, summary CPU/memory extraction and conflicting-mode preflight pass11/11 audit cases. Synthetic-summary numbers are explicit fixtures, not measured performance. The event-loop-ab contract fixes12 independent18-second alternating same-executable runs plus at most4 six-second diagnostic phases, cumulative costful benchmark budget59. It preserves first-accepted-presentation+1sec warmup and every accepted/source-frame link.
Controlled event-loop phase completed12/12: same image SHA2563E77130945220D5721A5D2354D96D1D99074D5CCC31732C1219E370AD97DF4AD, each variant/scenario3independent18sec runs, first-accepted-present+1sec warmup. Lower FPS runs are retained; no source gaps or history overwrites. All comparison metrics follow.

| Scenario | Metric | Polling median | Event-wait median | Change % | Qualification |
| --- | --- | ---: | ---: | ---: | --- |
| camera | averageFps | 889.6628 | 917.2091 | unproven | INVALID: overlapping run ranges |
| camera | medianMs | 0.8656 | 0.8199 | unproven | INVALID: overlapping run ranges |
| camera | p95Ms | 2.4076 | 2.3768 | unproven | INVALID: overlapping run ranges |
| camera | p99Ms | 2.7842 | 2.7011 | unproven | INVALID: overlapping run ranges |
| camera | low1PctFps | 280.7370 | 288.7722 | unproven | INVALID: overlapping run ranges |
| camera | sceneAllocatedBytesPerFrame | 736.0000 | 736.0000 | 0.00 | UNCHANGED |
| camera | rendererAllocatedBytesPerFrame | 265.2008 | 265.1652 | unproven | INVALID: overlapping run ranges |
| camera | frameUploadBytes | 5963.5332 | 5925.6379 | unproven | INVALID: overlapping run ranges |
| camera | layerStateUploadBytes | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | compiledIndexUploadBytes | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | compiledLayerDraws | 70.0935 | 69.7102 | unproven | INVALID: overlapping run ranges |
| camera | sharedBasisLayerDraws | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | sharedCameraLayerDraws | 70.0935 | 69.7102 | unproven | INVALID: overlapping run ranges |
| camera | compiledLayerCommands | 1812.9557 | 1771.6646 | unproven | INVALID: overlapping run ranges |
| camera | compiledDescriptorBuilds | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | compiledCommandVisits | 4295.9072 | 4302.6426 | unproven | INVALID: overlapping run ranges |
| camera | layerBoundsCommandVisits | 0.0000 | 0.0000 | unproven | UNCHANGED |
| camera | drawCalls | 99.0935 | 98.7102 | unproven | INVALID: overlapping run ranges |
| camera | gpuMicroseconds | 530.4791 | 512.1869 | unproven | INVALID: overlapping run ranges |
| camera | processCpuMilliseconds | 41484.3750 | 24015.6250 | -42.11 | disjoint-run-ranges |
| camera | processWallMilliseconds | 18975.6761 | 18969.0635 | unproven | INVALID: overlapping run ranges |
| camera | averageBusyCpuCores | 2.1862 | 1.2660 | -42.09 | disjoint-run-ranges |
| camera | processPeakWorkingSetBytes | 1734959104.0000 | 1734332416.0000 | unproven | INVALID: overlapping run ranges |
| motion-tour | averageFps | 870.7552 | 885.5681 | unproven | INVALID: overlapping run ranges |
| motion-tour | medianMs | 0.9306 | 0.9176 | unproven | INVALID: overlapping run ranges |
| motion-tour | p95Ms | 2.3564 | 2.3071 | unproven | INVALID: overlapping run ranges |
| motion-tour | p99Ms | 2.6914 | 2.6510 | unproven | INVALID: overlapping run ranges |
| motion-tour | low1PctFps | 343.7567 | 338.3228 | unproven | INVALID: overlapping run ranges |
| motion-tour | sceneAllocatedBytesPerFrame | 3592.0000 | 3592.0000 | 0.00 | UNCHANGED |
| motion-tour | rendererAllocatedBytesPerFrame | 592.1546 | 591.6125 | unproven | INVALID: overlapping run ranges |
| motion-tour | frameUploadBytes | 7114.9287 | 7087.2255 | unproven | INVALID: overlapping run ranges |
| motion-tour | layerStateUploadBytes | 488.3523 | 486.0470 | unproven | INVALID: overlapping run ranges |
| motion-tour | compiledIndexUploadBytes | 0.0000 | 0.0000 | unproven | UNCHANGED |
| motion-tour | compiledLayerDraws | 54.7777 | 54.3072 | unproven | INVALID: overlapping run ranges |
| motion-tour | sharedBasisLayerDraws | 0.0000 | 0.0000 | unproven | UNCHANGED |
| motion-tour | sharedCameraLayerDraws | 54.7777 | 54.3072 | unproven | INVALID: overlapping run ranges |
| motion-tour | compiledLayerCommands | 1595.5324 | 1569.5327 | unproven | INVALID: overlapping run ranges |
| motion-tour | compiledDescriptorBuilds | 7.1784 | 7.1422 | unproven | INVALID: overlapping run ranges |
| motion-tour | compiledCommandVisits | 3024.5591 | 3055.0583 | unproven | INVALID: overlapping run ranges |
| motion-tour | layerBoundsCommandVisits | 9.0000 | 9.0000 | 0.00 | UNCHANGED |
| motion-tour | drawCalls | 83.7777 | 83.3072 | unproven | INVALID: overlapping run ranges |
| motion-tour | gpuMicroseconds | 469.2692 | 461.9751 | unproven | INVALID: overlapping run ranges |
| motion-tour | processCpuMilliseconds | 41656.2500 | 25031.2500 | -39.91 | disjoint-run-ranges |
| motion-tour | processWallMilliseconds | 18957.3060 | 18981.4684 | 0.13 | disjoint-run-ranges |
| motion-tour | averageBusyCpuCores | 2.1970 | 1.3124 | -40.27 | disjoint-run-ranges |
| motion-tour | processPeakWorkingSetBytes | 1734942720.0000 | 1735860224.0000 | unproven | INVALID: overlapping run ranges |

CPU logical-core equivalents decrease2.186187 ->1.266042(camera42.09%) and2.197020 ->1.312389(motion40.27%), disjoint ranges. FPS889.66 ->917.21 and870.76 ->885.57 plus frame-time tails have overlapping ranges; no FPS improvement/regression percentage is established. Peak working-set ranges overlap at about1.62GiB; no memory reduction or GPU VRAM claim. This controlled image was superseded by the following sample-only fix and is historical by hash.
Diagnostics: event-loop-diagnostics camera Off/On each6sec, APP and Vulkan logs every16, first96frames/six Vulkan rows excluded;257/262 aligned samples each. Sampled Vulkan means(us): target196.882/185.184,begin50.820/55.312,frameFence48.078/48.963,upload159.904/161.705,record263.547/268.159,submit22.672/23.035,present76.630/76.193,GPU257.343/256.122. App renderer1385.995/1355.088us and total2269.060/2232.702us include detailed Vulkan log construction/output because sample intervals coincide. They are logged-frame diagnostics, not representative ordinary-frame latencies and not comparable with earlier APP-only intervals. Diagnostic1128.65FPS is insufficient repetition/different duration and excluded from target attainment. Next source inspection should separate repeated preparation, geometry binding/recording and detailed timing work; this is not a proven isolated bottleneck or an implemented optimization.
Idle Off/On single6sec probes:409/412UI and accepted frames,3boundary frames each; then no per-frame APP log for4.1768/4.1536sec. Whole-run CPU equivalents0.7905/0.3563 include startup and cleanup, not steady-state idle-only utilization. No claim of zero boundary frames or complete memory/endurance audit.
Late source inspection found ordinary no-output use still accumulated fixed1,048,576-frame benchmark arrays. Its next source-ID write could overrun capacity before the recorder error. Fix: nullable readonly recorder/IDs allocated only for explicit output; live presentation drain/FPS continue without sample retention, avoiding24MiB buffer payload allocation. Explicit measurement records before its source-ID write so capacity failure is descriptive and does not access an invalid index. Source invariant proves no-output accumulation is absent; event-loop-live-smoke is6sec actual NativeAOT with output unset and continuing render progress, not a million-frame endurance test. The new input contract adds exactly one smoke and six18sec current-only runs, cumulative budget66, because deployed default use must not retain bounded benchmark data indefinitely.
Latest current-only image SHA25670D4F7827ABAE6A8C8DB9876EDE6867532891A8B37680BDEB5374765ABA956C0: event-loop-live-current6/6, camera883.4064FPS(range837.4476–966.2380),motion980.4466(range808.3110–988.8863), target1000 attainment88.34%/98.04%,NOT_MET. Highest repeated-set average988.8863. Lower values remain included. No isolated speedup claim between this sample fix and previous measurements. The controlled CPU comparison remains a separate same-engine phase. Every final row passes warmup/process arithmetic/source-link audits; zero source gaps and overwrites, camera geometry bodies0/frame and motion1/frame.
Default EXE/PDB was replaced after all handles terminated and task process audit was empty; readback matches the final six-run image. event-loop-native-promotion.json preserves previous80D0B9D3 hash and explains phase scope. No previous denied cleanup was retried. Remaining full-goal work includes1000FPS, comprehensive latest .NET10/11 Managed/NativeAOT comparison, actual full-window/manual interaction, CPU/GPU memory/resource audit and normal-frame latency. GPU332checks each NET10/11 and direct308 remain authoritative for unchanged engine code; the sample-only fix has native build and separate smoke/current measurement proof. Goal remains active.

| Latest current scenario | Metric | Median of3runs | Run minimum | Run maximum |
| --- | --- | ---: | ---: | ---: |
| camera | averageFps | 883.4064 | 837.4476 | 966.2380 |
| camera | medianMs | 0.8501 | 0.7529 | 0.9912 |
| camera | p95Ms | 2.3794 | 2.3424 | 2.4165 |
| camera | p99Ms | 2.6223 | 2.6219 | 2.8094 |
| camera | low1PctFps | 290.3022 | 279.0406 | 291.2354 |
| camera | sceneAllocatedBytesPerFrame | 736.0000 | 736.0000 | 736.0000 |
| camera | rendererAllocatedBytesPerFrame | 265.2224 | 265.1010 | 265.3013 |
| camera | frameUploadBytes | 5964.7324 | 5864.4785 | 6018.8989 |
| camera | layerStateUploadBytes | 0.0000 | 0.0000 | 0.0000 |
| camera | compiledIndexUploadBytes | 0.0000 | 0.0000 | 0.0000 |
| camera | compiledLayerDraws | 70.7508 | 69.5314 | 71.5235 |
| camera | sharedBasisLayerDraws | 0.0000 | 0.0000 | 0.0000 |
| camera | sharedCameraLayerDraws | 70.7508 | 69.5314 | 71.5235 |
| camera | compiledLayerCommands | 1822.8392 | 1712.9624 | 1947.7070 |
| camera | compiledDescriptorBuilds | 0.0000 | 0.0000 | 0.0000 |
| camera | compiledCommandVisits | 4293.6688 | 4246.4068 | 4328.8547 |
| camera | layerBoundsCommandVisits | 0.0000 | 0.0000 | 0.0000 |
| camera | drawCalls | 99.7508 | 98.5314 | 100.5235 |
| camera | gpuMicroseconds | 532.3136 | 494.9764 | 560.2406 |
| camera | processCpuMilliseconds | 24796.8750 | 23281.2500 | 25453.1250 |
| camera | processWallMilliseconds | 18964.0244 | 18955.5513 | 19010.8320 |
| camera | averageBusyCpuCores | 1.3076 | 1.2246 | 1.3428 |
| camera | processPeakWorkingSetBytes | 1734258688.0000 | 1734193152.0000 | 1736454144.0000 |
| motion-tour | averageFps | 980.4466 | 808.3110 | 988.8863 |
| motion-tour | medianMs | 0.7870 | 0.7826 | 1.0451 |
| motion-tour | p95Ms | 2.2320 | 2.2302 | 2.4160 |
| motion-tour | p99Ms | 2.5531 | 2.5403 | 2.7427 |
| motion-tour | low1PctFps | 350.1508 | 321.3587 | 350.2750 |
| motion-tour | sceneAllocatedBytesPerFrame | 3592.0000 | 3592.0000 | 3592.0000 |
| motion-tour | rendererAllocatedBytesPerFrame | 590.7487 | 590.6948 | 591.7608 |
| motion-tour | frameUploadBytes | 6974.7675 | 6970.8229 | 7198.1582 |
| motion-tour | layerStateUploadBytes | 486.1185 | 484.8405 | 490.4224 |
| motion-tour | compiledIndexUploadBytes | 0.0000 | 0.0000 | 0.0000 |
| motion-tour | compiledLayerDraws | 52.6206 | 52.5999 | 58.1179 |
| motion-tour | sharedBasisLayerDraws | 0.0000 | 0.0000 | 0.0000 |
| motion-tour | sharedCameraLayerDraws | 52.6206 | 52.5999 | 58.1179 |
| motion-tour | compiledLayerCommands | 1490.0366 | 1488.5187 | 1701.8770 |
| motion-tour | compiledDescriptorBuilds | 7.1441 | 7.1225 | 7.2100 |
| motion-tour | compiledCommandVisits | 3032.6754 | 3026.4952 | 3223.0337 |
| motion-tour | layerBoundsCommandVisits | 9.0000 | 9.0000 | 9.0000 |
| motion-tour | drawCalls | 81.6206 | 81.5999 | 87.1179 |
| motion-tour | gpuMicroseconds | 425.8702 | 423.9252 | 500.1386 |
| motion-tour | processCpuMilliseconds | 24765.6250 | 24718.7500 | 24890.6250 |
| motion-tour | processWallMilliseconds | 18963.2561 | 18963.0018 | 19081.8880 |
| motion-tour | averageBusyCpuCores | 1.3060 | 1.2954 | 1.3126 |
| motion-tour | processPeakWorkingSetBytes | 1736396800.0000 | 1736327168.0000 | 1736966144.0000 |

Next concrete source question, inspected after measurements: RecordDrawCallProfile always takes Stopwatch timestamps when profiling is enabled, while LogProfileFrame increments a separate frame counter and returns for all non-log intervals. The demo uses interval1,000,000 during target measurements. DrawCallCount must remain available every frame, but private detailed CPU durations appear to be log consumers. Separate required counters/GPU timing from private CPU timing only after checking all consumers; do not disable accepted-presentation sampling, uploads/draw counts, GPU timestamps or quality to inflate FPS. This is an identified source path, not yet a verified timing optimization or a claim that it explains all remaining cost.
## Private CPU timing sampled on log frames (2026-11-03)

Hypothesis/source evidence: detailed CPU durations are accumulated on every profiled frame, but their only consumers are the profile log after its interval check. Public draw/upload/residency/allocation/GPU/presentation metrics need every-frame data; private duration fields do not. Separate them without reducing rendering work, geometry quality, GPU timestamps or frame accounting.

Implementation: default-enabled DUXEL_VK_PROFILE_TIMING_SAMPLING selects detailed CPU timing only when the next completed profile-frame counter reaches DUXEL_VK_PROFILE_EVERY.0 keeps legacy every-frame timing. One instance start helper gates the clock, and the elapsed helper handles0-start without another timestamp or huge fake duration. All CPU timestamp sites in frame wait/upload/resource transition/recording/state/draw/texture lookup/scheduler route through this clock. The independent accepted-presentation timestamp is untouched. Existing counter guards remain based on profiling enablement, not CPU timing sampling. UiRendererFrameMetrics.CpuTimingCollected and ERD cpuTimingCollectedFrames/gpuTimingSamples expose actual behavior. Boolean configuration is strict, and no shader/quality options were altered.

GPU tests cpu-timing-gpu and cpu-timing-gpu-net11 pass361 each, direct cpu-timing-direct-gpu337; Release cpu-timing-solution build0 warnings/errors.29 new checks cover default/legacy/invalid mode, six real GPU frames per mode at log interval2, the actual start/elapsed helper including empty-start handling, byte-identical pixels across all frames/modes, every-frame draws/uploads/presentation/source identity and independent GPU timestamps. Existing332/308 rendering/event/lifetime tests remain included. Clock-source audit finds only the gated start/elapsed clock and the independent accepted-present timestamp as Stopwatch.GetTimestamp sites in renderer code.

Runner cpu-timing-runner-audit passes10/10 for valid modes, missing timing counts, missing GPU samples, wrong mode, lost draw count, crossing the short-run log interval, disabled pipeline and conflicting/idle preflight rejection before directory/child creation. NativeAOT uses the existing candidate directory, SDK11.0.100-rc.3.26522.148, with actual native-code-generation logs. cpu-timing-ab-input-contract fixes12 independent18-second alternating same-image runs and at most2 six-second diagnostics, cumulative benchmark budget80. Both modes retain profiling counters, GPU timestamps, pipeline/event loop, first-accepted-present+1sec warmup and source-link auditing. No builds/tests overlap performance children. Quantitative results follow only after the full ledger is terminal.
Latest FPS record,2026-11-03: .NET11 RC NativeAOT,1200 tables,dense/resident,MSAA4,VSync off,one-second warmup after first accepted presentation,three independent18-second runs per scenario. Target1000 FPS. Camera median946.46 FPS(94.65%,range903.91–960.34); camera plus entity motion978.39 FPS(97.84%,range936.18–1000.71). The best single-run average1000.71 FPS is distinct from repeated medians; neither scenario meets the target across runs. The live panel displays FPS/1000. Raw records are in artifacts/perf-2026-11-03/cpu-timing-ab; fps-current.json preserves the latest machine-readable score. All12 presentation-link/warmup/timing/process audits pass. Detailed CPU durations default to log-frame sampling, while per-frame counters,GPU timestamps and presentation clocks remain intact. On/Off FPS run ranges overlap, so no improvement percentage is established.

Full Before/After medians; overlapping ranges invalidate quantitative speedup claims:

| Scenario | Metric | Before | After | Status |
| --- | --- | ---: | ---: | --- |
| camera | averageFps | 950.937455030737 | 946.462083309188 | INVALID: overlapping run ranges |
| camera | medianMs | 0.7638 | 0.7756 | INVALID: overlapping run ranges |
| camera | p95Ms | 2.3605 | 2.36698 | INVALID: overlapping run ranges |
| camera | p99Ms | 2.697395 | 2.653556 | INVALID: overlapping run ranges |
| camera | low1PctFps | 288.010702242585 | 290.67477410568 | INVALID: overlapping run ranges |
| camera | sceneAllocatedBytesPerFrame | 736 | 736 | UNCHANGED |
| camera | rendererAllocatedBytesPerFrame | 265.119036077834 | 265.139187491275 | INVALID: overlapping run ranges |
| camera | frameUploadBytes | 5881.68575583408 | 5890.24040206617 | INVALID: overlapping run ranges |
| camera | layerStateUploadBytes | 0 | 0 | UNCHANGED |
| camera | compiledIndexUploadBytes | 0 | 0 | UNCHANGED |
| camera | compiledLayerDraws | 70.1497126237795 | 70.0056191324608 | INVALID: overlapping run ranges |
| camera | sharedBasisLayerDraws | 0 | 0 | UNCHANGED |
| camera | sharedCameraLayerDraws | 70.1497126237795 | 70.0056191324608 | INVALID: overlapping run ranges |
| camera | compiledLayerCommands | 1749.04383352953 | 1726.51230041801 | INVALID: overlapping run ranges |
| camera | compiledDescriptorBuilds | 0 | 0 | UNCHANGED |
| camera | compiledCommandVisits | 4320.78277127623 | 4325.5758760296 | INVALID: overlapping run ranges |
| camera | layerBoundsCommandVisits | 0 | 0 | UNCHANGED |
| camera | drawCalls | 99.1497126237795 | 99.0056191324608 | INVALID: overlapping run ranges |
| camera | gpuMicroseconds | 503.22692334326 | 504.445429010191 | INVALID: overlapping run ranges |
| camera | processCpuMilliseconds | 23921.875 | 23359.375 | INVALID: overlapping run ranges |
| camera | processWallMilliseconds | 18953.9444 | 19010.3465 | INVALID: overlapping run ranges |
| camera | averageBusyCpuCores | 1.26274718456766 | 1.23006846039442 | INVALID: overlapping run ranges |
| camera | processPeakWorkingSetBytes | 1735282688 | 1734012928 | INVALID: overlapping run ranges |
| motion-tour | averageFps | 944.615557188746 | 978.389768076217 | INVALID: overlapping run ranges |
| motion-tour | medianMs | 0.8173 | 0.78395 | INVALID: overlapping run ranges |
| motion-tour | p95Ms | 2.243305 | 2.22768 | INVALID: overlapping run ranges |
| motion-tour | p99Ms | 2.546414 | 2.528797 | INVALID: overlapping run ranges |
| motion-tour | low1PctFps | 352.299729192761 | 357.260088461434 | INVALID: overlapping run ranges |
| motion-tour | sceneAllocatedBytesPerFrame | 3592 | 3592 | UNCHANGED |
| motion-tour | rendererAllocatedBytesPerFrame | 591.021980322378 | 590.780164842589 | INVALID: overlapping run ranges |
| motion-tour | frameUploadBytes | 7015.5349940688 | 6985.08850155384 | INVALID: overlapping run ranges |
| motion-tour | layerStateUploadBytes | 485.669388039913 | 486.73332861891 | INVALID: overlapping run ranges |
| motion-tour | compiledIndexUploadBytes | 0 | 0 | UNCHANGED |
| motion-tour | compiledLayerDraws | 52.9533877607983 | 52.8373868396163 | INVALID: overlapping run ranges |
| motion-tour | sharedBasisLayerDraws | 0 | 0 | UNCHANGED |
| motion-tour | sharedCameraLayerDraws | 52.9533877607983 | 52.8373868396163 | INVALID: overlapping run ranges |
| motion-tour | compiledLayerCommands | 1501.74795897006 | 1505.74138629915 | INVALID: overlapping run ranges |
| motion-tour | compiledDescriptorBuilds | 7.13781313237039 | 7.15472738844495 | INVALID: overlapping run ranges |
| motion-tour | compiledCommandVisits | 3070.00955969576 | 3006.26827455749 | disjoint-run-ranges |
| motion-tour | layerBoundsCommandVisits | 9 | 9 | UNCHANGED |
| motion-tour | drawCalls | 81.9533877607983 | 81.8373868396163 | INVALID: overlapping run ranges |
| motion-tour | gpuMicroseconds | 438.550206405693 | 426.509457640859 | INVALID: overlapping run ranges |
| motion-tour | processCpuMilliseconds | 24171.875 | 24859.375 | INVALID: overlapping run ranges |
| motion-tour | processWallMilliseconds | 18984.4397 | 18976.5224 | INVALID: overlapping run ranges |
| motion-tour | averageBusyCpuCores | 1.27475576015835 | 1.31030855991287 | INVALID: overlapping run ranges |
| motion-tour | processPeakWorkingSetBytes | 1736318976 | 1734361088 | INVALID: overlapping run ranges |

## Transient builder reservation ownership (2026-11-03)

Source finding: UiContext.Render summed all submitted geometry, including borrowed retained layer lists, and used the sum plus25% as the next base builder reservation. Those resident vertices/indices are not copied into that builder. For the prior1200-table camera diagnostic the submitted totals are22,651,552 vertices/67,481,268 indices, whereas transient UI generates a small owned chunk. This is an allocation-capacity defect, not proof of a measured working-set reduction.

Change: preserve complete public draw-data totals, but derive automatic builder reservations from the largest owned draw-list chunk only, plus the existing25% margin/minimum. Borrowed geometry keeps its own storage and lifetime. The public explicit Reserve API and renderer geometry are unchanged. New real Vulkan regression draws2000 retained triangles plus one dynamic triangle, verifies both first-frame and replay totals/reservations and byte-identical readback. NET10/NET11 each366 checks pass; Release solution0 warnings/errors. Native performance comparison remains pending; do not associate previous FPS with this new source change.

Before making this change, cpu-timing-app-diagnostics completed the two remaining six-second diagnostic slots of the80-run budget. APP interval17/Vulkan log interval1000000 avoids the earlier coinciding log frames. Filtering APP frame IDs at the first measured source ID gives baseline200samples: UI600.16us,handoff291.73us,renderer741.22us,total1633.11us; sampled178samples: UI677.54us,handoff311.60us,renderer831.40us,total1820.54us. These are single-run diagnostics, not improvement percentages, accepted-presentation FPS evidence or hardware display latency. Overlap remains useful for throughput while generation-to-completion delay remains. The measured current FPS stays946.46/978.39 and best single-run average1000.71 on image1C5109F until a newer comparison is terminal.

2026-11-03 completed transient-reservation comparison: same .NET11 RC NativeAOT/1200tables/dense/resident/MSAA4/VSync off,three independent18-second runs per variant/scenario in alternating order. Camera874.66→921.29 FPS(+5.33%);motion880.82→932.94 FPS(+5.92%); run ranges are disjoint. Median process peak working set decreases about0.25%/0.32%,but remains about1.6GiB and is not GPU VRAM. p95/p99/1% low ranges overlap; no latency improvement percentage is established. Current FPS921.29/932.94 means92.13%/93.29% of target1000; current image best single-run average999.52. Historical image record1000.71 remains separate. Presentation/warmup/process/timing/geometry audit passes12/12; scene builds0/frame(camera),1/frame(motion). See artifacts/perf-2026-11-03/reservation-ab and fps-current.json. Default NativeAOT EXE/PDB promotion matches measured imageAC3D25FB by hash readback.

| Scenario | Metric | Before | After | Change% | Status |
| --- | --- | ---: | ---: | ---: | --- |
| camera | averageFps | 874.662389061787 | 921.287171681462 | 5.33060335081825 | disjoint-run-ranges |
| camera | medianMs | 0.891 | 0.8697 |  | INVALID: overlapping run ranges |
| camera | p95Ms | 2.41114 | 2.43426 |  | INVALID: overlapping run ranges |
| camera | p99Ms | 2.728125 | 2.79001 |  | INVALID: overlapping run ranges |
| camera | low1PctFps | 283.99358487562 | 281.496378448557 |  | INVALID: overlapping run ranges |
| camera | sceneAllocatedBytesPerFrame | 736 | 736 |  | INVALID: overlapping run ranges |
| camera | rendererAllocatedBytesPerFrame | 265.224266382849 | 265.175929915266 | -0.018224753052265 | disjoint-run-ranges |
| camera | frameUploadBytes | 5969.29496402878 | 5921.09607927617 | -0.807446860023686 | disjoint-run-ranges |
| camera | layerStateUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledLayerDraws | 70.3197697342481 | 68.9813037249284 | -1.90339930630893 | disjoint-run-ranges |
| camera | sharedBasisLayerDraws | 0 | 0 |  | UNCHANGED |
| camera | sharedCameraLayerDraws | 70.3197697342481 | 68.9813037249284 | -1.90339930630893 | disjoint-run-ranges |
| camera | compiledLayerCommands | 1836.07465409761 | 1719.71489971347 | -6.33741956649001 | disjoint-run-ranges |
| camera | compiledDescriptorBuilds | 0 | 0 |  | UNCHANGED |
| camera | compiledCommandVisits | 4280.78166337236 | 4326.20014326648 | 1.06098566723765 | disjoint-run-ranges |
| camera | layerBoundsCommandVisits | 0 | 0 |  | UNCHANGED |
| camera | drawCalls | 99.3197697342481 | 97.9813037249284 | -1.34763301697243 | disjoint-run-ranges |
| camera | gpuMicroseconds | 536.690644366732 | 519.602968835272 | -3.18389666576414 | disjoint-run-ranges |
| camera | processCpuMilliseconds | 24703.125 | 24828.125 |  | INVALID: overlapping run ranges |
| camera | processWallMilliseconds | 19032.4004 | 19010.6771 |  | INVALID: overlapping run ranges |
| camera | averageBusyCpuCores | 1.29509917042091 | 1.30730511501613 |  | INVALID: overlapping run ranges |
| camera | processPeakWorkingSetBytes | 1735057408 | 1730781184 | -0.246460087158107 | disjoint-run-ranges |
| motion-tour | averageFps | 880.823274831353 | 932.937452464081 | 5.91653049162508 | disjoint-run-ranges |
| motion-tour | medianMs | 0.9317 | 0.8551 |  | INVALID: overlapping run ranges |
| motion-tour | p95Ms | 2.2847 | 2.2886 |  | INVALID: overlapping run ranges |
| motion-tour | p99Ms | 2.617632 | 2.58075 |  | INVALID: overlapping run ranges |
| motion-tour | low1PctFps | 347.62207047337 | 354.489559262366 |  | INVALID: overlapping run ranges |
| motion-tour | sceneAllocatedBytesPerFrame | 3592 | 3592 |  | INVALID: overlapping run ranges |
| motion-tour | rendererAllocatedBytesPerFrame | 591.556656904708 | 591.734506889835 |  | INVALID: overlapping run ranges |
| motion-tour | frameUploadBytes | 7094.91567169783 | 7032.59491757253 | -0.878386114917518 | disjoint-run-ranges |
| motion-tour | layerStateUploadBytes | 486.985784740354 | 486.876394787992 |  | INVALID: overlapping run ranges |
| motion-tour | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| motion-tour | compiledLayerDraws | 54.7466396335511 | 52.9774674249514 |  | INVALID: overlapping run ranges |
| motion-tour | sharedBasisLayerDraws | 0 | 0 |  | UNCHANGED |
| motion-tour | sharedCameraLayerDraws | 54.7466396335511 | 52.9774674249514 |  | INVALID: overlapping run ranges |
| motion-tour | compiledLayerCommands | 1577.53372497824 | 1532.5237203945 |  | INVALID: overlapping run ranges |
| motion-tour | compiledDescriptorBuilds | 7.15665796344648 | 7.15456050680297 |  | INVALID: overlapping run ranges |
| motion-tour | compiledCommandVisits | 3042.86453405422 | 3034.53552659996 |  | INVALID: overlapping run ranges |
| motion-tour | layerBoundsCommandVisits | 9 | 9 | 0 | UNCHANGED |
| motion-tour | drawCalls | 83.7466396335511 | 81.9774674249514 |  | INVALID: overlapping run ranges |
| motion-tour | gpuMicroseconds | 454.614744415434 | 447.866201713338 |  | INVALID: overlapping run ranges |
| motion-tour | processCpuMilliseconds | 25046.875 | 24937.5 |  | INVALID: overlapping run ranges |
| motion-tour | processWallMilliseconds | 18962.7456 | 18986.1547 |  | INVALID: overlapping run ranges |
| motion-tour | averageBusyCpuCores | 1.32084643903043 | 1.31118318562764 |  | INVALID: overlapping run ranges |
| motion-tour | processPeakWorkingSetBytes | 1736503296 | 1731031040 | -0.31513075803572 | disjoint-run-ranges |

## Dense frame geometry bindings (2026-11-03)

Per-frame static geometry bindings use a dense array indexed directly by draw-list number. Preparation,dynamic upload,compiled plans and command recording share the same frame binding. Reset clears previous references and sets the new list range,so shrinking/empty frames and dynamic lists cannot inherit stale bindings. GPU ownership,fences and retirement remain unchanged. Removing hash lookup is a performance hypothesis until a separate NativeAOT comparison completes.
Source evidence: Dictionary<int,StaticGeometryBuffer> keys are exactly bounded draw-list indices. Repeated TryGetValue/indexer/ContainsKey calls occur in static preparation,dynamic upload,compiled-plan checks,descriptor recording and direct command recording. Replace the hash dictionary with one reused nullable-reference array; preserve absent lookup behavior and release all previous frame references before reset. No GPU work/quality/profiling options change. A focused dictionary-oracle test covers0/1/5000/3/0/512/17/5000/0 slots,sparse holes,negative/out-of-range misses,lookup identity and clear. NET10 real Vulkan suite367 checks passes. Further validation and performance comparison pending.

Dense-binding correctness: NET10/NET11 GPU367 checks each; direct submission343. All existing rendering/lifetime/event tests remain included. Performance contract frame-bindings-input-contract.json permits12 alternating18-second runs plus at most2 six-second diagnostics, cumulative benchmark budget106 after preceding92. Baseline is the measured/promoted reservation imageAC3D25FB. No speedup is claimed before the new comparison is terminal.

Dense-binding final pre-performance checks: Release frame-bindings-solution0warnings/0errors; actual NativeAOT code generation frame-bindings-native-build with empty stderr. NET10/11GPU367each,direct343. The measured reservation image remains default; frame-bindings-ab compares it to the new candidate in12 isolated phases without concurrent builds/tests. Native candidate promotion awaits terminal measurement and audits.

Completed dense-binding comparison(2026-11-03): .NET11 RC NativeAOT,1200tables,dense/resident,three18-second runs per variant/scenario. Camera921.10→946.04 FPS;motion1006.53→974.12 FPS. Run ranges overlap,so neither improvement nor regression percentage is established. Current ranges721.13–1013.41(camera),880.19–984.70(motion) retain all low runs. Current-image best single-run average1013.41;median target attainment94.60%/97.41%;neither meets1000 across runs. p95/p99/1% low ranges also overlap. Motion median peak working set increases1730801664→1731289088bytes(about0.47MiB),with disjoint ranges;camera memory ranges overlap. Presentation/warmup/timing/process/geometry audits pass12/12. Geometry generation remains0/1 per frame and all GPU measurements stay enabled. Evidence:artifacts/perf-2026-11-03/frame-bindings-ab;latest score fps-current.json. Default EXE/PDB matches measured image917A9C59 by hash readback.

| Scenario | Metric | Before | After | Change% | Status |
| --- | --- | ---: | ---: | ---: | --- |
| camera | averageFps | 921.098941137645 | 946.044051613275 |  | INVALID: overlapping run ranges |
| camera | medianMs | 0.8718 | 0.8349 |  | INVALID: overlapping run ranges |
| camera | p95Ms | 2.413595 | 2.358845 |  | INVALID: overlapping run ranges |
| camera | p99Ms | 2.77051 | 2.670947 |  | INVALID: overlapping run ranges |
| camera | low1PctFps | 281.816930111817 | 293.443246639665 |  | INVALID: overlapping run ranges |
| camera | sceneAllocatedBytesPerFrame | 736 | 736 | 0 | UNCHANGED |
| camera | rendererAllocatedBytesPerFrame | 265.171740379092 | 265.413324666883 |  | INVALID: overlapping run ranges |
| camera | frameUploadBytes | 5920.93739230327 | 5895.81813078866 |  | INVALID: overlapping run ranges |
| camera | layerStateUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledLayerDraws | 69.6744623850554 | 68.764835879103 | -1.30553788980148 | disjoint-run-ranges |
| camera | sharedBasisLayerDraws | 0 | 0 |  | UNCHANGED |
| camera | sharedCameraLayerDraws | 69.6744623850554 | 68.764835879103 | -1.30553788980148 | disjoint-run-ranges |
| camera | compiledLayerCommands | 1739.34865020103 | 1728.74487791187 |  | INVALID: overlapping run ranges |
| camera | compiledDescriptorBuilds | 0 | 0 |  | UNCHANGED |
| camera | compiledCommandVisits | 4319.21632682366 | 4320.10545888296 |  | INVALID: overlapping run ranges |
| camera | layerBoundsCommandVisits | 0 | 0 |  | UNCHANGED |
| camera | drawCalls | 98.6744623850554 | 97.764835879103 | -0.921845920378828 | disjoint-run-ranges |
| camera | gpuMicroseconds | 511.872900631822 | 502.536770137524 |  | INVALID: overlapping run ranges |
| camera | processCpuMilliseconds | 24281.25 | 24375 |  | INVALID: overlapping run ranges |
| camera | processWallMilliseconds | 18958.3974 | 19015.6079 | 0.301768650550582 | disjoint-run-ranges |
| camera | averageBusyCpuCores | 1.28076490262832 | 1.2821276803392 |  | INVALID: overlapping run ranges |
| camera | processPeakWorkingSetBytes | 1728786432 | 1730846720 |  | INVALID: overlapping run ranges |
| motion-tour | averageFps | 1006.52873970229 | 974.121496401799 |  | INVALID: overlapping run ranges |
| motion-tour | medianMs | 0.7524 | 0.8212 |  | INVALID: overlapping run ranges |
| motion-tour | p95Ms | 2.23487 | 2.2745 |  | INVALID: overlapping run ranges |
| motion-tour | p99Ms | 2.551385 | 2.60574 |  | INVALID: overlapping run ranges |
| motion-tour | low1PctFps | 355.743383928723 | 349.821509405185 |  | INVALID: overlapping run ranges |
| motion-tour | sceneAllocatedBytesPerFrame | 3592 | 3592 | 0 | UNCHANGED |
| motion-tour | rendererAllocatedBytesPerFrame | 591.051302288871 | 591.288441937895 |  | INVALID: overlapping run ranges |
| motion-tour | frameUploadBytes | 6955.25072349382 | 6979.78772847727 |  | INVALID: overlapping run ranges |
| motion-tour | layerStateUploadBytes | 488.280898876404 | 485.677315651999 | -0.533214227793155 | disjoint-run-ranges |
| motion-tour | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| motion-tour | compiledLayerDraws | 52.4722441462773 | 53.1125908812937 |  | INVALID: overlapping run ranges |
| motion-tour | sharedBasisLayerDraws | 0 | 0 |  | UNCHANGED |
| motion-tour | sharedCameraLayerDraws | 52.4722441462773 | 53.1125908812937 |  | INVALID: overlapping run ranges |
| motion-tour | compiledLayerCommands | 1470.52229676401 | 1508.97248080451 |  | INVALID: overlapping run ranges |
| motion-tour | compiledDescriptorBuilds | 7.17741317671093 | 7.13810193557029 | -0.547707651388885 | disjoint-run-ranges |
| motion-tour | compiledCommandVisits | 3041.46833178272 | 3046.80152204933 |  | INVALID: overlapping run ranges |
| motion-tour | layerBoundsCommandVisits | 9 | 9 | 0 | UNCHANGED |
| motion-tour | drawCalls | 81.4722441462773 | 82.1125908812937 |  | INVALID: overlapping run ranges |
| motion-tour | gpuMicroseconds | 419.470450933966 | 431.01639953795 |  | INVALID: overlapping run ranges |
| motion-tour | processCpuMilliseconds | 24921.875 | 25125 |  | INVALID: overlapping run ranges |
| motion-tour | processWallMilliseconds | 19011.4928 | 19057.109 |  | INVALID: overlapping run ranges |
| motion-tour | averageBusyCpuCores | 1.3091477861652 | 1.31191734226276 |  | INVALID: overlapping run ranges |
| motion-tour | processPeakWorkingSetBytes | 1730801664 | 1731289088 | 0.028161747826938 | disjoint-run-ranges |

Latest queued interaction diagnostic frame-bindings-drag validates1200tables,entity635,zoom2.149838,six-second held drag,126motion frames and293.38world-unit movement. First-two-second UI p95/p99 859.115/1293.775us,total p95/p99 2666.41/4212.074us and max37520.1us; later UI p95/p991410.87/1852.382us,total p95/p993612.91/4646.654us. First-two-second total has3samples>16ms and2>33ms; later has0. Compiled index upload first-two-second total608328,max319992bytes,later0. This is instrumented stage timing,not target FPS or proof of no stalls. Direct OS status-window capture was rejected by automatic approval review with blocked by policy before execution. No screenshot was created and no alternate desktop-capture route was attempted. Latest full desktop status-panel pixels remain unverified; the rebuilt full-dynamic NativeAOT sample is ready for runtime comparison,not claimed visually reviewed.

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

## Compiled group index capacity (2026-11-03)

Source finding: a compiled group's index capacity was fixed at1,048,576,independently from the256-descriptor limit and512MiB cache cap. Long vector-text meshes can hit the index bound before the descriptor bound,causing additional real indexed draws even with compatible material/camera/depth. Hypothesis:2,097,152 entries reduce submission work while retaining exact geometry and the same overall cache bound. This is a CPU/GPU batching hypothesis,not a raster cache or lower-detail rendering path.

Implementation: per-renderer immutable index limit defaults to2Mi entries. DUXEL_VK_COMPILED_INDEX_LIMIT strictly accepts1/2/4/8Mi entries and rejects other values. All group planning/eligibility checks use the instance value;descriptor encoding,vertex bounds,shader binaries,512MiB cache,source anchors,clipping and resource lifetime remain unchanged. UiRendererFrameMetrics.CompiledIndexLimit records the actual setting. ERD JSON records that setting and the maximum observed compiled-index cache bytes over measured frames. This cache counter is not total GPU VRAM or whole-process peak memory.

GPU boundary fixture submits two retained600,006-index streams with visible quads plus degenerate triangles. 1Mi/2Mi use2/1actual compiled draws respectively,with byte-identical GPU pixels at three camera states and zero index/geometry reupload after initial residency. Parser default/supported/invalid checks are included. NET10/11 real Vulkan suites each pass390checks. Direct submission and Release are checked before performance execution. Runner audit10/10 validates actual-capacity/cache/draw/GPU fields,legacy process data compatibility and conflict/idle rejection before child creation. The same-image comparison flag is -CompareCompiledIndexCapacity. It preserves per-frame counters/GPU timestamps,accepted-presentation sampling,warmup,pipeline,event-loop and quality. index-capacity-input-contract.json fixes12 alternating18-second runs plus at most2six-second diagnostics,cumulative costful execution budget131 after117 preceding executions. No FPS claim is made until all runs and audits are terminal.

Capacity pre-performance validation completed: direct GPU366checks; Release index-capacity-solution0warnings/0errors; NativeAOT generation verified in index-capacity-native-build with empty stderr. The previous measured default EXE917A9C59 remains protected during the new same-image1Mi/2Mi comparison. All geometry,shader,MSAA and presentation-counter behavior remain unchanged.

Completed same-image1Mi/2Mi index-capacity NativeAOT comparison(2026-11-03):1200tables,dense/resident,MSAA4,VSync off,three nominal18-second executions per mode/scenario. Current camera988.05 FPS(98.80%,range899.53–1044.10),motion966.90 FPS(96.69%,828.92–1074.02);best single-run average1074.02. Neither meets1000 across runs.1Mi→2Mi FPS923.78→988.05 and1058.46→966.90 has overlapping ranges;no improvement/regression percentage is established. Mean draws97.59→91.91 and80.69→74.65;compiled index-cache peak270330504/270330552bytes is unchanged between modes. Distinguish the deterministic2→1draw/exact-pixel boundary proof from real-run averages. Per-frame means include camera-state sample weighting,not pure same-view work reduction. Raw frames,presentation links,actual limits/cache,warmup,process metrics and0/1scene-builds audits pass12/12. Default EXE/PDB matches measured image60FE419D. See fps-current.json and index-capacity-ab.

Measurement inspection:App auto-exit starts at appStartUtc before font setup;ERD tour time advances from rendering start;presentation sampling begins after first accepted present+1second. Actual sampled intervals span14.84–15.21seconds inside nominal18-second runs,and mean visible command counts differ. This does not prove every cause of FPS variability. Before more full comparisons,separate preparation from measured camera/motion workload start. Preserve these results as observations under their original nominal-run contract. runtime-current.NET10/11 results use matching source hashes before this capacity change;do not promote them to final performance measurements of the new image.

| Scenario | Metric | Before | After | Change% | Status |
| --- | --- | ---: | ---: | ---: | --- |
| camera | averageFps | 923.779071656797 | 988.047875835168 |  | INVALID: overlapping run ranges |
| camera | medianMs | 0.7705 | 0.6999 |  | INVALID: overlapping run ranges |
| camera | p95Ms | 2.45474 | 2.5782 |  | INVALID: overlapping run ranges |
| camera | p99Ms | 2.84048 | 3.190884 |  | INVALID: overlapping run ranges |
| camera | low1PctFps | 276.127715373639 | 254.547722640756 |  | INVALID: overlapping run ranges |
| camera | sceneAllocatedBytesPerFrame | 736 | 736 | 0 | UNCHANGED |
| camera | rendererAllocatedBytesPerFrame | 265.275353168653 | 265.208293366782 |  | INVALID: overlapping run ranges |
| camera | frameUploadBytes | 5919.86348720192 | 5840.26409317804 |  | INVALID: overlapping run ranges |
| camera | layerStateUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledLayerDraws | 68.587496829825 | 62.9124792013311 | -8.27412850854491 | disjoint-run-ranges |
| camera | sharedBasisLayerDraws | 0 | 0 |  | UNCHANGED |
| camera | sharedCameraLayerDraws | 68.587496829825 | 62.9124792013311 | -8.27412850854491 | disjoint-run-ranges |
| camera | compiledLayerCommands | 1710.70830598702 | 1602.7849428409 |  | INVALID: overlapping run ranges |
| camera | compiledDescriptorBuilds | 0 | 0 |  | UNCHANGED |
| camera | compiledCommandVisits | 4319.8358491942 | 4372.40188214489 |  | INVALID: overlapping run ranges |
| camera | layerBoundsCommandVisits | 0 | 0 |  | UNCHANGED |
| camera | drawCalls | 97.587496829825 | 91.9124792013311 | -5.81531222016084 | disjoint-run-ranges |
| camera | gpuMicroseconds | 515.014769634652 | 483.734599134776 |  | INVALID: overlapping run ranges |
| camera | compiledIndexCachePeakBytes | 270330504 | 270330504 | 0 | UNCHANGED |
| camera | processCpuMilliseconds | 25593.75 | 24156.25 |  | INVALID: overlapping run ranges |
| camera | processWallMilliseconds | 19008.4105 | 18988.1381 |  | INVALID: overlapping run ranges |
| camera | averageBusyCpuCores | 1.34644345985689 | 1.2672277464565 |  | INVALID: overlapping run ranges |
| camera | processPeakWorkingSetBytes | 1730195456 | 1730990080 |  | INVALID: overlapping run ranges |
| motion-tour | averageFps | 1058.46367105054 | 966.901564366957 |  | INVALID: overlapping run ranges |
| motion-tour | medianMs | 0.72545 | 0.7581 |  | INVALID: overlapping run ranges |
| motion-tour | p95Ms | 2.220575 | 2.42476 |  | INVALID: overlapping run ranges |
| motion-tour | p99Ms | 2.53711 | 2.758572 |  | INVALID: overlapping run ranges |
| motion-tour | low1PctFps | 361.488253877018 | 335.882096248224 |  | INVALID: overlapping run ranges |
| motion-tour | sceneAllocatedBytesPerFrame | 3592 | 3592 | 0 | UNCHANGED |
| motion-tour | rendererAllocatedBytesPerFrame | 591.010581351923 | 590.625709789971 |  | INVALID: overlapping run ranges |
| motion-tour | frameUploadBytes | 6901.04120502925 | 6986.38899911062 |  | INVALID: overlapping run ranges |
| motion-tour | layerStateUploadBytes | 487.144528818623 | 483.192539593215 |  | INVALID: overlapping run ranges |
| motion-tour | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| motion-tour | compiledLayerDraws | 51.6900449662753 | 45.6526647054799 | -11.6799671285533 | disjoint-run-ranges |
| motion-tour | sharedBasisLayerDraws | 0 | 0 |  | UNCHANGED |
| motion-tour | sharedCameraLayerDraws | 51.6900449662753 | 45.6526647054799 | -11.6799671285533 | disjoint-run-ranges |
| motion-tour | compiledLayerCommands | 1420.54384211841 | 1395.43599917904 |  | INVALID: overlapping run ranges |
| motion-tour | compiledDescriptorBuilds | 7.16089879248102 | 7.10041000080392 |  | INVALID: overlapping run ranges |
| motion-tour | compiledCommandVisits | 3028.17714427985 | 3039.66155826641 |  | INVALID: overlapping run ranges |
| motion-tour | layerBoundsCommandVisits | 9 | 9 | 0 | UNCHANGED |
| motion-tour | drawCalls | 80.6900449662753 | 74.6526647054799 | -7.48218725534076 | disjoint-run-ranges |
| motion-tour | gpuMicroseconds | 410.776913315012 | 431.395533419992 |  | INVALID: overlapping run ranges |
| motion-tour | compiledIndexCachePeakBytes | 270330552 | 270330552 | 0 | UNCHANGED |
| motion-tour | processCpuMilliseconds | 24953.125 | 25343.75 |  | INVALID: overlapping run ranges |
| motion-tour | processWallMilliseconds | 19036.3086 | 19010.4912 |  | INVALID: overlapping run ranges |
| motion-tour | averageBusyCpuCores | 1.31099725904184 | 1.33077881566872 |  | INVALID: overlapping run ranges |
| motion-tour | processPeakWorkingSetBytes | 1730002944 | 1730125824 |  | INVALID: overlapping run ranges |

Default2Mi native interaction index-capacity-drag validates actual capacity2097152,entity635,zoom2.149838,126motion frames over~6seconds and290.29world-unit movement. First-two-second UI p95~888.68us,total p99~4305.41us,max31889.4us;later UI p95~989.8us,total p99~3948.13us,max16079.8us. Initial compiled-index upload total608328/max319992bytes,later0;new selected-source cache peak270938880bytes. It verifies the real interaction and bounded initial index rebuild,not no-stall behavior or isolated FPS. No screenshot or global focus/pointer manipulation occurred. Timed runs12 plus this one diagnostic use130 of the declared131-execution cumulative budget;one diagnostic slot remains. No further full comparison is authorized by this phase contract without a new justified input/budget contract.

## Measured workload epoch (2026-11-03)

The preceding12-run audit exposed14.84–15.21seconds of presentation samples in nominal18-second executions. Source inspection confirms App starts its auto-exit timer before font setup,while ERD tour/motion starts before presentation warmup is complete. Startup and cleanup belong to nominal execution,not measured workload. Different visible-command means alone do not prove a specific source of all FPS variation. Correct the input epoch before more full comparisons;do not claim this measurement correction is an engine speedup.

BenchWorkloadClock provides a monotonic preparation/running/complete lifecycle with zero first delta,exclusive deadline and explicit timestamp-regression rejection. The opt-in -ControlledWorkloadClock runner uses DUXEL_ERD_WORKLOAD_CLOCK=post-warmup and DUXEL_ERD_MEASURE_SECONDS. It rejects idle and mixed old-baseline-executable comparisons before child creation. App auto-exit explicitly becomes duration+120 as a guard;the sample ends its measured workload itself. Legacy nominal-total mode remains explicit wall-total and ordinary interactive operation does not allocate the new clock/trace storage.

During controlled preparation,the real scene is built/uploaded at its initial pose. Simulation advances only after first accepted presentation+1sec. Each source telemetry row records workload membership and monotonic timestamp;the collector excludes queued preparation frames even if presented after warmup. A bounded8MiB timestamp array is allocated only for controlled measurement output. JSON records the epoch,deadline,first/last source and presentation timestamps,initial camera pose and source-epoch-v2 warmup method. A workload CSV links every measured source to its timestamp. This changes workload preparation/sampling,not shaders,geometry,MSAA or rendering work in the measured scenario.

NET10/NET11 GPU suites each pass397checks including7clock boundary/regression checks. Runner audit15/15 covers valid controlled and legacy data,early/wrong epochs,source-time leakage,source-link mismatch,nonmonotonic/missing/out-of-range CSV rows,duration mismatch and preflight rejection. The remaining previously declared diagnostic slot is consumed by exactly one6-second camera correctness run,not new target evidence. workload-clock-input-contract.json holds the explicit input/duration/guard/budget contract. No new full comparison is started before this real-source trace passes.

Controlled camera correctness run workload-clock-camera-check passes one requested6-second workload:5985presentation/source rows,presentation interval sum5.9983109sec,source timestamp span5.9994573sec,first source timestamp exactly equals the workload epoch,initial zoom0.06347403/pan0,0. All source timestamps fall within the declared epoch/deadline and match presentation IDs. NativeAOT image94E22DE0,SDK11RC actual code generation verified. This one run is NOT target FPS evidence and does not update fps-current.json. The prior131-execution budget is now exhausted. A new workload-controlled-input-contract explicitly authorizes one6-second motion correctness run and12fixed18-second workload comparisons,cumulative budget144,because the measured input contract changes from nominal app-total to post-warmup source epoch.

Controlled motion correctness workload-clock-motion-check passes:requested6sec,presentation interval sum5.9983938sec,source span5.9998223sec,first source timestamp exactly at epoch,initial zoom0.06347403 matching camera proof,and exactly1scene build/frame. Both actual camera/motion traces pass the consumed runner's source/deadline/duration checks. They are one-run correctness evidence only,not target attainment. New same-image controlled1Mi/2Mi12-run comparison now uses exact18-second source workload epochs after warmup. Old default image60FE419D remains protected until these timed runs and audits finish.

## Current FPS target record: controlled workload

> Latest measurement: **target 1000 FPS**, camera **870.27 FPS(87.03%)**, camera plus entity motion **906.79 FPS(90.68%)**, best single-run average **935.32 FPS**. Stable target remains unmet. .NET11 RC NativeAOT,1200 tables,MSAA4,actual18 seconds after preparation,three runs per scenario,identical initial camera. Measured SHA256:94E22DE0FFBB5F47D9CAD2E36D6F03207F10CDB2DA6D150F121A403FCF0A52CC. The default distribution EXE/PDB now matches this measured image by hash readback. Latest score:`artifacts/perf-2026-11-03/fps-current.json`; evidence:`workload-controlled-capacity-ab/`. Previous-condition best1074.02 FPS is not directly comparable.

The 12-run capacity comparison and presentation/source audit completed 12/12. Both capacity variants have overlapping FPS ranges; no FPS improvement or regression percentage is established. This correction changes the workload clock, not a demonstrated engine speedup. The live panel already displays current accepted-presentation FPS / 1000; results.json, fps-target.md and fps-current.json retain independent run results and target status. Full quality, memory and stable1000 validation remains incomplete.

The real dynamic3D sample and window chrome were rendered by the current renderer and reviewed through its owned GPU buffer: panel title,bottom line,compact line spacing and content-sized panel are visible. NET10/11 each pass8/8,including colored geometry;final pixels are identical. Evidence:artifacts/perf-2026-11-03/status-panel-gpu-review.json. This is neither desktop/DWM capture nor an FPS measurement. The initial texture-order hypothesis was rejected by exact pixel equality;no engine defect is claimed.

## Axis-aligned layer culling candidate

Hypothesis: the controlled ERD workload still visits approximately4104/3074 compiled commands per camera/motion frame. Its scale/translation cameras have constant clip W/Z and axis-aligned X/Y,so two opposite transformed endpoints determine the same bounds as four corners. Specialize this exact matrix shape; retain the general algorithm for rotation,perspective and other matrix shapes. The existing two-pixel AA guard,depth limits,negative scales and nonfinite handling remain. Vector4.Transform is retained for the endpoints to preserve its floating-point evaluation at clip boundaries. Geometry,vector quality,shaders,uploads and depth ordering are unchanged.

Implementation: Vulkan layer/command bounds classification has a renderer-lifetime mode snapshot. DUXEL_VK_AXIS_ALIGNED_CULLING accepts0/1 only(default1). UiRendererFrameMetrics.AxisAlignedLayerCulling and ERD axisAlignedLayerCulling JSON report the actual renderer setting. The measurement runner adds -CompareAxisAlignedCulling,forces common2Mi compiled capacity/pipeline/event-loop/profiling settings,validates the mode/capacity/process/warmup contract and restores the environment.

Correctness: NET10/11 Vulkan each403checks,direct379,general mode403.100000 signed-scale/translation/depth/viewport cases,rotation/perspective/nonfinite cases,and adjacent floats at guarded planes preserve classifications. All119 raw GPU PPM images are byte-identical between general and specialized modes(axis-culling-pixel-audit.json). Release solution build has0warnings/errors;runner audit6/6. A C# overload inference error in the initial two-endpoint edit was corrected with explicit Vector4 construction;failed logs are retained. Root-relative paths used from the SDK11 working directory failed before writing telemetry/contract;absolute repository paths,ErrorActionPreference=Stop and contract readback were then used. No failed check is counted as passed.

NativeAOT actual code generation is in axis-culling-native-build.stdout.log,empty stderr,image09584FF4. The measured input contract fixes12 alternating actual18-second runs plus at most2six-second diagnostics,cumulative ERD performance execution budget158 after144 preceding runs. Correctness tests/builds are outside that explicitly named ERD run denominator. Before/After FPS and percentages remain unconfirmed until all runs/audits are terminal;default image94E22DE0 and current870.27/906.79FPS remain protected. This is a CPU culling specialization,not a 3D rasterization cost elimination. Risks:matrix-shape dispatch overhead may offset arithmetic savings,and GPU work can remain dominant.

Completed axis-culling-ab12/12 and presentation/source audit12/12. > Latest measurement/default distribution: **target1000 FPS**,camera **1054.95 FPS(105.49%)**,camera plus entity motion **1116.81 FPS(111.68%)**,best single-run average **1122.60 FPS**. All three independent run averages per scenario exceed1000; ranges1044.20–1081.47/1031.41–1122.60 FPS. .NET11 RC NativeAOT,1200tables,MSAA4,actual18seconds after preparation,three runs per scenario,identical initial camera. This does not guarantee a1ms deadline for every frame. SHA256:09584FF4DF471401C9B53047BA793885BFA919290CEAA2B7D5EB27D501260AAA. Default EXE/PDB hash readback verified. Latest score:`artifacts/perf-2026-11-03/fps-current.json`;evidence:`axis-culling-ab/`(audit12/12).

Camera same-image general→specialized989.73→1054.95FPS(+6.59%,disjoint run ranges). Motion1042.68→1116.81FPS has overlapping ranges;no FPS improvement percentage is established. Camera p95/p99/1%low also overlap. These are workload measurements;frame-weighted GPU/draw means include different camera-state sampling weights. Motion p99 and1%low improve by1.65%/2.29% with disjoint ranges;this does not remove occasional long frames. Peak working set remains around1.6GiB,not a VRAM reading. Prior controlled870.27/906.79FPS is retained as history,not used to inflate the same-image gain. Native EXE/PDB09584FF4 promoted with hash readback;full goal remains incomplete.

| Scenario | Metric | General | Specialized | Change% | Evidence status |
| --- | --- | ---: | ---: | ---: | --- |
| camera | averageFps | 989.7346 | 1054.945 | 6.5887 | disjoint-run-ranges |
| camera | medianMs | 0.6942 | 0.6321 | -8.9455 | disjoint-run-ranges |
| camera | p95Ms | 2.40647 | 2.411665 |  | INVALID: overlapping run ranges |
| camera | p99Ms | 2.661948 | 2.6747 |  | INVALID: overlapping run ranges |
| camera | low1PctFps | 291.71343 | 291.06024 |  | INVALID: overlapping run ranges |
| camera | frameUploadBytes | 5835.6888 | 5772.6429 | -1.0803 | disjoint-run-ranges |
| camera | compiledIndexUploadBytes | 0 | 0 |  | UNCHANGED |
| camera | compiledCommandVisits | 4180.8943 | 4222.7651 | 1.0015 | disjoint-run-ranges |
| camera | drawCalls | 94.3495 | 93.423623 | -0.9813 | disjoint-run-ranges |
| camera | gpuMicroseconds | 580.25361 | 553.59917 | -4.5936 | disjoint-run-ranges |
| camera | averageBusyCpuCores | 1.1766161 | 1.2225278 | 3.9020 | disjoint-run-ranges |
| camera | processPeakWorkingSetBytes | 1.7381007e+09 | 1.7381908e+09 |  | INVALID: overlapping run ranges |
| motion-tour | averageFps | 1042.6824 | 1116.8064 |  | INVALID: overlapping run ranges |
| motion-tour | medianMs | 0.71865 | 0.6526 |  | INVALID: overlapping run ranges |
| motion-tour | p95Ms | 2.265075 | 2.25106 |  | INVALID: overlapping run ranges |
| motion-tour | p99Ms | 2.61017 | 2.5672 | -1.6463 | disjoint-run-ranges |
| motion-tour | low1PctFps | 358.45118 | 366.67493 | 2.2942 | disjoint-run-ranges |
| motion-tour | frameUploadBytes | 6910.9982 | 6846.0005 |  | INVALID: overlapping run ranges |
| motion-tour | compiledIndexUploadBytes | 1.5602686 | 1.4566439 |  | INVALID: overlapping run ranges |
| motion-tour | compiledCommandVisits | 3101.0133 | 3114.5465 |  | INVALID: overlapping run ranges |
| motion-tour | drawCalls | 79.446179 | 78.287598 |  | INVALID: overlapping run ranges |
| motion-tour | gpuMicroseconds | 485.15166 | 463.10269 |  | INVALID: overlapping run ranges |
| motion-tour | averageBusyCpuCores | 1.1937227 | 1.2395556 |  | INVALID: overlapping run ranges |
| motion-tour | processPeakWorkingSetBytes | 1.7394852e+09 | 1.724629e+09 | -0.8541 | disjoint-run-ranges |

Current measured09584FF4 NativeAOT input diagnostic axis-culling-drag passes:1200tables,actual axisAlignedLayerCulling=true,capacity2097152,entity635,126motion frames over~6seconds,292.45world-unit movement. First2seconds compiled-index upload total608328/max319992bytes,later0. UI-stage p95~605.61us initially/~631.07us later;total-stage maximum31644.6us/14792.6us. This is instrumented interaction evidence,not isolated FPS or no-stall proof;no global pointer/focus manipulation occurred. Cumulative ERD executions157/158 fixed budget,one diagnostic slot unused. Latest current FPS remains1054.95/1116.81. The1000FPS independent-run-average condition passes both scenarios;full memory/lifetime/current-source runtime comparison remains unfinished.

## Live resource observation and GC cause isolation

`scripts/measure-erd-resources.ps1` observes live resources. Before launch it checks the declared EXE hash,a fresh output path and existing same-name processes;it preserves existing windows. Empty benchmark output avoids sample arrays. One-second process samples record private bytes,working set,CPU,handles,threads and logged frames. Normal window close/process exit is verified;emergency termination,missing data and long sample gaps cannot pass. Process memory is distinct from VRAM and live-object size.

Image09584FF4,30-second preparation:over179.97active seconds,first/last-third private-byte medians rise118.31MiB and working set116.41MiB. Cause remains unresolved;this is not a leak-free result. Handles511–521,threads13–21,normal exit. Over59.84idle seconds:zero additional logged frames,46.875ms CPU(about0.0783% of one logical core),private/working-set median changes-0.168/-0.082MiB,normal exit. Completing2/2observations does not complete memory validation. Evidence:`artifacts/perf-2026-11-03/resource-observer-audit.json` and individual `samples.csv` files.

Optional `DUXEL_APP_PROFILE_MEMORY=1` adds an estimated current managed heap,last-collection commit/heap sizes,generation collection counts and total allocations only to emitted App profile records(default0). It does not force GC. Observer `-IncludeGcMetrics` probes the first profile for this capability and writes in-window `gc-samples.csv`. This diagnostic needs a newly built candidate;existing09584FF4does not contain it. The1000FPS evidence and default EXE remain protected.

Before/After performance percentage:unconfirmed;this phase is resource diagnosis,not an engine speedup. The observer is reused for active/idle and the next GC diagnostic. Exact source used for the two original observations is retained in resource-observer-used.ps1.txt;later observer improvements use absolute monotonic sampling deadlines and validate maximum sample gaps rather than assuming file IO takes no time. Observations consumed2new runs,cumulative159;the previous unused diagnostic allowance was retired. Further cause isolation requires a new input/budget contract.

| Scenario | Observed seconds | First private median bytes | Last private median bytes | Working-set median delta bytes | Mean busy CPU cores | Logged-frame delta |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| motion-tour | 179.97245010 | 3372883968 | 3496943616 | 122064896 | 1.3854551064 | 186000 |
| idle | 59.83755580 | 3291508736 | 3291332608 | -86016 | 0.0007833709 | 0 |

GC cause-isolation diagnostics pass NET10/NET11 GPU406checks each and Release0warnings/errors. Actual application-worker tests verify memory fields enabled/disabled;the completed-record/GC parser passes6/6 including an unfinished final log record. NativeAOT generation is verified for diagnostic candidate B13F2B47080445A72CE0503B65C950472AA6E4B5879189171ADA07A78BC67E93. It is not promoted over the measured09584FF4default. The first native GC profile contains real heap/commit/collection/allocation snapshots. A new resource-gc-input-contract permits exactly one180-second active observation after30seconds preparation,cumulative ERD run budget160,to distinguish managed contributions from native/driver accumulation. The observation is running(resource-gc-state.json,tool session86629);no completion or leak-free conclusion is claimed. Missing GC fields are rejected at the first completed profile before the full observation interval. The live FPS record remains1054.95/1116.81 and is unrelated to this instrumented resource diagnostic.

## Exclusive pixel ownership and pooled multiline rasters

Optional `IMemoryOwner<byte>` properties `PlatformTextRasterizeResult.PixelOwner` and `UiTextureUpdate.PixelOwner` transfer exclusive pixel ownership. Pixels remain immutable while consumed. `UiDrawData.CaptureTexturePayloads()` copies existing borrowed updates;an exclusively owned update stays with its frame without another copy. Frame consumption/discard,superseded queued updates,measurement-only text eviction and context disposal release ownership. GPU textures retain the existing fence/retirement lifetime.

Windows multiline bitmap composition uses returnable pooled storage,clears its pixels and performs the same composition. Measurement-only backend calls also return storage.3D vector layers,shaders,font outlines and camera quality remain unchanged. Direct rasterization callers dispose PixelOwner after use or transfer it once to a texture update;they must not return or mutate storage after transfer.

Validation:.NET11 GPU422,.NET10 text-atlas422,direct398;all119before/after GPU images are byte-identical. Lifetime checks cover mixed owned/borrowed frames,consumer-thread return,frames outliving their context,unconsumed frames,cache eviction and actual raster byte reuse including transparency. Candidate52E300FA's first180-second observation has private/working-set median changes-10559488/-11585536bytes;this is one run. Quantitative claims await three independent runs per variant. Default1000FPS image09584FF4remains protected.

Hypothesis/evidence:the prior GC observation has matched-third heap growth123693912bytes and private growth123994112bytes,82generation0/7generation1collections and0generation2collections. Source inspection found a new full multiline RGBA array per changing status string. The measured managed contribution motivates reuse;this does not establish that every byte is leaked. Changes cover Core pixel ownership/cache/frame lifecycle and Windows multiline composition. Ordinary borrowed updates preserve copy isolation. Actual RGBA composition and119GPU outputs match.

Native baselineB13F2B47and PDB were copied with hash readback to the owned memory-baseline directory;current default09584FF4and candidate52E300FA use three current comparison images. The pixel-owner-resource-input-contract fixes five new observations in after1/after2/before2/before3/after3order,reusing the completed before1. Warmup30seconds,observation180seconds,1200tables,motion-tour,no benchmark arrays,no forced GC. Cumulative ERD budget165 after160preceding executions. Initial after1 launch was rejected before child creation by the existing-process guard;fresh process observation was absent and the output directory did not exist before the successful launch. This was not a timed execution or permission-policy bypass. First after1 is complete;remaining four are running under the existing observer. No memory percentage or new candidate FPS is established.

최신 메모리 반복 검증(2026-11-03): 변경 전 B13F2B47/후52E300FA를 각각3회, 준비30초＋동작180초,1200테이블·카메라＋엔티티 이동·측정 출력 없음 조건에서 비교했다. 총6/6회 정상 종료와 원시 표본·GC·해시 감사를 완료했다. 초기/후기1/3구간 Private Bytes 증가 중앙값은116.33MiB→0.375MiB(증가량99.68% 감소,실행 범위 비중첩),관리 힙 증가123693912→-721148바이트,GC커밋 증가122568704→0바이트다. 전체 스레드 할당/기록된 UI프레임은9067.79→8064.53바이트(-11.06%,범위 비중첩)다. 전체 메모리99.68% 감소나 누수·VRAM 검증 완료를 뜻하지 않으며 CPU 범위는 겹쳐 향상률을 주장하지 않는다. 근거:`artifacts/perf-2026-11-03/pixel-owner-resources-summary.json`. 최신 소스의 MSAA 경계 수정과 FPS·런타임 재검증은 별도로 진행 중이다.

2026-11-03 MSAA 깊이 경계 수정: 3D depth-write 파이프라인의 MSAA가2x이상일 때 분석적 AA alpha를 hardware alpha-to-coverage로 전달한다. 덮인 sample만 불투명 RGB와 깊이를 쓰므로 앞쪽 둥근 사각형·원·테두리를 먼저 그려도 뒤쪽 도형과의 전체 픽셀이 같다. 별도 레이어 텍스처·합성 패스·sample-rate shading·alpha-to-one 기능을 추가하지 않는다. 2D,1x 분석적 AA,depth-read 재질 alpha 혼합은 기존 경로를 유지한다. alpha-to-coverage는 유한한 sample mask이며 연속 반투명 재질이나 임의 순서 투명도 정렬을 대신하지 않는다. 1x의 부분 AA 경계 혼합까지 순서 독립이라고 확대 해석하지 않는다. sample mask 매핑은 드라이버 의존이므로 두 벤더 실측 완료로 보고하지 않는다.

실제 포함 셰이더를 GPU 초기화 전에 선언한 아티팩트 SHA256과 비교한다. 임시 SPIR-V 파일의 과거 타임스탬프가 복사되어 증분 빌드가 이전 리소스를 재사용한 실패를 확인했고,최종 아티팩트의 수정 시각과 새 빌드를 확인했다. 최종 GPU NET10/NET11각435·직접411·실제 상태 패널 각9검사와 기존 이미지119/119동치,Release경고/오류0을 통과했다. 별도2048도형/16면/320×240/MSAA4 Managed 집중 비교는 준비2초＋측정6초×각3회이며 연속 완료 시각 표본을 감사했다. FPS중앙값7458.46→7306.80,관측 차이-2.03%이나 실행 범위가 겹쳐 개선·회귀율은 미입증이다. ERD1000FPS 판정과 분리한다. 근거:`coverage-embedded-net10/`,`coverage-embedded-net11/`,`coverage-embedded-direct/`,`coverage-probe-v2-summary.json`,`coverage-existing-pixel-audit.json`(모두artifacts/perf-2026-11-03하위).

최신 측정(2026-11-03): MSAA 경계 수정＋픽셀 소유권 재사용을 포함한 NativeAOT694550C9는1200테이블,dense/resident,MSAA4,VSync off,실제 준비 후18초×각3회에서 카메라937.19FPS(93.72%,범위919.51–982.09),카메라＋엔티티 이동960.30FPS(96.03%,942.55–970.95),단일 실행 평균 최고982.09FPS다. 두 시나리오 모두1000미달이다. 기존09584FF4의1000이상 기록은 보존하되 발견한 MSAA 경계 결함을 포함하므로 최신 화면 정확성의 증거로 재사용하지 않는다. 정확성 수정은 유지하며 단계 간 차이를 격리된 회귀율로 계산하지 않는다. 원시 프레임 통계·표시/소스 연결·준비 구간·초기 자세·설정·해시6/6감사 통과. 상위 일회성 실행 래퍼는 완료 뒤 PowerShell 스크립트에 native LASTEXITCODE를 잘못 검사해1로 끝났고,실제6개 자식은 기존 실행기가 정상 종료를 확인했다. 기록을 직접 감사했고 비용 큰 재실행은 하지 않았다. 근거:artifacts/perf-2026-11-03/coverage-erd-current 및 fps-current.json. 기본09584FF4EXE/PDB는 갱신하지 않았고 최신 후보는 기존binding-candidate경로다. 최종.NET10/11 Managed/AOT 비교와 처리량 개선은 남아 있다.
## 최종 검증 결과 — 2026-11-03

현재 검증한 배포 이미지는 **DE068949E4A92689455341057405F6BFE0BEC38C9C89DC64D50A1D43A6D60715**다. 아래의 과거 단계별 수치는 당시 소스·이미지의 기록이며 현재 결과는 이 절과 [최신 FPS 기록](../../../artifacts/perf-2026-11-03/fps-current.json)을 기준으로 한다.

| ERD1200테이블·MSAA4 | 독립3회 평균의 중앙값 | 실행 범위 | 목표1000 |
| --- | ---: | ---: | --- |
| 카메라 이동 | 1093.47FPS | 1046.70–1180.66 | 3/3통과 |
| 카메라＋엔티티 이동 | 1044.80FPS | 1028.66–1171.38 | 3/3통과 |

단일 실행 평균 최고1180.66FPS다. .NET11 RC3 NativeAOT,`OptimizationPreference=Speed`,이식 가능한 기본 CPU명령어 집합,VSync off,최초 표시 후1초 준비와 실제18초 측정 조건이다. Vulkan 표시 요청 성공 간격을 사용하며 모든 프레임1ms나 모니터의 실제1000Hz 표시를 보장하지 않는다. 이전 단계와는 여러 변경이 포함되어 격리된 개선율을 계산하지 않는다.

레이어는 공통 Vulkan 경로의 실제3D 면·MVP·깊이를 사용한다. 별도 레이어 비트맵이나 합성 타깃이 없다. 카메라만 바뀌면 월드 지오메트리 생성0회이며 GPU상주 데이터를 유지한다. GPU가 최종 픽셀을 다시 래스터화하는 비용은 남는다. 미니맵과 다른 위젯은 공통 경로의 독립 viewport·camera·depth scope를 사용한다. 불투명 경계의 color/depth coverage를 MSAA1/4모두 일치시켰으며 부분 alpha 재질은 depth-read와 뒤→앞 혼합을 사용한다. 1x의 경계 정밀도는 한 표본이며 기본4x의 부드러운 품질은 유지한다.

GPU검사 NET10/11각438개,직접 제출414개,실제 동적3D 샘플·상태 패널 각9개,전체 Release경고0/오류0을 통과했다. 상태 패널 상단·하단·줄 간격은 현재 렌더러의 GPU 버퍼에서 확인했다. 세 프레임의 NET10/11픽셀은 동일하다. 데스크톱 캡처나 이 세 프레임의 FPS측정으로 확대하지 않는다.

| 모든 내용이 매 프레임 바뀌는3D 장면 | 1층×1000 | 8층×1000 | 8층×4000 |
| --- | ---: | ---: | ---: |
| .NET10 Managed | 8461.55 | 2453.86 | 719.93 |
| .NET11 Managed | 8044.89 | 2600.19 | 882.55 |
| .NET10 NativeAOT Speed | 7049.16 | 1815.60 | 581.45 |
| .NET11 NativeAOT Speed | 8124.30 | 2426.84 | 806.74 |

각 값은 독립3회 FPS중앙값이다. 전체12실행·72구간·원시 프레임72개·GPU계측72구간·표시 창12개 검증을 완료했다. .NET10 SDK10.0.403/runtime10.0.14와 .NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148을 사용했다. MSAA1,VSync off,정적 레이어 캐시 없음,직렬 UI프레임 시작 시계,구간별 준비0.25초＋측정1.5초다. 순서 반전과 동일한 표시 창 조건을 적용했다. 이 표의 수치는 ERD의 병렬 렌더링·MSAA4·표시 요청 시계와 직접 비교하지 않는다. 평균 외 median/p95/p99/1%low,할당,드로우,업로드,GPU시간과 실행 범위는 [런타임 결과](../../../artifacts/perf-2026-11-03/coverage-complete-runtime/summary.json)에 보존했다. .NET11이나 AOT가 모든 부하에서 가장 빠르다고 주장하지 않는다.

픽셀 소유권 재사용의 이전 변경 전/후 각3회180초 비교에서 Private Bytes 증가 중앙값은116.33MiB→0.375MiB(증가량99.68%감소)였다. 최종 DE068949이미지의 별도180초 확인은 증가0.383MiB,작업 집합 변화-0.297MiB,정상 종료였다. 유휴59.99초는 추가 로그 프레임0,CPU62.5ms,정상 종료였다. 강제 GC를 사용하지 않았다. 전체 메모리99.68%감소,무기한 누수 없음 또는 VRAM 측정으로 해석하지 않는다. [현재 자원 관측](../../../artifacts/perf-2026-11-03/coverage-complete-resources-audit.json).

현재 GPU 실측은 NVIDIA RTX5070Ti Laptop이다. 다른 GPU의 성능 보장은 하지 않는다. 글리프는 벡터 메시이고 기본 곡선 근사 오차0.001폰트 픽셀이므로 무한 배율의 해석적 글리프라고 주장하지 않는다. 비트맵 이미지의 확대 품질은 원본 해상도에 제한된다. 기존 SDK10.0.301기준과 공개 지원 TFM은 유지한다.

## 공 튀기기 공통 엔진 최적화 — 진행 중

가설: 5,000개·Size 6의 병목은 CPU 공간 탐색과 개별 도형 구성이고, 배경 원/사각형의 서로 다른 명령 종류도 불필요한 draw call을 발생시킨다.

변경: 공통 SpatialGrid2D와 CircleCollisionSolver2D, AddRegularPolygons를 추가했다. 원과 사각형은 기존 shader의 음수 반지름 계약으로 같은 GPU 인스턴스 스트림에 넣는다. 정적 삼각형 확장은 음수 반지름도 제외한다. 샘플은 공통 기능을 사용하고 동적 캔버스와 정적 배경을 구분한다.

중간 검증: SDK11 RC3/NativeAOT Speed, 실제 framebuffer1920×1080, 캔버스1520×918.4625, 1초 준비+6초 측정, 설정별 독립3회. Render/cache 평균FPS 중앙값298.489→1076.373(+260.61%), 충돌CPU2.608ms→약0.436ms, draw call916→53. Display/cache1067.128FPS, Render/uncached1001.251FPS, 동적 캔버스off1094.186FPS다. 정지 상태 ContentVersion 재사용 추가 전 결과이며 현재 전체 소스의 최종 결과가 아니다. 프로필 사이의 화질 차이는 개선율로 합산하지 않았다.

검사: GPU451개, 변경 전후 전체 이미지136/136 동일, 공통 충돌 경계 및 10,000개 다각형 정점/삼각형 동치 검사 통과. 고정SDK10.0.301 Release solution 빌드 경고0/오류0. 재현과 원시 기록은 docs/polygon-performance.ko.md와 artifacts/ball-perf-2026-11-03을 따른다.

리스크·후속: 동적 레이어 관리 비용, 매 프레임 약685KB 정점 전송, 기존 CPU 물리 궤적과의 차이, 원형 경계 모델의 한계를 구분한다. GPU 충돌 계산은 가능성 조사 단계이며 미구현이다. 정지 상태 재사용 이후 측정, ERD 회귀 및 최종 배포 검증이 남았다.


### 정지 캔버스 재사용 경로의 중간 성능 악화와 수정

매 프레임 geometry가 바뀌는 캔버스에 ContentVersion을 매번 부여하면 retained capture와 교체 비용으로 FPS가 약552~581까지 낮아졌다. 직접 그리기는 약1016~1091이었다. 기록은 resident-canvas-profiled에 보존했다. 기능을 제거하지 않고, 움직이는 내용은 동적 스트림으로, 멈춘 내용은 버전이 있는 retained geometry로 제출하도록 실제 갱신 계약을 수정했다. 이후 smart-canvas-profiled의 각3회 검증은 이동1020.81FPS, 직접1051.35FPS, 정지6630.17FPS 중앙값이었다. 이 단계는 GPU 충돌 계산 구현 전의 기록이다.

### 공통 GPU 시뮬레이션·명령 분류·캐시 통합 후 최신 검증

가설: 모든 몸체가 움직이는 부하에서는 CPU 충돌 계산과 정점 전송을 없애는 GPU 상주 경로가 유리하며, 일반 UI의 명령 분류는 변경된 상태에 대해서만 수행해야 한다.

변경: 공통 `UiGpuParticleSystem`/`UiGpuParticleFrame`/`AddGpuParticles`, Vulkan compute 공간 분할·Jacobi 응답·정점 생성, 같은 알고리즘의 CPU 모듈 `ParticleJacobiSimulation`을 구현했다. 입자 상태는 매 프레임 CPU로 읽지 않는다. 샘플의 수동 배경 캐시는 공통 `BeginLayer` 정적 캐시로 통합했고 공통 렌더 파이프라인을 기본 활성화했다. `UiDrawList`와 `UiDrawData`의 명령 기능 분류 재사용으로 일반 UI의 반복 탐색 비용도 줄였다.

초기 GPU 응답의 오차는 실패로 보존했다. 분리되는 접촉에 반복 반발을 적용하던 원인을 CPU/GPU 계약에서 수정하고, 좌표 규모에 따른 수치 접촉 여유를 동일하게 적용했다. 허용 오차를 완화하지 않았다. 5,000개·61단계 CPU 스냅샷 기준 최대 위치 오차0.000122픽셀, 방향0.0000661, 각속도0.0000425이며 유한 상태와 경계를 확인했다.

검증: SDK11.0.100-rc.3.26522.148, 런타임.NET11.0.0-rc.3.26522.148, NativeAOT Speed, RTX5070Ti Laptop, framebuffer1920×1080, 캔버스1520×918.4625, Size6, Render/MSAA1, VSync off, 충돌 반복2회. 준비1초+측정6초, CPU/GPU 교대 각3회. 동일 알고리즘 FPS중앙값860.141→5464.768(6.35배,+535.33%), p95프레임1.42550→0.34580ms, p992.34539→0.49297ms, 1%low340.299→1717.504FPS, 동적 전송685696→9200B/프레임이다. CPU 충돌 구간은 약0.795ms(이동 포함), GPU compute+draw는 약151.42µs다. GPU 시간 감소를 주장하지 않는다.

별도 설정15회는 Render/cache5498.85FPS, uncached5418.75, Display/cache4167.15, 직접5621.42, 정지7721.79 중앙값이다. 배경 캐시의 도형 구성74.16→3.17µs·전송78320→9200B 절감을 확인했다. 동적 캔버스는 직접 그리기보다 약2.18% 느린 관리 비용이 남는다. 프로필 품질 차이와 이동/정지 작업량 차이를 개선율로 합산하지 않는다.

품질·회귀: 최신 코어 NET10/11 각465검사, 기존 이미지136/136동일, ERD1200테이블 각3회6초 카메라1259.94·동시 이동1249.24FPS. 명령 전수 탐색 도입 시 카메라1103.06FPS였던 중간 악화도 보존했고, 공통 분류 재사용으로 개선했다. 초기18초 투어와는 직접 개선율로 비교하지 않는다. 계측 비활성 상태의 NaN JSON 저장 실패도 수정하여 미수집 값은null과 표본 수로 기록하고 기본 실행 정상 종료를 확인했다.

재현: [측정 문서](../../polygon-performance.ko.md)의 빌드·실행 명령. 최신 EXE SHA256은 F69412D1AF0694EE6C7C687FCCE4C01E62BEDA47ADC96C804FA238F17E11C033. 근거는 final-native-paired, final-profile-cache-suite, final-native-raw-audit.json, frame-cache-image-regression.json, gpu-current-source-manifest.json이다. UI 할당은 샘플 Render 현재 스레드, renderer 할당은 렌더 스레드 측정 범위이며 프로세스 전체 할당이 아니다.

남은 검증: 최종 요구사항별 배포 감사와 빌드 산출물 수명 관리. 원형 경계·이산 시간 모델이며 연속 충돌 검출이나 일반 강체 물리 엔진이 아니다. 다른 GPU 벤더의 실측은 없다. 과거 기준 바이너리2개의 정리는 자동 승인 검토에서 거부되어 실행하지 않았고 같은 삭제를 우회 재시도하지 않는다.


### 최종 로컬 배포 검증

공통 FBA 실행기의 NativeAOT 게시 경로에 BuildArtifactLifetime.ps1을 통합했다. 최대3개 보관, 소유권·완료 상태·전체 해시·활성 프로세스·경로 경계 확인과 실행 전 정리, 미등록/수정/실패/취소/실행 중 출력 보호, mutex 직렬화를 구현했다. 전용 검증13/13과 기존 실행기 인수7/7을 통과했다. 시험 바이너리는 결과와 소유권·정리 계획·해시를 보존한 후 제거했고 자식 프로세스 종료를 확인했다. 이전 삭제 거부 대상은 재시도하지 않았다. 실제 공통 실행기 게시의 완료 기록과 현재 출력3개 이내도 확인했다.

재게시된 최신 NativeAOT SHA256은4839615A2D412268B0DB6AF5DD3C42DB333E628FA9494AD584A4AA70EB89FB4A다. 동일한 C# 엔진/샘플 소스를 다시 게시한 실행 파일로 설정15회와 CPU/GPU 교대6회를 다시 측정했다. 알고리즘/5000개/Size6/속도/해상도/화질 조건을 유지했다. CPU849.066→GPU5448.071FPS(6.42배,+541.65%), median1.10910→0.16620ms,p951.42032→0.33793ms,p992.59723→0.49069ms,1%low325.517→1757.484FPS다. 원시 프레임21회와 실제 크기·설정·정상 종료를 감사했다. 새 실행 파일의 기본 계측 비활성 실행도 통과했다.

새 설정별 중앙값은 Render/cache5476.160,uncached5436.324,Display/cache4254.199,직접5657.287,정지7763.649FPS다. 배경 구성76.35→3.18µs, 동적 업로드78320→9200B이며, FPS 차이는약+0.73%다. 동적 캔버스 관리 비용은 직접 그리기 대비약3.20%로 기록한다. 프로필·정지 여부가 다른 결과를 같은 작업량의 개선율로 합산하지 않는다.

별도 상세 진단은 준비 구간 포함638로그 표본이며 최종 FPS비교에 포함하지 않는다. 중앙값은 frame fence58.3µs,acquire0.5µs,upload3.3µs,record34.0µs,submit11.2µs,present49.8µs,GPU143.456µs다. GPU와 CPU가 겹치므로 단순 합산하지 않는다. 그래픽 큐 compute는 구현됐고 별도 compute 큐 비동기 중첩은 미구현이다.

최종 근거: release-native-paired,release-profile-cache-suite,release-native-raw-audit.json,release-default-native-smoke-validation.json,release-diagnostic/stage-summary.json,build-lifetime-tests.json,release-lifetime-process-audit.json. 기존465검사 및 ERD회귀에 대응하는 엔진 소스는 변경되지 않았음을 해시로 확인했다. 최종 요구사항별 감사와 소스·실행 파일 대응은 release-completion-audit.json을 따른다.
