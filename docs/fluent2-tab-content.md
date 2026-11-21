# Fluent tab content and circular appearances

Last synced: 2026-11-11

Run `./run-gallery.ps1`, select **Fluent**, then scroll to **Tab styles and icons**. Four actual tab lists share a persistent selection. Compare text with icons, icon-only content, disabled items and Transparent/Subtle/SubtleCircular/FilledCircular. The last two appearances use pill rounding; they do not draw an underline. Arrow keys move focus; Enter or Space selects. Selection switches the regular icon to its filled export. Hover and focus alone do not select that export.

```csharp
var icon = UiTabIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>();
var tabs = Fluent.TabList("Navigation", selection.Binding,
    new UiTabOption[] { new("add", "Add", Icon : icon, IconOnly : true) },
    new(Appearance : UiTabListAppearance.FilledCircular));
```

Both icon types implement the existing `IUiButtonIcon` contract. `UiTabIcon.Create<TRegular, TFilled>()` caches one static painter per closed pair, accepts custom vector painters or generated Fluent glyph exports and keeps icon selection independent of interaction state. It introduces no font discovery or substitute icon. Font-backed exports require the existing configured DirectText backend and shipped Fluent font assets. The observer uses generated Home, Document and Settings regular/filled exports.

`UiTabOption` retains its stable value, accessible label and enabled flag, adding optional `Icon` and `IconOnly`. `Label` remains the accessibility name when hidden visually. Icon-only content requires an icon; all icon content requires a nonblank label. Invalid items fail before replacement and leave the caller's binding unchanged. Existing text-only calls and text-presentation methods continue to work. Custom `IUiTabListPresentation` implementations support icons through the item overloads of `MeasureTab` and `PaintTab`; the default overloads explicitly reject unsupported icon content instead of discarding it.

Small/Medium icons occupy20 units; Large uses24. Circular list gaps are6 for Small and8 otherwise. Icon/text gaps are2 for Small and6 otherwise. Text-only dimensions stay unchanged. Circular borders participate in measurement. Vertical content aligns at its leading edge, RTL places the leading icon on the right, and circular gaps are excluded from pointer hit targets. Selection, native UIA patterns, panel relationships and final-frame focus continue through the common TabList path. Appearance changes retain the binding and runtime item identities.

`UiFluentTabListPalette` exposes the resolved background, border, text, icon and indicator colors. Expected test data comes from the pinned Fluent UI8830c10b styles and actual brandWeb/grey expressions, not alias comments or the production palette. `tests/Duxel.Graphics.Tests/Fixtures/TabAppearances.json` records64 light/dark, four-appearance, four-state and selected/unselected combinations with token names and resolved colors.

Qualification: core197 checks; actual GPU800 checks across MSAA1/4 and both schemes; external native selection/controller client25 and host3; observer CPU44 and GPU40, including scrolling to the actual four lists and pointer selection. Original scheduling35, accessibility49, containers179 and legacy10 regressions pass. GPU checks compare state colors, underline presence, image reuse and real font glyph occupancy/regular-to-filled geometry. They do not prove complete official story-image fidelity, forced colors, focus shadows, indicator transitions, overflow menus, screen-reader speech or NativeAOT. Full Fluent scope remains incomplete.

Evidence: `artifacts/tab-representations-2026-11-11/verification.json`. SDK11.0.100 is pinned without roll-forward and runtime is11.0.0; this does not replace the unrun RC3 NativeAOT/runtime-image comparison. Palette/font GPU PPM captures from this step are stored in byte-verified ZIP archives under `L:/Cache/Duxel/tab-representations-2026-11-11`; `gpu-archives.json` records original filenames, archive locations and manifests. Selected PNG views and JSON results remain easy to inspect. Observer GPU captures use that explicit L: cache location after a capacity check. A W: storage failure and recovery are recorded separately from successful tests.

Primary source: [pinned Tab styles](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/library/src/components/Tab/useTabStyles.styles.ts), [pinned TabList spacing](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/library/src/components/TabList/useTabListStyles.styles.ts).

### Legacy BeginTabBar/BeginTabItem semantics (2026-11-11)

The streaming legacy API now publishes a TabList, real Tab SelectionItems and only the active TabPanel, with reciprocal ControllerFor/LabeledBy links. Body controls belong to their panel; nested bars restore the original outer parent. TabItemButton remains an Invoke button, outside tab selection. Required selection rejects removing the selected header. Closing or removing a pending target cannot report a successful selection that was never published.

Pointer and accessibility selection preserve the existing next-frame transition, with one body per frame. A UiAccessibilityTree Select request with wait=true completes after that selected frame is published; closing/removing the target before publication returns InvalidOperation/Unavailable. Existing Windows SelectionItem methods still accept requests asynchronously: observe committed property/selection events before reading the new state. Selected headers/panels keep the legacy nonfocusable contract; full keyboard/flag coverage and narration remain open. A committed transition schedules no continuing wakeup or animation deadline.

Managed legacy25, common TabList197, actual Observer131, external legacy client15/host3 and generated-tab regression client25/host3 pass. ObserverGPU47 passes and all73 comparable body captures match the prior numeric-accessibility baseline (FPS header excluded). Tests distinguish equally named headers/panels by their actual TabList scope; generated panel checks still require focusability by default, while legacy checks explicitly require false. Run ./run-gallery.ps1 → Existing API → Hello / Immediate. Evidence: artifacts/legacy-tabs-2026-11-11/verification.json. NativeAOT/full-goal qualification is separate.
## Scrolled body boundaries (2026-11-17)

Legacy BeginTabItem, Fluent.TabList.RenderPanel and Nav.RenderPage share screen-coordinate clipping. A child-scroll cursor already includes its scroll offset; its remaining layout budget is not reused as a screen-space clipping height. Ancestor clipping, content padding and existing column-width limits are retained. Even when content exceeds the viewport, semantic bounds contain visible descendants and focus outlines stay within the visible region.

Qualification: the actual Gallery reveals its deeply scrolled Scale control and verifies its parent bounds. GPU checks cover legacy tabs, Fluent tabs and navigation pages at MSAA 1/4, including all four outline edges for both focusable paths. A control using only the previous Core fails the same parent-boundary regression. Evidence: `artifacts/tab-panel-scroll-clip-2026-11-17/result.json`.
