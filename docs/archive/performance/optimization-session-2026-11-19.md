# 2026-11-19 SVG browser comparison scroll

## SVG replacement at the raster cache limit

A 3,000×3,000 SVG preview uses 36,000,000 bytes in the context's logical RGBA budget. The 3,200×3,200 replacement uses 40,960,000. The previous code rejected the replacement when it could not retain both at once. It did so even when the widget was the sole owner of the old texture.

The cache now prepares the candidate source before queuing retirement of an exclusive, earlier-frame texture. The cache remains limited to 128 entries and 64 MiB of logical residency. Shared leases, currently used entries and retained-layer references keep their protection. A preparation failure leaves the previous texture reusable. The frame queue keeps the old and new texture identities separate.

The Windows .NET 11 SVG test uses the two large logical dimensions without GPU allocation. It reproduces the original exception and passes after the fix. GPU tests use a full 128-entry cache of smaller vector textures to check ordered playback of the old frame and the widened replacement. These are behavior and ownership checks. Frame timing and peak native memory were outside this comparison. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/svg-replacement-2118`.

The final Windows Release build passed without warnings. SVG passed 138 checks. The GPU lifetime suites passed 20 checks at one slot, 20 at three slots, and 22 for the rounded case at three slots. Gallery SVG passed 17 and ordered input 597. Fresh Linux x64 .NET 11.0.0 NativeAOT passed 109 Gallery scenarios on WSLg, with all 109 matched to accepted Vulkan sequences and both lockfiles unchanged.

## First partial repair during an Arc zoom gesture

**Hypothesis.** The first partial repair was compiling the rectangle and circle comparison loops as one method. Separating the loops could reduce first-use CPU cost without changing which recorded drawing commands need repair.

**Change.** Sparse primitive repair checks shared command properties, then compares the rectangle or circle records as needed. Unsupported rectangle variants exit before the shared comparison. Both loops apply the same clip, framebuffer conversion and bounds union. The refactor adds no cache or buffer.

**Verification.** Windows x64, SDK 11.0.100 and managed runtime 11.0.0. The same Graphics fixture at 1280×900, 4× MSAA and V-Sync off ran `gallery-svg-pointer arc win32 burst highlight` with per-submission Vulkan profiling. The first 96 frames warmed the fixture. Each gesture used 33 batches of 16 moves. Three fresh processes per build ran in base, candidate, candidate, base, base, candidate order. The module-only rerun fixed every fixture assembly except `Duxel.Vulkan.dll`. All 396 measured moves matched their Vulkan frames, repair rectangles, transform values and scheduled input steps. The six settled PNGs matched. A separate pair series using complete builds confirmed the direction of the first-repair result.

| Module-only replay, median of independent runs | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Zoom to accepted Vulkan submission | 2.909 → 2.882 | 2.589 → 2.549 | 6.412 → 6.044 | 9.327 → 9.022 | 343.8 → 347.0 | 107.2 → 110.8 |
| Rotation to accepted Vulkan submission | 2.508 → 2.551 | 2.428 → 2.481 | 2.868 → 3.141 | 2.977 → 3.493 | 398.8 → 391.9 | 335.9 → 286.3 |

The second zoom move triggers the first partial repair in this replay. Its CPU repair planning fell from 1,575.4 µs to 1,357.6 µs (−13.8%), with disjoint ranges of 1,473.0–1,609.6 and 1,328.3–1,367.2 µs. In the comparison of complete builds it fell from 1,461.3 to 1,360.6 µs (−6.9%). Zoom and rotation submission p95 ranges overlapped in the module-only rerun. This supports a first-use CPU improvement and leaves whole-gesture timing inconclusive. The repair area did not shrink. The replay did not sample process-memory peaks or physical display latency.

Windows Release solution and Graphics built with zero warnings. Repaint 134, presentation reuse 3,774 and Gallery SVG 17 passed. The fresh Linux x64 NativeAOT .NET 11.0.0 build passed 109 WSLg Gallery scenarios and each reported presentation matched an accepted Vulkan sequence. Both package locks matched. This verifies Linux behavior, and the timing comparison covers Windows managed only. Raw traces, comparisons and build logs are under `W:/Cache/Duxel/gallery-source-20261117/arc-repair-2115`.

## Shared code view work during Arc zoom, rotation and selection

**Hypothesis.** The Arc page lays out its source samples on a transform and revisits those samples during code selection. The code surface was measuring its wrapped text after its parent had already provided exact bounds, including for clipped samples.

**Change.** The surface uses its assigned dimensions directly when both axes are fixed. It prepares the editor and its typography only when its clip is visible. Its natural measurement still determines wrapped height for loose constraints. Line-number gutter measurement passes its numeric string directly to the existing text measurement path. No cache or buffer was added. The image phase change was reverted after the ordered-input suite confirmed that the existing contract delivers texture updates created during intermediate input. The SVG telemetry experiment was also removed after its paired zoom p95 rose from 6.28 to 6.62 ms.

**Verification.** Windows x64, SDK 11.0.100, managed runtime 11.0.0, 1280×900, 4× MSAA, V-Sync off. Replayed `gallery-svg-pointer arc win32 burst highlight` and `gallery-code-selection arc` in three fresh processes per build and per mode, in the order base, candidate, candidate, base, base, candidate. Each transform has 96 initial warmup frames and 33 measured batches of 16 moves. Each selection mode has 96 initial warmup frames, another 24 for that mode and 160 measured frames. The columns use medians of independent runs. Pointer times end at accepted Vulkan submission. Selection times combine UI and renderer stages.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate | UI allocation per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Zoom | 2.855 → 2.833 | 2.562 → 2.503 | 6.245 → 6.235 | 8.748 → 8.679 | 350.2 → 353.0 | 114.3 → 115.2 | 34,572 → 33,373 B (−3.5%) |
| Rotation | 2.529 → 2.503 | 2.467 → 2.423 | 3.074 → 3.176 | 3.100 → 3.265 | 395.5 → 399.5 | 322.6 → 306.3 | 38,042 → 36,890 B (−3.0%) |
| Code selection, one move | 2.022 → 1.832 | 2.005 → 1.776 | 2.404 → 2.138 | 3.106 → 2.695 | 494.5 → 545.8 | 237.9 → 359.1 | 13,535 → 12,927 B (−4.5%) |
| Code selection, 16 moves | 3.322 → 3.102 | 3.292 → 3.133 | 3.933 → 3.633 | 4.628 → 4.853 | 301.0 → 322.4 | 213.3 → 167.1 | 26,571 → 25,212 B (−5.1%) |

All 12 replays produced the same final pixels per scenario. Gesture values, selection lengths, input handling and reported repair sizes matched. Both transform gestures requested zero SVG textures during measured moves. Zoom made one full acquired-image repair per run and rotation made none. The median transform repair covered 711,144 pixels out of 1,152,000. The p95 timing ranges overlap in all four cases, so the runs establish lower allocation without establishing an interaction speedup. Median sampled private-memory peaks for 16-move selection were 303.09 → 303.22 MiB, with overlapping ranges. This sample does not measure a lifetime memory peak.

The .NET 11 Release solution and Graphics builds passed without warnings. Gallery 326, SVG 17, ordered input 597 and repaint 134 passed. A freshly published Linux x64 NativeAOT .NET 11.0.0 Gallery completed 109 WSLg Vulkan checks with both package locks unchanged. NativeAOT speed and physical display latency were outside this replay.

Three additional instrumented Windows runs joined all 198 transform moves to their Vulkan submissions. In each run the first zoom move needed a full acquired-image repair and spent 4.09–4.64 ms on UI composition. The second was a partial repair and spent 4.47–4.61 ms in the renderer. The CPU repair-planning component took 1.44–1.52 ms, compared with 0.09–0.10 ms on the following move. Each of those second and third moves also redrew five diagnostic strips covering 6,800 pixels. They are distinct from the actual content repair. This separates a first-use planning cost from steady right-pane redraws. Raw replays, comparisons and the joined Vulkan profile are under `W:/Cache/Duxel/gallery-source-20261117/arc-common-2114`.

## Direct timing of Gallery sections during repeated zoom

The Windows Graphics diagnostic `gallery-svg-cold-warm input-profile` records navigation, toolbar and selected-page time for the input and presentation passes. This instrumentation is compiled only into the Graphics test project. Six .NET 11.0.0 Windows x64 processes alternated dynamic PGO on and off at 1280×900, 4× MSAA, V-Sync off. All final images and motion states matched.

| Steady zoom in the repeated session, median across three processes | PGO on | PGO off |
| --- | ---: | ---: |
| Input pass navigation / toolbar / page | 258 / 76 / 550 µs | 98 / 36 / 223 µs |
| Presentation pass navigation / toolbar / page | 345 / 87 / 722 µs | 102 / 32 / 259 µs |
| Total Core composition per frame | 2,710 µs | 988 µs |
| Core composition outside those six sections | 650 µs | 217 µs |

The selected page shows the largest difference. Both passes and every measured section take longer with PGO enabled. The Core remainder includes work before and after the Gallery callback. Each row reports its own median. The experiment locates the extra time in this managed replay, and the production renderer and runtime settings remain as they were. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-sections-2111/sections.json`.

## Visible Linux SVG browser comparison

The existing `x11-gallery-svg-pixels` probe now scrolls the right-hand SVG preview into view after setting zoom 2 and rotation 37 through AT-SPI. It requires painted red pixels in the viewport to distinguish the actual vector from its white background. The initial transformed viewport had zero matching red pixels. The scrolled viewport had 88,913 on both runtimes.

The new Linux x64 .NET 11.0.0 managed image and the hash-verified NativeAOT image from the 109-presentation run each passed five external client checks on WSLg and exited normally. Two XGetImage captures per state produced six cross-runtime pairs that match below the diagnostic header. The two scrolled captures within each runtime straddle the scrollbar fade and match in the page body. The only non-header differences within each pair occupy the right scrollbar strip. This verifies the displayed Duxel output across runtimes. Independent Chromium pixel equivalence, ARM64 hardware and input latency remain separate. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-sections-2111/linux-scrolled-pixels.json`.

## Repeated Arc zoom sampling with managed runtime controls

Six .NET 11.0.0 Windows x64 processes ran the same `gallery-svg-cold-warm input-profile` binary, alternating dynamic PGO on and off. At 1280×900, 4× MSAA and V-Sync off, all six final PNGs matched. Samples were joined to the measured UI-build intervals of the 30 steady zoom moves in the repeated session. There were 134 test-thread samples with PGO on and 50 with it off. Median composition per frame was 2.698 and 0.882 ms, respectively.

| Overlapping stack scopes in the traced intervals | PGO on | PGO off |
| --- | ---: | ---: |
| Allocated child rendering | 99 | 34 |
| SVG comparison body | 36 | 12 |
| Navigation rendering | 31 | 7 |
| Accessibility frame completion | 20 | 10 |
| Direct text lookup | 19 | 8 |

These are inclusive sample counts and cannot be summed. The sampled top symbol was `Buffer.BulkMoveWithWriteBarrier` in 129 and 45 samples under many different callers. This does not reliably identify a particular copy operation as the cause. A separate experiment eliminated a duplicate item-state copy in allocated child rendering. Three alternating before/after pairs preserved all gesture states, repair rectangles and twelve images. Repeated zoom composition was 2.737 → 2.680 ms with overlapping ranges. First zoom UI build was 3.985 → 4.431 ms. The edit was reverted. Its isolated Release build, transform, margin, ordered input and Gallery checks passed.

Another three pairs used PGO off on both sides and compared tiered compilation enabled versus disabled. At the same settings, all twelve PNGs matched. Disabling tiered compilation reduced steady repeated UI time from 1.076 to 0.609 ms but increased first zoom message-to-accepted-present from 8.159 to 12.530 ms in the first session. Steady allocated bytes per frame matched by session. These runtime controls show a tradeoff and leave the product settings unchanged. They describe the managed replay on this host, with no result for physical input, whole-process memory, or NativeAOT speed. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-pgo-2110`.

## Arc zoom in first and repeated Gallery sessions

`gallery-svg-cold-warm` runs the same Win32 input replay twice in one Windows x64 .NET 11.0.0 process. It creates a fresh Gallery, window and Vulkan renderer for each session at 1280×900, 4× MSAA and V-Sync off. All three default pairs preserve the same inputs, transforms, repair rectangles and final PNG. The optional `input-profile` argument writes Core's existing preparation, composition and finalization measurements by draw-data frame.

| Median of three default processes | First session | Repeated session |
| --- | ---: | ---: |
| First zoom move, UI pass | 4.121 ms | 4.067 ms |
| First zoom move, UI allocation | 93,848 B | 67,496 B |
| First zoom move, Win32 message to accepted presentation | 8.532 ms | 5.735 ms |
| Second zoom move, renderer call | 3.149 ms | 1.535 ms |
| Steady zoom moves, UI pass | 1.826 ms | 2.735 ms |

One joined EventPipe trace observed first-use JIT for ordered input and input-only drawing helpers during the first session's first zoom. Those specific JIT starts were absent in the repeated session. UI time for the first zoom still overlapped between sessions. This setup also reuses managed process state, so it cannot attribute all changes to JIT. Three independent pairs on the same executable, alternating dynamic PGO enabled and disabled, reproduced higher steady composition in the repeated session with PGO enabled (2.687 ms vs 0.957 ms with PGO disabled). All twelve final images matched. Runtime settings were kept as diagnostic controls. Production settings were not changed.

A separate attempt set the input phase before resetting existing builders, eliminating the subsequent empty-command cleanup. Three paired Windows replays preserved pixels, inputs, repair and selection. First zoom UI time was 4.135 → 4.178 ms, and allocation stayed at 93,848 B. The change was reverted. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-input-preparation-2109`.

Three pairs with `input-profile` put first-session zoom move time at 0.179 ms preparation, 3.240 ms composition and 0.016 ms finalization. Steady composition rose from 1.744 to 2.552 ms between sessions. All motion frames completed two input traversals for sixteen queued moves and kept the same final image. Composition includes application callbacks, layout and paint generation. It is now the priority for a stack comparison under both PGO settings. The diagnostic Release build had zero warnings. Ordered input, repaint and Gallery SVG checks passed.

## Vulkan frame correlation during Arc zoom

With Vulkan profiling enabled, submitted-frame records now identify the draw-data frame, actual submission, presentation sequence, acquired image, acceptance and MSAA state. The rendering metrics capture the presentation timestamp before writing the profile log. The GPU timestamp on each line still refers to the completed submission named by `gpuSubmission`. It can belong to an earlier frame.

Three alternating Windows x64 .NET 11.0.0 pairs replayed 33 zoom and 33 rotation moves per run at 1280×900, 4× MSAA and V-Sync off. Partial repair and forced full repaint ran from the same instrumented executable. All 396 motion frames matched by draw-data ID, present sequence and repair rectangle, and all six final PNGs matched.

| Median of the per-run zoom p95 values | Partial repair | Forced full repaint |
| --- | ---: | ---: |
| Win32 message to accepted presentation | 5.58 ms | 2.93 ms |
| Acquired-image repair planning on CPU | 667.0 µs | 0.2 µs |
| GPU render for the matching submission | 45.6 µs | 59.0 µs |
| CPU present call | 444.2 µs | 474.8 µs |

The slowest partial zoom frame rendered an uninitialized image and spent 4.06–4.16 ms in UI composition. The next frame repaired the right pane and spent 0.93–0.99 ms planning repair. Its acquire call took 2.9–3.3 µs and the matching GPU render took 42.5–45.4 µs. One joined EventPipe trace observed the render thread compiling the comparison and primitive-damage helpers on first use in that second frame. This accounts for part of the spike. It does not assign all elapsed time to JIT or represent display latency.

In three further pairs with partial repair on both sides, forcing early optimization of the comparison method raised the second frame's median repair planning from 0.94 to 1.48 ms. Zoom p95 rose from 5.38 to 6.08 ms. The hint was reverted. Neither MSAA nor the default partial-repair policy changed. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-msaa-2108`.

