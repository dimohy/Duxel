# Optimization Session — 2026-07-10

## Scope and decision

This session closed the highest-value follow-ups from the 2026-07-03 GPU-driven renderer work:

- evaluate the per-frame dynamic dirty-rectangle calculation without weakening the public draw-data contract;
- pin the repository SDK and add repeatable frame-tail measurements;
- avoid expanded static-circle layout materialization on cache hits;
- isolate and reduce DirectText changing-string rasterization cost;
- evaluate, but reject, DirectText page mode as a default.

The retained DirectText candidate improved all measured changing-string metrics on the local Windows/NVIDIA gate. The dirty-rectangle candidate was fully reverted: its A-B-A-B direction reversed, and removing production of public `UiDrawData` fields would change observable semantics. DirectText page mode remains diagnostic-only because its high FPS result coincided with a mostly black capture with the UI missing.

## Environment and measurement rules

- SDK: `10.0.301`, selected by the new root `global.json` (`rollForward: latestPatch`, prerelease disabled); verified with `dotnet --version`.
- Build/run mode: Release, local-source FBA path, VSync off for focused benches.
- Comparison rule: warm up each phase, run at least three independent processes, and compare run medians.
- Frame metrics: average FPS plus raw-frame median, p95, p99, and 1% low FPS. Average FPS alone is not a completion gate.
- DirectText comparison: page mode off, `1.2`-second phases, `32` warmup frames, `8` rows, `256` corpus frames; table values are the medians of three independent changing-cache-miss runs. The sample defaults remain `3` seconds and `96` warmup frames.

## Changes and evidence

### 1. Rejected candidate — dynamic dirty-rectangle removal

`UiContext` walks every non-static draw command in `EndFrame()` to compute `DynamicDirtyRect` and copies the result into public `UiDrawData`. The current Vulkan renderer does not consume those fields and still clears/renders the full target, so removing the O(commands) walk was evaluated as a candidate. The experiment also stopped producing meaningful values for public draw-data fields, which is a contract change even if the current Vulkan backend ignores them.

The layer-widget gate used `2.2`-second measured phases, `0.25`-second warmup, density scales `100,170`, and the four phases `nocache-100`, `cache-100`, `nocache-170`, `cache-170`.

| Sequence | Source state | Runs | nocache-100 | cache-100 | nocache-170 | cache-170 |
|---|---|---:|---:|---:|---:|---:|
| A1 | Original calculation | 5 | 789.020 | 5199.005 | 587.225 | 5230.274 |
| B1 | Calculation removed | 5 | 930.827 | 7530.403 | 825.135 | 7071.623 |
| A2 | Calculation restored | 3 | 872.711 | 7408.636 | 811.814 | 7097.359 |
| B2 | Calculation removed, second candidate | 3 | 763.461 | 7106.577 | 739.189 | 6653.663 |

The first A→B comparison improved, while the second regressed. The quantitative result is therefore **INVALID: no FPS improvement claimed**. Because the result was unstable and the candidate changed public `UiDrawData` semantics, the removal was fully reverted. `ComputeDynamicDirtyRect` and population of `DynamicDirtyRect`/`HasDynamicDirtyRect` remain in the final source.

### 2. Reproducible SDK and frame-tail output

The root `global.json` selects SDK `10.0.301`, so developer and benchmark commands do not silently switch to an installed preview SDK. `BenchOptions.FromEnvironment(prefix)` and `BenchOptionsReader` provide one typed, prefixed option surface for focused samples. `BenchFrameRecorder` records raw frame durations and calculates sample count, measured seconds, average FPS, median/p95/p99 frame time, and 1% low FPS. It owns fixed-capacity working arrays and sorts only when a phase completes, keeping per-frame recording allocation-free.

`layer_widget_mix_bench_fba.cs` now uses this recorder after warmup and writes JSON schema version `2`. A frame that crosses the warmup boundary is excluded instead of being partly attributed to the measured phase. Each record adds `measuredSeconds`, `medianFrameMs`, `p95FrameMs`, `p99FrameMs`, and `low1PctFps` while preserving the existing phase/cache/backend/density/average fields. Its phase option is bounded at `30` seconds.

Both the layer-widget recorder and the DirectText frame/text-work recorders use capacity `1,048,576`. The original `262,144` capacity could fail a valid 30-second phase after the DirectText Atlas control was observed near `10.7k` FPS; the larger bound preserves explicit overflow failure while covering that supported long-run range.

### 3. Static primitive cache-hit layout materialization

Static geometry preparation now separates cheap source/hash/count inspection from expanded primitive layout materialization. Cache matching happens first; full rect/circle triangle layout and geometry content are created only for a miss, rebuild, or update. The Vulkan profile `staticPrim(...)` block now includes `layout=...` to expose the number of layouts materialized in that frame.

