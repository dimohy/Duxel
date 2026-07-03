# Duxel GPU-Driven Renderer Migration Plan

> Status owner: any agent continuing this work MUST update the Status table below and `docs/optimization-session-2026-07-03.md`.
> Approved by user on 2026-07-03: full A+B+C migration plus `VulkanRendererBackend` partial-class consolidation.
> Companion Copilot memory note: `/memories/repo/duxel-gpu-driven-renderer-plan-2026-07-03.md`.

## 1. Goal

Convert the Vulkan backend from a multi-pipeline, per-texture-descriptor renderer into a modern GPU-driven UI renderer:

- **A. Bindless textures** (Vulkan 1.2 descriptor indexing) — one global descriptor array bound once per frame; commands carry a texture slot index.
- **B. Unified dual-source-blend pipeline** — every draw outputs `outColor` + `outBlendFactor`; standard draws emit `blendFactor = vec4(alpha)` which is mathematically identical to classic alpha blending; ClearType subpixel text keeps per-channel coverage. Removes font↔triangle↔primitive pipeline alternation.
- **C. Vertex pulling** — vertex and primitive-instance data read from storage buffers keyed by `gl_VertexIndex`/push-constant base offsets; no vertex-input state; converges to a single pipeline.
- **Cleanup** — delete legacy pipeline-selection/descriptor/binding code paths made obsolete by A–C and consolidate the 60+ `VulkanRendererBackend.*.cs` partial files into coherent modules.

## 2. Baseline (must re-measure before starting any phase)

2026-06-05 profile, mixed layer-widget cache scene (`DUXEL_VK_PROFILE=1`):

- `binds(pipe=47 desc=69 geom=7 prim=7)` per sampled frame.
- Pipeline churn: `font=22, tri2prim=22, prim2tri=21` — text/primitive alternation dominates.
- FPS gates: long mixed cache ≈ `6812` (density 100) / `6046` (170); quick global static-cache ≈ `1642`; layer dirty single ≈ `770`.

Expected end state: `desc ≈ 1–2` per frame, `pipe ≈ 2–5` per frame, no per-command vertex-buffer binds.

## 3. Working method (mandatory protocol for every phase)

1. **Measure first.** Capture a fresh baseline with the benchmark gates in §6 before touching code. Never reuse stale numbers from this document.
2. **One phase at a time.** Land A, gate it, record it, then start B. Never combine unvalidated phases in one measurement.
3. **No fallbacks.** If a required device feature is missing, throw with a clear message. Do not add runtime branches that keep the old path alive as a silent fallback. Old paths are deleted, not disabled.
4. **Explicit failure over defensive code.** Root-cause fixes only.
5. **Gate rules.** A phase is retained only if: build is clean (0 warnings), shader validation passes, FPS gates are neutral-or-better on the mixed scenes, AND visual captures confirm correctness. A benchmark win with broken/unverified visuals is a rollback, not a win (see the 2026-05-16 Layer Canvas regression lesson).
6. **Record everything.** Per-item Before/After table (target, summary, validation command, numbers, improvement %, risk/follow-up) appended to `docs/optimization-session-2026-07-03.md`. Negative results are recorded, not hidden.
7. **Comments in code are English.** User-facing summaries are Korean.

## 4. Key facts an agent needs (verified 2026-07-03)

