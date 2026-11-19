# Common retained input IDs at10,000 buttons

Last synced: 2026-11-05

## Latest: transient object allocation regression resolved

On2026-11-05 identity cache storage became lazy at the first scoped render. UiWidget's existing identity reference holds a string token or null at construction, then owned cache storage after scoped rendering. This is input-identity lifetime state, not runtime theme selection. Generated concrete behavior/presentation binding remains unchanged. Unowned Parent factories create no cache object.

.NET10 allocation for1,024 factories is122,880→98,304B(-20%), restoring the pre-cache level and remaining25% below the old131,072B delegate factory. The25% budget now has a focused regression check. Full802 checks, generator64 checks and net8/9/10 builds pass with zero warnings/errors;323 existing images match the preceding version.

The same CPU probe compared original, three-inline-field and lazy variants in three independent processes each. Orders: original/inline/lazy, lazy/inline/original, original/lazy/inline; original/final ordering is AB/BA/AB. SDK/runtime/RID/Speed/workload/clip/glyph fixture and warmup/measurement durations match the setup below. Final allocation stays664B/iteration in every case.

| Case | Original→final CPU median ms | Reduction | Inline→lazy end Private MiB |
|---|---:|---:|---:|
| idle | 3.5012 → 3.0672 | 12.40% | 15.27 → 15.75 |
| scroll | 3.5261 → 3.0569 | 13.31% | 23.50 → 24.18 |
| partial | 3.5191 → 3.0761 | 12.59% | 31.70 → 32.63 |
| full | 3.5223 → 3.0760 | 12.67% | 39.89 → 41.07 |

Original/final median ranges do not overlap. All inline/lazy CPU ranges overlap, so no performance change is established against the preceding version. Original/final idle/scroll p95/p99 improve without overlap; partial/full p95/p99 overlap. Worsened central values are preserved in comparison.json.

**A residency tradeoff remains.** Removing24B from the widget body requires one48B cache object when a retained scope first renders: body+cache is24B larger than the preceding inline version. End Private increased approximately0.47–1.18MiB versus inline across these phases, and working set increased3.42–4.42%, with nonoverlapping ranges. Fixing transient allocation does not prove an overall memory reduction. Final end Private remains below the original pre-cache implementation in all cases, but these short sequential observations may contain uncollected objects from prior phases. Long-duration, peak and whole-app validation remain open.

Current evidence: artifacts/widget-identity-storage-2026-11-05/comparison.json, verification.json, runs.jsonl, full-net10 and image-comparison.json. The following sections preserve the historical three-inline-field candidate.

This improves retained builtin/user-widget input ID construction. Generated factories bind one concrete implementation type;10,000 instances do not require10,000 generated classes. It does not establish complete Fluent fidelity, DirectText, GPU uploads/draws, application FPS or Pi performance.

## Cause and implementation

The existing shared ID cache admits4,096 entries and1,048,576 characters. Traversing10,000 retained buttons regenerated IDs beyond admission on every frame. UiWidget now owns its current parent prefix, resolved scope and primary input ID. Builtins and user subclasses share this path. Parent changes replace the previous cache; disposal clears it. Additional arbitrary PushID scopes retain existing resolution semantics.

Each widget admits at most4,096 characters including retained input references. Oversized paths remain correct without widget-owned admission. The shared limits remain unchanged. Input/callbacks execute every frame; layout, paint, vector quality and workload were not reduced.

## Fixed RC3 NativeAOT CPU comparison

The existing Performance.Bench/BenchFrameRecorder gained --widgets. Typed result DTOs and a source-generated JsonSerializerContext replace reflection-based benchmark serialization. Initial Native publication exposed IL2026/IL3050 warnings; that artifact was excluded from measurements and the recording paths were corrected.

SDK11.0.100-rc.3.26522.148, runtime11.0.0-rc.3.26522.148, win-x64, NativeAOT Speed. Both Core assemblies target net10.0; original Core/generator sources are preserved separately. Conditions match: atlas glyph fixture,1600×1000 clip,10,000 buttons,0.5s warmup and1s measurement. All10 columns×1,000 rows execute, including offscreen widgets. Partial changes100 labels; full changes10,000 labels using prebuilt equal-length strings; scrolling changes position. Three independent processes per variant run AB/BA/AB without forced GC.

| Case | CPU median ms before→after | Reduction | Allocated B/iteration before→after | End Private MiB before→after |
|---|---:|---:|---:|---:|
| idle | 3.4985 → 3.0398 | 13.11% | 378528 → 664 | 36.26 → 15.67 |
| scroll | 3.5061 → 3.0379 | 13.36% | 425752 → 664 | 36.90 → 23.89 |
| partial | 3.5407 → 3.0963 | 12.55% | 425752 → 664 | 35.64 → 32.09 |
| full | 3.5600 → 3.0569 | 14.13% | 425752 → 664 | 46.39 → 40.29 |

Median, allocation, end Private and working-set ranges do not overlap across the three runs. Idle/scroll p95 ranges do not overlap; partial/full p95 overlap and remain inconclusive. Partial p99 is also inconclusive. Raw samples, all metrics and ranges are in artifacts/widget-scale-2026-11-05/comparison-final.json and final-*.csv. Both variants produce562 CPU draw-data commands; this is not a GPU draw-call count.

Private values are end-of-phase observations. Four phases run sequentially within a process and may include uncollected objects from earlier phases. They do not prove long-duration stability, peak memory, post-disposal residency or whole-application memory reduction.

## Verification, costs and remaining work

.NET10 full801 checks, generator64 checks, and net8/9/10 solution builds pass without warnings/errors. Checks cover10,000 distinct IDs with equal labels, reuse of every ID string, parent changes, oversized-path admission and exactly one callback for the final button despite renaming during a press.323 existing output images match before/after final admission changes; this is not an official Fluent visual comparison. The existing RC3 full GPU particle mismatch remains unresolved and was not rerun unnecessarily.

Three added reference fields cost24B/widget on measured .NET10 x64. Creating1,024 transient factory objects increased from98,304B to122,880B(+25%). It remains6.25% below the old delegate factory's131,072B, but is a real cost of this change, separate from steady retained rendering. Resolving this regression remains open. Native diagnostic executable size is3,351,552→3,352,576B(+1,024B).

Next work covers transient factory overhead, DirectText/layout/paint reuse and actual application NativeAOT GPU/memory/long-duration validation. The complete objective and unfinished axes remain active. Previous binaries and rejected cleanup targets are preserved.
