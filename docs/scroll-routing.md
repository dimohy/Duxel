# Nested scroll input routing

2026-11-10 [Owned ScrollView](scroll-view.md): Dux.ScrollView owns finite viewport measurement, real child placement, offset, scrollbar lifetime and invalidation. CPU47 and separate GPU/routing1933 checks pass. World-coordinate precision loss fails explicitly; rebased large content/virtualization and current Native remain pending. Container stages stay3/6. Sample: `layout_scroll_view_fba.cs`.

The 2026-11-10 change establishes the input kernel used by existing child viewports and internal windows. It does not complete the owned scroll widget or virtualization stage.

`EndChild` offers each wheel axis to the innermost hovered viewport. A viewport consumes the event only if its clamped offset changes. An inner viewport already at the requested boundary leaves the event for its enclosing child or internal window. Each axis has independent ownership. If a viewport moves partway to a boundary, it consumes the whole event; fractional residual wheel propagation is not implemented.

Wheel hit testing uses the positive-area intersection of viewport bounds and caller clip, the popup tier and the current internal window's order. Both shared scrollbar renderers use the same hit test for initial thumb capture and track clicks. Higher popups and clipped tracks cannot capture the pointer. A thumb that already owns the pointer continues dragging outside the clip until release. Popup scrollbars retain their own tier independently of their parent window's rectangle.

Internal window wheel steps use the measured text line height, including DirectText configurations whose atlas line height does not provide the actual text metric. No widget/context fields, caches or alternate renderer paths were added. Content layout and persisted offsets remain in the existing kernel. Existing child frame latency is preserved: wheel processing at `EndChild` updates the offset for the next requested frame.

## Verification

Evidence: `artifacts/scroll-routing-2026-11-10/verification.json`. The original 13-condition child fixture failed seven boundary/clip conditions before the fix. Additional tests exposed the internal-window line-height path before its correction. The final fixed .NET11 RC3 build has zero warnings and errors; the expanded regression passes 1,645 checks: 1,133 CPU checks and 512 GPU checks. The new scroll scope contains 18 CPU checks plus 32 GPU offset assertions and 32 exact acquired-image comparisons. Existing routing and leaf tests remain included, so these scopes must not be added as unique requirements.

Raster/vector, Light/Dark, MSAA 1/4 and three frame slots cover vertical/horizontal movement and boundary handoff. Images are compared against independently assigned expected offsets after the event frame. The run contains 1,025 readbacks including warmups and existing regressions. Pixel equality does not imply upload, CPU allocation or FPS improvements.

`samples/fba/layout_scroll_routing_fba.cs` demonstrates nested viewports, clipped input, live row counts and clickable rows. Prefer `./run-fba.ps1 samples/fba/layout_scroll_routing_fba.cs -NoCache` with the supported shell. The recorded smoke uses a same-body local-project copy because of the existing PS5 helper limitation; its build and 15-second exit succeeded. This checks startup, not manual OS interaction. Current Native/runtime comparison, external Windows UIA and official visual approval remain unverified. Resize remains deferred by the user.

## Remaining container work

The container ledger stays at 3/6. The later [UiScrollView checkpoint](scroll-view.md) supplies finite measurement, actual allocation, ownership/disposal and invalidation. Rebased large content and virtualization remain incomplete. Legacy list boxes, combo boxes and multiline editors retain their existing wheel handling; their wheel blocks have not all been migrated to this helper. The change does update their shared scrollbar hit testing.