Circle-cache profiling recorded `layout=18` on the initial creation frame and `layout=0` for the following `2631` cache-hit frames. This proves that steady cache replay no longer rebuilds the expanded circle layout on the CPU.

### 4. DirectText changing-string benchmark and rasterizer batching

The new `directtext_dynamic_text_bench_fba.cs` gate runs stable-cache-hit and changing-cache-miss phases against a prebuilt unique ASCII corpus. It reports frame and text-work tail distributions, per-frame allocated bytes, and Gen0/1/2 collection counts. Required output and optional controls use the `DUXEL_DIRECTTEXT_BENCH_` prefix.

The retained Windows DirectWrite path batches a whole text run instead of resolving and measuring each glyph through separate native calls. Runs up to 128 UTF-16 code units use stack buffers; larger runs rent codepoint, glyph-index, advance, and metric buffers from `ArrayPool<T>`. One `GetGlyphIndices` call and one `GetDesignGlyphMetrics` call fill the run, removing the prior `List<T>`, `ToArray()`, per-glyph native-call, and managed pin-handle churn. The single-glyph helpers also use direct stack addresses instead of temporary arrays and `GCHandle` instances. Unavailable or nonpositive design advances retain the previous half-em fallback semantics.

#### Final retained result — changing-cache-miss, three-run median

| Metric | Before | After | Change |
|---|---:|---:|---:|
| Average FPS | 417.201 | 443.841 | +6.385% |
| Median frame time | 2.4074 ms | 2.2695 ms | -5.728% |
| p95 frame time | 2.9174 ms | 2.7812 ms | -4.669% |
| p99 frame time | 3.2239 ms | 3.20031 ms | -0.732% |
| 1% low FPS | 222.475 | 266.752 | +19.901% |
| Average text work | 252.764 us | 184.548 us | -26.988% |
| Average allocated bytes/frame | 197387 | 137090 | -30.548% |

The final page-off all-features audit (`artifacts/directtext-dwrite-batched-audit.png`) rendered the expected UI after batching.

### 5. DirectText page-mode candidate rejected

With `DUXEL_DIRECT_TEXT_PAGE=1`, a performance run reached `1819.709` FPS. However, the page-enabled all-features capture (`artifacts/directtext-page-on-audit.png`) was mostly black with the UI missing; the page-off capture (`artifacts/directtext-page-off-audit.png`) rendered correctly. The performance number is not accepted as an improvement because the output is visually invalid. Page mode remains off by default and must pass rendered visual comparison before it can be reconsidered.

## Commands used

### SDK and build

```powershell
dotnet --version
dotnet build Duxel.slnx -c Release --no-restore
```

### Layer-widget A-B-A-B gate

The following command pattern was repeated for the A1/B1/A2/B2 source states, with a unique temporary JSON output for each independent process:

```powershell
$env:DUXEL_LAYER_WIDGET_DENSITY_SCALES = '100,170'
$env:DUXEL_LAYER_WIDGET_PHASE_SECONDS = '2.2'
$env:DUXEL_LAYER_WIDGET_WARMUP_SECONDS = '.25'
$env:DUXEL_LAYER_WIDGET_BENCH_OUT = $temporaryJsonPath
./run-fba.ps1 samples/fba/layer_widget_mix_bench_fba.cs -Managed -Configuration Release -NoCache -ManagedTimeoutSeconds 120 -KillProcessTreeOnTimeout
```

### Static circle cache profile

```powershell
$env:DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT = $temporaryJsonPath
$env:DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE = 'circles'
$env:DUXEL_STATIC_CACHE_REBUILD_LAYERS = '18'
$env:DUXEL_STATIC_CACHE_REBUILD_DENSITY = '900'
$env:DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS = '8'
$env:DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS = '.6'
$env:DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS = '.25'
$env:DUXEL_VK_PROFILE = '1'
$env:DUXEL_VK_PROFILE_EVERY = '1'
$env:DUXEL_VK_PROFILE_OUT = $temporaryProfilePath
./run-fba.ps1 samples/fba/static_cache_rebuild_bench_fba.cs -Managed -Configuration Release -NoCache -ManagedTimeoutSeconds 120 -KillProcessTreeOnTimeout
```

### DirectText focused gate

The before/after gate was run three times per candidate with unique output paths:

