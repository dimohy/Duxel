# Compiled tooltip targets

Last synced: 2026-11-11

Current [shared positioning](tooltip-positioning.md) adds a layout/options snapshot, 12 logical positions, RTL, viewport collision handling and custom arrow anchors. Presentation signatures now accept/return the documented layout contract.

Current [surface nodes/events](tooltip-events.md) add the real target child, ToolTip role, open/close and asynchronous event lifetime during removal.

Current [target accessibility](tooltip-accessibility.md) adds description/label/inaccessible relationships, Field preservation and a separate Windows UIA client qualification. Nested targets agree on the nearest surface/semantics; text is captured per target frame.

`UiTooltip<TBehavior, TPresentation>` owns a real target and preserves its natural measurement and parent allocation. Behavior selects requests; presentation supplies surface arrangement and paint. `UiHoverFocusTooltipBehavior` supports target hover, navigable descendant focus and pointer movement into the open surface. Disabled targets may provide hover explanations.

With `Visible = null`, the window resolves one automatic owner after targets render. Show/hide delays default to 0.25 seconds and use the scheduled-frame clock. Handover from a visible target is immediate. Returning to the target or surface cancels hiding. Escape consumes the event before child handlers, retains focus and suppresses automatic reopening until the trigger is left. A later Escape reaches the child again when no tooltip is open.

Set `Visible = false` or `Visible = true` before first render to use caller-owned visibility. The mode is fixed at first render. `VisibleChangeRequested` receives the requested value and the `Interaction`, `Escape`, `Handover` or `HostHidden` reason. Assign `Visible = change.Visible` to accept the request. The next frame observes that assignment. External values take effect without an interaction delay. Declined requests leave `Visible` intact, and declining handover can leave several controlled surfaces open. Escape goes to the earliest still-visible registration. `IsOpen` tracks the actual presentation. Omission, full clipping, a higher popup, context transfer and disposal release presentation state while preserving the supplied value for a live widget.

On Windows, minimizing or hiding the host removes visible tooltip surfaces from painting and accessibility and issues `HostHidden` dismissal requests. When minimizing leaves no drawable area, the app processes the visibility transition once with the previous layout extent and submits no GPU frame. Restoring honors the caller's latest `Visible` value. The automatic owner stays suppressed until its trigger is left. The host's `UiHostVisibility.HideSequence` preserves a hide followed by a restore between two renders. A window covered by other windows retains its native visibility state. Platform backends supply `IPlatformBackend.HostVisibility`. Direct frame producers can set `UiFrameInfo.HostVisibility`.

The surface uses a popup tier and the frame's root clip, escaping ancestor clipping while its target remains visible. It does not advance layout. Target input/rendering uses shared allocated-child constraints. Invalid surface geometry fails explicitly. Measurement does not register a popup or consume input. `Text`/`Options` are mutable between frames; nullable `ShowDelaySeconds`/`HideDelaySeconds` accept finite nonnegative values and use defaults when null. The wrapper owns/disposes its child. Window ownership uses no idle target dictionary; its new allocation/speed costs have not been measured.

The Footer button in `layout_custom_contract_fba.cs` uses a generated `ConsumerTooltip` with a Fluent surface and a custom offset policy. `UiWidgetStyle(typeof(UiTooltip<,>), ...)` forwards constructors to the closed pair. The sample acknowledges requests by assigning the supplied value and also exercises programmatic visibility. Hover or Tab-focus Footer, then press Escape to dismiss.

```powershell
./run-fba.ps1 samples/fba/layout_custom_contract_fba.cs -NoCache
```

Earlier ownership checkpoint: fixed .NET11 RC3: build warnings/errors 0; CPU 473 including 29 new checks; Tooltip GPU 224 across light/dark, raster/vector targets, MSAA 1/4 and repeated frames. Independent literal images cover handover/Escape/clipping/disabled/omitted/pending targets. Existing input 400, FluentInput 1362, multiline 2560, containers 237, allocation-routing 2317, accessibility 40 and lifetime 101 pass. Previous 693 input and 3857 multiline images match exactly. The sample builds and exits normally after a 15-second exercise. Evidence: `artifacts/tooltip-owner-2026-11-11`.

`Fluent.Tooltip` is still unavailable. Programmatic focus-origin and browser document visibility, full pointer event parity, full property-change/shutdown notifications, multi-primary target relationships and current Native/performance remain open. Legacy active editors without navigation registration use the tested hover/Escape path. See the [official contract](fluent2-tooltip.md).
