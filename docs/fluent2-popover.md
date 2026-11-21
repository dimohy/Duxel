# Fluent Popover

```csharp
var name = new UiState<string>("정세일");
var editor = Fluent.Input("Name", name.Binding, 64, 240);
using var popup = Fluent.Popover("Edit profile", editor, options: new(Width: 320, WithArrow: true));
popup.Render(ui);
```

Create the widget once in its screen owner, call only `Render` each frame, and dispose it when that screen owner ends.

Popover owns its trigger and arbitrary `UiWidget` content. Do not reuse the content under another parent or render it separately. The default surface is nonmodal. It measures natural content height and uses an owned ScrollView when the viewport or `MaximumHeight` limits that height. Configure `Width`, `Size` (Small/Medium/Large), `Appearance` (Neutral/Inverted/Brand), and `Positioning`. Default placement is above/center, adjusted at screen edges by the existing placement engine.

`Show()` requests opening on the next render. `Close()` returns whether closing was accepted. Optional `Open : UiBinding<bool>` reads the authoritative getter after writing the setter, preserving rejected open/close requests. `IsOpen` reflects the actual surface; `OpenChanged` reports actual transitions only. Hidden or disabled owners stop publishing their surface even when the external model remains true. A true model can reopen when its owner becomes visible again. Closing and tab changes retain the content instance and editing model; disposing the owner disposes its content.

Autofocus targets the first eligible child. Information surfaces without an eligible child receive focus themselves. `DisableAutoFocus` preserves existing focus. Nonmodal Tab can leave and return; `TrapFocus` cycles inside. Escape from the surface, outside pointer presses, and optional outside scrolling with `CloseOnScroll` dismiss it. Escape during IME composition stays with the composition path. Escape and explicit close restore trigger focus. Nested surfaces dismiss from the innermost level.

