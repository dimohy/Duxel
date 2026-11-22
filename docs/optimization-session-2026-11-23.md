# Performance session — 2026-11-23

## CodeView paragraph assembly

Managed stack samples from the first SVG CodeView scroll showed time spent assembling positioned runs and copying paragraph data. Repositioning now writes directly into the glyph array owned by the new run. CodeView also transfers ownership of the freshly assembled paragraph arrays. Public constructors still copy their input and retain the same validation.

Windows RTX 5070 Ti, .NET SDK 11.0.100, runtime 11.0.0. The Gallery diagnostic runtime and assets were identical across variants except for the rebuilt Core DLL. At a fixed 1483 × 956 framebuffer and 141% SVG zoom, each process warmed through the same page setup and then received four bursts of 24 desktop wheel events. The frame timing and allocation comparisons each used three independent processes per variant. The frame timings used application QPC with Vulkan profiling disabled.

| Metric | Before | After |
| --- | ---: | ---: |
| Peak UI-thread allocation during the first wheel burst, median of runs | 1,006,920 B | 815,896 B |
| CPU frame time during active input, median / p95 / p99 | 1.18 / 2.42 / 4.36 ms | 1.19 / 2.15 / 6.04 ms |
| Acknowledged render submissions during active input, mean / 1% low per second | 748.2 / 232.8 | 745.4 / 156.8 |

The first-burst peak allocation fell 19.0%. Timing ranges overlapped, and the candidate had a higher median p99. The experiment did not establish a change in frame speed or display cadence.

Release solution build and 109 Windows checks passed. Those checks covered mixed-script code reflow, paragraph cache lifetime, scrollbar dragging, text accessibility and defensive copies of caller-owned arrays. A 1050 × 620 desktop crop after identical scrolling matched exactly. The run data is under `artifacts/codeview-owned-arrays-20261122` in the local workspace. The existing SVG wheel replay runs with `-DesktopWheel -WheelOnly -VulkanProfile Off`; the separate allocation replay adds `DUXEL_APP_PROFILE_MEMORY=1`.

On WSLg, Linux x64 NativeAOT builds on Dozen and RTX 5070 Ti each passed 108 Gallery presentation checks. In the normal 1280 × 900 Gallery window, a click opened SVG and 30 X11 wheel events scrolled down and back. Five desktop capture pairs matched exactly in the main content area, including both scrolled CodeView positions. WSLg results are under `artifacts/codeview-owned-arrays-wslg-20261123` in the local workspace. WSLg timing, Windows NativeAOT and long-run memory for this change remain open.

## Ordered accessibility text geometry

Accessibility text capture visits graphemes in source order. The capture now advances through visual lines and boundaries in that order. Separate cursors preserve the distinction between the caret position and the snapped start and end of a grapheme, including preedit text.

Windows RTX 5070 Ti, SDK 11.0.100 and runtime 11.0.0. The 120-line mixed-script probe used 12 warmup iterations and 30 rebuild/reuse pairs in each of three independent processes per variant. It compared 5,529 character bounds and 5,530 caret bounds. All were identical. The actual Gallery replay used the same fixed window, 141% SVG zoom, four bursts of 24 desktop wheel events per run and three runs per variant. Render submission measurements cover active wheel input.

| Metric | Before | After |
| --- | ---: | ---: |
| UI composition with fresh accessibility tree, median | 2.25 ms | 1.85 ms |
| First wheel burst peak CPU frame, median | 9.24 ms | 7.80 ms |
| CPU frame time during active input, median / p95 / p99 | 1.13 / 2.18 / 4.86 ms | 1.11 / 2.11 / 3.67 ms |
| Acknowledged render submissions during active input, mean / 1% low per second | 798.4 / 211.3 | 747.1 / 126.7 |

The new-tree composition median fell 17.6% and the first wheel CPU peak median fell 15.6%. The candidate runs had longer gaps in delivery of the fastest wheel events. In a separate replay targeting 8 ms between events, the first wheel CPU peak median fell from 9.27 to 7.56 ms and the p95 submission gap was 7.34 versus 7.38 ms. Submission cadence remains inconclusive across runs and does not measure display refresh.

The Release solution build and 386 Windows regression checks passed, including text accessibility, styled source, code layout and scrollbar interactions. At the same 44% scroll position the 1050 × 620 desktop code crop matched exactly. The local run data, probe results and the 8 ms repeat are under `artifacts/codeview-accessibility-order-20261123`.

The WSLg replay used Linux x64 NativeAOT on Dozen and RTX 5070 Ti at 1280 × 900. Five main-content capture pairs matched exactly and both builds passed 108 Gallery presentation checks. The changed build also passed 916 focused Linux text and accessibility checks, 146 independent AT-SPI client checks and four host checks.

Three separate NativeAOT processes per build measured app-side CPU time with detailed Vulkan profiling disabled. The median peak UI time on SVG entry was 140.93 versus 123.91 ms, with overlapping ranges. The median first wheel CPU peak was 5.50 versus 5.71 ms, while the renderer CPU peak was 4.19 versus 4.55 ms. This replay showed no Linux scroll speedup. The sample does not establish why renderer CPU time shifted. The Linux evidence is under `artifacts/codeview-accessibility-order-wslg-20261123`.

### WSLg SVG GPU stages and memory

