# Owned scroll view

In the Gallery, open the Layout Examples page under Layout and Input and find **ScrollView** in the page. Switch between vertical and two-axis scrolling and scroll inside the fixed-size preview. The C# code directly underneath reflects the selected axes.

The Linux check `dotnet Duxel.Linux.App.Tests.dll x11-gallery-scroll-view <gallery-pid> <fresh-result.json> <X11-window-id-in-decimal>` runs in the Gallery's D-Bus and X11 session. It reads the inline C# through AT-SPI, sends wheel events on both axes, and checks that an item moves within the preview.

Use `x11-gallery-scroll-profile` with the same arguments for 24 alternating horizontal wheel inputs. Set `DUXEL_APP_PROFILE=1`, `DUXEL_APP_PROFILE_EVERY=1` and `DUXEL_APP_PROFILE_OUT` for the Gallery, and give the client the same output path. For Vulkan stage timings, also set `DUXEL_VK_PROFILE=1`, `DUXEL_VK_PROFILE_EVERY=1` and share `DUXEL_VK_PROFILE_OUT` with the client. It reports a line range for each log, including the scrollbar's settling frames.

The Vulkan log starts each successfully created swapchain with a `[duxel-vk-wsi]` line showing the V-Sync setting, available present modes, selected mode, image count and extent. In the WSLg lavapipe ScrollView run, V-Sync was off, `ImmediateKhr` was active and the CPU-side present p95 was 4.333 ms for 38 profiled frames.

2026-11-10 [Keyed virtual list](virtual-list.md): fixed-height vertical rows use double offsets and viewport-local placement for billion-item sources. CPU43 and expanded routing2317 checks pass; rows outside overscan dispose through existing ownership. Variable-height/other axes, cross-key recycling policy, external UIA and current Native remain open; container stages stay3/6. Sample: `layout_virtual_list_fba.cs`.

`Dux.ScrollView(child, options)` creates a `UiScrollView` that owns one `UiWidget`, its content offset and its scrollbar interaction lifetime. It uses the existing widget measurement, world-coordinate child placement, clip, wheel and scrollbar kernels. It is layout infrastructure, not a new Fluent visual component.

```csharp
var editor = Fluent.Input("Editor", text.Binding, 256);
using var view = Dux.ScrollView(
    new UiColumn(
        [editor, .. rows],
        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
    new(UiScrollAxis.Vertical, Width : 300, Height : 280));
view.Offset = new(0, 120);
view.AccessibleName = "Editor viewport";
```

`text` is a `UiState<string>` and `rows` contains owned widget instances. A child cannot belong to another owner. Disposing the view disposes its subtree once, releases scrollbar capture and removes its scrollbar auxiliary state. Real input children also release their buffers and undo history through their existing ownership path. A weak context reference allows the same scroll instance to move between UI contexts while releasing the old context's scrollbar state. Default instance identity keeps each view independent; parent identity requires the caller's normal distinct ID scope.

## Constraints and content

`UiScrollOptions` selects `Vertical` (default), `Horizontal` or `Both`, with optional `Width` and `Height`. Both viewport axes must be finite, supplied by parent constraints or explicit options. A tight parent allocation takes precedence. In an unbounded-height column, provide a height or an enclosing sized box. Missing finite bounds fail before child measurement; the view does not invent a default height.

The child is measured with an unbounded maximum on each scroll axis and a tight cross axis. Vertical scrolling therefore reflows text at the actual viewport width. Scrollbars reserve space only when content overflows. The bounded measurement loop resolves the dependency between vertical and horizontal gutters; nonconverging custom geometry fails explicitly. Zero and tiny allocations clip without creating negative thumb geometry.

`Offset` is a finite, nonnegative requested content offset. Pure measurement leaves it untouched; rendering clamps each axis to the current content range. Wheel handling occurs after nested content, so the nearest movable viewport owns each axis and an immovable inner view passes the event outward. Changes request the next frame. Like the existing child kernel, an event processed after drawing is visible in that next frame.

Set `UiScrollOptions.RightToLeft = true` to start overflowing horizontal content at its right edge. A larger `Offset.X` reveals content toward the left, and the vertical scrollbar gutter moves to the left edge. The wheel and thumb follow physical viewport motion. The vertical offset continues to measure from the top. The sample's `Direction` button switches the two-axis view between these directions.

The existing `UiRetainedLayout` payload carries prepared geometry for assigned children; cross-frame measurement reuse remains conditional. Child/property invalidation, constraints, typography/resource keys and scrollbar thickness govern reuse. Wheel movement leaves measurement intact. Non-retained content is measured afresh. No runtime theme registry, second renderer or global scroll-owner registry is introduced.

## Windows accessibility

Windows UI Automation exposes the view as a Pane and places accessible content under it. Set `AccessibleName` to describe the viewport. While the visible content overflows, the Pane exposes the Scroll pattern on the applicable axes. Percent and step requests move the owned offset before child rendering, so the published child bounds and visibility reflect that frame. The Pane and child hierarchy persist when the content fits.