- Target API: Vulkan 1.2 (`Vk.Version12` in `src/Duxel.Vulkan/Vulkan/VulkanApi.cs`), so descriptor indexing is core — enable via `PhysicalDeviceDescriptorIndexingFeatures` chained into `DeviceCreateInfo.PNext` in `VulkanRendererBackend.DeviceResources.cs` (features needed: `runtimeDescriptorArray`, `descriptorBindingPartiallyBound`, `descriptorBindingSampledImageUpdateAfterBind`, `descriptorBindingUpdateUnusedWhilePending`). Query support via `vkGetPhysicalDeviceFeatures2`; if unsupported, fail explicitly.
- The per-draw texture index comes from a push constant → it is **dynamically uniform** → `nonuniformEXT` and the NonUniformIndexing feature are NOT required.
- `DualSrcBlend = 1` is already enabled at device creation (used by the existing subpixel pipeline), so Phase B needs no new device feature.
- Push constants today: `scale` @0 (vec2), `translate` @8 (vec2), `opacity` @16 (float) — 20 bytes, vertex stage only (`VulkanRendererBackend.PipelineResources.cs`, `CommandPushConstantState.cs`). Phase A adds a fragment-stage range carrying the texture slot index; keep the vertex range layout untouched so partial 4-byte opacity updates keep working.
- Shaders live in `src/Duxel.Vulkan/Shaders/` (GLSL + committed `.spv`, embedded resources loaded in `PipelineResources.cs`). Compile: `glslangValidator -V --target-env vulkan1.2 <src> -o <spv>`; validate: `spirv-val --target-env vulkan1.2 <spv>`. **Back up working `.spv` files before recompiling** (see user memory `shader-compilation-safety`).
- Texture registry: `VulkanRendererBackend.TextureResources.cs` allocates one descriptor set per texture (`AllocateDescriptorSets` + `UpdateDescriptorSets`). Phase A replaces this with slot indices into one variable-count array binding.
- Pipeline selection switch: `VulkanRendererBackend.CommandPipelineSelection.cs`; descriptor bind dedup: `CommandDescriptorState.cs`; pipeline bind dedup: `CommandPipelineState.cs`; primitive instance layout (20-byte packed) documented in `PrimitiveInstanceEncoding.cs` and `Shaders/primitive.vert`.
- Static geometry cache binds cached device-local vertex/index/primitive buffers per draw list (`StaticGeometryCache.cs`, `CommandBufferBindingState.cs`). Phase C must keep static caching but rebind via SSBO descriptor or buffer-device-address instead of vertex-input bindings.
- Vendor/device policy and env-var pipeline modes live in `DevicePolicy.cs` (`DUXEL_VK_SOLID_UNIFIED_PIPELINE`, triangle-color mode, static-primitive-triangle mode…). Phases B/C supersede these solid/color pipeline splits — delete the obsolete modes and their env vars as part of cleanup, do not leave them as dead toggles.

## 5. Phase details

### Phase A — Bindless textures
1. Enable descriptor-indexing features at device creation; fail explicitly when unsupported.
2. New descriptor layout: binding 0 = `sampler2D[]` (combined image sampler array, variable descriptor count, UPDATE_AFTER_BIND | PARTIALLY_BOUND), one set allocated once; capacity from device limits (cap e.g. 16k, assert ≥ pool needs).
3. Texture slot allocator: texture ID → array slot (free-list reuse; slot 0 reserved for the app white texture). Write descriptor on create/update; on destroy, return slot after frame-fence retirement (reuse existing retired-pool pattern from `StaticGeometryRetiredPool.cs`).
4. Fragment shaders index the array with a push-constant slot index (fragment-stage push range after the 20-byte vertex range).
5. Bind the global set once per command buffer; delete `CommandDescriptorState` dedup logic and per-texture sets.
6. Gate + record. Expected: `desc 69 → ~1`.

### Phase B — Unified dual-source-blend pipeline
1. All fragment shaders gain dual-source outputs; standard draws write `outBlendFactor = vec4(outColor.a)` (identical result to `SRC_ALPHA/ONE_MINUS_SRC_ALPHA`), subpixel text keeps coverage output; select via push-constant mode flag (per-draw uniform branch, cheap).
2. Collapse `imgui.frag` / `imgui_color.frag` / `imgui_subpixel.frag` / `primitive.frag` / `primitive_color.frag` variants; blend state = dual-source for the unified pipelines.
3. Delete `_graphicsColorPipeline`, `_solidColorPipeline`, `_subpixelPipeline`, `_primitiveColorPipeline` and the `DevicePolicy` modes/env-vars that selected them.
4. **Vendor gate required**: dual-source blending can disable blend fast paths on some GPUs. Run full gates on NVIDIA (primary dev machine) and record; if broad FPS regresses beyond noise, stop and reassess before deleting pipelines.
5. Expected: pipeline churn `font/tri2prim/prim2tri → 0`; remaining binds only triangle↔primitive if C not yet landed.

