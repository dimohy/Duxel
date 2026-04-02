namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginListBox(UiVector2 size, int itemsCount = -1, string? id = null)
    {
        return ListBoxHeader(size, itemsCount, id);
    }

    public void EndListBox()
    {
        ListBoxFooter();
    }

    public bool ListBoxRow(string key, bool selected, out UiRect rowRect)
    {
        var rowHeight = GetFrameHeight();
        return ListBoxRow(key, selected, new UiVector2(0f, rowHeight), out rowRect);
    }

    public bool ListBoxRow(string key, bool selected, UiVector2 size, out UiRect rowRect)
    {
        key = string.IsNullOrWhiteSpace(key) ? "ListBoxRow" : key;

        var frameHeight = GetFrameHeight();
        var height = size.Y > 0f ? size.Y : frameHeight;
        var width = size.X;

        if (_listBoxStack.Count > 0)
        {
            var listRect = _listBoxStack.Peek().Rect;
            width = width > 0f ? width : MathF.Max(1f, listRect.Width - 4f);

            var finalSize = new UiVector2(width, height);
            var cursor = AdvanceCursor(finalSize);
            rowRect = new UiRect(listRect.X + 2f, cursor.Y, width, height);
        }
        else
        {
            width = width > 0f ? width : ResolveItemWidth(InputWidth);
            var finalSize = new UiVector2(width, height);
            var cursor = AdvanceCursor(finalSize);
            rowRect = new UiRect(cursor.X, cursor.Y, width, height);
        }

        var pressed = ButtonBehavior(key, rowRect, out var hovered, out _);

        if (selected)
        {
            AddRectFilled(rowRect, _theme.ListBoxItemBgActive, _whiteTexture);
        }
        else if (hovered)
        {
            AddRectFilled(rowRect, _theme.ListBoxItemBgHovered, _whiteTexture);
        }

        return pressed;
    }

    public bool ListBoxHeader(UiVector2 size, int itemsCount = -1, string? id = null)
    {
        var resolvedId = ResolveId(string.IsNullOrWhiteSpace(id) ? "ListBox" : id);
        var frameHeight = GetFrameHeight();

        var width = size.X > 0f ? size.X : ResolveItemWidth(InputWidth);
        var visibleCount = Math.Max(1, itemsCount > 0 ? itemsCount : 6);
        var height = size.Y > 0f ? size.Y : (frameHeight + ItemSpacingY) * visibleCount - ItemSpacingY + 4f;

        var cursor = AdvanceCursor(new UiVector2(width, height));
        var listRect = new UiRect(cursor.X, cursor.Y, width, height);
        AddRectFilled(listRect, _theme.ListBoxBorder, _whiteTexture);
        var innerListRect = new UiRect(listRect.X + 1f, listRect.Y + 1f, MathF.Max(0f, listRect.Width - 2f), MathF.Max(0f, listRect.Height - 2f));
        AddRectFilled(innerListRect, _theme.ListBoxBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(resolvedId);
        var prevMaxScroll = _state.GetScrollY($"{resolvedId}##maxScroll");
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && !(_popupTierDepth == 0 && IsMouseOverAnyBlockingPopup()))
        {
            scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, MathF.Max(0f, prevMaxScroll));
            _mouseWheel = 0f;
        }

        PushListBoxLayout(resolvedId, listRect, scrollY);
        return true;
    }

    public void ListBoxFooter()
    {
        PopListBoxLayout();
    }
}

