# Keyed virtual list

`Dux.VirtualList(source, itemCount, options)` creates a `UiVirtualList<TSource>` with one scrolling axis. The default is vertical. Set `Axis = UiScrollAxis.Horizontal` to place items with a scrollbar below them. `FlowDirection` selects horizontal order and defaults to the enclosing Fluent provider's direction. The list stores extent and offset in `double` and places only the visible and overscan items near the viewport in ordinary float coordinates.

```csharp
using var list = Dux.VirtualList(
    source, 1_000_000_000,
    new(ItemExtent : 32, Width : 300, Height : 280, Overscan : 1));
list.Offset = 32_000_000_000d - 280.25;
```

`source` is a class implementing `IUiVirtualListSource`. `GetKey(index)` supplies a stable identity, `Create(index)` transfers one new widget to normal keyed ownership, and `Bind(widget, index)` updates an existing compatible widget. Refresh and reordering reuse overlapping keys. Keys must be unique for logical items; the bounded implementation detects duplicates in the realized range rather than scanning the entire dataset. Template changes need compatible binding or different keys. Use stable model references in bindings rather than an index lookup that can become invalid during a model mutation.

The source and its data remain caller-owned. The list owns the created widgets. `Refresh()` rebinds the current range after model or key changes, and `ItemCount` supplies explicit geometry metadata. Pure measurement skips source and extent index calls and creates no controls. Rows render within their assigned slots and can contain the same Fluent controls and layout containers as other widget trees.

## Item extent

`ItemExtent` sets the fixed height or width along the selected axis. For variable sizes, set `UiVirtualListOptions.ExtentIndex` to an `IUiVirtualListExtentIndex` backed by the model. `GetBoundary(index)` returns the cumulative extent before the item, with zero at index `0` and the total extent at `ItemCount`. Use finite, strictly increasing boundaries and positive item sizes representable as `float`. Keep the boundaries and keys stable during a frame. The list uses binary search over the boundaries and reads only the slots it needs.

Call `Refresh()` after changing heights or order in place. The list uses `FindIndexByKey(key, itemCount)` to recover a moved visible anchor. Return `-1` if the key is absent and use a bounded model lookup. When possible, the list preserves the first visible key and its offset within the row, clamped to the new height. It uses the prior absolute offset when the key disappears. An explicit scroll offset takes precedence.

If the viewport is fully clipped during a height change, the list defers anchoring until it can use the current clip. The restored row keeps its position relative to the visible edge, including when the enclosing clip has moved. Row widgets that remain realized keep their keyed identity and editor state.

## Lifetime and input

Unchanged ranges render existing rows without new key enumeration, binding or creation. Overlapping keys preserve widget identity, editor focus and undo. Leaving the overscan range disposes the row through `UiBuildContext`; focused editor ownership, edit buffers and undo are released. Returning to an evicted key creates a new widget, so persistent text/selection/business state belongs in the model. There is no automatic pooling or reparenting across different keys.

New rows are adopted before old unused rows are released, so a range transition can temporarily own the old and new windows. Backing collections retain a bounded high-water capacity determined by the configured window/budget, not the dataset count. Creation/binding failures use existing Build abort cleanup. An explicit `MaxRealizedItems` budget (default 4096) is checked before keys or widgets are requested. Overscan defaults to one row. Exceeding the budget fails; it does not silently omit rows.

The list uses wheel input on its active axis and passes unused events outward. The shared double-precision scrollbar supports thumb dragging and viewport-sized paging on both axes. Changing the axis, transferring contexts, or disposing the list releases its scrollbar ownership. Explicit offset changes from item callbacks survive the viewport return, and item-extent changes take effect on the following prepared frame. A fully clipped or zero-size viewport realizes no items.

Thumb dragging starts from the exact content offset. Pressing or holding the thumb, or returning it to the press position, preserves fractional offsets even near the end of one billion rows.

## Precision and scope

Both viewport axes must be finite through the parent or `Width`/`Height`. `Offset` is finite and nonnegative; rendering clamps it to the current range. Range arithmetic is clamped before integer conversion. The implementation rejects extents that cannot represent the viewport even in double precision. Each row still uses the ordinary float geometry of its own controls; a huge individual row is not itself virtualized.

Automatic item measurement and cross-key pooling remain open. For two-axis virtualization, use the grid below. The ordinary full-content `UiScrollView` still rejects unrepresentable giant world-coordinate children. Container progress remains 3/6.

## Two-axis grid

