# Gallery code and startup

## Gallery code and startup

Hypothesis: eagerly constructing every source viewer and showing the full gallery implementation on Overview caused unnecessary first-frame text layout. Changed to lazy page frames, no source on informational pages, cached lexical code display and native paragraph layout for code. One historical run: full atlas 503.10ms, first present 3009.41ms (old overview plain Textarea). Latest run: renderer 376.74ms, full atlas 400.55ms, first present 720.36ms. Revisions differ and there is one baseline run: improvement percentage, median/p95/p99, 1% low and fixed-RC3 NativeAOT conclusions remain unverified. Commands: ./run-gallery.ps1 -Verify -VerificationOutput <fresh directory> -NoRestore; current evidence artifacts/gallery-source-2026-11-17/verification.json. Risk: the richer Overview changes the workload; Vulkan instance startup and actual resize performance still need isolated measurement.

## Full source, resize and text reuse

Hypothesis: scrollbar probe widths thrash one layout slot and the512-entry cold budget evicts visible text. Reflow gives identical glyph pixels new paragraph identities and repeatedly frees many small GPU allocations.

Changes: two width variants, shared measurement/paint geometry, native snapshots per hard source line, exact raster identity independent of source index/integer translation, active painted working-set protection, and small text texture reuse after the existing fence retirement deadline. The pool is bounded by2,048 entries and16MiB of actual allocations, reclaimed under descriptor pressure and on shutdown. Interpolated text, delimiters, expressions, escapes, methods and operators have distinct colors; unchanged source is not reparsed.

Windows managed SDK11.0.100/runtime11.0.0 diagnostics: three1280x900 before/after processes reduced inner UI mean17.323→1.204ms, allocation6,102,916→37,887B/frame and rebuilds1536→0 across768samples. A separate full-source1654x1040 alternating3+3 comparison reduced median mean submit18.460→0.358ms and increased median presentation-count/wall-time rate47.02→243.58/s. This rate differs from the smoothed banner FPS. Source hash, scales and options match within each comparison.

Resize: identical harness DLL, explicit0.8s warmup, three alternating independent processes per variant, each3cycles×48synchronous native size commits. Aggregate phase means, then per-process median, then the3-process median:86.563→32.739ms (62.18% reduction). Earlier753–758 lack explicit viewport warmup and are excluded. The prior worst texture retirement interval was59.77ms; pooled diagnostic747 averaged0.004ms and retained1,071idle entries/2,702,848actual allocation bytes.

Risk: median maximum phase-end Private Bytes400,228,352→459,882,496B (+56.89MiB). These are neither working set nor continuous peak; managed/JIT/native contributions were not separated. Resize commits still exceed16.7ms. Actual application-frame median/p95/p99/1%low, real Linux and fixed-RC3 NativeAOT performance acceptance remain unverified. Inner UI frames.csv samples are not full-frame metrics.

Validation: code capture pixels match exactly;18native paragraph cases/5,273selection ranges match exactly. Gallery843, common4,642, text-cache35, resize pixels39 and shutdown/minimize11checks pass. From tests/managed-validation reuse publish-760/Duxel.Graphics.Tests.exe <fresh output> with gallery-resize, code-view-full-bench, observer-only, widgets-no-resize, directtext-cache, resize-coherence or session-failure-only. Evidence: artifacts/gallery-source-2026-11-17/code-layout-comparison.json, full-source-comparison.json, resize-comparison.json, text-equivalence.json and verification.json.

## Removing repeated allocations from integration source-state synchronization

- Hypothesis/scope: counters and checkbox/radio setup strings were constructed before equal-state comparison. Measure only actual IGallerySample.SynchronizeSource calls; exclude complete frames, renderer work, caller enumeration and other threads.
- Change: TabOverflowExamples/OverflowExamples read integer/boolean/selection tuples. Format setup strings only after state changes, reusing GallerySample.BindSource.
- Execution: managed-validation SDK 11.0.100 / runtime 11.0.0, Release. Same gallery-samples-only path and input sequence; publication 1020 before, 1022 after. Each fresh process warms up 1000 passes over eight sources, then measures another 1000 passes (8000 calls) with GC.GetAllocatedBytesForCurrentThread. Three independent processes per version.
- Before: 2,360,000 / 2,360,000 / 2,360,000 B. After: 0 / 0 / 0 B. Median 2,360,000 to 0 B: 100.00% fewer managed allocations within this region. Functional state/source synchronization checks also pass.
- Evidence: W:/Cache/Duxel/gallery-source-20261117/sync1021a.stdout.log, sync1021b.stdout.log, sync1021c.stdout.log and sync1023a.stdout.log, sync1023b.stdout.log, sync1023c.stdout.log; INTEGRATION_SOURCE_SYNC_ALLOCATION records. Ledger: artifacts/gallery-integration-source-2026-11-17/allocation.json.
- Limits/risks: FPS, CPU time, median/p95/p99 frame times, 1% low, total process memory and GPU/VRAM are unmeasured (N/A). Do not extrapolate to complete application speed or memory. This managed Windows diagnostic does not establish fixed RC3 NativeAOT or actual Linux performance. Changed-state formatting costs are excluded. Keep the zero-byte unchanged-state synchronization check as regression coverage.


