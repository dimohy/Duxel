# Optimization Session — 2026-05-13

## Scope

2D Vulkan renderer hot-path optimization and FBA FPS measurement infrastructure.

## Changes

| Target | Summary | Validation Command | Before | After | Improvement | Risk / Follow-up |
|---|---|---|---:|---:|---:|---|
| `VulkanRendererBackend` texture/font command path | Cached resolved font texture id as raw `nuint` and replaced hot-path `UiTextureId.Equals`/option recomputation with direct value comparison. | `dotnet build Duxel.slnx -c Release` | Not separately measured | Build clean; hot path has fewer struct equality calls | N/A — no pre-change microbenchmark captured | Low risk; behavior-preserving comparison change. |
| `perf_2d_render_fps.cs` measurement sample | Added fixed render benchmark settings: VSync off, Render profile, MSAA 1x, idle frame skip disabled. | `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Managed` | Sample displayed FPS only in GUI; no terminal/file data | Managed FPS logs emitted to artifacts | N/A — instrumentation change | Keep sample for repeatable before/after captures. |
| `perf_2d_render_fps.cs` measurement logging | Added `DUXEL_PERF_TEST_LEVEL`, `DUXEL_PERF_CONSOLE`, `DUXEL_PERF_LOG_PATH`, and opt-in `DUXEL_PERF_DEBUG=1` diagnostics. | Managed runs with `DUXEL_SAMPLE_AUTO_EXIT_SECONDS=5`; NativeAOT runs with `./run-fba.ps1 ... -Wait` | No file-based telemetry | Managed and NativeAOT FPS logs emitted to artifacts | N/A — instrumentation change | Use `-Wait` for automated NativeAOT benchmark collection. |
| `run-fba.ps1` NativeAOT wait mode | Added `-Wait` to block until the published NativeAOT executable exits and surface a non-zero exit code. | `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait -NoBuild` | NativeAOT launch returned before GUI sample exit | NativeAOT benchmark log collected in one command | N/A — workflow improvement | `-NoLaunch` still disables execution, so `-Wait` has no effect with `-NoLaunch`. |
| Secondary command buffer groundwork | Added Vulkan bindings for secondary command buffers: `CommandBufferLevel.Secondary`, render-pass continuation flags, inheritance info, secondary subpass contents, and `vkCmdExecuteCommands`. | `dotnet build Duxel.slnx -c Release` | Secondary command buffers could not be represented by local bindings | Binding layer compiles cleanly | N/A — groundwork only | Next step is renderer command recording split; no runtime behavior changed yet. |
| Command recording target split | Parameterized the draw-list recording local function by target `CommandBuffer` instead of capturing the primary command buffer directly. | `dotnet build Duxel.slnx -c Release` | Draw recording was primary-buffer-captured | Draw recording can target primary now and secondary later | N/A — behavior-preserving groundwork | Runtime path remains inline until static secondary cache is implemented. |
| Static secondary invalidation groundwork | Added texture generation tracking for create/update/destroy and deterministic static draw command signature hashing. Static command callbacks are excluded from static cache eligibility. | `dotnet build Duxel.slnx -c Release`; `./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -Wait -NoBuild` | No texture generation or command signature key existed for future secondary cache invalidation | Build clean; density 16000 static-heavy benchmark stayed at 1133.396 FPS for global static cache | +189.750% vs current all-dynamic phase | Use this as cache-key groundwork only; renderer path remains inline. |
| Static secondary record-only validation | Added opt-in static secondary command cache records and empty secondary command buffer recording with render-pass inheritance. The active renderer still executes the existing inline primary path. | `DUXEL_VK_VALIDATE_STATIC_SECONDARY=1 ./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -Wait` | No secondary command buffer record validation existed beyond bindings | NativeAOT record-only validation succeeded; density 16000 global static cache measured 1181.639 FPS | +201.875% vs current all-dynamic phase | Next step is moving real static draw recording into cached secondary buffers; current validation records skeleton buffers only. |
| Static secondary real draw recording validation | Extended the opt-in validation path to record actual static draw commands into cached secondary command buffers while keeping execution on the inline primary path. | `DUXEL_VK_VALIDATE_STATIC_SECONDARY=1 ./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -Wait`; default inline rerun without the flag | Skeleton secondary recording only; static draw commands were not recorded into secondary buffers | NativeAOT validation succeeded; default inline global static cache measured 1120.695 FPS after the change | +196.560% vs current default all-dynamic phase | Next step is all-secondary execution path with cached static secondaries plus per-frame dynamic secondary. |
| Global dirty visual distribution | Replaced the benchmark sample's modular dot placement with deterministic hash-based scatter to remove diagonal aliasing bands that looked like broken background rendering. | `dotnet build Duxel.slnx -c Release`; manual capture of `global_dirty_strategy_bench.cs` with and without `DUXEL_VK_VALIDATE_STATIC_SECONDARY=1` | Dot placement used correlated modulo sequences and produced repeated diagonal bands | Dots distribute across tiles without diagonal streak artifacts | N/A — visual benchmark-data fix | This was a sample data-generation issue, not a renderer secondary-command-buffer corruption. |

