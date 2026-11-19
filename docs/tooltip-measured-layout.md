# Measured tooltip placement

Last synced: 2026-11-11

Current [auto-size/overflow](tooltip-auto-size.md) provides stateful remeasurement and visible/full content extents.

`UiPopupPlacement.ArrangeMeasured<TMeasure, TContent>` measures actual content before placement. A pure `IUiPopupMeasurement<TContent>` returns a positive finite body size. Apply `UiPopupMeasureRequest.Width` before wrapping and measuring height. The helper restores parent cursor/layout/constraint/input-slot and next-item state; input processing is rejected during measurement. Policies must not draw or mutate application state.

`MatchTargetWidth` remeasures intrinsic-width content at the target width before candidate selection. An explicit request width is preserved. Fixed-size `Arrange` rejects width matching so callers must supply a measured path. The pinned [matchTargetSize](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/matchTargetSize.ts) makes one attempt per placement lifecycle.

`Arrange<TOffset>` and `ArrangeMeasured<TMeasure, TContent, TOffset>` select a pure `IUiPopupOffset` statically. Its context includes target bounds, measured size, logical candidate, options and physical `UiPopupSide`. Return main/cross offsets before arrow height. `UiConfiguredPopupOffset` reads Gap/CrossAxisOffset. Policies can be evaluated repeatedly and must not depend on invocation count. Physical Side exposes the source callback's direction convention in RTL.

The generated consumer sample implements real Type200 text measurement and wraps paint at the resolved body width. It uses target-width matching and a custom compiled offset policy. Existing frame text/options capture remains in effect.

The retained core1.6.8 and pinned matchTargetSize/coverTarget source execute against an explicit unit-scale rectangle and line-measurement platform.384 new cases cover LTR/RTL,12 placements, existing explicit widths and callbacks using measured size/physical side:192 enable matching,196 request a width,192 use dynamic policy,188 reflow to multiple lines. Previous2304 reference cases remain. CPU also exercises actual DirectText wrapping and cursor/input-scope recovery. GPU uses real tooltip ownership, numeric upstream fixtures and literal text scenes. Evidence: `artifacts/tooltip-measured-layout-2026-11-11`.

Current [auto-size and overflow](tooltip-auto-size.md) implements the stateful lifecycle and actual content reflow. Measurement now returns UiPopupContentMeasure(Size, ContentSize) and requests carry Width/Height/MaxWidth/MaxHeight. Enabled auto-size axes clear previous inline sizes before matching. Official surface/control/accessibility behavior, NativeAOT/runtime comparison and the full goal remain open.
