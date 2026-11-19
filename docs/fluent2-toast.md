# Toast notifications

UiToastController and UiToasterWidget now display real nonmodal notification surfaces. Finite entry/exit/reflow motion, automatic dismissal, hover/focus pause, F6 return, action/close/Delete/Escape and announcement events are connected. Independent composition, determinate/indeterminate progress and captured native activation are connected. Further acceptance work remains open. Fluent.Toast is registered, with Fluent.Toaster as the viewport-host helper. Factory linkage is50/55 with five components pending. Toast has no unimplemented badge; the current NEW menu is Select.

## Official references and selected defaults

[Fluent guidance](https://fluent2.microsoft.design/components/web/react/core/toast/usage) recommends temporary feedback, seven seconds, hover pausing, up to four notifications and16px spacing. The [pinned implementation](https://github.com/microsoft/fluentui/blob/8ee8138daffeb7dbb95aaa7418d20472cc2e6fd1/packages/react-components/react-toast/library/src/state/vanilla/createToaster.ts) instead defaults to three seconds. Duxel follows the design guidance for its seven-second default. Timing starts after entry completion and ownership lasts until exit completion.

## Rendering a real surface

```csharp
var controller = new UiToastController();
using var toaster = Fluent.Toaster(controller);
controller.Dispatch(new("Saved", "The document is ready", UiToastIntent.Success));
// Render after page content, once each frame. The portal reserves no layout space.
toaster.Render(ui);
```

UiToasterOptions supports six positions, default292px width and16px margin/gap, RTL logical edges, ReducedMotion and host-supplied IsWindowActive. Native activation is captured automatically by the App host; custom hosts supply UiFrameInfo.IsWindowInactive. Surfaces contain an icon, semibold title, optional body/action and an optional close button. Configure CanDismiss, CloseLabel and ActionLabel/Action; the dismissible Gallery example retains recent notification history separately.

Moving surfaces use projected text; settled surfaces return to physical LCD text and cached GPU paint. The host schedules a single future expiry wake. The portal remains at the viewport edge while page content scrolls. F6 enters an interactive toast or returns to the previous control. Escape dismisses notifications; Delete dismisses the focused notification. External assistive-technology speech has not been verified.

## Low-level lifecycle contract (diagnostics)

Dispatch returns a stable ID. MarkVisible completes entry. SetPaused manages independent Hover, Focus, WindowInactive and Explicit reasons. Advance begins expired exits; Dismiss begins an explicit exit and Remove completes it. DismissAll clears waiting notifications and starts visible exits.

All mutations are serialized on the UI owner thread. Reentrant mutations from Changed are rejected. There is no background timer or polling loop. UiToasterWidget connects NextTimeout to the shared event-driven scheduler and supplies Advance/entry/exit completion. Direct controller consumers must provide that host behavior themselves.

- Lifecycle states are Queued, Entering, Visible and Exiting. Queue time never consumes visible duration.
- Limit is1–4(default4). Visible snapshots are newest-first. Waiting entries sort by descending Priority then FIFO. Exiting content holds its slot until removed.
- Timeout=null is persistent. Nonpositive durations are invalid. Content-only updates preserve remaining time. Explicit restartTimer or a changed Timeout resets duration.
- Pause reasons are independent; releasing the final reason resumes the exact unused duration. Hover and inactive-window pausing can be disabled separately.
- Immutable Visible/Queued snapshots change only with lifecycle changes. Idle Advance does not publish events.
- Info exposes Polite announcement priority; other intents expose Assertive. UiToasterWidget emits Announce on visibility and content revisions. External assistive-technology speech remains unqualified.

## Gallery and remaining work

Run `.\run-gallery.ps1` and open **Fluent controls → Notifications and progress → Toast**. The upper example shows actual notification surfaces. The lower example manually advances lifecycle state. Both show adjacent current source.

[Surface evidence](../artifacts/fluent-toast-surface-2026-11-18/result.json), [lifecycle foundation](../artifacts/fluent-toast-lifecycle-2026-11-18/result.json). Nested modal pointer/Escape/close behavior, long-content layout and Windows mixed-language action labels have focused evidence. Actual Linux, NativeAOT and external assistive-technology acceptance remain open. MessageBar/Popover aliases do not constitute Toast completion.

## Independent composition and progress

`UiToastWidget` can participate in normal content layout without a controller. `Show()` and `Hide()` control visibility; hidden content reserves no space. Configure `Width`, `RightToLeft`, `ReducedMotion`, and `AnimationPaused` independently.

```csharp
using var toast = Fluent.Toast(new("Uploading", "Keep visible until completion")
{
    Timeout = null,
    Progress = new(.35f, "35% complete"),
    CanDismiss = true,
    CloseLabel = "Dismiss notification"
});
```

A null `Progress.Value` displays a spinner; a value from zero to one displays a progress bar. Progress notifications require explicit `Timeout = null`. Numeric-only updates do not repeat the same announcement. Remove `Progress` and update the title/body on completion. Use `DisableAnnouncement` for static demonstrations that should not announce automatically. The Gallery provides four adjacent-source examples: live portal, independent composition, progress composition, and lifecycle diagnostics.

## Native window activation

Windows `WM_ACTIVATE` and Linux X11 FocusIn/FocusOut populate `InputSnapshot.IsWindowInactive`. App wakes the idle renderer when activation changes and forwards `UiFrameInfo.IsWindowInactive`. Custom hosts can supply this field; its default preserves active behavior for existing hosts. Read the current state through `ui.IsHostWindowActive`. `UiToasterOptions.IsWindowActive = false` remains an independent manual pause; setting it true cannot override native inactivity. While inactive, automatic expiry and progress spinners pause and resume from the remaining duration.

Windows activation uses the low word specified by the [official WM_ACTIVATE contract](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-activate).

## Long content and constrained screens

The host reduces visible capacity according to viewport height and text scaling. Deferred notifications retain their unused duration while queued. A focused notification is preferentially retained; exiting slots remain occupied until their existing exit completes.

Content taller than its allocated surface uses the existing ScrollView for title, body, progress, and actions. The close button and status icon remain pinned. Offscreen actions support keyboard/accessibility ScrollIntoView. Use **Show long notification** in the Gallery to exercise it. RTL mirrors icon/close/action logical edges and switches title/body/status paragraph direction and per-line alignment. Action labels also use shared native paragraph shaping; mixed-language order and actual invocation are verified. Actual Linux qualification remains open.

## Controller and host ownership

One controller supports one live `UiToasterWidget`. Duplicate attachment throws `InvalidOperationException` before changing notification snapshots or queue capacity. Use separate controllers for separate windows or independent surfaces.

After disposing a host, attach a new host to the same caller-owned controller. Host disposal preserves notifications and restores the controller's configured capacity.

Outside rendering, a `Changed` callback may dispose its host. Capacity restoration and its resulting notification run after the current callback dispatch, preventing recursive callbacks. Attaching a new host or mutating the controller inside `Changed` remains prohibited. External observer exceptions are propagated while owned surface resources are still released.

## Paragraph direction and compact width

Title, body, status, and action labels reuse InlineText and native paragraph shaping. Original source order is preserved; RTL sets the paragraph base direction and aligns each line physically right. Title/body retain 14/20 typography and status retains 12/16. Shared progress fill follows the logical start.

Below 160px, text moves beneath the icon/close header to preserve a usable text column. When less than the complete 76px header is available, the host queues notifications and restores them when space returns. Gallery mixed-language, direction, and width controls show the behavior and update adjacent source.

Registration evidence: [factory, Gallery, source compilation and host builds](../artifacts/fluent-toast-registration-2026-11-18/result.json). Toaster is a host helper, not a 56th canonical component.
