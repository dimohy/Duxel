# Common layer and widget optimization assessment

## Current high-load application and primitive-run candidate — 2026-11-06

Freeze the current Overlay application as a new baseline. It reuses five text labels and is not the2048-text memory probe. Its idle pilot had zero actual draws/uploads but approximately5..9ms UI Render versus2.46us GPU time.32768 swatches repeatedly update the same large immutable command. The common builder now accumulates count/bounds in four value fields and commits before other command accesses or storage replacement. Public command immutability,input,primitive count/colors,ownership and generated wiring remain intact. Additional builder fields and branches are costs,not free storage.

SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148,win-x64 NativeAOT Speed.128 palettes x256 swatches plus1800 background rectangles,actual1600x1000,MSAA1,VSync off,pipeline on,idle skip off. At least96 presentations/1s warmup then12s measured;three independent processes per variant per idle/motion in AB/BA/AB order:12 runs. Reuse `measure-overlay-layers.ps1`,app profiling and250ms memory sampling. No forced GC,workload or quality reduction.

| Metric | Idle before→candidate | Motion before→candidate |
| --- | ---: | ---: |
| FPS | 139.784→153.782 | 115.081→142.446 |
| Median frame ms | 7.0353→6.0980 | 7.0032→6.7267 |
| p95 ms | 8.2064→7.50848 | 10.52866→7.9197 |
| p99 ms | 9.272276→8.944864 | 66.191211→9.977248 |
| 1% low FPS | 53.532→47.081 | 13.528→51.340 |
| Private B | 180,019,200→180,150,272 | 182,091,776→180,436,992 |
| Working set B | 132,579,328→132,567,040 | 132,833,280→133,210,112 |
| GPU µs | 3.5683→3.7346 | 27.8728→29.3575 |

**All timing/memory run ranges overlap:application improvement remains unconfirmed.** Idle1% low median fell12.05%;motion working set rose0.284%;GPU medians rose4.66%/5.33%,also inconclusive. Do not combine these values with historical11/04 runs or attribute cross-checkpoint differences to this change. CPU diagnostics are sparse120-frame wall-clock samples including warmup,not exact measurement-phase accounting or process CPU time.

Both variants preserve idle128 reused/0 rebuilt lists,draw0/upload0;motion127 reused/1 rebuilt and66,512B upload/frame. Motion draw medians137.537→136.634 overlap. Allocation measured inside the sample callback stays0/608B for idle/motion;it excludes engine work outside that callback. Both binaries already contain the previous text-cache fix,so this does not resolve the2048-text short Private increase.

Managed and Native74 focused checks each pass;all100 GPU readback images match. Coverage includes run count/bounds,clip/callback boundaries,channels,queued-frame immutability,reset,MSAA1/4 gradients,popups/text and reference lifetime. This is neither official Fluent reference comparison nor desktop capture. Initial new-test channel names/IDisposable assumptions caused seven compile errors,corrected to the actual API and explicit ownership release without weakening criteria.

Evidence:`artifacts/highload-app-2026-11-06/verification.json`,`comparison.json`,`cpu-diagnostics.json`. Raw records,source copies/hashes and12 runs:`L:/Cache/Duxel/highload-app-2026-11-06/`. Only UiDrawListBuilder.cs differs in production sources. Lossless NTFS compression of20 existing debug-symbol files preserved all hashes and freed space;no files were deleted/moved. Whole-goal memory/performance remains open;resize is separately deferred. Development validation uses.NET11 only.

Date: 2026-11-04. Completing this evaluation, improvement and revalidation does not imply every product goal has been met on every device.

## Verdict

The direction matches the goal and the Windows common path is effective. Automatic skipping of arbitrary CPU UI composition and Raspberry Pi device performance are not established. Existing optimizations were preserved; this evaluation removed unnecessary streamed GPU uploads and changed a primitive command hot path to reference access.

