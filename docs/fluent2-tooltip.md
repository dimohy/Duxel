# Fluent 2 Tooltip reference contract

Last synced: 2026-11-11

The shared Tooltip and popup rasterizer reuses four temporary spans across the two shadow passes within each rasterization. Scratch storage is released to the GC after that call and the output texture owns an independent array. A mixed-size NativeAOT control found lower sampled memory peaks and approximately 29% fewer temporary allocations with this approach than with the original rasterizer. [Memory policy comparison](archive/performance/optimization-session-2026-11-19.md#popup-scratch-retention-controls)

Current foundation: [compiled tooltip targets](tooltip-targets.md) implement actual target composition, timed ownership, deferred popup paint and Escape consumption with CPU/GPU/sample evidence. [Target accessibility](tooltip-accessibility.md) now qualifies description/label/inaccessible relationships and actual Windows UIA properties. The generated Fluent factory now binds the existing target behavior and Fluent presentation. Text-property and shutdown notifications are qualified through an external client; complete official conformance remains pending. [Surface nodes and open/close](tooltip-events.md) are qualified with an external UIA client. The checklist below records the full Fluent qualification scope.

`Fluent.Tooltip(target, text, options)` now provides the source-generated target wrapper. Existing immediate-mode `BeginTooltip`/`SetTooltip` APIs do not establish Fluent conformance: they display immediately at the mouse position with a 16px offset and 6px padding.

## Authority and behavior

The [official Web guidance](https://fluent2.microsoft.design/components/web/react/core/tooltip/usage) defines brief, nonessential, noninteractive text associated with a target. Use Popover for interactive content. Hover or focus reveals the tooltip; leaving or blurring dismisses it. Moving into the tooltip must keep it available. The default target gap is 4px; an optional arrow points at the target.

The pinned React implementation supplies additional reference defaults: 250ms show/hide delays, above-center placement, normal/inverted appearance, optional arrow disabled, one visible owner, immediate handover between targets, Escape dismissal with focus retained, and label/description/inaccessible relationships. Description semantics must remain available while the visual is hidden. Controlled visibility must retain its change notification contract. These are reference contracts for the official component, not extra top-level components.

The [iOS guidance](https://fluent2.microsoft.design/components/ios/core/tooltip/usage) includes workflow/programmatic presentation and tap-away dismissal. Its page is marked Preview. The [Windows guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips) includes press-and-hold and Ctrl dismissal, and allows native-window placement and richer content. Record those platform differences explicitly; do not silently replace the site's Web defaults or introduce a second Windows theme.

## Observed visual baseline

The live [official Storybook](https://storybooks.fluentui.dev/react/?path=/docs/components-tooltip--docs) was checked in light and dark themes. Its deployed commit is unknown; it is not assumed to match the pinned repository snapshot.

| Property | Observation |
| --- | --- |
| Typography | Segoe UI, 12px, weight 400, line height 16px |
| Geometry | Maximum width 240px, radius 4px, transparent 1px border |
| CSS padding | Top 4px, right 11px, bottom 6px, left 11px; outer content insets include the border |
| Light normal / inverted | Fill `#FFFFFF` / `#333333`; text `#242424` / `#FFFFFF` |
| Dark normal / inverted | Fill `#292929` / `#3D3D3D`; white text |
| Shadow | Chained drop shadows: `(0, 0, 2px)` then `(0, 4px, 8px)`; light alpha .12/.14, dark .24/.28 |
| Arrow | Optional, reference height 6px; included in the surface's filtered silhouette |

Five screenshots and computed styles are retained in `artifacts/fluent-tooltip-2026-11-11`. Escape dismissal with the trigger focus retained was observed. These are official reference captures, not Duxel comparison results. The browser theme was restored to Web Light.

## Implemented path and remaining qualification

`Fluent.Tooltip(Fluent.Button("Save"), "Save a local copy")` statically binds `UiHoverFocusTooltipBehavior` and `UiFluentTooltipPresentation`. It owns its real target and shares the existing delay, Escape, placement, accessibility and lifetime implementation. Set `Positioning.ArrowHeight = 6` for an arrow or `Relationship = UiTooltipRelationship.Label` for a naming relationship. Caller-controlled `Visible` and `VisibleChangeRequested` remain available. The `--tooltip` mode in `widget_tree_theme_fba.cs` and the observer's Tooltip tab exercise the generated factory.

The factory checkpoint passed nine focused CPU checks and 24 additional light/dark, MSAA1/4 GPU comparisons against the established generic consumer. Generator115 and catalog34-name checks passed. The complete focused Tooltip GPU suite passed792 checks. External factory-backed shutdown and visibility pairs passed4/6 and5/9. These are Duxel consumer-path checks, not official-browser pixel equivalence. Current NativeAOT/runtime-image comparison and complete official state/visual/accessibility qualification remain open. The following records the original implementation requirements; its underlying target, owner, Escape, surface and factory paths now exist:

1. Compose a real target wrapper with stable identity, pure measurement and statically selected behavior/presentation.
2. Resolve one window-local owner after all targets have registered; implement delay deadlines, handover, omission/disposal and scheduled wakeups without an idle per-target state table.
3. Consume Escape before child input and popup handlers; preserve focus and implement label/description relationships, including disabled target explanations.
4. Render target-relative placement, arrow, combined surface shadows and popup layering with root clipping. A tooltip may escape an ancestor clip while its target remains visible; a fully clipped target must not show it.
5. Connect a generated factory only after the actual consumer works. Qualify CPU timing/lifetime/input, GPU light/dark/placement/clipping, a runnable sample, accessibility and fixed .NET11 Native/runtime comparisons. Current Native toolchain location remains unconfirmed.

The catalog exposes 35 names: 16 of 55 top-level names and 19 parent-linked variants, with 39 top-level names missing. Full variant/state/visual/accessibility coverage has no confirmed denominator. Factory binding does not establish complete Fluent Tooltip conformance or a performance improvement.

Pinned reference: `microsoft/fluentui` commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`, Tooltip source and private constants. The copied `LICENSE.txt` contains the MIT grant and asset notes; GitHub's license API reported `NOASSERTION`. See the stage contract, catalog drift audit and reference manifest for provenance.

Current TabList catalog: `artifacts/fluent-tablist-2026-11-11/final-catalog-only/result.json`. See the [TabList foundation](fluent2-tablist.md).

## Target measurement delegation — 2026-11-12

Tooltip explicitly delegates ancestor measurement retention to its live owned target using CanRetainOwnedMeasurement(Target). Popup presentation and visibility behavior are not cached by this contract: scheduling, show/hide deadlines, actual target render, popup painting, Escape and ownership cleanup still execute. RenderTooltipTarget now queries the shared validated owned-child size cache. Known unchanged targets reuse a previously measured size; opaque targets still measure and validate on every render. Unknown targets remain dynamic. Borrowed/disposed target delegation fails explicitly. Field uses the same ownership guard. Evidence: artifacts/delegated-layout-retention-2026-11-12/verification.json.