A separate NativeAOT replay at the same viewport matched all 593 accepted submissions in the measured phases to their Vulkan timestamps. The first wheel burst's median GPU peak was 0.144 ms before and 0.152 ms after, with overlapping ranges. Both builds had a median of 491,280 staging bytes on that burst, with zero buffer copies and zero texture bytes on the three repeated bursts.

During first SVG entry, peak-frame vector output preparation took a median of 316.06 ms before and 314.45 ms after. Two separate diagnostic runs timed the native call directly. `vkCreateComputePipelines` took 286.29 and 296.41 ms for the vector scene with stack capacity four. The Overview had already created the stack-capacity-one scene pipeline. Neither diagnostic recorded pipeline creation during the first wheel burst.

With app and Vulkan profiling disabled, the builds received 178 and 172 wheel cycles over 185 and 180 seconds. Median process RSS from seconds 20–45 to 155–180 went from 432.57 to 432.39 MiB before and from 434.81 to 433.35 MiB after. These are single-process observations per build over the stated interval. A longer-run memory bound and display cadence remain unverified. The raw measurements and temporary instrumentation patch are under `artifacts/codeview-wslg-gpu-20261123`.

### Dozen uses one pipeline for shallow vector scenes

The Linux device policy reads the standard Vulkan driver ID. For Mesa Dozen, scenes requiring stack capacities one, two, or four share the capacity-four compute pipeline. Larger scenes and other drivers use the scene's required capacity. The shader and scene data are unchanged. [Vulkan driver identification](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceDriverProperties.html)

On WSLg NativeAOT with Dozen and RTX 5070 Ti at 1280 × 900, three processes per policy measured the first app log and app-side CPU time with detailed Vulkan profiling disabled. Median time from launch to the first app log was 1,077.66 versus 1,077.16 ms. Median peak app CPU time on first SVG entry was 449.77 versus 145.85 ms, a 67.6% reduction. The first wheel CPU peak was 6.34 versus 5.70 ms.

In a separate GPU profile, the baseline created a capacity-four pipeline in all three SVG entries. The shared policy created it during Overview and performed no new vector-scene pipeline creation on SVG entry. First wheel GPU peak ranges overlapped. Five actual WSLg viewport pairs matched exactly, and the comparison builds passed 108 Gallery presentation checks each. A diagnostic build of the driver-gated product verified ID 23 on Dozen and ID 13 on llvmpipe. Dozen selected capacity four at startup and reused it on SVG entry. llvmpipe created capacities one and four at their original stages. Results are under `artifacts/wslg-svg-stack-pipeline-20261123` in the local workspace.

The driver-gated build passed all 108 Gallery presentation checks on Dozen. The Windows Release solution build completed with zero warnings or errors, and the Windows Gallery passed all 108 presentation checks.

## MessageBar width changes

Each width-only option update invalidated the MessageBar arrangement and the cached unconstrained size of its body. This repeated natural measurement during rapid dragging, even though the body content and its measurement context had not changed. The arrangement and ancestor measurements still invalidate on width changes. The body's natural size remains reusable until a child or other measurement input changes.

The Windows Gallery comparison used the same process image, assets, and diagnostic profiler with only `Duxel.Core.dll` exchanged between versions. Each arm ran in three independent managed .NET 11.0.0 processes (SDK 11.0.100) on an RTX 5070 Ti. After warming the page, each foreground window received two bursts of 68 desktop mouse moves at a median spacing of 2.08–2.13 ms. V-Sync and detailed Vulkan profiling were disabled. App-side CPU time and accepted presentation intervals were measured during active input.

| Metric | Before | After |
| --- | ---: | ---: |
| UI composition p95 | 8.21 ms | 6.00 ms |
| App CPU frame time, median / p95 / p99 | 12.38 / 14.28 / 16.55 ms | 10.21 / 11.41 / 13.28 ms |
| Accepted presentation interval, median / p95 / p99 | 12.58 / 14.46 / 16.73 ms | 10.41 / 12.79 / 18.61 ms |
| Accepted presentation rate, mean / 1% low | 90.68 / 59.78 FPS | 102.41 / 53.74 FPS |

UI composition p95 fell 26.9%, app CPU p95 fell 20.1%, and accepted presentation interval p95 fell 11.6%. Per-process p95 ranges were disjoint for all three metrics. The worst 1% of presentation intervals did not improve, and these intervals measure app-side acknowledgements. The separate comparison with slider markers hidden did not improve p95, so the product retains the original slider. The event-driven drag reached both ends of the tested sweep and published the final CodeView width after button release.

The Windows solution built in Release. All 52 MessageBar checks and 34 Gallery page checks passed. The baseline failed the new width-only measurement reuse check, and both variants correctly invalidated the cache after a body size change. All 64 Windows MessageBar GPU fixtures matched their reference pixels. The Windows Gallery and the Linux x64 NativeAOT Gallery on WSLg Dozen acknowledged 108 presentations each. The timing comparison uses the managed Windows runtime because Windows NativeAOT tooling remains on hold. Run it with `pwsh -File artifacts/messagebar-width-layout-20261123/isolate.ps1 -Root artifacts/messagebar-width-layout-20261123` after preparing the two profiled Core builds. Raw timing, input and binary hashes are in `artifacts/messagebar-width-layout-20261123/isolated`.
