# Widget CPU reuse — work in progress

[한국어](widget-cpu-retention.ko.md)

## Shared layout and geometry reuse · 2026-11-19

On Windows .NET 11 managed, a Wrap and a Grid each held 10,000 children. After warmup, 1,000 measurements under unchanged constraints made zero child measurements and allocated zero bytes within the measured section. A separate test with 1,000 controls per family also recorded zero repeat child measurements for Checkbox, Slider and RadioGroup.

Changes to child size and state invalidate prepared measurements. All 597 ordered-input checks passed. With one and three frame slots, processing the full input sequence and drawing once matched sequential full-repaint pixels. Atlas text tests separately establish geometry reuse and cache lifetime. The measurements cover the prepared layout and fixture scenes. Full-application frame time and memory are separate criteria. Evidence is under `L:/Cache/Duxel/cpu-reuse-2134/verification.json`.

## Cache budgets and queued-frame lifetime · 2026-11-19

The Windows .NET 11 atlas text test sent 900 distinct requests while enforcing 256 entries and a 256 KiB geometry budget. Held frame vertices survived eviction and scratch reuse. The DirectWrite tests held texture references for retained layers and restored the ordinary cache bound after release.

Vulkan checks used one and three frame slots for SVG, images, rounded images and vector-cache lifetime. The container and widget tests also verified layout invalidation and ownership release. High-load process memory growth remains a separate measurement. Evidence is under `L:/Cache/Duxel/cache-lifetime-2133/verification.json`.

## Shared widget storage,2026-11-06

Generated containers and inherited user widgets share UiBuildContext. Dependency dictionaries and build rollback/removal lists now allocate only when used. Disposal drops their backing storage references. A direct widget stores its frame callback in the same reference slot that holds the Build context once composition/ownership is needed; that context then owns the callback. This reduces the base widget footprint without adding per-widget wrapper objects. Static generated layout/presentation selection is preserved.

Fixed .NET11 RC3 NativeAOT Speed allocation probes use10000 generated Container ownership constructions and10000 user-widget first Builds,96 warm frames,three independent processes per variant in AB/BA/AB order and no forced GC. Exact per-run byte counts agree:

| Current-thread allocated bytes | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Container ownership construction | 7,840,000 | 6,000,000 | 23.47% |
| First retained Build | 3,360,000 | 880,000 | 73.81% |
| Subsequent unchanged Build traversal | 0 | 0 | unchanged |

Both variants use parent-scoped identity. The ownership case has one child; the first-Build case has no watched state or children. This isolates unused shared storage rather than representing every widget workload.

All10000 Builds still execute once and children dispose exactly once. Preallocated arrays are excluded; widget objects are excluded from the first-Build phase. This is allocation evidence,not whole-application Private/working-set,FPS,draw-call,latency or Pi improvement. Callback storage adds a type check on render/invalidation paths; CPU timing remains unmeasured. The existing25-percent factory allocation criterion was preserved and passes the .NET11 managed tree regression. Late child ownership and callback replacement have focused coverage. Evidence and executable hashes: `artifacts/build-context-memory-2026-11-06/`. Window resize work is deferred by explicit user request.

2026-11-05 follow-up: [10,000 retained buttons](widget-scale.md) adds common input-ID reuse.
It is separate from the atlas geometry work below and does not complete DirectText or full layout/paint reuse.

The additional optimization goal remains active. This iteration implements common atlas glyph-layout/geometry reuse and corrects measurement-cache identity,lifetime and callback behavior. It does not skip whole DirectText widgets,cursor transitions,user code or every paint operation. Overall memory reduction and Pi execution support remain incomplete.

## Consumption paths and contract

`artifacts/widget-retention-2026-11-04/widget-sources.txt` lists20 built-in widget source files; `cpu-layout-consumers.txt` records shared measurement,text and cursor call sites.

