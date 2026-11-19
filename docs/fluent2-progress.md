# Generated ProgressBar widgets

## Readonly numeric accessibility — 2026-11-14

All `UiProgress` source/presentation/behavior variants, including generated Fluent widgets, publish a stable ProgressBar semantic element. Set `AccessibleName` for its purpose. Determinate progress exposes a readonly range from0 toMaximum with the clamped logical source value and zero adjustment increments. Interpolation changes only painting; accessibility reports the target immediately and does not emit a range event for each visual frame. Indeterminate progress keeps the element identity and omits the numeric range. Returning to a determinate value restores it. Offscreen progress preserves metadata without scheduling animation frames.

Accessible immediate rendering uses `ui.ProgressBar<TPresentation>(id, value, size)` or `ui.ProgressBar<TBehavior, TPresentation>(id, ref behavior, value, size)`. IDs must be nonempty and unique in their scope. Existing id-free overloads remain painting calls without semantic identity, so multiple old calls do not collide. Use the explicit overload or a retained widget when the indicator must be discoverable by assistive tools. Parent-owned children reuse their existing child keys without new identity fields or a global registry. Rendering without an accessibility tree skips semantic identifier resolution.

Managed .NET11 control95 assertions cover target-versus-paint values, changed-only range publication, clamping, readonly writes, indeterminate mode, one getter read, explicit IDs, parent-owned siblings and clipped idle. Actual Linux app35 and independent AT-SPI32 assertions per runner additionally verify native readonly ranges and cached Value-interface removal/restoration without clearing the client cache. Windows UIA already consumes the common range contract; a new physical screen-reader/Windows native run is not claimed. Evidence: `artifacts/linux-accessibility-2026-11-14/progress-verification.json`. Full progress and platform conformance, fixed-RC3 NativeAOT and hardware acceptance remain open.

## Parent allocation and pure measurement — 2026-11-10

Progress now measures and renders the actual parent root. `IUiProgressPresentation.Arrange` explicitly accepts changed allocations; an unsupported or incorrect implementation fails during value-aware measurement. Fluent uses the assigned width/height for the track, fill fraction, moving gradient and pulse. Natural thickness remains 2/4px; an explicit size or tight parent allocation overrides that height. Painting is clipped to the actual root. Existing immediate overloads retain natural sizing; a new final `constrainLayout` argument opts custom controls into allocation.

`IUiProgressSource<T>.Measure<TPresentation>` is a separate static pure-measurement contract. Static values use their real shape/thickness metadata. Getter sources do not execute their render callback when measuring: Fluent requires `Size.Y` or a tight parent height. Missing height fails explicitly; the engine does not assume medium thickness. A custom source can implement pure measurement using its own declared metadata, while keeping `Read` exclusively for rendering. That metadata/presentation must match the source's layout contract. No widget fields, arrays, runtime registry or value snapshots were added. Measurement never evaluates transition behavior; a getter is still read once per render.

```csharp
var progress = Fluent.ProgressBar(() => new(value.Value, 100));
var allocated = Dux.SizedBox(progress, 80, 64);
// For natural container layout, declare the getter's height instead:
var natural = Fluent.ProgressBar(() => new(value.Value, 100));
natural.Size = new(0, 4);
```

User presentations can implement source-independent `Measure(context, size)` and static `Arrange`; custom controls share `UiControlContext.MeasureProgress<TPresentation>`. Requested dimensions must be finite and nonnegative. Zero-sized roots request no visible animation. Existing 300ms transitions, source values and disposal remain intact.

Original-source tests reproduce missing static measurement and a 220x4 painted root inside an 80x64 parent. Managed .NET11 RC3 qualification passes 306 checks, including 60 CPU checks, 236 new exact allocation-pixel comparisons, source-read budgets and existing transition/GPU tests. MSAA1/4, Light/Dark, three frame slots and 465 readbacks cover live parent changes, colors, shapes, endpoints, indeterminate motion, generated user presentation and acquired-image replays. Builds have no warnings/errors. Native remains unverified because C++ Build Tools are absent. Evidence: `artifacts/progress-allocation-2026-11-10/verification.json`. This is a functional fix; FPS, Private memory and allocation improvements are unmeasured. Whole-goal counts and resize deferral remain unchanged.

Local sample: `samples/fba/layout_progress_allocation_fba.cs` (Width/Height, a live Slider and shared value). Current .NET11 source is required; this API/sample is not published in the existing NuGet package. [Run the verified sample](widget-gallery-run.md).


Last synced: 2026-11-14

Fluent.ProgressBar(float? value, float maximum=1) and Fluent.ProgressBar(Func<UiProgressValue>) return generated inheritable widgets. UiProgress<TSource,TSourcePolicy,TPresentation> closes source and presentation types without string lookup or runtime implementation registries. Getters run once per render. Users can inherit built-ins or replace measurement, layout and painting through IUiProgressPresentation.

## Contract and implementation

