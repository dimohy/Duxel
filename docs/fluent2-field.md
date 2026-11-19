# Fluent 2 Field

`Fluent.Field(control, controlSize, options)` owns a real `UiWidget` and composes its label, required indicator, validation message and hint. `Dux.Field` and the immediate `ui.Field<TBehavior, TPresentation>` share this implementation. Default and user widgets follow the same ownership, input and rendering paths.

```csharp
var field = context.Child("email", () => Fluent.Field(
    Fluent.Input("Email", email.Binding, 256, 220),
    new(220, 32),
    new() { Label = "Work email", Hint = "Use your work address", Required = true }));
field.Options = field.Options with {
    ValidationMessage = email.Value.Length < 3 ? "Enter at least three characters" : null
};
```

Give the child one owner. Registering the same input in the outer `context.Child` also is rejected as shared ownership. Disposing the Field before its first render still disposes its child. To replace the control, compose a Field under a new key.

Inherit `UiField<TBehavior, TPresentation>` or generate a closed class with `UiWidgetStyle`. The generated default `UiFluentField` binds `UiLabelFieldBehavior` to `UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>`. Three label sizes and horizontal/vertical layouts are selected by types without a runtime theme registry.

Label clicks use actual child focus handling. An Input claims focus for its editor before its adornment buttons. A button receives focus without activation. Existing controls retain keyboard and callback handling. `Options.Disabled` disables only the child; labels and descriptions remain readable. Outer disabled scopes also block interaction.

Validation states are `None`, `Error`, `Warning` and `Success`. A nonempty message defaults to Error when no state is supplied. Errors reach the Input presentation without mutating its own Options. `ui.CurrentField` and `UiControlContext.Field` expose label, descriptions, required and validation metadata to user controls, restoring scopes on nesting and failure. The [common accessibility provider](widget-accessibility.md) now exposes primary Input metadata through actual Windows UIA. External managed/NativeAOT clients verify14 Field cases including validation changes and physical bounds. Events and screen-reader announcements remain open.

Alongside explicit allocation,`Fluent.Field(control)` provides natural measurement. Width is outer Field width;0 consumes available width. Actual heights come from built-in form controls or user measurement implementations. All-container constraint negotiation remains open. Horizontal layout uses the official33percent label column;descriptions follow the control column. See the [common measurement contract](widget-measurement.md).

The official presentation retains measured lines in a widget-owned cache. Retention limits are1024 source characters combined,4096 derived characters,256 lines and1024 characters per font path. Larger content renders at the same quality without retaining its layout after the frame. Width, options, fonts, text settings, scale, backend generation and context changes invalidate it; disposal clears references. Context references are weak. Immediate callers can own and dispose `UiFieldContent<TPresentation>` for reuse. Extra cache/layout storage has a cost; application CPU,Private and FPS improvements are not yet claimed.

