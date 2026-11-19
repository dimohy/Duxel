# Textarea implementation status

[Shared text drag ownership](text-drag-ownership.md): single-line and multiline now share explicit press ownership and a timestamp; focus-only held input cannot select. CPU444/multiline2560 pass, including context/scrollbar lifetime and pointer GPU checks. Full OS capture/word-drag/partial-occlusion/native fidelity remains open.

[Multiline source elements](multiline-elements.md): plain editing now keeps combining/supplementary elements intact; all modes align between same-frame edits and queued IME. CPU393/multiline2464 pass; prior input693/multiline3457 images match. [Native status](native-toolchain-status.md) now identifies the historical P: toolchain as unavailable here, with its current location unconfirmed.

[Shared keyboard commands](text-commands.md): Ctrl+Backspace/Delete and held keys remove complete word ranges; multiline Left/Right collapse selections. CPU301, input-kernel304, multiline2336 pass. Full native/Unicode/OS conformance remains open.

The [nonwrapping source-geometry integration](multiline-hard-lines.md) now preserves readonly CRLF/CR ranges and supplies text-element navigation, both scrollbar gutters and endpoint caret painting for readonly/Fluent editors. CPU204/GPU2208 pass with 3,025 unchanged prior images. The sample includes Wrap switching and raw delimiter preservation.

The shared [text edit integrity rules](text-edit-integrity.md) now prevent partial incoming text elements at the length limit and reset stale history after external model replacement. Accessibility Value remains undoable. These paths pass CPU175; complete Unicode/bidi and actual OS input qualification remain open.

`Fluent.Textarea(...)` now constructs the generated `UiFluentTextarea` widget. It uses the [wrapped multiline foundation](multiline-wrap.md), common Tab/Shift+Tab navigation, Field label activation and accessibility Focus/Value actions. Full Fluent fidelity, actual OS input, current Native and integrated performance qualification remain incomplete.

```csharp
var notes = Fluent.Textarea("Notes", text.Binding, 4096, width : 400, height : 140);
notes.Options = new()
{
    Placeholder = "Write a note...",
    Flags = UiInputTextFlags.AutoSelectAll,
    Invalid = false
};
```

`UiMultilineInputOptions` applies to generic widgets and a new generic immediate overload. Existing signatures remain available. `Flags` supports ReadOnly/AutoSelectAll; `Wrap` is nullable so presentations retain their default unless explicitly overridden. Unknown flags are rejected. Field invalid state is merged without mutating widget options and standalone invalid state is published to accessibility.

