# List

Linux native Selection uses shared child-selection actions, with original-child and selected-subset indices kept distinct. Child state and parent selection events update external caches. The common action gate protects the last selected child of a published required owner while permitting one of several required choices to be removed. The lower Linux city lists demonstrate single/multiple selection. Current app35/native48 per runner/policy7/control99 evidence: `artifacts/linux-accessibility-2026-11-14/selection-verification.json`. Full widget/physical qualification remains open. Bulk requests preflight known refusals, then commit through live bindings in order; a later refusal or concurrent hierarchy change can retain earlier commits.

`Fluent.List` returns a generated widget inheriting `UiList<UiFluentListPresentation>` with owned `UiListItem` rows. Plain, single and multiple selection have separate constructor overloads. The catalog contains59 factories,35 of55 official top-level names connected and20 missing; these counts do not establish complete Fluent conformance.

```csharp
var selected = new UiState<string?>("a");
var list = Fluent.List("Documents", selected.Binding,
[
    new("a", "First document", Fluent.Body1("First document"), selectionLabel: "Select first document"),
    new("b", "Second document", Fluent.Body1("Second document"), selectionLabel: "Select second document")
], new(Width: 320));
```

Omit the binding for a plain list; use `UiBinding<string[]>` for multiple selection. Rows require stable unique values. Selection reuses `UiOptionSelection` and reads rejected writes back. Single selection can toggle off. The list owns and disposes arbitrary row content.

Navigation is Off, Items or Composite; selection implies Items when Off is specified. Off preserves independent child controls. Items requires noninteractive content; use Composite for inputs or buttons. A roving Tab entry uses vertical arrows, Home/End and remembered focus. Composite horizontal arrows enter/traverse children; Escape returns to the row. Arrow-consuming editors retain their editing keys. Tab exits the list.

Space toggles selection without a row action. Enter and row clicks call `Action` first; `UiListItemAction.PreventSelection` suppresses default selection. Checkmarks and secondary controls do not invoke row actions. `DisabledSelection` blocks selection while an explicit row action can remain available. Native selection requests use the same binding authority.

Run `./run-gallery.ps1` → **List** for plain, single and multiple selection, a real input, row commands and independent child commands. All three examples consume the generated factory.

Native structure maps List/ListItem, ListBox/Option and Grid/Row/GridCell roles and selection semantics. Composite lists publish Grid/GridItem patterns, dimensions, logical coordinates, spans and containing-grid relationships. Column zero is the selection cell when present, at the leading edge in either direction; content occupies the remaining column. Rows describe their full extent while GetItem returns the actual child cell. Empty content still has a cell provider. Focused CPU tests qualify hidden/disabled/disposed/context-transfer and editor key propagation. A separate Windows UIA client checks real cells, selection, editing, disabling and omission. Multi-DPI and physical screen-reader qualification remain open.

Sources: pinned [List specification](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-list/library/docs/Spec.md), [ListItem behavior](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-list/library/src/components/ListItem/useListItem.tsx).

Rows center their naturally measured content vertically beside the32px checkmark target. A composite Row should also specify CrossAxisAlignment = UiCrossAxisAlignment.Center; Observer does so for names, inputs and buttons. Checkbox painting runs once after all row selection writes settle, preserving each row clip and enabled state. Selecting a later row therefore updates earlier indicators in the same displayed frame.

Custom IUiListPresentation defaults CanRetainMeasurement to false. Fluent explicitly opts in. A changed parent-derived checkmark extent invalidates the row and owning arrangement; mutable custom metrics cannot reuse stale geometry.

The list paints its row focus outline after all row focus requests and roving-entry repair settle. A request to a later row removes the earlier outline in that same frame. The outline retains the row clip and does not cover a child editor's own focus indicator.

`UiAccessibilityNode.Grid` and `.GridItem` describe a complete loaded grid. Publication rejects negative dimensions, out-of-range spans, overlapping cells, missing coordinates and unrelated containing grids. The cell index and metadata commit together; aborted frames retain the previous snapshot. Single cells use a coordinate dictionary, while merged cells retain their rectangle without allocating each covered coordinate. Count, coordinate, span and containing-grid changes publish notifications after commit.

Retirement is qualified through unavailable element identity and `GetItem` returning `UIA_E_ELEMENTNOTAVAILABLE`. On this Windows client, a retired Grid scalar property returned success with zero. Separate direct-provider tests require unavailable row-count getters and properties. [UIA may supply property defaults](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-propertiesforclients), so zero alone does not establish a live empty grid. The internal Windows implementation producing this observed result has not been proven.

