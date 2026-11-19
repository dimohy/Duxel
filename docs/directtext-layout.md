# DirectText layout and lazy rasterization

## Retained text maintenance — 2026-11-06

When a static layer pinned more than512 text keys,maintenance allocated a full-cache eviction array every frame despite zero eligible candidates. The common context now remembers an exact all-keys-retained proof and invalidates it on cache membership or layer retention changes. Equal dictionary counts are insufficient. Eviction scratch uses actual candidate counts,and the comparer is shared. Limits,expiry,eviction priority and generated widget wiring remain unchanged.

SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148,win-x64 NativeAOT Speed,2048 actual Windows text labels,font14,320x240,one static layer,96 warm frames,1000 measured frames,three independent processes per variant in AB/BA/AB order,no forced GC. Medians across runs:

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Allocated B/Render | 98,732.768 | 256 | -99.74% |
| Median Render µs | 377 | 0.7 | -99.81% |
| p95 Render µs | 653.2 | 0.8 | -99.88% |
| p99 Render µs | 901.2 | 1.2 | -99.87% |
| Post-phase Private B | 21,004,288 | 31,891,456 | +51.83% |

Allocation/timing run ranges do not overlap. Only `UiContext.Render` is measured;`NewFrame`,`GetDrawData` and pixel ownership release are outside the phase. These are not whole-frame latency,FPS or GPU timings. Higher post-phase Private remains an adverse observation. Baseline had29..30 collections in each generation;candidate had0. Natural GC differences may explain part of this,but neither the cause nor long-run memory stability is established. Whole-app Private/working-set,GPU residency,upload and draw qualification remain open.

Every run preserves2048 cache/pinned entries and one content build,checks new unretained eviction,dependency replacement,layer removal restoring512 entries and602 idle frames expiring all. Final Native35 focused checks include existing594 Windows metric combinations,600 nested GPU textures through replay/change/parent-child removal,and10000-button MSAA4 exact pixels across five scenes with three frame slots. GPU readback is not desktop capture or user acceptance.

Evidence:`artifacts/retained-text-maintenance-2026-11-06/verification.json`,`comparison.json`,`paired-summary.json`,before/after source copies. Raw logs/executables:`L:/Cache/Duxel/retained-text-maintenance-2026-11-06/`. Final Native SHA256:`A47882ECA51BEDCCC84C63E1E5A8E40DBD7AD0985E8565508FF5A0E0AC4B29B4`. Commands:`Duxel.Graphics.Tests.exe <output> retained-text-maintenance` and `<output> directtext-cache 3`. Development validation uses.NET11 only;NET9/10 deployment compilation is deferred to whole-goal completion.

2026-11-05. A focused common-engine improvement; this does not complete Fluent2 or whole-application optimization.

The optional `IPlatformTextLayoutBackend` extends the existing text backend with exact `TryLayout` metrics and `TextLayoutVersion`. Layout and raster metrics must match for the same request. Increment the version when identical requests can produce different results; immutable providers may return0. Windows uses the same DirectWrite glyph-run analysis through bounds selection, creating alpha/RGBA pixels only for visible text.

The common `UiWidget` owns at most one metrics memo per scoped widget. Default Fluent buttons and user-derived buttons share this implementation. Keys include exact float size, text, font paths, backend identity and version. Labels are limited to256 characters and all retained strings to1024; oversized requests clear prior strings. Backend references are weak, disposal releases storage, and backend/version changes invalidate retained static layers. Input, focus, behavior, layout and paint calls still execute.

After clipping, visible text is rasterized lazily. The shared512-entry cache evicts unuploaded entries before resident textures. Inconsistent layout/raster metrics fail explicitly. Source-generated static widget calls remain intact; no runtime theme lookup is introduced.

## Matched measurement

SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, win-x64 NativeAOT Speed, actual Segoe UI14,10000 distinct unchanged labels,1280x720 CPU construction. Three independent processes per variant, AB/BA/AB, identical executable and font. Both variants use current exact keys and protected texture eviction; this is not a literal comparison against an old release.