`UiFluentTextareaPresentation<TSize, TAppearance>` supports outline, filled darker and filled lighter through existing input appearance types; underline is explicitly unsupported. Size types use the following root geometry. Default heights include the textarea minimum, two outer border pixels and two pixels reserved for the focus indicator. These are source-derived geometry choices, not a browser screenshot equivalence claim. The [official type ramp](https://fluent2.microsoft.design/typography) and [typography token styles](https://github.com/microsoft/fluentui/blob/master/packages/tokens/src/global/typographyStyles.ts) inform the line boxes.

| Size type | Default root height | Horizontal / vertical padding | Font / line height |
| --- | ---: | ---: | ---: |
| `UiFluentSmallTextareaSize` | 44 | 8 / 4 | 12 / 16 |
| `UiFluentMediumTextareaSize` | 56 | 12 / 6 | 14 / 20 |
| `UiFluentLargeTextareaSize` | 68 | 14 / 8 | 16 / 22 |

Readonly editors preserve the caller's value, including CR/LF, and allow selection/copy/navigation/scrolling. Cut, paste, undo, redo, deletion, Enter, character events and committed IME input cannot mutate it. Readonly IME commits are drained and composition ownership is released. CRLF is one text element and a hard break in source-indexed modes. Placeholder rows are separately owned display data, clipped to the content; they do not enter the value, undo history or scrollbar extent. Readonly and Fluent nonwrapping modes use owned hard rows; standard unwrapped editable behavior retains its existing path. See [nonwrapping source geometry](multiline-hard-lines.md).

Fluent Tab navigates without inserting spaces; the standard multiline behavior keeps its existing Tab insertion. Enter inserts a hard break and Escape preserves Fluent focus. Captured scrollbar drags retain their owner while the editor participates in keyboard navigation. `samples/fba/fluent_textarea_fba.cs` demonstrates generated construction, three sizes/appearances, readonly, placeholder, Field validation and live allocation changes. Evidence is recorded under `artifacts/fluent-textarea-2026-11-11`; the catalog name indicates exposure, not full conformance.

The [official usage guidance](https://fluent2.microsoft.design/components/web/react/core/textarea/usage) describes long-form text, a fixed default size and scrolling overflow. The [React type contract](https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-textarea/library/src/components/Textarea/Textarea.types.ts) defines three sizes, outline/filled appearances and optional resize directions. Exact downloaded reference snapshots and hashes live in `artifacts/textarea-kernel-2026-11-11`; these are observed sources, not a claim of complete conformance.

## Current foundation

The existing `InputTextMultiline` signature remains available. Its allocated geometry, editing result, frame/text paint and scrollbar completion are now separate. Finite field geometry and positive line height are validated before creating editing storage; the public height overload also rejects nonfinite values before advancing the cursor.

`UiMultilineInput<TBehavior, TPresentation>` composes `IUiMultilineInputBehavior` and `IUiMultilineInputPresentation`. Presentation measurement/arrangement uses metadata without reading the binding. Rendering reads it once, processes the same editor and paints inside one parent-assigned root. `UiMultilineInputLayout` includes an explicit content rectangle and line box; `UiMultilineEditResult` carries the edit snapshot. `UiMultilineInputOwner` owns the editor history, both scrollbar axes and drag timing across context transfer and disposal. Content shrink clamps offsets and releases disappearing captured scrollbars; edits update overflow metrics in the same frame.

The common kernel consumes each wheel axis only when it moves. The expanded CPU suite passes 37 checks, including pure measurement, a 20px typography line box, editing/undo through allocation changes, context transfer, disposal, disabled interaction, queued IME commit handling and tiny roots. GPU checks pass 192 assertions: 32 legacy replay checks and 160 custom-presentation comparisons against independent frame/text drawing across raster/vector, Light/Dark, MSAA 1/4 and acquired-image reuse. The 41 legacy image files remain identical to the pre-change build when their original capture order is preserved. Inserting extra frames between the old MSAA groups changed scrollbar-recency opacity in five images; preserving the original elapsed-frame sequence resolved the comparison setup.

`layout_multiline_contract_fba.cs` consumes the public API from a generated custom style, with a real editable model, width changes and long/empty content. Its palette demonstrates an extension contract; it is not the Fluent Textarea appearance. This new type is classified as infrastructure and adds no Fluent factory/catalog name. Standard behavior supports pointer-owned multiline editing and accessibility Value actions. Accessibility focus remains unavailable.

The accessibility extension passes CPU93 (27 new checks), the existing accessibility suite passes 40 checks, and multiline GPU320 still passes. Evidence: `artifacts/multiline-accessibility-2026-11-11/verification.json`.

`UiAccessibilityNode.Multiline` is an opt-in property; its default preserves single-line validation. Compiled multiline nodes set it and `UiStandardMultilineInputBehavior.AccessibilityValue` advertises the implemented action. Value requests allow CR/LF and use the editor's existing CRLF/CR-to-LF normalization, one binding write, undo/redo and Field metadata. Tabs, NUL and other control characters remain invalid. Both enqueue and consumption validate availability and maximum length; a queued request cannot bypass later disabling, clipping or a reduced maximum. Requests retain arrival order and rejected requests do not replay. The published maximum applies to the incoming UTF-16 string before normalization. This verifies the managed tree/editor connection, not external Windows UIA client behavior or complete Textarea accessibility.

## Open work

The implemented component still needs full fidelity of the [wrapped visual-line foundation](multiline-wrap.md), external Windows UIA/actual IME qualification, complex scripts, bidi/ligatures and full Unicode/browser editing semantics beyond the qualified incoming-element, external-model history and hard-line contracts. Shared palette/GPU references verify internal composition and do not prove complete official size/appearance/state fidelity. User-deferred resize stays deferred. Current Native/runtime comparison and whole-app performance, including retained row/placeholder memory and rebuild cost, remain open.

The separate lifetime regression exposed a managed allocation-test instability: the qualified default-JIT build failed the unchanged button same-value allocation assertion in two of three bounded repeats, while the baseline passed three. Both builds passed with tiered compilation disabled and with tiered PGO disabled. A [follow-up measurement correction](allocation-validation.md) now warms the same isolated loop and passes 101 lifetime checks in three independent default-JIT runs without changing the zero-byte assertion or product code. Earlier failures remain retained; their precise cold-runtime allocation source is unproven.

Local sample qualification replaces the package directive with current in-tree project references and explicit generator analyzer metadata. The sample requires this source revision; availability in older published packages is not established. The final managed app ran 15 seconds with 30 programmatic layout/content changes and exited successfully. This is not manual OS-input or performance qualification.

## Ancestor measurement retention — 2026-11-12

`IUiMultilineInputPresentation.CanRetainMeasurement` and `IUiTextareaSize.CanRetainMeasurement` default false. Built-in Textarea sizes and supported deterministic Input appearance traits opt in. Measurement depends on extents/options/text environment, never the editor binding. The existing parent cache can retain this geometry while actual rendering continues value validation, editing, wrapping, selection, scrolling, caret/IME and undo. Dimension/options/environment changes invalidate the original ownership chain. Custom size/appearance/presentation policies remain dynamic unless they explicitly promise stable measurement. No new editor cache or fields are added. Evidence: artifacts/delegated-layout-retention-2026-11-12/verification.json.