Windows Release Graphics built with zero warnings, and 3,774 presentation reuse, 134 repaint and 17 Gallery SVG checks passed. Linux x64 .NET 11.0.0 NativeAOT passed 109 WSLg scenarios. All of their presentation sequences have a matching accepted profile entry. The verifier acknowledged the same draw in 97 scenarios and a reused earlier presentation in 12.

## Separate SVG and code updates after a gesture

The comparison Gallery shows the exact SVG first when a transform settles. It synchronizes up to two C# examples per following frame. Event-wait mode gets one deadline for each pending batch. A new transform restarts pending work against the latest values. This uses the existing examples and rendering paths without a new render cache.

Three fresh Windows x64 .NET 11.0.0 processes per build alternated `gallery-svg-pointer arc win32 burst` followed by `gallery-code-selection arc`. The fixture used 1280×900, 4× MSAA and V-Sync off.

| Median of three runs | Before | After | Change |
| --- | ---: | ---: | ---: |
| Peak UI composition per settling frame | 13.31 ms | 5.26 ms | −60.4% |
| Peak UI allocation per settling frame | 6,681,952 B | 2,187,928 B | −67.3% |
| Total UI composition until all code is current | 13.31 ms | 16.63 ms | +25.0% |
| Total UI allocation over settling frames | 6,681,952 B | 6,731,344 B | +0.7% |
| New Vulkan presentations during settling | 1 | 1 | No change |
| SVG render requests during settling | 1 | 1 | No change |

The final image and code-selection PNGs matched across all six runs. The code converged in three extra composition frames, all of which reused the existing Vulkan presentation at this zoom and scroll position. Zoom, rotation and selection timing ranges during the gestures overlapped. Median sampled private-memory peaks during the 16-move source-selection workload were 303.89 and 303.55 MiB; those samples do not measure process-wide peak memory while settling. The test covers a managed Windows fixture and does not measure physical pointer or monitor latency. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-source-2107/final`.

Windows Release Gallery and Graphics built without warnings. The Gallery passed 105 Vulkan presentation steps, with 326 Gallery, 17 SVG, 134 repaint and 132 component source checks in the focused fixtures. A freshly published Linux x64 .NET 11.0.0 NativeAOT Gallery passed 109 WSLg presentation steps. Both package locks matched their pre-publish hashes.

## Arc comparison with the example source visible

Windows x64, .NET 11.0.0, 1280×900, 4× MSAA, V-Sync off. Each variant ran in three fresh processes in alternating order. `gallery-svg-pointer arc win32 burst` sent 33 batches of 16 Win32 moves per transform and then waited for the final high-resolution render and example source. `gallery-code-selection arc` replayed 160 frames each for one-move and 16-move selections. Across both comparisons, all endpoint PNG hashes, transform and selection states, and measured repair rectangles matched.

| Median of three runs | Baseline | Ordered style traversal and owned glyph arrays | Baseline rerun | Ordered style traversal only |
| --- | ---: | ---: | ---: | ---: |
| UI composition after the gesture | 13.16 ms | 21.10 ms (+60.4%) | 13.20 ms | 15.18 ms (+14.9%) |
| UI allocation after the gesture | 6,681,952 B | 5,815,896 B (−13.0%) | 6,681,952 B | 6,681,952 B |
| Zoom to Vulkan submission, p95 | 4.90 ms | 5.03 ms | 5.10 ms | 5.03 ms |
| Rotation to Vulkan submission, p95 | 2.97 ms | 3.03 ms | 3.00 ms | 2.97 ms |
| 16-move selection, UI plus renderer p95 | 3.80 ms | 3.79 ms | 3.73 ms | 3.76 ms |

Both experiments were removed because they made the settling frame slower. Each zoom run needed one full acquired-image repair. Rotation and code selection needed none. No SVG texture was generated during the measured drag moves. After the gesture, six code examples were reparsed and one visible SVG texture was requested. These are separate costs in the same page. The Windows Gallery replay does not measure physical input or monitor latency. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-joint-2106`.

Three additional pairs used the baseline executable and changed only Vulkan partial repair. With partial repair, the median dirty area for both drags was 711,144 of 1,152,000 pixels (61.7%). Zoom message-to-submission p95 was 5.01 ms with partial repair and 3.09 ms with full repaint. Rotation was 3.12 and 2.87 ms, with overlapping run ranges. The final PNG hashes were identical. A separate profiled pass put partial-repair snapshot CPU p95 at 228 µs and repair planning p95 at 62 µs across submitted frames. Those figures do not account for the entire zoom gap or isolate swapchain presentation. Full repaint was a comparison control. The default remains partial.

Three pairs comparing the visible highlight switch kept the same repair sizes and full-window counts. Zoom p95 was 5.11 ms with the switch off and 6.06 ms with it on. Rotation was 3.02 and 3.00 ms. Final images differed only at the green outline and the switch. The fixed replay samples a hidden Vulkan fixture and does not establish a general overhead for the desktop compositor.

## Gallery Tooltip process memory on WSLg

The earlier process sampling used Linux x64 .NET 11.0.0 NativeAOT, with one run per build and the same AT-SPI sequence. Both apps opened and closed the Tooltip 56 times and exited normally. The 48 measured cycles ran for about 61.7 seconds per build. Sampled private memory peaked at 558.66 MiB before and 561.99 MiB after the temporary-only scratch change; RSS peaked at 586.93 and 589.86 MiB. Median private memory between the first and last sampled thirds fell 5.16 and 1.86 MiB respectively. This pair alone cannot attribute the difference to scratch allocation policy. The same candidate also confirmed 109 Vulkan presentation steps on WSLg. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/gallery-popup-memory-2105`.

## Fluent popup raster scratch storage

The external custom-container probe attributed 1,277,472 bytes of a recurring Tooltip-open frame to Fluent paint and 5,240 bytes to placement. The common Tooltip and popup rasterizer created six full-size temporary float arrays in addition to the owned RGBA image. The first candidate reused intermediate spans from one scratch allocation and kept one buffer per size bucket for surfaces up to 65,536 pixels. The retention controls below supersede that pool policy. The published RGBA buffer has independent ownership in every variant.

Linux x64 .NET 11.0.0 NativeAOT, SDK 11.0.100, WSLg llvmpipe, 820×520, V-Sync off, 96 warmup and 160 measured frames per run. Three fresh processes per side ran baseline, candidate, candidate, baseline, baseline, candidate. All 15 measured openings per side produced the same allocation result. Both modes held the same state-change cadence and ran with App and Vulkan profiling enabled.

| Measurement | Before | After | Change |
| --- | ---: | ---: | ---: |
| UI render allocation per opening | 1,293,904 B | 200,608 B | −84.5% |
| Mean UI allocation per measured frame | 44,627 B | 10,461.5 B | −76.6% |
| Peak observed GC committed | 5.30 MiB | 6.13 MiB | +0.83 MiB |
| Mean frames/s | 408 | 435 | Run ranges overlap |
| Frame interval p50 / p95 / p99 | 1.341 / 5.809 / 20.550 ms | 1.267 / 6.399 / 17.704 ms | Run ranges overlap |
| Slowest 1% frames/s | 47.5 | 45.0 | Run ranges overlap |

Vulkan draw calls and dynamic upload stayed at 429 and 21,760 bytes per run. The scratch pool reduces transient work at the cost of increased committed GC memory in this short run. No throughput or whole-process memory improvement was established. Windows Release solution and .NET 11 Graphics builds passed without warnings; multiline 4,115, Tooltip GPU 792 and Popover GPU 452 passed. Linux managed and NativeAOT each matched the previous four 820×520 X11 exercise images. The first NativeAOT capture reached its exit deadline and was rerun with the same build and a longer window lifetime. Evidence `W:/Cache/Duxel/gallery-source-20261117/tooltip-raster-2101`.

### Mixed-size popup raster validation

A separate Linux x64 NativeAOT executable linked each archived Core validation assembly. Six surfaces covered light and dark palettes, a fractional arrow at 125%, and raster sizes on both sides of the scratch-pool limit. Three fresh processes per variant alternated baseline, candidate, candidate, baseline, baseline, candidate. Each process warmed up 16 cycles and measured 64 cycles. All 2,304 measured renders matched their shape's exact byte hash, and hashes also matched across builds.

| Raster size | Before allocation | After allocation | Reduction |
| --- | ---: | ---: | ---: |
| 144×116 | 468,264 B | 67,224 B | 85.6% |
| 241×181 | 1,222,048 B | 174,976 B | 85.7% |
| 193×236 | 1,275,896 B | 182,600 B | 85.7% |
| 256×256 | 1,835,560 B | 262,552 B | 85.7% |
| 257×256 | 1,842,728 B | 1,316,272 B | 28.6% |
| 337×277 | 2,614,608 B | 1,867,672 B | 28.6% |

The limit transition is visible between 256×256 and 257×256: the larger raster keeps no pooled scratch. Per-shape median raster times decreased 4.0–7.7% with disjoint run ranges. These timings isolate the CPU rasterizer, with no window or renderer instance. The full application timing ranges in the preceding study still overlap. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/tooltip-pool-2102/final`.

In a separate 65-second continuous run per build, the baseline rendered 5,388 rasters and the candidate 5,838. No forced collection was used. With five-second process samples, peak private memory rose from 9.45 to 25.77 MiB, and peak RSS from 19.35 to 35.70 MiB. The candidate's private memory fell between peaks, ranging from 14.53 to 25.77 MiB after 30 seconds. This is one time-matched run per build with unequal completed work. It exposes a resident-memory regression under mixed sizes; the memory source and long application behavior still require a separate control.

An equal-work follow-up separated poolable sizes from larger sizes and tried splitting the unpooled scratch array into four pass buffers. Each Linux x64 .NET 11.0.0 NativeAOT process warmed up 16 cycles, rendered 128 measured cycles, and sampled process memory once every four cycles. All 9,216 measured rasters preserved their shape hashes. The pooled-only and above-limit cohorts ran once per build; the mixed cohort ran three times per build in rotating order.

| Size cohort | Original sampled private peak | Pooled, contiguous oversized scratch | Pooled, split oversized scratch |
| --- | ---: | ---: | ---: |
| Poolable sizes only | 9.85 MiB | 16.05 MiB | 16.10 MiB |
| Above-limit sizes only | 8.29 MiB | 7.00 MiB | 7.11 MiB |
| Mixed sizes, median of three runs | 9.02 MiB | 20.67 MiB | 20.21 MiB |

The pooled-sized workload reproduced the increase without any oversized scratch allocation. Splitting the oversized arrays leaves the dominant increase in place, so that experiment was removed from the product. The measurements identify the workload that triggers the higher peaks. They do not measure buffers retained by the pool separately from GC heap behavior. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/tooltip-pool-2103`.

### Popup scratch retention controls

Four variants used the same Linux x64 .NET 11.0.0 NativeAOT driver and the same mixed-size operations. The baseline used the original Core rasterizer. The three controls used the current rasterizer with the size-bucket pool, a single retained buffer, or temporary scratch only. Each variant ran three fresh processes per workload with 16 warmup and 96 measured cycles, with memory sampled every four cycles and no forced GC. All 11,520 measured renders retained the same six output hashes.

| Mixed-size control | Median sampled private peak | Allocated bytes per cycle | Gen 2 collections during sampled interval |
| --- | ---: | ---: | ---: |
| Original code | 9.25 MiB | 9,259,104 | 230 |
| One buffer per size bucket | 18.21 MiB | 3,871,296 | 92 |
| One retained buffer | 18.33 MiB | 3,871,296 | 92 |
| Temporary scratch only | 7.09 MiB | 6,613,936 | 184 |

The single-buffer control retained less scratch yet kept a similar private-memory peak and collection count. The product now uses temporary scratch only. It still reuses the intermediate spans between shadow passes, and the original-to-final allocation reduction in the mixed-size raster probe is 28.6%. Windows Release and Graphics builds were clean. Multiline 4,115, Tooltip GPU 792 and Popover GPU 452 passed. On WSLg, managed and NativeAOT each matched all four original exercise-state X11 images and exited normally. Actual application memory and timing require separate long-running measurements. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/tooltip-pool-policy-2104`.

## External custom-container steady and changing profiles

The external sample previously matched the four rendered states across runtimes. This measurement isolates the cost of continuing to change the Footer, Inset and Tooltip state. The same Linux x64 .NET 11.0.0 NativeAOT executable ran at 820×520 on WSLg llvmpipe with V-Sync off. Each of six fresh runs had 96 identical warmup frames and 160 measured frames. The steady mode held the final warmup state. The changing mode ran the existing sample callbacks every eight frames. Run order was steady, change, change, steady, steady, change. Both profile streams were enabled throughout.

| Measurement | Steady | Changing |
| --- | ---: | ---: |
| Mean frames/s | 1,019 | 606 |
| Frame interval p50 / p95 / p99 | 0.953 / 1.256 / 1.455 ms | 0.973 / 3.898 / 14.993 ms |
| Slowest 1% frames/s | 653 | 66.2 |
| CPU UI p95 | 48.6 µs | 124.0 µs |
| Renderer call p95 | 4.12 µs | 2,647 µs |
| Mean UI allocations per measured frame | 672 B | 44,627 B |
| Vulkan-log dynamic upload over full run | 8,160 B | 21,760 B |

All 15 changing-mode Tooltip opening frames allocated 1,293,904 bytes inside UI rendering. This gives the next allocation trace a reproducible starting point. The p50 interval ranges overlap. The profiling data include logging overhead and describe one software-rendered workload. Vulkan upload sums also include warmup. There is no engine before/after comparison, GPU timestamp measurement or physical-display timing. SDK 11.0.100, builds and runs succeeded. Evidence and replay script are under `W:/Cache/Duxel/gallery-source-20261117/container-perf-2100/final`.

## Versioned layer metadata allocation

Successive revisions of the same layer reuse its capture-record array when the list count is unchanged and keep each slot's geometry key. Each revision still publishes independent command and geometry data under the existing frame lease. The isolated Windows .NET 11 managed probe rendered one changed layer per frame after 128 warmup frames, with 1,024 samples and three fresh processes per build in alternating order. Allocation fell from 1,784 to 1,640 B per measured frame in all repetitions (144 B, 8.07%). This was a thread-local allocation diagnostic. Full-app timing, process memory and NativeAOT performance were outside its scope.

Run `dotnet <Duxel.Graphics.Tests.dll> <fresh-output> presentation-reuse 3` for the pixel and lease check. The current managed build passed 3,778 checks at one and three slots. All 642 shared three-slot RGBA pairs and reuse decisions match the baseline. The newly asserted key identity and independent held-frame commands pass at 1× and 4× MSAA. Build, probe source, per-run hashes and results are under `L:/Cache/Duxel/layer-metadata-2138`.

## External custom-container NativeAOT build comparison

The external consumer used identical sample source, SDK 11.0.100, runtime 11.0.0, WSLg llvmpipe, an 820×520 viewport, and V-Sync off. The previous and current builds each ran in three fresh processes in previous/current/current/previous/previous/current order. Each run had 96 warmup frames and 160 measured frames with the same Footer, Inset and Tooltip callbacks every eight frames. Both profiling streams were enabled. This comparison measures the accumulated changes between the two binaries.

