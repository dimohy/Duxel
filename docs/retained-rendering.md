# Damage-based retained rendering

For the2026-11-06 application follow-up,see the primitive-run candidate in the [optimization evaluation](optimization-evaluation.md). Correctness passes,but overlapping timing/memory ranges leave improvement inconclusive. The11/04 values below are historical checkpoint evidence,not additive results for the current candidate.

See the latest [common-path assessment and improvements](optimization-evaluation.md). Damage planning now precedes streamed geometry upload/capacity growth; empty damage skips uploads and draw planning. Static-cache lifetime, input, layout, texture invalidation and frame submission remain. Primitive commands access the current item by ref. The new FPS/memory results remain separate from the historical comparisons below.

> Last synced: 2026-11-06

For the latest memory conclusion, use the **extended12-second comparison** below. Private decreases2.17% idle/3.02% moving with nonoverlapping ranges. The earlier3s result is preserved separately.

The common Vulkan renderer preserves previous images for screen-space static/dynamic lists and built-in menus, popups and tooltips. It compares command history for each acquired swapchain image, clears the union of old/new changed bounds, and redraws every intersecting command in its original order. Moving or removing translucent content restores the background first; vectors remain vectors. Unchanged images issue no draws. Input, layout, UI construction callbacks and presentation still execute.

Dynamic geometry compares actual per-command vertex, index and primitive bytes. Resident geometry uses the existing `StaticGeometryKey` and content revision contract. History never retains borrowed source buffers. Snapshot storage is recycled after its last image reader releases it. Dynamic byte equality is not inferred from hashes alone.