Accessible descendants of overflowing views expose the ScrollItem pattern, including elements currently outside the viewport. `ScrollIntoView` uses their unclipped layout bounds and calculates the required movement from the innermost viewport outward. It completes after a frame publishes the revealed element with the same identity and preserves keyboard focus. The request can fail if the element disappears or another clip prevents it from being displayed.

On a retained Pane, Windows UI Automation reports changes to the two axes' scroll percentage, view size and scrollability, as well as Scroll pattern availability. Clients can read the new values from the same Pane as soon as they receive the notification. An axis that stops scrolling reports `-1` percent and `100` view size. Unchanged properties do not raise a notification. Scrollable Tooltip surfaces use the same publication path.

For RTL, the horizontal UIA percentage runs from `0` at the right to `100` at the left, and horizontal increments move the viewport left. `ScrollIntoView` still resolves physical clipping through nested viewports. [Microsoft Scroll pattern](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingscroll)

Retained panes and their rendered accessible descendants send `BoundingRectangle` and `IsOffscreen` changes after layout or clipping changes. The rectangle matches the provider's clipped screen bounds. If direct children are added, removed, moved to another parent or reordered, the retained Pane raises one `ChildrenInvalidated` event for that frame. Clients can navigate the updated list immediately. Moving a retained child into or out of the visible viewport leaves the list intact. [Microsoft Pane control type](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportpanecontroltype)

## Coordinate precision and remaining work

This implementation renders a complete child in world coordinates. It does **not** virtualize or rebase large lists. A million-unit test preserves the full 60-unit viewport. A billion-unit test exposed float cancellation that shortened the visible clip. The view now rejects such unrepresentable placement with `NotSupportedException` before child rendering/input, allowing only ordinary viewport arithmetic rounding. The old failure log and pre-guard source are preserved. This is an explicit unsupported case, not successful billion-unit rendering.

Rebased large-content rendering, visible-item realization, item recycling and the complete virtualization lifecycle remain required work. The container ledger remains 3/6 and the overall goal remains active.

## Evidence and sample

`artifacts/scroll-view-uia-2026-11-11/verification.json` records 52 ScrollView CPU checks and an external Windows UI Automation client driving the sample. The client found the editor under its Pane, moved it out of view and back without replacing its provider, and set both axes independently.

The sample's `Toggle row` adds or removes one accessible row while keeping its space in the layout. A separate UIA client verifies both list invalidations, the editor's visibility changes and the screen rectangle after a viewport width change.

`artifacts/scroll-view-2026-11-10/verification.json` records fixed .NET11 RC3 validation. The owned-view CPU suite passes 47 checks: measurement purity, axes/gutters, retention/invalidation/style changes, nested wheel ownership, real editor typing/undo/Core accessibility, disposal, context transfer, exceptions and representability. The separate expanded routing suite passes 1,933 checks, including 144 new offset assertions and 144 acquired-image matches. Its 1,313 readbacks cover raster/vector, MSAA 1/4, Light/Dark and three frame slots. These overlapping scopes are not unique-requirement counts or performance improvements. Build warnings/errors are zero.

`samples/fba/layout_scroll_view_fba.cs` demonstrates named vertical and two-axis views, a real editor, clickable rows, Width/Height changes and Top/End controls. Prefer `./run-fba.ps1 samples/fba/layout_scroll_view_fba.cs -NoCache` with the supported shell. The saved smoke uses a same-body local-project copy because of the existing PS5 helper limitation. Current Native/runtime comparison and official appearance remain unverified. Resize is user-deferred; .NET9/10 deployment compilation remains scheduled for the entire goal's end.

## Prepared arrangement during callbacks

An earlier control can change a later sibling after its parent has measured the frame. The original sample exposed a horizontal overflow (children624 against an already assigned384). A deterministic CPU callback reproduced it. Layout measurement reuse and consumption of prepared child bounds are now separate: an exact parent allocation uses its prepared geometry; invalidation causes the next parent measurement to recompute it. A glyph observer still disables cross-frame measurement reuse, but non-retained layouts keep a frame-stamped arrangement for the current assigned render. Row/Column, single-child boxes, Stack, Wrap/Grid and ScrollView share this mechanism. No child-source values or child arrays are snapshotted.

The existing payload gains one frame integer and is acquired once on first layout preparation even for non-retained containers. Non-retained first-acquisition cost is a remaining performance risk; this is not presented as an optimization. The narrow managed10,000-Padding diagnostic pair kept construction5,280,384B, first layout2,320,000B, steady0B and invalidation reuse0B. It does not measure every payload type or observed-container acquisition and is not a whole-application benchmark.

The sample's optional `DUXEL_SAMPLE_EXERCISE=1` driver performs Width/Height and Top/End actions midway through the tree's render, reproducing the real callback ordering. Final warm and disk-font-cache-disabled runs exercise these transitions; they do not substitute for manual OS input. Original failing native-window logs and the CPU pre-fix result remain in the evidence directory. The application process is managed .NET11; NativeAOT is still unverified.
The new sample requires current local sources; a previously published package does not establish support for this factory.
