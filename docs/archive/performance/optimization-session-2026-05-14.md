# Optimization Session — 2026-05-14

## Scope

Vulkan renderer vendor-aware optimization strategy for NVIDIA and AMD, static secondary command buffer execution experiments on the local NVIDIA GPU, global static cache correctness, and frame pipeline profiling for the global dirty benchmark.

Local hardware observed during this session:

- Intel(R) Graphics — driver 32.0.101.8424
- NVIDIA GeForce RTX 5070 Ti Laptop GPU — driver 32.0.15.9649

## Vendor Guidance Reviewed

- NVIDIA Vulkan Dos and Don'ts: command buffer recording should be parallelized when useful, queue submissions should be minimized, tiny command buffers should be avoided, command buffers should be reused only when beneficial, and `SIMULTANEOUS_USE` should only be used when necessary.
- NVIDIA Advanced API Performance: command buffer work should be batched enough to avoid GPU queue bubbles; frequent mixing of draw/dispatch/copy work can introduce drains; Nsight Graphics/Systems should be used for GPU timeline and shader/performance validation.
- AMD GPUOpen RDNA Performance Guide: secondary command buffers/bundles can hurt GPU performance and should have at least about 10 draws/dispatches if used; barriers and resource usage flags should be minimal; RGP/RGA/RMV/GPU Reshape are the intended AMD validation tools.
- AMD GPUOpen Vulkan Barriers Explained: source/destination stages should be producer/consumer-tight to avoid unnecessary pipeline bubbles.

## Changes and Measurements

| Target | Summary | Validation Command | Before | After | Improvement | Risk / Follow-up |
|---|---|---|---:|---:|---:|---|
| Vendor secondary policy | Added GPU vendor classification and a vendor policy for static secondary command buffers. NVIDIA and AMD candidates require at least 10 draw commands before static secondary execution can be attempted. Static secondary recording no longer uses `SIMULTANEOUS_USE`, matching NVIDIA guidance that it should be avoided unless required. | `dotnet build Duxel.slnx -c Release`; `./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -Managed -ManagedTimeoutSeconds 180 -KillProcessTreeOnTimeout` | 5/13 default inline global-static-cache: 1120.695 FPS | Default OFF opt-in-gated path: 1122.892 FPS | +0.196% vs 5/13 documented baseline | Measurement is within run-to-run noise. This is primarily a risk-control/strategy change, not a proven FPS gain. |
| Static secondary execution gate | Implemented cached static secondary + per-frame dynamic secondary execution structure, but placed it behind `DUXEL_VK_EXECUTE_STATIC_SECONDARY=1` and the 10-draw vendor policy gate. Default rendering remains the inline primary strategy unless explicitly opted in. | Same benchmark plus internal candidate run before disabling default execution | Current-session pre-change global-static-cache: 1183.785 FPS | Forced NVIDIA static-secondary execution candidate: 1115.040 FPS | -5.809% vs current-session pre-change run; -0.505% vs 5/13 documented baseline | Candidate regressed on the local NVIDIA static-heavy sample and remains opt-in only. AMD validation requires an AMD RDNA GPU owner. |
| Swapchain usage flag minimization | Tested reducing swapchain image usage to color attachment only, based on minimal resource flag guidance. | Same benchmark, two managed runs | Current-session pre-change global-static-cache: 1183.785 FPS | 1141.710 FPS / 1144.206 FPS | -3.555% / -3.344% vs current-session pre-change run | Rejected and reverted. The local NVIDIA driver did not benefit in this benchmark. |
| Hot-path push constant setup | Removed unused stack allocation for a push constant array in command recording. | `dotnet build Duxel.slnx -c Release` | Unused stack allocation existed in `RecordCommandBuffer()` | Build clean with no warnings | Not separately measurable | Low risk; behavior-preserving cleanup. |
| Global static tile correctness | Fixed global static cache validity to include the full canvas rect, not only size. Renderer static geometry cache keys now use static tag occurrence within the frame rather than tag alone, preventing same-tag multi-list replacement. | `dotnet build Duxel.slnx -c Release`; manual screenshot capture `global-dirty-final-stable-capture.png`; managed benchmark with density 16000 and 2s phases | Final default global-static-cache before correctness pass: 1122.892 FPS | Final stable global-static-cache: 1190.198 FPS | +5.994% vs prior final default cache; +188.529% vs same-run all-dynamic | Retained/borrowed draw list replay produced visible triangle/tile corruption and was rejected. A future zero-copy static path needs explicit retained geometry handles, not borrowed frame draw lists. |
| App/Vulkan frame profiling | Added opt-in file-based profiling for app-level frame phases and Vulkan upload/record/submit/present phases. | `DUXEL_APP_PROFILE=1`, `DUXEL_APP_PROFILE_OUT=...`, `DUXEL_VK_PROFILE=1`, `DUXEL_VK_PROFILE_OUT=...` | Renderer/Core bottleneck attribution required external observation | Final logs show Core render around 240–298 µs, Vulkan upload around 52–70 µs, record around 20–28 µs, while app-level renderer phase includes synchronization variance around 565–1019 µs | Not an FPS feature by itself | Profiling is opt-in and disabled by default. Use it before structural optimization decisions. |
| Pooled draw-list transform helpers | Removed transient `List<T>` construction from draw-list deindexing and clip-rect scaling helpers. Transform results now write directly into `ArrayPool<T>` buffers wrapped by `UiPooledList<T>`. | `dotnet build Duxel.slnx -c Release`; managed `global_dirty_strategy_bench.cs` with `DUXEL_GLOBAL_DIRTY_BENCH_OUT=artifacts\global-dirty-after-pooled-transform-2026-05-14.json`, density 16000, 2s phases | `DeIndexAllBuffers()` allocated 3 temporary `List<T>` instances plus 3 list backing arrays and then copied into 3 pooled arrays. `ScaleClipRects()` allocated 1 command `List<T>` per transformed draw list and `UiDrawData.ScaleClipRects()` allocated 1 draw-list `List<T>`. | `DeIndexAllBuffers()` allocates 0 temporary `List<T>` instances and fills 3 pooled arrays directly. Clip-rect scaling allocates 0 temporary `List<T>` instances and fills pooled command/draw-list arrays directly. | Per full deindex transform: -3 `List<T>` objects, -3 list backing arrays, and -3 extra copy passes. Per draw-data clip scaling: -1 draw-list `List<T>` and -1 command `List<T>` per draw list. | This is a helper-path allocation optimization. The global dirty benchmark is runtime validation context, not an isolated FPS proof for this helper. |
| Global static cache lifetime fix | Replaced borrowed static draw-list frame references with frame-owned pooled clones and included canvas/density identity in `global_dirty_strategy_bench` static geometry keys. | `dotnet build Duxel.slnx -c Release`; managed `global_dirty_strategy_bench.cs` with `DUXEL_GLOBAL_DIRTY_BENCH_OUT=artifacts\global-dirty-after-static-cache-lifetime-fix-2026-05-14.json`, density 16000, 2s phases | Borrowed static replay candidate reached 1688.870 FPS but was invalid because frame draw data could reference cached pooled buffers released or regenerated before renderer consumption, causing broken background geometry. | Lifetime-safe static-cache path measured 1237.753 FPS with successful wrapper exit code 0. | -26.715% vs invalid borrowed candidate; +187.898% vs same-run all-dynamic 429.930 FPS. | Correctness takes priority over the rejected borrowed path. A future zero-copy static path needs explicit renderer-owned retained geometry handles and lifetime, not borrowed `UiDrawList` buffers. |

