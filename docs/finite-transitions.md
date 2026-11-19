# Finite value transitions and tab-scroll motion

## Reuse and purpose

Keep the existing string-key `AnimateFloat`, UI clock and `RequestFrameAt`. An owned `UiFloatTransition` gives widgets a lifetime-scoped scalar timeline without retaining removed widgets in a global ID dictionary. Both APIs share `UiAnimationCurve`; new consumers use finite deadlines.

## Contract

Initialize explicitly with `new UiFloatTransition(0)`, then sample through `ui.AnimateFloat(ref transition, target, options)`. Pure `Update` returns value, running state and the next absolute deadline. Retarget from the value sampled at the current time, including reversal during entry or exit. Schedule only `min(end, now + interval)`. Settled values and reduced motion have no deadline. Validate finite input, supported curves and a monotonic UI clock; use `Reset` when transferring to another clock. No Timer or background thread is created. The legacy string-key API retains its existing lifetime and continuous-flag behavior.

## Tab header consumer

Drag and wheel move the actual header. Each edge ellipsis appears only when that side contains obscured tabs. Clicking it opens that side's tab context menu without moving the header. Choosing an item updates the original selection binding and panel, then reveals the header.

The bottom 3px thumb overlays the header without adding layout space. Defaults:120ms entry,400ms stationary hold and160ms exit, with durations and curves configured through `UiTabListScrollOptions.Motion.Enter` and `.Exit`. A held drag keeps it shown; stationary holding does not request periodic frames once entry settles. Content removal and hidden/transferred owners cancel through their lifetime. Reduced motion resolves immediately.

## Validation

Check pure sampling, continuous retargeting, reduced motion, exact settlement, UI deadlines, drag-release suppression, both edges and middle, directional menus, original panel binding, narrow viewports, keyboard and RTL through CPU/GPU fixtures. NativeAOT, physical screen readers and complete Fluent conformance remain separate gates.