| Goal | Verdict | Evidence and limit |
| --- | --- | --- |
| Ordinary widget automatic integration | Met | Common acquired-image damage applies without BeginLayer; stable controls have zero draws. |
| Static/dynamic presentation reuse | Met | Menus, popups, combos, tooltips and foreground share snapshots, GPU residency and dynamic transitions. |
| Skip all ordinary-widget CPU composition | Partial | ContentVersion/StaticCache opt-in retention and whole-app idle waiting exist. Requested-frame user Render, layout and input still execute. |
| Correct changed-region repaint | Met within supported scope | Image history, MSAA1/4, movement/removal/alpha, command/clip/texture changes and asynchronous cadence pass. Projected3D, callbacks and simulation payloads invalidate full images. |
| Reduce upload and CPU duplication | Met | Five stable widget screens now upload0 streamed bytes; rectangle/circle/rounded primitives access the current command by ref. No workload or shader change. |
| Throughput and tails | Partial | Idle FPS/median/p95 and motion p95 improved. Motion average, p99 and1% low ranges overlap. |
| Memory | Partial | Earlier Private reductions were re-audited from raw data. This change has no confirmed Private reduction and raises idle working set by about0.50MiB. |
| Raspberry Pi | Unverified | Evidence and the available backend are Windows x64. Linux/ARM64 backend, driver and device throughput are unproven. |

UI geometry retention, GPU residency and acquired-image history reduce different costs. VkPipelineCache opaque contents are driver-managed; Duxel persists them. That cache is distinct from the widget/presentation caches evaluated here.

## Findings and changes

Five stable screens had zero draws yet streamed2776/5296/4408/10320/6384 bytes per frame (radio/button/checkbox/slider/input). Damage was decided after geometry upload. Damage planning now precedes streamed upload/capacity growth; empty damage skips streamed writes, pipeline binds, compiled planning and draw traversal. Static-cache bookkeeping, texture invalidation, input/layout, acquire/submit/present and MSAA preservation remain. Full invalidation for projected3D/callbacks remains. No bitmap substitute, lower vector quality, sample-only branch or reduced load was used.

The first candidate eliminated57632B/frame idle uploads but regressed idle FPS382.07→374.21(-2.06%). Those raw results remain in comparison/ and iteration-1-review.md. CPU composition dominated, so AddRectFilledPrimitiveCommand was changed from copying a command value to ref access. The tables measure the combined final candidate; they do not isolate a single change or certify an exact machine-code copy reduction.

## Matched final measurements

SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, win-x64 NativeAOT Speed;1600×1000, MSAA1, VSync off, pipeline on, idle skip off,128 palettes×256 swatches. At least96 accepted presentations and1second warmup, then12seconds, three independent alternating runs. App/GC profiling every120frames was identical; no forced GC. FPS measures accepted presentation intervals, not physical display refresh.

The baseline already includes the FPS-flicker correction. Do not combine its results with the older pre-integration baseline. First and final candidates each retain12 separate process results.

### Idle

| Metric | Before | After | Observed change | Verdict |
| --- | ---: | ---: | ---: | --- |
| FPS | 381.678 | 403.149 | 5.63% | improved |
| median ms | 2.568 | 2.423 | -5.64% | improved |
| p95 ms | 3.012 | 2.860 | -5.03% | improved |
| p99 ms | 4.182 | 3.945 | -5.66% | inconclusive (overlap) |
| 1% low FPS | 216.333 | 227.360 | 5.10% | inconclusive (overlap) |
| draw calls | 0.000 | 0.000 | — | equal |
| dynamic upload B/frame | 57632 | 0 | -100.00% | improved |
| GPU µs | 4.149 | 4.799 | 15.67% | inconclusive (overlap) |
| Private MiB | 172.926 | 172.152 | -0.45% | inconclusive (overlap) |
| Working set MiB | 123.023 | 123.520 | 0.40% | increased/regressed |

### Motion

| Metric | Before | After | Observed change | Verdict |
| --- | ---: | ---: | ---: | --- |
| FPS | 372.826 | 395.349 | 6.04% | inconclusive (overlap) |
| median ms | 2.677 | 2.468 | -7.81% | inconclusive (overlap) |
| p95 ms | 2.996 | 2.887 | -3.64% | improved |
| p99 ms | 3.941 | 3.857 | -2.11% | inconclusive (overlap) |
| 1% low FPS | 220.256 | 219.432 | -0.37% | inconclusive (overlap) |
| draw calls | 137.173 | 137.133 | -0.03% | inconclusive (overlap) |
| dynamic upload B/frame | 66512 | 66512 | 0.00% | equal |
| GPU µs | 35.859 | 34.727 | -3.16% | inconclusive (overlap) |
| Private MiB | 175.543 | 175.551 | 0.00% | inconclusive (overlap) |
| Working set MiB | 124.629 | 124.977 | 0.28% | inconclusive (overlap) |