```csharp
using var grid = Dux.VirtualGrid(source, 1_000_000_000, 1_000_000_000,
    new(RowHeight : 44, ColumnWidth : 200, Width : 960, Height : 320));
grid.Offset = new(double.MaxValue, double.MaxValue);
```

Implement `IUiVirtualGridSource` with a stable key for each cell and a bounded cell factory. The grid realizes the product of the visible row and column ranges plus overscan. It checks `MaxRealizedCells` before calling the source. `RowExtents` and `ColumnExtents` can supply indexed boundaries. `SetDimensions` changes the counts, and `Refresh` rebinds the realized cells after a model edit.

The native grid publishes the full logical dimensions alongside both scroll axes. UIA `Grid.GetItem` and AT-SPI `Table.GetAccessibleAt` accept zero-based row and column coordinates. Looking up a distant cell publishes one offscreen provider without moving the viewport or creating its widget. UIA `VirtualizedItem.Realize` or AT-SPI `Component.ScrollTo(Nearest)` brings it into view and attaches its editor or other content to that same provider. The next distant lookup can replace the placeholder. Call `Refresh` after changing keys or names. A placeholder whose key no longer matches its coordinate is retired. Use row and column coordinates for large grids. AT-SPI's flat index uses a 32-bit integer.

In the Gallery, open **가상 그리드**, edit the first cell, choose **마지막 행·열**, and return with **첫 행·첫 열**. The code sits directly below the example.

Run `virtual-grid-pixels` in the Windows or Linux Vulkan test runner to compare independently drawn cells and scrollbar tracks. It covers both axes at fractional offsets, fixed and indexed sizes, right-to-left order, inherited clipping, and cases where one scrollbar makes the other necessary.

Use `virtual-grid-host-pixels` with three frame slots to retain raw RGBA for the clear control and selected fit, fixed-tail, and clipped indexed-tail scenes. It also isolates the two vector tails' cell frames, circles, and scrollbars and runs the full reference suite on the same host.

## Windows accessibility

Set `AccessibleName` to name the virtual list's Pane. Its Scroll pattern uses the full extent of the active axis and keeps double precision for percentages and steps. Read `HorizontalOffset` and `HorizontalContentWidth` or `VerticalOffset` and `VerticalContentHeight` on `UiAccessibilityScrollInfo` for the exact values. The frame applies a Scroll request before realizing and publishing its new range.

In right-to-left flow, `UiVirtualList.Offset` and accessibility scroll percentages count from the right. `UiAccessibilityScrollInfo.HorizontalOffset` describes the physical position from the left. ScrollItem and point-alignment requests use the same client coordinates as left-to-right layouts.

Controls in the realized range, including overscan, appear under the Pane. A clipped overscan control can use `ScrollIntoView` while its provider remains live. When a row leaves the realized range, its provider becomes unavailable and the Pane invalidates its child list after publishing the new range. Returning to an evicted key creates a new provider. Store persistent edits in the model.

Implement `IUiVirtualListAutomationSource` to expose `ItemContainer` and a `ListItem` for each row. `GetAccessibleItemName` supplies the model name and `GetKey` supplies the item's `AutomationId`. `FindAccessibleItem` receives a start index and item count. Use a model index or map for bounded lookup. The provider supports Name, AutomationId and unfiltered enumeration starting after a supplied item. Search callbacks run on the render writer and should return promptly. Call `Refresh()` after changing keys or accessible names.

A match outside the realized range publishes one peer placeholder without moving the viewport or creating a widget. A later search can replace that placeholder. If a held key moves during `Refresh()`, the Pane locates its new index by AutomationId and preserves the provider. A removed key retires the provider. `VirtualizedItem.Realize` rechecks the key and reveals the target through owned scrolling ancestors when they clip its row. It attaches the row's controls to the same provider before returning. A fixed clip that prevents the row from being shown can fail the request. A query matching an already realized row returns its live provider. [Microsoft virtualized items](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-workingwithvirtualizeditems)

## Verification and sample

On Linux, run `dotnet Duxel.Linux.App.Tests.dll atspi-gallery-virtual-list <gallery-pid> <fresh-result.json>` in the Gallery's D-Bus user session. The separate AT-SPI client edits key 0, goes to the billionth row, and swaps the keys of the first and last rows. It checks that the exposed subtree stays bounded and the text follows key 0 into the recreated editor.

Use `atspi-gallery-virtual-horizontal` with the same arguments to verify the horizontal Gallery example. An external AT-SPI client switches the direction and uses `Component.ScrollTo` to bring an offscreen overscan editor into view while retaining the same accessible object. It also checks bounded realization and keyed edits at the billionth item.