| Median across runs | Previous | Current |
| --- | ---: | ---: |
| Mean FPS | 566.4 | 560.6 |
| Frame p50 / p95 / p99 | 1.079 / 4.164 / 14.879 ms | 1.101 / 4.066 / 14.567 ms |
| Slowest 1% FPS | 67.0 | 67.0 |
| UI allocation per measured frame | 44,627 B | 33,236 B |
| UI allocation per Tooltip opening | 1,293,904 B | 929,400 B |
| Renderer call allocation per measured frame | 1,557 B | 1,617 B |
| Profiled draw calls / dynamic uploads including warmup | 429 / 21,760 B | 429 / 21,760 B |

UI allocations fell 25.5% per frame and 28.2% per Tooltip opening. Renderer call allocations rose about 3.9%. Frame time ranges overlap and process memory and physical display latency were outside the measurement. Replay with `bash run.sh`, then `python3 verify.py <evidence-directory>`, using a fresh output directory as set in the script. The run script, pinned binaries' hashes, raw profiles and summary are under `W:/Cache/Duxel/gallery-source-20261117/container-integration-2131`.

## SVG browser scroll profile

Changing the visible rectangle on each scroll position generated an exact camera texture repeatedly. The comparison page now detects changes in its child content origin before painting and keeps the existing fixed-resolution GPU preview active. It schedules one exact frame after 120 ms with no position changes. This uses the same setting as the transform controls. Source code refresh retains its own timer.

Use the Release graphics test executable built with SDK 11.0.100 and `-p:DuxelBenchmarkFramework=net11.0-windows`. Run `dotnet Duxel.Graphics.Tests.dll <output> gallery-svg-profile`. The Windows Vulkan fixture uses .NET 11.0.0, a 1280×900 viewport, and 16 warmup plus 40 measured frames per transform mode before settling the 1.75× zoom and beginning the scroll sample. Each run feeds 12 identical wheel ticks, observes 12 motion frames after the first event, and checks exact mode after idle. Execution order was before, after, after, before, before, after.

| Measurement | Before | After | Change |
| --- | ---: | ---: | ---: |
| Exact vector requests during motion, each run | 4 | 0 | −100% |
| Median frame build and submit | 6.84 ms | 4.92 ms | −28.1% |
| P95 frame build and submit | 9.29 ms | 7.46 ms | −19.7% |
| P99 frame build and submit | 9.56 ms | 7.72 ms | −19.2% |
| Build-and-submit rate based on average frame duration | 147.1/s | 208.8/s | +41.9% |
| Rate of the slowest 1% by frame duration | 103.4/s | 128.5/s | +24.3% |

The table uses the median of the three independent runs per build. Each run is short, so the 1% figure is the slowest of its 12 samples. Each baseline had four vector requests and each candidate had zero. The measurements stop at submission and do not measure display FPS, physical input latency, or NativeAOT performance. Exact resolution was restored after idle in every run. Magnification can look softer during scrolling while using the preview. The cost of the final exact camera frame still needs work.

The Windows `observer-only` regression suite passed 1,799 checks. Raw samples and summary are in `W:/Cache/Duxel/gallery-source-20261117/svg-scroll-2057`.

## SVG curve endpoint reuse trial

The isolated compute shader reused the existing snapped endpoints when finishing the winding binary search. The candidate passed Vulkan 1.2 SPIR-V validation. Its SPIR-V contains three static calls to the curve point function in the search instead of five, with the same workgroup and push-constant offsets.

Run the Release graphics tests `gpu-transparent-arc-geometry`, `gpu-arc-geometry`, `gpu-transparent-shadow`, `gpu-transparent-browser`, and `gpu-svg-camera` against the baseline and isolated candidate shaders. Windows reproduced the 12 baseline camera captures byte for byte. The candidate changed one pixel in each of two 1× camera captures (maximum 8-bit channel deltas of 8 and 4) and two bytes of a raw transparent SVG readback. Thirty-two of the 35 raw output pairs matched. The existing external browser failures occurred in both builds.

The paired workload used SDK 11.0.100 and Linux x64 NativeAOT runtime 11.0.0. Each run launched the Gallery with `--verify-gpu-camera`, WSLg lavapipe and four workers. It presented 105 Gallery scenarios before the four camera scenarios. The order was baseline, candidate, candidate, baseline, baseline, candidate. All six runs passed the presentation contract. Each camera case measures a single exact-frame call, so frame distribution and FPS statistics do not apply to this probe.

| Camera case | Baseline QueuePresent range | Candidate range |
| --- | ---: | ---: |
| 1× | 8.06–8.52 ms | 8.30–8.70 ms |
| 2× | 417–451 ms | 413–443 ms |
| 4× | 414–445 ms | 413–451 ms |
| 2× at 37° | 173–202 ms | 174–177 ms |

The ranges overlap and the direction reverses, so the speed result is inconclusive. The failed pixel comparison keeps the original renderer shader in the default build. Hardware input latency remains unmeasured. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-curve-reuse-2058`.

## SVG material evaluation after coverage

The general SVG path tested each of the four samples after evaluating material color for the pixel. A path can miss all samples inside its bounds and still read gradient data or pattern texels. Build its four-bit coverage mask first and skip material work when it is empty. The sample test, material coordinates, layer order, and analytic coverage remain the same. The existing stroke overflow checks still run for every sample that enters the path test.

Use the same Windows Release Vulkan profiles as the preceding trial. All 35 raw output pairs match, and 20 additional RGBA outputs per run agree across the presentation benchmark. Both existing external browser-oracle failures were reproduced unchanged. The candidate SPIR-V passed Vulkan 1.2 validation. Push constants and workgroup dimensions are unchanged.

On Windows .NET 11.0.0 and an NVIDIA GeForce RTX 5070 Ti Laptop GPU, `gpu-svg-camera-present-bench 3` ran 16 warmup and 32 measured frames per case. Each run covers 320 samples. Run order was A/B/B/A/A/B. GPU timestamps include UI rendering. Per-run median ranges were lower in 9 of 10 cases. For Red Gallardo at 4× they were 3.618–3.629 ms before and 3.593–3.601 ms after. The median of run medians fell from 3.622 to 3.599 ms (0.6%). For the yellow Gallardo at 2× they were 1.145–1.150 ms before and 1.125–1.128 ms after (1.8% reduction in the median of run medians). Red 2× overlapped.

The Windows render-call timing does not establish a change in display latency. For Red 4×, using the median of each distribution statistic across runs, average call rate was 184.25→184.75/s, median was 5.368→5.359 ms, p95 was 6.030→6.055 ms, p99 was 6.143→6.149 ms, and the slowest-sample rate was 162.09→161.65/s. Each run has 32 frames for this case, so a 1% tail rate uses its slowest sample.

The Linux x64 NativeAOT Gallery used SDK 11.0.100 and runtime 11.0.0. Run `--verify-gpu-camera` on WSLg lavapipe with four workers. After 105 Gallery scenarios, each of six runs finished the four exact camera scenarios. Final QueuePresent remained in overlapping ranges at 2× (408–423 ms before, 412–424 ms after), 4× (391–421 ms before, 411–415 ms after), and 2×/37° (163–176 ms before, 169–173 ms after). No software Vulkan speedup is established. AMD hardware and physical input-to-display latency were not measured. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-paint-coverage-2059`.

## SVG slider release and consecutive gestures

A stationary captured pointer could already exceed the transform debounce interval. In that case releasing a slider immediately triggered exact GPU output and C# example refresh. A second gesture could then supersede that output. Keep the fixed-resolution preview during capture and for the full 120 ms after release. Keyboard and programmatic changes still use the time of the last value change.

Use the Release graphics tests with SDK 11.0.100 and runtime 11.0.0. Run `dotnet Duxel.Graphics.Tests.dll <output> gallery-svg-profile` to produce `pointer.tsv`. In a 1280×900 Windows Vulkan fixture, render 16 warmup frames, send pointer input to the actual zoom slider, hold for 200 ms and release. Immediately repeat on the rotation slider and then leave both controls idle. Each run has 32 move frames. Run order was A/B/B/A/A/B. Zoom and rotation matched frame for frame between builds, ending at 2.94× and 259°.

| Measurement | Before | After |
| --- | ---: | ---: |
| Exact vector render requests across the two releases and idle | 2 | 1 |
| Pixels requested, including first-time preview drawing | 1,230,224 | 724,888 |
| Windows zoom release frame composition | 28.97–30.87 ms | 2.17–2.24 ms |
| Windows rotation release frame composition | 30.55–33.71 ms | 2.01–2.04 ms |
| Move frame build and submit median | 4.502 ms | 4.453 ms |
| Move frame build and submit p95 / p99 | 4.975 / 5.566 ms | 5.455 / 6.152 ms |
| Move frame average submit rate / slowest-sample rate | 217.52 / 172.05 per second | 219.85 / 156.86 per second |

Requested pixels dropped 41.1% for this two-gesture case. The moving-frame tail was slower in this sample. The improvement is the removal of an intermediate exact render and work from both release frames. The final idle frame still takes 28.55–29.99 ms to compose and 4.29–4.75 ms to submit in the Windows fixture. Frame statistics use the median of run summaries, and a run's slowest sample represents its 1% tail. This probe supplies software pointer input directly to Gallery and stops its timer at submission. It does not measure physical pointer latency, panel scanout, or NativeAOT interaction timing. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-pointer-2060`.

## CodeView reuse after an SVG transform

When Gallery rewrites a sample's C# values, CodeView parses the new document and projects it into native lines. The old native layout cache discarded every line on any source edit. It now reuses the immutable natural and wrapped layouts at the unchanged start and end of the document. A line must have the same text and line-local font styles, and the resource key and vector text mode must match. The new document supplies line starts and paint style indices so shifted lines retain correct glyph and caret positions.

Use `gallery-svg-profile` in Release with SDK 11.0.100 and Windows runtime 11.0.0, 1280×900, 16 warmup frames, the same zoom/rotation gestures and 32 move samples per run. Run order was A/B/B/A/A/B. Every run reached 2.94× and 259° and requested one exact texture of 494,488 pixels on the idle frame. The entire input and vector request sequence matched across builds.

| Measurement | Before | After |
| --- | ---: | ---: |
| Idle frame composition range | 28.46–30.61 ms | 7.24–8.29 ms |
| Idle frame composition median | 30.44 ms | 8.09 ms |
| Idle frame renderer call range | 4.32–4.33 ms | 4.17–4.96 ms |
| Move frame build and submit median | 4.493 ms | 4.207 ms |
| Move frame build and submit p95 / p99 | 5.591 / 5.973 ms | 5.077 / 5.510 ms |
| Move frame average submit rate / slowest-sample rate | 215.92 / 160.01 per second | 225.71 / 177.01 per second |

The final idle composition median fell 73.4%. Renderer and moving-frame timings vary across runs, including a slower move outlier, so neither is an established improvement. A regression inserts a line before a wrapped suffix and verifies one new native layout plus fresh-equivalent glyphs, carets and paint indices. All 35 selected Gallery source/SVG PNGs match the prior build byte for byte. This workload uses injected pointer input and stops at submission. It does not measure panel latency or Linux drag performance. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/codeview-line-reuse-2061`.

## SVG blur workgroup tile trial

Large axis-aligned convolutions repeatedly load source pixels shared by adjacent outputs. The isolated candidate loaded 32 taps plus the seven neighboring pixels for each line of an 8×8 workgroup. The trial kept tap order and covered positive and negative axes, including partially populated edge groups. It adds two workgroup barriers per tile and raises shared storage from 8,200 to 13,192 bytes.

The Vulkan 1.2 SPIR-V candidate compiled and validated with unchanged push constants. An index check covered 24,384 axis/lane cases. All 35 Windows raw pairs matched across the same five graphics profiles, with the same two pre-existing external Chromium oracle failures. That confirms these outputs only. No timing test ran on Windows hardware for this candidate.

Using SDK 11.0.100, run Linux x64 NativeAOT Gallery with `--verify-gpu-camera` on WSLg lavapipe and four workers. Each run presents 105 initial Gallery scenarios before four camera cases. A/B/B/A/A/B all completed. Each final frame contributes a single QueuePresent sample per run.

| Camera case | Baseline | Candidate |
| --- | ---: | ---: |
| 1× QueuePresent | 8.09–8.87 ms | 8.25–8.47 ms |
| 2× QueuePresent | 409–432 ms | 512–518 ms |
| 4× QueuePresent | 406–419 ms | 539–558 ms |
| 2× at 37° QueuePresent | 167–180 ms | 193–197 ms |
| First blur stage at 4× | 248–255 ms | 381–397 ms |

At 4× the QueuePresent median rose from 407.38 to 540.59 ms (+32.7%). The product keeps the original shader and its pretrial binary hash. The measurement isolates a regression in the first blur phase but does not assign it to a specific instruction or resource. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-blur-shared-2062`.

## SVG blur skips transparent input

Large filter regions include transparent pixels outside the source drawing. Cache the union of encoded draw bounds per retained vector scene, map it conservatively to the current camera pixels, and pass it to the separable blur. The first pass restricts its tap range to the source support. The second expands the first-pass axis by its radius. Both keep the same kernel normalization and tap order. The unrestricted range still applies to oblique or non-orthogonal passes and opaque backgrounds. The renderer allocates the same filter surfaces, and the shortcut writes zeros outside the range.

Vulkan 1.2 SPIR-V validation passed. The four bounds start at push-constant offset 44, and the native struct occupies 64 bytes. Windows matched all 35 raw outputs from the five camera/browser profiles, all 172 additional captures from `gpu-svg-pixels`, and 20 continuation images from the presentation benchmark. The same two independent Chromium comparison profiles retain their earlier failures.

SDK 11.0.100, Linux x64 NativeAOT runtime 11.0.0, WSLg lavapipe with four workers, `--verify-gpu-camera`. Runs used A/B/B/A/A/B order and each presented 105 Gallery scenarios followed by four camera cases.

| Final camera measurement | Baseline | After |
| --- | ---: | ---: |
| 2× QueuePresent | 419–443 ms | 341–347 ms |
| 4× QueuePresent | 406–420 ms | 316–321 ms |
| 2× at 37° QueuePresent | 170–179 ms | 148–150 ms |
| First blur phase at 4× | 249–262 ms | 158–163 ms |

The median 4× QueuePresent fell from 418.11 to 320.99 ms (23.2%). A case is one final frame per run, so it does not provide frame distribution metrics.

Windows .NET 11.0.0 RTX 5070 Ti Laptop GPU, `gpu-svg-camera-present-bench 3`, A/B/B/A/A/B, 16 warmup and 32 measured frames per case. For the red SVG at 4×, GPU timestamp median fell from 3.599 to 2.660 ms (26.1%). Render-call median fell from 5.342 to 4.236 ms, p95 from 5.873 to 4.670 ms, p99 from 6.062 to 4.842 ms, average call rate rose from 185.39 to 233.55/s, and the slowest-sample rate rose from 162.86 to 203.92/s. These are medians of run statistics. With 32 frames, the rate of the slowest sample represents the 1% tail. The yellow scene's GPU ranges overlapped. AMD and physical input-to-display latency were not measured. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-blur-bounds-2063`.

## SVG path coverage reuses camera parameters

Pass the current pixel's scale and origin and the already-loaded drawing bounds into the path containment routine. This removes duplicate camera calculation and repeated header loads from subpixel coverage tests. Sampling, winding, bounds and shader ABI remain the same. The isolated SPIR-V passed Vulkan 1.2 validation.

Windows .NET 11.0.0 GPU output matched all 35 raw pairs from the five comparison profiles. All 20 images matched across six additional `gpu-svg-camera-present-bench 3` runs on the RTX 5070 Ti Laptop GPU, with 16 warmup and 32 measured frames for each of ten cases and order A/B/B/A/A/B. GPU timing ranges were lower in nine cases. The yellow 1× median fell from 1.224 to 1.192 ms. The red 4× ranges overlapped at 2.649–2.681 ms and 2.633–2.665 ms.

