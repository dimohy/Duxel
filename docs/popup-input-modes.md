# Popup input modes — Popover foundation

The subsequent [Fluent Popover](fluent2-popover.md) adds owned content, autofocus, dismissal/restoration and lifecycle management. Implementation gaps below describe the earlier foundation checkpoint.

```csharp
if (ui.Button("Open details")) ui.OpenPopup("details");
ui.SetNextPopupPosition(new(300, 100));
if (ui.BeginPopup("details", UiPopupInputMode.NonModal))
{
    try
    {
        ui.Text("Use Tab to leave and Shift+Tab to return.");
        if (ui.Button("Close details")) ui.CloseCurrentPopup();
    }
    finally { ui.EndPopup(); }
}
```

`BeginPopup(string)` keeps its existing exclusive keyboard behavior. The new overload selects `UiPopupInputMode.Exclusive` or `NonModal`. Both retain popup paint order and pointer occlusion of covered controls. NonModal preserves background Tab stops and native focus/action eligibility; Exclusive keeps keyboard navigation at the highest current exclusive layer. This is keyboard isolation, not a native modal-window or ARIA-dialog declaration. Existing public `PreviousPopupBlockingRects` and the two-argument registration API keep their tuple/signature contracts. Invalid input-mode values fail explicitly.

Popup registration now stores a parallel owner/isolation metadata, swapped/cleared/removed together with each rectangle, so identical rectangles can carry independent registrations. Previous registrations gate frame-start keyboard processing; current exclusive registrations determine final Tab eligibility. Higher nonmodal layers remain navigable. Mode/geometry changes follow the existing previous-frame input model; this API does not promise a synchronous rebuild of controls already processed in that frame.

`CloseCurrentPopup` removes the current registration, releases only that owner's previous keyboard isolation, and requests one follow-up frame. Previous pointer rectangles remain as the input-frame baseline to prevent click-through. Repeated close calls in the same closed scope are harmless. Identical geometry belonging to another popup retains its own input mode; closing an exclusive popup restores next-frame controls without an extra idle capture.

Focus groups also respect their paint layer. A nonmodal portal rendered inside a Card does not inherit its NoTab trap or Escape handling, while the Card still traps its own controls and Enter targets an own-layer child. Popup layering still covers later base geometry; paint order is independent of keyboard isolation. The same shared button/input/focus pipeline is used, with no new renderer, shader, parser, native import or timer.

Run `./run-gallery.ps1` → **팝업 입력**. Open the popup, click a child, and try Tab/Shift+Tab. Change mode after closing it; compare background keyboard availability and actual inside/outside commands. The sample uses the existing fixed-size/positioned generic popup API and preserves its mode/counters. Close through the button or outside pointer click. This is an inspectable engine foundation: **Fluent Popover is still pending**, including its owned arbitrary-content surface, source-matched role/style/positioning, autofocus, Escape/focus return, controlled visibility, hidden-owner lifecycle and full conformance. The generic popup API does not acquire those behaviors merely by selecting NonModal.

Evidence: `artifacts/popup-input-2026-11-13/`; large readbacks: `L:/Cache/Duxel/popup-input-2026-11-13-evidence/`. Managed qualification is separate from fixed RC3 NativeAOT/runtime-image/device/performance gates. Source motivation: the pinned [Popover surface](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/PopoverSurface/usePopoverSurface.ts) distinguishes ordinary group and trapped dialog, while [Popover props](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/Popover/Popover.types.ts) default `trapFocus` to false. This foundation is not that full React component.
