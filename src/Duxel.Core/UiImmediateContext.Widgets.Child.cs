namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginChild(string id, UiVector2 size, bool border = false)
    {
        id ??= "Child";
        var resolvedId = ResolveId(id);

        var width = size.X > 0f ? size.X : InputWidth;
        var height = size.Y > 0f ? size.Y : GetFrameHeight() * 6f;
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        if (border)
        {
            AddRectFilled(rect, _theme.FrameBg, _whiteTexture);
        }

        var scrollY = _state.GetScrollY(resolvedId);

        PushClipRect(rect, true);
        var start = new UiVector2(rect.X + 2f, rect.Y + 2f - scrollY);
        _layouts.Push(new UiLayoutState(start, false, 0f, rect.X + 2f));
        _childStack.Push(new UiChildState(resolvedId, rect, start, scrollY));
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
        var visibleHeight = state.Rect.Height - 4f;
        var maxScroll = MathF.Max(0f, contentHeight - visibleHeight);
        var scrollY = Math.Clamp(state.ScrollY, 0f, maxScroll);

        // Mouse wheel
        var hovered = IsHovering(state.Rect);
        var popupBlocking = _popupTierDepth == 0 && IsMouseOverAnyBlockingPopup();
        if (hovered && !popupBlocking && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f)
        {
            var frameHeight = GetFrameHeight();
            scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, maxScroll);
        }

        // Render scrollbar before PopClipRect (respects parent clip)
        if (maxScroll > 0f)
        {
            var trackRect = new UiRect(
                state.Rect.X + state.Rect.Width - ScrollbarSize,
                state.Rect.Y,
                ScrollbarSize,
                state.Rect.Height
            );
            scrollY = RenderScrollbarV($"{state.Id}##childscroll", trackRect, scrollY, maxScroll, contentHeight, CurrentClipRect);
        }

        PopClipRect();
        _state.SetScrollY(state.Id, scrollY);
    }
}

