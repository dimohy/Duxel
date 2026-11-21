# Fluent Breadcrumb

[한국어](fluent2-breadcrumb.ko.md)

Last synced: 2026-11-12

`Fluent.Breadcrumb` owns a path of unique values and labels. The last item is the passive current location. Selecting an earlier item passes its value to the callback. Responsive overflow starts with the second level, preserving the first and current levels while they fit. At extreme widths only the clipped current location remains.

```csharp
using var path = Fluent.Breadcrumb("Document path",
    [new("home", "Home"), new("folder", "Documents"), new("current", "Current document")],
    value => OpenLocation(value),
    new(MaximumCharacters: 30));
path.Render(ui);
```

`UiBreadcrumbOptions` provides Small/Medium/Large heights24/32/40, Tab/Arrow focus modes, RightToLeft and label/Tooltip character limits. Limits count text elements without splitting combining sequences or Korean syllables. Long labels receive an ellipsis and a focus/hover Tooltip. Changing options remeasures the same instance and preserves input state. Items are copied at construction; replacing the path is not currently public API. Labels and values must be nonempty, and values must be unique.

`UiBreadcrumbOption.Enabled = false` blocks navigation. `DisabledFocusable = true` defaults to allowing keyboard and native Focus on visible disabled levels for their descriptions. Disabling the entire parent does not allow that exception. Arrow mode uses Left/Right/Home/End and reverses directional navigation in RTL. Overflow reuses the common Menu popup, selection, focus and Escape behavior. Disabled entries inside that popup follow the common Menu rule and are skipped by keyboard navigation.

Accessibility exposes Navigation landmark → OrderedList → ListItem → actual button. The current passive ListItem exposes ItemStatus, `current=page`, its original position and the total item count. Dividers have no accessibility nodes. Reappearing items retain logical identifiers, while retired native runtime identifiers remain unavailable.

Default chevron and more icons use the **Fluent icon font**. Generated glyph descriptors draw through the common DirectText cache. Supply a separately owned `UiSvgIcon.MenuIcon` through `UiBreadcrumbOption.Icon` for SVG content. No per-frame SVG XML parsing path is added.

Run `./run-gallery.ps1` → **Breadcrumb** to change widths520/280/80, keyboard mode, direction and size, and inspect navigation history. State survives page changes. Breadcrumb schedules no periodic frames. The default presentation retains measurement and processed labels while still processing input and drawing on requested frames.

Evidence: `artifacts/breadcrumb-2026-11-12/verification.json`. Managed CPU56 suites/8803 assertion executions, GPU14 profiles/2944 assertions and separate external Windows UIA7 profiles/138 assertions pass; counts include repetition. Complete official conformance, dynamic path replacement, pixel-aware extreme-width ellipsis, forced colors, all devices and the fixed RC3 NativeAOT/original performance validation remain open.

References: [official usage](https://fluent2.microsoft.design/components/web/react/core/breadcrumb/usage), [pinned source](https://github.com/microsoft/fluentui/tree/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-breadcrumb/library/src/components). Source snapshots and attribution are retained in `sources.json` under the evidence directory.


Vertical text alignment uses the shared typography line box and baseline, centered inside each breadcrumb button. GPU comparisons use independent typography widgets across Korean/English, three sizes, current-item weight, hover and RTL, including a regression case reproducing the retired legacy text origin.