Full ranges, audited raw frame CSVs and active-window memory CSVs are in comparison-ref/. The idle working-set increase is retained; Private reduction and motion average-FPS improvement are not confirmed. No new cache or image copy was introduced.

Earlier12-second raw measurements were re-audited with the existing summary tool into reused-performance/summary.json. That historical stage reduced Private2.17% idle/3.02% moving and draws1031→0/137.262. Those figures are not added to this candidate's percentages.

## CPU, allocation and memory

Sampled per-run app-profile medians, then median over three runs, show UI render2525.95→2412.80µs idle and2672.50→2401.25µs moving; renderer570.20→517.80µs idle and664.80→592.90µs moving. These samples after UI frame600 are diagnostic, not phase-exact CPU accounting. Damage planning moved from record into geometry/upload preparation; isolated phase labels cannot establish a speed gain.

No generation-count increase occurred between the observed GC-profile endpoints. This is not an app-wide no-GC or zero-allocation claim. Palette Screen.Render allocation was0; ordinary showcase composition still allocates several KiB per screen. Private, working set, managed heap and upload bandwidth are separate metrics. Future CPU work must preserve arbitrary Render side effects and input semantics.

Total GPU VRAM residency was not measured. Zero uploaded bytes does not mean existing GPU buffers were freed. Working-set differences over equal time with more completed frames are retained without interpreting them as proof of a leak or reduced cache size.

Lower bandwidth alone does not establish Raspberry Pi suitability. The current renderer requests buffer device address, descriptor indexing/update-after-bind and dynamic rendering, and configures4096texture descriptors. Target-driver feature/limit checks and Linux/ARM64 platform/text-backend validation are required. These Windows results establish neither support nor lack of support on a specific Pi device.

## Validation and preservation

- .NET10.0.9 and pinned RC3:717 full checks each; Release net8/net9/net10 solution build:zero warnings/errors.
- Ordinary widgets, child wheel scrolling and own native-window resizing:70 checks on each runtime. Scroll0→211.6875; framebuffer1100×740→884×641 invalidates the full image. Input uses UiInputState, not physical mouse automation.
- FPS held16frames/NF changed every3frames, held button and128 synchronous/128 in-flight comparisons:386 checks. Geometry/command-state regression remains.
- All317 previous .NET10 images match. All37 showcase windows and six interactions passed after the renderer change; later ref access was covered by full geometry regression and image parity.
- Clip-only changes resume uploads after empty damage and match full repaint. Readback now resolves resized target dimensions before allocating its buffer.
- Preserve unrelated runtime/shader/sample edits. Final audit records six runtime and four focused-test changes against the initial snapshot. No retry of protected/denied deletions; earlier22 targets and manual cleanup remain in [cleanup guidance](rejected-build-cleanup.md).

## Reproduction and identity

Evidence root:artifacts/optimization-evaluation-2026-11-04/. The18 evaluation milestones are work completion, not a claim that every row above is Met. Native output: native-ref-rc3/net11.0-windows7.0/overlay_layer_lab.exe. SHA256:A7C5994C1A8B7E31F511DFA9E3409C90190923E2CCF22E11EF2AF8711C9354F1. Baseline SHA256:3B5C4C972CD17936E0E9D28177AA122662B51F41EF3B420B6322F4A169EFA465.

See the [paired Korean report](optimization-evaluation.ko.md#실행과-추적) for exact PowerShell commands, including the existing --widget-optimization-only fixture path and measure-overlay-layers.ps1 with Runs3/Seconds12/Counts128/Swatches256. Keep the three app-profile environment variables identical to comparison-contract.json. If temporary fixture IL is gone, rebuild it with run-fba.ps1 -NoCache -NoLaunch and use the new publish log path. RC3 publication/tests use toolchain/global.json; repository global.json remains unchanged. The earlier .NET10 all_features executable is historical and was not republished as this RC3 overlay artifact.