## Managed FPS Measurements

Environment: Windows workspace, local GPU/driver stack, `run-fba.ps1 -Managed`, `VSync=false`, `DuxelPerformanceProfile.Render`, `MsaaSamples=1`, `EnableIdleFrameSkip=false`, 1200x800 window, 5 seconds per level.

| Level | Final Frame | Final Average FPS | Final Average Frame Time |
|---:|---:|---:|---:|
| 1 (Light) | 3215 | 642.9 FPS | 1.555 ms |
| 2 (Normal) | 3170 | 634.0 FPS | 1.577 ms |
| 3 (Heavy) | 3090 | 617.9 FPS | 1.618 ms |

Raw logs:

```text
DUXEL_PERF level=1 second=5 frame=3215 currentFps=785.7 avgFps=642.9 avgFrameMs=1.555
DUXEL_PERF level=2 second=5 frame=3170 currentFps=913.8 avgFps=634.0 avgFrameMs=1.577
DUXEL_PERF level=3 second=5 frame=3090 currentFps=985.2 avgFps=617.9 avgFrameMs=1.618
```

## Final Managed / NativeAOT Normal-Level Measurements

Environment: same benchmark settings as above, level 2 (Normal), 5 seconds.

| Mode | Final Frame | Final Average FPS | Final Average Frame Time | Notes |
|---|---:|---:|---:|---|
| Managed | 3025 | 605.0 FPS | 1.653 ms | `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Managed` |
| NativeAOT | 3847 | 769.4 FPS | 1.300 ms | `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait -NoBuild` |

NativeAOT measurement collection note: `run-fba.ps1` now supports `-Wait` for benchmark collection. The earlier `DUXEL_PERF_BEGIN`-only result was caused by reading the log before the detached GUI process finished, not by a renderer or NativeAOT frame-loop failure.

Raw final logs:

```text
DUXEL_PERF level=2 second=5 frame=3025 currentFps=873.7 avgFps=605.0 avgFrameMs=1.653
DUXEL_PERF level=2 second=5 frame=3847 currentFps=756.6 avgFps=769.4 avgFrameMs=1.300
```

## Static-Heavy NativeAOT Benchmark

Environment: `global_dirty_strategy_bench.cs`, NativeAOT `./run-fba.ps1 ... -Wait`, 2 seconds per phase, 8x6 tiles, density 16000.

| Strategy | Average FPS | Samples | Improvement vs all-dynamic |
|---|---:|---:|---:|
| all-dynamic | 383.602 FPS | 751 | baseline |
| global-static-cache | 1110.141 FPS | 2133 | +189.399% |
| global-static-cache after inline restore | 1121.728 FPS | 2145 | +193.800% vs restored all-dynamic |
| all-dynamic after texture generation/signature key | 391.172 FPS | 761 | baseline |
| global-static-cache after texture generation/signature key | 1133.396 FPS | 2177 | +189.750% |
| all-dynamic with static secondary record-only validation | 391.428 FPS | 768 | baseline |
| global-static-cache with static secondary record-only validation | 1181.639 FPS | 2274 | +201.875% |
| all-dynamic with static secondary real recording validation | 350.790 FPS | 673 | baseline |
| global-static-cache with static secondary real recording validation | 1123.044 FPS | 2153 | +220.142% |
| all-dynamic default inline after real recording path | 377.900 FPS | 738 | baseline |
| global-static-cache default inline after real recording path | 1120.695 FPS | 2151 | +196.560% |