For the red 4× Windows render call, medians of run statistics were 4.270→4.206 ms, p95 4.788→4.700 ms, p99 5.069→4.979 ms, average rate 231.70→235.56 calls/s, and slowest-sample rate 195.20→196.39/s. One candidate run had a slow call (p99 14.245 ms). There is no repeatable render-call latency result.

Linux x64 .NET 11.0.0 NativeAOT built with SDK 11.0.100 presented 109 Gallery scenarios per run. On WSLg lavapipe with four workers, six runs in A/B/B/A/A/B order measured 4× final QueuePresent at 315.46–316.12 ms and 315.60–318.22 ms. The 2× and rotated cases also overlapped. No software speedup is established. Each camera case gives one final-frame timing per run.

A separate .NET 11.0.0 NativeAOT Gallery run with automatic fonts and without ICD, Gallium driver, or worker-count overrides selected the CPU-backed `llvmpipe` device on this Ubuntu WSL installation and completed all 109 scenarios. Its 4× final QueuePresent was 143.89 ms. This single observation uses different defaults from the four-worker comparison and is not a speedup estimate. The benchmark does not measure mouse-to-display latency. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-camera-params-2064`.

## SVG filter timing by submission

The renderer records a timestamp pair for each blur filter when GPU profiling is enabled. The retired frame slot supplies the filter count, and only written queries are read. Up to 16 pairs are collected per submission. A larger count leaves all-filter and outside-filter timings unavailable. The outside-filter time includes path work, stroke GPU preparation, copies, and associated barriers in the vector scope. It is not a path-only measurement.

Use `--verify-gpu-camera` with `DUXEL_VK_PROFILE=1 DUXEL_VK_GPU_PROFILE=1 DUXEL_VK_PROFILE_EVERY=1`. A Linux x64 .NET 11.0.0 NativeAOT build, SDK 11.0.100, completed 109 Gallery presentations in each of two WSLg llvmpipe runs with matching explicit Windows fonts. One run forced four workers and one used Mesa's default. There was no forced Vulkan ICD. Join retired queries by submission sequence.

| Worker setting and camera | Filters | First blur | All blur | Remaining vector scope | QueuePresent |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4 workers, 2× | 2 | 77.84 ms | 161.53 ms | 227.28 ms | 409.80 ms |
| 4 workers, 4× | 1 | 184.19 ms | 184.19 ms | 166.78 ms | 359.55 ms |
| Default workers, 2× | 2 | 21.37 ms | 45.71 ms | 73.53 ms | 127.33 ms |
| Default workers, 4× | 1 | 48.01 ms | 48.01 ms | 51.12 ms | 107.00 ms |

Validation found 852 retired query frames, including 842 without blur and 8 with multiple filters. No submission exceeded the recording capacity. The last, rotated camera submission had a QueuePresent result but its query slot was not read before exit. The runs establish the phase distinction, not a performance delta or physical input latency. Instrumentation may affect the durations. Runtime regression checks passed 31 camera and 58 SVG pixel cases on Windows. The Windows hardware profiling fixture returned 320 finite timing samples and matched all 20 prior camera images. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-filter-profile-2065`.

## SVG path dispatch and transfer timing

The opt-in Vulkan profiler now measures each vector scene paint dispatch and three explicit copy sites: scene upload, dependency table upload, and output texture transfer. Stroke preparation, other barriers, and command gaps may contribute to the remaining scope. The tool reads only written timestamps from the retired frame slot. Each new category has a limit of 64 intervals, above which its aggregate is unavailable.

The same Linux x64 .NET 11.0.0 NativeAOT Gallery profiling command completed 109 presentations for four workers and for default workers on WSLg llvmpipe. Fonts were identical. These are single executions per worker setting, with no performance-change claim.

| Camera | Path dispatches | Path time | Blur time | Copy time | Remaining vector time |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4 workers, 2× | 7 | 206.04 ms | 137.58 ms | 0.63 ms | 2.06 ms |
| 4 workers, 4× | 6 | 151.11 ms | 158.67 ms | 0.64 ms | 2.70 ms |
| Default workers, 2× | 7 | 64.82 ms | 42.23 ms | 0.73 ms | 10.10 ms |
| Default workers, 4× | 6 | 51.07 ms | 51.28 ms | 0.77 ms | 14.23 ms |

The recorded intervals and remaining time fit the vector scope for all 856 retired frames. Fifty-five had path and copy work, and none exceeded the recording limit. The last rotated submission again exited before its slot was read. The Windows default camera test passed 31 checks. All 12 camera and 20 benchmark images matched the preceding build, and the hardware profile produced 320 positive GPU timing samples. The full Release solution built cleanly. These timings include the effect of profiling and do not measure physical input-to-display latency. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-path-profile-2066`.

## SVG filtered source coverage trial

The isolated candidate sent a four-coordinate conservative paint rectangle to the vector scene shader. A transparent filter source emits zero immediately for pixels outside the rectangle. It retains the same dispatch size, pixel centers, coverage and compositing inside the rectangle. Vulkan 1.2 SPIR-V validation passed. The trial increased the push-constant size to 112 bytes.

The Windows five-profile raw comparison matched 35 outputs. The additional `gpu-svg-pixels` comparison matched 172 files, and the presentation benchmark matched all 20 captures across the paired runs. The existing external Chromium failures appeared in both builds. No product renderer or shader changes were applied.

Use SDK 11.0.100 and the Linux x64 NativeAOT runtime 11.0.0 Gallery on WSLg lavapipe, four workers, `--verify-gpu-camera`, and GPU timing by submission. All six runs in A/B/B/A/A/B order passed 109 initial and camera presentation scenarios. Each case contains a single final frame per run.

| Measurement | Baseline | Candidate |
| --- | ---: | ---: |
| 2× path dispatches | 200.56–209.42 ms | 198.44–206.06 ms |
| 4× path dispatches | 151.60–152.60 ms | 149.35–149.86 ms |
| 2× QueuePresent | 345.19–367.81 ms | 341.50–356.81 ms |
| 4× QueuePresent | 320.73–323.32 ms | 318.44–326.21 ms |

The 4× path median dropped from 151.72 to 149.86 ms (1.2%), while QueuePresent ranges overlapped. The remaining measured path commands and the number of blur filters stayed the same.

For Windows .NET 11.0.0 on the RTX 5070 Ti Laptop GPU, `gpu-svg-camera-present-bench 3` used A/B/B/A/A/B order, 16 warmup and 32 measured frames per case. Red 2× GPU timing increased from 1.492–1.498 to 1.562–1.564 ms, and Red 4× increased from 2.556–2.564 to 2.637–2.641 ms. Red 4× median GPU time rose 2.9%. Median of run render-call statistics at 4× was 4.199→4.280 ms, p95 4.676→4.879 ms, p99 5.062→4.994 ms, average call rate 233.91→231.92/s, and slowest-sample rate 193.84→199.49/s. With 32 samples, the slowest sample represents its 1% tail.

The additional branch showed no repeatable final-frame benefit on software Vulkan and regressed the Windows GPU case. The default sources and binary still match their pretrial hashes. Physical input-to-display latency was not measured. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-filter-source-2067`.
## Browser comparison reuses the camera texture after input stops

The previous default drew the exact visible SVG again 120 ms after each gesture, including its paths and filters. The main Gallery comparison now keeps the GPU image at the browser reference resolution and uses its camera transform for zoom, rotation and scrolling. High-resolution comparison remains available through the switch above the examples. The C# sample reflects the selected policy.

Linux x64 NativeAOT, SDK 11.0.100 and runtime 11.0.0, WSLg llvmpipe with four workers. The same build ran in alternating order with `--verify-gpu-camera` and `--verify-gpu-camera-reuse`, three independent runs per mode. Each run presented the same 109 scenarios, with 105 preceding presentations before the camera sequence. Values are the median of the three Vulkan QueuePresent call durations for the named final presentation.

| Transform | High-resolution | Camera reuse | Reduction | Cumulative SVG requests |
| --- | ---: | ---: | ---: | ---: |
| 2× | 305.92 ms | 7.36 ms | 97.59% | 11 → 9 |
| 4× | 264.61 ms | 9.38 ms | 96.46% | 12 → 9 |
| 2×, 37° | 72.74 ms | 7.66 ms | 89.47% | 14 → 10 |

The 1× timings overlapped. The 4× ranges were 261.47–721.00 ms and 6.98–46.10 ms. Rotation exposed a second example, which requested its first base-resolution image. Transform changes added no CPU SVG rasterization. These scenario-level QueuePresent measurements were taken with profiling enabled. Physical input-to-display latency, FPS and frame time percentiles were outside this comparison. The fixed image can look softer at large zoom. Raw evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-camera-default-2069/linux-comparison.json`.

Windows x64 managed .NET 11.0.0, `gallery-svg-pointer`, three runs per mode with 96 warmup frames. The same input reached 2.94× and 259° in every run. After release and the idle deadline, vector requests were 1 → 0 and requested pixels were 406,968 → 0. Neither mode requested new vectors during the measured pointer moves. The Release solution and Graphics tests built without warnings. The Gallery passed 1,805 checks, camera 31, SVG pixels 58, and all 12 camera captures matched the previous baseline. Evidence for the Windows runs is `windows-comparison.json` in the same directory.

## SVG arc comparison with visible C# source

The timed scroll triggered zero SVG texture requests. CodeView had been rebuilding full accessibility character and caret geometry when only the viewport moved. Native multiline painting also searched every paragraph run for each visible row. The viewport now reuses immutable character geometry, and painting indexes glyph runs by visual line in the backend's original order. The C# source remains visible and selectable.

Run `Duxel.Graphics.Tests <output> gallery-svg-arc-scroll` with SDK 11.0.100 and `DuxelBenchmarkFramework=net11.0-windows`. This exercised the actual Arc tab with accessibility enabled in a 1280×900 Windows Vulkan fixture on .NET 11.0.0. Replay warmed 96 scroll frames and measured 96 further frames, with a visible source block in every sample and no new vector textures. Run order was before, after, after, before, before, after. The baseline swaps only the previous Core and Vulkan assemblies into the same probe runtime.

| Measurement | Before | After | Result |
| --- | ---: | ---: | --- |
| Mean UI allocations per frame | 751,136 B | 607,228 B | −19.2% |
| P95 CPU composition | 14.09 ms | 9.81 ms | −30.4% |
| P95 composition and renderer call | 16.28 ms | 11.90 ms | −26.9% |
| Median composition and renderer call | 9.24 ms | 8.17 ms | Overlapping runs |
| P99 composition and renderer call | 25.86 ms | 25.36 ms | Overlapping runs |
| Average build-and-submit rate | 98.72/s | 116.37/s | +17.9% |
| Slowest 1% build-and-submit rate | 38.67/s | 39.44/s | Overlapping runs |

These rates describe the fixture's CPU build and renderer calls. Physical input latency and application FPS were outside the test. This trial's change-only outline was superseded by the actual repair diagnostic. Raw evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-arc-scroll-2070/final-comparison.json`.

## Fast scroll over SVG sample source

The SVG page's child viewport was traversing the Gallery once for each queued Win32 wheel packet, even when all packets stayed over one expanded C# block and requested no SVG textures. That child now opts into combining compatible vertical input. It keeps individual steps when the direction changes, the pointer moves out of the code, a popup blocks input, or the scroll reaches a boundary. A captured thumb can also use its last pointer position before release.

Run `Duxel.Graphics.Tests <output> gallery-svg-code-scroll` with Windows SDK 11.0.100 and runtime 11.0.0. Each sample generated 64 actual Win32 wheel messages, used 4 warmup bursts and measured 16 bursts per tab. Three runs per side were interleaved. The control uses the same test and code with batching disabled at the child viewport. The table reports the median of each run's p95 CPU composition times.

| SVG tab | Before | After | Change |
| --- | ---: | ---: | ---: |
| Stroke domains | 77.27 ms | 8.96 ms | −88.4% |
| Arc geometry | 35.20 ms | 7.24 ms | −79.4% |

Both tabs drop from 64 traversals to 2. The compared scroll offsets and settled screenshots match across all six runs, and the measured source bursts request zero new SVG textures. Direction reversals remain ordered. A 64-move thumb drag uses 2 traversals with the same final offset as the unbatched control in both tabs. The current Windows managed and Linux x64 NativeAOT Gallery each acknowledged 105 presentation scenarios. The rapid-gesture timing measurements are from Windows. Evidence is under `L:/Cache/Duxel/svg-code-wheel-2140`.

## Partial Vulkan repaint for affine SVG transforms

Previously each scaled or rotated SVG marked the entire frame as a projected scene requiring full repair. The renderer now tracks the old and new screen bounds of affine widgets within their ancestor clip, and rejects fully clipped draw bounds. The green outline again represents actual scene repair. Scene projection and depth-writing commands retain the existing whole-scene repair path.

Windows .NET 11.0.0, SDK 11.0.100, 1280×900 Vulkan window, three frame slots. Run `Duxel.Graphics.Tests <output> gallery-svg-pointer primary`. Each run warms 96 frames and measures 32 identical zoom/rotation steps. Measurements use three runs per side in before/after/after/before/before/after order. All six settled screenshots match. Tests also replay `camera`, `arc`, and `high-resolution` modes. All three settled screenshots match their full-repaint controls.

| Metric per run | Before | After | Result |
| --- | ---: | ---: | --- |
| Full repairs during 32 move frames | 32 | 1 | −96.9% |
| Median repaired framebuffer pixels per move | 1,152,000 | 686,392 | −40.4% |
| Mean repaired framebuffer pixels per move | 1,152,000 | 707,130 | −38.6% |
| Median build and renderer call | 3.41 ms | 3.42 ms | Overlapping runs |
| P95 build and renderer call | 5.56 ms | 6.49 ms | +16.6% |
| P99 build and renderer call | 7.18 ms | 7.14 ms | Overlapping runs |
| Mean build-and-submit rate | 270.66/s | 266.61/s | Overlapping runs |
| Slowest 1% build-and-submit rate | 139.34/s | 140.04/s | Overlapping runs |

The second and third new swapchain images required one full press frame and one full move frame. Later moves stayed on the right side at x=308 or farther right. Three profiled pairs on RTX 5070 Ti matched 31 retired GPU submissions each, excluding the first cold move. Median GPU command duration across the runs was 158.50 → 150.34 μs (−5.1%). GPU vector work stayed close at 130.24 → 130.59 μs. This does not establish a lower input-to-display time or improved Windows FPS. The alternate lower-resolution drag implementation increased the tail, so it was discarded. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-transform-damage-2072`.

## CPU time of partial SVG repaint

The Vulkan profile now separates logical damage snapshot time, acquired-image repair planning, and texture update processing. The new `damageCpu(snapshot=… repair=…)` and `textureCpuUs` values use the existing CPU timing sampling interval. They measure renderer-side CPU work. GPU timing stays in the existing retired submission fields.

Windows x64 .NET 11.0.0, SDK 11.0.100 and RTX 5070 Ti, using the same Gallery diagnostic binary with `DUXEL_VK_DAMAGE=0` and `DUXEL_VK_DAMAGE=1`. Each mode ran three times, in full/partial/partial/full/full/partial order, with 96 warmup frames and 31 matched move frames after excluding the first cold move. The damage flag also changes the swapchain clipping option. All six settled images have identical hashes. The table gives the median of the three run medians.

| Stage | Full repair | Partial repair |
| --- | ---: | ---: |
| Logical snapshot | 0.70 μs | 55.50 μs |
| Acquired-image repair planning | 0.10 μs | 19.70 μs |
| Vulkan command recording | 131.10 μs | 88.80 μs |
| All texture updates on the CPU | 247.30 μs | 253.50 μs |
| GPU submission | 156.80 μs | 151.84 μs |

Texture update time is the largest listed CPU stage, and includes all texture updates for the frame. Texture, renderer and GPU ranges overlap across runs. These timings locate the aggregate cost, and do not explain the earlier p95 difference or establish a change in interaction latency. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-damage-cpu-profile-2073/comparison.json`.