| Work | Common path | Contract |
|---|---|---|
| Text dimensions in buttons,menus,input,sliders,trees,tabs,etc. | MeasureTextInternal → DirectText or UiTextBuilder.MeasureText | Reuse pure dimensions for identical font/settings/line-height/text; preserve missing-glyph notifications |
| Atlas glyph placement,quad/index/bounds | AddTextInternal → UiDrawListBuilder.AddText | Compare actual atlas,text,position,color,texture,clip,settings and nullable line-height; regenerate on changes |
| Default DirectText | Cached raster/atlas entry → AddImage | Existing path retained; not covered by the new glyph-run cache |
| 3D vector text | Mesh cache → RenderVectorText | Existing vector quality retained; not replaced with atlas images |
| Cursor,columns,tables,last-item state | AdvanceCursor and layout methods | Execute each call; preserve ordering,scroll,resize and state transitions |
| Input,focus,active IDs,popups,user callbacks | Widget behavior and caller | Execute each frame; do not suppress UiScreen.Render because pixels are unchanged |
| Backgrounds,borders,analytic primitives | Existing designed-frame/draw builder | Preserve vector geometry and damage handling; further CPU paint reuse remains |
| Resident geometry and acquired-image damage | Presentation snapshots → Vulkan damage | Existing queue lifetime and upload/draw skipping remain; distinct from CPU reuse |

Pure text layout is reusable while immediate-mode state transitions remain active. Further work follows measured cost. No sample-specific branch or unsupported-feature backend substitution was added.

## Implementation

`UiDrawListBuilder.TextRuns.cs` retains owned atlas geometry after a second identical request; the first request admits metadata only. Subsequent hits skip glyph layout,kerning,snap and clip computation,copy immutable geometry into current scratch,and apply current command user data. Original estimated-character flush boundaries and command merging remain. Queued frames do not borrow mutable cache arrays.

Limits per builder:256 entries,256KiB geometry and256 UTF-16 code units per request. Keys,strings and object overhead are additional but bounded by entry/text counts. Entries unused for120frames are removed at60frame intervals; `_ClearFreeMemory` releases ownership. Longer text and observer-bearing requests execute the original generator every time.

Measurement uses a per-atlas ConditionalWeakTable,preventing object-hash aliasing and retirement leaks from strongly retaining large atlases. Each font retains at most512 short measurements; access is synchronized. MissingGlyphObserver is never memoized away,and null line-height differs from explicit-1.

## Validation and measurement

.NET10.0.9 and pinned RC3 runtime11.0.0-rc.3.26522.148 each pass731 final full checks. The focused17 checks include108 kerning,Unicode,clip,scale,snap and baseline combinations over three frames,comparing vertex bytes,indices and commands against the original generator. Additional checks cover current user data,observer callbacks,font separation and collection,nullable line-height,900 changing strings,bounded geometry,queued-frame independence,idle eviction and explicit release. Value-type cache metadata adds zero current-thread bytes over600 one-use admissions/evictions after warmup. Release net8/net9/net10 solution build has zero warnings/errors.

The `text-probe` is a CPU diagnostic only: pinned SDK11.0.100-rc.3.26522.148 NativeAOT Speed,same executable,32×64characters,same font/position/clip/color,three independent repetitions per path in AB/BA/AB order,at least1second/96warmup iterations and2seconds measured. No forced GC. Units are CPU construction iterations/second,not application or Pi FPS. See `text-cpu-summary.json` and raw records. Steady-state geometry retains212,992B in32entries. This does not prove overall memory reduction or DirectText application gains.

## Evidence and continuation

Final isolated CPU medians:29,371.79→132,492.16iterations/s(+351.09%),median0.0336→0.0074ms(-77.98%),p950.0360→0.0080ms(-77.78%),p990.039857→0.0096ms(-75.91%). All throughput/timing ranges are nonoverlapping. Observed allocation440.02→440.00B/iteration overlaps and is inconclusive. This is not overall application FPS or memory improvement.

Slowest1% iteration throughput is15,249.12→55,760.01iterations/s(+265.66%),with nonoverlapping run ranges. It is calculated from the mean duration of the slowest ceiling(N×1%) raw samples.

`artifacts/widget-retention-2026-11-04/` contains `plan.json`,`baseline-edit-manifest.json`,`complete-final-net10/`,`complete-final-net11/`,`build-final-net10-repo.stdout.log`,`text-cpu-final/summary.json` and `iteration-audit.json`. Initial reference-type results,executable and source snapshots remain preserved.240 existing source files match the prior final hashes;three have intentional edits,and two sources are new. A .NET10 command run from the RC3-only working directory failed SDK selection and was corrected by using the repository directory,with logs retained and no repository SDK changes.

Next,profile and extend common reuse for default DirectText,nontext paint and container/cursor layout,then validate overall high-load performance and Private/working-set/residency including added cache memory. Whole-application NativeAOT comparison remains outstanding. Pi GPU feature mismatch and absent Linux platform implementations follow the [separate analysis](raspberry-pi-simulation.md).