### Phase C — Vertex pulling, single pipeline
1. Upload `UiVertex` streams and packed primitive instances into `STORAGE_BUFFER`-usage buffers (host-visible dynamic ring + device-local static, mirroring today's split).
2. Vertex shader reads vertex/instance data by index: `firstVertex`/`firstInstance` style base offsets via push constants (or vertex offset encoded in draw call); no `VkVertexInputState`.
3. Merge triangle and primitive vertex shaders into one with a mode branch (rect/circle expansion already runs in `primitive.vert`; triangle mode reads the vertex stream directly).
4. Result: ONE pipeline for all UI draws. Delete geometry/primitive buffer bind dedup (`CommandBufferBindingState` geometry parts), `CommandPipelineSelection`, and per-kind pipeline fields.
5. Storage buffer alignment: respect `minStorageBufferOffsetAlignment`; keep 4-byte-aligned staging ranges (existing rule in `UploadScheduler`).
6. Gate + record. Watch for vertex-pulling cost on AMD/Intel iGPU when hardware is available; NVIDIA gate is the retention criterion for now.

### Cleanup phase
1. Delete obsolete files/modes; fold single-use partial files into their callers. Target ≤ ~25 partial files with clear module boundaries (bootstrap/device, swapchain/frame, upload, static cache, recording, textures-bindless, diagnostics).
2. `docs/design.ko.md` renderer section, `docs/duxel-agent-reference.md` + `.ko.md` must be updated (with `Last synced` dates).
3. Final full gate matrix + NativeAOT publish gate + visual captures.

## 6. Validation commands

```powershell
# Build (must be 0 warnings / 0 errors)
dotnet build Duxel.slnx -c Release

# Shader compile + validate (from src/Duxel.Vulkan/Shaders/, back up .spv first)
glslangValidator -V --target-env vulkan1.2 imgui.vert -o imgui.vert.spv
spirv-val --target-env vulkan1.2 imgui.vert.spv

# Benchmark gates (managed, local source)
$env:DUXEL_LAYER_WIDGET_DENSITY_SCALES='100,170'; $env:DUXEL_LAYER_WIDGET_PHASE_SECONDS='2.2'
$env:DUXEL_LAYER_WIDGET_BENCH_OUT='artifacts\<phase>-layer-widget.json'
./run-fba.ps1 samples/fba/layer_widget_mix_bench_fba.cs -Managed

$env:DUXEL_GLOBAL_DIRTY_BENCH_DENSITY='1600'; $env:DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS='0.4'
$env:DUXEL_GLOBAL_DIRTY_BENCH_OUT='artifacts\<phase>-global.json'
./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -Managed

# Bind-count profile (compare binds(...) and transitions(...) lines)
$env:DUXEL_VK_PROFILE='1'

# NativeAOT publish gate
./run-fba.ps1 samples/fba/text_render_validation_fba.cs -NoLaunch

# Visual verification (mandatory before retaining a phase)
./run-fba.ps1 samples/fba/all_features.cs
# plus capture scripts under artifacts/ (e.g. capture-layer-widget-mix.ps1) and compare captures/
```

## 7. Status

| Phase | State | Session log entry | Notes |
|---|---|---|---|
| A. Bindless textures | **done (2026-07-03)** | optimization-session-2026-07-03.md | mixed cache +18.6%, visual verified |
| B. Unified pipeline | **done (2026-07-03)** | optimization-session-2026-07-03.md | nocache +26.1%, cache +8.8%, pipe binds 47→20 |
| C. Vertex pulling | **done (2026-07-03)** | optimization-session-2026-07-03.md | BDA + single pipeline; BAR memory required for dynamic pull perf; nocache +15%, cache +16.7% |
| Partial-class consolidation | **done (2026-07-03)** | optimization-session-2026-07-03.md | 55 → 19 modules, shaders 9 → 2 |
| Docs/final gates | **done (2026-07-03)** | optimization-session-2026-07-03.md | design.ko.md + agent-reference pair synced; final long gate + visual + NativeAOT passed |
| D. Dynamic rendering | **done (2026-07-03)** | optimization-session-2026-07-03.md | render pass/framebuffers deleted; explicit barriers; inline MSAA resolve; perf-neutral |

## Remaining follow-ups

- Gate dual-source blending and vertex pulling on AMD/Intel hardware when available (NVIDIA verified).
- Devices without BAR memory fall to host-visible dynamic buffers and will show slower vertex pulling; measure before shipping defaults for such targets.
- Devices without `VK_KHR_dynamic_rendering` fail explicitly; all 2026-era desktop drivers expose it on Vulkan 1.2.