```powershell
$env:DUXEL_DIRECT_TEXT_PAGE = '0'
$env:DUXEL_DIRECTTEXT_BENCH_OUT = $temporaryJsonPath
$env:DUXEL_DIRECTTEXT_BENCH_PHASE_SECONDS = '1.2'
$env:DUXEL_DIRECTTEXT_BENCH_WARMUP_FRAMES = '32'
$env:DUXEL_DIRECTTEXT_BENCH_ROWS = '8'
$env:DUXEL_DIRECTTEXT_BENCH_CORPUS_FRAMES = '256'
./run-fba.ps1 samples/fba/directtext_dynamic_text_bench_fba.cs -Managed -Configuration Release -NoCache -ManagedTimeoutSeconds 120 -KillProcessTreeOnTimeout
```

### Visual and NativeAOT gates

```powershell
$env:DUXEL_DIRECT_TEXT_PAGE = '0'
./artifacts/capture-all-features.ps1 -CapturePath 'artifacts/directtext-page-off-audit.png'
$env:DUXEL_DIRECT_TEXT_PAGE = '1'
./artifacts/capture-all-features.ps1 -CapturePath 'artifacts/directtext-page-on-audit.png'
$env:DUXEL_DIRECT_TEXT_PAGE = '0'
./artifacts/capture-all-features.ps1 -CapturePath 'artifacts/directtext-dwrite-batched-audit.png'

./run-fba.ps1 samples/fba/directtext_dynamic_text_bench_fba.cs -NoCache -NoLaunch
./run-fba.ps1 samples/fba/all_features.cs -NoCache -NoLaunch
./run-fba.ps1 samples/fba/text_render_validation_fba.cs -NoCache -NoLaunch
```

### Shader source and committed SPIR-V validation

```powershell
$shaderDirectory = 'src/Duxel.Vulkan/Shaders'
Get-ChildItem $shaderDirectory -File | Where-Object Extension -in '.vert', '.frag' | ForEach-Object {
    $temporarySpirv = Join-Path $env:TEMP ($_.Name + '.validation.spv')
    glslangValidator -V --target-env vulkan1.2 $_.FullName -o $temporarySpirv
    spirv-val --target-env vulkan1.2 $temporarySpirv
}
Get-ChildItem $shaderDirectory -Filter '*.spv' -File | ForEach-Object {
    spirv-val --target-env vulkan1.2 $_.FullName
}
```

## Final verification

- `dotnet --version` selected `10.0.301` from the repository root.
- `dotnet build Duxel.slnx -c Release --no-restore` passed with `0` warnings and `0` errors.
- NativeAOT no-launch gates passed for `directtext_dynamic_text_bench_fba.cs`, `layer_widget_mix_bench_fba.cs`, `all_features.cs`, and `text_render_validation_fba.cs`.
- `glslangValidator -V --target-env vulkan1.2` compiled every committed `.vert`/`.frag` source to temporary SPIR-V, and `spirv-val --target-env vulkan1.2` passed for those outputs.
- `spirv-val --target-env vulkan1.2` also passed for the committed `imgui.vert.spv` and `imgui.frag.spv` binaries.
- Page-off and final batched-DirectWrite captures rendered the UI; the page-on capture remained visually invalid as documented above.
- `git diff --check` passed for the final change set.

## Remaining work checklist

- [ ] Re-run the DirectText and Vulkan static-cache correctness/performance gates on AMD, Intel, and non-BAR systems, and retain the device-attributed artifacts.
- [ ] Fix the DirectText page-mode mostly-black/missing-UI regression and pass page-off/page-on rendered comparison before considering the page path as a default.
- [ ] Remove stable-to-changing order bias by alternating phase order or splitting phases into independent processes, recording order metadata, and automating three-or-more-run median/noise decisions.

## Risks and follow-up

- The dirty-rectangle FPS result is invalid because A-B-A-B direction reversed. The candidate is not retained and must not be cited as a speedup. Any future attempt must preserve public `UiDrawData` semantics or explicitly version that contract, then benchmark the consuming partial-redraw path end to end.
- DirectText results are local Windows/NVIDIA measurements. Repeat on AMD, Intel, non-BAR systems, and representative scripts before declaring global completion.
- Batched DirectWrite calls increase the amount of unsafe stack/pool-backed native interop in one scope. Keep NativeAOT and rendered text validation in the gate whenever this path changes.
- `BenchFrameRecorder` capacities are fixed by each benchmark (`1,048,576` for the layer-widget and DirectText gates). A phase that exceeds capacity still fails explicitly; do not silently drop samples or raise the 30-second phase limit without resizing and revalidating the bound.
- Static cache-hit matching must continue to include content hash, source counts, primitive-instance base count, and expanded/non-expanded mode before layout materialization is skipped.
- DirectText page mode is not shippable as a default until the mostly-black/missing-UI regression is fixed and page-off/page-on captures are visually equivalent.
- SDK pinning improves reproducibility but patch roll-forward can still select a newer installed `10.0.3xx` patch. Always record `dotnet --version` with performance artifacts.
