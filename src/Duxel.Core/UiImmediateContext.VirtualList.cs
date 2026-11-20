namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    internal UiVector2 MeasureVirtualViewport(UiVirtualListOptions options)
    {
        options.Validate();
        return MeasureVirtualViewport(options.Width, options.Height);
    }

    internal UiVector2 MeasureVirtualViewport(float? requestedWidth, float? requestedHeight)
    {
        var constraints = LayoutConstraints.Validate();
        var width = requestedWidth is { } w ? Math.Clamp(w, constraints.MinWidth, constraints.MaxWidth) : constraints.MaxWidth;
        var height = requestedHeight is { } h ? Math.Clamp(h, constraints.MinHeight, constraints.MaxHeight) : constraints.MaxHeight;
        if (!float.IsFinite(width) || !float.IsFinite(height))
            throw new InvalidOperationException("Virtualized content requires a finite viewport from its parent or Width/Height.");
        return constraints.Constrain(new(width, height));
    }
}

public sealed partial class UiImmediateContext
{
    internal double RenderVirtualViewport(string id, UiVector2 size, in UiVirtualListGeometry geometry, double requested,
        bool changed, bool horizontal, bool rightToLeft, UiScrollOwner owner, Action<UiImmediateContext, UiVirtualViewport> render,
        UiVirtualItemContainer? virtualItems, Func<double, double>? restoreAnchor, out bool anchorDeferred, bool ownsWheelInput = false)
    {
        EnsureNotMeasuring();
        var contentExtent = geometry.ContentExtent;
        var mainExtent = horizontal ? size.X : size.Y;
        var maximum = Math.Max(0, contentExtent - mainExtent);
        if (mainExtent > 0 && contentExtent > mainExtent && maximum >= contentExtent)
            throw new NotSupportedException("The virtual extent cannot represent this viewport in double precision.");
        var origin = AdvanceCursor(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var scrollbar = maximum > 0 ? ScrollbarSize : 0;
        var viewport = horizontal
            ? root with { Height = MathF.Max(0, size.Y - scrollbar) }
            : root with { Width = MathF.Max(0, size.X - scrollbar) };
        var enclosingClip = CurrentClipRect;
        using var clip = BeginControlClip(root);
        var resolvedId = ResolveId(id);
        owner.Bind(this, resolvedId);
        var clippedViewport = IntersectRect(CurrentClipRect, viewport);
        var visible = clippedViewport is { Width : > 0, Height : > 0 };
        var virtualViewport = new UiVirtualViewport(viewport, clippedViewport, requested, horizontal, rightToLeft);
        if (restoreAnchor is not null && visible)
            requested = restoreAnchor(virtualViewport.LeadingInset);
        var initialOffset = Math.Clamp(requested, 0, maximum);
        var track = horizontal
            ? new UiRect(origin.X, origin.Y + viewport.Height, size.X, size.Y - viewport.Height)
            : new UiRect(origin.X + viewport.Width, origin.Y, size.X - viewport.Width, size.Y);
        var offset = GetCapturedVirtualOffset(owner, track, initialOffset, maximum, contentExtent, horizontal, rightToLeft);
        if (ownsWheelInput && IsScrollHoverable(root, CurrentClipRect))
            offset = ApplyOwnedVirtualWheel(offset, maximum, GetFrameHeight() * (rightToLeft ? -3f : 3f), horizontal,
                horizontal ? _mouseWheel == 0 : _mouseWheelHorizontal == 0);
        var previousParent = _accessibilityParent;
        var tree = _accessibility;
        UiAccessibilityTree.Request? virtualRequest = null;
        if (tree is not null)
        {
            var enabled = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier;
            if (enabled)
                offset = tree.ApplyVirtualScrollIntoView(resolvedId, offset, maximum, horizontal);
            var viewportSize = new UiVector2(viewport.Width, viewport.Height);
            var range = UiAccessibilityScrollInfo.VirtualList(viewportSize, contentExtent, offset, horizontal, rightToLeft);
            var scroll = TakeAccessibilityScrollRequest(tree, resolvedId, clippedViewport, range, enabled);
            if (scroll is { Scroll : { } command })
            {
                var step = GetFrameHeight() * 3f;
                offset = horizontal ? command.ApplyVirtualHorizontal(range, step) : command.ApplyVertical(range, step);
                range = UiAccessibilityScrollInfo.VirtualList(viewportSize, contentExtent, offset, horizontal, rightToLeft);
                scroll.Result = UiAccessibilityResult.Success;
            }
            else if (virtualItems is not null)
            {
                virtualRequest = virtualItems.Prepare(tree, resolvedId, geometry, virtualViewport, maximum, enabled, ref offset);
                range = UiAccessibilityScrollInfo.VirtualList(viewportSize, contentExtent, offset, horizontal, rightToLeft);
            }
            ExposeAccessibility(resolvedId, previousParent, UiAccessibilityRole.Pane, "Virtual list", root,
                focusable : false, valuePattern : false, scroll : CanPublishScroll(clippedViewport, range) ? range : null,
                virtualItemContainer : virtualItems is not null);
            tree.CaptureScrollViewport(resolvedId, viewport, enclosingClip, range);
            _accessibilityParent = resolvedId;
        }
        try
        {
            render(this, virtualViewport with { Offset = offset });
            if (tree is not null)
                virtualItems?.Complete(tree, resolvedId, viewport, virtualRequest);
        }
        finally
        {
            _accessibilityParent = previousParent;
        }
        var beforeWheel = offset;
        if (!ownsWheelInput && IsScrollHoverable(root, CurrentClipRect))
            offset = ApplyScrollWheel(offset, maximum, GetFrameHeight() * (rightToLeft ? -3f : 3f),
                ref (horizontal ? ref _mouseWheelHorizontal : ref _mouseWheel));
        offset = RenderVirtualScrollbar(owner, track, offset, maximum, contentExtent, horizontal, rightToLeft,
            changed || offset != beforeWheel || offset != requested);
        FinishLayoutBox(origin, size);
        if (offset != requested)
            _requestFrame?.Invoke();
        anchorDeferred = restoreAnchor is not null && !visible && offset == initialOffset;
        return offset;
    }

    private double RenderVirtualScrollbar(UiScrollOwner owner, UiRect track, double offset, double maximum, double contentExtent,
        bool horizontal, bool reverse, bool reveal)
    {
        var (length, thickness) = horizontal ? (track.Width, track.Height) : (track.Height, track.Width);
        if (maximum <= 0 || thickness <= 4 || length <= 0)
        {
            owner.ReleaseAxis(horizontal);
            return offset;
        }
        owner.UseAxis(horizontal);
        var trackId = horizontal ? owner.HorizontalId : owner.VerticalId;
        if (reveal)
            MarkScrollbarInteraction(trackId);
        return RenderPreciseScrollbar(trackId, track, offset, maximum, contentExtent, CurrentClipRect, horizontal,
            reverse : reverse, coalesceMoves : true);
    }

    private double GetCapturedVirtualOffset(UiScrollOwner owner, UiRect track, double offset, double maximum,
        double contentExtent, bool horizontal, bool reverse)
    {
        var length = horizontal ? track.Width : track.Height;
        var thickness = horizontal ? track.Height : track.Width;
        if (!_leftMouseDown || maximum <= 0 || length <= 0 || thickness <= 4
            || _state.PreciseScrollbarDrag is not { } drag || _state.ActiveId != drag.Id
            || drag.Id != (horizontal ? owner.HorizontalId : owner.VerticalId))
            return offset;
        var travel = (double)length - PreciseThumbExtent(length, contentExtent, horizontal);
        return travel <= 0 ? offset : GetPreciseThumbOffset(drag, horizontal ? _mousePosition.X : _mousePosition.Y,
            horizontal ? track.X : track.Y, travel, maximum, reverse ? -1 : 1);
    }

    private double ApplyOwnedVirtualWheel(double offset, double maximum, float step, bool horizontal, bool singleAxis)
    {
        ref var axis = ref (horizontal ? ref _mouseWheelHorizontal : ref _mouseWheel);
        var wheel = axis;
        var next = ApplyScrollWheel(offset, maximum, step, ref axis);
        if (next != offset && singleAxis && !_leftMouseDown && _state.ActiveId is null
            && _state.PreviousPopupBlockingRects.Count == 0 && float.IsFinite(wheel))
        {
            var room = wheel * step < 0 ? maximum - next : next;
            if (room > 0)
                _state.WheelCoalescing = (wheel, room / Math.Abs(step), horizontal);
        }
        return next;
    }

    internal void RenderVirtualItem(UiWidget widget, int index, string key, string name, UiRect bounds, UiRect viewport)
    {
        var parent = _accessibilityParent;
        if (_accessibility is { } tree)
        {
            var id = UiVirtualItemContainer.Id(parent, key);
            var clip = IntersectRect(IntersectRect(CurrentClipRect, viewport), bounds);
            tree.Add(new(id, parent, UiAccessibilityRole.ListItem, name, clip,
                IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, clip.Width <= 0 || clip.Height <= 0,
                Focused : false, Focusable : false, ValuePattern : false)
            {
                VirtualItem = new(index, key, IsPlaceholder : false)
            }, bounds);
            _accessibilityParent = id;
        }
        try
        {
            RenderLayoutChild(widget, bounds, viewport);
        }
        finally
        {
            _accessibilityParent = parent;
        }
    }

    private double ApplyScrollWheel(double offset, double maximum, float step, ref float wheel)
    {
        if (maximum <= 0 || MathF.Abs(wheel) <= 0.001f)
            return offset;
        var next = Math.Clamp(offset - (double)wheel * step, 0, maximum);
        if (next != offset)
            wheel = 0;
        return next;
    }
}
