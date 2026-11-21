# Fluent Accordion

[한국어](fluent2-accordion.ko.md)

Run `./run-gallery.ps1` and select **Accordion** (use the tab overflow menu when necessary). Open the first item, edit its note, collapse it and visit another tab; the same content widget and value remain. Controls switch multiple open items, last-item collapse, chevron position and four header sizes. The third header demonstrates disabled behavior.

```csharp
using System.Collections.Immutable;

var open = new UiState<ImmutableArray<string>>(ImmutableArray<string>.Empty);
var note = new UiState<string>("Saved text");
var accordion = Fluent.Accordion("Settings", open.Binding,
    new UiAccordionItem[]
    {
        new("note", "Note", Fluent.Input("Note text", note.Binding, 256, 300)),
        new("action", "Action", Fluent.Button("Apply"))
    }, width : 360, options : new(Collapsible : true));
// In the existing screen callback:
accordion.Render(ui);
// Dispose the accordion when the screen is released; it owns both content widgets.
```

`UiAccordion<TPresentation>` separates pure header measurement/painting through `IUiAccordionPresentation`. The generated factory selects `UiFluentAccordionPresentation`. `UiAccordionItem` has a stable unique Value, Label, exclusively owned Content, Enabled flag and borrowed UiMenuIcon painter. Item descriptors are copied; content stays alive while hidden and is released with the accordion. `SetHeader(value, label, enabled, icon)` changes a header without replacing its key or panel widget. The icon owner must outlive the accordion.

The controlled value is `ImmutableArray<string>`. Initial default/empty means all closed. Multiple=false permits one open item. Collapsible=false prevents closing the final open item through user/UIA actions; callers may still explicitly supply an empty controlled value. Unknown/duplicate keys and invalid single-mode state fail explicitly. Update a multiple selection before changing Options to single mode. A rejecting binding setter does not cause a false successful Expand/Collapse result.

Enter and Space release toggle the focused header; Tab/Shift+Tab follow header → visible panel content → next header. The reference's deprecated arrow-navigation option is not enabled. Closing focused content restores its header, including with accessibility capture disabled. Header events are processed once before final panel arrangement; actual tab order includes content. Parent allocations bound painting and input. A parent measured before the input change may apply its new natural height on the requested next frame. No periodic animation is scheduled. Measurement is deliberately non-retained while arbitrary binding getters can change independently; no CPU/performance improvement is claimed.

UIA exposes an Accordion Group, Button headers with Invoke and ExpandCollapse, and live nonfocusable panels. Expanded headers control their actual panels; panels are LabeledBy their headers. Collapsing removes content nodes/providers and retains the widget model. Disabled requests return Disabled; forbidden final collapse returns InvalidOperation. Existing post-publication expansion/relationship events are reused.

Pinned reference: FluentUI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`; [usage](https://fluent2.microsoft.design/components/web/react/core/accordion/usage), [state behavior](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-accordion/library/src/components/Accordion/useAccordion.ts). Default headers use transparent backgrounds, 44px minimum height (Small32), regular12/16,14/20,16/22,20/28 typography, start/end chevrons and12px panel side margins. Forced colors, RTL-specific behavior, collapse motion, official browser pixel equivalence and full conformance remain open.

Evidence: `artifacts/fluent-accordion-2026-11-12/verification.json`.47 CPU contracts/7876 assertion executions including Accordion27 and Observer164; native Accordion13 and shared Label/Radio/Toggle43; GPU32 across Light/Dark×MSAA1/4. Native runner Profile=Accordion uses the same explicit-binary/fresh-output command as [contract validation](widget-contract-validation.md). Exact RC3 NativeAOT and whole-goal gates remain separate.


Direct comparison with the official React demo confirms a pointer cursor while retaining the transparent default hover background. Disabled headers use a not-allowed cursor and disabled foreground. Duxel applies that cursor only within the actual clip and unoccluded popup tier. Keyboard Tab shows a focus outline; Enter and Space retain the existing header button contract. No extra hover background is added to the official default presentation.

Sources: [design guidance](https://fluent2.microsoft.design/components/web/react/core/accordion/usage), [live React demo](https://react.fluentui.dev/?path=/docs/components-accordion--docs). Browser and pinned-source evidence is preserved in `artifacts/accordion-hover-2026-11-13/official-evidence.json`. This checks the stated interaction scope, not complete Fluent visual or motion conformance.
