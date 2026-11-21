# Fluent 2 TabList

Current overflow integration: [selected-priority overflow and shared menu](fluent2-tab-overflow.md). Existing no-overflow behavior remains the default; whole TabList fidelity is still incomplete.

Last synced: 2026-11-11

SDK provenance: the workspace requests RC3 with latestPatch roll-forward, and currently selects SDK11.0.100. This panel qualification pins11.0.100 with roll-forward disabled in `artifacts/tab-panel-2026-11-11/global.json`; its runtime is11.0.0. It does not replace the unrun RC3 NativeAOT/runtime-image comparison. Earlier selection-event records did not probe the exact SDK before building, so their RC3 SDK label is unverified.

`Fluent.TabList` is a compiled tab header with independent selection and keyboard focus. Inheritable `UiFluentTabList` statically binds `UiTabList<UiFluentTabListBehavior, UiFluentTabListPresentation>`. The caller displays content from the selected value; the header creates no separate windows or page instances.

```csharp
var selection = new UiState<string?>("overview");
UiTabOption[] pages = [new("overview", "Overview"), new("details", "Details"), new("unavailable", "Unavailable", false)];
var tabs = Fluent.TabList("Pages", selection.Binding, pages);
```

Render the header and its borrowed bodies in the same frame:

```csharp
tabs.Render(ui);
tabs.RenderPanel(ui, "overview", overviewWidget);
tabs.RenderPanel(ui, "details", detailsWidget);
```

`RenderPanel` also accepts a cached `Action<UiImmediateContext>` for existing screen code. It returns true only for the selected body and does not invoke inactive bodies. It borrows widgets and callbacks; the caller retains their lifetime and state. The panel uses the caller's layout and ID scope, creates no window, and has its own optional keyboard stop before its children (`focusable : false` omits it). Rendering before the header, in another context/frame, with an unknown rendered key, or twice for the selected key fails explicitly. Selection and item changes after a header preserve that header's selection, key and label until the next frame, so the body matches the header already drawn.

The selected TabPanel publishes its clipped consumed body bounds, contains the body's accessibility nodes and restores the enclosing parent afterwards. Windows maps it to Pane (50033) with AriaRole `tabpanel`; LabeledBy (30018) refers to its actual tab, and that tab's ControllerFor (30104) returns a native provider array containing the live panel. Inactive tabs expose an empty array. Leaving a panel retires its provider; returning reuses the semantic ID with a fresh runtime identity. Ordinary Tab/Shift+Tab and queued accessibility focus share the existing navigation owner. LabeledBy and ControllerFor change notifications are qualified in [relationship changes](accessibility-relationships.md); native focus events follow [focus publication](accessibility-focus.md), while screen-reader narration remains open.

Current managed qualification: TabListCPU77 = basic27 + COM12 + selection-publication13 + panel25; observerCPU39; external native client21/host3; observerGPU37. The 58 prior observer capture bodies match below the changing 128-pixel metrics/header area. Graphics and Rendering test projects plus the observer build with zero warnings/errors. Evidence: `artifacts/tab-panel-2026-11-11/verification.json`. Full official fidelity and NativeAOT remain separate open gates.

Run `./run-gallery.ps1` from the repository root. Every observer page uses the actual factory in its header. Tab enters the group; Left/Right move focus and Enter or Space selects. Leaving and re-entering returns to the selected tab. Vertical layout uses Up/Down and horizontal RTL reverses Left/Right. Disabled items are skipped. Home, End and circular navigation are supported. Escape cancels pending pointer or Space activation.

`UiTabListOptions` provides Small/Medium/Large sizes, horizontal/vertical layout, RTL, Transparent/Subtle/SubtleCircular/FilledCircular appearance and reserved selected-text width. `SelectOnFocus` defaults false; true selects as focus moves. Small and Medium use Body1 14/20 and Large uses Body2 16/22, with Semibold selection. Default reservation keeps header geometry stable across selection. Option changes and `SetItems` request a frame. Items are copied; duplicate or null keys/labels reject before replacing valid state. Values are independent of labels and use collision-free UTF-16-code-unit encoding for IDs.

The binding owns selection. Null means explicitly unselected; no replacement selection is invented. Focus follows its key through reordering. `IUiTabListBehavior` and `IUiTabListPresentation` allow statically selected input, measurement and painting; input state is supplied by the widget. Default reserved measurement does not read the selection binding, and rendering reads selection once for processing.

Accessibility publishes a TabList parent and Tab children with distinct selection, focus, enabled and clip state. Windows exposes actual Tab/TabItem control types and Selection/SelectionItem patterns. Select, Focus and Deselect use the existing render-thread action queue; provider calls enqueue asynchronously. Selection is single-valued and may be empty. AddToSelection rejects a second simultaneous selected item; removed providers become unavailable. Complete external-client qualification remains open; the selected-panel relationship path is qualified above.

`UiAccessibilityTree.SelectionChanged` runs after the new tree is published and outside its reader lock. Callbacks may read the committed snapshot or enqueue actions, but cannot reenter publication or disposal. A replacement selection raises IsSelected property changes on both tabs and ElementSelected on the new selection. Explicit empty selection raises ElementRemovedFromSelection on the surviving deselected tab. Removing the selected item without a replacement raises SelectionInvalidated on the surviving container; a removed container emits no event. Initial discovery, unchanged frames and stable-key reordering do not fabricate selection events. Inserting a selected tab into a live container has no invented previous property value.

