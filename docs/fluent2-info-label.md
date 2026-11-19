# InfoLabel

`Fluent.InfoLabel` composes a standalone label and an information button. It borrows an independently rendered input target and owns supplemental popover content.

```csharp
var name = new UiState<string>("정세일");
var input = Fluent.Input("Name", name.Binding, 100, 240);
var label = Fluent.InfoLabel("User name", Fluent.Body1("The name shown to other users."),
    input, new(Required: true), informationLabel: "More information");
var form = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min), label, input);
```

Clicking the label focuses its target. The information button has its own Tab stop. Pointer release or Enter/Space opens the popover; Escape, outside clicks and focus leaving the owned region dismiss it. Its accessible name combines visible text and localized `InformationLabel`, normalizing line breaks to spaces.

Defaults: `Note` surface role, above-start placement, arrow and width264. Control it through `PopoverOptions`, `Open`, `OpenChanged`, `Show` and `Close`. Explicit focus trapping uses a Dialog role. `Text`, `Target` and `Options` are mutable. Disposal releases the association and owned popover content without disposing the borrowed target. No information content means no information button.

| Size | Icon | Button target | Text line height |
| --- | ---: | ---: | ---: |
| Small | 12 | 20×20 | 16 |
| Medium | 16 | 24×24 | 20 |
| Large | 20 | 24×24 | 22 |

Default icons are Fluent icon-font regular/filled glyphs. Hover and open states use the filled glyph and brand foreground. This path does not parse SVG XML; SVG input and transforms remain available through the separate Image/Icon paths.

The button follows the last text line and flows to the next line when necessary. Native parents clip allocated children, so the CSS negative margins become internal text insets of2/2/1px. Relative icon/text alignment is preserved while the full target remains inside its allocation. Explicit ancestor clips and width/height constraints still apply.

Run `./run-gallery.ps1` and select **추가 정보**. Change size/text, focus the input through its label, use the popover command, Tab/Escape and page round trips. Unchanged replay schedules no periodic frames.

References: [official usage](https://fluent2.microsoft.design/components/web/react/core/infolabel/usage/), [pinned InfoButton styles](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-infolabel/library/src/components/InfoButton/useInfoButtonStyles.styles.ts).

Validation covers generated API, semantics, input and ownership CPU contracts and real GPU pixels. Physical screen readers, forced colors, full RTL appearance and NativeAOT remain separate work. This is not complete Fluent conformance.
