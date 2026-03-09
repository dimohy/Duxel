namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginChild(string id, UiVector2 size, bool border = false)
    {
        id ??= "Child";
        var resolvedId = ResolveId(id);

        var available = GetContentRegionAvail();
        var width = size.X > 0f ? size.X : MathF.Max(1f, available.X > 0f ? available.X : InputWidth);
        var height = size.Y > 0f ? size.Y : GetFrameHeight() * 6f;
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);
        var contentRect = new UiRect(rect.X + 2f, rect.Y + 2f, MathF.Max(0f, rect.Width - 4f), MathF.Max(0f, rect.Height - 4f));

        if (border)
        {
            AddRectFilled(rect, _theme.FrameBg, _whiteTexture);
        }

        var scrollY = _state.GetScrollY(resolvedId);
        var scrollX = _state.GetScrollX(resolvedId);

        PushClipRect(contentRect, true);
        var start = new UiVector2(contentRect.X - scrollX, contentRect.Y - scrollY);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _childStack.Push(new UiChildState(
            resolvedId,
            rect,
            start,
            scrollY,
            scrollX,
            contentRect.X,
            _lastItemPos,
            _lastItemSize,
            _lastItemId,
            _lastItemFlags,
            _columnsActive,
            _columnsCount,
            _columnsIndex,
            _columnsStartX,
            _columnsStartY,
            _columnsWidth,
            _columnsMaxY,
            _columnYs,
            _columnWidths));
        _columnsActive = false;
        _columnsCount = 0;
        _columnsIndex = 0;
        _columnsStartX = 0f;
        _columnsStartY = 0f;
        _columnsWidth = 0f;
        _columnsMaxY = 0f;
        _columnYs = Array.Empty<float>();
        _columnWidths = Array.Empty<float>();
        return true;
    }

    public void EndChild()
    {
        if (_childStack.Count == 0)
        {
            return;
        }

        var cursorY = _layouts.Peek().Cursor.Y;
        _layouts.Pop();

        var state = _childStack.Pop();

        // Calculate content height and max scroll
        var contentHeight = cursorY - state.Rect.Y - 2f + state.ScrollY;
        var contentWidth = MathF.Max(0f, state.ContentMaxX - (state.Rect.X + 2f));
        var visibleWidth = MathF.Max(0f, state.Rect.Width - 4f);
        var visibleHeight = MathF.Max(0f, state.Rect.Height - 4f);
        var maxScrollX = 0f;
        var maxScrollY = 0f;
        var hasHScroll = false;
        var hasVScroll = false;

        for (var i = 0; i < 2; i++)
        {
            maxScrollX = MathF.Max(0f, contentWidth - visibleWidth);
            maxScrollY = MathF.Max(0f, contentHeight - visibleHeight);
            hasHScroll = maxScrollX > 0f;
            hasVScroll = maxScrollY > 0f;

            var nextVisibleWidth = MathF.Max(0f, state.Rect.Width - 4f - (hasVScroll ? ScrollbarSize : 0f));
            var nextVisibleHeight = MathF.Max(0f, state.Rect.Height - 4f - (hasHScroll ? ScrollbarSize : 0f));
            if (MathF.Abs(nextVisibleWidth - visibleWidth) < 0.001f && MathF.Abs(nextVisibleHeight - visibleHeight) < 0.001f)
            {
                break;
            }

            visibleWidth = nextVisibleWidth;
            visibleHeight = nextVisibleHeight;
        }

        var scrollX = Math.Clamp(state.ScrollX, 0f, maxScrollX);
        var scrollY = Math.Clamp(state.ScrollY, 0f, maxScrollY);

        // Mouse wheel
        var hovered = IsHovering(state.Rect);
        var popupBlocking = _popupTierDepth == 0 && IsMouseOverAnyBlockingPopup();
        if (hovered && !popupBlocking && MathF.Abs(_mouseWheel) > 0.001f && maxScrollY > 0f)
        {
            var frameHeight = GetFrameHeight();
            scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, maxScrollY);
            _mouseWheel = 0f;
        }
        if (hovered && !popupBlocking && MathF.Abs(_mouseWheelHorizontal) > 0.001f && maxScrollX > 0f)
        {
            var frameWidth = GetFrameHeight();
            scrollX = Math.Clamp(scrollX - (_mouseWheelHorizontal * frameWidth * 3f), 0f, maxScrollX);
            _mouseWheelHorizontal = 0f;
        }

        // Render scrollbar before PopClipRect (respects parent clip)
        if (maxScrollY > 0f)
        {
            var trackRect = new UiRect(
                state.Rect.X + state.Rect.Width - ScrollbarSize,
                state.Rect.Y,
                ScrollbarSize,
                state.Rect.Height - (hasHScroll ? ScrollbarSize : 0f)
            );
            scrollY = RenderScrollbarV($"{state.Id}##childscroll", trackRect, scrollY, maxScrollY, contentHeight, CurrentClipRect);
        }

        if (maxScrollX > 0f)
        {
            var trackRect = new UiRect(
                state.Rect.X,
                state.Rect.Y + state.Rect.Height - ScrollbarSize,
                state.Rect.Width - (hasVScroll ? ScrollbarSize : 0f),
                ScrollbarSize
            );
            scrollX = RenderScrollbarH($"{state.Id}##childscrollx", trackRect, scrollX, maxScrollX, contentWidth, CurrentClipRect);
        }

        PopClipRect();
        _state.SetScrollX(state.Id, scrollX);
        _state.SetScrollY(state.Id, scrollY);
        _lastItemPos = new UiVector2(state.Rect.X, state.Rect.Y);
        _lastItemSize = new UiVector2(state.Rect.Width, state.Rect.Height);
        _lastItemId = state.Id;
        _lastItemFlags = state.LastItemFlags;
        _columnsActive = state.ColumnsActive;
        _columnsCount = state.ColumnsCount;
        _columnsIndex = state.ColumnsIndex;
        _columnsStartX = state.ColumnsStartX;
        _columnsStartY = state.ColumnsStartY;
        _columnsWidth = state.ColumnsWidth;
        _columnsMaxY = state.ColumnsMaxY;
        _columnYs = state.ColumnYs;
        _columnWidths = state.ColumnWidths;

        if (MathF.Abs(scrollY - state.ScrollY) > 0.001f || MathF.Abs(scrollX - state.ScrollX) > 0.001f)
        {
            _requestFrame?.Invoke();
        }
    }
}