## DirectText layout/cache update — 2026-11-05

Optional `IPlatformTextLayoutBackend` and revision contracts, a single widget-owned metrics memo and rasterization after clipping now share the common widget path. Backend/revision changes invalidate static layers.10000-button CPU median79.0671 to2.07605ms is not application FPS or Pi performance. See [implementation,matched measurement,lifetime and GPU validation](directtext-layout.md) and [separate particle precision correction](particle-contact-precision.md).

The DirectText harness now honors current Fluent Semibold14/20 and passed three alternating independent runs per variant on fixed RC3. CPU78.9842→1.9952ms and Private77451264→36249600B compare existing capabilities under the same current font policy, not a new engine patch/GPU/app FPS improvement. [Conditions and limits](directtext-layout.md).

## Shared widget Build storage — 2026-11-06

The common UiWidget path now validates ownership against its existing keyed child store and reuses the existing identity slot for parent-scoped keys. Dependency and child cleanup lists live on their existing dictionary objects. This removes an8-byte widget field and24 bytes from dependency-free Build contexts without adding wrapper allocations. Built-in/generated and user-derived widgets use the same path; state subscriptions, failed-Build cleanup, identity, input, disposal and layout invalidation remain active.

Pinned.NET11 RC3 NativeAOT, three independent runs per side in ABBAAB order for each of three modes (18 processes):10000 generated owned Containers allocate6,080,000→5,760,000B during construction(-5.26%) and880,000→640,000B for their first Build(-27.27%).10000 distinct watched roots allocate5,280,000→5,200,000B(-1.52%);10000 keyed-child first Builds allocate4,800,000→4,560,000B(-5%). Unchanged/rebuilt watched and unchanged owned Build phases remain0B. Root objects/states/arrays and disposal are excluded from dependency phases; child factories are included. Construction and first retained-layout payload acquisition are separate costs.

Managed and Native each pass101 tree/ownership/lifetime and175 container checks;all121 GPU images match. The existing strict25-percent factory-allocation condition is unchanged:Native114,688→81,920B for1024 unowned buttons(-28.57%). Pure retained Wrap/Grid median0.2→0.2us,p950.3→0.3,p990.3→0.3;run ranges overlap, so no speedup is established. Small-probe Private/working-set changes do not prove application or long-duration memory gains. No forced GC or workload/quality reduction. The earlier+80,000B Container construction cost is exceeded by this reduction; historical results remain preserved.

Evidence:[verification](../artifacts/build-store-2026-11-06/verification.json),[comparison](../artifacts/build-store-2026-11-06/comparison.json). Raw runs and initial failed probes remain under L:/Cache/Duxel/build-store-2026-11-06/. Full Fluent2,remaining container contracts,high-load memory and portability remain open;resize remains deferred. Development checks use.NET11 only;.NET9/10 deployment compilation waits for whole-goal completion.

## Stateful leaf measurement participation — 2026-11-12

Checkbox/Switch/ToggleButton, Slider and RadioGroup now allow their existing parent layout to retain deterministic measurement. This reuses UiRetainedLayout and the ownership invalidation chain; it adds no per-leaf cache or storage field. Rendering, input, binding reads and accessibility publication still run. Values do not determine these built-in controls' desired size; Label/Size/Options/SetItems mutations call RequestRebuild and invalidate ancestor arrangements. Constraints, text/resource keys and glyph-observer bypass remain authoritative.

IUiTogglePresentation and IUiSliderPresentation expose static virtual CanRetainMeasurement, default false. Known Fluent implementations opt in. Generic Checkbox/Switch presentations require both size and label-position traits to opt in; arbitrary user traits default false. IUiRadioGroupLayout likewise defaults false, and UiFluentRadioGroupPresentation<TLayout>.ReuseLayout now follows that promise instead of unconditionally caching a mutable custom layout. UiRadioGroup propagates the existing presentation ReuseLayout promise to its parent. Derived controls with external mutable measurement must override CanRetainMeasurement to false or invalidate correctly. Dynamic text, Badge, Progress, Input, Spinner and other families are not automatically opted in.

