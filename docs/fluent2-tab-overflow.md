# Tab overflow menus

Last synced: 2026-11-11

Enable `UiTabListOptions.Overflow` to keep selected headers visible and select hidden tabs through the shared Fluent menu. The original item array, binding and `RenderPanel` keys remain intact.

```csharp
var selection = new UiState<string?>("one");
var tabs = Fluent.TabList("Pages", selection.Binding,
    new UiTabOption[] { new("one", "One"), new("two", "Two"), new("three", "Three"), new("four", "Four") },
    new() { Overflow = new() });
using var header = Dux.SizedBox(tabs, width : 140, height : 40);
// Render header, then tabs.RenderPanel(ui, key, body) for the original keys.
```

The default minimum is two visible headers. The selected value receives priority 2 and other values priority 1, following the [pinned official composition](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/stories/src/Tabs/TabListWithOverflow.stories.tsx). Remaining headers retain source order; hidden headers stop publishing input/accessibility controls. Menu entries include only hidden values and preserve disabled state. The trigger uses the compiled MoreHorizontal icon, an unselected Tab role and explicit Invoke capability; ordinary tabs do not gain Invoke.

Menu selection changes the original binding and promotes its header. Header accessibility publication is deferred until input and menu selection resolve, so the new selected header and real panel agree in the same frame. `RenderPanel` keeps its same-context/frame snapshot contract and live LabeledBy/ControllerFor relationships. Returning to a page reuses caller-owned bodies and their counters.

Arrows include the overflow action in roving focus and skip hidden/disabled headers. The entire group remains one Tab stop. Enter/Space opens the action; horizontal Down/Up also uses menu trigger navigation. Escape restores the action, while Tab leaves through the pre-open group sequence. If expansion removes the action, focus returns to the selected header. Horizontal RTL and vertical allocation use the same priority resolver. Menu labels currently use text; header icons remain supported, but menu icon slots are still pending.

`UiTabListOverflowOptions.MinimumVisible` must be positive and `Label` nonblank. The accessible action name prefixes the current hidden count. Setting `Overflow = null` restores the original tab layout. `OverflowResult` is null when disabled, otherwise reports the latest arrangement. If the minimum cannot fit, `Fits` is false; the action retains available space and reports `IndicatorClipped`, while header input/semantics are clipped to the remaining region. The minimum is not silently reduced. Changing parent allocation in the sample does not exercise native window resizing.

Run `./run-gallery.ps1` → **컨테이너** → **탭 넘침과 본문**. Click the Home body counter, choose Help through the ellipsis, then return Home to check the counter. Toggle width 160/520 and LTR/RTL. Settings is disabled. The enclosing ScrollView can bring the actual group or body into view through UIA ScrollIntoView; requesting only the header does not promise that its body is visible too.

Qualification: `artifacts/tab-overflow-2026-11-11/verification.json`. Managed SDK 11.0.100/runtime 11.0.0; CPU 20, GPU 48 (light/dark, MSAA 1/4, real pointer selection, same-frame body, acquired-image reuse, RTL and vertical allocation), external Windows UIA client 7/host 2 and observer 70. The external client invokes the Tab action, selects an actual hidden MenuItem, verifies the promoted Tab and real panel relationships, retires the old menu provider, and invokes the selected panel child. Focused regressions retain tab list 197, menu 57, accessibility 49, scheduling 35 and overflow 26.

Full indicator transitions, all size/appearance/icon combinations, menu icon slots, exact shadow16/forced colors and complete native selection/focus notification/narration coverage remain open. NativeAOT and runtime-image comparison remain separate unrun gates. These focused checks do not complete the original full goal. Custom tab presentations must explicitly implement `MeasureOverflow` and `PaintOverflow`; unsupported presentations fail rather than substituting another appearance.

The observer top navigation also enables overflow and clamps its free-flow header to available viewport space. A fixed 320×900 fixture reaches all ten real pages through pointer headers/menu items; no native window resizing is performed. Button toolbars use Wrap so narrow pages do not violate strict Row constraints. The Containers example includes a horizontal/vertical orientation control in addition to width and RTL.


### Drag-scroll tab headers

Set `UiTabListOptions.Scroll = new UiTabListScrollOptions()` to enable horizontal header scrolling. This is a separate choice from the existing `Overflow` allocation mode; do not enable both. Implementation Observer uses this mode for its top-level pages.

Drag the header or use the wheel. Each ellipsis appears only on an obscured edge, allowing left-only, right-only, or both. Clicking an ellipsis opens that side's hidden-tab context menu without moving the header. Choosing an item selects and reveals the original tab and panel. Selecting or focusing an edge tab also reveals one neighbouring tab in that direction to support continued navigation. Releasing a drag does not activate the tab under the pointer.

The bottom scrollbar overlays the header without reserving layout space. Defaults are120ms entry,400ms stationary hold and160ms exit; a held drag keeps it shown. Configure timing, curves and reduced motion through shared `UiScrollbarMotionOptions`, with per-tab overrides in `UiTabListScrollOptions.Motion`. Settled animation stops scheduling frames. See the [shared transition design and API](finite-transitions.md).


All scrollbar families use the same [shared motion module](scrollbar-motion.md).