Raw result:

```json
{"phaseSeconds":2,"results":[{"phase":0,"strategy":"all-dynamic","tiles":"8x6","density":16000,"avgFps":383.602,"avgCpu":0,"samples":751},{"phase":1,"strategy":"global-static-cache","tiles":"8x6","density":16000,"avgFps":1110.141,"avgCpu":0,"samples":2133}]}
```

Post texture-generation/signature-key result:

```json
{"phaseSeconds":2,"results":[{"phase":0,"strategy":"all-dynamic","tiles":"8x6","density":16000,"avgFps":391.172,"avgCpu":0,"samples":761},{"phase":1,"strategy":"global-static-cache","tiles":"8x6","density":16000,"avgFps":1133.396,"avgCpu":0,"samples":2177}]}
```

Opt-in static secondary record-only validation result:

```json
{"phaseSeconds":2,"results":[{"phase":0,"strategy":"all-dynamic","tiles":"8x6","density":16000,"avgFps":391.428,"avgCpu":0,"samples":768},{"phase":1,"strategy":"global-static-cache","tiles":"8x6","density":16000,"avgFps":1181.639,"avgCpu":0,"samples":2274}]}
```

Opt-in static secondary real draw recording validation result:

```json
{"phaseSeconds":2,"results":[{"phase":0,"strategy":"all-dynamic","tiles":"8x6","density":16000,"avgFps":350.79,"avgCpu":0,"samples":673},{"phase":1,"strategy":"global-static-cache","tiles":"8x6","density":16000,"avgFps":1123.044,"avgCpu":0,"samples":2153}]}
```

Default inline rerun after real recording path:

```json
{"phaseSeconds":2,"results":[{"phase":0,"strategy":"all-dynamic","tiles":"8x6","density":16000,"avgFps":377.9,"avgCpu":0,"samples":738},{"phase":1,"strategy":"global-static-cache","tiles":"8x6","density":16000,"avgFps":1120.695,"avgCpu":0,"samples":2151}]}
```

Interpretation: static-heavy rendering already benefits strongly from existing static geometry caching. Secondary command buffer reuse should therefore be evaluated on static-heavy scenes first; mostly dynamic scenes may show little gain or regress if secondary recording overhead exceeds cache reuse savings.

Dynamic-only secondary experiment: routing all draw commands through one per-frame secondary command buffer compiled and ran, but regressed the static-heavy benchmark (`all-dynamic` 362.787 FPS, `global-static-cache` 1031.777 FPS). That path was removed. The retained changes are only binding support and target-command-buffer parameterization.

## Verification

- `dotnet build Duxel.slnx -c Release` — succeeded, 0 warnings, 0 errors.
- Managed FBA measurement — succeeded for Light/Normal/Heavy.
- NativeAOT FBA publish/run — succeeded, 0 build errors; FPS frame logs collected after waiting for the published executable to exit.
- Static-heavy NativeAOT benchmark after texture-generation/signature-key groundwork — succeeded, density 16000, `all-dynamic` 391.172 FPS, `global-static-cache` 1133.396 FPS.
- Static secondary record-only validation — succeeded with `DUXEL_VK_VALIDATE_STATIC_SECONDARY=1`, density 16000, `all-dynamic` 391.428 FPS, `global-static-cache` 1181.639 FPS.
- Static secondary real draw recording validation — succeeded with `DUXEL_VK_VALIDATE_STATIC_SECONDARY=1`, density 16000, `all-dynamic` 350.790 FPS, `global-static-cache` 1123.044 FPS.
- Default inline rerun after real recording path — succeeded without validation flag, density 16000, `all-dynamic` 377.900 FPS, `global-static-cache` 1120.695 FPS.
- Global dirty visual distribution — manual captures before/after showed the diagonal dot bands were caused by deterministic sample placement aliasing; hash-based scatter removed the artifacts in both default inline and static-secondary-validation modes.