Earlier selection-event qualification records managed .NET 11.0.0: CPU52 = basic27 + COM12 + publication13; a separate Windows UIA client passes8 checks and its owned host passes3, both exit0. The client receives cached selection and removal events, four IsSelected property changes, and invalidation after removing the selected item. Evidence: `artifacts/tab-selection-events-2026-11-11/verification.json`. This owned HWND fixture does not draw GPU frames or validate screen-reader narration. The earlier GPU192 qualification below remains separate.

Earlier managed .NET11 qualification: CPU39 (27 input/lifetime-boundary checks and12 actual COM ABI checks), GPU192 (schemes, three sizes, both orientations, both appearances, MSAA1/4, acquired-image replay and independently specified indicator-color samples), observerCPU29/GPU30, existing accessibility49, containers179 and scheduling35 pass. GPU checks qualify the Duxel consumer path, not complete official-browser pixel equivalence or performance. Evidence: `artifacts/fluent-tablist-2026-11-11/verification.json`.

References are the [official usage guidance](https://fluent2.microsoft.design/components/web/react/core/tablist/usage), react-tabs at Fluent UI commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`, and [Selection](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nn-uiautomationcore-iselectionprovider)/[SelectionItem](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nn-uiautomationcore-iselectionitemprovider) specifications. Reference originals and hashes are retained in the same artifact directory.

The foundation now supports [icon content and circular appearances](fluent2-tab-content.md). Overflow menus, indicator transition animation, forced colors, complete official fidelity and current NativeAOT/runtime comparison remain incomplete. Catalog35 comprises16 top-level names and19 variants;39 of55 top-level names remain unconnected. Full Fluent conformance has no confirmed denominator.

## Panel focus bounds — 2026-11-11

Selected panel focus borders paint inside the caller's visible content region, including root padding and child clips. Right and bottom edges remain visible when a body fills or exceeds its viewport. Short bodies retain their consumed height; header → panel → control navigation is unchanged. Evidence: artifacts/observer-idle-panel-2026-11-11/verification.json,16 four-edge GPU checks across MSAA1/4 and43 actual observer GPU checks.


## Selection width reservation — 2026-11-12

With `ReserveSelectedSpace` enabled, both selected and unselected tabs reserve the maximum of regular and semibold text metrics. Regular typography can be wider than semibold when applications configure different typefaces; selection must not shrink the reserved header. Disabling reservation retains selection-dependent measurement. This correction applies across sizes, orientations and appearances and does not introduce an intrinsic-size cache.

## Bounded intrinsic measurement — 2026-11-12

`IUiTabListPresentation.CanRetainMeasurement` defaults false. Opting in promises intrinsic sizes independent of host constraints, determined by immutable items, selection/options and the text environment; `ReserveSelectedSpace` must make them independent of selection. Built-in Fluent opts in. The widget keeps one optional buffer of at most256 UiVector2 values (2048 payload bytes). Larger lists, glyph observation or oversized key strings use the ordinary measurement path without retaining that buffer. These limits apply to the additional metric cache, not the widget's existing item/input/layout storage.

The key includes resource identity, typography/text settings, options and selection when reservation is off. Item/property invalidation clears key references; disposal drops the buffer. Failed or invalidated-in-progress measurements cannot commit. Header rectangles and overflow visibility are rebuilt every time from intrinsic sizes, so hidden/RTL/selected overflow positions never poison the cache. Overflow indicator measurement and actual input, painting, popup actions and panels remain live. Reserved nonoverflow tabs also opt into the existing ancestor measurement cache; nonreserved or overflow tabs remain dynamic to ancestors.

Observer ./run-gallery.ps1 → Measurement reuse includes actual sample tabs, a process-wide diagnostic metric count, selection and a size-change button. Selection preserves that count; changing size invalidates it. This sample counter is diagnostic and introduces no periodic frame request. Full application timing/memory and exactRC3 NativeAOT qualification remain open. Evidence: artifacts/tab-measurement-retention-2026-11-12/verification.json.


### Drag-scroll tab headers

Set `UiTabListOptions.Scroll = new UiTabListScrollOptions()` to enable horizontal header scrolling. This is a separate choice from the existing `Overflow` allocation mode; do not enable both. Implementation Observer uses this mode for its top-level pages.

Drag the header or use the wheel. Each ellipsis appears only on an obscured edge, allowing left-only, right-only, or both. Clicking an ellipsis opens that side's hidden-tab context menu without moving the header. Choosing an item selects and reveals the original tab and panel. Selecting or focusing an edge tab also reveals one neighbouring tab in that direction to support continued navigation. Releasing a drag does not activate the tab under the pointer.

The bottom scrollbar overlays the header without reserving layout space. Defaults are120ms entry,400ms stationary hold and160ms exit; a held drag keeps it shown. Configure timing, curves and reduced motion through shared `UiScrollbarMotionOptions`, with per-tab overrides in `UiTabListScrollOptions.Motion`. Settled animation stops scheduling frames. See the [shared transition design and API](finite-transitions.md).


All scrollbar families use the same [shared motion module](scrollbar-motion.md).

Tab options can include UiTabBadge(Text, Options) through UiTabOption.Badge. Fluent measures and paints the trailing badge through the common Badge pipeline and publishes its text as accessibility help. Vertical headers use the actual allocated width so status adornments can remain visible when the label clips. Text-only custom presentations remain unchanged; icon/badge content requires the item overloads. Gallery uses Small/Informative/Tint badges for missing entries. Existing overall TabList fidelity limits remain.
