namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
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
    /// Auto-hides when not hovered/active; 80% opacity on hover, 100% on active.
    /// Returns the updated scrollY value.
    /// </summary>
    private float RenderScrollbarV(string scrollId, UiRect trackRect, float scrollY, float maxScroll, float contentHeight, UiRect clipRect)
    {
        if (maxScroll <= 0f)
        {
            return scrollY;
        }

        // Determine visibility state
        var scrollActive = _state.ActiveId == scrollId;
        var trackHovered = IsHovering(trackRect);

        // Alpha: 100% when active, 80% when hovered, 20% when idle
        var alphaFactor = scrollActive ? 1.0f : trackHovered ? 0.8f : 0.2f;

        // Track background
        _builder.AddRectFilled(trackRect, ApplyAlpha(_theme.ScrollbarBg, alphaFactor), _whiteTexture, clipRect);

        // Handle size: proportional to visible/content ratio, with minimum
        var handleHeight = MathF.Max(16f, trackRect.Height * (trackRect.Height / contentHeight));
        var handleY = trackRect.Y + (scrollY / maxScroll) * (trackRect.Height - handleHeight);
        var handleRect = new UiRect(trackRect.X + 2f, handleY, trackRect.Width - 4f, handleHeight);

        var handleHover = IsHovering(handleRect);
        var trackClickArea = trackHovered && !handleHover;

        // Handle drag
        if (_leftMousePressed && handleHover)
        {
            _state.ActiveId = scrollId;
            scrollActive = true;
            // Store offset from handle top to mouse position for smooth drag
            _state.SetScrollY($"{scrollId}##ofs", _mousePosition.Y - handleY);
        }

        if (!_leftMouseDown && scrollActive)
        {
            _state.ActiveId = null;
            scrollActive = false;
        }

        if (scrollActive && _leftMouseDown)
        {
            var offset = _state.GetScrollY($"{scrollId}##ofs");
            var t = Math.Clamp((_mousePosition.Y - offset - trackRect.Y) / (trackRect.Height - handleHeight), 0f, 1f);
            scrollY = t * maxScroll;
        }

        // Track click → page scroll
        if (_leftMousePressed && trackClickArea)
        {
            var visibleHeight = trackRect.Height;
            if (_mousePosition.Y < handleY)
            {
                scrollY = MathF.Max(0f, scrollY - visibleHeight);
            }
            else
            {
                scrollY = MathF.Min(maxScroll, scrollY + visibleHeight);
            }
        }

        // Render handle
        var handleColor = scrollActive ? _theme.ScrollbarGrabActive : handleHover ? _theme.ScrollbarGrabHovered : _theme.ScrollbarGrab;
        _builder.AddRectFilled(handleRect, ApplyAlpha(handleColor, alphaFactor), _whiteTexture, clipRect);

        return scrollY;
    }

    /// <summary>
    /// Renders a horizontal scrollbar and handles drag/track-click interactions.
    /// Auto-hides when not hovered/active; 80% opacity on hover, 100% on active.
    /// Returns the updated scrollX value.
    /// </summary>
    private float RenderScrollbarH(string scrollId, UiRect trackRect, float scrollX, float maxScrollX, float contentWidth, UiRect clipRect)
    {
        if (maxScrollX <= 0f)
        {
            return scrollX;
        }

        // Determine visibility state
        var scrollActive = _state.ActiveId == scrollId;
        var trackHovered = IsHovering(trackRect);

        // Alpha: 100% when active, 80% when hovered, 20% when idle
        var alphaFactor = scrollActive ? 1.0f : trackHovered ? 0.8f : 0.2f;

        // Track background
        _builder.AddRectFilled(trackRect, ApplyAlpha(_theme.ScrollbarBg, alphaFactor), _whiteTexture, clipRect);

        // Handle size: proportional to visible/content ratio, with minimum
        var handleWidth = MathF.Max(24f, trackRect.Width * (trackRect.Width / contentWidth));
        var handleX = trackRect.X + (scrollX / maxScrollX) * (trackRect.Width - handleWidth);
        var handleRect = new UiRect(handleX, trackRect.Y + 2f, handleWidth, trackRect.Height - 4f);

        var handleHover = IsHovering(handleRect);
        var trackClickArea = trackHovered && !handleHover;

        // Handle drag
        if (_leftMousePressed && handleHover)
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

        if (scrollActive && _leftMouseDown)
        {
            var offset = _state.GetScrollX($"{scrollId}##ofs");
            var t = Math.Clamp((_mousePosition.X - offset - trackRect.X) / (trackRect.Width - handleWidth), 0f, 1f);
            scrollX = t * maxScrollX;
        }

        // Track click → page scroll
        if (_leftMousePressed && trackClickArea)
        {
            var visibleWidth = trackRect.Width;
            if (_mousePosition.X < handleX)
            {
                scrollX = MathF.Max(0f, scrollX - visibleWidth);
            }
            else
            {
                scrollX = MathF.Min(maxScrollX, scrollX + visibleWidth);
            }
        }

        // Render handle
        var handleColor = scrollActive ? _theme.ScrollbarGrabActive : handleHover ? _theme.ScrollbarGrabHovered : _theme.ScrollbarGrab;
        _builder.AddRectFilled(handleRect, ApplyAlpha(handleColor, alphaFactor), _whiteTexture, clipRect);

        return scrollX;
    }
}
