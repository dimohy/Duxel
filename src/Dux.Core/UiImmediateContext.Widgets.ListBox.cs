namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool ListBox(string label, ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int visibleItems = 6)
    {
        label ??= "ListBox";
        ArgumentNullException.ThrowIfNull(itemsGetter);
        var id = ResolveId(label);

        if (itemsCount <= 0)
        {
            currentIndex = 0;
        }
        else if (currentIndex < 0 || currentIndex >= itemsCount)
        {
            currentIndex = Math.Clamp(currentIndex, 0, itemsCount - 1);
        }

        visibleItems = Math.Max(1, visibleItems);

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var boxHeight = (frameHeight * visibleItems) + 4f;
        var listWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + listWidth, MathF.Max(textSize.Y, boxHeight));
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + (totalSize.Y - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            labelPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        var listRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (totalSize.Y - boxHeight) * 0.5f, listWidth, boxHeight);
        AddRectFilled(listRect, _theme.FrameBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(id);
        var contentHeight = itemsCount * frameHeight;
        var maxScroll = MathF.Max(0f, contentHeight - (listRect.Height - 4f));
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f)
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

            var itemId = ResolveId($"{label}##item{i}");
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
        PopClipRect();

        _state.SetScrollY(id, scrollY);
        return changed;
    }

    public bool ListBox(string label, ref int currentIndex, IReadOnlyList<string> items, int visibleItems = 6)
    {
        label ??= "ListBox";
        ArgumentNullException.ThrowIfNull(items);
        var id = ResolveId(label);

        if (items.Count == 0)
        {
            currentIndex = 0;
        }
        else if (currentIndex < 0 || currentIndex >= items.Count)
        {
            currentIndex = Math.Clamp(currentIndex, 0, items.Count - 1);
        }

        visibleItems = Math.Max(1, visibleItems);

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var boxHeight = (frameHeight * visibleItems) + 4f;
        var listWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + listWidth, MathF.Max(textSize.Y, boxHeight));
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + (totalSize.Y - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            labelPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        var listRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (totalSize.Y - boxHeight) * 0.5f, listWidth, boxHeight);
        AddRectFilled(listRect, _theme.FrameBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(id);
        var contentHeight = items.Count * frameHeight;
        var maxScroll = MathF.Max(0f, contentHeight - (listRect.Height - 4f));
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f)
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

            var itemId = ResolveId($"{label}##item{i}");
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
        PopClipRect();

        _state.SetScrollY(id, scrollY);
        return changed;
    }
}
