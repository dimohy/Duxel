# Graphics-queue depth attachment sharing

2026-11-06. The common Vulkan renderer keeps one depth attachment instead of one per swapchain image. Each rendering clears depth; depth is neither presented nor preserved across frames. MSAA, depth comparisons, alpha/coverage, scope clears and geometry remain unchanged.

An image barrier orders EARLY_FRAGMENT_TESTS and LATE_FRAGMENT_TESTS reads/writes before the next clear and depth access. UNDEFINED discards prior contents and CLEAR initializes them. Resize retirement still follows the last submitted graphics fence. This contract assumes the same graphics queue. See the [Khronos synchronization examples](https://docs.vulkan.org/guide/latest/synchronization_examples.html). No CPU wait or QueueWaitIdle was added.

## Measurement

Evidence: `artifacts/gpu-current-2026-11-06/`. Fixed SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, win-x64 NativeAOT Speed. GPU particles5,000, size6, two collision iterations, VSync off, MSAA4, render pipeline/static cache/dynamic canvas enabled. Requested1920×1080; actual framebuffer2400×1350 at current DPI. No load or quality reduction.

The existing runner executes three independent processes per variant, A/B, B/A, A/B, one-second warmup and six-second measurement. Builds and functional checks precede timing. No forced GC. Private/working-set values aggregate each run's process-lifetime samples at seconds3..6, then the three run medians; these samples are not precisely aligned to UI warmup.

| Metric | Before | After | Interpretation |
| --- | ---: | ---: | --- |
| Depth allocation | 162,201,600B / 3 images | 54,067,200B / 1 image | 66.67% reduction |
| Private | 434,470,912B | 326,250,496B | 24.91% reduction; disjoint run ranges |
| Working set | 131,657,728B | 131,538,944B | Overlapping ranges |
| Average FPS | 2,084.26 | 2,081.14 | Overlapping ranges; no speedup claim |
| median / p95 / p99 frame(ms) | 0.46740 / 0.57898 / 0.78469 | 0.46690 / 0.58907 / 0.81540 | Overlapping ranges; increased tail medians retained |
| 1% low FPS | 1,130.41 | 1,094.84 | Overlapping ranges |
| GPU time(μs) | 362.00 | 364.43 | Overlapping ranges |
| Draws / dynamic upload | 55 / 5,992B | 55 / 5,992B | Unchanged |

GPU timestamps include particle compute through rendering, not CPU or presentation completion. A separate57-sample CPU-stage diagnostic had median imageFence366.8μs and recording31.9μs. This scene had55 draws rather than thousands; GPU/image-reuse waits were substantial. Depth allocation and process Private are distinct accounting domains; the Private reduction is not a measurement of total GPU VRAM.

A30-second pre-change observation showed similar Private values after startup. This does not prove long-term leak resolution, peak bounds, whole-widget performance or Pi hardware results. Sharing may reduce depth-stage overlap between frames; other GPUs/scenes remain unverified.

## Runner and validation

`scripts/measure-polygon-performance.ps1` records Width/Height requests and one-second Private/working-set/CPU/handle/thread CSV samples. It validates cases and existing output paths before launch and does not overwrite results. An absent MainWindowHandle on a hidden launch is informational. Exit status, raw-frame statistics and GPU telemetry validate execution; manual visual/input acceptance remains separate. The initial incorrect handle rejection and original data are retained.

.NET10 full rendering1,862 checks and1,067 graphics checks including new queued-depth tests passed. At MSAA1/4, the new test submits12 changing-depth frames without intervening readback, then checks exact whole pixels, clears and one attachment. Final Native and pixel comparisons are recorded in `verification.json`. Historical320×240 images cannot be compared directly with current400×300 images. Reuse the exact old binary at current DPI and run new tests after existing snapshots to isolate UI history. No resized images or relaxed tolerances hide differences.
