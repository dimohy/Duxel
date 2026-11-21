# Fluent 2 Input

In an active system contrast palette, selected plain text uses HighlightText over Highlight. The shared editor paints disjoint portions of the original shaped run, preserving the ordinary text color outside the selection and avoiding duplicate alpha at glyph edges. [Scope, GPU verification and styled/IME limits](system-forced-colors.md).

The context-aware `UiFluentInputPalette.Resolve(context, state, appearance, invalid, held)` consumes active system contrast colors. Enabled fields use Canvas/CanvasText and Highlight for the focus edge; disabled fields use GrayText and retain a transparent background. Underline also stays transparent. Input adornments, Dropdown chevrons and SearchBox icons share the same system foreground. The scheme-only overload still resolves authored Fluent colors. [Shared contract and remaining work](system-forced-colors.md).

[Single-line drag](input-drag.md): an owned press continues selection outside the field with horizontal autoscroll; release,hidden/clipped/disabled owners and higher popups end capture. CPU336/input-kernel400 pass, with all 357 prior input and 3457 multiline images unchanged. Actual OS capture and complete text/native fidelity remain open.

[Shared keyboard commands](text-commands.md): Ctrl+Backspace/Delete and held keys remove complete word ranges; multiline Left/Right collapse selections. CPU301, input-kernel304, multiline2336 pass. Full native/Unicode/OS conformance remains open.

The shared [single-line source-element rules](single-line-elements.md) now cover atomic movement/deletion, pointer alignment, directional selection collapse and repeat. Combined CPU231 and input-kernel240 pass. Sample: `fluent_input_elements_fba.cs`; complete shaping/bidi/OS fidelity remains open.

Last synced: 2026-11-16

Owned interactive child slots are now supported below. Earlier noninteractive-only statements and counts describe their recorded implementation stage.

Single-line input shares its editing kernel between default/user widgets and direct calls. Source generation binds `Fluent.Input` to medium Outline at compile time. Custom presentations inherit the same `UiInput<TBehavior, TPresentation>` and own their editing storage lifetime.

```csharp
var field = Fluent.Input("Name", state.Binding, maxLength: 256, width: 220);
field.Options = new(UiInputTextFlags.AutoSelectAll, Placeholder: "Type here");
IUiView view = Dux.Input("Name", state.Binding, 256, 220);

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior),
    typeof(UiFluentInputPresentation<UiFluentLargeInputSize, UiFluentFilledDarkerInput>))]
public partial class ProductInput;
```

`Label` supplies identity/name. `Fluent.Field` can provide the visible label, hint, required/validation metadata and primary-control accessibility association. A placeholder never replaces essential labels. `Value`, `MaxLength`, `Width`, `Options` are mutable and subclasses can extend `OnChanged(string)`. Retain instances with `context.Child` in Build. Disposal releases bindings, editing buffers, selection/caret and undo/redo storage.

The shared [text edit integrity rules](text-edit-integrity.md) define incoming UTF-16 length limits and external-model undo baselines. Accessibility Value changes remain undoable editor transactions. These fixes pass CPU175 across the combined single-line/multiline integrity suite; complete Unicode editing and actual OS input remain open.

Shared behavior handles Tab/Shift+Tab, pointer selection/drag, clipboard, undo/redo and IME. Enter/Escape preserve Fluent input focus. ReadOnly allows selection/copy/navigation and blocks mutation/cut/paste/IME. AutoSelectAll selects on new focus. Disabled and Tab blur release IME ownership. Fake IME tests are separate from actual OS candidate-window validation.

Small/Medium/Large use24/32/40px heights and Regular12/16,14/20,16/22 typography. Outline/Underline/FilledDarker/FilledLighter are closed generic presentation implementations. Runtime light/dark state resolves the palette. Outline draws neutral borders with an accessible bottom edge; Underline draws a square bottom edge; Filled uses surfaces and transparent borders. Focus uses a2px bottom edge, with error/disabled/placeholder colors. Empty text skips glyph lookup and composition runs share body line metrics.

Direct calls consume the same implementation:

```csharp
ui.InputText<UiFluentTextInputBehavior,
    UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput>>(
    "Name", ref value, 256, new UiInputOptions(Placeholder: "Type here"), 220, owner);
```

