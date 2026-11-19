# Tooltip offsets, cover and shift limits

Last synced: 2026-11-11

Current [auto-size/overflow](tooltip-auto-size.md) provides stateful remeasurement and visible/full content extents.

Current [measured placement](tooltip-measured-layout.md) adds compiled per-candidate offsets and actual target-width content remeasurement.

`UiPopupPositioning.Gap` now accepts finite negative values. It is the signed main-axis offset before adding arrow height. `CrossAxisOffset` moves above/below placements horizontally, reversing in RTL, and before/after placements vertically. `CoverTarget` moves the body toward the target by the target's main-axis size before flip selection. Defaults preserve the existing geometry.

`OverflowBoundaryPadding` uses `UiPopupBoundaryPadding(Start, Top, End, Bottom)` or the uniform constructor `new(value)`. Start/end reverse in RTL; top/bottom stay physical. Finite negative padding expands the effective overflow boundary. Padding applies to shifting, while the separate flip boundary remains unchanged.

`ShiftToCoverTarget` allows the body to shift along the facing axis when space is insufficient, with the source limiter preserving its target attachment range. `TetherMode` follows Fluent's source options:

| Mode | Fluent flag | Facing-axis shift | Facing-axis limiter |
| --- | --- | --- | --- |
| Default | unset | follows ShiftToCoverTarget | follows ShiftToCoverTarget |
| Disabled | true | disabled | enabled |
| DisabledAllAxes | all | enabled | disabled |

The parallel edge axis always shifts. Ordered limiter comparisons preserve behavior when large negative offsets invert its limits. Arrow alignment resets retain coordinates, skip offset/flip and rerun cover/shift, matching the source middleware order. This includes a second cover adjustment when cover and an arrow reset combine.

Reference: pinned [Fluent coverTarget](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/coverTarget.ts), [shift composition](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/shift.ts), and locked Floating UI core1.6.8. The reference runner executes the original core module and the retained cover function with imports/types removed and physical-side extraction inlined. It supplies explicit unit-scale rectangles and source-matched shift options; this does not qualify DOM ancestor/scale discovery.

1536 additional reference cases include negative offsets, all12 positions in LTR/RTL, three tether modes, logical padding and cover/arrow-reset combinations. They cover286 of288 position/direction/cover/shift/tether combinations, including64 cover+arrow resets. All pass at tolerance0.001, alongside the previous768 cases. CPU2863 and Tooltip GPU464 pass; GPU adds12 actual-owner reference scenes across light/dark and MSAA1/4. The custom consumer sample uses cross offset8, uniform boundary padding8 and ShiftToCoverTarget. Evidence: `artifacts/tooltip-offset-cover-2026-11-11`.

Official rounded arrow/shadows, controlled visibility and complete accessibility property/shutdown events remain open, along with full Fluent qualification, current NativeAOT/runtime comparison and the original full project goal. No performance improvement is claimed. Core distributes both Floating UI and Microsoft MIT notices.