## Raw Static-Heavy Managed Measurements

Environment: `global_dirty_strategy_bench.cs`, managed FBA, 2 seconds per phase, 8x6 tiles, density 16000, Windows local NVIDIA RTX 5070 Ti Laptop GPU.

| Artifact | all-dynamic FPS | global-static-cache FPS | Notes |
|---|---:|---:|---|
| `global-dirty-before-2026-05-14.json` | 414.891 | 1183.785 | Current-session pre-change reference. |
| `global-dirty-after-vendor-policy-2026-05-14.json` | 395.472 | 1141.710 | Included swapchain usage minimization candidate. |
| `global-dirty-after-vendor-policy-rerun-2026-05-14.json` | 406.332 | 1144.206 | Rerun of swapchain usage minimization candidate. |
| `global-dirty-after-static-secondary-exec-2026-05-14.json` | 401.955 | 1115.040 | Forced NVIDIA static-secondary execution candidate; rejected. |
| `global-dirty-after-conservative-secondary-policy-rerun-2026-05-14.json` | 410.029 | 1117.754 | Conservative default after rejecting forced execution. |
| `global-dirty-after-secondary-optin-gate-default-2026-05-14.json` | 374.817 | 1122.892 | Final default path with execution opt-in gate off. |
| `global-dirty-after-static-key-borrowed-2026-05-14.json` | 387.766 | 1559.916 | Rejected borrowed static replay candidate; screenshot validation later showed visible corruption. |
| `global-dirty-borrowed-repeat-1-2026-05-14.json` | 415.710 | 1616.950 | Rejected borrowed static replay candidate; FPS gain invalid because rendering was incorrect. |
| `global-dirty-borrowed-repeat-2-2026-05-14.json` | 418.550 | 1670.740 | Rejected borrowed static replay candidate; FPS gain invalid because rendering was incorrect. |
| `global-dirty-borrowed-repeat-3-2026-05-14.json` | 412.510 | 1666.370 | Rejected borrowed static replay candidate; FPS gain invalid because rendering was incorrect. |
| `global-dirty-no-geometry-hash-profile-2026-05-14.json` | 406.047 | 1178.802 | Removed per-frame full static geometry hashing after profiling showed it dominated upload time. |
| `global-dirty-final-stable-profile-2026-05-14.json` | 412.506 | 1190.198 | Final stable path with tile correctness and profiling instrumentation. |
| `global-dirty-after-pooled-transform-2026-05-14.json` | 379.395 | 1688.870 | Invalid borrowed-buffer candidate kept for transparency; background corruption was reported after this path and the wrapper returned exit code 1 after cleanup. |
| `global-dirty-after-static-cache-lifetime-fix-2026-05-14.json` | 429.930 | 1237.753 | Lifetime-safe static-cache path after replacing borrowed frame references with frame-owned pooled clones and canvas-identity static keys. |