| Metric | Eager | Lazy |
| --- | ---: | ---: |
| CPU median |79.0671ms|2.07605ms|
| CPU p95 |96.8242ms|2.113125ms|
| Allocated bytes/frame |86,779,292.31|664|
| Post-run Private median |101,425,152B|36,364,288B|
| Post-run Working Set median |110,657,536B|45,838,336B|

CPU median decreased97.37%; Private decreased64.15%. Three-run ranges do not overlap. Both paths emit462 draw-data commands; lazy performs zero layout/raster calls during measurement. These are CPU construction results, not application FPS, GPU throughput, long-duration memory or Raspberry Pi performance. Evidence: `artifacts/directtext-layout-2026-11-05/comparison-versioned.json`, `benchmark-versioned-execution.json` and raw CSVs.

## Validation

.NET10 runtime10.0.9 passed all1035 checks. The19 text checks include594 actual Windows metric combinations, label/font/provider/version invalidation, disposal, weak references, clipping and static layers. GPU183 checks passed managed and pinned RC3 NativeAOT core/EXT with1/2/3 frame slots. A prior executable was rerun at current DPI:157 prior images and942 current Native images match byte-for-byte. Half the10000 scale-test buttons derive from the default button. Five phases (idle,retained idle,scroll,partial text change,full text change) match in35 eager/lazy pairs and60 cross-runtime images. Actual GPU viewport1600x900 at scale1.25 differs from the CPU benchmark1280x720. Old96-DPI images were not compared against current120-DPI images. See `pixel-comparison.json`.

The separate particle contact defect discovered by full regression is documented in [particle contact precision](particle-contact-precision.md). Full component/theme coverage, Linux/ARM64, whole-app memory and WinUI-like resize responsiveness remain separate acceptance work.

DirectWrite references: [GetAlphaTextureBounds](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwriteglyphrunanalysis-getalphatexturebounds), [CreateAlphaTexture](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwriteglyphrunanalysis-createalphatexture).


The final production Native host also passed183 checks and157 additional image comparisons. At120DPI, resize input regression uses the actual button rectangle and passed managed/Native3x48 size changes with button input. The earlier fixed-coordinate fixture failure and correction remain recorded in verification.json. WinUI-like subjective responsiveness awaits user review.

## Current Fluent font contract requalification (2026-11-06)

The existing CPU benchmark had stopped at the first frame because its adapter lacked the new font-metrics contract and its context had no typography face configuration. The harness now gives both adapters the same regular/semibold/bold faces, forwards font metrics and records the actual compiled Semibold14/20 profile. Production engine code was not changed for this repair.

One pinned RC3 NativeAOT executable ran eager/lazy, lazy/eager, eager/lazy in six independent processes. Current-scene medians: CPU78.9842→1.9952ms (-97.4739%), p95 100.52194→2.06032ms, p99 108.633964→2.089660ms; allocation88927336→664B/iteration; Private77451264→36249600B (-53.1969%); working set87511040→45772800B. Per-run ranges do not overlap. The phase performed0.5s warmup and at least1s/12 samples, with no forced GC. Eager runs have13 samples each, so this is a short focused qualification, not long-tail or long-run memory proof. Raw data and exact inputs/hashes: `artifacts/directtext-current-2026-11-06/summary.json` and `measurement-plan.json`.

The optimized path made zero layout/raster/font-metric backend calls during measurement; the reference made123344 raster calls per run. Both produced462 DrawData commands. Commands are not GPU draw calls, and CPU iteration rate is not app FPS. The normal Windows backend already exposes the optimized layout capability: this verifies the existing optimization under the current font contract, not a new production-engine speedup. Do not compare historical absolute values across font policies. .NET10 input validation is separate from the Native measurements. GPU upload/draw costs, actual high-load app Private growth and Pi performance still need their own measurements.
