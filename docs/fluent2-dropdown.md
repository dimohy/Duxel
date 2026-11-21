# Fluent Dropdown

[한국어](fluent2-dropdown.ko.md)

Popup options now use a shared `UiFluentOptionPalette` for system contrast colors. Enabled labels, icons, secondary text, checkmarks and checkbox borders use CanvasText; disabled content and borders use GrayText. Selected multiple-choice boxes use Canvas behind the mark. Hover/press backgrounds use Canvas, rather than MenuItem's explicit Highlight treatment. A two-pixel outer CanvasText focus line is painted through the existing post-row `PaintFocus` pass. Ordinary authored colors and input/selection behavior are retained. This follows the official [Option styles](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-combobox/library/src/components/Option/useOptionStyles.styles.ts) and a Chromium diagnostic of those CSS color rules; it does not establish complete browser pixel conformance or actual OS transitions. Evidence: `artifacts/option-contrast-2026-11-16/verification.json`.

Verification covers 192 system-color GPU contracts, 192 exact ordinary-painter GPU comparisons and 16 real bound popup actions; all 400 new checks and 845 including prior checks pass. Existing Dropdown 41, Combobox 84 and composite 500 checks also pass. Windows verification presents 64 scenarios; Linux host compilation passes, with native Linux execution still excluded.

`Fluent.Dropdown` provides a readonly field and a real ListBox. Bind `UiBinding<string?>` for single selection or `UiBinding<string[]>` for multiple selection. Single selection closes the popup; multiple selection keeps it open. `UiSelectOption(Value, Text, Enabled)` values must be unique. Unknown or duplicate selected values fail validation. Options are copied; dynamic replacement is not exposed.

Derive from `UiDropdown<TBehavior, TPresentation>` to choose pointer activation for the field and options independently of measurement and painting. Select uses the same `IUiDropdownBehavior`. Painting and accessibility reflect the selection accepted by the binding.

```csharp
string? selected = null;
using var dropdown = Fluent.Dropdown("Location",
    new UiBinding<string?>(() => selected, value => selected = value),
    [new("kr", "Korea"), new("us", "United States")],
    new(Placeholder: "Choose a location"));
dropdown.Render(ui);
```

`Options` supports three sizes, four appearances, width, RTL, maximum popup height and invalid state. Long selected names use `SelectedCountFormat`. The chevron uses the Fluent icon font. Options may borrow an SVG `MenuIcon`; keep its owner alive until the widget is disposed.

Common popup and scrolling machinery handles arrows, Home, End, PageUp, PageDown, Enter, Space, Escape and incremental name search, skipping disabled options. No periodic frames or XML reparsing are introduced. Windows exposes readonly ComboBox, ListBox and Option nodes, closed selection, actual GetSelection, multiple add/remove and Value changes.

Run `./run-gallery.ps1` → **Input and selection → Dropdown**. Try single/multiple selection, SVG, size/appearance/direction/width and reset. State persists across page changes.

Evidence: `artifacts/dropdown-2026-11-13/`. Dynamic options, large-list virtualization, complete Fluent conformance and fixed RC3 NativeAOT/performance remain open.

References: [Fluent usage](https://fluent2.microsoft.design/components/web/react/core/dropdown/usage), [Windows ComboBox contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcomboboxcontroltype).

Additional allocation/state validation covers80×64 and live120×24 parent bounds, isolated Parent-identity siblings sharing option values, in-place external selection-array mutation, setter normalization in the same frame, Space press/release toggling and empty lists. Actual Observer pointer tests select Korean/image/SVG options, operate all appearance controls, switch pages and reset both bindings. Moving to another UI context with the same frame number invalidates shared popup publication stamps and republishes the closed logical list.

Standalone `UiDropdownOptions.Invalid` now reaches Windows accessibility validation state and clears when reset. Run `./run-gallery.ps1` → **Input and selection → Dropdown / Toggle validation state** to inspect the city editor and both Dropdowns. External UIA DataValidForForm transitions and page persistence are covered. Evidence: `artifacts/combobox-lifetime-2026-11-13/`.
