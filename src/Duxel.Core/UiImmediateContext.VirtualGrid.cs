namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal UiVirtualGridOffset RenderVirtualGrid<TSource>(UiVector2 size, double contentWidth, double contentHeight,
        UiVirtualGridOffset requested, bool changed, bool rightToLeft, UiScrollOwner owner,
        UiVirtualGrid<TSource> grid) where TSource : class, IUiVirtualGridSource
    {
        EnsureNotMeasuring();
        var origin = AdvanceCursor(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var horizontalBar = contentWidth > size.X;
        var verticalBar = contentHeight > size.Y;
        horizontalBar |= verticalBar && contentWidth > MathF.Max(0, size.X - ScrollbarSize);
        verticalBar |= horizontalBar && contentHeight > MathF.Max(0, size.Y - ScrollbarSize);
        var width = MathF.Max(0, size.X - (verticalBar ? ScrollbarSize : 0));
        var height = MathF.Max(0, size.Y - (horizontalBar ? ScrollbarSize : 0));
        var gutter = size.X - width;
        var viewport = new UiRect(origin.X + (rightToLeft ? gutter : 0), origin.Y, width, height);
        var maximumX = Math.Max(0, contentWidth - width);
        var maximumY = Math.Max(0, contentHeight - height);
        if (width > 0 && contentWidth > width && maximumX >= contentWidth
            || height > 0 && contentHeight > height && maximumY >= contentHeight)
            throw new NotSupportedException("The virtual extent cannot represent this grid viewport in double precision.");

        var x = Math.Clamp(requested.X, 0, maximumX);
        var y = Math.Clamp(requested.Y, 0, maximumY);
        var enclosingClip = CurrentClipRect;
        using var clip = BeginControlClip(root);
        var id = ResolveId("VirtualGrid");
        owner.Bind(this, id);
        var visible = IntersectRect(CurrentClipRect, viewport);
        var parent = _accessibilityParent;
        var horizontal = new UiVirtualViewport(viewport, visible, x, Horizontal : true, RightToLeft : rightToLeft);
        var vertical = new UiVirtualViewport(viewport, visible, y, Horizontal : false, RightToLeft : false);
        var tree = _accessibility;
        var enabled = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier;
        UiAccessibilityTree.Request? virtualRequest = null;
        if (tree is not null)
        {
            if (enabled)
            {
                x = tree.ApplyVirtualScrollIntoView(id, x, maximumX, horizontal : true);
                y = tree.ApplyVirtualScrollIntoView(id, y, maximumY, horizontal : false);
            }
            var viewportSize = new UiVector2(width, height);
            var range = UiAccessibilityScrollInfo.VirtualGrid(viewportSize, contentWidth, contentHeight, x, y, rightToLeft);
            if (TakeAccessibilityScrollRequest(tree, id, visible, range, enabled) is { Scroll : { } command } request)
            {
                var step = GetFrameHeight() * 3f;
                x = command.ApplyVirtualHorizontal(range, step);
                y = command.ApplyVertical(range, step);
                request.Result = UiAccessibilityResult.Success;
            }
            else
                virtualRequest = grid.PrepareAccessibility(tree, id, enabled, horizontal, vertical, maximumX, maximumY, ref x, ref y);
            range = UiAccessibilityScrollInfo.VirtualGrid(viewportSize, contentWidth, contentHeight, x, y, rightToLeft);
            ExposeAccessibility(id, parent, UiAccessibilityRole.Grid, "Virtual grid", root,
                focusable : false, valuePattern : false, scroll : CanPublishScroll(visible, range) ? range : null);
            tree.SetGrid(id, new(grid.RowCount, grid.ColumnCount) { IsVirtualized = true });
            tree.CaptureScrollViewport(id, viewport, enclosingClip, range);
            _accessibilityParent = id;
        }
        try
        {
            grid.RenderViewport(this, horizontal with { Offset = x }, vertical with { Offset = y });
            if (tree is not null)
                grid.CompleteAccessibility(tree, id, viewport, enabled, virtualRequest);
        }
        finally { _accessibilityParent = parent; }

        if (IsScrollHoverable(root, CurrentClipRect))
        {
            var step = GetFrameHeight() * 3f;
            x = ApplyScrollWheel(x, maximumX, rightToLeft ? -step : step, ref _mouseWheelHorizontal);
            y = ApplyScrollWheel(y, maximumY, step, ref _mouseWheel);
        }
        var horizontalTrack = new UiRect(viewport.X, origin.Y + height, width, size.Y - height);
        var verticalTrack = new UiRect(rightToLeft ? origin.X : origin.X + width, origin.Y, gutter, height);
        x = RenderVirtualScrollbar(owner, horizontalTrack, x, maximumX, contentWidth,
            horizontal : true, reverse : rightToLeft, reveal : changed || x != requested.X);
        y = RenderVirtualScrollbar(owner, verticalTrack, y, maximumY, contentHeight,
            horizontal : false, reverse : false, reveal : changed || y != requested.Y);
        FinishLayoutBox(origin, size);
        var result = new UiVirtualGridOffset(x, y);
        if (result != requested)
            _requestFrame?.Invoke();
        return result;
    }

    internal void RenderVirtualGridCell(UiWidget widget, int row, int column, string key, string name, UiRect bounds, UiRect viewport)
    {
        var parent = _accessibilityParent;
        var id = UiVirtualItemContainer.Id(parent, key);
        var visible = IntersectRect(IntersectRect(CurrentClipRect, viewport), bounds);
        _accessibility!.Add(new(id, parent, UiAccessibilityRole.GridCell, name, visible,
            IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, visible.Width <= 0 || visible.Height <= 0,
            Focused : false, Focusable : false, ValuePattern : false)
        {
            GridItem = new(parent, row, column),
            VirtualItem = new(row, key, IsPlaceholder : false)
        }, bounds);
        _accessibilityParent = id;
        try { RenderLayoutChild(widget, bounds, viewport); }
        finally { _accessibilityParent = parent; }
    }
}
