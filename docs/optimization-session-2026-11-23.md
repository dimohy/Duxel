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