## Secondary Command Buffer / Static Reuse Plan

### Current blocker removed

The local Vulkan binding layer now exposes the minimum Vulkan 1.0 pieces required to record and execute secondary command buffers:

- secondary command buffer allocation
- render-pass continuation usage flag
- simultaneous-use usage flag
- command buffer inheritance info
- `VK_SUBPASS_CONTENTS_SECONDARY_COMMAND_BUFFERS`
- `vkCmdExecuteCommands`

### Required renderer restructure

Vulkan render pass contents mode is selected at `vkCmdBeginRenderPass`. To use secondary command buffers inside the pass, the primary command buffer must begin the render pass with secondary contents and then execute secondary buffers. That means static and dynamic draws cannot remain inline in the current primary command buffer recording path.

Recommended implementation sequence:

1. Extract draw-list command recording into a reusable method that accepts any target `CommandBuffer`.
2. Record a per-frame dynamic secondary command buffer for non-static draw lists.
3. Cache static secondary command buffers per static tag and swapchain framebuffer generation.
4. Track invalidation keys: static geometry tag, framebuffer index, render pass, framebuffer, display transform, texture generation, and scissor/command signature.
5. Change the primary command buffer to begin render pass with `SecondaryCommandBuffers`, execute static cached buffers plus current dynamic secondary buffer, then end the render pass.

### Risk controls

- Do not add fallback rendering paths.
- Keep the current inline path until secondary path is complete, then switch explicitly.
- Invalidate static command buffers on swapchain recreation, pipeline recreation, static geometry recreation, and texture descriptor recreation.
- Validate with `perf_2d_render_fps.cs -Wait` and at least one full visual sample.

### Final safe implementation decision

Dynamic-only secondary command buffers were tested and reverted because they added recording overhead without static command reuse. The next implementation must therefore avoid a broad secondary conversion and focus on cacheable static work.

Vulkan render pass contents mode prevents freely mixing inline primary draw commands and secondary draw commands in the same subpass. Because of that, the safe implementation path is:

1. Keep the current inline primary path as the active renderer path until the static-secondary path is complete.
2. Build static-secondary command buffer cache for draw lists that are already recognized by `TryGetStaticDrawListTag()`.
3. Add an all-secondary render pass path only after both pieces exist:
	- cached static secondary command buffers
	- one per-frame dynamic secondary command buffer
4. Enable the all-secondary path only for frames with at least one static binding and with ordering that can be preserved. Frames without static bindings should remain on the current inline path until a separate benchmark proves global secondary is beneficial.

This is not a failure fallback; it is a composition-based renderer strategy. The secondary strategy must be selected before command recording starts, not after an error.

### Static secondary cache key

Minimum cache key fields:

- static tag string
- swapchain generation
- framebuffer index
- render pass handle
- framebuffer handle
- static geometry vertex/index buffer handles
- display size and framebuffer scale
- display position / transform constants
- texture generation
- command count and command signature hash

The command signature hash should include texture id, clip rect, element count, index offset, vertex offset, translation, and callback presence. Static draw commands with callbacks must not be cached as static secondary command buffers.

### Invalidation events

- swapchain recreation
- render pass or framebuffer recreation
- graphics/subpixel pipeline recreation
- static geometry buffer recreation
- texture descriptor recreation or destruction
- font texture id/descriptor change
- display transform change
- static command signature change

### Next code steps

1. Add a texture generation counter and increment it on texture create/update/destroy.
2. Add static command signature hashing for static draw lists.
3. Add cache record type for static secondary command buffers, but do not execute them yet.
4. Record and validate cached static secondary command buffers in isolation.
5. Only then switch selected static-heavy frames to the all-secondary execution path and measure.

## Follow-up

1. Capture pre-change baseline on a clean branch or tag to compute true Before/After improvement percentages.
2. Implement the secondary command recording split described above.