## Full-window repairs from the repaint outline

The earlier pointer benchmark used one sample per pixel with highlighting disabled. With the Gallery's four-sample setting and highlighting enabled, the first drag acquired two cold images. It made one full repair on press and one on the first move. Clearing the old full outlines then forced three more full-window GPU color passes, even though the content damage rectangle was already partial.

The renderer now restores old outline pixels in thin strips outside the content repair. It clips the green line to each render area, preserves both the acquired image and shared MSAA history, and reports extra passes separately. The Vulkan profile also separates vector output creation from texture processing to support further diagnosis. In the earlier three non-highlight runs per mode, creating the primary vector output image and view took 46.5–49.4 μs at the run medians. Camera and arc had no new vector outputs during moves, while total CPU texture time still reached 218–290 μs.

Windows .NET 11.0.0, RTX 5070 Ti, 1280×900, 4× MSAA, V-Sync off. One before/after run per tab, with 32 matching zoom and rotation moves per run. Pixel counts are the sum of Vulkan render areas, with repeated pixels counted again if two passes cover them.

| SVG tab | Full move frames | Full move frames caused by the outline | Total repair pixels |
| --- | --- | --- | --- |
| Image and text | 4 → 1 | 3 → 0 | 25,397,776 → 24,029,020 (−5.39%) |
| Browser comparison | 4 → 1 | 3 → 0 | 25,744,304 → 24,284,540 (−5.67%) |
| Arc comparison | 4 → 1 | 3 → 0 | 25,843,312 → 24,525,748 (−5.10%) |

Each tab's final PNG hash matched its baseline. The extra outline work during the moves covered at most 6,800 pixels per frame. The remaining full move initializes an acquired swapchain image. Renderer submission p95 increased in these single pairs, so the results establish less redrawn area and no interactive latency or FPS gain. Additional passes may add command-recording cost. MSAA 1×/4× repaint tests, presentation reuse, the SVG camera and cache tests, and the Windows Gallery's 105 verification steps passed. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-highlight-2075`.

## CPU wait after texture upload

The graphics queue was submitting texture copies and then blocking the CPU on the upload fence before submitting the draw commands. The copies already end with a transfer-to-fragment barrier. When only textures are uploaded on the same VkQueue as the draw, that barrier orders the later sampling. The renderer now submits the frame directly and checks the upload fence before reusing the upload command buffer or staging storage. Uploads on a separate queue or batches containing geometry copies retain the host wait. The upload profile adds `frameWaitUs` to identify the wait immediately before graphics submission within the aggregate `waitUs`.

Windows x64 .NET 11.0.0 managed diagnostic, SDK 11.0.100, RTX 5070 Ti, V-Sync off, 1280×900, 4× MSAA and repaint highlighting enabled. Each tab ran three paired comparisons on the same binary, alternating blocking and queue-ordered uploads. Each run used 96 warmup frames and 31 matched drag moves after excluding the first cold image. The table reports the median of the three run medians.

| SVG tab | Total CPU upload wait | Renderer wall time |
| --- | --- | --- |
| Image and text | 89.2 → 2.2 μs (−97.5%) | 1.333 → 1.196 ms (−10.3%) |
| Browser comparison | 65.8 → 2.3 μs (−96.5%) | 1.181 → 1.075 ms (−9.0%) |
| Arc comparison | 48.9 → 2.4 μs (−95.1%) | 1.199 → 1.114 ms (−7.0%) |

No move on the new path waited at the graphics submission in the measured median. The small remaining waits occur on staging or command-buffer reuse. All six settled image hashes matched for each tab. Renderer p95 varies across runs, and the test does not measure display latency. The separate transfer queue was exercised on the same GPU with queue family 1 and retained the observed host wait and identical final pixels. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-upload-wait-2076/comparison.json`.

Linux x64 .NET 11.0.0 NativeAOT passed 109 WSLg Gallery presentations. The Vulkan log recorded 135 texture-only upload frames on the shared queue, all with zero pre-submit wait. This is Linux functional path verification. No paired Linux or NativeAOT timing comparison was run.

## Win32 pointer message to Vulkan presentation

The previous diagnostic supplied pointer inputs directly to the Gallery. The same benchmark can now post Win32 mouse messages to its own window. It requires the matching input from the platform's ordered capture and, for every move, a distinct SVG transform and a new Vulkan presentation of the matching draw-data frame. It measures the interval from before posting the message through the renderer's presentation timestamp. The fixture polls and renders synchronously, so this does not include physical mouse input, the production application loop's queueing, or compositor scanout.

Run `dotnet <Duxel.Graphics.Tests.dll> <output> gallery-svg-pointer <primary|camera|arc> win32 highlight`. The existing `DUXEL_TEST_BLOCKING_UPLOAD=1` validation override enables the old host wait in the same test binary. The diagnostic writes `post_to_input_ms` and `post_to_present_ms` in `pointer.tsv`.

Windows x64 .NET 11.0.0, SDK 11.0.100, RTX 5070 Ti, 1280×900, 4× MSAA, V-Sync off and repaint highlighting on. Each tab ran three pairs using the same executable. All 32 moves per run were verified and the first cold full-window repair was excluded from the timing summary.

| SVG tab | Median post to presentation | Change | P95 range with the new upload path |
| --- | --- | ---: | --- |
| Image and text | 3.706 → 3.660 ms | −1.2% | 7.46–7.56 ms |
| Browser comparison | 4.066 → 3.992 ms | −1.8% | 8.51–9.03 ms |
| Arc comparison | 4.110 → 3.893 ms | −5.3% | 10.00–10.53 ms |

All six input state sequences and final PNG hashes matched within each tab. Browser and arc p95 ranges overlapped their baselines. The newer path spent a median 2.42–2.87 ms composing UI and 1.08–1.27 ms in the renderer across the measured moves in each tab. This identifies UI composition as the larger remaining measured stage but does not identify a hot function. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/svg-win32-input-2077/comparison.json`.

## Reusing the two widths of a wrapped description

Sampled thread stacks during SVG drag reached native shaping from `UiInlineText.MeasureContent` inside `MeasureScrollView`. When a scroll view's child overflows vertically, the view measures at the full width and again with space for the vertical scrollbar. A literal inline-text widget kept only one of those layouts, so on the next frame both widths needed native shaping again.

`UiInlineText` now shares the two-width layout cache already used by `UiCodeView`. It allocates the second slot only when the width changes. Unwrapped text shares one slot across widths, and every layout validates the source revision and the typography context. The cache holds at most two shaped layouts per widget.

Windows x64 managed .NET 11.0.0, SDK 11.0.100, RTX 5070 Ti, 1280×900, 4× MSAA, V-Sync off and repaint highlighting on. Each tab ran three alternating before/after pairs. Each run confirmed all 32 pointer moves, excluded the first cold full repair from timing, and preserved the final PNG hash.

| SVG tab | UI composition median | Win32 message to Vulkan presentation median |
| --- | --- | --- |
| Image and text | 2.318 → 1.711 ms (−26.2%) | 3.483 → 2.855 ms (−18.0%) |
| Browser comparison | 2.821 → 2.212 ms (−21.6%) | 3.831 → 3.153 ms (−17.7%) |
| Arc comparison | 2.708 → 2.176 ms (−19.7%) | 3.750 → 3.183 ms (−15.1%) |

Browser and arc p95 did not show consistent improvement. The diagnostic ends at Vulkan presentation submission and does not measure physical input or scanout. Evidence and sampled stacks are in `W:/Cache/Duxel/gallery-source-20261117/svg-compose-2078`.

The alternating-width test confirms that both exact heights are reused after warmup. Windows passed 37 inline text, 3,774 presentation reuse and 17 SVG gallery checks. The SVG code syntax parse counter stayed at five throughout all 224 profile frames. The standard Breadcrumb code benchmark completed 256 samples with zero additional code measurements, paint builds or parses. Its separate full-source mode rejected the scroll-to-bottom action in both builds. Linux x64 .NET 11.0.0 NativeAOT passed 109 WSLg Gallery presentations.

## Code-dominated Gallery viewport

The old full-source fixture targeted Breadcrumb after it had switched to a compact example. At the large window size, the page fit vertically and had no scroll action. The regular benchmark continues to use Breadcrumb. The full-source benchmark now scrolls the actual Markdown Gallery page to the bottom and checks the published C# text, clip and scroll position before timing. It verifies the same source and geometry again at the end. The reported source hash comes from the visible code widget's text.

Run `dotnet <Duxel.Graphics.Tests.dll> <fresh output> code-view-full-bench` with .NET 11.0.0. The three Windows x64 runs used a physical 1654×1040 framebuffer, 1.14 text scale and V-Sync off. They each measured 256 UI frames with the same C# source at scroll position 100%, covering 98.8% of the page viewport height and exposing 40 of 93 visual lines. Median UI time ranged from 1.44 to 1.67 ms and mean allocations ranged from 133 to 139 kB per frame. Code measurement, visual-line builds and syntax parses each remained at zero after warmup. This is a baseline for the repaired workload.

A separate sampled-thread trace of the same fixture placed 34.9% of the sampled time inside `ObserverScreen.Render` on stacks containing Windows native paragraph-run rasterization. This measures inclusive stack time, so it does not attribute the managed allocation total to that function. The next investigation should check raster cache hits, empty-ink entries and evictions per frame. See `W:/Cache/Duxel/gallery-source-20261117/code-full-viewport-2079/verification.json` and `compose-sampled-stacks.json` in the same folder.

## Retaining empty paragraph rasters

The same Markdown Gallery viewport made 26,624 native paragraph raster requests in each 256-frame run after warmup. Every result had no pixels. The cache counted a paragraph run as visible only when it had a texture, so empty runs repeatedly entered the bounded cold cache and were evicted. A visible run now stays in the working set even if it needs no GPU texture. Its entry becomes eligible for cold-cache eviction when the run leaves the viewport.

Windows x64 .NET 11.0.0 with SDK 11.0.100, three fresh processes per side, 0.8 seconds minimum warmup and 256 frames per run. Command `dotnet <Duxel.Graphics.Tests.dll> <output> code-view-full-bench`. The same source hash, 40/93 visible lines and 98.8% viewport coverage held throughout. Empty raster requests fell from 104 per frame to zero. Empty cache evictions fell from 26,520 to zero. The median of the three median UI times went from 1.662 to 1.419 ms (−14.6%). The median mean allocation went from 134,786 to 67,153 bytes per frame (−50.2%). The code benchmark now asserts that warmed empty entries are reused. The measurements describe managed UI time and allocations and do not establish NativeAOT frame rates. Keeping empty entries for visible text can occupy more cache metadata while the text is visible. The cold-cache limit still applies to unused entries. Raw samples and result JSON are in `W:/Cache/Duxel/gallery-source-20261117/code-raster-cache-2080/before-{1,2,3}` and `W:/Cache/Duxel/gallery-source-20261117/arc-burst-2081/code-a{1,2,3}`.

## Arc comparison input and code selection

Rapid zoom, rotation and code selection in the same Gallery tab all replayed the full UI once for every queued pointer position. Intermediate passes also calculated code selection rectangles and measured line numbers, even though only the final pass draws. SVG sliders now opt into coalescing while they own the pointer. Read-only code can coalesce moves inside its visible text. Press and release, keyboard, wheel, character input and moves at clipping or scroll boundaries retain ordered processing. Selection paint visits visible rows and uses the prepared layout's height without rescanning the source. No additional persistent drawing, texture or selection cache was introduced.

Windows x64, .NET 11.0.0 with SDK 11.0.100, RTX 5070 Ti Laptop GPU, 1280×900, 4× MSAA and V-Sync off. Each phase used three independent alternating runs per side (B1/A1/A2/B2/B3/A3). Run `dotnet <Duxel.Graphics.Tests.dll> <output> gallery-svg-pointer arc win32 burst` and append `unmerged` for the transform control on the same binary. Each run warmed 96 frames and measured 33 bursts per gesture. Zoom alternated between 0.25× and 4×, and rotation alternated between 54° and 281°. `gallery-code-selection arc` scrolls to the actual C# example and alternates short and long selections, taking 160 samples each with one and sixteen steps. Selection testing feeds positioned states into the app's Core input batch API. The transform case posts Win32 mouse messages and matches each batch to a Vulkan submission. These times stop at submission.

| Action and measured interval | Median, before → after | p95, before → after |
| --- | ---: | ---: |
| Zoom, Win32 post through Vulkan submission | 17.37 → 3.96 ms (−77.2%) | 25.88 → 12.72 ms |
| Rotation, Win32 post through Vulkan submission | 15.86 → 2.53 ms (−84.0%) | 18.21 → 4.12 ms |
| Code selection, UI work for 16 moves | 6.58 → 2.45 ms (−62.7%) | 18.73 → 3.42 ms |

Each optimized batch processed two steps and produced one drawing, with the same submitted values and final selection. All captured endpoint pixels matched the corresponding before pixels. A selection crossing the code clip still processed all four steps. SVG generated no new vector textures during measured moves. One cold swapchain image needed a full repair in each zoom run, including with highlighting. The remaining zoom spikes include an early layout pass when the smaller picture reveals the code and a later expensive damage snapshot. The 1-step selection medians overlapped (about 1.27–1.74 ms). During 16-step selection, median mean allocation fell from 945,896 to 101,721 bytes per frame and the median sampled process peak fell from 315.9 to 304.9 MiB private and 240.4 to 226.6 MiB working set. The process figures include Vulkan and GC and cover the measured interaction. Evidence in `W:/Cache/Duxel/gallery-source-20261117/arc-burst-2081/combined-comparison.json` includes p99, latency-derived rates and raw selection/presentation logs. Windows NativeAOT timing and physical pointer/display latency were not measured.

## Vulkan damage snapshot recompilation during zoom

The runtime trace recorded two synchronous optimizations of `PrepareLogicalDamageSnapshotCore` on the renderer's thread. They took 9.6 and 11.8 ms. The latter matched the repeatable zoom spike in the Vulkan damage-snapshot interval. The damage loop now delegates individual command comparison, copying and streamed buffer slicing to separate methods. The same snapshots, damage comparisons, GPU passes and buffer ownership are used. An analogous extraction in accessibility text layout raised median UI time in a trial and was reverted.

The same three-pair Win32 arc test compared builds that both include the input and text optimizations. The median time spent in the renderer on the repeatable slow frame fell from 13.22 to 0.72 ms. Zoom median latency stayed within the previous run range at 4.08 → 4.04 ms. Zoom p95 fell from 14.47 to 10.50 ms (−27.4%). Rotation latency overlapped. All six final captures had the same SHA-256 hash. The final selection replay kept the same result pixels and averaged 106,123 bytes allocated per 16-step frame. The first newly acquired image still required one full-window repair. At other turns in the zoom sweep, the SVG and the C# example can move together and change most of the right pane. Remaining short pauses when code becomes visible are recorded separately from the renderer spike. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/arc-burst-2081/final-damage-comparison.json` and `zoom-jit-events.csv`.

## Shared text work during arc comparison gestures