The default accessibility surface is `Group`; trapped surfaces publish `Dialog` and modal metadata. The trigger exposes actual ExpandCollapse state and its controlled surface. Ordinary children keep their editor/Invoke contracts. ARIA dialog maps to Windows UIA Pane under [Core AAM](https://www.w3.org/TR/core-aam-1.2/), rather than a separate OS window or Window pattern; see the [Microsoft Pane contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportpanecontroltype).

```powershell
./run-gallery.ps1
```

Select **Popover**, using the header overflow menu if hidden. Open profile editing and exercise Korean input, commands, closing, focus trapping, sizes, appearances, and rejected closing. The separate **Pop-up input** page demonstrates the generic engine input modes. The FPS display requests no periodic frames. Popover schedules finite entry frames, then returns to event wait.

Behavior and appearance reference the pinned [Popover source](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/Popover/usePopover.ts) and PopoverSurface at the same commit. Size padding12/16/20 plus a1px transparent border, radius 4, Shadow16, and Body1 are implemented. `WithArrow` defaults to false. Enabling it uses a 6px Small arrow or 8px Medium/Large arrow, matching the rotated square, 2px rounded tip and transparent-border geometry. The combined body and arrow receive the official sequential drop shadows. Full Tabster behavior, forced colors and physical screen-reader conformance remain incomplete; full React parity is not claimed.

Evidence: `artifacts/popover-enter-2026-11-13/verification.json`; large GPU outputs: `L:/Cache/Duxel/popover-enter-2026-11-13-evidence/`. Managed .NET11 validation does not complete the original fixed RC3 NativeAOT, runtime-image, device, or performance gates.

Each owner leases its current shape through `UiPopupSurface`. Unchanged dimensions, colors, scale and physical pixel phase reuse the raster; integral pixel translation does not rasterize again. Shape/appearance/scale/fractional-phase changes prepare a new raster. Hiding, transfer and disposal release the lease; the shared cache can retain unused entries within its budget. Texture statistics measure queued upload/residency, not GPU completion or total process memory. `IUiPopoverPresentation.PaintSurface` receives `UiPopupSurface` and `UiPopupLayout`, including actual arrow placement for custom presentations.

Arrow dimensions follow the official `Size` values and override `Positioning.ArrowHeight`; the default `WithArrow: false` omits the arrow. Dismissal uses the shared arrow shape, while a separate arrow placement rectangle occludes lower-layer pointer targets. Body and arrow registrations share their owner/tier and are released together. [Official arrow geometry](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/createArrowStyles.ts).

`TriggerMode` is `Click` (default), `Hover`, or `Context`. Hover opens immediately on entry and keeps ordinary click/keyboard toggling. Leaving the trigger, surface and nested surfaces closes after `MouseLeaveDelaySeconds` (default 0.5 seconds); reentry cancels it. Rejected opens are requested once per entry and rejected delayed closes once per leave, avoiding a past-deadline loop. Mode changes, hiding, disposal and later same-frame closes cancel pending work. Deadlines use the existing `RequestFrameAt` path at frame end; no worker thread, CLR Timer or continuous render loop is introduced.

Context ignores ordinary left clicks and uses the completed right-click release position as a virtual point. Focus the trigger and press Menu or Shift+F10 for keyboard access. UIA Invoke/Expand and `Show` open at the trigger center. Context always dismisses on outside scrolling without requiring `CloseOnScroll`. Shared placement accepts zero-width/height point targets while keeping positive body/boundary dimensions and rejecting negative/nonfinite targets. Windows [VK_APPS](https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes) maps to the appended `UiKey.ContextMenu`, preserving earlier enum values.

`CoverTarget` automatically omits the arrow. Default arrow padding is 8px, twice the official radius 4. Behavior follows the pinned [trigger](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/PopoverTrigger/usePopoverTrigger.ts), Popover and PopoverSurface sources at the same commit.

Close the sample popup and use **Change popover trigger** to cycle Click→Hover→Context. The Hover demo disables autofocus to expose static event-wait behavior; focusing its editor can still schedule normal caret blinking. CPU checks 500ms scheduling/cancellation/rejection/lifetime. GPU uses zero leave delay for deterministic actual pointer hover/right-click pixels and placement. Native checks actual Windows mouse input, UIA and the Menu key; its manually rendered input fixture is not an idle application CPU/power measurement.

An owned menu portal participates in hover retention and outside-pointer containment. Hovering it cancels the leave deadline; clicking its item invokes once without dismissing the parent Popover. Escape closes the menu first, then the parent on the next press. Open menus retain their trigger focus inside trapped Popovers too. Observer → Popover → Menu in popover exposes the actual path.

`MotionEnabled` defaults to `true`. Official entry combines 400ms, `cubic-bezier(0,0,0,1)`, a placement-aware 10px slide and whole-surface fade. Closing unmounts immediately without exit animation. Choose `MotionEnabled: false` for reduced movement or a static surface. The Observer exposes **Toggle popover animation**.

Live content renders once at its displayed coordinates, so pointer input, focus, IME and accessibility bounds move together. Existing dual-backdrop compositing fades overlapping children as one group. Moving text uses Projected mode, then returns to ordinary painting. Identical time, paint and referenced texture versions reuse the group result; unrelated texture updates do not invalidate it. Body and arrow retain the settled raster. Actual movement or content changes can repaint the group scene; this is not a claim that all motion is a single translated quad.

Entry schedules only its next frame through the existing scheduler, at60Hz for at most400ms. Completion, hiding, disposal and opt-out remove remaining motion deadlines. Temporary composites use normal deferred texture destruction. Focused editors retain ordinary caret scheduling. Existing2D paint-group limits apply: no3D scenes, external draw callbacks or mutable GPU particle sources; the paired targets fit64MiB. Such content explicitly selects ordinary painting with `MotionEnabled: false`.

Pinned sources: [PopoverSurfaceMotion](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-popover/library/src/components/Popover/PopoverSurfaceMotion.ts), [timing/curve tokens](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-motion/library/src/motions/motionTokens.ts), [physical direction](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/usePositioningSlideDirection.ts).

Custom presentations declare screen-space painting with IUiPopoverPresentation.PaintBounds(context, layout, options). Its default is the current viewport, retaining wide decoration. The Fluent presentation explicitly preserves its existing88px shadow extent and compact targets. Declared bounds must be finite, positive and contain the visible body. Bounds and the existing64MiB target budget are checked before surface painting. Presentations remain responsible for including all decoration. These are visual compositing bounds; decoration clicks retain ordinary outside-body dismissal. Observer exposes Open wide decorative popover. Evidence: artifacts/popover-paint-bounds-2026-11-13/verification.json.


`UiPopoverOptions.CloseOnFocusLeave` defaults to `false`. Enable it to dismiss when focus leaves the trigger, content, or an owned nested surface for another control, preserving that destination focus. It also observes departure from the trigger when autofocus is disabled. A rejected binding write keeps the surface open; reentering and leaving the region rearms dismissal. No Timer or periodic frame request is added. In Implementation Observer, choose Popover → **Toggle close on focus loss**. This supplies shared behavior required by the official InfoButton; it does not complete InfoLabel itself.