Use `atspi-gallery-virtual-rtl` to check the same interactions with right-to-left order, including the editors' actual X positions. In the Gallery, click **가로 순서 : 왼쪽부터** in horizontal mode to switch to right-to-left. The `virtual-list-rtl-pixels` mode compares right-to-left placement with independent reference draws.

Run `./run-gallery.ps1` on Windows or `./run-gallery-linux.ps1` for WSL. Open **가상 목록** and click **방향 : 세로** to switch to the horizontal example. Try its bottom scrollbar, horizontal wheel, and **끝** button. The source below the example updates to show the active axis and extent configuration.

Run `virtual-list-pixels` in the Windows or Linux Vulkan test runner to compare fixed and variable sizes on both axes with reference draws. The cases include fractional offsets near the end of one billion items, a narrow inherited clip, legacy columns, and raster and vector layers.

Use `virtual-list-host-pixels` on both hosts to compare raw RGBA captures at the fractional tail. It records clear, geometry-only and complete frames for each axis, theme, MSAA setting and raster/vector layer. In the Windows managed and WSLg NativeAOT comparison, all 16 clear pairs matched. Four MSAA 4× raster cases showed a one-pixel fringe along the leading clip edge on WSLg. The other 12 geometry pairs differed by at most one RGB code value per channel. The native labels also differed. The comparison does not identify the renderer stage that produced the fringe.

The mode also writes one `.scissor.tsv` per frame. It records each draw's logical clip, effective Vulkan scissor and render area. In the four fringe cases the scissor records are identical across hosts and explicitly exclude the fringe. Disabling damage tracking on WSLg reproduced all 48 captured frames, including the fringe.

The `clip-host-pixels` mode reproduces the fringe with a single bordered rectangle or two triangles, without a list or text. On Mesa lavapipe 25.2.8, it appeared in all 32 MSAA 4× cases. Windows and Mesa 26.0.8 kept all 64 minimal cases and the tested virtual-list scenes within the leading clip edge. The newer Mesa test used the same Linux NativeAOT binary in an isolated container. The observed behavior matches Mesa's [scissor plane fix](https://docs.mesa3d.org/relnotes/25.3.0.html).

The `Variable heights` control in `layout_virtual_list_fba.cs` alternates 36- and 64-unit rows. An external Windows UI Automation client verified the visible key and fractional position after switching heights, including when the parent fully hides the list and reveals it at a new position. It also verified that `VirtualizedItem.Realize` reveals a relocated last row at the exact scroll limit.

`artifacts/virtual-list-uia-2026-11-11/verification.json` records a separate Windows UIA client using the running sample. It reveals an overscan editor, scrolls to the last of one billion rows, verifies eviction of the old provider, and checks that the restored model value survives under a fresh provider on return.

The sample's outer pane can scroll the list out of view. `Reorder keys` swaps two distant keys. The separate UIA client holds a placeholder across that swap and verifies that `Realize` scrolls both panes to the moved row at the end. See `artifacts/nested-virtual-realize-2026-11-11/verification.json`.

Evidence: `artifacts/virtual-list-2026-11-10/verification.json`. Fixed .NET11 RC3 build has zero warnings/errors. CPU43 checks cover pure measurement/parent discovery, billion-row tail and fractional precision, bounded ownership, refresh/reorder, duplicate/factory errors, budgets/subnormals, nested wheel input, thumb/page input, context transfer and actual editor input/undo/eviction. Separate regressions pass ScrollView47, routing2317, containers229 and lifetime101; these overlapping scopes are not unique requirement totals.

The routing run includes 192 new exact acquired-image comparisons against explicitly rebased row coordinates and the common scrollbar, plus 192 bounded-realization/offset assertions. Raster/vector, MSAA1/4, Light/Dark, ordinary/legacy-column routing and narrow clips use three frame slots; routing readbacks total1697. No speed, whole-model memory or whole-application allocation improvement is inferred from these tests.

`samples/fba/layout_virtual_list_fba.cs` demonstrates a billion-item logical source, editable visible rows, sparse model text/marks and Top/Middle/End/count controls. `DUXEL_SAMPLE_EXERCISE=1` schedules automatic transitions for startup verification. The saved warm/cold runs use a same-body local-project copy because of the existing PS5 helper limitation. Prefer `./run-fba.ps1 samples/fba/layout_virtual_list_fba.cs -NoCache` with the supported shell. Current local sources are required; a previously published package is not proof of support. These managed Windows runs do not establish NativeAOT, manual OS input or official visual approval.
