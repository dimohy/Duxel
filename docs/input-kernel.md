# Single-line input engine separation and lifetime

[Single-line drag](input-drag.md): an owned press continues selection outside the field with horizontal autoscroll; release,hidden/clipped/disabled owners and higher popups end capture. CPU336/input-kernel400 pass, with all 357 prior input and 3457 multiline images unchanged. Actual OS capture and complete text/native fidelity remain open.

[Shared keyboard commands](text-commands.md): Ctrl+Backspace/Delete and held keys remove complete word ranges; multiline Left/Right collapse selections. CPU301, input-kernel304, multiline2336 pass. Full native/Unicode/OS conformance remains open.

The [source-element integration](single-line-elements.md) adds buffer-owned boundaries for keyboard, repeats, pointer hits and selection. CPU231/input-kernel240 pass, retaining earlier image baselines. Full shaping/bidi, actual OS input and current Native/performance remain open.

The shared [text edit integrity rules](text-edit-integrity.md) apply to both Input and Textarea: bounded incoming text-element prefixes, selection replacement at the limit, external-model undo reset and undoable accessibility Value transactions. The expanded CPU suite passes 175 checks. Complete Unicode navigation/shaping and current Native/performance remain open.

2026-11-10 [Keyed virtual list](virtual-list.md): fixed-height vertical rows use double offsets and viewport-local placement for billion-item sources. CPU43 and expanded routing2317 checks pass; rows outside overscan dispose through existing ownership. Variable-height/other axes, cross-key recycling policy, external UIA and current Native remain open; container stages stay3/6. Sample: `layout_virtual_list_fba.cs`.

2026-11-10 [Owned ScrollView](scroll-view.md): Dux.ScrollView owns finite viewport measurement, real child placement, offset, scrollbar lifetime and invalidation. CPU47 and separate GPU/routing1933 checks pass. World-coordinate precision loss fails explicitly; rebased large content/virtualization and current Native remain pending. Container stages stay3/6. Sample: `layout_scroll_view_fba.cs`.

2026-11-10 [Nested scroll routing](scroll-routing.md): boundary handoff on independent wheel axes, caller clip and popup/window-order hit testing, shared scrollbar capture/release, and measured DirectText window steps. Expanded managed regression passes 1,645 checks and 1,025 readbacks; container stages remain 3/6. Owned scroll widgets, virtualization and current Native remain pending. Sample: `layout_scroll_routing_fba.cs`.

Last synced: 2026-11-11

The separated kernel is used by Fluent Input and Textarea as well as custom widgets. Behavior, measurement, placement, painting and ownership share the same editor; this foundation does not establish complete Fluent conformance.

```csharp
[UiWidgetStyle(typeof(UiInput<,>), typeof(UiStandardTextInputBehavior),
    typeof(ProductInputPresentation))]
public partial class ProductInput;

var field = new ProductInput("Name", text.Binding, maxLength : 256, width : 240);
```

The generator forwards the actual closed `UiInput<TBehavior, TPresentation>` constructor. Inherit the generated class to extend `OnChanged(string)`. Idle frames do not write bindings; multiple characters in one frame produce one final-value callback. Properties are `Label`, `Value`, `MaxLength` and `Width`, with common identity/disposal. The caller specifies maximum length.

`IUiTextInputBehavior.Process` handles editing. Its ID is already resolved. `UiControlContext.EditSingleLine` reuses selection, caret, horizontal scroll, clipboard, undo/redo and IME processing. Custom behavior can delegate to this kernel or supply another algorithm.

`IUiInputPresentation` supplies a font scope, measurement, placement and painting before/after editing. `PaintBeforeEdit` preserves compatibility label missing-glyph callback order. Tests compare actual observation sequences and a deliberately late-label negative control. `UiInputLayout` separates total size, field/text bounds, label position and line height. `Place` selects default relative placement or another algorithm. `PaintEditableText` provides common selection/text/IME/caret rendering without forcing frame or child composition. Compatibility placement preserves original floating-point operation order, colors, spacing and draw order. Invalid layouts are rejected before editing storage is created.

Existing `ui.InputText(label, ref value, maxLength)` also consumes this generic kernel through explicit compatibility behavior/presentation. Numeric text-field skins remain in that compatibility path; they are not counted as a new Fluent implementation.

Direct calls can retain an owner:

```csharp
using var owner = new UiTextInputOwner();
ui.InputText<UiStandardTextInputBehavior, ProductInputPresentation>(
    "Name", ref value, maxLength: 256, width: 240, owner: owner);
```

Use one owner per logical field. It weakly references the context and releases that field's edit buffer, history, selection, caret and scrolling. Generated widgets own it automatically. Existing ownerless string API lifetime is preserved for compatibility. External bindings are not disposed. IME release failures propagate through existing widget AggregateException behavior while binding/editing references are released.

Unused history slots and every returned pooled array slot are cleared. This corrects string-reference lifetime without reducing undo depth or input content. It does not mean all pool or dictionary capacity is returned. Private/FPS improvement was not measured; generated code or entry counts are not speedup evidence.

Test-only `InputLegacyReference.cs` freezes the function before extraction. Core includes it only with `DuxelValidationHooks=true`, excluding it from normal library inputs. Tests compare idle/hover/active/selection/scroll/typing/label across light/dark, MSAA1/4 and raster/vector. A separate above-label/rounded-frame test presentation also matches generated/direct calls; it is not a new default theme. Initial tests accidentally held keys; their records are preserved and capture key state is now explicit.

IME uses a test double for composition, commit, caret and release failures. Real Windows IME candidate windows and OS input acceptance remain separate. Current counts and source/executable hashes are in `artifacts/input-kernel-2026-11-06/verification.json`.

Official Fluent Input sizing, appearance/states, placeholder, focus/Tab, readonly/flags, Field, accessibility, RTL/forced colors and whole widget/engine/Pi missions remain active. See the correction below: only None was previously defined, and new input options are now explicitly validated.

To exercise the current source through the existing full-feature app, run `./run-fba.ps1 samples/fba/all_features.cs -NoCache` from the repository root. This does not imply Fluent Input appearance or manual acceptance of a new executable.

Full2876/GPU2197 per configuration precede a test-only custom-presentation theme correction. Library code is unchanged; current managed/Native focused144 checks and117 exact images qualify the corrected scenes while unrelated results are reused. A normal Core rebuild has148 compile inputs and DLL metadata confirms exclusion of the frozen reference. The .NET10.0.14 metadata-inspection host is excluded from functional/performance results.

Correction (2026-11-06): the earlier UiInputTextFlags enum defined only None. ReadOnly/AutoSelectAll are new options, not previously supported flags that were lost. Unknown cast values now fail explicitly in the shared input contract; see fluent2-input.md.