## Measurement Gallery source synchronization (managed correctness and scoped allocation)

- Change: two actual captured sources reuse compiled MeasurementPrimitives/MeasurementForm declarations; snapshots contain values/settings and omit diagnostic measurement/progress-read counters. Original measured overrides and finite panels are retained.
- Validation: published Graphics.Tests `gallery-samples-only` runs measurement1074 and measurement1078 plus `observer-only` gallery1075; each executes 1000 warmup passes then 1000 measured passes over 2 real SynchronizeSource callbacks (2000 measured calls).
- Environment: SDK11.0.100/runtime11.0.0,managed Release. Current-thread GC allocation counter,without forced GC. Three fresh processes:0/0/0 B. Previous path had no per-example source synchronization,so Before and improvement are N/A. No frame/FPS/CPU-time/RSS/VRAM/AOT/Linux speed claim.
- Risks: excludes complete UI frames,changed-state formatting,other threads and total memory. New geometry/source layout is checked by existing measurement/input/menu/tab/tooltip contracts and actual GPU interactions.
- Evidence: [result](../../../artifacts/gallery-measurement-source-2026-11-17/result.json),[allocation](../../../artifacts/gallery-measurement-source-2026-11-17/allocation.json).

| Metric | Before | After | Improvement |
| --- | --- | --- | --- |
| Calling-thread managed bytes per 2000 unchanged source-sync calls | N/A | 0/0/0 B | N/A |

## Legacy inline source caching and child reveal coherence

Hypothesis: typed value snapshots can retain the source string and parsed UiCodeView syntax while preserving the original immediate-mode callbacks. The twelve legacy callbacks now provide their actual caller-captured bodies and explicit current initializers; Gallery owns and disposes one code widget per visited section. Repeated full-page source presentation is removed.

Validation: managed `gallery-legacy-source-only` from publish-1181, result `W:/Cache/Duxel/gallery-source-20261117/legacy1182/result.json`: 30 checks passed. Twelve initialized source bindings synchronized 128 times allocated 0 calling-thread managed bytes. Before source-sync allocation: unavailable because the former page had no equivalent section binding; improvement percentage: unavailable. This is not a whole-frame, GPU or throughput measurement.

The same finite nested-scroll test exposed a common engine defect: BeginChild published its newly requested offset after drawing at the previous position. Reveal planning then applied the same delta twice. The update now occurs before drawing descendants. Before: actual Scale remained0.5 after a failed reveal and the target overscrolled out of view; after: reveal remains visible, completed range request sets0.75, and its corresponding source follows the actual value. The real first table row changes its initializer totrue without rebuilding popup source. Native and common regression validation is recorded in the accompanying artifact result. Risk: new child content extents become known at EndChild; existing clamp and scrollbar behavior is preserved, and no application-wide performance gain is claimed.

## TagPicker popup selection publication coherence

User run20261117-094723-352-702a1ec9 terminated on duplicate canonical Kim option ID. The popup had published its old visible rows, then completion rebuilt the exclude-selected filter before hidden options were published. The selected row was therefore added a second time. Move only projection rebuilding after both publications, retaining strict uniqueness validation and canonical selection identities.

Before control: new pointer regression running against pre-fix Core1191 terminates with the same item-6B0069006D00 duplicate ID (tag1196-before.stderr.log). After: TagPicker1197 passes25checks, including two actual press/release selections and canonical closed-option uniqueness; common1198 passes4642. Full Gallery Vulkan, native Windows and Linux HOST outcomes are recorded in artifacts/gallery-tag-picker-selection-2026-11-17/result.json. No throughput, allocation or percentage performance improvement is claimed. Risk: the projection refresh now occurs after current-frame option publication; next-frame filtering and controlled selection rejection remain qualified by existing tests.

## Scrolled tab boundaries

