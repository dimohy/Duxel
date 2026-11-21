# Fluent Select

Select is a plain single-choice form field. Use Dropdown for icons, descriptions or multiple selection, and Combobox for editable/filterable text. [Official usage](https://fluent2.microsoft.design/components/web/react/core/select/usage) and [official implementation](https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-select/library/src/components/Select/useSelect.tsx) distinguish native HTML selection from rich dropdown content.

```csharp
var value = new UiState<string?>(null);
using var select = Fluent.Select("Region", value.Binding,
    [new("kr", "Korea"), new("us", "United States"), new("jp", "Japan"), new("de", "Germany", false)],
    new(Width : 250, ExpandLabel : "Show options"));
select.Render(ui);
```

The constructor proposes the first enabled item once when the binding is initially null. An explicit initial value is preserved. A controlled setter can reject the proposal; rendering does not keep rewriting it. Later external changes, including null, are read from that binding. Unknown values and duplicate item keys fail validation. Items are copied. Empty lists are valid. Rich option icons/descriptions are rejected; there is no multi-selection constructor.

Derive from `UiSelect<TBehavior, TPresentation>` to choose press or release independently for the field and popup options. Select and Dropdown use the same `IUiDropdownBehavior`. Select continues to handle closed-field keyboard navigation and single-selection rules.

Closed Up/Down, Home/End and PageUp/PageDown commit an enabled option while preserving field focus. Typeahead commits a matching name and supports spaces and repeated-character cycling through the shared selection model. Alt+Down, Space or the entire field/chevron open the popup. Expanded keyboard navigation, Enter, Escape, outside dismissal, scrolling, pointer selection and accessibility requests use the existing menu engine. Required selection prevents accessibility Deselect from clearing the last item. Form validation Required and SelectionRequired are separate concepts.

UiSelectOptions supports width, shared UiDropdownSize, four UiInputAppearance values, RTL, maximum popup height, invalid state and a localized expand label. Field chrome shares Fluent input metrics (24/32/40 logical heights); plain rows have no rich-content or checkmark slots and visibly highlight selection. Forced colors use the existing system palette. Disabled controls block edits.

Duxel draws its popup in the application's renderer. This is not a browser-native HTML select or a platform-native picker dialog; no claim of matching those native popup pixels is made. The factory connection count50/55 is not full visual, platform or assistive-technology conformance.

Run `.\run-gallery.ps1` → **Fluent controls → Input and selection → Select**. Each of three examples has adjacent current source and size/appearance/direction/validation controls. Select alone carries NEW. [Verification](../artifacts/fluent-select-2026-11-18/result.json). Actual Linux execution, fixed NativeAOT and external screen-reader acceptance remain unverified.
