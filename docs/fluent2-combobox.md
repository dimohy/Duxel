# Fluent Combobox

[한국어](fluent2-combobox.ko.md)

Popup rows delegate to the same system-color option presentation as Dropdown, including Canvas/CanvasText, disabled GrayText, checked single/multiple markers and the post-row outer focus line. The editor and its bindings are unchanged. See [Dropdown contrast roles and scope](fluent2-dropdown.md). Evidence: `artifacts/option-contrast-2026-11-16/verification.json`.

`Fluent.Combobox` connects the existing single-line editor, IME, selection and undo kernel to the shared popup. Input text and canonical selected option values have separate bindings. Typing retains the original selection; filtering uses case-insensitive substring matching. Hidden selections retain their provider IDs and remain available through actual GetSelection.

```csharp
string query = "";
string? city = null;
using var combo = Fluent.Combobox("City",
    new UiBinding<string>(() => query, value => query = value),
    new UiBinding<string?>(() => city, value => city = value),
    [new("seoul", "Seoul"), new("new-york", "New York")],
    new(Field: new(Width: 260, Placeholder: "Search cities"), Freeform: true));
combo.Render(ui);
```

Use a `UiBinding<string[]>` selection for multiple choice. Popup activation toggles options without losing hidden selections. Exact-name Enter clears the multiple query and keeps the list open. `SelectedText` exposes selected option labels. A selected-tag widget is not yet provided.

Arrow keys, Home, End, PageUp and PageDown navigate options; Enter and Space commit after navigation. Before navigation, spaces remain part of the query. Escape dismisses while preserving the editor; Tab moves to the next real control. Clicking the editor or expander opens the popup; the expander restores the full collection. The editor preserves popup pointer capture until release, and dismissal immediately unblocks subsequent controls.

With default `Freeform=false`, unmatched Enter restores the selected label. Single-selection `Freeform=true` preserves unmatched text and clears the stale option selection. Freeform data stays in the text binding instead of inventing option values. Multiple unmatched text is not automatically added as a new selected option.

`UiComboboxOptions.Field` shares Dropdown's three sizes, four appearances, width, RTL, popup height and invalid state. `MaxLength` defaults to4096. Option values must be unique; labels/descriptions are single-line. SVG `MenuIcon` assets are borrowed and must outlive the widget. The chevron uses the Fluent icon font. No XML reparsing, duplicate editor or new shader is introduced. Focused caret updates follow the existing editor contract.

Windows exposes editable ComboBox, child Edit, ListBox and Option nodes with Value, Selection and ExpandCollapse. Hidden options support selection/add/remove without replacing provider identities. IME composition Enter cannot select an option; committed text is consumed once.

Run `./run-gallery.ps1` → **Input and selection → Combobox** → **Search cities**. Type `New ` and select `New York`, or enter Korean text. State survives page changes. Upper size/appearance/direction/width controls also affect the city field. The reset button clears the two Dropdowns below it.

Evidence: `artifacts/combobox-2026-11-13/`, managed .NET11. Physical Windows IME interaction, selected tags, dynamic options, large-list virtualization, complete Fluent states/forced colors and fixed RC3 NativeAOT/performance/device acceptance remain open.

References: [Fluent Combobox usage](https://fluent2.microsoft.design/components/web/react/core/combobox/usage), [Windows ComboBox contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcomboboxcontroltype). Pinned source snapshots reuse the preceding Dropdown phase's `sources.json` and `ComboboxBase.types.ts`.

## Dismissal and controlled visibility

With `Freeform=false`, Escape, Tab, outside dismissal and input blur restore an uncommitted query to the selected label. Multiple selection clears only the query. Dismissal does not implicitly select a matching draft. Freeform drafts remain intact.

```csharp
var popup = new UiState<bool>(false);
combo.Open = popup.Binding;
combo.OpenChanged = Console.WriteLine;
```

A null `Open` uses internal state. With a binding, its setter receives visibility requests and the getter's confirmed value controls the actual result. Retaining the previous value rejects a request. Rejected UIA Expand/Collapse returns InvalidOperation without hiding or inventing a popup transition. `OpenChanged` reports actual observed transitions once; rejected requests and stable frames do not notify. External state changes are not echoed back to the setter. Model-only updates follow the existing Watch/frame-request contract.

External query updates refresh filtering without manufacturing user open requests. Explicit expander/UIA expansion restores the full collection; an already-open list keeps its filter. Selection-driven binding changes are reread before rendering so a stale snapshot cannot reopen a dismissed popup.

`EmptyText` defaults to `No matches`. An empty result has a properly sized popup with unselectable static text, preserving canonical option providers instead of fabricating a disabled Option. The sample localizes the message to Korean. Use **Toggle city list** and its transition counter to inspect external control and callback stability.

Additional evidence: `artifacts/combobox-behavior-2026-11-13/`. [Pinned Fluent dismissal source](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-combobox/library/src/utils/useComboboxBaseState.ts). This API's `OpenChanged` reports actual transitions; its `Open` binding receives requests.

## Lifetime, allocation and validation

The editable search field's arrow opens the complete option collection and closes it on a second click. The shared editor preserves the expander's pointer ownership across held frames. Query editing and keyboard navigation retain their existing behavior.

Context transfer releases the previous editor/popup providers and republishes the logical list even at the same frame number. Disposing an expanded widget releases popup/editor ownership and rejects stale native requests. Actual 80×64 and updated 120×24 parent allocations cover size, popup placement and provider identity.

Standalone `Field.Invalid` reaches both the ComboBox and actual child Edit validation properties; clearing it clears both. Run `./run-gallery.ps1` → **Input and selection → Dropdown / Toggle validation state** to inspect the city editor and both Dropdowns, including page persistence. Evidence: `artifacts/combobox-lifetime-2026-11-13/`.
