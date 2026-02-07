namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginCombo(string label, string previewValue, int popupMaxHeightInItems = 8)
    {
        label ??= "Combo";
        previewValue ??= string.Empty;
        var id = ResolveId(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var comboWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + comboWidth, height);
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + (height - textSize.Y) * 0.5f);
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

        var comboRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, comboWidth, frameHeight);
        var pressed = ButtonBehavior(label, comboRect, out var hovered, out var held);
        var bg = held ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(comboRect, bg, _whiteTexture);

        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, previewValue, _textSettings, _lineHeight);
        var valuePos = new UiVector2(comboRect.X + 6f, comboRect.Y + (comboRect.Height - valueSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            previewValue,
            valuePos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        if (pressed)
        {
            _state.OpenComboId = _state.OpenComboId == id ? null : id;
        }

        if (_state.OpenComboId != id)
        {
            return false;
        }

        var visibleItems = Math.Clamp(popupMaxHeightInItems, 1, 12);
        var popupHeight = visibleItems * frameHeight;
        var popupRect = new UiRect(comboRect.X, comboRect.Y + comboRect.Height + ItemSpacingY, comboRect.Width, popupHeight);

        PushPopup();
        _state.AddPopupBlockingRect(popupRect);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);

        if (_leftMousePressed && !IsHovering(popupRect) && !IsHovering(comboRect))
        {
            _state.OpenComboId = null;
            PopPopup();
            return false;
        }

        PushClipRect(popupRect, true);
        var start = new UiVector2(popupRect.X + 6f, popupRect.Y + 4f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _comboStack.Push(popupRect);
        return true;
    }

    public void EndCombo()
    {
        if (_comboStack.Count == 0)
        {
            return;
        }

        _layouts.Pop();
        PopClipRect();
        PopPopup();
        _comboStack.Pop();
    }

    public bool Combo(string label, ref int currentIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8)
    {
        label ??= "Combo";
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

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var comboWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + comboWidth, height);
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + (height - textSize.Y) * 0.5f);
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

        var comboRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, comboWidth, frameHeight);
        var pressed = ButtonBehavior(label, comboRect, out var hovered, out var held);
        var bg = held ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(comboRect, bg, _whiteTexture);

        var currentText = items.Count > 0 ? items[currentIndex] : string.Empty;
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, currentText, _textSettings, _lineHeight);
        var valuePos = new UiVector2(comboRect.X + 6f, comboRect.Y + (comboRect.Height - valueSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            currentText,
            valuePos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        if (pressed)
        {
            _state.OpenComboId = _state.OpenComboId == id ? null : id;
        }

        var changed = false;
        if (_state.OpenComboId == id)
        {
            var maxVisible = Math.Clamp(popupMaxHeightInItems, 1, 12);
            var displayCount = Math.Min(maxVisible, items.Count);
            var popupHeight = displayCount * frameHeight;
            var popupRect = new UiRect(comboRect.X, comboRect.Y + comboRect.Height + ItemSpacingY, comboRect.Width, popupHeight);
            var contentHeight = items.Count * frameHeight;
            var maxScroll = MathF.Max(0f, contentHeight - popupHeight);
            var comboScrollId = $"{id}##comboscroll";
            var scrollY = _state.GetScrollY(comboScrollId);
            scrollY = Math.Clamp(scrollY, 0f, maxScroll);

            PushPopup();
            _state.AddPopupBlockingRect(popupRect);

            if (displayCount > 0)
            {
                AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
            }

            var clickedOutside = _leftMousePressed && !IsHovering(popupRect) && !IsHovering(comboRect);
            if (clickedOutside)
            {
                _state.OpenComboId = null;
            }

            // Mouse wheel
            if (IsHovering(popupRect) && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f)
            {
                scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, maxScroll);
            }

            PushClipRect(popupRect, false);
            for (var i = 0; i < items.Count; i++)
            {
                var itemY = popupRect.Y + (i * frameHeight) - scrollY;
                if (itemY + frameHeight < popupRect.Y || itemY > popupRect.Y + popupHeight)
                {
                    continue;
                }

                var itemRect = new UiRect(popupRect.X, itemY, popupRect.Width, frameHeight);
                var itemId = ResolveId($"{label}##item{i}");
                var itemHovered = ItemHoverable(itemId, itemRect);
                if (itemHovered)
                {
                    AddRectFilled(itemRect, _theme.HeaderHovered, _whiteTexture);
                }

                if (_leftMousePressed && itemHovered)
                {
                    currentIndex = i;
                    changed = true;
                    _state.OpenComboId = null;
                }

                var itemText = items[i];
                var itemSize = UiTextBuilder.MeasureText(_fontAtlas, itemText, _textSettings, _lineHeight);
                var itemPos = new UiVector2(itemRect.X + 6f, itemRect.Y + (itemRect.Height - itemSize.Y) * 0.5f);
                _builder.AddText(
                    _fontAtlas,
                    itemText,
                    itemPos,
                    _theme.Text,
                    _fontTexture,
                    CurrentClipRect,
                    _textSettings,
                    _lineHeight
                );
            }
            PopClipRect();

            // Scrollbar
            if (maxScroll > 0f)
            {
                var trackRect = new UiRect(
                    popupRect.X + popupRect.Width - ScrollbarSize,
                    popupRect.Y,
                    ScrollbarSize,
                    popupRect.Height
                );
                scrollY = RenderScrollbarV($"{id}##comboscrollbar", trackRect, scrollY, maxScroll, contentHeight, popupRect);
            }

            _state.SetScrollY(comboScrollId, scrollY);
            PopPopup();
        }

        return changed;
    }

    public bool Combo(string label, ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int popupMaxHeightInItems = 8)
    {
        label ??= "Combo";
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

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var comboWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + comboWidth, height);
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + (height - textSize.Y) * 0.5f);
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

        var comboRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, comboWidth, frameHeight);
        var pressed = ButtonBehavior(label, comboRect, out var hovered, out var held);
        var bg = held ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(comboRect, bg, _whiteTexture);

        var currentText = itemsCount > 0 ? itemsGetter(currentIndex) ?? string.Empty : string.Empty;
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, currentText, _textSettings, _lineHeight);
        var valuePos = new UiVector2(comboRect.X + 6f, comboRect.Y + (comboRect.Height - valueSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            currentText,
            valuePos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        if (pressed)
        {
            _state.OpenComboId = _state.OpenComboId == id ? null : id;
        }

        var changed = false;
        if (_state.OpenComboId == id)
        {
            var maxVisible = Math.Clamp(popupMaxHeightInItems, 1, 12);
            var displayCount = Math.Min(maxVisible, itemsCount);
            var popupHeight = displayCount * frameHeight;
            var popupRect = new UiRect(comboRect.X, comboRect.Y + comboRect.Height + ItemSpacingY, comboRect.Width, popupHeight);
            var contentHeight = itemsCount * frameHeight;
            var maxScroll = MathF.Max(0f, contentHeight - popupHeight);
            var comboScrollId = $"{id}##comboscroll";
            var scrollY = _state.GetScrollY(comboScrollId);
            scrollY = Math.Clamp(scrollY, 0f, maxScroll);

            PushPopup();
            _state.AddPopupBlockingRect(popupRect);

            if (displayCount > 0)
            {
                AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
            }

            var clickedOutside = _leftMousePressed && !IsHovering(popupRect) && !IsHovering(comboRect);
            if (clickedOutside)
            {
                _state.OpenComboId = null;
            }

            // Mouse wheel
            if (IsHovering(popupRect) && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f)
            {
                scrollY = Math.Clamp(scrollY - (_mouseWheel * frameHeight * 3f), 0f, maxScroll);
            }

            PushClipRect(popupRect, false);
            for (var i = 0; i < itemsCount; i++)
            {
                var itemY = popupRect.Y + (i * frameHeight) - scrollY;
                if (itemY + frameHeight < popupRect.Y || itemY > popupRect.Y + popupHeight)
                {
                    continue;
                }

                var itemRect = new UiRect(popupRect.X, itemY, popupRect.Width, frameHeight);
                var itemId = ResolveId($"{label}##item{i}");
                var itemHovered = ItemHoverable(itemId, itemRect);
                if (itemHovered)
                {
                    AddRectFilled(itemRect, _theme.HeaderHovered, _whiteTexture);
                }

                if (_leftMousePressed && itemHovered)
                {
                    currentIndex = i;
                    changed = true;
                    _state.OpenComboId = null;
                }

                var itemText = itemsGetter(i) ?? string.Empty;
                var itemSize = UiTextBuilder.MeasureText(_fontAtlas, itemText, _textSettings, _lineHeight);
                var itemPos = new UiVector2(itemRect.X + 6f, itemRect.Y + (itemRect.Height - itemSize.Y) * 0.5f);
                _builder.AddText(
                    _fontAtlas,
                    itemText,
                    itemPos,
                    _theme.Text,
                    _fontTexture,
                    CurrentClipRect,
                    _textSettings,
                    _lineHeight
                );
            }
            PopClipRect();

            // Scrollbar
            if (maxScroll > 0f)
            {
                var trackRect = new UiRect(
                    popupRect.X + popupRect.Width - ScrollbarSize,
                    popupRect.Y,
                    ScrollbarSize,
                    popupRect.Height
                );
                scrollY = RenderScrollbarV($"{id}##comboscrollbar", trackRect, scrollY, maxScroll, contentHeight, popupRect);
            }

            _state.SetScrollY(comboScrollId, scrollY);
            PopPopup();
        }

        return changed;
    }
}

