namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const float ScrollbarRecentInteractionDurationSeconds = 0.75f;
    private const float ScrollbarTrackIdleAlphaFactor = 0.06f;
    private const float ScrollbarTrackHoverAlphaFactor = 0.18f;
    private const float ScrollbarTrackActiveAlphaFactor = 0.32f;
    private const float ScrollbarHandleIdleAlphaFactor = 0.28f;
    private const float ScrollbarHandleHoverAlphaFactor = 0.58f;
    private const float ScrollbarHandleActiveAlphaFactor = 0.86f;

    /// <summary>
    /// Applies an alpha multiplier to a color's alpha channel.
    /// </summary>
    private static uint ApplyAlpha(uint color, float alphaFactor)
    {
        var a = (byte)((color >> 24) * alphaFactor);
        return (color & 0x00FFFFFF) | ((uint)a << 24);
    }

    private bool IsScrollbarRecentlyInteracted(string scrollId, float value)
    {
        var previousValue = _state.GetScrollY($"{scrollId}##prev");
        if (MathF.Abs(previousValue - value) > 0.001f)
        {
            _state.SetScrollY($"{scrollId}##flash", (float)_state.TimeSeconds);
        }

        _state.SetScrollY($"{scrollId}##prev", value);
        var lastInteractionTime = _state.GetScrollY($"{scrollId}##flash");
        return (_state.TimeSeconds - lastInteractionTime) <= ScrollbarRecentInteractionDurationSeconds;
    }

    private static float GetScrollbarTrackAlphaFactor(bool active, bool hovered, bool recentInteraction)
        => active
            ? ScrollbarTrackActiveAlphaFactor
            : hovered || recentInteraction
                ? ScrollbarTrackHoverAlphaFactor
                : ScrollbarTrackIdleAlphaFactor;

    private static float GetScrollbarHandleAlphaFactor(bool active, bool handleHovered, bool trackHovered, bool recentInteraction)
        => active || recentInteraction
            ? ScrollbarHandleActiveAlphaFactor
            : handleHovered || trackHovered
                ? ScrollbarHandleHoverAlphaFactor
                : ScrollbarHandleIdleAlphaFactor;

    /// <summary>
    /// Renders a vertical scrollbar and handles drag/track-click interactions.
    /// The theme colors already carry their own alpha, so the thumb must stay visible while idle.
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
        var recentInteraction = IsScrollbarRecentlyInteracted(scrollId, scrollY);

        var trackAlphaFactor = GetScrollbarTrackAlphaFactor(scrollActive, trackHovered, recentInteraction);

        // Track background
        _builder.AddRectFilled(trackRect, ApplyAlpha(_theme.ScrollbarBg, trackAlphaFactor), _whiteTexture, clipRect);

        // Handle size: proportional to visible/content ratio, with minimum
        var handleHeight = MathF.Max(16f, trackRect.Height * (trackRect.Height / contentHeight));
        var handleY = trackRect.Y + (scrollY / maxScroll) * (trackRect.Height - handleHeight);
        var handleRect = new UiRect(trackRect.X + 2f, handleY, trackRect.Width - 4f, handleHeight);

        var handleHover = IsHovering(handleRect);
        var trackClickArea = trackHovered && !handleHover;

        // Handle drag (only capture if no other interaction owns ActiveId)
        if (_leftMousePressed && handleHover && (_state.ActiveId is null || _state.ActiveId == scrollId))
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
        if (_leftMousePressed && trackClickArea && _state.ActiveId is null)
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
        var handleColor = scrollActive || recentInteraction
            ? _theme.ScrollbarGrabActive
            : handleHover
                ? _theme.ScrollbarGrabHovered
                : _theme.ScrollbarGrab;
        var handleAlphaFactor = GetScrollbarHandleAlphaFactor(scrollActive, handleHover, trackHovered, recentInteraction);
        _builder.AddRectFilled(handleRect, ApplyAlpha(handleColor, handleAlphaFactor), _whiteTexture, clipRect);

        return scrollY;
    }

    /// <summary>
    /// Renders a horizontal scrollbar and handles drag/track-click interactions.
    /// The theme colors already carry their own alpha, so the thumb must stay visible while idle.
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
        var recentInteraction = IsScrollbarRecentlyInteracted(scrollId, scrollX);

        var trackAlphaFactor = GetScrollbarTrackAlphaFactor(scrollActive, trackHovered, recentInteraction);

        // Track background
        _builder.AddRectFilled(trackRect, ApplyAlpha(_theme.ScrollbarBg, trackAlphaFactor), _whiteTexture, clipRect);

        // Handle size: proportional to visible/content ratio, with minimum
        var handleWidth = MathF.Max(24f, trackRect.Width * (trackRect.Width / contentWidth));
        var handleX = trackRect.X + (scrollX / maxScrollX) * (trackRect.Width - handleWidth);
        var handleRect = new UiRect(handleX, trackRect.Y + 2f, handleWidth, trackRect.Height - 4f);

        var handleHover = IsHovering(handleRect);
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

        if (scrollActive && _leftMouseDown)
        {
            var offset = _state.GetScrollX($"{scrollId}##ofs");
            var t = Math.Clamp((_mousePosition.X - offset - trackRect.X) / (trackRect.Width - handleWidth), 0f, 1f);
            scrollX = t * maxScrollX;
        }

        // Track click → page scroll
        if (_leftMousePressed && trackClickArea && _state.ActiveId is null)
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
        var handleColor = scrollActive || recentInteraction
            ? _theme.ScrollbarGrabActive
            : handleHover
                ? _theme.ScrollbarGrabHovered
                : _theme.ScrollbarGrab;
        var handleAlphaFactor = GetScrollbarHandleAlphaFactor(scrollActive, handleHover, trackHovered, recentInteraction);
        _builder.AddRectFilled(handleRect, ApplyAlpha(handleColor, handleAlphaFactor), _whiteTexture, clipRect);

        return scrollX;
    }
}
