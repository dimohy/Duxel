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
            AddRectFilled(rowRect, _theme.HeaderActive, _whiteTexture);
        }
        else if (hovered)
        {
            AddRectFilled(rowRect, _theme.HeaderHovered, _whiteTexture);
        }

        return pressed;
    }

    public bool ListBoxHeader(UiVector2 size, int itemsCount = -1, string? id = null)
    {
        var resolvedId = ResolveId(string.IsNullOrWhiteSpace(id) ? "ListBox" : id);
        var frameHeight = GetFrameHeight();

        var width = size.X > 0f ? size.X : ResolveItemWidth(InputWidth);
        var height = size.Y > 0f ? size.Y : frameHeight * Math.Max(1, itemsCount > 0 ? itemsCount : 6);

        var cursor = AdvanceCursor(new UiVector2(width, height));
        var listRect = new UiRect(cursor.X, cursor.Y, width, height);
        AddRectFilled(listRect, _theme.FrameBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(resolvedId);
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && !(_popupTierDepth == 0 && IsMouseOverAnyBlockingPopup()))
        {
            scrollY = MathF.Max(0f, scrollY - (_mouseWheel * frameHeight * 3f));
        }

        PushListBoxLayout(resolvedId, listRect, scrollY);
        return true;
    }

    public void ListBoxFooter()
    {
        PopListBoxLayout();
    }
}

