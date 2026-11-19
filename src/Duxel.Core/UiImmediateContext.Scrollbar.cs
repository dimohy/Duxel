namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private static float ReverseHorizontalScroll(float offset, float maximum, bool rightToLeft) =>
        rightToLeft ? maximum - offset : offset;

    private bool IsScrollHoverable(UiRect rect, UiRect clip)
    {
        var visible = IntersectRect(rect, clip);
        return visible.Width > 0f && visible.Height > 0f && IsHovering(visible)
            && !IsMouseOverHigherTierPopup(_popupTierDepth)
            && (_popupTierDepth > 0 || _currentWindowId is null || IsWindowTopmostAtMouse(_currentWindowId, _windowRect));
    }

    private float ApplyScrollWheel(float offset, float maximum, float step, ref float wheel)
    {
        if (maximum <= 0f || MathF.Abs(wheel) <= 0.001f)
            return offset;

        var next = Math.Clamp(offset - wheel * step, 0f, maximum);
        // An immovable inner viewport leaves the event for its enclosing viewport.
        if (next != offset)
            wheel = 0f;
        return next;
    }

    /// <summary>
    /// Applies an alpha multiplier to a color's alpha channel.
    /// </summary>
    private static uint ApplyAlpha(uint color, float alphaFactor)
    {
        var a = (byte)((color >> 24) * alphaFactor);
        return (color & 0x00FFFFFF) | ((uint)a << 24);
    }

    /// <summary>
    /// Renders a vertical scrollbar and handles drag/track-click interactions.
    /// Visibility is sampled by the common finite scrollbar motion policy.
    /// Returns the updated scrollY value.
    /// </summary>
    internal float RenderScrollbarV(string scrollId, UiRect trackRect, float scrollY, float maxScroll, float contentHeight, UiRect clipRect,
        bool interactive = true, UiRect? documentViewport = null)
        => (float)RenderScrollbarVDouble(scrollId, trackRect, scrollY, maxScroll, contentHeight, clipRect,
            interactive: interactive, documentViewport: documentViewport);

    internal double RenderScrollbarVDouble(string scrollId, UiRect trackRect, double scrollY, double maxScroll, double contentHeight, UiRect clipRect,
        bool interactive = true, UiRect? documentViewport = null)
        => RenderPreciseScrollbar(scrollId, trackRect, scrollY, maxScroll, contentHeight, clipRect, horizontal: false,
            interactive, documentViewport: documentViewport);

    internal double RenderPreciseScrollbar(string scrollId, UiRect trackRect, double offset, double maxScroll,
        double contentExtent, UiRect clipRect, bool horizontal, bool interactive = true, bool reverse = false,
        UiRect? documentViewport = null)
    {
        var visible = IntersectRect(trackRect, clipRect);
        if (maxScroll <= 0)
        {
            _state.ReleasePreciseScrollbarDrag(scrollId);
            _state.ReleaseScrollbarMotion(scrollId);
            if (_state.ActiveId == scrollId)
                _state.ActiveId = null;
            return offset;
        }

        // Determine visibility state
        if (!interactive && _state.ActiveId == scrollId)
            _state.ActiveId = null;
        var scrollActive = interactive && _state.ActiveId == scrollId;
        var trackHovered = interactive && IsScrollHoverable(trackRect, clipRect);

        // Handle size: proportional to visible/content ratio, with minimum
        var trackExtent = horizontal ? trackRect.Width : trackRect.Height;
        var trackStart = horizontal ? trackRect.X : trackRect.Y;
        var pointerPosition = horizontal ? _mousePosition.X : _mousePosition.Y;
        var handleExtent = MathF.Min(trackExtent, MathF.Max(horizontal ? 24f : 16f,
            trackExtent * (float)(trackExtent / contentExtent)));
        var progress = (float)(offset / maxScroll);
        var direction = reverse ? -1 : 1;
        var handleStart = trackStart + (reverse ? 1 - progress : progress) * (trackExtent - handleExtent);
        var handleRect = horizontal
            ? new UiRect(handleStart, trackRect.Y + 2f, handleExtent, trackRect.Height - 4f)
            : new UiRect(trackRect.X + 2f, handleStart, trackRect.Width - 4f, handleExtent);

        var handleHover = trackHovered && IsHovering(handleRect);
        var trackClickArea = trackHovered && !handleHover;

        // Handle drag (only capture if no other interaction owns ActiveId)
        if (_leftMousePressed && handleHover && (_state.ActiveId is null || _state.ActiveId == scrollId))
        {
            _state.ActiveId = scrollId;
            scrollActive = true;
            _state.PreciseScrollbarDrag = new(scrollId, pointerPosition, pointerPosition - handleStart, offset);
        }

        if (!_leftMouseDown && scrollActive)
        {
            _state.ActiveId = null;
            scrollActive = false;
        }

        if (!scrollActive)
            _state.ReleasePreciseScrollbarDrag(scrollId);
        else if (_leftMouseDown && trackExtent > handleExtent
            && _state.PreciseScrollbarDrag is { } drag && drag.Id == scrollId)
        {
            var travel = (double)trackExtent - handleExtent;
            if (documentViewport is { } document)
            {
                _state.PreciseScrollbarDrag = drag with { DocumentTrack = new(document, trackStart, travel, maxScroll) };
                if (_state.PreviousPopupBlockingRects.Count == 0)
                    _state.PointerMoveCoalescing = (scrollId, null);
            }
            offset = GetPreciseThumbOffset(drag, pointerPosition, trackStart, travel, maxScroll, direction);
        }

        // Track click → page scroll
        if (_leftMousePressed && trackClickArea && _state.ActiveId is null)
        {
            if (_controlSkins.Scrollbar.TrackClick == UiScrollbarTrackClick.Jump && trackExtent > handleExtent)
            {
                var clicked = Math.Clamp((pointerPosition - trackStart - handleExtent / 2) / (trackExtent - handleExtent), 0, 1);
                offset = (reverse ? 1 - clicked : clicked) * maxScroll;
            }
            else
            {
                var page = pointerPosition < handleStart ? -trackExtent : trackExtent;
                offset = Math.Clamp(offset + direction * page, 0, maxScroll);
            }
        }

        if (visible.Width > 0 && visible.Height > 0)
        {
            var opacity = SampleScrollbarMotion(scrollId, offset, scrollActive, trackHovered);
            PaintScrollbarVisual(trackRect, handleRect, clipRect, opacity, scrollActive, handleHover);
        }
        else
            _state.ReleaseScrollbarMotion(scrollId);

        return offset;
    }

    private static double GetPreciseThumbOffset(UiPreciseScrollbarDrag drag, double pointer, float start,
        double travel, double maximum, int direction)
    {
        // A rounded thumb coordinate cannot recover an exact large content offset.
        if (pointer == drag.Pointer)
            return Math.Clamp(drag.Scroll, 0, maximum);
        var thumbPosition = pointer - drag.GrabOffset - start;
        if (thumbPosition <= 0)
            return direction < 0 ? maximum : 0;
        if (thumbPosition >= travel)
            return direction < 0 ? 0 : maximum;
        return Math.Clamp(drag.Scroll + direction * (pointer - drag.Pointer) / travel * maximum, 0, maximum);
    }

    private void MarkScrollbarInteraction(string scrollId)
        => _state.RequestScrollbarReveal(scrollId);

    /// <summary>
    /// Renders a horizontal scrollbar and handles drag/track-click interactions.
    /// Visibility is sampled by the common finite scrollbar motion policy.
    /// Returns the updated scrollX value.
    /// </summary>
    internal float RenderScrollbarH(string scrollId, UiRect trackRect, float scrollX, float maxScrollX, float contentWidth, UiRect clipRect,
        bool interactive = true)
    {
        var visible = IntersectRect(trackRect, clipRect);
        if (maxScrollX <= 0)
        {
            _state.ReleaseScrollbarMotion(scrollId);
            if (_state.ActiveId == scrollId)
                _state.ActiveId = null;
            return scrollX;
        }

        // Determine visibility state
        if (!interactive && _state.ActiveId == scrollId)
            _state.ActiveId = null;
        var scrollActive = interactive && _state.ActiveId == scrollId;
        var trackHovered = interactive && IsScrollHoverable(trackRect, clipRect);

        // Handle size: proportional to visible/content ratio, with minimum
        var handleWidth = MathF.Min(trackRect.Width, MathF.Max(24f, trackRect.Width * (trackRect.Width / contentWidth)));
        var handleX = trackRect.X + (scrollX / maxScrollX) * (trackRect.Width - handleWidth);
        var handleRect = new UiRect(handleX, trackRect.Y + 2f, handleWidth, trackRect.Height - 4f);

        var handleHover = trackHovered && IsHovering(handleRect);
        var trackClickArea = trackHovered && !handleHover;

        // Handle drag (only capture if no other interaction owns ActiveId)
        if (_leftMousePressed && handleHover && (_state.ActiveId is null || _state.ActiveId == scrollId))
        {
            _state.ActiveId = scrollId;
            scrollActive = true;
            _state.SetScrollX($"{scrollId}##ofs", _mousePosition.X - handleX);
        }

        if (!_leftMouseDown && scrollActive)
        {
            _state.ActiveId = null;
            scrollActive = false;
        }

        if (scrollActive && _leftMouseDown && trackRect.Width > handleWidth)
        {
            var offset = _state.GetScrollX($"{scrollId}##ofs");
            var t = Math.Clamp((_mousePosition.X - offset - trackRect.X) / (trackRect.Width - handleWidth), 0f, 1f);
            scrollX = t * maxScrollX;
        }

        // Track click → page scroll
        if (_leftMousePressed && trackClickArea && _state.ActiveId is null)
        {
            var visibleWidth = trackRect.Width;
            if (_controlSkins.Scrollbar.TrackClick == UiScrollbarTrackClick.Jump && trackRect.Width > handleWidth)
            {
                scrollX = Math.Clamp((_mousePosition.X - trackRect.X - handleWidth / 2) / (trackRect.Width - handleWidth), 0, 1) * maxScrollX;
            }
            else if (_mousePosition.X < handleX)
            {
                scrollX = MathF.Max(0f, scrollX - visibleWidth);
            }
            else
            {
                scrollX = MathF.Min(maxScrollX, scrollX + visibleWidth);
            }
        }

        if (visible.Width > 0 && visible.Height > 0)
        {
            var opacity = SampleScrollbarMotion(scrollId, scrollX, scrollActive, trackHovered);
            PaintScrollbarVisual(trackRect, handleRect, clipRect, opacity, scrollActive, handleHover);
        }
        else
            _state.ReleaseScrollbarMotion(scrollId);

        return scrollX;
    }
}