CPU samples from both the transform and the source selection replay led to the per-text traversal of the layer stack to choose bitmap or vector text. Core now maintains the depth of the innermost camera when a layer is entered or left. The portal's entry depth still excludes cameras outside its screen-space scope. The input-pass and exception cleanup clears the camera depth with the layer stack. DirectText also skips layer dependency traversal when no layer is open. There are no new text or GPU caches.

Windows x64, SDK 11.0.100, managed runtime 11.0.0, 4× MSAA, V-Sync off. Three alternating processes per build with 96 warmup frames, 33 bursts of 16 Win32 pointer moves per transform, and 160 selection frames per mode after a further 24 warmup frames. Commands were `dotnet <test DLL> <output> gallery-svg-pointer arc win32 burst highlight` and `dotnet <test DLL> <output> gallery-code-selection arc`. The transform fixture presented to a 1600×1125 framebuffer. These timings end at Vulkan submission and are independent of physical input and scanout. The rates below are derived from the measured intervals and are not application FPS.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Zoom, Win32 post to submission | 2.958 → 2.856 | 2.582 → 2.486 (−3.7%) | 7.649 → 7.651 | 8.969 → 8.979 | 338.0 → 350.1 | 111.5 → 111.4 |
| Rotation, Win32 post to submission | 2.688 → 2.591 | 2.471 → 2.388 (−3.4%) | 4.371 → 4.274 | 4.863 → 4.770 | 372.0 → 385.9 | 205.6 → 209.6 |
| Source selection, one move, build and submit | 1.689 → 1.635 | 1.641 → 1.587 (−3.3%) | 1.938 → 1.853 | 2.919 → 2.986 | 592.1 → 611.5 | 329.2 → 293.9 |
| Source selection, 16 moves, build and submit | 3.107 → 2.963 | 2.979 → 2.930 (−1.6%) | 3.939 → 3.795 | 5.411 → 4.939 | 321.9 → 337.5 | 162.4 → 166.6 |

The rotation and 16-move selection median ranges overlap between runs. Zoom tail timing showed no improvement. In the 16-move selection, median run mean allocation was 104,769 → 104,709 B per frame and median sampled peak process private bytes were 319.95 → 319.90 MiB. Both sets of endpoint images match byte for byte. Each zoom sweep performed one full-frame repair out of 33 moves and rotation performed zero. Neither requested any SVG vector textures during the moves. Resizing the SVG widget can still move following content and repaint most of the page pane.

The Release solution and test builds passed without warnings. Widget transforms passed 218 checks, DirectText 45, Gallery SVG 17 and CPU HUD portal 20. The GPU HUD and presentation-reuse suites hit the same existing paint-group opacity assertions in both builds. NativeAOT and physical pointer-to-display timing were not measured for this change. Evidence and raw frames are in `W:/Cache/Duxel/gallery-source-20261117/arc-joint-2083`.

## Arc comparison repair planning

The pointer benchmark records code measure, paint and syntax build counts, paragraph raster requests and UI-thread allocations. With repair highlighting enabled, the arc sweep kept nine measured code views, zero independent paint builds, nine parses and zero paragraph rasters during zoom and rotation. The first two zoom moves still requested no new vector texture. The second move repeatedly spent about 1.66 ms on acquired-image repair planning under the normal tiered runtime. Disabling tiering raised this first repair to about 7.60 ms and lowered steady-state repair cost. That experiment isolates runtime configuration sensitivity but does not establish which method the runtime was compiling.

The renderer now separates command list alignment, changed-command handling and bound accumulation. A triangle command bypasses the rectangle and circle refinement path before its command and mask comparison. The acquired image and the shared MSAA attachment still contribute their respective history. The same snapshots and reader ownership rules apply.

Windows x64 SDK 11.0.100, managed runtime 11.0.0, NVIDIA RTX 5070 Ti Laptop GPU, 1600×1125 framebuffer, 4× MSAA, V-Sync off. Each comparison used three independent alternating baseline and candidate processes at the same runtime configuration. The 96-frame warmup preceded 33 batches of 16 Win32 moves per gesture. Command `dotnet <Duxel.Graphics.Tests.dll> <output> gallery-svg-pointer arc win32 burst highlight` with `DUXEL_VK_PROFILE=1` and `DUXEL_VK_PROFILE_EVERY=1`. The table shows the normal tiered configuration. Rates are calculated from Win32-message-to-Vulkan-submission durations.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Zoom | 3.126 → 2.984 | 2.664 → 2.632 | 7.863 → 7.684 | 9.787 → 9.032 | 319.9 → 335.2 | 102.2 → 110.7 |
| Rotation | 2.780 → 2.731 | 2.566 → 2.532 | 4.452 → 4.330 | 4.568 → 5.199 | 359.7 → 366.2 | 218.9 → 192.4 |

The repair-planning median fell from 78.3 to 56.9 µs for zoom (−27.3%) and from 66.5 to 53.0 µs for rotation (−20.3%). The three run ranges are disjoint for those two measurements. End-to-end submission timings overlap. Typical UI allocations remained identical at 106,463 B per zoom move and 108,897 B per rotation move. All 396 paired move records have the same transform, repair rectangle and raster work. All six paired endpoint PNGs are byte-identical. The second zoom move still needs about 1.60 ms of repair planning with tiering enabled. The highest percentile is based on a small number of samples, and physical display timing and Windows NativeAOT performance remain unmeasured.

The same run corrected GPU fixtures that assumed a 320×240 framebuffer on the 125% DPI host, where Vulkan read back 400×300 pixels. The HUD and paint-group fixtures use framebuffer width for pixel indexing, and the repaint fixture uses the actual height for its whole-frame and border checks. The expected compositing values stay exact. Windows passed 22 GPU HUD and 3,774 presentation-reuse checks. A fresh Linux x64 NativeAOT Gallery built with SDK 11.0.100 and runtime 11.0.0 passed all 109 WSLg presentations. The Linux run verifies presentation behavior and does not measure paired performance. The managed benchmark files, Vulkan timings, paired comparison and regression outputs are in `W:/Cache/Duxel/gallery-source-20261117/arc-reveal-2084`.

## Arc comparison and navigation allocations

EventPipe allocation samples from both Arc gestures led to the left navigation. Expanded categories concatenated the same accessibility parent ID once per destination on every input traversal. Expansion queries also created a predicate to look up a group whose index was already available. The navigation now creates one child-collection ID per nonempty group when its root ID changes, reads expansion by group index and skips the keyboard row search when there are no key events. The public selection and accessibility relationships keep the same identifiers and values. Storage grows with group count and does not accumulate keys during gestures.

Windows x64 with SDK 11.0.100 and managed runtime 11.0.0, 1280×900 framebuffer, 4× MSAA and V-Sync off. Run `Duxel.Graphics.Tests <output> gallery-svg-pointer arc win32 burst highlight` and `Duxel.Graphics.Tests <output> gallery-code-selection arc`. Three fresh processes per build alternated base, candidate, candidate, base, base, candidate. The pointer scenario warmed 96 frames and recorded 33 batches of 16 moves per gesture. The selection scenario warmed 96 frames, then 24 per mode, and recorded 160 frames per mode. Times end at Vulkan submission. Rates describe the measured intervals, with the slowest 1% contributing to the last column.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate | Mean UI allocation per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Zoom | 3.810 → 3.844 | 3.752 → 3.731 | 9.305 → 10.224 | 13.890 → 14.092 | 262.5 → 260.2 | 72.0 → 71.0 | 179,951 → 117,387 B (−34.8%) |
| Rotation | 2.723 → 2.629 | 2.527 → 2.473 | 3.991 → 4.157 | 4.538 → 4.671 | 367.3 → 380.4 | 220.4 → 214.1 | 108,094 → 45,550 B (−57.9%) |
| Selection, one move | 2.240 → 1.918 | 2.232 → 1.854 | 3.064 → 2.308 | 4.376 → 3.014 | 446.5 → 521.5 | 185.9 → 327.1 | 61,693 → 30,347 B (−50.8%) |
| Selection, 16 moves | 3.078 → 3.206 | 3.069 → 3.134 | 3.799 → 4.072 | 4.349 → 4.786 | 324.9 → 311.9 | 167.9 → 169.6 | 105,504 → 43,204 B (−59.1%) |

All paired transform values, repaint rectangles, selection lengths and final PNGs match. Zoom still needs one full repair at the first use of a swapchain image. Rotation needed zero full repairs. Both gestures requested zero SVG textures during measured moves. The median sampled peak of the process's private memory in the 16-move selection was 305.02 → 302.80 MiB. Allocation reductions hold across the observed run ranges. Submission timing is mixed, with slower tail medians during large moves. The widest zoom jumps move the source panel and can still expose a large area. Physical input-to-display and Windows NativeAOT timing are unmeasured. Two DirectText cache candidates were removed after their mixed timing and allocation results. The retained text test takes one extra frame before checking eviction of a formerly visible key, in line with the previous frame's paint protection.

Windows Release solution and focused test builds passed without warnings. Nav passed 77 checks, including group and row relationships after reparenting, and its section rendering passed 12 checks. Gallery passed 326, SVG 17, repaint 134, and presentation reuse 3,774. Linux x64 NativeAOT running on .NET 11.0.0 passed 109 WSLg Gallery presentations. The package-lock hashes match the preceding checkpoint. Linux timing was outside this comparison.

Raw data, EventPipe traces, and comparison scripts are under `W:/Cache/Duxel/gallery-source-20261117/arc-cache-2088`. The accepted navigation pair is `nav-comparison.json`.

## Arc comparison when code returns to the viewport

Large zoom changes alternately hid and revealed the same code. The public accessibility node correctly omitted geometry while fully clipped, but the next visible frame rebuilt every character and caret rectangle. The immediate context now keeps one immutable geometry snapshot while the clipped code is still rendered. The geometry arrays are capped at 256 KiB per context. Reopening validates the same content, layout and font keys, and uses the live viewport. Removing the widget or changing the tree releases the extra reference. Clipped nodes continue to publish no geometry.

Windows x64, SDK 11.0.100 and managed runtime 11.0.0, 1280×900, 4× MSAA, V-Sync off. In `Duxel.Graphics.Tests <output> gallery-svg-pointer arc win32 burst highlight`, 96 warmup frames preceded 33 batches of 16 moves per gesture. `gallery-code-selection arc` warmed a further 24 frames and recorded 160 frames per mode. Three pairs used the order base, candidate, candidate, base, base, candidate. Times end at Vulkan submission.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate | Mean UI allocation per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Zoom | 3.695 → 2.923 | 3.675 → 2.541 | 9.564 → 7.516 (−21.4%) | 13.683 → 9.519 | 270.6 → 342.1 | 73.1 → 105.0 | 117,387 → 40,967 B (−65.1%) |
| Rotation | 2.612 → 2.606 | 2.439 → 2.442 | 3.900 → 4.129 | 4.327 → 4.341 | 382.8 → 383.8 | 231.1 → 230.4 | 45,550 → 45,550 B |
| Selection, one move | 2.271 → 2.206 | 2.386 → 2.266 | 2.760 → 2.758 | 3.850 → 3.392 | 440.3 → 453.2 | 187.1 → 209.7 | 30,395 → 30,419 B |
| Selection, 16 moves | 3.013 → 3.072 | 2.951 → 3.105 | 3.731 → 3.784 | 4.003 → 4.133 | 331.9 → 325.5 | 181.0 → 188.6 | 43,528 → 43,439 B |

The zoom p95 run ranges were 9.510–9.704 ms before and 7.365–7.909 ms after. Reported rotation and selection timing varied. Selection's median sampled peak private memory was 303.45 → 302.37 MiB in the 16-move mode, with overlapping ranges. Transform, selection and repair sequences matched exactly, as did all settled PNGs. Initial swapchain image repair, physical pointer-to-display latency and Windows NativeAOT timing remain open. Evidence and comparison scripts are under `W:/Cache/Duxel/gallery-source-20261117/arc-reclip-2089`.

Release solution and focused build completed with zero warnings and errors. The four targeted geometry checks passed within the 1,811 Observer checks. Gallery integration (326), SVG (17), text accessibility (83), repaint (134), and presentation reuse (3,774) passed. Fresh Linux x64 .NET 11.0.0 NativeAOT Gallery passed 109 WSLg presentations. Both package-lock hashes are unchanged.

## Native text selection fragments

Selecting C# code created a growing list of character rectangles per visible line, then copied the merged result again when scaling it. Short selections now use temporary stack storage for up to 64 fragments. Larger selections count their remaining fragments, use a returned pool rental up to 4,096 fragments, or allocate exact temporary storage for longer lines. The caller scales the single returned array in place. Visual order, overlap merging and source grapheme boundaries use the existing rules.

Windows x64 with SDK 11.0.100 and managed runtime 11.0.0, 1280×900, 4× MSAA and V-Sync off. The `gallery-code-selection arc` scenario warmed 96 frames, then 24 per mode, and recorded 160 frames per mode. The `gallery-svg-pointer arc win32 burst highlight` controls warmed 96 frames and recorded 33 batches of 16 moves per gesture. Three pairs ran base, candidate, candidate, base, base, candidate. Times end at Vulkan submission.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate | Mean UI allocation per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Selection, one move | 2.187 → 2.059 | 2.237 → 2.022 | 2.942 → 2.463 | 3.678 → 3.245 | 457.3 → 485.6 | 269.6 → 208.3 | 30,405 → 13,582 B (−55.3%) |
| Selection, 16 moves | 3.207 → 3.227 | 3.129 → 3.112 | 3.900 → 3.854 | 4.523 → 4.599 | 311.8 → 309.9 | 179.2 → 216.7 | 43,456 → 26,609 B (−38.8%) |
| Zoom control | 2.907 → 2.958 | 2.539 → 2.601 | 7.383 → 7.862 | 9.561 → 9.363 | 343.9 → 338.0 | 104.6 → 106.8 | 40,967 → 40,967 B |
| Rotation control | 2.636 → 2.609 | 2.502 → 2.454 | 3.981 → 3.854 | 4.445 → 4.599 | 379.4 → 383.3 | 225.0 → 217.5 | 45,550 → 45,550 B |

Allocation ranges were disjoint for both selection modes. Selection timing is mixed. The sampled process private-memory peak in the 16-move mode was 303.36 → 303.34 MiB, with overlapping ranges. All final PNGs, repair rectangles, transform and selection outputs matched. The first two rotation moves were the slowest in all six runs. They allocated 232,376 and 191,472 B during UI composition, compared with a median of 34,816 B for the remaining moves. Code measurement and syntax counts stayed fixed. There were zero SVG texture requests and zero paragraph rasters on measured rotation moves. Raw runs, comparisons and per-frame rotation evidence are in `W:/Cache/Duxel/gallery-source-20261117/arc-selection-2090`. Physical input-to-display timing remains unmeasured.

Release solution and focused build passed without warnings. The 219 paragraph geometry checks include reversed visual runs at the 64, 65 and 4,097 fragment boundaries. The GPU paragraph geometry, text accessibility, Gallery, SVG, repaint and presentation reuse suites passed. Fresh Linux x64 NativeAOT Gallery passed 109 WSLg presentations with package-lock hashes intact.

## Rotation startup and DirectWrite diagnostics

The pointer replay now exports its existing composition start and end counters. Three Windows EventPipe allocation traces aligned with those ranges by QPC and sampled DirectWrite font selection and bitmap creation in the rotation press and first two moves. On the first move each trace sampled the same 21,696-byte array in the SVG page status line's bitmap path. The second move sampled font selection through the browser comparison's size label. Allocation samples identify the calling paths and do not account for every byte.