MSAA stores the existing multisample attachment without allocating another screen copy. It compares both the shared MSAA surface history and the acquired resolved image, repairs their combined damage, then resolves that region. Both `renderArea` and scissor are restricted. Initial images, framebuffer/display changes, clear color changes and texture updates invalidate the full image. [Vulkan attachment operations apply within the render area](https://docs.vulkan.org/refpages/latest/refpages/source/VkRenderingAttachmentInfo.html).

General projected/depth-tested 3D scenes, user draw callbacks and GPU simulation commands have whole-scene dependencies. Those frames redraw fully and invalidate older image histories. Existing shared geometry/GPU residency still applies to 3D camera scenes. Arbitrary 3D partial repaint is outside this contract. Widely distributed changes can produce a large union and smaller savings.

Long-lived presentation snapshots own exact-length arrays instead of retaining pool bucket slack for every surface. The common retained geometry batcher also reserves known stream sizes and uses owned long-lived arrays. Ordinary frame builders and continuously changing surfaces retain their existing pooling/capacity policy.

Upload staging is released after the shared 180-frame cache grace period, only once its upload fence has completed and no batch is being recorded. A later upload recreates storage at the required size. Startup upload high-water buffers are not kept indefinitely with idle resident geometry. This requires neither forced GC nor early destruction of buffers still in GPU use.

Enabled by default. Set `DUXEL_VK_DAMAGE=0` before starting a diagnostic process to compare full repaint. This does not reduce sample shape counts, resolution, MSAA or vector quality.

Reuse the existing measurement script. The follow-up section of `SESSION_HANDOFF.md` and `artifacts/retained-damage-2026-11-04/` contain comparison plans, executable hashes, raw frames/memory and stdout/stderr. .NET10 source validation and pinned .NET11 RC3 NativeAOT Speed measurements are separate results.

The performance measurements below were taken on Windows with an RTX 5070 Ti Laptop GPU. The current Linux X11 backend also runs the Gallery. Its Linux x64 NativeAOT build passed 100 Vulkan presentation checks under Ubuntu/WSLg. Those checks cover presentation. Performance on physical ARM64 or Raspberry Pi hardware remains unverified. [Linux Gallery run instructions and evidence](../samples/Duxel.Gallery.Linux/README.md).

## Final equal-load comparison — 2026-11-04

Baseline40C51851 → final0F939A9C. .NET11 RC3 NativeAOT Speed, 1600×1000, MSAA1, VSync off, 128 popups × 256 swatches, warmup1s/at least96frames, measurement3s, three independent alternating runs. Sample, shaders and load are unchanged. Percentages describe observed median changes; overlapping run ranges are inconclusive.

### idle

| Metric | Before | After | Observed change | Before range | After range | Result |
| --- | ---: | ---: | ---: | --- | --- | --- |
| FPS | 389.215 | 386.481 | -0.70% | 388.727–390.427 | 358.309–390.840 | inconclusive-overlapping-ranges |
| Median ms | 2.534 | 2.558 | 0.96% | 2.528–2.545 | 2.528–2.767 | inconclusive-overlapping-ranges |
| p95 ms | 2.837 | 2.846 | 0.29% | 2.795–2.920 | 2.821–3.086 | inconclusive-overlapping-ranges |
| p99 ms | 3.327 | 3.282 | -1.35% | 3.251–3.336 | 3.017–3.365 | inconclusive-overlapping-ranges |
| 1% low FPS | 250.288 | 277.032 | 10.69% | 236.403–280.171 | 276.718–295.594 | inconclusive-overlapping-ranges |
| Draw calls | 1031.000 | 0.000 | -100.00% | 1031.000–1031.000 | 0.000–0.000 | lower-nonoverlapping |
| GPU us | 175.860 | 2.556 | -98.55% | 172.610–176.356 | 2.358–3.627 | lower-nonoverlapping |
| Private MiB | 196.281 | 197.297 | 0.52% | 195.242–196.566 | 196.164–197.496 | inconclusive-overlapping-ranges |
| Working set MiB | 122.664 | 122.279 | -0.31% | 122.613–122.887 | 122.213–122.336 | lower-nonoverlapping |

### motion

| Metric | Before | After | Observed change | Before range | After range | Result |
| --- | ---: | ---: | ---: | --- | --- | --- |
| FPS | 388.735 | 387.766 | -0.25% | 381.126–389.053 | 381.698–391.294 | inconclusive-overlapping-ranges |
| Median ms | 2.531 | 2.543 | 0.46% | 2.525–2.587 | 2.518–2.593 | inconclusive-overlapping-ranges |
| p95 ms | 2.868 | 2.858 | -0.33% | 2.856–2.929 | 2.814–2.918 | inconclusive-overlapping-ranges |
| p99 ms | 3.532 | 3.205 | -9.25% | 3.371–3.539 | 3.182–3.602 | inconclusive-overlapping-ranges |
| 1% low FPS | 232.146 | 264.156 | 13.79% | 228.678–246.496 | 233.807–299.152 | inconclusive-overlapping-ranges |
| Draw calls | 1031.000 | 140.500 | -86.37% | 1031.000–1031.000 | 140.481–140.550 | lower-nonoverlapping |
| GPU us | 176.535 | 27.222 | -84.58% | 174.125–177.944 | 25.402–29.087 | lower-nonoverlapping |
| Private MiB | 197.320 | 197.973 | 0.33% | 196.336–197.953 | 197.598–197.984 | inconclusive-overlapping-ranges |
| Working set MiB | 124.277 | 123.129 | -0.92% | 123.469–124.418 | 123.047–123.266 | lower-nonoverlapping |

Idle and moving draw/GPU time ranges do not overlap the baseline. Working set also decreases in both scenarios. Private, overall FPS and frame-tail ranges overlap, so their improvement is unproven. Remaining overall frame cost lies outside GPU execution, including UI construction/CPU work. This does not claim the Private increase is fully resolved.

.NET10/RC3 each pass708checks; Release(net8/9/10) has0warnings/errors;155prior images are byte-identical. Coverage includes MSAA1/4, movement/removal/alpha, changed geometry bytes under unchanged commands, snapshot reader counts/bounded repeated-change storage, and staging reclamation/recreation. Raw results and all intermediate regressions remain in artifacts/retained-damage-2026-11-04.


## Extended 12-second memory comparison — latest conclusion

All terminal250ms samples in the3s comparison showed lower Private for the candidate. To observe memory beyond startup commitment changes, measurement duration was extended equally to12s for both binaries. Source, executables, load, warmup and MSAA remain unchanged. The additional budget is12runs; no forced GC. Do not combine these observations with the3s comparison. Raw evidence is comparison-memory-steady/summary.json and its frame/memory CSV files.

### 12s idle

| Metric | Before | After | Observed change | Before range | After range | Result |
| --- | ---: | ---: | ---: | --- | --- | --- |
| FPS | 384.135 | 385.851 | 0.45% | 380.708–389.356 | 378.588–392.399 | inconclusive-overlapping-ranges |
| Median ms | 2.540 | 2.528 | -0.47% | 2.526–2.574 | 2.514–2.585 | inconclusive-overlapping-ranges |
| p95 ms | 2.975 | 2.946 | -0.97% | 2.917–3.012 | 2.850–3.004 | inconclusive-overlapping-ranges |
| p99 ms | 3.845 | 3.864 | 0.50% | 3.280–4.152 | 3.078–4.121 | inconclusive-overlapping-ranges |
| 1% low FPS | 227.590 | 227.032 | -0.25% | 206.483–279.284 | 209.713–297.301 | inconclusive-overlapping-ranges |
| Draw calls | 1031.000 | 0.000 | -100.00% | 1031.000–1031.000 | 0.000–0.000 | lower-nonoverlapping |
| GPU us | 180.199 | 4.086 | -97.73% | 179.544–182.450 | 3.069–4.644 | lower-nonoverlapping |
| Private MiB | 175.926 | 172.113 | -2.17% | 175.594–176.148 | 171.256–172.504 | lower-nonoverlapping |
| Working set MiB | 124.551 | 122.754 | -1.44% | 124.367–128.727 | 122.426–122.777 | lower-nonoverlapping |

### 12s motion

| Metric | Before | After | Observed change | Before range | After range | Result |
| --- | ---: | ---: | ---: | --- | --- | --- |
| FPS | 385.919 | 379.473 | -1.67% | 382.284–386.011 | 360.177–384.760 | inconclusive-overlapping-ranges |
| Median ms | 2.529 | 2.574 | 1.76% | 2.521–2.555 | 2.538–2.715 | inconclusive-overlapping-ranges |
| p95 ms | 2.981 | 3.093 | 3.78% | 2.924–3.110 | 2.974–3.120 | inconclusive-overlapping-ranges |
| p99 ms | 4.204 | 4.050 | -3.67% | 4.176–4.423 | 4.034–4.446 | inconclusive-overlapping-ranges |
| 1% low FPS | 207.008 | 206.278 | -0.35% | 199.475–207.480 | 197.293–207.337 | inconclusive-overlapping-ranges |
| Draw calls | 1031.000 | 137.262 | -86.69% | 1031.000–1031.000 | 137.162–137.284 | lower-nonoverlapping |
| GPU us | 178.377 | 26.880 | -84.93% | 177.646–184.196 | 26.104–33.829 | lower-nonoverlapping |
| Private MiB | 179.609 | 174.180 | -3.02% | 179.094–179.957 | 173.393–175.172 | lower-nonoverlapping |
| Working set MiB | 126.656 | 124.402 | -1.78% | 126.418–126.777 | 124.301–124.426 | lower-nonoverlapping |

Both scenarios have nonoverlapping lower Private, working set, draw-call and GPU-time ranges in the12s comparison. Private decreases2.17% idle and3.02% moving. Overall FPS and frame tails remain inconclusive. The moving FPS median change of-1.67% is retained, not classified as a proven regression. The3s inconclusive result remains preserved. These measurements do not retroactively establish comparisons with historical binaries measured over different durations.


