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

The Release solution build and 386 Windows regression checks passed, including text accessibility, styled source, code layout and scrollbar interactions. At the same 44% scroll position the 1050 × 620 desktop code crop matched exactly. Linux runtime performance and long-run memory remain open. The local run data, probe results and the 8 ms repeat are under `artifacts/codeview-accessibility-order-20261123`.
