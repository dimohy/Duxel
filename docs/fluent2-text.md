# Generated text widgets and shared presentation

## Standalone text native accessibility — 2026-11-15

`UiText` widgets, including `Fluent.Body1`, `Fluent.Title2` and `Fluent.Text`, publish their displayed strings as static Text/Label nodes. Equal sibling strings retain distinct identities. Updating `Source` on the same instance changes its native name without replacing identity; `AccessibleName` provides an explicit name. The public `UiWidgetIdentity.Parent` default and input identity remain unchanged. Immediate `ui.Text` calls are separate from this widget publication path.

The source and presentation run once. Bounds come from existing common text placement and follow allocation, wrapping, clipping, margins and rotation. Disabled/offscreen state and the accessibility parent are preserved; removed widgets retire. Static text exposes no keyboard focus, editable Value or numeric Range. Without an attached tree, publication and identity-string creation do not run. With a tree, each text instance retains a lazily created token and reuses the existing bounded identifier cache.

Use `widget.Source = "Changed content"` for generated static Fluent text. This differs from the compatibility `UiText<TPresentation>.Text` property. Custom presentations must place their real root through `UiControlContext.LayoutText`/`PlaceText` or common typography. With accessibility enabled, a presentation that places no common root fails explicitly instead of publishing guessed bounds.

