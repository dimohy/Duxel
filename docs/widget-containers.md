# Compiled layout containers

On Linux x64 .NET 11 NativeAOT, the external layout sample was profiled in steady and changing modes. It repeatedly allocated 1,293,904 bytes during UI rendering on the Tooltip-opening frame. The allocation source and broader integrated performance remain open. [Profile scope and results](optimization-session-2026-11-19.md#external-custom-container-steady-and-changing-profiles)

2026-11-11 Assigned-layout invalidation: Grid, Stack and single-child containers now recompute changed options on the following frame even when the parent supplies the same tight size without measuring again. A change during widget rendering preserves the parent's prepared sibling geometry for that frame. Explicit measurement reuse refreshes the preparation frame; nested rendering restores the previous transient context. Unchanged arrangements still reuse child measurement. The shared kernel adds one preparation-frame integer per retained record, without a new cache per widget. Seven new CPU checks, container GPU252 and 169 unchanged baseline readbacks qualify this correction. ScrollView75, virtual-list75, lifetime101 and accessibility49 pass. See `artifacts/assigned-arrangement-invalidation-2026-11-11/verification.json`. Container stages remain3/6; this is a correctness fix, with no speed, NativeAOT or overall completion claim.

2026-11-10 [Custom container contract](custom-container-contract.md): external generated layouts pass 15 public-API checks; actual root clipping fixes oversized custom paint/input. Container GPU regression 237 and routing 2,317 pass. Sample: `layout_custom_contract_fba.cs`. Current Native/performance gates remain open; container stages stay 3/6.

2026-11-10 [Keyed virtual list](virtual-list.md): fixed-height vertical rows use double offsets and viewport-local placement for billion-item sources. CPU43 and expanded routing2317 checks pass; rows outside overscan dispose through existing ownership. Variable-height/other axes, cross-key recycling policy, external UIA and current Native remain open; container stages stay3/6. Sample: `layout_virtual_list_fba.cs`.

2026-11-10 [Owned ScrollView](scroll-view.md): Dux.ScrollView owns finite viewport measurement, real child placement, offset, scrollbar lifetime and invalidation. CPU47 and separate GPU/routing1933 checks pass. World-coordinate precision loss fails explicitly; rebased large content/virtualization and current Native remain pending. Container stages stay3/6. Sample: `layout_scroll_view_fba.cs`.

2026-11-10 [Nested scroll routing](scroll-routing.md): boundary handoff on independent wheel axes, caller clip and popup/window-order hit testing, shared scrollbar capture/release, and measured DirectText window steps. Expanded managed regression passes 1,645 checks and 1,025 readbacks; container stages remain 3/6. Owned scroll widgets, virtualization and current Native remain pending. Sample: `layout_scroll_routing_fba.cs`.

2026-11-10 [Field parent allocation](fluent2-field.md#parent-allocation--2026-11-10): actual width precedes text measurement; static FieldWidth/Arrange and a local layout copy reserve description rows and allocate remaining height to the real editor. The 80 x 64 / 80 x 32 defect is fixed. Managed Field1018 and separate routing1563 checks pass; natural cache, editor/undo and Core UIA metadata persist. Panel measurement rejection/closed-root boundary checked. Native remains unverified; container stages remain3/6. Local sample:layout_field_allocation_fba.cs.

2026-11-10 [Badge measurement and allocation](fluent2-badge.md#measurement-and-parent-allocation--2026-11-10): shared geometry and one common root, centered fixed glyph/icon metrics, root clipping and hidden-counter reservation. Read-only providers and the existing bounded number memo are shared across explicit phases. Managed 374 checks pass; Native unverified. No fields/new caches. Local sample: layout_badge_allocation_fba.cs.

2026-11-10 [Link measurement and parent allocation](fluent2-link.md#measurement-and-parent-allocation--2026-11-10): real width controls wrapping; static Arrange returns a local copy while the bounded natural cache stays reusable. Input, paint and clips share the actual root. Managed 495 checks pass across CPU/input, raster/vector, MSAA 1/4, Light/Dark and paced retained frames. No new fields/caches; Native remains unverified. Local sample: layout_link_allocation_fba.cs.

2026-11-10 [Divider measurement and parent allocation](fluent2-divider.md#measurement-and-parent-allocation--2026-11-10): static Measure and CanArrange use one common root; actual width controls caption wrapping and both axes control lines, centering and clipping. Content providers remain read-only and are read once per explicit measurement/render phase. Managed525 checks pass; Native remains unverified. No new widget/context fields or caches. Local sample:layout_divider_allocation_fba.cs.

2026-11-10 [Text parent allocation](fluent2-text.md): CanArrange opts into one shared text root; assigned/measurement width controls wrapping without changing fonts. Existing content-provider reads are preserved. Managed321 checks pass across raster/vector,MSAA1/4,Light/Dark and retained16/3-frame updates. No per-widget fields were added;one context counter is added. Native remains unverified. New local sample:layout_text_allocation_fba.cs.

## Parent allocation and pure measurement — 2026-11-10

Progress now uses static source/presentation measurement and Arrange for actual parent roots. Static values supply real thickness; getter measurement uses declared Size.Y or a tight parent height and never executes the render callback. Custom sources can supply pure layout metadata. The existing immediate API retains natural sizing; explicit constrainLayout opts in. See [full Progress contracts and examples](fluent2-progress.md).

Managed .NET11 RC3 passes306 checks with MSAA1/4,Light/Dark,three frame slots and465 readbacks. No widget fields or arrays were added. Native and cross-runtime comparison remain unverified because the C++ tool discovery file is absent. Whole-goal counts/resize deferral are unchanged. Evidence:artifacts/progress-allocation-2026-11-10/verification.json. Sample:layout_progress_allocation_fba.cs.

## Parent allocation — 2026-11-10

Spinner now uses the same pure measurement and allocated rendering path. `IUiSpinnerPresentation.Arrange` receives the natural layout and the actual root size. Fluent centers its unchanged ring and label in that root, preserving diameter, typography and the 8px gap; clipping handles small or zero allocations. This follows the pinned [Fluent root centering and ring flex-shrink rules](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinner/library/src/components/Spinner/useSpinnerStyles.styles.ts). It does not claim full browser fidelity.

```csharp
var spinner = Fluent.Spinner("Working", new() { Width = 260 });
var allocated = Dux.SizedBox(spinner, 80, 96);
```

Custom presentations must implement static `Arrange` when their root size changes. The default throws, and an incorrect returned size fails during measurement. Custom controls can use `UiControlContext.MeasureSpinner<TPresentation>` and the explicit `Spinner(..., constrainLayout : true)` overload. Existing immediate overloads retain natural sizing. Delayed content remains absent while its assigned root footprint is preserved; natural delayed height remains zero. Measurement does not evaluate or start animation behavior. No new widget fields or arrays were added.

Original source reproduced an 80x96 measured parent with an actual 260x32 root. Current .NET11 RC3 managed validation passes 520 checks: 180 CPU/semantic/scheduling checks and 340 exact pixel comparisons across sizes, label positions, RTL, Light/Dark and MSAA1/4 with three frame slots. There are 617 readbacks including warmup. Live parent changes, acquired-image replays, generated custom presentations, unsupported/wrong Arrange, zero roots and delayed roots are covered. Build warnings/errors are zero. Native remains unverified because C++ Build Tools are absent. Evidence: `artifacts/spinner-allocation-2026-11-10/verification.json`. Whole-goal counts and resize deferral are unchanged; no performance improvement is claimed.


## RadioGroup parent allocation — 2026-11-10

The actual parent width controls wrapped text. Static `IUiRadioGroupPresentation.Arrange` returns the allocated root without changing canonical option geometry. Fluent options keep natural,start-aligned hit regions;blank root space does not select. Root clipping applies to painting and input. Height-only changes reuse natural measurement when `ReuseLayout=true`;height-dependent custom presentations must leave it false. Unsupported/incorrect Arrange implementations fail explicitly.

Current-source .NET11 RC3 managed validation passes264 checks with MSAA1/4 and three frame slots,producing245 readback images. Tests and the new `layout_radio_allocation_fba.cs` sample build with zero warnings/errors. The sample has three layouts,shared selection and Width/Height controls. First presentation and15-second normal exit are verified;manual input/appearance approval is separate.

Native publish failed because Visual Studio C++ Build Tools/vswhere.exe are absent. Native execution and managed/native image comparison remain unverified. Historical L:/Cache evidence is inaccessible. Evidence:`artifacts/radio-resume-2026-11-10/verification.json`. Whole-goal counts remain engine15/27,widgets9/21,containers3/6,integration8/15,portability5/8;Fluent/overall denominator unresolved. Resize stays deferred. No performance improvement is claimed.


Last synced: 2026-11-10

## Retained measurement and arrangement

The shared result payload now also serves Row/Column,Container and Stack. Parent-data Expanded/Flexible/Spacer/Positioned wrappers render directly into the parent's tight allocation instead of measuring their child a second time. Row/Column still compute child positions during traversal;this is measurement retention,not a claim that all incremental layout is finished. `IUiContainerLayout`,custom axis and custom flex-fit policies default to non-retained measurement;the built-in box layout/axes/fits explicitly opt in. User subclasses with extra untracked sizing inputs must disable `CanRetainArrangement` or `CanRetainMeasurement` as applicable.

All container families share one optional `UiRetainedLayout<TResult>` payload. Its mutation revision lives inside the payload;volatile arranged instances no longer carry an unconditional revision field. A previously ineligible subtree becomes eligible on a later measure after its children establish retained results. This avoids publishing a first result whose children mutated during measurement. Container adds one reference field;its construction allocation cost is tracked separately from steady cache-hit time. Exact current results:`artifacts/container-retention-2026-11-06/verification.json`.

Current qualification:managed/fixedRC3 Native175 checks each and121 exact MSAA1/4 images at3 slots. Existing counter tests verify no additional measurements across unchanged Row/Column/Container/Stack renders,with live weights/options/positions and volatile custom policies still active. Reusing the prior qualified Native baseline avoids another baseline build;unchanged benchmark sources run3 times each in ABBAAB order. Existing Wrap/Grid cache-hit median remains0.3µs on both sides,with an after run at1.7µs and p95/p99 median0.3→0.4µs. Ranges overlap;no speed improvement is established by this comparison.10000 Container ownership constructions allocate6,000,000→6,080,000B(+80,000B,+1.33%) from the added reference. First Build remains880,000B and unchanged Build0B. Private/working-set ranges also overlap. These costs remain visible;the result expands correct reuse rather than proving complete memory/performance optimization.

Wrap/Grid reuse their existing child rectangles when constraints,configured typography and invalidated widget properties are unchanged. `RequestRebuild`,watched state,child adoption/removal and disposal invalidate ancestor measurement without rebuilding ancestor composition. An unchanged sibling layout may keep its own result. Rendering,input and callbacks still execute. A render allocation matching the measured size uses that measured arrangement instead of measuring again under its tight allocation.

User widgets/presentations are non-retained by default. A leaf may override `CanRetainMeasurement`;a custom `UiArrangedChildrenWidget` must opt in through `CanRetainArrangement`. The promise permits only box constraints,configured typography and widget properties whose changes call `RequestRebuild`. Time,pointer state,arbitrary theme/style reads and untracked external data require non-retained measurement. Subclasses changing a retained Fluent button's measurement must honor the inherited promise or explicitly disable it:

```csharp
protected override bool CanRetainMeasurement => false;
```

`IUiButtonPresentation.CanRetainMeasurement` is a compile-time policy,default false. Fluent medium/small/large button presentations opt in;their behavior/paint remain independent. Context identity,backend generation,typography/font resources,content/text scale,font/line settings and constraints participate in reuse. Glyph observers disable reuse. Resource tokens have no back-reference to the rendering context/backend/atlas. Each eligible arranged widget has at most one optional cache record and reuses its existing bounds array. Mutation during measurement prevents publishing that result;failed layout invalidates the old result. Those container families now use the shared payload described above;complete incremental layout remains separate work.

Qualification:`artifacts/layout-retention-2026-11-06/verification.json`. Managed/fixedRC3 Native159 checks each and109 matching GPU images include live label geometry/glyph changes and acquired-image reuse. A CPU-only benchmark uses10000 Wrap+10000 Grid **textless** Fluent buttons,requested1x1,96 warmups and1000 pure measurements. Three independent Native runs per side alternate ABBAAB. Per-run median aggregates:1827.4µs→0.2µs;child measurement calls20,000,000→0;both phase allocations0B and GC counts0. The candidate is near timer resolution;this is not a whole-frame,visible button workload,FPS,draw or Pi throughput claim. Private/working-set ranges overlap,so no process-memory improvement is established. Existing high-load app/2048-text memory limitations remain open. Benchmark build intermediates use the SDK's explicit `UseArtifactsOutput`/`ArtifactsPath` on L: after W:space preflight rejected a build before execution;no source cache was deleted or moved.

## Wrap and uniform Grid

Qualification:managed and fixedRC3 Native each136 checks;MSAA1/4 with3 frame slots,93 images match exactly.10000 Wrap plus10000 Grid children allocate0B for warmed pure measurement. The new Native gallery presents its first frame and exits normally after15seconds. This completes the combined Stack/Positioned/Wrap/Grid stage,bringing container stages to2/6;it does not complete the full container objective or establish whole-frame performance.

`UiWrap`/`UiVerticalWrap` select `UiWrapWidget<UiHorizontalAxis>`/`<UiVerticalAxis>` at compile time. `UiGrid`/`UiHorizontalGrid` select `UiUniformGridWidget<UiVerticalAxis>`/`<UiHorizontalAxis>`;the grid axis is the direction in which rows/columns advance. `[UiWidgetStyle]` also generates user-derived layouts through the same implementation path. All four are infrastructure attributes outside the Fluent visual catalog. Existing `Dux.Grid` delegate overloads remain unchanged;use `Dux.UniformGrid` for an owned widget tree.

```csharp
var chips = Dux.Wrap(new(Spacing: 12, RunSpacing: 8),
    Fluent.Button("One"), Fluent.Button("Two"));
var cells = Dux.UniformGrid(new(3, CrossAxisSpacing: 12,
    MainAxisSpacing: 12, MainAxisExtent: 40),
    Fluent.Button("A"), Fluent.Button("B"), Fluent.Button("C"));
```

Wrap measures children with a bounded main axis and an unbounded cross axis,starts a new run when the next child and gap do not fit,and shrink-wraps its largest run and combined cross extent. Under tighter parent constraints,`Alignment` distributes items within each run and `RunAlignment` distributes runs. Both use the six main-axis alignment values;`CrossAlignment` is Start/Center/End. An unbounded main axis forms one run. Options may change without replacing child instances.

Grid requires a finite cross-axis maximum and a positive `CrossAxisCount`. Cross-axis gaps are subtracted before equal cell allocation. `ChildAspectRatio` is cross extent divided by main extent;`MainAxisExtent` explicitly overrides that ratio for cell size. The main axis may be unbounded;the final row may be partial. Contradictory/non-finite options,unbounded cross extent,excessive gaps and coordinate overflow fail explicitly. Use `new UiUniformGridOptions()` or explicit valid options;`default(UiUniformGridOptions)` has zero count and is invalid. This avoids silently substituting a column count.

`UiArrangedChildrenWidget` shares reusable child rectangles,render/input clipping and exclusive ownership. Derived layouts must return finite bounds and satisfy parent constraints. Wrap also retains at most one run record per child;Grid needs no per-cell layout object. Non-retained measurement still visits each child;eligible unchanged Wrap/Grid layouts use the retained-measurement path above. This does not establish complete incremental layout,zero whole-frame allocation or app throughput. Content beyond a bounded main/cross extent is clipped;scrolling and virtualization remain separate work. RTL/reversed flows and pixel-level input policies are not full Flutter parity.

Run `samples/fba/layout_flow_containers_fba.cs` using the fixed .NET11 toolchain and `run-fba.ps1 -NoCache -TargetFramework net11.0-windows7.0`. **Columns** switches between2/3 while preserving all six cell counters. The sample uses native minimum480x560 until overflow/scrolling integration is complete;this is a sample constraint,not an engine optimization. Exact validation scope and hashes are recorded in `artifacts/flow-containers-2026-11-06/verification.json`. Design references reviewed2026-11-06:[Wrap](https://api.flutter.dev/flutter/widgets/Wrap-class.html),[fixed-count grid delegate](https://api.flutter.dev/flutter/rendering/SliverGridDelegateWithFixedCrossAxisCount-class.html). This layout Grid does not claim Flutter GridView scrolling or lazy construction.

## Stack and Positioned

`UiStack` and `UiPositioned` are generated, inheritable infrastructure widgets. The generator selects `UiStackWidget` and `UiPositionedWidget` as concrete implementations. They share `UiMultiChildWidget` ownership and the same guarded composition-root discovery as Row/Column. No runtime theme registry is introduced. References reviewed2026-11-06: [Stack](https://api.flutter.dev/flutter/widgets/Stack-class.html), [Positioned](https://api.flutter.dev/flutter/widgets/Positioned-class.html).

```csharp
var overlay = Dux.Positioned(Fluent.Button("Move"),
    new(Right: 12, Top: 8, Width: 140, Height: 32));
var layers = Dux.Stack(new(Fit: UiStackFit.Expand),
    Fluent.Body1("Background"), overlay);
```

Loose fit measures ordinary children to determine the Stack size. Expand requires bounded axes; Passthrough forwards the parent constraints. Edge-positioned children are measured afterward; opposing edges force their dimension, while right/bottom align from the far edge. With no positional properties, Positioned participates in ordinary sizing. All-positioned content needs bounded axes. Alignment defaults to top-left. Negative offsets are allowed; contradictory or non-finite options fail before ownership changes. Updating `Options` preserves the child and requests a frame.

Children paint in insertion order. Paint and standard control pointer input are clipped to the Stack box. At the pointer, the last overlapping child allocation blocks pointer hovering in lower allocations, including press-activated buttons. This uses rectangular allocations; transparent/pixel-level hit testing, pointer pass-through policies and custom controls that bypass the standard input path are not provided by this contract. Keyboard/accessibility paths remain separate. A physical layout widget is a parent-data boundary; composition-only Build widgets forward data. Positioned under Row and Expanded under Stack fail explicitly.

The gallery includes a **Move overlay** button. Stack/Positioned qualification is recorded in `artifacts/stack-containers-2026-11-06/verification.json`. Wrap/Grid are qualified in the newer flow record above;the combined stage is complete and container progress is2/6. Incremental layout,virtualized scrolling,mutable keyed child lists and full Flutter parity remain open.

## Weighted layout and generated widgets

`UiExpanded` fills its share,`UiFlexible` may use less,and`UiSpacer` leaves weighted empty space. Design references reviewed2026-11-06:[Flex](https://api.flutter.dev/flutter/widgets/Flex-class.html),[Expanded](https://api.flutter.dev/flutter/widgets/Expanded-class.html),[Flexible](https://api.flutter.dev/flutter/widgets/Flexible-class.html),[Spacer](https://api.flutter.dev/flutter/widgets/Spacer-class.html). This is not full Flutter parity.

```csharp
var save = Dux.Expanded(Fluent.Button("Save"));
var row = Dux.Row(new(Spacing: 12),
    save, Dux.FlexSpacer(), Dux.Expanded(Fluent.Button("Cancel"), 2));
// Requests a frame after rendering; the owned child keeps its identity/state.
save.Flex = 3;
```

The existing source generator emits classes/constructors directly inheriting`UiFlexibleWidget<UiTightFlexFit>`,`UiFlexibleWidget<UiLooseFlexFit>` and`UiFlexSpacer`. Fit and axis implementation types are fixed at compilation;weights/constraints are runtime layout data. No runtime theme lookup or implementation registry is introduced. Built-in and attributed user widgets consume the same implementation. These`Infrastructure` components stay outside the Fluent visual catalog.

Non-flex content/spacing precede weighted allocation. Loose children can shrink the minimum main-axis extent. Cross-axis Stretch requires a finite maximum. Loose Flexible with MainAxisSize.Min can shrink-wrap an unbounded main axis;unbounded tight flex fails explicitly. Zero flex measures naturally;Spacer requires positive flex. Changing Flex requests layout without replacing the child.

Flex parent data crosses composition-only UiWidget.Build roots and stops at physical UiControlWidget layout barriers. Discovery uses the shared measurement scope to reject ordinary render/input calls and restore state on failure. Generated buttons use allocated paint/hit/accessibility bounds. Custom leaf widgets can honor LayoutConstraints during measurement and ConstrainToLayout during painting. Explicit immediate-button sizes inside custom controls and existing input-slot sizing stay unchanged. Legacy Dux.Spacer(float height) remains fixed spacing;use Dux.FlexSpacer(int flex) or new UiSpacer() for weighted space.

Managed/pinned-RC3 Native67 checks each pass;37 GPU images match. MSAA1/4,three frame slots,live weight changes and acquired-image replays match independent references. Child identity/disposal,input,Build forwarding/failure restoration and zero current-thread bytes for a warmed10000-child pure measurement are covered. Those children are small measurement probes,not10000 rendered buttons or an app throughput/Private/FPS result. Optional parent-data storage is retained at child-count capacity and released on disposal. Incremental layout remains open.

The sample shows1:1:2. First presentation/15s normal exit reuse the fixed-weight binary built immediately before the setter extension;the sample never invokes that extension,qualified by final Native67 checks. This is compatible fixed-path reuse,not manual or resize acceptance. Exact sources/hashes/scopes:artifacts/flex-containers-2026-11-06/verification.json. One of the six container stages,weighted Row/Column,is complete;the broader mission remains active.

This ongoing implementation follows Flutter's box-layout model: constraints move down, desired sizes move up, and the parent assigns positions. Sources checked2026-11-06: [constraints](https://docs.flutter.dev/ui/layout/constraints), [layout catalog](https://docs.flutter.dev/ui/widgets/layout), [Container](https://api.flutter.dev/flutter/widgets/Container-class.html), [Row](https://api.flutter.dev/flutter/widgets/Row-class.html).

`UiContainer`, `UiRow` and `UiColumn` are generated inheritable widgets. Their closed base implementations select container layout/surface algorithms or layout axis at compile time. They own ordinary `UiWidget` children, including generated Fluent controls and user widgets. These are layout infrastructure; they do not add fictitious components to the official Fluent catalog.

```csharp
protected override IUiView Build(UiBuildContext context) =>
    context.Child("root", () => new UiContainer(
        new UiRow([Fluent.Button("Save"), Fluent.Button("Cancel")],
            new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min)),
        new(Padding: new(24), Margin: new(12))));
```

Containers adopt their children. A child already owned by another Build context/container cannot be adopted again. Register the owning root with `context.Child`; nested children belong to their containers. Disposal releases the owned subtree. Child-list replacement/reordering is not yet implemented for these initial containers.

`UiBoxConstraints` supports minimum/maximum extents and unbounded maxima. `MeasureConstrained` is distinct from the existing `Measure(available)` API, preserving existing call resolution and measurement behavior. A Build-only user widget can prepare its retained keyed tree for constrained measurement if its root is a measurable `UiWidget`. Measurement runs no rendering/input, and rendering still executes the actual children. Delegate-only legacy views must provide explicit measurement. A leaf that cannot resolve a finite desired size under an unbounded axis fails explicitly.

Container options provide padding,margin,alignment,requested width/height and additional constraints. Alignment expands bounded space and shrink-wraps unbounded axes;otherwise natural children stay top-left under requested/parent bounds. Painting is clipped. Row/Column offer spacing,main-axis size,six distributions and start/center/end/stretch cross alignment. Overflow is explicit. General leaf stretching,mutable keyed children,RTL/baseline alignment,scroll/virtualization and full retained incremental layout remain open. Dedicated single-child wrappers are implemented below.

Custom layouts implement `IUiContainerLayout`; custom surfaces implement `IUiContainerPresentation`. `[UiWidgetStyle]` closes `UiContainerBox<TLayout, TPresentation>` at compile time. The sample includes a generated custom surface and real Fluent button callbacks.

Run the current local sample from the fixed.NET11 toolchain directory, using absolute paths:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
$layoutRun = @{
    Path = 'W:/MyWorks/Duxel/samples/fba/layout_containers_fba.cs'
    NoCache = $true
    TargetFramework = 'net11.0-windows7.0'
    DotnetPath = 'C:/Program Files/dotnet/dotnet.exe'
}
& 'W:/MyWorks/Duxel/run-fba.ps1' @layoutRun
```

Development validation uses.NET11 only. NET9/10 deployment compilation is deferred until final completion. Evidence/remaining stages:artifacts/widget-containers-2026-11-06/plan.json. The10000-child pure-measurement allocation contract does not establish layout throughput or full rendering performance.

The initial demo has a480x400 minimum native size while scrolling/overflow layouts are unfinished. This is a sample constraint, not an engine performance improvement or a substitute for the scrolling mission. The gallery test also requires an actual first-frame presentation and no render exception; exit code0 alone is insufficient.

## Shared widget Build storage — 2026-11-06

The common UiWidget path now validates ownership against its existing keyed child store and reuses the existing identity slot for parent-scoped keys. Dependency and child cleanup lists live on their existing dictionary objects. This removes an8-byte widget field and24 bytes from dependency-free Build contexts without adding wrapper allocations. Built-in/generated and user-derived widgets use the same path; state subscriptions, failed-Build cleanup, identity, input, disposal and layout invalidation remain active.

Pinned.NET11 RC3 NativeAOT, three independent runs per side in ABBAAB order for each of three modes (18 processes):10000 generated owned Containers allocate6,080,000→5,760,000B during construction(-5.26%) and880,000→640,000B for their first Build(-27.27%).10000 distinct watched roots allocate5,280,000→5,200,000B(-1.52%);10000 keyed-child first Builds allocate4,800,000→4,560,000B(-5%). Unchanged/rebuilt watched and unchanged owned Build phases remain0B. Root objects/states/arrays and disposal are excluded from dependency phases; child factories are included. Construction and first retained-layout payload acquisition are separate costs.

Managed and Native each pass101 tree/ownership/lifetime and175 container checks;all121 GPU images match. The existing strict25-percent factory-allocation condition is unchanged:Native114,688→81,920B for1024 unowned buttons(-28.57%). Pure retained Wrap/Grid median0.2→0.2us,p950.3→0.3,p990.3→0.3;run ranges overlap, so no speedup is established. Small-probe Private/working-set changes do not prove application or long-duration memory gains. No forced GC or workload/quality reduction. The earlier+80,000B Container construction cost is exceeded by this reduction; historical results remain preserved.

Evidence:[verification](../artifacts/build-store-2026-11-06/verification.json),[comparison](../artifacts/build-store-2026-11-06/comparison.json). Raw runs and initial failed probes remain under L:/Cache/Duxel/build-store-2026-11-06/. Full Fluent2,remaining container contracts,high-load memory and portability remain open;resize remains deferred. Development checks use.NET11 only;.NET9/10 deployment compilation waits for whole-goal completion.

## Generated single-child layouts — 2026-11-06

`UiPadding`, `UiAlign`, `UiCenter`, `UiSizedBox` and `UiConstrainedBox` now use the shared `UiSingleChildBox<TLayout,TOptions,TPresentation>` ownership/retention path. Existing `UiContainerBox` delegates to the same base through a compile-time layout adapter; its options, required child and user layout/presentation extension remain compatible. `[UiWidgetStyle]` emits actual closed base types/constructors for built-in and user wrappers. All five are Infrastructure,excluded from the Fluent visual component count. No runtime theme lookup or artificial child is needed.

```csharp
var counter = Fluent.Button("Count", OnCount);
var body = Dux.SizedBox(
    Dux.Padding(Dux.Align(counter, UiAlignment.BottomRight), new(12)),
    width: 240, height: 80);
var centered = Dux.Center(Fluent.Button("Reset", OnReset), widthFactor: 2);
var bounded = Dux.ConstrainedBox(centered, new(100, 300, 40, 100));
```

Padding deflates both minimum and maximum constraints. Align/Center loosen the child's constraints,expand bounded axes without a factor,and shrink-wrap unbounded axes;nonnegative factors size each axis independently. SizedBox tightens specified dimensions within the parent's authority;unspecified dimensions follow the child or zero when empty. `Dux.Expand()` requires bounded axes. ConstrainedBox enforces additional constraints within the parent bounds. Empty children allocate no placeholder widget. Layout surfaces clip overflow. Options validate before adoption/mutation;live updates preserve child instances and invalidate ancestors. Existing Container keeps its earlier natural-child semantics;SizedBox is the explicit child-tightening wrapper.

Pinned RC3 managed/Native each pass229 container/GPU checks (previous175 plus54),with159 byte-identical GPU images at MSAA1/4 and three frame slots. Checks cover tight/loose/unbounded constraints,factors,empty space,nesting,real Fluent pointer callbacks,live updates,disposal,custom generated wrappers,conservative custom-layout opt-in and acquired-image replay. Unchanged101 lifetime checks are reused only after source/path compatibility review. No whole-suite/high-load repeat or new speedup claim.

Separate Native10000-Padding probe:construction5,280,000B;first layout acquisition2,320,000B;steady and invalidation-reuse measurement0B;10000 child disposals. First-layout cost includes widget identity preparation and retained payloads;it is not isolated cache-only storage or process Private. Arrays/context/frame/disposal are outside allocation phases;no forced GC. Managed construction observed5,280,384B;first layout and steady/reuse values match. This is one storage diagnostic per runtime,not a before/after speed or memory improvement. Existing Container construction5,760,000B/first Build640,000B/steady0B also remains unchanged in a focused Native regression probe.

[Evidence](../artifacts/single-child-2026-11-06/verification.json). Container stage3/6 is complete;leaf extension,scroll/virtualization and full integration/performance remain open. Resize stays deferred;development uses.NET11 only. Official references read2026-11-06:[Padding](https://api.flutter.dev/flutter/widgets/Padding-class.html),[Align](https://api.flutter.dev/flutter/widgets/Align-class.html),[SizedBox](https://api.flutter.dev/flutter/widgets/SizedBox-class.html),[ConstrainedBox](https://api.flutter.dev/flutter/widgets/ConstrainedBox-class.html). These are live API pages;no Flutter source commit was pinned and full Flutter API parity is not claimed.

## Parent allocation for Slider and Toggle — 2026-11-06

A reproduced mismatch let a SizedBox measure80x64 while its generated Slider still painted/hit-tested120x32. The short rail could not reach the intended endpoint through the allocated clip. Generated Slider and Toggle widgets now pass explicit layout participation into their existing common renderer. Slider/Checkbox/Switch/ToggleButton retain their behavior and fixed visual parts while arranging their root,rail or indicator/label composition within the actual parent size. Small rails keep the original thumb geometry,clip overflow,and avoid invalid clamp ranges/division by zero;keyboard operation remains available even with zero pointer travel.

`IUiSliderPresentation.Arrange` and `IUiTogglePresentation.Arrange` are static virtual contracts,selected with the existing closed generic presentation. A changed parent size requires a presentation to implement Arrange;unsupported custom presentations fail during measurement rather than silently drawing different bounds. Returning a size that ignores the allocation also fails. Built-in presentations implement the contract. A generated user dial demonstrates different actual layout/paint with the same Slider behavior. No runtime presentation lookup or new per-widget field is introduced.

The original immediate overloads retain natural-size behavior. User control implementations can explicitly call the new `Button(...,size,constrainLayout:true)`, `Slider(...,options,size,constrainLayout:true)` or `Toggle(...,ref value,constrainLayout:true)` overload. Public `UiControlContext.MeasureSlider<TPresentation>` and `MeasureToggle<TPresentation>` share the same constrained measurement path. Fixed input-child-slot behavior stays governed by the existing allocation opt-in.

Pinned RC3 managed/Native each pass172 focused checks,including existing Slider/Checkbox/Switch contracts,allocated hit regions,endpoints,vertical and zero-travel rails,keyboard,custom opt-in/failure and actual user-generated Arrange selection.43 GPU images match across runtimes at MSAA1/4 and three frame slots;independent assigned geometry verifies fixed thumb/indicator dimensions and live parent changes. The initial original-engine mismatch,missing DirectText fixture and neutral-reference/hover-input fixture mismatch remain in raw logs/images. The last fixture now explicitly places the pointer outside the scene for both actual/reference rest-state captures;pixel criteria were not widened.

[Evidence](../artifacts/leaf-constraints-2026-11-06/verification.json). This is a functional layout/input repair,not a performance claim. Container stages remain3/6;Input,RadioGroup,text,progress/spinner and remaining leaf contracts still need a complete audit. Full Fluent2/long-memory/portable work remains open;resize stays deferred.

## Editable Input parent allocation — 2026-11-06

The prior engine measured an80x64 parent but rendered an80x32 Input field. A separate original-source build reproduces the mismatch;the candidate uses the existing typed input engine with explicit layout participation. `IUiInputPresentation.Arrange` statically selects parent-specific layout before placement and editing. Unsupported custom presentations fail during measurement,and placement cannot change the assigned root size. Built-in Fluent outline/underline/filled,size and adornment implementations share one geometry path;the compatibility context presentation also implements Arrange.

Field,editing bounds,text-line position and prefix/suffix rectangles follow the actual allocation. The font/line metrics and explicit child-slot sizes remain unchanged;short/zero extents clip content. Arrange consumes already measured adornment extents rather than calling their Measure again. Live parent changes preserve the input owner,active editing,undo history and real child callbacks. The new public typed InputText overload accepts explicit constrainLayout participation;existing immediate overloads preserve natural sizing. Public UiControlContext.MeasureInput shares the constrained measurement helper.

Pinned RC3 managed/Native each pass212 checks,including existing InputKernel,Fluent Input,adornment,owned-child and Field tests.25 GPU images match at MSAA1/4 and three frame slots:independent80x64/160x48 geometry,typography,underline baseline,24px owned children and acquired-image replay. The original source selection records all five files with hashes matching the prior qualified manifest. Initial overlapping builds were cancelled and excluded;the original build uses separate intermediates,then the candidate was fully rebuilt serially. The first lifetime assertion overlooked another live input owner;the corrected check proves each owner retains/releases its own state and all storage reaches zero after both are disposed. The GPU reference now uses an explicit hidden nonempty ID instead of an invalid empty editor key. Initial records remain preserved.

[Evidence](../artifacts/input-allocation-2026-11-06/verification.json). This is a functional layout/editing repair,not a throughput/FPS/Private improvement. Container stages remain3/6;RadioGroup,text,progress/spinner,scroll/virtualization and full leaf/integration work remain open. Resize stays deferred;only.NET11 development verification ran.

## Allocated child routing — 2026-11-10

The common RenderAllocatedChild path now captures the caller's clip before isolating legacy table/column positioning. Assigned bounds already include parent placement, so nested Padding and descendant controls must not apply that legacy placement again. The caller's routing flags, builder, layout, item and next-item state are restored on success or exception. This applies to layout children and the same shared path used by Field/input-owned children. There are no new fields or caches.

Before: all 28 cases across 14 variants and table/columns failed. A table child expected at (10, 10) instead rendered at (0, 0); the column case expected (167, 10) but rendered at (157, 208). After: both nested padding offsets are honored and a throwing child restores caller table placement. Managed 1529 checks pass: 1113 CPU/source/input/lifetime/container checks and 416 exact GPU comparisons. GPU covers 13 stable variants, table/columns, normal/narrow ancestor clips, raster/vector, MSAA 1/4 and Light/Dark with three frame slots. Spinner participates in CPU coordinate checks; its time-varying image is not part of these new exact comparisons. There are 833 readbacks including warmup.

The initial GPU reference omitted the actual inherited table-cell clip, which had been captured before row growth. The fixture now supplies that real caller clip to the independent world-coordinate reference. Production rendering was not changed to erase the discrepancy. Initial failed images/logs remain in fixture-initial. Qualified logs, final result, native-toolchain-audit.json and source hashes are in artifacts/allocation-routing-2026-11-10/verification.json and adjacent files.

This does not complete the whole leaf gate. A separate current diagnostic proves Field parent measurement is 80 x 64 while its actual root is 80 x 32. That Field defect is fixed by the later Field allocation checkpoint; the old routing evidence remains scoped to its own revision. Panel is an independent internal panel root over arbitrary IUiView and still rejects generic constrained leaf measurement; its boundary needs explicit acceptance/documentation. Container stages remain 3/6.

Native discovery was rechecked through PATH, standard/selected alternative folders, environment and registry. No compiler/SDK installation was confirmed there; L:/Runtimes, L:/Cache and L:/Containers deny directory access and are not asserted empty. Two earlier publish logs are zero bytes; the earlier gallery log records native generation but cannot establish today's installation. No discovery bypass, install or repeated publish was attempted. Current Native/cross-runtime qualification remains pending. A current managed rebuild/smoke run reuses the Badge sample with identical body/local project directives; manual input and official visual approval remain distinct. No FPS/Private/allocation/native-code-size improvement is claimed. Resize remains user-deferred and NET9/10 deployment compilation stays deferred.
