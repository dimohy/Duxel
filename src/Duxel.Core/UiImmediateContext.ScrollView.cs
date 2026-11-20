namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    internal float ScrollViewScrollbarThickness => _ui.ScrollViewScrollbarThickness;
    internal UiScrollLayout MeasureScrollView(UiWidget child, UiScrollOptions options) => _ui.MeasureScrollView(this, child, options);
}

public sealed partial class UiImmediateContext
{
    private UiVector2 _displayFramebufferScale = new(1, 1);
    internal void SetDisplayFramebufferScale(UiVector2 scale)
    {
        if (!float.IsFinite(scale.X) || !float.IsFinite(scale.Y) || scale.X <= 0 || scale.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));
        _displayFramebufferScale = scale;
    }
    internal float ScrollViewScrollbarThickness => ScrollbarSize;
    internal UiScrollLayout MeasureScrollView(UiControlContext context, UiWidget child, UiScrollOptions options)
    {
        options.Validate();
        var constraints = context.LayoutConstraints.Validate();
        var width = options.Width is { } w ? Math.Clamp(w, constraints.MinWidth, constraints.MaxWidth) : constraints.MaxWidth;
        var height = options.Height is { } h ? Math.Clamp(h, constraints.MinHeight, constraints.MaxHeight) : constraints.MaxHeight;
        if (!float.IsFinite(width) || !float.IsFinite(height))
            throw new InvalidOperationException("ScrollView needs a finite viewport on both axes from its parent or explicit Width/Height.");
        var size = constraints.Constrain(new(width, height));
        var vertical = false;
        var horizontal = false;
        for (var pass = 0; pass < 3; pass++)
        {
            var viewport = new UiVector2(
                MathF.Max(0, size.X - (vertical ? ScrollbarSize : 0)),
                MathF.Max(0, size.Y - (horizontal ? ScrollbarSize : 0)));
            var childConstraints = new UiBoxConstraints(
                options.Horizontal ? 0 : viewport.X, options.Horizontal ? float.PositiveInfinity : viewport.X,
                options.Vertical ? 0 : viewport.Y, options.Vertical ? float.PositiveInfinity : viewport.Y);
            var content = context.MeasureConstrained(child, childConstraints);
            var nextVertical = options.Vertical && content.Y > viewport.Y;
            var nextHorizontal = options.Horizontal && content.X > viewport.X;
            if (nextVertical == vertical && nextHorizontal == horizontal)
                return new(size, content, viewport, ScrollbarSize);
            vertical = nextVertical;
            horizontal = nextHorizontal;
        }
        throw new InvalidOperationException("ScrollView content did not settle after reserving both scrollbar axes.");
    }

    internal UiVector2 RenderScrollView(string id, UiWidget child, UiScrollLayout layout, UiVector2 requested, UiScrollOwner owner, bool rightToLeft)
    {
        EnsureNotMeasuring();
        var maximum = new UiVector2(MathF.Max(0, layout.Content.X - layout.Viewport.X), MathF.Max(0, layout.Content.Y - layout.Viewport.Y));
        var offset = new UiVector2(Math.Clamp(requested.X, 0, maximum.X), Math.Clamp(requested.Y, 0, maximum.Y));
        offset = offset with
        {
            X = ReverseHorizontalScroll(offset.X, maximum.X, rightToLeft)
        };
        var origin = AdvanceCursor(layout.Size);
        var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var gutter = layout.Size.X - layout.Viewport.X;
        var viewport = new UiRect(origin.X + (rightToLeft ? gutter : 0), origin.Y, layout.Viewport.X, layout.Viewport.Y);
        var enclosingClip = CurrentClipRect;
        using var clip = BeginControlClip(root);
        var resolvedId = ResolveId(id);
        var previousParent = _accessibilityParent;
        if (_accessibility is { } tree)
        {
            var enabled = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier;
            if (enabled)
                offset = tree.ApplyScrollIntoView(resolvedId, offset, maximum);
            var scroll = ApplyAccessibilityScrollRequest(tree, resolvedId, layout.Viewport,
                IntersectRect(CurrentClipRect, viewport), layout.Content, GetFrameHeight() * 3f,
                enabled, rightToLeft, ref offset);
            ExposeAccessibility(resolvedId, previousParent, UiAccessibilityRole.Pane, "Scroll view", root,
                focusable: false, valuePattern: false, scroll: scroll);
            tree.CaptureScrollViewport(resolvedId, viewport, enclosingClip, scroll ?? new(layout.Viewport, layout.Content, offset));
        }
        var bounds = new UiRect(viewport.X - offset.X, origin.Y - offset.Y, layout.Content.X, layout.Content.Y);
        if (!ScrollCoordinatesRepresentable(viewport, bounds))
        {
            owner.Release();
            throw new NotSupportedException($"Scroll content exceeds physical-pixel coordinate precision: viewport = {viewport}, content = {bounds}, scale = {_displayFramebufferScale}.");
        }
        owner.Bind(this, resolvedId);
        if (_accessibility is not null)
        {
            _accessibilityParent = resolvedId;
        }
        var revealVersion = _scrollRevealVersion;
        _scrollRevealDepth++;
        try
        {
            // The viewport owns clipping. The translated full-content box is only a
            // layout allocation; intersecting it again loses its near edge at large offsets.
            RenderAllocatedChild(_builder, child, bounds, viewport, constrainLayout: true, clipToAllocation: false);
        }
        finally
        {
            _scrollRevealDepth--;
            _accessibilityParent = previousParent;
        }
        offset = ApplyControlScrollReveal(revealVersion, viewport, enclosingClip, offset, maximum);
        if (IsScrollHoverable(root, CurrentClipRect))
        {
            var step = GetFrameHeight() * 3f;
            offset = new(
                ApplyScrollWheel(offset.X, maximum.X, step, ref _mouseWheelHorizontal),
                ApplyScrollWheel(offset.Y, maximum.Y, step, ref _mouseWheel));
        }
        var verticalTrack = new UiRect(rightToLeft ? origin.X : origin.X + layout.Viewport.X, origin.Y, gutter, layout.Viewport.Y);
        if (maximum.Y > 0 && verticalTrack.Width > 4 && verticalTrack.Height > 0)
        {
            owner.UseAxis(horizontal: false);
            offset = offset with
            {
                Y = RenderScrollbarV(owner.VerticalId, verticalTrack, offset.Y, maximum.Y, layout.Content.Y, CurrentClipRect)
            };
        }
        else
            owner.ReleaseAxis(horizontal: false);
        var horizontalTrack = new UiRect(viewport.X, origin.Y + layout.Viewport.Y, layout.Viewport.X, layout.Size.Y - layout.Viewport.Y);
        if (maximum.X > 0 && horizontalTrack.Height > 4 && horizontalTrack.Width > 0)
        {
            owner.UseAxis(horizontal: true);
            offset = offset with
            {
                X = RenderScrollbarH(owner.HorizontalId, horizontalTrack, offset.X, maximum.X, layout.Content.X, CurrentClipRect)
            };
        }
        else
            owner.ReleaseAxis(horizontal: true);
        FinishLayoutBox(origin, layout.Size);
        offset = offset with
        {
            X = ReverseHorizontalScroll(offset.X, maximum.X, rightToLeft)
        };
        if (offset != requested)
            _requestFrame?.Invoke();
        return offset;
    }

    internal void ReleaseOwnedScrollbar(string id) => _state.ReleaseScrollbar(id);

    private bool ScrollCoordinatesRepresentable(UiRect viewport, UiRect bounds)
    {
        static bool Axis(float start, float extent, float viewportExtent, float pixelScale)
        {
            var end = start + extent;
            if (!float.IsFinite(start) || !float.IsFinite(end)) return false;
            if (viewportExtent == 0) return true;
            var magnitude = MathF.Max(MathF.Abs(start), MathF.Abs(start + viewportExtent));
            var step = MathF.BitIncrement(magnitude) - magnitude;
            // A full-content layout must still distinguish individual device pixels.
            // Larger documents need virtualization instead of a rounded viewport clip.
            return (double)step * pixelScale < 1;
        }
        return Axis(bounds.X, bounds.Width, viewport.Width, _displayFramebufferScale.X * WidgetRasterScale)
            && Axis(bounds.Y, bounds.Height, viewport.Height, _displayFramebufferScale.Y * WidgetRasterScale);
    }

}

public sealed partial class UiState
{
    internal UiPreciseScrollbarDrag? PreciseScrollbarDrag;

    internal void ReleasePreciseScrollbarDrag(string id)
    {
        if (PreciseScrollbarDrag is { } drag && drag.Id == id)
            PreciseScrollbarDrag = null;
    }

    internal (int X, int Y) ScrollStorage => (_scrollX.Count, _scrollY.Count);

    internal void ReleaseScrollbar(string id)
    {
        ReleasePreciseScrollbarDrag(id);
        _scrollbarMotions?.Remove(id);
        void Remove(string key)
        {
            _scrollX.Remove(key);
            _scrollY.Remove(key);
        }
        Remove(id);
        Remove(id + "##prev");
        Remove(id + "##flash");
        Remove(id + "##ofs");
        if (_activeId == id)
            _activeId = null;
    }
}

internal readonly record struct UiPreciseScrollbarDrag(string Id, float Pointer, float GrabOffset, double Scroll)
{
    internal UiDocumentThumbTrack? DocumentTrack { get; init; }
    internal bool CoalesceMoves { get; init; }
}

internal readonly record struct UiDocumentThumbTrack(UiRect Viewport, float Start, double Travel, double Maximum);