Focused14 and containers179 cover pure/reused measurement, continued rendering, label/size/items/constraint/text-scale invalidation, glyph observers and mutable custom Toggle/Slider/Checkbox-size/Switch-size/Radio-layout implementations. Existing Radio input/allocation91, semantic Radio18, Toggle21, range44, accessibility49, scheduling35 and actual Observer142 pass. GPU28 compare retained/bypassed parents at five value/property/allocation/removal phases, MSAA1/4 and three frame slots, with four acquired-image replays per phase. ObserverGPU50 includes the new page; all73 earlier comparable bodies match belowY128. Sample/Graphics/generator builds have zero warnings/errors; generators115 pass. This is .NET11.0.0 managed validation, not fixedRC3 NativeAOT.

A bounded diagnostic uses1000 actual controls per family, a10-column Grid with80px row extent,4000x30000 constraints,96 warmup frames and256 samples. Six independent processes run AB/BA/AB using immutable binary snapshots. Each baseline run measured children768000 times; each retained run0 times. Both allocate67584 bytes across the measured context-render phase. Raw median microseconds are baseline922.5/931.1/905.4 and retained1.5/1.4/1.5. The timed phase only renders a measurement-only screen; it is not the whole application's render cost, CPU time, FPS, Private/working-set measurement or NativeAOT acceptance. No final performance percentage is claimed. The earlier full highload workload was not rerun or reduced. See artifacts/leaf-layout-retention-2026-11-11/verification.json and diagnostic-summary.json.

Run ./run-gallery.ps1 and choose Measurement reuse (측정 재사용). Changing values keeps the displayed child-measurement count; Label/Width buttons trigger measurement. State/cache survive tab switches. The display requests no periodic frame or animation. Original full CPU/lifetime/load, containers, NativeAOT/runtime-image and portability gates remain active.

## Content and visibility contracts — 2026-11-12 follow-up

Text now requires both IUiTextSource.CanRetainMeasurement and IUiTextPresentation.CanRetainMeasurement. Static strings opt in; getter strings remain dynamic. Known context/Fluent presentations opt in. Generic typography also requires its IUiTypographyTypeface and IUiTypographyMetrics traits; arbitrary custom selectors/metrics default false. Link propagates its existing presentation ReuseLayout promise. None of these changes skips rendering, input, navigation or current source reads needed for painting.

Badge requires a deterministic behavior and presentation, and a generic icon presentation also requires IUiBadgeIcon's explicit promise. Built-in string and immutable CounterBadge source policies opt in, as does the no-icon presentation; getter and arbitrary custom behaviors/icons stay dynamic. Source/options mutations invalidate the owned parent through the existing RequestRebuild chain, including hidden-zero CounterBadge becoming visible.

Progress requires both its source measurement policy and presentation to opt in. Getter Progress retains only its declared Size/tight-parent geometry; it still reads/validates the getter once per Render. Missing explicit height still fails rather than reading the getter during measurement. Static source values can affect thickness, so Source changes conservatively invalidate layout. Dynamic Text/Badge are not classified like getter Progress: their returned content affects their desired size.

Spinner requires a deterministic presentation and a behavior promise that visibility cannot change without options changing. The default behavior permits retention with zero delay or after it has actually become visible. Before delayed reveal it stays dynamic and schedules the original deadline. Evaluate/painting run even with retained geometry: animated Spinner still requests frames, and Paused clears animation scheduling. Custom visibility behaviors default false. Derived controls overriding ReadText/ReadContent/measurement with external mutable data must disable inherited CanRetainMeasurement or invalidate correctly.

No per-leaf cache, storage field, runtime type registry or reflection was added. Focused26 cover pure measurement, real getter frequency, actual Link pointer callbacks, source/size/width/environment invalidation, dynamic source/typeface/metrics/icon/behavior protection, delayed Spinner reveal, continued animation and pausing. Existing allocation/input checks text47/badge73/link61/progress60/spinner186, containers179, priorleaf14, scheduling35 and actual Observer145 pass. Generators115 pass; their strict leading-declaration removal cache gate remains false.

ContentGPU28 compares6real paths (Text,Link,Progress,string Badge,CounterBadge,paused Spinner) with explicit measurement-bypassed references at5value/source/allocation/hiding phases,MSAA1/4,3frame slots and4replays. Previous leafGPU28 passes the shared comparison loop, with all121 previous PPM files byte-identical. ObserverGPU50 passes:73 older body captures remain exact belowY128; the8measurement-page captures intentionally change to show8controls. The actual screenshot was viewed. Graphics/generator/sample builds have zero warnings/errors, SDK11.0.100/runtime11.0.0 managed. No new timing,allocation,Private/working-set,FPS or NativeAOT performance comparison was run. Previous bounded leaf timing remains historical, not proof for this newer Core binary.