Sources are [Field Preview](https://fluent2.microsoft.design/components/web/react/core/field/usage) and Fluent UI commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`. The actual `useField.tsx` selects `DiamondDismiss12Filled`, unlike the stale ErrorCircle comment in its types file. Connecting U+F0A11 exposed and fixed the Windows text path's BMP restriction. DirectWrite's UCS4 input and16-bit output glyph indices have different ranges. Raster and vector paths now use the actual supplementary glyph.

Initial evidence lives in `artifacts/fluent-field-2026-11-06/`; current natural measurement and label-column clipping evidence lives in `artifacts/widget-measurement-2026-11-06/`. NET10 input,ownership,cache,UCS4 and full checks passed. GPU readback compares generated/immediate paths across three sizes,two orientations,five states,two themes,MSAA1/4 and raster/vector rendering. This does not prove official browser parity. Full official fidelity,UIA,RTL,forced colors,motion,info buttons/arbitrary slots,all-container measurement and Pi qualification remain open.

```powershell
.\run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' -ExtraArgs @('--field')
```

## Parent allocation — 2026-11-10

The generated Field widget now opts into actual parent allocation. Its root, control, label target, descriptions and root clipping share the assigned region. Actual width is applied before text measurement, including the horizontal 33/67 columns. Remaining height goes to the real control after reserving label and description rows. Fonts/icons stay unchanged; short/zero parents clip existing content rather than shrink typography. Declared Width/ControlSize is not mutated.

```csharp
using var field = Fluent.Field(Fluent.Input("Email", email.Binding, 128));
field.Options = new() { Label = "Email", Required = true, Hint = "Work address" };
using var box = Dux.SizedBox(field, 180, 120);
```

IUiFieldPresentation.Arrange(context, options, natural, allocatedSize) must explicitly support changed parent sizes. For constrained explicit ControlSize, FieldWidth(controlWidth) declares natural outer width, including any label column; its default fails explicitly. ControlWidth(fieldWidth) is the forward mapping. Built-in horizontal fields implement both; natural-child fields already start from the known outer Width. Invalid metadata/extents or an incorrect arranged root fail before label/input behavior.

The existing bounded UiFieldContent caches natural geometry using effective control width. Arrange works on a local copy, preserving the natural cache; height-only parent changes do not remeasure field text. Public UiControlContext.MeasureField(control, controlSize, width, options, content) exposes the same pure path for user wrappers. New immediate Field/AutoField overloads accept the explicit constrainLayout argument after content. Existing immediate overloads and standalone natural rendering preserve their prior behavior. Actual owned-child stretching applies when a real parent allocation is active.

Measurement reads no input binding and invokes no input/event behavior. Required/invalid/disabled/name/hint metadata reaches the real editor; Core accessibility bounds follow that editor. Label focus, typing, undo and editor identity survive parent changes. Nested fields retain nearest metadata and restore outer scopes. Independent Panel over arbitrary IUiView explicitly rejects generic leaf measurement before reading its Open binding; its closed direct-root behavior remains. This boundary check does not qualify every floating-panel lifecycle scenario. No widget/context fields, new caches, runtime registry or source snapshots were added.

Current fixed .NET11 RC3 managed Field qualification passes 1018 checks: CPU/semantics/lifetime 64, existing Field GPU 480, new allocation pixels 432, paced retained pixels 40 and reuse budgets 2. All three label sizes, both orientations, natural/explicit sizing, empty validation icons, disabled, short/zero bounds, raster/vector, MSAA 1/4, Light/Dark and three frame slots are covered; 1425 readbacks include warmup. Distinct FPS16/NF3 label/description updates and root clip changes retain exact images. The expanded table/column regression adds Field and passes 1563 checks (CPU1115, GPU448, 897 readbacks). These are separate scopes and are not summed as unique requirements.

The original diagnostic measured 80 x 64 but rendered 80 x 32; the same diagnostic now renders 80 x 64. New layout_field_allocation_fba.cs includes generated vertical and user-generated horizontal fields, Width : 180, Height : 180 and Validation controls. Identical-body local-reference compilation, first presentation 543.94ms and 15-second exit0 pass. The initial local FBA reference omitted Analyzer metadata; sample-validation.targets supplies standard OutputItemType=Analyzer/ReferenceOutputAssembly=false metadata before reference resolution and preserves emitted AllocationHorizontalField source. No generator implementation was changed to make the sample pass.

Evidence: artifacts/field-allocation-2026-11-10/verification.json, qualified-build.log, managed-qualified/result.json, routing-regression/result.json and sample-generated. Manual OS interaction, external Windows UIA, official visual acceptance and current Native/cross-runtime qualification remain open. Native prerequisites remain unavailable in checked standard paths; alternate L: paths deny enumeration as recorded in the earlier toolchain audit. No performance or whole-app memory improvement is claimed. Container stages stay 3/6, resize remains user-deferred and .NET9/10 deployment compilation stays deferred.

Constrained measurement obtains intrinsic child height under an unbounded vertical axis, supporting shrink-wrapped Columns and vertical scrolling measurement. Two original failures were reproduced and fixed using MeasureConstrained rather than inventing height. Natural Field needs a finite outer width or declared Width/ControlSize.

## Custom measurement traits — 2026-11-12

`IUiFieldSize.CanRetainMeasurement` and `IUiFieldOrientation.CanRetainMeasurement` default to false. The generic Fluent presentation reuses its bounded natural layout only when both traits explicitly promise stable measurement. Built-in sizes and orientations opt in. Mutable custom spacing/orientation is remeasured; it no longer silently inherits the built-in cache contract. Existing input rendering and ownership continue unchanged.


## Parent measurement retention — 2026-11-12

Field now propagates measurement retention only when its presentation ReuseLayout and the owned control RetainsMeasurement both allow it. Explicit ControlSize alone does not authorize skipping arbitrary child measurement. Natural Field rendering now shares validated child sizing with measurement. Known unchanged owned controls can reuse that result; opaque controls and legacy immediate AutoField retain live measurement. Rendering, field metadata, focus and editing remain live.

Evidence: artifacts/form-layout-retention-2026-11-12/verification.json. Observer: ./run-gallery.ps1 → Measurement reuse (측정 재사용).