DirectWrite font selection now appends each Rune to the existing builder through two UTF-16 stack characters. Glyph lookup still logs errors and missing glyphs when diagnostics are enabled. It only creates the message when a diagnostics file is configured. The selected fonts, native glyph output and live value updates use the existing behavior.

Windows x64 with SDK 11.0.100 and managed runtime 11.0.0, 1280×900, 4× MSAA, V-Sync off. `gallery-svg-pointer arc win32 burst highlight` warmed 96 frames and measured 33, each with 16 moves per gesture. `gallery-code-selection arc` warmed 96 frames plus 24 per mode and measured 160 per mode. Three pairs alternated base, candidate, candidate, base, base, candidate. Timing ends at Vulkan submission.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate | Mean UI allocation per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Rotation | 2.600 → 2.619 | 2.458 → 2.499 | 3.653 → 3.922 | 4.356 → 4.708 | 384.7 → 381.8 | 229.5 → 212.4 | 45,550 → 44,456 B (−2.4%) |
| Zoom | 2.967 → 2.911 | 2.606 → 2.515 | 7.688 → 7.766 | 10.105 → 9.688 | 337.1 → 343.6 | 99.0 → 103.2 | 40,967 → 39,896 B (−2.6%) |
| Selection, one move | 2.244 → 2.273 | 2.358 → 2.418 | 2.762 → 2.934 | 3.218 → 3.339 | 445.6 → 439.9 | 301.2 → 217.3 | 13,555 → 13,575 B |
| Selection, 16 moves | 3.090 → 3.114 | 3.059 → 3.115 | 3.800 → 3.758 | 4.295 → 4.796 | 323.6 → 321.2 | 218.9 → 207.1 | 26,720 → 26,810 B |

The first two rotation moves consistently fell from 232,376 to 210,680 B and 191,472 to 177,072 B. Timing showed no improvement on rotation. All final PNGs, repair rectangles, transforms and selected ranges match. The normal diagnostic-off path uses no extra persistent storage. The cost of rasterizing newly formatted labels still applies, and physical pointer-to-display latency and Windows NativeAOT performance remain unmeasured. Traces, source parser and comparison are under `W:/Cache/Duxel/gallery-source-20261117/arc-rotation-2091`.

Release solution and focused builds passed without warnings. Windows shaping passed 56 checks, paragraph geometry 219, font context 36, styled paragraph 171, Gallery 326, SVG 17, repaint 134 and presentation reuse 3,774. With diagnostic logging enabled, the SVG regression also passed and recorded the expected missing-glyph messages. Both package-lock hashes are intact.

## SVG Gallery live number labels

The SVG transform and browser-size labels now draw the same live formatted values using reusable text fragments during a transform gesture. The presentation places one row and takes its height from a stable sample of the same label. It draws each digit through the existing text cache. The established transform idle deadline restores the original whole-label presentation. No renderer or texture cache budget was changed. An always-segmented trial displaced following rows and raised steady code-selection allocation, so the final comparison uses the gesture-scoped presentation.

Windows x64, SDK 11.0.100 and managed runtime 11.0.0, 1280×900, 4× MSAA, V-Sync off. Run `gallery-svg-pointer arc win32 burst highlight` (96 warmup frames and 33 batches of 16 moves per gesture) and `gallery-code-selection arc` (96 initial warmup, then 24 per mode and 160 measured). Three pairs alternated control, final, final, control, control, final. Timing ends at Vulkan submission.

| Interaction | Mean ms | Median ms | p95 ms | p99 ms | Mean interval rate | 1% low interval rate | Mean UI allocation per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Rotation | 2.613 → 2.633 | 2.500 → 2.566 | 4.014 → 2.998 (−25.3%) | 4.459 → 3.232 | 382.8 → 379.8 | 224.3 → 309.4 | 44,456 → 38,042 B (−14.4%) |
| Zoom | 2.904 → 2.970 | 2.574 → 2.647 | 7.578 → 6.313 (−16.7%) | 9.372 → 8.958 | 344.4 → 336.7 | 106.7 → 111.6 | 39,896 → 34,572 B (−13.3%) |
| Selection, one move | 2.329 → 2.268 | 2.458 → 2.403 | 2.812 → 2.823 | 3.583 → 3.485 | 429.3 → 440.9 | 214.3 → 208.9 | 13,603 → 13,549 B |
| Selection, 16 moves | 3.037 → 3.157 | 3.003 → 3.136 | 3.815 → 4.210 | 4.111 → 4.452 | 329.3 → 316.7 | 240.7 → 209.8 | 26,752 → 26,783 B |

The first two rotation frames dropped from 210,680 to 42,960 B and 177,072 to 37,888 B. Rotation p95 ranges were 3.999–4.175 ms and 2.982–3.053 ms. Zoom p95 ranges were 7.480–7.896 ms and 6.131–6.438 ms. The managed median rose slightly on the transform scenarios. In the selection control the segmented presentation is inactive, and the p95 ranges of the 16-move case overlapped. Its sampled peak private-memory ranges also overlapped around 303–304 MiB. All transform and selection sequences matched. Rotation did zero whole-window repairs in every run. Rotation repair area summed across measured moves rose 3.4%, and zoom area stayed the same with one initial whole-window repair per run. Highlighted settled captures differ only at the green diagnostic outline. The final selection captures are byte-identical. Raw evidence is under `W:/Cache/Duxel/gallery-source-20261117/arc-labels-2092`. Physical input-to-display latency and Windows NativeAOT performance remain unmeasured.

A separate highlight-off Windows pair settled at the same PNG hash. Release solution and focused builds passed without warnings, as did Gallery 326, SVG 17, repaint 134 and presentation reuse 3,774. A fresh Linux x64 NativeAOT Gallery passed all 109 WSLg presentations on .NET 11.0.0 with both lock hashes intact.

## Windows visible desktop observations

The optional `gallery-svg-pointer arc win32 burst desktop` test opens its owned window after the usual warmup. Each submitted movement frame alternates a small marker in the same draw data. A screen device context reads its center only while the owned window covers the point. The probe rejects obstruction, unreadable pixels and one-second timeouts. It records the first successful observation after submission with the matching frame ID. The ordinary replay leaves the window hidden and emits no marker.

Windows x64, SDK 11.0.100 and managed runtime 11.0.0, 1280×900, 4× MSAA, V-Sync off. Three fresh executions registered 33 zoom and 33 rotation movements each. Each movement posts 16 synthetic Win32 messages and waits for its marker before the next movement. The table shows the median of the three per-run summaries.

| Interaction | Message to desktop median | Message to desktop p95 | Return from Vulkan submission to observation median | Return from Vulkan submission to observation p95 |
| --- | ---: | ---: | ---: | ---: |
| Zoom | 4.97 ms | 8.86 ms | 2.38 ms | 5.73 ms |
| Rotation | 4.54 ms | 8.55 ms | 1.93 ms | 5.38 ms |

The zoom p95 range was 8.78–8.91 ms and rotation was 8.36–8.57 ms. The display check polls after submission and includes the wait to observe the marker. It does not measure mouse hardware, display scanout or monitor response. Serial confirmation and the marker change the workload from the submission-only replay. No before/after desktop-speed claim was made. The official PresentMon 2.6.0 binary was verified using the release asset hash. Its ETW attempt returned access denied under the current Windows account. Evidence, including all 198 frame matches and the denied trace attempt, is under `W:/Cache/Duxel/gallery-source-20261117/arc-display-2093`.

The Windows Release Graphics test build passed without warnings. The unmarked replay reproduced the previous highlight-off endpoint PNG SHA-256 `5222A3AB4A0FCA947D4F6728033726A11897B735B4D193BCE13C63E0225A2DC0`. Repaint passed 134 checks. Product code was unchanged.

## App input handoff during rapid pointer movement

The Gallery's source selection, zoom and rotation use the same App input handoff. If multiple native packets arrived before the UI worker drained them, each merge copied the entire pending prefix. The producer now appends into an owned list and detaches that list when the worker drains it. The app no longer retains the drained events for idle comparisons. An ordered excursion ending back at the previous position still requests processing. The change adds no render cache and keeps packet order.

The isolated .NET 11.0.0 Linux x64 NativeAOT `InputProbe` measured copying each packet and appending it through the previous or current App implementation. SDK 11.0.100, three fresh processes per side ordered before/after/after/before/before/after, 96 warmup and 144 measured drains per process. Every run checked all captured positions. Table values are medians across runs. Rates describe input batches per second.

| One-move packets per drain | Median µs | p95 µs | p99 µs | Mean batches/s | Slowest 1% batches/s | Allocated bytes per drain |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 16 | 11.25 → 6.05 (−46.2%) | 17.8 → 12.7 | 52.5 → 21.9 | 76,535 → 158,190 | 13,514 → 32,103 | 16,448 → 5,424 (−67.0%) |
| 128 | 141.65 → 29.45 (−79.2%) | 189.3 → 37.6 | 242.3 → 62.1 | 6,881 → 32,601 | 3,837 → 14,695 | 877,952 → 43,128 (−95.1%) |
| 1,024 | 7,236.90 → 77.80 (−98.9%) | 7,982.0 → 99.8 | 8,724.6 → 484.4 | 137 → 11,086 | 113 → 1,988 | 54,734,720 → 344,256 (−99.4%) |

Windows `dotnet build Duxel.slnx -c Release` and the .NET 11 Graphics build passed. `app-ordered-input-only` passed 36, `native-ordered-app-only` 40, `ordered-input-batch-only` 597, `observer-svg` 17 and `repaint` 134. Run the Gallery regressions with `gallery-svg-pointer arc win32 burst highlight` and `gallery-code-selection arc`. Their settled endpoint PNGs matched the earlier Gallery control. The Linux x64 NativeAOT Gallery verified 109 presentations and preserved both package locks. The isolated probe bypasses widget layout, GPU submission and display. Real desktop packet counts, process peak memory and user-visible latency under uninterrupted fast input still need live measurement. Full data and reproduction source are in `W:/Cache/Duxel/gallery-source-20261117/arc-input-handoff-2095`.

## Native SVG texture memory during replacement

The previous logical-limit test did not allocate large native surfaces. The follow-up creates 3,000×3,000 and 3,200×3,200 textures and alternates them 32 times. It captures the authored pixel at the start and end and samples allocation sizes without a readback during the loop. The original allocation record returned zero for vector textures on both hosts. The corrected record stores the Vulkan memory requirement already available at image creation. This also eliminates duplicate queries for reusable text and paint group MSAA allocations.

| Runtime | Frame slots | Maximum pending textures | Peak current and pending bytes |
| --- | ---: | ---: | ---: |
| Windows x64 managed .NET 11.0.0 | 1 | 1 | 77,922,304 |
| Windows x64 managed .NET 11.0.0 | 3 | 3 | 155,844,608 |
| Linux x64 NativeAOT .NET 11.0.0 on WSLg | 1 | 1 | 77,056,000 |
| Linux x64 NativeAOT .NET 11.0.0 on WSLg | 3 | 3 | 154,112,000 |

All 128 frames kept one current texture. Pending allocations reached zero after consuming the frame slots. Only the SVG textures in the test are counted. The measurement excludes other renderer allocations and transient working surfaces. The baseline reported zero because it lacked the accounting field, so a memory reduction percentage cannot be calculated. No render speed change is inferred. Logs and frame records are in `W:/Cache/Duxel/gallery-source-20261117/svg-native-pressure-2119/final`.

## Gallery arc interaction CPU and GPU timing

The selection replay now logs draw-data frame and presentation IDs. The analyzer joins these to accepted Vulkan submissions and maps delayed timestamp reads back to the source submission. Three independent runs per replay at 1280×900 and 4× MSAA joined 1,158 repair records and 1,152 GPU timestamps. The other six were final selection submissions with no slot reuse before the replay ended. Each replay produced identical final PNGs across its three runs.

| Motion | Median repair pixels | Range of median UI times | Range of median GPU render times |
| --- | ---: | ---: | ---: |
| Zoom | 711,144 | 1.83–1.84 ms | 38.43–40.13 µs |
| Rotation | 711,144 | 1.76–1.78 ms | 37.22–39.90 µs |
| Selection, one requested motion | 429,500 | 1.16–1.50 ms | 33.65–33.92 µs |
| Selection, sixteen requested motions | 429,500 | 2.48–2.62 ms | 31.94–33.50 µs |

Each zoom run included exactly one full repair. Rotation and selection stayed partial. More selection input increased CPU composition while area and GPU render time stayed comparable. This suggests investigating input processing and page composition before changing the GPU path for that case. The samples are from an instrumented Windows x64 .NET 11.0.0 fixture. Timing excludes physical pointer and display response. No production setting or resource policy changed, so there is no before/after speedup to report. Evidence `W:/Cache/Duxel/gallery-source-20261117/arc-gpu-2120/comparison.json`.

## CPU stages in the Gallery arc selection replay

Use `dotnet <Duxel.Graphics.Tests.dll> <output> gallery-code-selection arc input-profile` to record the existing Core stage metrics and Gallery section timers in `selection-stages.tsv`. They join to the existing selection trace by draw-data frame ID. The switch is optional and adds no instrumentation to the application. Three profiled Windows x64 runs covered 960 frames with identical selection lengths, repair sizes and final images. The unprofiled Markdown replay also passed.

| CPU stage | One requested motion | Sixteen requested motions |
| --- | ---: | ---: |
| UI build | 1.15–1.16 ms | 2.36–2.68 ms |
| Core preparation | 0.061–0.062 ms | 0.087–0.128 ms |
| Input-only page | No input-only pass | 0.428–0.454 ms |
| Input-only navigation and options, separately | No input-only pass | 0.233–0.259 ms and 0.073–0.075 ms |
| Final presentation page | 0.582–0.589 ms | 0.721–0.782 ms |

The table shows the range of three run medians. The sixteen-motion batch coalesced to two traversals. The extra traversal covers the page, navigation and options, and the final page pass also costs more in this condition. These runs do not isolate the reason for the final-pass increase, and they do not measure physical input latency. This change only adds profiling to the test so there is no production speedup or added cache. Evidence `W:/Cache/Duxel/gallery-source-20261117/arc-selection-stages-2121/comparison.json`.

## Navigation group traversal during code selection

The Arc selection trace showed an extra navigation pass during a pointer burst. The first candidate reused the last resolved destination index for validation and page lookup. Its navigation timings were mixed, so the candidate was removed. The second candidate replaces the all-rows scan per accessibility group with one sequential scan of the prepared rows. With no accessibility tree it skips the group scan. The same rows still supply group bounds and labels. The added retained-memory cost is zero.

Each candidate was compared with three alternating Windows x64 .NET 11.0.0 runs per side. Only the Core assembly changed. Input counts, selection extents, repair dimensions and final PNGs matched. The retained change's median of sixteen-motion UI run medians was 2.556 → 2.336 ms (8.60% lower). The individual run ranges overlap and sampled private-memory peaks also overlap. The measurement does not establish an interaction speedup or a whole-process memory reduction. Nav passed 77 checks and ordered input passed 597. The full Gallery suite reproduced the same three pre-existing SVG failures in the baseline and candidate. Evidence `W:/Cache/Duxel/gallery-source-20261117/arc-nav-2122`.

## SVG code update and idle verification

The broad Gallery checks used one or two draws after transforming or changing the background. That could sample the first settled preview while code updates were still scheduled. The test fixture now advances to announced deadlines and renders at most eight scheduled frames. Afterward the transform and background checks compare all six active Arc examples, and the idle check requires an unchanged scene/texture count and no additional deadline. A sequence that keeps scheduling after eight frames still fails.

