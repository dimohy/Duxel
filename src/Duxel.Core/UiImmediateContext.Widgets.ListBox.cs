namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool ListBox(ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int visibleItems = 6, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(itemsGetter);
        var listId = string.IsNullOrWhiteSpace(id) ? "ListBox" : id;
        var resolvedId = ResolveId(listId);

        if (itemsCount <= 0)
        {
            currentIndex = 0;
        }
        else if (currentIndex < 0 || currentIndex >= itemsCount)
        {
            currentIndex = Math.Clamp(currentIndex, 0, itemsCount - 1);
        }

        visibleItems = Math.Max(1, visibleItems);

        var frameHeight = GetFrameHeight();
        var boxHeight = (frameHeight * visibleItems) + 4f;
        var listWidth = ResolveItemWidth(InputWidth);
        var cursor = AdvanceCursor(new UiVector2(listWidth, boxHeight));
        var listRect = new UiRect(cursor.X, cursor.Y, listWidth, boxHeight);
        AddRectFilled(listRect, _theme.FrameBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(resolvedId);
        var contentHeight = itemsCount * frameHeight;
        var maxScroll = MathF.Max(0f, contentHeight - (listRect.Height - 4f));
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f && !(_popupTierDepth == 0 && IsMouseOverAnyBlockingPopup()))
        {
            scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, maxScroll);
        }

        var changed = false;
        PushClipRect(listRect, true);
        for (var i = 0; i < itemsCount; i++)
        {
            var itemY = listRect.Y + 2f - scrollY + (i * frameHeight);
            var itemRect = new UiRect(listRect.X, itemY, listRect.Width, frameHeight);
            if (itemRect.Y + itemRect.Height < listRect.Y || itemRect.Y > listRect.Y + listRect.Height)
            {
                continue;
            }

            var itemId = ResolveId($"{listId}##item{i}");
            var itemHovered = ItemHoverable(itemId, itemRect);

            if (i == currentIndex)
            {
                AddRectFilled(itemRect, _theme.HeaderActive, _whiteTexture);
            }
            else if (itemHovered)
            {
                AddRectFilled(itemRect, _theme.HeaderHovered, _whiteTexture);
            }

            if (_leftMousePressed && itemHovered)
            {
                currentIndex = i;
                changed = true;
            }

            var itemText = itemsGetter(i) ?? string.Empty;
            var itemSize = UiTextBuilder.MeasureText(_fontAtlas, itemText, _textSettings, _lineHeight);
            var itemPos = new UiVector2(itemRect.X + ButtonPaddingX, itemRect.Y + (frameHeight - itemSize.Y) * 0.5f);
            _builder.AddText(
                _fontAtlas,
                itemText,
                itemPos,
                _theme.Text,
                _fontTexture,
                IntersectRect(CurrentClipRect, listRect),
                _textSettings,
                _lineHeight
            );
        }

        if (maxScroll > 0f)
        {
            var trackRect = new UiRect(
                listRect.X + listRect.Width - ScrollbarSize,
                listRect.Y,
                ScrollbarSize,
                listRect.Height
            );
            scrollY = RenderScrollbarV($"{resolvedId}##lbscroll", trackRect, scrollY, maxScroll, contentHeight, CurrentClipRect);
        }
        PopClipRect();

        _state.SetScrollY(resolvedId, scrollY);
        return changed;
    }

    public bool ListBox(ref int currentIndex, IReadOnlyList<string> items, int visibleItems = 6, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var listId = string.IsNullOrWhiteSpace(id) ? "ListBox" : id;
        var resolvedId = ResolveId(listId);

        if (items.Count == 0)
        {
            currentIndex = 0;
        }
        else if (currentIndex < 0 || currentIndex >= items.Count)
        {
            currentIndex = Math.Clamp(currentIndex, 0, items.Count - 1);
        }

        visibleItems = Math.Max(1, visibleItems);

        var frameHeight = GetFrameHeight();
        var boxHeight = (frameHeight * visibleItems) + 4f;
        var listWidth = ResolveItemWidth(InputWidth);
        var cursor = AdvanceCursor(new UiVector2(listWidth, boxHeight));
        var listRect = new UiRect(cursor.X, cursor.Y, listWidth, boxHeight);
        AddRectFilled(listRect, _theme.FrameBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(resolvedId);
        var contentHeight = items.Count * frameHeight;
        var maxScroll = MathF.Max(0f, contentHeight - (listRect.Height - 4f));
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f && !(_popupTierDepth == 0 && IsMouseOverAnyBlockingPopup()))
        {
            scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, maxScroll);
        }

        var changed = false;
        PushClipRect(listRect, true);
        for (var i = 0; i < items.Count; i++)
        {
            var itemY = listRect.Y + 2f - scrollY + (i * frameHeight);
            var itemRect = new UiRect(listRect.X, itemY, listRect.Width, frameHeight);
            if (itemRect.Y + itemRect.Height < listRect.Y || itemRect.Y > listRect.Y + listRect.Height)
            {
                continue;
            }

            var itemId = ResolveId($"{listId}##item{i}");
            var itemHovered = ItemHoverable(itemId, itemRect);

            if (i == currentIndex)
            {
                AddRectFilled(itemRect, _theme.HeaderActive, _whiteTexture);
            }
            else if (itemHovered)
            {
                AddRectFilled(itemRect, _theme.HeaderHovered, _whiteTexture);
            }

            if (_leftMousePressed && itemHovered)
            {
                currentIndex = i;
                changed = true;
            }

            var itemText = items[i];
            var itemSize = UiTextBuilder.MeasureText(_fontAtlas, itemText, _textSettings, _lineHeight);
            var itemPos = new UiVector2(itemRect.X + ButtonPaddingX, itemRect.Y + (frameHeight - itemSize.Y) * 0.5f);
            _builder.AddText(
                _fontAtlas,
                itemText,
                itemPos,
                _theme.Text,
                _fontTexture,
                IntersectRect(CurrentClipRect, listRect),
                _textSettings,
                _lineHeight
            );
        }

        if (maxScroll > 0f)
        {
            var trackRect = new UiRect(
                listRect.X + listRect.Width - ScrollbarSize,
                listRect.Y,
                ScrollbarSize,
                listRect.Height
            );
            scrollY = RenderScrollbarV($"{resolvedId}##lbscroll", trackRect, scrollY, maxScroll, contentHeight, CurrentClipRect);
        }
        PopClipRect();

        _state.SetScrollY(resolvedId, scrollY);
        return changed;
    }
}

