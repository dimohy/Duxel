# Optimization Session — 2026-07-20

## Scope and acceptance target

Rounded declarative controls must remain visually smooth at 1x MSAA without trading away throughput. The accepted path must remove fixed-segment rounded-rectangle approximation, reuse one analytic primitive contract for circles and rounded rectangles, combine common fill/border rendering, and avoid shading the guaranteed-empty interior of border-only large panels.

## Hypothesis

The declarative rounded-rectangle path independently generated 3–8 segment corner polygons/polylines while the renderer already transported circle primitives. The visible facets came from this duplicate geometry path, and the CPU vertex/index generation plus separate fill/stroke commands also increased upload and draw work. A packed analytic instance evaluated in the fragment shader should make edge quality independent of segment count, while a combined fill/border instance and a tight border-only perimeter should reduce CPU work, command count, vertex traffic, and fragment overdraw.

## Retained implementation

- `UiRectFilledPrimitive` carries radius, border thickness, fill color, and border color in one 32-byte Vulkan instance record.
- Rounded fills and combined fill/border panels emit one rect primitive. Declarative Surface, Badge, callout, frame, and decoration paths use the combined overload.
- `AddCircleFilled(...)` emits a bounding quad and encodes circle kind in the radius field. Caller segment values no longer split batches or control edge quality.
- The fragment shader evaluates circle/rounded-box signed distance and `fwidth` coverage at 1x MSAA, then blends fill and border in one pass.
- Border-only rounded rectangles use eight non-overlapping cells of a 3x3 perimeter grid (48 generated vertices), excluding the center from rasterization.
- Static primitive triangle expansion rejects any circle, rounded rectangle, or bordered primitive so cached rendering cannot silently lose analytic coverage.
- Draw-list append/replay preserves the border-only geometry variant.
- `run-fba.ps1` and benchmark scripts resolve filesystem inputs with `-LiteralPath`, enumerate literal directories, and compare file properties instead of passing wildcard paths to PowerShell providers. Named PowerShell parameters use hashtable splatting; native tools use argument arrays.

## Focused benchmark method

- Sample: `samples/fba/analytic_rounded_primitives_bench_fba.cs`
- Runtime: NativeAOT, .NET runtime `10.0.10`; SDK command version `10.0.302`
- Configuration: Release, Vulkan Render profile, VSync off, MSAA 1x, 1280×720
- Logical workload per set: one rounded fill/border panel plus one circle
- Counts: 2,000 / 6,000 / 12,000 sets
- Warmup: 0.3 s per phase
- Measurement: 1.5 s per phase
- Independent runs: 3 baseline + 3 candidate
- Aggregation: median of the three run-level values for each metric
- Raw artifacts: `samples/fba/artifacts/analytic_rounded_primitives_bench_fba/baseline-{1,2,3}.json` and `analytic-{1,2,3}.json`

## Before / after evidence

| Primitive sets | Avg FPS before | Avg FPS after | FPS gain | Median ms before | Median ms after | Median reduction | p95 ms before | p95 ms after | p95 reduction | p99 ms before | p99 ms after | p99 reduction | 1% low FPS before | 1% low FPS after | 1% low gain |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2,000 | 385.582 | 1,446.919 | +275.26% | 2.540100 | 0.665900 | 73.78% | 2.883170 | 0.841800 | 70.80% | 3.135070 | 0.997190 | 68.19% | 273.825 | 871.450 | +218.25% |
| 6,000 | 115.484 | 551.164 | +377.26% | 8.413350 | 1.793200 | 78.69% | 10.062460 | 1.917695 | 80.94% | 11.282532 | 2.141675 | 81.02% | 84.170 | 438.498 | +420.97% |
| 12,000 | 54.307 | 277.106 | +410.26% | 18.166300 | 3.577550 | 80.31% | 20.560190 | 3.943075 | 80.82% | 21.412106 | 4.217903 | 80.30% | 46.118 | 226.814 | +391.81% |

Candidate average-FPS raw runs:

- 2,000: `1486.841`, `1446.919`, `1424.775`
- 6,000: `551.717`, `544.426`, `551.164`
- 12,000: `277.106`, `271.388`, `278.857`

Broad `layer_widget_mix_bench_fba.cs` regression gate (three-run medians, 0.25 s warmup + 0.9 s measurement per phase):

| Phase | Avg FPS | Median ms | p95 ms | p99 ms | 1% low FPS |
|---|---:|---:|---:|---:|---:|
| nocache-drawlist-100 | 953.825 | 1.012000 | 1.273960 | 1.521796 | 580.106 |
| cache-drawlist-100 | 7,051.316 | 0.130000 | 0.207700 | 0.324520 | 2,367.205 |
| nocache-drawlist-170 | 613.488 | 1.579050 | 1.870100 | 2.134320 | 371.844 |
| cache-drawlist-170 | 6,214.970 | 0.143500 | 0.245830 | 0.452604 | 1,748.009 |

This broad gate is recorded as a post-change regression artifact, not as an A/B percentage claim. The focused logical-shape gate above is the accepted identical-scene before/after comparison.

## Validation

- GLSL vertex/fragment compilation: passed for Vulkan 1.2.
- `spirv-val --target-env vulkan1.2`: passed for both checked-in SPIR-V binaries.
- Release solution build with warnings as errors: passed for `net8.0`, `net9.0`, and `net10.0`; 0 warnings, 0 errors.
- NativeAOT local-source FBA publish: passed through `run-fba.ps1 -NoCache -NoLaunch`.
- Broad layer/widget NativeAOT regression: three independent runs completed for all four phases.
- Primitive contract self-check: no CPU vertices/indices for rounded/circle shapes, combined and border-only variants retained, circle requests with different segment counts share one command, and replay preserves variants.
- PowerShell parser: all 16 repository `.ps1` files passed after the final literal-path/argument-boundary edits.
- PowerShell execution smoke: managed `run-fba.ps1` produced the contract benchmark output through `ProcessStartInfo.ArgumentList`; the vector A/B wrapper also completed both child-process runs.
- Automated Windows screenshot capture was unavailable because the Computer Use connection rejected its initialization metadata. No unverified screenshot claim is made; the visual reference mode remains in the focused sample for direct inspection.

## Risks and follow-up

- Signed-distance coverage uses screen-space derivatives and must remain on the primitive-instance path; future static-cache optimizations must retain the analytic-shape exclusion.
- The 1-pixel perimeter safety band is intentionally conservative. Very large border thicknesses are clamped to half the smaller extent by Core.
- Performance values are device- and driver-specific; the structural reduction in instances, CPU geometry, and shaded border interior is portable, while exact percentages require independent AMD/Intel gates.
- The `segments` parameter on `AddCircleFilled(...)` remains source-compatible but no longer affects Vulkan edge tessellation or batching.