[Official Input usage](https://fluent2.microsoft.design/components/web/react/core/input/usage) is Preview. Styles use commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad` and existing hashed palette sources. Combined horizontal padding is8/12/18px plus the separate1px outline border. Sources, hashes and validation are in `artifacts/fluent-input-2026-11-06/plan.json` and `verification.json`.

Correction: the previous UiInputTextFlags enum defined only None. ReadOnly/AutoSelectAll are new features, not previously defined flags that were lost. Unknown cast bits now fail before layout/editing.

Run `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe -ExtraArgs @('--input')`. The gallery shares actual values across12 size/appearance combinations and includes readonly/disabled fields, without an internal Window. NET10 functionality is separate from fixed NET11 RC3 NativeAOT; no speed or Private-memory improvement is claimed.

Input conformance remains open: interactive contentBefore/After, focus/reduced motion, deprecated shadow variants, Field/UIA, RTL/forced colors, actual IME candidates and matched-condition browser visual/interaction comparisons. Catalog connection and generated/direct GPU equality do not prove complete official fidelity. All Fluent, engine and Pi goals remain active.

Validation: NET10 full2913/Input37/focused GPU229/generator115; net8/9/10 zero warnings/errors. Fixed RC3 Native full EXT2/core3 each2427, focused each229.193 matched400×300 focused images agree per Native mode;2097 full255×273 images agree between Native modes.1901/1905 historical full images are unchanged;four reflect the previously recorded test-theme correction. Core outputs66→67 preserve65 existing files; only catalog and new Input adapter differ. The managed full GPU rerun failed the existing12336B Badge allocation assertion. Current focused Input evidence and unrelated pre-IME GPU2421 evidence are reused separately, never reported as a final full GPU pass.

After the Badge probe improvement, managed fullGPU2428 passes and2097 images match each preserved Native full set. The earlier failure remains recorded and the12336B cause is unknown. Product source is unchanged. Follow-up evidence: artifacts/badge-allocation-2026-11-06/verification.json.

## Before/after content and compiled presentation composition (2026-11-06)

Default Input displays single-line text through `Options.ContentBefore`/`ContentAfter`. These are runtime content data, not value or style selection. Line breaks are outside this text implementation and fail explicitly before editing. Icons or different drawing algorithms are selected at compile time through `UiFluentInputPresentation<TSize, TAppearance, TBefore, TAfter>` and `IUiInputAdornment`.

```csharp
field.Options = new(Placeholder: "Amount") { ContentBefore = "$", ContentAfter = "USD" };

[UiFluentIcon("search", UiFluentIconStyle.Regular, 20)]
public readonly partial struct ProductSearchIcon;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior),
    typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput,
        UiInputIconAdornment<ProductSearchIcon>, UiInputTextAdornment>))]
public partial class ProductSearchInput;
```

An icon slot is fixed by its presentation type; a text slot is absent when null. `IUiInputAdornment` statically defines presence, measurement and painting. This is currently a noninteractive contract, not completed child-button input/lifetime composition. Custom presentations use the same common editor/renderer as default widgets. `IUiInputPresentation.MeasureContent` adds option-dependent measurement after base measurement; existing presentations retain their original layout.

Official root/input horizontal padding is6+2/10+2/12+6px, gaps2/2/6px and icons16/20/24px. Adornment foreground uses neutral3 or disabled. Root and editing bounds are separate: decoration clicks do not edit text, and selection/scroll/IME cannot overpaint adornments. Narrow inputs shrink to zero editing width while preserving adornment order and clipping overflow to the root. Content changes preserve editing identity; disposal releases option strings.

Text paint uses already measured bounds to construct its single-line block without a second measurement. No separate per-frame adornment objects or runtime type registry is introduced. Option references and layout value fields are added; whole-memory/FPS improvement has not been measured.

Current evidence: `artifacts/fluent-input-slots-2026-11-06/verification.json`. NET10 full2934/GPU3408/focused1209; fixedRC3 Native EXT2/core3 full3408 and focused1209 each. Existing2097 full/193 focused images are preserved; managed/Native2481 full and577 focused images agree per mode. Tests cover independent size coordinates, actual adornment ink, isolation from long selected text,40px narrow ordering, repeated1/50/400px widths and editing/disposal. GPU readback exposed narrow overlap; corrected output and earlier failure artifacts are retained. All67 Core generated files are unchanged. New consumer-generated icons/slots are executed by tests and gallery.

Gallery `--input` adds `$`/`USD` and a generated search icon/`items` suffix. Interactive child slots, Field/UIA, motion, RTL/forced colors, actual OS IME and matched-browser conformance remain open, together with all Fluent/engine/Pi goals.

Underline centers adornments in the content area excluding its1px bottom border. The follow-up review expands slot GPU checks to three sizes/two content kinds/four appearances. Final2481 full/577 focused images are compared within their respective matching dimensions.

## Owned child-widget slots

`BeforeSlot`/`AfterSlot` own default or user `UiWidget` instances. A button uses its existing input, presentation and callback instead of a separate imitation. Theme/behavior selection remains fixed in generated classes and closed generics; child composition/focus/value are runtime state.

```csharp
var input = Fluent.Input("Amount", state.Binding, 256, 220);
var clear = Fluent.Button("x", () => state.Value = "");
clear.Size = new(24, 24);
input.AfterSlot = new("clear", clear, new(24, 24));

// Move the same keyed instance without replacing its state.
input.SetSlots(input.AfterSlot, null);
```

Size explicitly allocates the child's region. Match the child's `Size` or equivalent to that region. Intrinsic measurement/constraint negotiation is not complete. Sizes must be finite and positive. Compose arbitrary content in a user `UiWidget.Build()`. One side cannot specify both string content and an owned child.

Keep keys and instances stable. Replacing an instance at the same key or sharing another parent's child is rejected before mutation. Use a new key for replacement and `SetSlots` for atomic movement/swaps. Self/ancestor cycles and duplicate keys/instances are rejected. Input owns slot children; do not also register them under the page's `context.Child`. Retain the Input under the page instead.

```csharp
var input = context.Child("input", () => {
    var field = Fluent.Input("Value", state.Binding, 256, 220);
    var button = Fluent.Button("x", () => state.Value = "");
    button.Size = new(24, 24);
    field.AfterSlot = new("clear", button, new(24, 24));
    return field;
});
return Dux.Row(input);
```

Children join existing Build ownership immediately; removal disposes them at the next Build. Disposing Input before its first render still releases adopted children. Build-time slot mutation is rejected before changing the graph. Default and user widgets share the same UiWidget key/lifetime path. Protected `OwnChild`/`ValidateOwnedChild` and `RenderBuiltContent` reuse existing Build; ordinary leaves keep their direct path.

Input order is prefix/editor/suffix. Real Enter/Space/pointer behavior, keyboard focus, parent-disabled propagation and removal while pressed are preserved. Moving from the editor to a button releases IME ownership and paints focus-within in the same frame. Child layout/clip/available area are bounded and parent cursor/item metadata restored. Custom presentations declare `SupportsChildren` and use `HasBeforeChild`/`HasAfterChild`, `Before`/`After` in `MeasureContent`. Unsupported presentations reject adoption.

Child input is processed first, parent paint follows, then existing child draw lists are transferred above it without another vertex/index copy. Capture builders are shared per context, retaining at most8 nesting depths; deeper nesting uses transient builders with identical rendering. Failed child captures are discarded; context disposal clears retained scratch. Whole CPU/Private/draw improvement has not been measured. Child actions that update external bindings follow the existing next-frame invalidation model.

Gallery `--input` now clears the shared value through a real generated Fluent dismiss-icon button. Current validation/executable/hashes are in `artifacts/fluent-input-children-2026-11-06/verification.json`. Full browser conformance, intrinsic measurement, Field/UIA/motion/RTL/forced colors/actual OS IME and whole engine/Pi goals remain open.

Final interactive-slot validation: NET10 full2959/input82/GPU3561/focused1362; fixedRC3 Native EXT2/core3 full3561 and focused1362 each. Managed/Native2737 full/833 focused images agree per mode; existing2481 full/577 focused images are preserved. All four appearances compare rest, prefix press, invalid suffix press and disabled state against independently ordered parent/real-button painting. All67 Core generated files are unchanged. A misrendered progress table was corrected from Panel6/7 and DirectText6/6 ledgers through server report repair; no policy asset changed.


## Parent measurement retention — 2026-11-12

Known Fluent Input sizes, appearances and adornments now explicitly opt into parent measurement retention through CanRetainMeasurement. Unknown custom traits and the compatibility presentation default false. Measurement uses label/options/width and declared slot allocations; it does not consume editor values or child measurement. Owned slot widgets still render normally, even when they are opaque to measurement. Typing, undo, IME and caller bindings continue through the existing editor path. Property and slot changes invalidate the original ownership chain; no extra leaf cache is added.

Evidence: artifacts/form-layout-retention-2026-11-12/verification.json. Observer: ./run-gallery.ps1 → Measurement reuse.


## TextField

`Fluent.TextField(label, text.Binding, inputOptions: ..., field: ...)` wraps the shared single-line editor in a labeled Field. Use `UiFieldOptions.Hint`, `Required` and `ValidationMessage` to provide persistent guidance and application validation. The application decides when to validate. An editable field shows Clear when focused or nonempty. Clear updates the binding and returns focus to the editor. Read-only mode hides Clear. RightToLeft mirrors the trailing clear slot. The Gallery includes independent short-text and submit-validation examples with current source below each. [Fluent iOS behavior reference](https://fluent2.microsoft.design/components/ios/core/textfield/usage).