The [official guide](https://fluent2.microsoft.design/components/web/react/core/progressbar/usage), react-progress API, styles, motion and color sources at microsoft/fluentui commit8830c10bfb2f4c1bdd540287b09713b9679cd1ad were compared on2026-11-05. Public API annotations are confirmed; the complete release-version/channel review remains open. A Preview document heading is not treated as a package channel.

- Value/Maximum clamps to0–1; null is indeterminate. NaN, infinities, nonpositive Maximum and invalid enums fail explicitly. This intentionally reports invalid input rather than applying the web implementation's corrective defaults.
- Rounded/Square, Medium2px/Large4px and Brand/Success/Warning/Error are provided. Status primary colors #107c10/#da3b01/#d13438 were checked against both schemes' Background3 mapping. Brand/background still use the current Duxel theme, so complete official palette equivalence is not claimed.
- Indeterminate progress moves a real33%-width gradient through a three-second linear cycle. ReducedMotion pulses full-width opacity from0.2 to1. Drawing intersects root/current clips and explicitly selects/restores the white texture.
- Only visible indeterminate progress schedules animation. Clipped or unchanged determinate progress does not keep waking the loop. No permanent animation registry or subscription is added.

Typed immediate ui.ProgressBar<UiFluentProgressPresentation>(state,size) and generated widgets share the same kernel. Existing ui.ProgressBar(float,size,overlay) and Dux.ProgressBar(Func<float>,size,overlay) retain their presentation, overlay and callback contracts. The legacy presentation shares placement but has not migrated to the new default Fluent presentation. Hiding/migrating this compatibility path remains part of the full goal.

## Running and validation

The existing widget_tree_theme_fba.cs accepts --progress for slider-driven values, three status colors, indeterminate movement and reduced motion.

```powershell
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -NoLaunch
# Pass --progress to the printed executable; below is the preserved fixed-RC3 build.
& './artifacts/fluent-progress-2026-11-05/native-gallery-rc3/net11.0-windows7.0/widget_tree_theme_fba.exe' --progress
```

.NET10 passed898 full checks and66 generator checks; the net8/9/10 solution built with zero warnings/errors. All323 preceding images are identical; seven new GPU images cover generated/immediate pixel equality, value/color changes, zero changed pixels outside the moving gradient's root and reduced-motion output. Actual GPU output was inspected, without claiming an official browser reference comparison. Initial tests incorrectly expected rect/circle vertices; they were corrected to inspect the engine's actual analytic primitives.

The shared windowless consumer passed the same46 checks in managed and fixed-RC3 NativeAOT execution:24 keyboard plus22 progress checks. SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, win-x64, Speed and actual native code generation were verified. The gallery also published as NativeAOT; this is not a claim of GUI startup/user-input validation in this phase. The user-requested ERD window and previous executables were preserved.

## Follow-up: widget-owned300ms transitions

Generated widgets now bind UiFluentProgressBehavior for300ms CSS ease width transitions using the [W3C ease control points](https://www.w3.org/TR/css-easing-1/#valdef-cubic-bezier-easing-function-ease), (0.25,0.1,0.25,1). Retargeting starts from the current display width; clamped values at or below0.01 reset immediately. First display and return from indeterminate mode do not start an entry transition. Reaching the exact displayed target stops further requests despite floating-point deadline rounding.

UiProgress<TSource,TSourcePolicy,TBehavior,TPresentation> separates behavior, source and presentation. IUiProgressBehavior<TSelf>.Evaluate/Release operate on widget-owned value-type state, selected statically without name lookup or runtime theme branching. User behaviors use the same lifecycle, with Release invoked once on Dispose. Existing UiProgress<TPresentation>/three-argument generic types remain immediate and carry no animation field. Interpolation uses a rendering copy and preserves the logical Source target.

Immediate callers retain state outside the frame and call ui.ProgressBar<UiFluentProgressBehavior,UiFluentProgressPresentation>(ref state,value,size). Recreating state every frame also recreates the transition. Build keyed children retain both generated widget and state.

Follow-up validation passed915 full .NET10 checks and the same62 shared managed/fixed-RC3 NativeAOT checks. Independent inversion over4,097 samples has error below1e-6; tests cover retarget continuity, exact completion, release, clipped/removed frame scheduling and actual GPU start/final widths. All323 preceding images are unchanged. An initial Native test nullable warning was corrected; final publication has no warnings, with the initial60-check artifact preserved separately.

With identical Parent identities and1,024 constructions, immediate106,496B versus animated131,072B adds24,576B (+23.08%,24B/widget), identically observed in managed and Native execution. This measures construction/storage, not Private memory, app FPS or complete default Instance-identity cost. No global animation dictionary or hidden subscription is added; idle curves are not evaluated. No speed improvement was measured. New evidence is under artifacts/fluent-progress-motion-2026-11-05/.

## Shared rounded-gradient follow-up

Generated/immediate indeterminate ProgressBar now uses the [common gradient primitive](rounded-gradients.md). One instance combines background/moving color under an analytic rounded mask, resolving the prior mask limitation described below. Complete official visual fidelity, motion, palette and UIA remain open. Validation passes954 fullNET10,45 shared managed/RC3 GPU and62 input checks, with323 identical original-shader reference images at the same size. This is not a whole-app speed claim.

## Remaining scope

CSS reversing-shortening rules, complete color tokens, OS reduced-motion/high-contrast integration, Field semantics/UIA and official visual/motion comparison remain open. Rounded masks or ordinary300ms curve checks do not certify every browser motion rule. The23 discovery names,289 known requirements and266 missing names are not final overall coverage.

No speed, allocation or Private-memory improvement was measured. Previous residency costs, the full-RC3 GPU comparison failure, complete Fluent/default Text and Pi/Linux/ARM64 work remain open. Evidence: artifacts/fluent-progress-2026-11-05/verification.json, controls-native-rc3.json, image-comparison.json, source-hashes.json and preserved official source files.

2026-11-12 follow-up — [Content measurement contract](widget-cpu-retention.md): deterministic sources/presentations participate in parent retention; dynamic string/badge getters and custom traits default to fresh measurement. Declared getter Progress geometry and stable Spinner visibility are separate promises; rendering/getters/animation/navigation continue. Focused26 and exact GPU comparisons qualify this scope, not full NativeAOT/performance. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.
