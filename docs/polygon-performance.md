# Shared engine optimization and bouncing-polygon validation

> Last synced: 2026-11-04
> Status: shared engine implementation, equal-condition repeated measurements, correctness/regression and local delivery verified.

The sample is `samples/fba/Duxel_perf_test_fba.cs`. Defaults are 5,000 bodies, Size 6,
and a requested 1920×1080 window. Observed framebuffer: 1920×1080; UI viewport:
1920×1032; canvas: 1520×918.4625.

Shared implementation:

- `SpatialGrid2D`: a reusable dense spatial index supporting negative coordinates,
  with an explicit cell allocation budget.
- `Duxel.Core.Simulation.CircleCollisionSolver2D`: a shared circular collision module
  operating on caller-owned struct spans. Appearance and rendering stay with the caller.
  Neighbor coverage uses the actual maximum diameter and initial positions; optional
  bounds constrain bodies after penetration correction.
- `UiDrawListBuilder.AddRegularPolygons`: ordered vector polygon batches, preserving
  exact vertices, colors and triangles from individual convex polygon generation.
- `AddCircleFilled`: circles and rectangles share the six-vertex instance stream using
  the existing shader's negative-radius encoding. Analytic AA and submission order remain.
  Legacy `UiCircleFilledPrimitive` input remains supported. Static triangle expansion
  excludes negative radii to preserve analytic coverage.
- `UiRendererFrameMetrics.FramebufferSize`: exposes the actual rendering target size.

The canvas uses the existing GPU vector `BeginLayer` path, separates static backdrop
geometry from moving polygons, and uses `ContentVersion` to retain geometry while paused.
Motion, appearance, region and backdrop-cache changes invalidate that geometry.
Controls occupy a separate UI region. There are no per-layer bitmap render targets.

The backdrop now uses shared nested `BeginLayer` with `StaticCache`/`ContentVersion`
instead of sample-owned draw-list copies. The common render pipeline is enabled by
default; `DUXEL_PERF_PIPELINE=0` explicitly selects the serial comparison path.
`UiDrawList.GetCommandFeatures` and `UiDrawData.GetCommandFeatures` reuse unchanged
callback/GPU command classification across producer and renderer. Changed command
state invalidates classification; unstamped external commands are inspected directly.
There are no sample-specific branches in this shared metadata path.

## 2026-11-04: controls and population edits

