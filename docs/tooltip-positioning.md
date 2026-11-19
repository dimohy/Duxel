# Shared tooltip positioning

2026-11-13: shared placement accepts zero-width/height virtual point targets. Negative/nonfinite targets and nonpositive body/boundary dimensions remain invalid. [Popover context mode](fluent2-popover.md) uses actual mouse points.

Last synced: 2026-11-11

Current [auto-size/overflow](tooltip-auto-size.md) provides stateful remeasurement and visible/full content extents.

Current [measured placement](tooltip-measured-layout.md) adds compiled per-candidate offsets and actual target-width content remeasurement.

Current [offset/cover support](tooltip-offset-cover.md) adds signed main/cross offsets, logical overflow padding and source-matched cover/shift limits. Default options retain the geometry below.

`UiPopupPlacement.Arrange(target, size, boundary, options)` provides 12 logical placements: above/below center/start/end and before/after center/top/bottom. RTL reverses before/after and horizontal start/end. Defaults are Above, gap 4, no arrow and arrow padding 8. Arrow height adds to the body gap; optional `ArrowWidth` defaults to twice its height. `UiPopupLayout` contains body bounds, resolved position, optional triangle anchors and `ArrowCenterOffset`. `UiControlContext.Triangle` respects the current clip. The sample paints a custom triangle, not the rounded Fluent CSS arrow.

Unless pinned, candidate order is requested, reversed alignment, opposite side with original alignment, then opposite side with reversed alignment. Centered placements try only requested/opposite sides. Nonempty immutable `FallbackPositions` replaces this default candidate list after the requested position. `FlipBoundary` and `OverflowBoundary` separately override the supplied boundary. Selection follows locked Floating UI core 1.6.8: first candidate fitting its facing side and both cross sides; otherwise favor facing-side fits ranked by signed alignment-side overflow; if none fit that side, minimize summed positive overflow of those three sides. Ties preserve candidate order. The body shifts parallel to its edge; oversized dimensions are preserved and clipped. Arrow padding follows the upstream one-unit reserve, and aligned small targets can shift the body once before reapplying edge constraints. Width/height remain explicit, including an arrow wider than its edge. `ArrowCenterOffset` reports the remaining displacement from the target center. Target availability belongs to the tooltip owner.

`UiTooltipOptions.Positioning` is captured with text before target rendering. `IUiTooltipPresentation.Arrange` now accepts this snapshot and returns `UiPopupLayout`; `Paint` receives that same layout. Late option writes apply next frame. Custom implementations must update both signatures and wrap their geometry in a layout or call the shared resolver. `layout_custom_contract_fba.cs` uses AboveStart and arrow height 6.

Reference: pinned [Fluent React positioning source](https://github.com/microsoft/fluentui/tree/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src), whose lock selects core 1.6.8/dom 1.6.12/utils 0.2.8. See [collision qualification](tooltip-collision.md). Unit-scale rectangle calculations match that core's pipeline across the recorded cases. Full DOM/Floating UI/Fluent equivalence, ancestor boundary discovery, rounded arrow/shadows and the Fluent factory remain open. MIT attribution is distributed with Core.

Evidence: `artifacts/tooltip-positioning-2026-11-11`. Literal fixtures cover 12 positions in LTR/RTL, four boundaries, shift/arrow padding, pinned/oversized geometry, translated boundaries and invalid options. GPU fixtures consume the actual tooltip owner and late option writes against literal body/triangle images. Existing target/accessibility/event tests remain in the CPU suite. Current NativeAOT/runtime comparison remains pending the known toolchain location issue.