## Interpretation

The local NVIDIA result did not validate static secondary command buffer execution for the current global dirty sample. The sample's static background is already represented efficiently through static geometry caching, so converting that frame to secondary execution adds command buffer complexity without an FPS gain.

The useful result from this session is a safer vendor-aware strategy:

1. Do not enable static secondary execution by default.
2. Allow explicit experiments via `DUXEL_VK_EXECUTE_STATIC_SECONDARY=1`.
3. Require at least 10 static draw commands before secondary execution is considered for NVIDIA/AMD.
4. Do not set `SIMULTANEOUS_USE` on cached static secondary command buffers unless a future measured scenario requires simultaneous pending reuse.
5. Continue using the inline primary renderer as the measured default path for this workload.

The later borrowed/retained static replay experiments confirmed the visible CPU bottleneck, but they were rejected after screenshot/user validation showed triangle/tile corruption. The stable path copies cached static geometry into frame-owned pooled draw lists, so the next structural optimization should be an explicit retained static geometry handle/replay model with renderer-owned identity and lifetime, not direct borrowed frame draw lists.

Final profiling shows the next bottlenecks to address:

1. Core render/build still emits the static background into frame draw data every frame (`144701` vertices / `385434` indices in the final static phase).
2. Vulkan upload/record is comparatively small after removing full geometry hashing.
3. App-level renderer timing includes synchronization/acquire/present variance, so GPU utilization work should be validated with app and Vulkan profile logs together.

## AMD Validation Plan

AMD hardware is not available in this workspace. AMD-specific strategy is therefore based on GPUOpen guidance and must be validated by an AMD GPU owner.

Recommended AMD validation:

1. Use RDNA hardware and current Radeon driver.
2. Run the default path first with `DUXEL_VK_EXECUTE_STATIC_SECONDARY` unset.
3. Run opt-in static secondary experiments with `DUXEL_VK_EXECUTE_STATIC_SECONDARY=1` on workloads with at least 10 static draw commands.
4. Profile with Radeon GPU Profiler and Radeon GPU Analyzer.
5. Treat any negative or noisy result as a rejection unless repeated measurements and GPU timelines show a clear win.

## Verification

- `dotnet build Duxel.slnx -c Release` — succeeded, 0 warnings, 0 errors.
- Managed FBA `global_dirty_strategy_bench.cs` final stable run — succeeded, `all-dynamic` 412.506 FPS, `global-static-cache` 1190.198 FPS.
- Managed FBA `global_dirty_strategy_bench.cs` after pooled transform helper optimization — produced `artifacts\global-dirty-after-pooled-transform-2026-05-14.json` with `all-dynamic` 379.395 FPS and `global-static-cache` 1688.870 FPS, but this borrowed-buffer candidate was rejected after background corruption was reported.
- Managed FBA `global_dirty_strategy_bench.cs` after static-cache lifetime fix — succeeded with exit code 0 and produced `artifacts\global-dirty-after-static-cache-lifetime-fix-2026-05-14.json` with `all-dynamic` 429.930 FPS and `global-static-cache` 1237.753 FPS.
- Manual screenshot `global-dirty-final-stable-capture.png` — static cache tile tearing/large triangle corruption not visible in the final stable path.
- Rejected candidates were measured and not hidden: swapchain usage minimization, forced NVIDIA static secondary execution, full per-frame static geometry hashing, and borrowed/retained static draw list replay were rejected for either performance regression or visual corruption.