`Dynamic Canvas Layer` switches the canvas3Dlayer. `Global Static Cache` controls this
sample's static backdrop, not every glyph/texture cache. `Render Profile` switches
Render/MSAA1 and Display/MSAA4 independently of VSync. VSync ON now uses FIFO in the
shared renderer; OFF uses supported Immediate/Mailbox presentation. The previous ON
Mailbox policy could keep rendering FPS high while replacing pending frames without
tearing. [Khronos presentation contract](https://docs.vulkan.org/refpages/latest/refpages/source/VkPresentModeKHR.html).
`UiRendererFrameMetrics.VSyncEnabled`/`MsaaSamples`/`PresentationMode` expose applied state.

`UiGpuParticleSystem.Append` preserves existing live GPU state and adds only new seeds.
`Truncate` preserves the leading surviving bodies (minimum one). Clear/Reset explicitly
initialize a new world. Edits publish immutable `UiGpuParticleSnapshot` instances captured
when `UiGpuParticleFrame` is constructed, so queued frames keep their original population.
Construct a new frame after edits; an old frame's `with { DeltaSeconds = ... }` retains
its old snapshot. `Initial` contains seeds, not live GPU positions.

```csharp
using var system = new UiGpuParticleSystem(initialBodies);
// Each submitted frame captures a stable snapshot.
drawList.AddGpuParticles(new(system, bounds, deltaSeconds));
system.Append(addedBodies);
drawList.AddGpuParticles(new(system, bounds, nextDeltaSeconds));
// Keep the leading live bodies without resetting them.
system.Truncate(remainingCount);
```

GPU buffers reserve power-of-two capacity. Within-capacity additions upload only new
state; growth copies live state on the device and uploads new seeds/indices. Truncation
keeps the live prefix. Per-body birth identities also distinguish a truncate/append pair
that leaves the same count. Capacity is less than twice the requested count at allocation time; truncation retains
it for later additions rather than immediately shrinking. Clear/Dispose releases the
system. Drawing uses active count, not capacity.

Focused checks compare every byte of existing48-byte states and cover queued frames,
capacity growth, truncation/replacement, resumed motion and the CPU reference. Adding100
bodies to5000uploads only4800state bytes. The NativeAOT interaction fixture reads the
original sample, adds observation/click probes, and tests actual Win32 button messages
and applied renderer settings. Successful fixture binaries are removed after preserving
ownership and file hashes.

Further regression exposed an existing discontinuity: a tiny grazing normal-velocity
sign difference could generate finite spin. CPU/GPU now limit angular response by the
actual normal impulse, without widening tolerances.
[Normal-force-proportional friction reference](https://box2d.org/documentation/md_simulation.html).
This remains distinct from the ordered CPU model and is not a general rigid-body engine.
Failures are retained in `glancing-before-fix` and `full-net10-final`; corrected checks
are in `glancing-after-fix-net10`, `glancing-stability-net10-2/3`,
`full-net10-glancing-fixed` and `full-net11-glancing-fixed`. NET10/11full suites each pass474checks.

Latest default executable: `samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe`;
SHA256 `A75CBAE0CC669A7932A21B4B4619382555EF99E087D561E2149AAA157279B546`.
SDK11.0.100-rc.3.26522.148 / runtime.NET11.0.0-rc.3.26522.148 / NativeAOT Speed;
5000bodies,Size6,framebuffer1920×1080,canvas1520×918.4625,Render/MSAA1,VSync off,
one-second warmup plus six-second measurement,three independent runs:

| Metric | Final median |
| --- | ---: |
| Average FPS | 5561.846 |
| median / p95 / p99 frame ms | 0.16590 / 0.30840 / 0.46203 |
| 1% low FPS | 1880.374 |
| GPU time | 151.733µs |
| draw / dispatch | 55 / 8 |
| Dynamic transfer / particle initialization | 9200B / 0B |
| UI Render / renderer scoped allocation | 3371.816B / 137.465B |

Run-average range:5521.04–5600.16FPS. The intermediate API-only checkpoint compares
5494.694→5472.606FPS(-0.40%) with overlapping ranges; it is not an isolated speedup or
regression claim. Final angular-response correctness changes also prevent an isolated
percentage comparison against the earlier model. UI allocation rises approximately8bytes
per frame, including the captured snapshot reference. GPU data stays resident; reserved
capacity is a memory tradeoff for fewer reallocations, not a full5000-body upload.
Evidence: `artifacts/particle-controls-2026-11-04/performance-final-summary.json`,
`raw-statistics-audit.json`(nine runs),`image-regression.json`(142/142unchanged), and
`native-controls-4`(24actual-input checks).

Reproduce interaction checks:

```powershell
& ./tests/RunPolygonControlsTests.ps1 -OutDir 'W:/MyWorks/Duxel/artifacts/controls-reproduction'
```

The runner posts messages only to its own test window and does not move the global cursor.

The following2026-11-03measurements describe their original executable, not an isolated
speedup claim for the latest changes.

## 2026-11-03: GPU-resident path and equal-algorithm comparison

`UiGpuParticleSystem`, `UiGpuParticleFrame`, `AddGpuParticles`, and Vulkan compute form
shared engine functionality. Initial state and indices upload once. GPU kernels perform
integration, spatial indexing, collision response and vertex generation. The existing
vector graphics pipeline directly reads the generated GPU vertex buffer. There is no
per-frame particle-state readback. `ParticleJacobiSimulation` is the reusable CPU
implementation of the same response algorithm.

Three independent runs per mode alternated CPU/GPU order with5,000bodies,Size6,
two Jacobi iterations, identical framebuffer/canvas,Render/MSAA1 (`release-native-paired`).
The common render pipeline is enabled. SDK: `11.0.100-rc.3.26522.148`; executable
runtime: `.NET 11.0.0-rc.3.26522.148`, X64 NativeAOT/Speed. Executable SHA256:
`4839615A2D412268B0DB6AF5DD3C42DB333E628FA9494AD584A4AA70EB89FB4A`.

| Same physics algorithm | Median FPS | median / p95 / p99 ms | 1% low FPS | Dynamic bytes/frame |
| --- | ---: | --- | ---: | ---: |
| CPU Jacobi | 849.066 | 1.10910 / 1.42032 / 2.59723 | 325.517 | 685696 |
| GPU Jacobi | 5448.071 | 0.16620 / 0.33793 / 0.49069 | 1757.484 | 9200 |

The FPS ratio is approximately6.42 (+541.65%). Run-average ranges are838.64–852.48CPU
and5425.83–5462.73GPU FPS. Approximately151.26µsGPU includes compute and drawing;
this moves former CPU work onto the GPU rather than merely reducing GPU time.
Eight dispatches process5,000bodies per moving frame; particle initialization upload
is zero after warmup. The9.2KBdynamic transfer includes changing UI. UI allocation
remains approximately3364bytes/frame. The previous ordered CPU response uses a
different update order and is not presented as an equal-algorithm comparison.
FPS describes the submission loop, not monitor refresh.

Additional three-run medians from the same image (`release-profile-cache-suite`):

| Setting | Median FPS | median / p95 / p99 ms | 1% low FPS | Dynamic bytes/frame |
| --- | ---: | --- | ---: | ---: |
| Render, backdrop cache on | 5476.160 | 0.16670 / 0.33889 / 0.48394 | 1741.088 | 9200 |
| Render, backdrop cache off | 5436.324 | 0.16650 / 0.34630 / 0.48556 | 1729.631 | 78320 |
| Display, backdrop cache on | 4254.199 | 0.21620 / 0.41766 / 0.55318 | 1520.836 | 9168 |
| Render, dynamic canvas off | 5657.287 | 0.15980 / 0.32950 / 0.48670 | 1747.663 | 9168 |
| Render, paused reuse | 7763.649 | 0.11290 / 0.25649 / 0.40329 | 2087.907 | 9160 |

Backdrop caching reduces geometry construction from approximately76.35to3.18µs and
dynamic transfer from78320to9200bytes/frame. FPS increases approximately0.73%; CPU
savings do not translate proportionally into FPS for this primarily GPU-bound workload.
Dynamic canvas is approximately3.20% slower than direct drawing and adds approximately
648UI-construction bytes/frame. This overhead is not described as a speedup. Paused
simulation uses zero compute dispatches. Differences in profile quality or moving/paused
workloads are not used to calculate improvement percentages.

GPU is the default. `DUXEL_PERF_SIMULATION` accepts `gpu`, `cpu-jacobi`, and `cpu`.
`cpu` retains the earlier ordered model; record the algorithm and iteration count.
Current GPU Add/Remove preserves existing state. Size,speed,sides and pause are
frame settings. A graphics queue lacking compute support fails explicitly.

CPU/GPU Jacobi apply impulses only to approaching contacts. Penetration below
coordinate scale times four float machine epsilons does not generate another impulse after an earlier iteration separated
the pair. Over61steps of5,000bodies against CPU snapshots, maximum position error was
0.000122pixels,direction-component error0.0000661,and angular-speed error0.0000425.
Finite state and bounds were checked. Static generated polygons exactly match CPU
images at1x/4xMSAA. This is circular-bound collision,not continuous collision detection
or a general rigid-body engine.

Following GPU integration and command-feature reuse, full.NET10/11 suites passed465checks each;136existing GPU
images matched exactly. A synchronization check with the validation layer actually
loaded reported no errors. These observations cover the tested NVIDIA device and
inputs,not all vendors or inputs. `UiDrawList.GetCommandFeatures` shares retained
command-state inspection between UI and render threads. ERD1200tables/MSAA4 regression
uses a6-second workload with3runs per scenario (`erd-frame-feature-regression`):
camera1259.94FPS, simultaneous entity motion1249.24FPS medians. These are not used to
calculate improvement against earlier18-second tours.

UI allocations cover the sample's current-thread `Render` region; renderer allocations
cover the renderer thread's measured region. Neither is whole-process allocation.
Uncollected GPU time/renderer allocation is JSON `null` with sample counts, not zero.
The default NativeAOT execution with profiling disabled also exits normally. Raw-frame
statistics and settings for21latest runs are audited in `release-native-raw-audit.json`;
current source/binary hashes are in `gpu-current-source-manifest.json`.

A separate detailed profile of this image (`release-diagnostic/stage-summary.json`)
contains638log samples including preparation, sampled once per60frames. Medians:
frame-fence wait58.3µs, image acquisition0.5µs, upload3.3µs, command recording34.0µs,
submission11.2µs, present49.8µs and GPU143.456µs. Its FPS is excluded from the final
comparison. CPU/GPU execution overlaps; summing these stages does not yield a frame
deadline. Remaining costs are GPU execution/synchronization and ordinary UI transitions.

Compute runs on the graphics queue. Asynchronous overlap on a separate compute queue
is not implemented. There is no per-frame GPU-to-CPU state readback path, and none was
added just for performance comparison. Test-only readback checks correctness. Continuous
collision detection, general rigid-body physics and other GPU vendors are outside the
verified implementation scope.

Executable path at the2026-11-03measurement:
`samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe`.
The default-execution check and21repeated measurements used the SHA256 above.

## Earlier CPU optimization executable

The earlier CPU-stage executable occupied `samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe`,
SHA256 `FA7EA73F1FFDA7396786CFC5C328351E4A31C4CE6C9967A43DDCBD77185666A5`.
Three independent runs after correcting the paused-retention contract (`smart-canvas-profiled`):

| Setting | Median FPS | median / p95 / p99 ms | 1% low FPS |
| --- | ---: | --- | ---: |
| Moving, dynamic canvas | 1020.809 | 0.90790 / 1.26606 / 1.99403 | 414.442 |
| Moving, direct drawing | 1051.348 | 0.89720 / 1.14252 / 1.72872 | 450.088 |
| Paused, retained canvas | 6630.166 | 0.13600 / 0.26070 / 0.42250 | 2013.995 |

All three moving-canvas averages exceed1000FPS; this is not a per-frame1ms guarantee.
Moving and paused workloads differ and are not used to calculate an improvement ratio.
Moving canvas:53draw calls, approximately108µsGPU,685592dynamic bytes/frame and3316UI
allocated bytes/frame. Paused dynamic upload falls to8952bytes/frame with retained canvas
geometry; remaining transfers include changing UI. Moving UI allocation exceeds the
baseline's approximately2707bytes/frame and remains an optimization target.

The runner recomputes median/p95/p99/1%low from raw intervals in addition to averageFPS.
`raw-statistics-audit.json` checks30baseline/intermediate/current runs, including actual
window and canvas sizes. `diagnostic-profiled` separately records CPU rendering stages
and is excluded from the finalFPS comparison.

Across44diagnostic logs, median upload was35.9µs, command recording30.2µs,
submission21.9µs,present69.55µs andGPU105.2µs. CPU andGPU work overlap;
these values are not summed into a frame time.

A separate NativeAOT ERD1200-table build using the current engine passed six
regression runs. Three independent six-second workloads per scenario yielded
1276.02FPS for camera motion and1265.15FPS for camera plus entity motion
(`erd-common-regression`). Duration and camera-path coverage differ from the earlier
eighteen-second ERD results; no direct improvement percentage is claimed.
The existing default ERD executable was not replaced.

## Intermediate measurements

SDK 11.0.100-rc.3.26522.148 / NativeAOT / Speed / VSync off. Each setting ran in a
visible window with one second of warmup and six seconds of measurement, three
independent times. These results precede the paused-canvas ContentVersion change;
they are not the final performance result for the complete current source.

| Setting | Baseline median FPS | Shared optimization candidate median FPS |
| --- | ---: | ---: |
| Render, backdrop cache on | 298.489 | 1076.373 |
| Render, backdrop cache off | 293.074 | 1001.251 |
| Display, backdrop cache on | 297.048 | 1067.128 |
| Render, backdrop cache on, dynamic canvas off | Not measured | 1094.186 |

Render+cache draw calls fell from 916 to 53. Draw calls do not scale one-for-one with
body count. CPU-generated moving vertices still upload every frame, approximately
685KB. Dynamic layers have management overhead: this record is slightly slower than
direct drawing. This is not a claim that layers are free or that enabling them alone
improved throughput. Render uses 1x MSAA and Display uses 4x; their difference is not
an engine improvement measured at identical quality.

The collision changes also correct cell sizing, candidate lookup after earlier
position corrections, and post-collision boundary escape. Trajectories are not claimed
to be bit-identical to the old implementation. Collision uses circular bounds rather
than polygon edges; this is not continuous collision detection or a validated general
rigid-body engine.

Evidence under `artifacts/ball-perf-2026-11-03/`:

- `baseline-profiled`: nine baseline runs and raw frame intervals.
- `unified-profiled`: twelve candidate runs and raw frame intervals.
- `gpu-unified-primitives`: 451 GPU checks.
- `unified-primitives-image-comparison.json`: 136/136 exact GPU image matches.
- `core-tests-bounds`: spatial queries, overlap, coincident centers, bounds and large polygon batches.

## GPU design sources

GPU spatial subdivision and collision calculation are feasible. Directly translating
the current sequential two-body mutation into parallel invocations causes concurrent
writes to the same body. Colored-cell passes or buffered accumulation/application
need a defined response algorithm and validation. Reading results back to the CPU
to regenerate vertices must include transfer and synchronization costs. The appropriate
target is GPU-resident state → spatial index → collision response → direct GPU drawing.
The implementation uses two state buffers for Jacobi accumulation/application.
It does not claim to reproduce the ordered CPU solver's execution order on the GPU.

Sources: [Khronos GPU particle compute](https://docs.vulkan.org/tutorial/latest/11_Compute_Shader.html),
[NVIDIA spatial subdivision and parallel collision response](https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-32-broad-phase-collision-detection-cuda).

## Reproduction

The common `run-fba.ps1` integrates `scripts/BuildArtifactLifetime.ps1` for NativeAOT
output ownership and a maximum of three retained outputs. Variants within one sample
output root are conservatively counted together. Before starting another output it
reclaims only completed, unchanged, inactive outputs after checking their full file
snapshot and absolute path boundary. Unregistered legacy outputs count toward the
limit but are never deleted. Failed, cancelled, running and user-modified outputs are
protected; if protected outputs exhaust capacity, the build fails before publication.
Ownership, cleanup plans and reclaimed bytes remain in `.build-records`. A named mutex
serializes ownership changes. Thirteen focused cases cover success, failure, cancellation,
active images, path escape, retry, junctions, changed hashes and legacy ownership; test
binaries were removed after retaining results/hashes. Previously rejected baseline
deletions are outside this cleanup root and were not retried.

Keep the repository's pinned SDK unchanged. Run from the .NET 11 selector directory:

```powershell
Set-Location tests/runtime-net11
$build = @{
    Path = 'W:/MyWorks/Duxel/samples/fba/Duxel_perf_test_fba.cs'
    DotnetPath = 'C:/Program Files/dotnet/dotnet.exe'
    TargetFramework = 'net11.0-windows7.0'
    NoCache = $true
    NoLaunch = $true
    ExtraArgs = @('-p:OptimizationPreference=Speed')
}
& ../../run-fba.ps1 @build
```

For measurement, supply Executable and OutDir to `scripts/measure-polygon-performance.ps1`.
Do not run other performance samples or GPU tests simultaneously. The default is
three independent runs. Cases accepts `render-cache`, `render-uncached`, `display-cache`
and `render-direct`. FPS measures UI frame-start intervals, not physical display refresh.

Reproduce the latest settings from the repository root. CPU/GPU comparisons use three
independent runs per mode with alternating order. Choose a fresh output directory.

```powershell
$measure = @{
    Executable = 'W:/MyWorks/Duxel/samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe'
    OutDir = 'W:/MyWorks/Duxel/artifacts/polygon-reproduction-gpu'
    Simulation = 'gpu' # cpu-jacobi: CPU reference with the same algorithm
    Runs = 3
    Count = 5000
    Size = 6
    Seconds = 6
    Cases = @('render-cache', 'render-uncached', 'display-cache', 'render-direct', 'render-paused')
}
& ./scripts/measure-polygon-performance.ps1 @measure
```