The same Windows x64 .NET 11.0.0 product assemblies passed 1,813/1,813 full Gallery checks and 17/17 focused GPU SVG checks with the corrected fixture. The previous fixture passed 1,810/1,813. Core, Svg and Vulkan hashes match. No application timing policy or production performance changed. Evidence `W:/Cache/Duxel/gallery-source-20261117/svg-scheduled-tests-2123`.

## NavigationBar repaint on Linux

With continuous mode and repaint highlighting, the managed Linux Gallery repainted all 1280×900 pixels in 81 of 81 quiet frames after entering NavigationBar. The changed paint-group matte had a consumer revision but also invalidated the global acquired-image epoch. The renderer now follows the consumer revisions, preserving pixels outside that group's coverage. That change alone left 144 quiet frames repairing the 222×36 period selector. SegmentedControl then stopped rebuilding its layout for different maximum widths that produce the same actual width.

Final Linux replays had zero full-window repairs in 652 managed and 1,277 NativeAOT accepted frames covering entry, title actions and period selection. In the quiet interval before Back, managed submitted one frame with no content repair and NativeAOT submitted none. Neither runtime ran a paint-group pass. Actual period transitions can still repaint the selector, and title actions also update the live source block. Two Windows baseline/final PNG pairs match exactly. This compares repaint behavior; there is no interaction timing improvement claim. Evidence `W:/Cache/Duxel/gallery-source-20261117/navigation-repaint-2124/comparison.json`.

The follow-up on Linux x64 WSLg compared partial repaint with forced full repaint using the current source in .NET 11.0.0 managed and NativeAOT. Each of the four runs passed 3,792 checks and compared 646 RGBA pairs, including 44 paint-group images. All four acquired images participated. Across runtimes, 1,292 same-slot hashes were identical. The texture-only dependency change required a scoped consumer repair at both 1× and 4× MSAA on an initialized image. [Run evidence](W:/Cache/Duxel/gallery-source-20261117/paint-group-linux-2125/verification.json).

## Dragging the SVG source scrollbar

The document child used to draw at the old offset and advance the captured scrollbar after laying out its contents. The caller then requested another frame. The child now resolves the thumb against the previous track geometry before laying out the document. When a captured batch consists entirely of pointer moves, only the final position is applied. Press, release and other input still enter the usual ordered path.

Run `dotnet <Duxel.Graphics.Tests.dll> <output> gallery-svg-code-scroll` to post native Win32 press, drag and release messages to both Gallery tabs. Three Windows managed runs per build used 16 measured bursts of 64 moves per tab. The runs went from two UI passes to one per burst. The difference between the committed scroll offset and the code text origin went from 111.22 to 0 px for Stroke Domains and from 129.32 to 0 px for Arc Geometry. Offsets and all settled images match, with zero new vector jobs during the bursts. Vulkan still repairs the 952×747 document viewport. No framebuffer scroll copy or retained cache was added.

The median of the three run p95s for UI build and the renderer call combined was 2.95 → 4.74 ms for Stroke Domains and 5.95 → 6.22 ms for Arc Geometry. The old measurements include frames that drew a lagged position, so they do not establish a rendering speed comparison at identical on-screen positions. The maximum transient managed allocation over the measured frames was 23,016 → 189,864 bytes and 158,160 → 247,864 bytes, respectively. This test does not reproduce a one-FPS drop or establish improvement on Linux. Ordered input passed 597 checks, ScrollView 97, and Gallery SVG 17. [Measurements](L:/Cache/Duxel/svg-thumb-2141/final-comparison.json).

The Linux `x11-gallery-svg-thumb <pid> <result.json> <window-id>` probe captures the Gallery scrollbar with a button press and sends 64 native moves in each direction on both tabs. The button stays down until the original code pixels return. With .NET SDK 11.0.100 and runtime 11.0.0, managed and NativeAOT WSLg runs passed both tab checks and exited normally. The sampled code regions match across runtimes for all six states and match within each runtime after returning. There were no full-window repairs or new vector outputs over 14 accepted frames between sending moves and capturing the result. Managed repaired 952×747 pixels on all five frames. NativeAOT repaired up to 1000×839 or 954×882 pixels over nine frames. Client timestamps include X11 capture time. The first matching captures at 40.25–81.91 ms do not measure GPU time, physical display latency, or a speedup. Both runs used four software raster threads. The verification harness changed and the Gallery rendering code did not. [Linux captures and profile results](L:/Cache/Duxel/svg-thumb-2142/comparison.json).

Full-window capture differences identify the concurrent changes. Stroke Domains had an FPS-label update at `(308,22)–(548,32)`. Arc Geometry had changes in the navigation scrollbar at `(278,559)–(286,731)` and the outer scrollbar at `(1262,63)–(1270,811)`. Vulkan combines changed command bounds into one repair rectangle and accounts for acquired-image history. The additional rectangle area therefore includes unchanged pixels between those controls and the scrolling document. The replay now waits 650 ms after revealing the example to let the other scrollbars finish their 400 ms hold and 160 ms fade. Five of six repeated runs passed. All 25 repairs in the successful runs stayed at 952×747, with zero vector outputs. In one NativeAOT run the Stroke Domains movement changed 408 pixels in the thumb and none in the document before timeout. The Gallery exited normally and the client failed. Its cause remains open. The Gallery binaries and render settings were identical between the two sets of runs, so these results isolate a contributing condition and do not establish a product speedup. [Pixel differences](L:/Cache/Duxel/svg-thumb-2142/pixel-regions.json) · [Repeat profiles](L:/Cache/Duxel/svg-thumb-2142/scope-comparison.json).

The trace client now records X11 focus and input events and captures the pressed state separately. Four NativeAOT runs on the same Gallery executable each passed both tabs and exited normally. Across all 16 bursts, the observer saw 64 motion events and no focus or button-release event during each burst. In each downward capture the thumb moved 8 pixels and the code changed. On return, both matched their original state. In the archived failure the brightest continuous run of the thumb stayed at rows 183–231 but dimmed from 97 to 58, while the code samples were unchanged. This narrows the visual symptom to the thumb not moving; the failed run had no input trace to identify why. [Native event and pixel traces](L:/Cache/Duxel/svg-thumb-2142/thumb-trace.json).

## Mixed scrolling and source selection in SVG Layer and Borders

The Gallery sampled frame cadence using elapsed wall time. In event-driven mode, an idle wait of 2.5 seconds could produce an immediate 0.4 FPS sample even when the ensuing frame built and submitted in about 3 ms. The app host now reports the time it explicitly parked while waiting for work. Gallery FPS excludes that interval and retains actual frame-processing and presentation time. The animation clock still advances by the full elapsed time.

The independent Windows `gallery-svg-layer-mix` replay opens Layer and Borders and alternates native wheel bursts, thumb drags and code selections. It verifies that the thumb moved and returned and that the code range was actually selected. A captured read-only code block with no internal scroll range now combines consecutive pointer moves inside the window even if the pointer leaves the code clip. Capture boundaries, presses, releases, wheel input, and movement that can scroll inside the code block retain their normal ordered processing.

At 1280×900, 4× MSAA and .NET 11 managed, three alternating runs per build took 64 move packets per selection burst. Both builds used the corrected FPS sampling. Selection composition went from up to 64 passes to two. The median of run medians was 10.019 → 0.823 ms (91.79% lower), and the largest transient allocation per selection frame was 707,800 → 29,360 bytes. Scroll offsets, selection lengths, vector requests and final body pixels match. Some wheel bursts still need up to 64 passes and spent about 17–18 ms in UI composition. The result does not establish a live desktop or Linux speedup for every gesture. [Replays and timings](L:/Cache/Duxel/svg-layer-mix-2145/comparison.json).

The Windows Gallery launcher built with no warnings and acknowledged all 105 presentation scenarios. Ordered input passed 597 checks, the app paths 36 and 40, SVG 17, and ScrollView 97. Arc and Markdown selection replays also passed. [Gallery presentation results](L:/Cache/Duxel/svg-layer-mix-2145/gallery-verify/result.json).

The external `x11-gallery-svg-layer-mix <pid> <result.json> <window-id>` probe runs the same Gallery tab on Linux. It drags the scrollbar down and back, wheels down and back, then moves the held selection pointer outside the code clip. Both fresh Linux x64 builds use .NET SDK 11.0.100 and runtime 11.0.0. Managed and NativeAOT each passed three checks and exited normally. The selected AT-SPI text range was `[8,144)` and the source was unchanged. All eight document captures matched across runtimes, excluding the diagnostics, navigation and scrollbar. Both builds recorded four accepted frames with no vector jobs during the thumb and selection intervals. Those frames each repaired the 952×747 document viewport. Wheel movement and restoration were checked from pixels. This verifies Linux behavior and image equivalence. [Linux mixed input and pixel comparison](L:/Cache/Duxel/svg-layer-linux-2146/comparison.json).

### Wheel input after releasing a code selection

The text control retains focus after selection ends. This prevented the document from combining wheel events even when the pointer remained over that same read-only code block. The scoped wheel path now accepts that explicit focus owner after the button is released. The previously established code bounds, available scroll distance, direction and popup guards still govern the batch.

Three alternating Windows managed runs on each build used the same mixed-input replay. Each build ran 66 focused wheel bursts of 64 events. They went from 64 UI traversals per burst to two. The median of the three run medians for all wheel bursts was 15.951 → 0.807 ms down and 16.271 → 0.869 ms up. Selection, scroll offsets, vector request counts and final body pixels match. The largest transient allocation on wheel-up went from 788,312 to 534,816 bytes. No retained cache was added. [Before and after replays](L:/Cache/Duxel/svg-focused-wheel-2147/comparison.json).

The fresh Linux .NET 11 managed and NativeAOT Gallery builds each passed four X11 checks, including wheeling after a code selection and preserving its AT-SPI range. All 11 document captures matched across the runtimes. This Linux test verifies behavior and image parity. The Windows validation also passed the ordered-input 597, ScrollView 97 and Gallery SVG 17 checks, plus the real code-scroll regression. [Linux comparison](L:/Cache/Duxel/svg-focused-wheel-2147/comparison-linux.json).

## Reusing cache records for versioned layers

Replacing owned layer content previously allocated another cache record at the matching slot. The slot now reuses its record after retiring the previous source and clears its derived replay data. Submitted frames keep their own draw lists and resource leases, which protect the previous buffers until the final consumer releases them. This adds no cache entries.

With .NET 11.0.0, each build ran three alternating repetitions with 128 warm-up frames and 1,024 measured frames per flat or projected layer. UI-thread allocation dropped from 1,640 to 1,216 bytes per revision frame on Windows managed (25.85%) and from 1,632 to 1,208 on Linux NativeAOT (25.98%). All measured frames kept the same geometry key. The presentation suite compared 1,284 Windows and 1,292 Linux before/after pixel pairs with one and three frame slots. All matched, including replay while earlier frames held their sources. The measurement covers this allocation path. The full ERD process memory ceiling and Windows NativeAOT remain unverified. [Allocation and pixel comparison](L:/Cache/Duxel/layer-record-2149/verification.json).

## Sharing retained layer capture scratch

When a draw list took ownership of its builder buffers, each cached layer's builder received replacement buffers sized for the same geometry. A 1,200-table ERD revealed the cost because thousands of layer builders kept that scratch capacity while their content was replayed. Cached layer captures now share a builder at each open layer depth. The builder transfers its finished buffers to the draw lists, which continue to own them under the established frame leases. Dynamic layers keep their existing builder path.

In Linux x64 NativeAOT with .NET SDK 11.0.100 and runtime 11.0.0, the 256-layer flat and projected probes each ran three alternating processes per build. Live managed memory after collection fell from 50.072 to 25.228 MiB (49.62%). Initial render allocation fell from 52,408,552 to 26,357,848 bytes. Revising a single layer while an older draw-data frame was still held allocated 121,328 bytes with either build. The geometry hashes matched across builds and the old frame kept its original hash. Vulkan passed 3,794 checks per frame-slot configuration and all 1,292 before/after image pairs matched across slot counts 1 and 3. [Probe and Vulkan evidence](L:/Cache/Duxel/erd-managed-2150/native-verification.json).

On Windows, build the ERD with `./run-fba.ps1 samples/fba/erd_layer_lab.cs -Managed -NoLaunch -NoCache -TargetFramework net11.0-windows7.0`. Run `scripts/measure-erd-resources.ps1` with the app host and executable hash, `-Scenario motion-tour -Tables 1200 -WarmupSeconds 30 -Seconds 180 -IncludeGcMetrics -IncludeVirtualMemory`. Both managed .NET 11.0.0 builds completed normally with 181 steady samples. Private Bytes peaked at 3,956,858,880 before and 2,819,588,096 after (28.74% lower). Median GC commit fell by 1,064.32 MiB. Peak working set went from 1,185,951,744 to 1,137,442,816 bytes. The draw shape, active Vulkan static buffer bytes and compiled cache bytes matched. The candidate still gained 6.12 MiB of Private Bytes over its last minute, compared with 3.22 MiB in the baseline. This is one motion run per build. The baseline idle observation was rejected after four additional frames, and the steady long-run growth limit remains open. [Windows and Linux measurement records](L:/Cache/Duxel/erd-managed-2150/verification.json).

The Windows motion runs each supplied 19 process and GC sample pairs with the same logged frame ID. In the last seven pairs, Private Bytes rose by 3.22 MiB before and 6.12 MiB after. GC commit rose by 2.13 and 2.31 MiB. Neither run collected generation 2 in the steady interval. These samples come from separate clocks and cannot establish which allocator owns the remaining increase. The Windows scheduling checks also passed all 38 cases on both builds, including hidden and visible nested retained layers. [Motion memory observations](L:/Cache/Duxel/erd-managed-2150/windows-motion-commit-attribution.json) · [Scheduling checks](L:/Cache/Duxel/erd-managed-2150/schedule-verified-after/result.json).

The matching Windows idle control kept the same 1,200-table setup, SDK, runtime and binaries as motion. Each run completed 180 seconds after 30 seconds of warmup, with zero additional frames, no resize and a normal close. Median Private Bytes went from 3,924,389,888 to 2,793,238,528 (28.82% lower). Median working set and GC commit also fell, by 38,301,696 and 1,264,173,056 bytes. The failed first baseline is preserved. The managed app hosts share a hash, and separate payload hashes identify the application DLL and the changed Core. The Windows Vulkan suite passed 3,778 checks at each of 1 and 3 frame slots, and all 1,284 before/after image pairs matched. [Idle and Windows GPU comparison](L:/Cache/Duxel/erd-managed-2150/windows-idle-comparison.json).

### Allocation-base observations during a five-minute ERD run

The Windows managed .NET 11 candidate from the paired run completed 300 seconds of motion after 30 seconds of warmup. The resource observer collected 301 process samples and 44 allocation-base snapshots. The same largest allocation base was present throughout and grew 14.94 MiB, close to 14.81 MiB of runtime GC commitment growth. It matched the last logged GC commitment in 23 snapshots. In 20 others the difference was within 132 KiB, and the remaining difference was 2.19 MiB. The scan and runtime log are asynchronous.

Private Bytes rose 13.87 MiB from start to finish and peaked at 2,825,035,776 bytes. In the last minute they fell 1.21 MiB, with one address that previously held 3.11 MiB of committed private memory absent at the end. Vulkan active and compiled cache sizes stayed constant in the logs. This run identifies where most of the growth was observed. It does not establish an indefinite bound or allocator ownership for the transient addresses. Reproduce with the same hashed image and `scripts/measure-erd-resources.ps1 -Scenario motion-tour -Tables 1200 -Seconds 300 -WarmupSeconds 30 -IncludeGcMetrics -IncludePrivateAllocations`, supplying the image, its hash and a fresh output directory. [Allocation-base and GC comparison](L:/Cache/Duxel/erd-vma-2152/verification.json).