The implementation follows the static name, bounds and noneditable patterns of the [Microsoft Text control contract](https://learn.microsoft.com/windows/win32/winauto/uiauto-supporttextcontroltype). Rich selectable Text patterns, per-character geometry and whole-screen-reader conformance remain separate work.

Focused checks passed: 64 CPU checks including existing allocation/source-lifetime regressions; 24 Windows raw GPU pairs; Windows UIA 10 checks (4 text and 6 existing Skeleton); pinned .NET11 RC3 NativeAOT Linux AT-SPI 12 checks (4 text and 8 existing Skeleton). Windows uses managed .NET11, not Windows RC3 NativeAOT. The current delivery passed34Observer presentation stages, idle/restoration and actual launcher4-worker consumption. Three performance pairs have overlapping CPU/input ranges and some worse medians; neither speed improvement nor proven no regression is claimed. Previous binaries remain preserved. See `artifacts/text-accessibility-2026-11-15/plan.json` and `artifacts/linux-observer-delivery.json` for current state.


## Parent allocation and content measurement — 2026-11-10

Text widgets now reserve the actual parent root, including short, empty and zero-sized content. Built-in Fluent text, typography presets and context-font presentations opt into static `IUiTextPresentation.CanArrange`. An opted-in presentation must place exactly one root through the shared PlaceText/LayoutText/typography helpers; rendering checks its final bounds. A custom presentation that does not opt in rejects a changed allocation during measurement. Old standalone and typed-immediate calls preserve natural layout; a final `constrainLayout` argument explicitly opts a custom control in.

The common placement step reserves the assigned size while preserving glyph size, baseline and line height. Parent/measurement width controls wrapping and takes precedence over legacy absolute PushTextWrapPos inside that layout. Use a narrower child box or explicit WrapText(text, width) for a smaller content budget. Outside assigned layout, legacy wrap positions still apply. The allocation scope isolates/restores table/column routing, so their alignment is not applied twice. No extra presentation measurement is inserted in built-in rendering. One integer scope counter is added per UiImmediateContext; no per-widget fields or new text caches are added.

Text content determines its intrinsic size. Existing source semantics are preserved: measurement reads its content provider once, and a widget render reads it once. Providers must be read-only; event handlers, input and rendering are not part of measurement. This differs from Progress's source-independent geometry contract and must not be described as avoiding all text-provider reads. Static strings, function sources and inherited ReadText overrides keep their existing lifecycle and invalidation.

```csharp
var text = Fluent.Text("Several words wrap at the assigned parent width.");
var allocated = Dux.SizedBox(text, 80, 64);
// Custom pure measurement uses the same static presentation contract:
// context.MeasureText<MyTextPresentation>(content);
```

Original-source reproduction reports an 80x64 parent versus a 13.330078x20 actual text root. Current .NET11 RC3 managed qualification passes321 checks:CPU47,232 variant pixel comparisons,40 independently paced retained-frame comparisons and2 reuse budgets. Raster/vector,Light/Dark,MSAA1/4 and three frame slots produce545 readbacks including warmup. The retained test changes FPS every16 frames and NF every3,then changes clipping width/height. Fonts,geometry quality and workload are not reduced. No FPS/Private/allocation improvement is claimed; context-size and native-code-size costs are unmeasured. Native/cross-runtime checks remain pending the absent C++ tool discovery file. Evidence:artifacts/text-allocation-2026-11-10/verification.json.

New local sample:layout_text_allocation_fba.cs,with Width/Height and Short/Empty/Reset. Current local source is required;this API/sample is not in the existing NuGet package. [Run the verified sample](widget-gallery-run.md). Full official text decoration/alignment/accessibility fidelity remains outside this allocation checkpoint.


Last synced: 2026-11-10

## Text lifetime and idle invalidation correction

Common `UiText<TSource,TSourcePolicy,TPresentation>` publishes Source/Color before requesting a frame. Equal assignments remain idle. Static strings normalize null to empty before comparison; replacing a getter does not evaluate it. Dispose releases owned content/getter and rejects subsequent mutations. Generated Text, typography presets and user subclasses share this base. Framework source release runs separately even when user OnDispose omits a base call or throws.

The explicit static-layer ContentVersion contract, rendering policies, fonts and quality remain unchanged. An explicitly rebuilt original-source control fails the new idle-notification assertion. The initial copy-only control reused the fixed assembly because of preserved timestamps and is excluded as evidence.

Fixed .NET10 focused63 (text31+typography32), full1108, and pinned .NET11 RC3 NativeAOT text31 checks passed. Reassigning unchanged content/color10000 times allocates0B and requests0 frames. This is not whole-app FPS/Private/Pi performance evidence. net8/9/10 build:0 warnings/errors. Logs and source/executable hashes: `artifacts/text-lifecycle-2026-11-05/verification.json`.

## Default typography follow-up

Generated `Fluent.Text` and `Dux.Text/TextColored/TextWrapped` now use actual Regular Body1 typography (14px/20px), with wrapping enabled by default. Existing source generation binds source/presentation directly to closed generic types. Static, dynamic, inherited widgets and `ui.Text<UiFluentTextPresentation>` use the same presentation. Wrapping is measured inside the selected font scope; no runtime theme-selection branch is added.

User presentations can call `TypographyWrapped<TTypeface,TMetrics>`. Missing metric/font configuration fails explicitly. App defaults supply Segoe UI and an explicit Korean font; low-level UiContext consumers must configure DirectText, a metrics backend and UiTypographyFonts.

Existing nongeneric `ui.Text/TextColored/TextWrapped` preserve surrounding context fonts through explicit UiContextTextPresentation/UiContextWrappedTextPresentation policies. Complete default migration of those APIs remains open. These are compatibility paths, not automatic substitutions for unsupported Fluent typography. Initial implementation/results below are historical.

Pinned official useText.ts sets wrap=true; useTextStyles.styles.ts selects base300 size/line-height and Regular. Sources/hashes are preserved under artifacts/fluent-default-text-2026-11-05. Validation covers actual raster2D/vector3D pixels against Body1, Korean, dynamic changes, narrow wrapping and font-scope restoration. Italic, underline/strikethrough, alignment/truncation, semantics and complete official visual fidelity remain open. Whole-app FPS/Private gains are unmeasured.

String/function overloads of `Dux.Text`, `TextColored` and `TextWrapped` now use inheritable widgets while preserving `IUiView` return contracts. Standard `Fluent.Text(string)` directly constructs `UiFluentText`; `Fluent.Text(Func<string>)` constructs `UiFluentDynamicText`. Wrapping uses generated `UiFluentWrappedText`/`UiFluentDynamicWrappedText`. Discovery deduplicates Text across input variants.

## Compile-time binding and extension

`UiText<TSource,TSourcePolicy,TPresentation>` fixes both content sourcing and presentation as concrete types. Strings use `UiStaticTextSource`; functions use `UiGetterTextSource`. Rendering has no string-versus-function selection branch or unused function field. A function source executes once per rendered frame; its callback is never skipped as a caching optimization. Invalid source/policy combinations produce DUXWGT004.

Users inherit `UiFluentText` or `UiText<TPresentation>` and override `ReadText()`. Presentation implements `IUiTextPresentation.Render`. UiControlContext's MeasureText, PlaceText, LayoutText, WrapText, PaintText and drawing primitives allow different measurement, placement, backgrounds, markers and paint algorithms. ShowcaseNoticeText in widget_tree_theme_fba.cs is a generated user widget with distinct padding/background/marker composition, rather than a numeric color preset.

Existing nongeneric Text/TextColored/TextDisabled/TextUnformatted and TextWrapped preserve context-font policies. Typed Fluent immediate APIs and generated widgets select Body1. Both reuse table placement, item clips, layers, textures and common DirectText/atlas drawing without a second renderer or automatic presentation substitution.

## Official scope and remaining work

The [official Text guidance](https://fluent2.microsoft.design/components/web/react/core/text/usage) was checked on2026-11-05. It covers font selection, alignment, typography presets and semantic information; this migration does not complete that specification. The API reference is react-text.api.md at pinned commit8830c10bfb2f4c1bdd540287b09713b9679cd1ad.

Default Fluent Text now uses Body1 from the [17 typography presets](fluent2-typography.md), as described above. Italic/underline/strikethrough, alignment/truncation, native semantics/accessibility and official visual comparison remain open. A Preview page title is not a package release-channel claim. The five discovery names at the validation below have since grown to23;289 known requirements still do not establish the final overall denominator.

## Verification and costs

Default typography follow-up: fullNET10 963, shared managed/fixedRC3 Native67 checks, net8/9/10 Release zero warnings/errors. All417 preceding images match; six new Text images cover raster/vector Body1 output and content changes. One check description differs in allocation values:1,024 constructions use114,688→90,112B managed and98,304→90,112B Native. Comparisons are valid within each runtime. This reconfirms existing generated-factory cost, not a speed gain from the font change.

Common wrapping returns the original fitting single-line string, avoiding word strings/output builders, and resets measured width at explicit newlines. A resident-text test incorrectly rejected retirement of unrelated idle fonts created by the new raster tests; it now checks textures actually referenced by the resident list. Its650-frame/pixel checks remain intact. The initial Korean vector fixture omitted the explicit Korean face; configuring the same font set as app defaults fixes it without reducing content or substituting glyphs. Final evidence: artifacts/fluent-default-text-2026-11-05/verification.json.

.NET10 full815 checks, final focused103 checks, generator66 checks and net8/9/10 solution builds pass with zero warnings/errors. Checks cover vertex/index/primitive/command equality including clip/texture, wrapping/table/clip scopes, per-frame getters, inheritance, alternative presentation, mutable content and cursor movement.323 existing images match the preceding implementation; this is not an official Fluent screenshot comparison.

The .NET10 construction probe reproduces the actual old parameter-capturing factory body:1,024 objects allocate114,688→90,112B(-21.43%). This is not frame-time, Private memory or NativeAOT speed evidence. The initial fixture incorrectly used a constant lambda and was corrected to the original capturing body. PaintText has a distinct name to avoid ambiguity with existing target-typed new calls to Text.

Fixed RC3 NativeAOT native code generation, first GPU presentation and exit0 are verified. Final source-kind selection is also a closed type. The preceding B2C61 prototype executable/results remain preserved. Startup does not prove complete interaction, visual fidelity or performance. The prior RC3 full GPU mismatch and extra retained-ID cache residency remain unresolved.

Evidence: artifacts/fluent-text-2026-11-05/full-final-net10, widgets-final-typed-net10, generators-final-net10, image-comparison-final.json, native-final-run.json and verification.json. Local execution uses `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache`; saved RC3 validation uses dedicated pinned tooling/output paths.

2026-11-12 follow-up — [Content measurement contract](widget-cpu-retention.md): deterministic sources/presentations participate in parent retention; dynamic string/badge getters and custom traits default to fresh measurement. Declared getter Progress geometry and stable Spinner visibility are separate promises; rendering/getters/animation/navigation continue. Focused26 and exact GPU comparisons qualify this scope, not full NativeAOT/performance. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.