Hypothesis: applying a remaining layout budget at an already scrolled cursor collapses parent semantic height to zero. Change: share physical content-end clipping between legacy tabs, Fluent tabs and navigation pages. Qualification: previous Core controls fail GPU visibility and actual Gallery parent-boundary checks; the fixed version passes42 GPU,31 focused Gallery,4642 common and1715 full Gallery checks. This is a geometry correction, not a measured FPS optimization. Evidence: artifacts/tab-panel-scroll-clip-2026-11-17/result.json.

## Standalone source reuse

All22 examples now share GallerySample and UiCodeView;16 interactive states regenerate code only on typed value changes. All22 unchanged bindings allocate0 managed bytes across128 synchronization repetitions. This does not measure whole-frame allocation or FPS. GPU268 checks confirm actual selection and displayed code agreement. Identical Core/Svg/Vulkan/App/Windows hashes permit reuse of the previous4642 common,42 tab GPU and31 legacy-source checks. No automatic global cache or elapsed-time improvement is claimed. Evidence: artifacts/gallery-factory-state-2026-11-17/coverage-and-reuse.json.

## Provider scope and measurement identity

Geometry token values now participate in layout identity to prevent reuse across differently configured subtrees. Theme/token restoration and warm measurement retention are checked. Qualification:21 focused Provider checks plus1 corrected retention case;12 MSAA1/4 GPU;4642 common;4114 Tooltip/input;270 Gallery GPU. Passing results with identical Core are reused after fixture-only edits. No FPS or timing improvement is claimed. Evidence: artifacts/fluent-provider-2026-11-17/result.json.


Provider font scope: native UiTypographyFonts inheritance/restoration, full-role layout identity and deferred Tooltip capture; no global invalidation on local entry. Focused CPU9, live Gallery3, font GPU6 (MSAA1/4), system typography38, base Provider GPU12, common4642 and Tooltip/input4114 pass. Linux HOST build only; no actual Linux or pinned RC3 run. publications1244/1249 share Core SHA256; earlier checks are reused, not rerun. No FPS or startup improvement is claimed.


Shimmer reuses Skeleton, UiFloatTransition and UiPresencePaint, skipping hidden content and stopping frame requests after loading. CPU14/GPU8/catalog4/GalleryGPU270 and both HOST builds pass. Shared engine sources are unchanged; prior common4642/Tooltip4114/font suites are not repeated. No measured FPS improvement is claimed.


Windows close response: phase observations confirmed visible HWND retention during teardown. Hiding accepted close while preserving destruction ordering reduced median hidden response116.94→13.77ms(88.22%) and observed process exit130.91→82.80ms across3independent runs per side. Vulkan teardown dominated the sampled baseline. Scope: default Gallery/Win32 visible state/.NET11managed; no general driver/heavy-page cleanup claim. See windows-close-response.md and artifacts/gallery-close-latency-2026-11-17/result.json. Module hashes match except Windows platform. Unchanged widget suites were not repeated.


ProgressIndicator reuses existing progress interpolation, indeterminate painting and text layout. PassingCPU14/GPU8 are reused on identical Core hashes; only the incorrectly frozen reduced-motion expectation was corrected against current official opacity motion. FocusedCPU1/additionalGPU4/catalog4/GalleryGPU270 and bothHOSTbuilds pass. common4642/Tooltip4114/closeA-B were not repeated. No speed claim.


ActivityIndicator reuses common arcs/circles/clipping/frame scheduling without a new timer. CPU14/GPU12/catalog4/GalleryGPU270 and both final HOST builds pass. A later unsupported-palette guard preserves supported light/dark values; those cases and unchanged close/widget suites were not repeated. No FPS improvement is claimed.


## HeadsUpDisplay content

HUD content reuses prepared bounded text/size through the shared measurement key and UiRetainedLayout. Scaled typography is additive and preserves the bool signature. CPU17/Vulkan14/Gallery3 cases qualify behavior. Performance Before/After and improvement rates are unmeasured, not claimed. Portal integration and real-platform comparison remain open. Evidence: artifacts/fluent-heads-up-display-2026-11-17/result.json.


## HUD portal and calendar model

HUD portals reuse common overlays, focus, exit paint and frame deadlines. HUD64/sharedDialog107/catalog4 cases qualify behavior. A constructor-name predicate was corrected with only its2affected source cases rerun. Identical Core publications and unrelated checks are reused. Calendar snapshots locale data and prepares month grids;20model cases pass. No performance Before/After or improvement rate is claimed. ActualLinux/RC3NativeAOT/CalendarUI remain open.