Run ./run-gallery.ps1 → Measurement reuse (측정 재사용). It now includes static body,Checkbox,Slider,RadioGroup,Link,getter Progress,CounterBadge and paused Spinner. Slider changes still update Progress without increasing measurement count; Link's actual pointer callback updates its counter; Body update changes text/badge/spinner labels and remeasures. The display itself schedules no periodic frame. Link semantic UIA remains a separate unimplemented scope; the test uses its real rendered pointer bounds instead of fabricating a semantic node.

Original whole-app CPU/geometry/lifetime/load,container extension/integration,complete Fluent/SVG,portable and fixedRC3 NativeAOT/runtime-image gates remain open. Input has not been blanket opted in. Fixed parent dimensions alone do not authorize skipping an arbitrary non-retained child's measurement: its source validation,custom behavior and input/render contracts still need explicit qualification. Next work must reconcile the original gate/consumer matrix rather than treating these focused units as total completion.

## Form measurement participation — 2026-11-12

Input, static Divider, Menu trigger and qualifying Field now participate in the existing parent measurement cache. Input size/appearance/adornment, Divider presentation/source and Menu trigger promises default false for unknown policies. Field requires both presentation reuse and a retaining child, including when explicit control dimensions are supplied. Input slots are a distinct explicit allocation contract: their children were never measured by Input and continue rendering normally. Actual editor values are not measurement inputs. Field AutoField render still measures its natural child; do not claim that every child measure is eliminated.

Observer Measurement reuse now displays the original8 controls and a second form group with separate parent measurement count, real two-way editors and popup checkbox action. Actual typing/undo/menu checks are verified without changing its parent count. New managed17 and GPU28 compare retained and explicit measurement-bypassed forms through5value/options/allocation phases, MSAA1/4, three image slots and four replays. Full application CPU, memory growth and exactRC3 NativeAOT performance remain separate open gates.

## TabList intrinsic measurements — 2026-11-12

Known opt-in TabList presentations use a single bounded intrinsic-size buffer, keyed by the existing resource/text environment, options and nonreserved selection. Constraint-dependent positional arrays are rebuilt, including overflow's second pass. Reserved nonoverflow tabs propagate ancestor retention; custom presentations default dynamic. See fluent2-tablist.md for the256-entry limit and failure/invalidation contracts. This is actual repeated measurement elimination, with no elapsed-time/FPS/Private/working-set improvement claim. Remaining Textarea/Tooltip/composed/legacy and original full CPU/lifetime/behavior/performance gates stay open.

## Multiline and explicit owned delegation — 2026-11-12

Known Textarea presentation/size/appearance policies now propagate existing parent measurement retention. Tooltip delegates to its owned target, and Field/Tooltip/user wrappers share protected CanRetainOwnedMeasurement. The base composition default remains false; there is no automatic Build/body inference. No new cache or widget fields are introduced. Actual editing/undo and Tooltip scheduling/show/Escape remain live. Field/Tooltip still query the sizing consumer during render; the validated owned-child sizing step below reuses known unchanged results. The remaining original full CPU/lifetime/behavior/load qualification and legacy immediate consumers are not closed by this scoped work.

## Validated owned-child sizing — 2026-11-12

Natural Field widgets and Tooltip share the existing UiRetainedLayout with explicit child constraints. Each owner has one optional single-result size cache, created only for a consenting live child in a retainable text environment. The base UiWidget acquires no new storage. First sizing still executes the child's validation/measurement. Subsequent hits require unchanged resource/text keys and compatible constraints; the render-only tight allocation of an already validated natural size may reuse it. Ownership/disposal is checked before every lookup. Unknown children, glyph observation and legacy immediate AutoField do not use this cache.

Property/owner invalidation clears references and increments the existing revision. An invalidated or failed attempt cannot commit, and the sizing consumer never borrows stale same-frame prepared geometry. Existing container prepared-arrangement behavior is preserved separately. All actual rendering, input values, field metadata/disabled scopes, editing/undo, popup scheduling/painting/actions remain live. Disposal drops the single-result cache. The additional owner cache storage is bounded; full application Private/working-set/long-growth and exactRC3 NativeAOT performance remain open.

Observer ./run-gallery.ps1 → 측정 재사용 includes `소유 자식 크기 측정` for the actual natural Field input and Tooltip target. This counts widget measurement calls, not every internal text/presentation operation. Evidence: artifacts/owned-child-sizing-2026-11-12/verification.json.
