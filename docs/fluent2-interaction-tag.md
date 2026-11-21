# InteractionTag — primary and dismissal actions

```csharp
var tag = Fluent.InteractionTag("SVG", () => Inspect(), () => Remove());
var tags = new UiFluentTagGroup("Features", selected.Binding, options, activated: InspectValue);
```

The required `activated` callback owns the primary action. An optional standalone `dismissed` callback owns ×. Callbacks change application data; the widget does not automatically dispose itself. A group's `activated` receives the canonical `UiSelectOption.Value` without automatically changing selection. Its × removes only that value through the existing selection model. A `dismissible: false` group retains primary actions.

Primary and × have independent buttons, pointer regions and Enter/Space activation. Delete/Backspace dismiss only when × is focused; Delete on the primary does not dismiss. Group arrows traverse primary → × → next tag and skip both actions of disabled options. RTL reverses horizontal placement/navigation. Leaving with Tab and returning with Shift+Tab remembers the last sub-action.

The existing three Tag sizes, three appearances, two shapes, secondary text and borrowed SVG icons are shared. Secondary widths are Medium 32 and Small/ExtraSmall 24. Backgrounds, foregrounds and dividers resolve pinned Fluent tokens using the web brand ramp; basic Tag Brand foreground was corrected to the same authority. Hover changes only the relevant action surface. Existing rounded clips, typography and icon rendering are reused; SVG XML is not reparsed per frame. × uses the Fluent icon font.

Accessibility publishes a passive Group with two real Buttons. × has a LabeledBy relationship to the primary and a contextual full name. TagGroup remains a Toolbar with one roving Tab stop across both sub-actions. Observer action feedback uses an actual Label rather than paint-only typography.

Existing custom `IUiTagPresentation` implementations keep basic Tag support. InteractionTag requires `SupportsInteraction`, `MeasureInteraction`, `SecondaryWidth` and `PaintInteraction`. Unsupported presentations fail explicitly at construction instead of switching styles. Borrowed icon/SVG owners must outlive widgets.

Run `./run-gallery.ps1` → **Selection lists · combined** → **Features to try** and select image/SVG features. Click an upper interactive tag's primary text to update **Tag activation** while preserving selection. Click × to remove it. Lower basic tags dismiss from their entire surface. Compare size changes, direction changes and page persistence.

Evidence: `artifacts/interaction-tag-2026-11-13/`. [Official Tag guidance](https://fluent2.microsoft.design/components/web/react/core/tag/usage), pinned commit `8830c10b`. InteractionTag is a Tag variant and does not increase the top-level denominator. Overflow, forced colors, extended small touch targets, physical screen-reader and fixed RC3 NativeAOT/performance/device acceptance remain open.


## Controlled selection emphasis

```csharp
var selected = new UiState<bool>(false);
var tag = Fluent.InteractionTag("Highlight", () => Inspect());
tag.Selected = selected.Binding;
```

`Selected` is an optional `UiBinding<bool>` on the standalone InteractionTag. Primary pointer release, Enter/Space and native Toggle request the inverse value once; the binding getter is read back before painting and publishing accessibility in that same frame. A setter may reject a request. The primary callback still runs for each activation. The secondary dismissal never writes this binding. External values are read on the next requested frame; no periodic render is scheduled.

A bound primary exposes the existing Button Toggle pattern, rather than Invoke; the secondary remains Invoke-only. Clearing the binding removes Toggle capability. `UiTagOptions.Selected` supplies visual emphasis only when no binding overrides it; it does not invent selection or Toggle semantics. Disabled styling takes precedence. Selected rest/hover/pressed, white foreground/divider and outline use the pinned brand aliases. TagGroup's existing selection binding still controls visible values; it is independent of this standalone emphasis binding.

Run `./run-gallery.ps1` → **Selection lists · combined**, scroll to **Emphasis selection**, then toggle **Toggle whether tag changes are allowed** to compare accepted and rejected changes. The displayed state, color and native Toggle state follow the accepted model value. Evidence: `artifacts/tag-selection-2026-11-13/`.


## Independent group emphasis

```csharp
var visible = new UiState<string[]>(["image", "svg"]);
var emphasized = new UiState<string[]>([]);
var group = new UiFluentTagGroup("Features", visible.Binding, items,
    emphasized: emphasized.Binding);
```

The optional `emphasized` binding controls selected styling and primary Toggle state independently of the existing visible-value `selection`. It also enables interaction without requiring `activated`. A primary action toggles only emphasis; an optional `activated` callback still receives the canonical value. × removes only visibility. Hidden emphasis values are retained deliberately, so restoring a dismissed item restores its emphasis. Both arrays must contain distinct known option values. Rejected or normalized writes are read back through the existing selection model, and external array mutation is detected before arrangement. The emphasis snapshot is shared across child getters rather than scanning the entire binding for every painted child. No periodic frame is requested.

`dismissible: false` retains primary Toggle actions and roving keyboard navigation. Disabled options reject Toggle. Changing size/RTL does not replace selected providers. Group children inherit the same standalone selected rendering; `TagOptions.Selected` remains only a visual default when no emphasis binding is present.

Run `./run-gallery.ps1` → **Selection lists · combined** → select features. Click an upper tag's text to toggle its highlight, × to remove it, then add that feature again to verify the highlight is retained. The lower basic group still follows the same visible values. Evidence: `artifacts/tag-group-emphasis-2026-11-13/`.

Media now shares [Tag avatar/image slots](fluent2-tag.md#borrowed-avatar-and-image-media), including independent per-tag resource ownership.
