namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginMainMenuBar()
    {
        return BeginMenuBar();
    }

    public void EndMainMenuBar()
    {
        EndMenuBar();
    }

    public bool BeginMenuBar()
    {
        var frameHeight = GetFrameHeight();
        var width = _hasWindowRect ? MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f)) : InputWidth;
        var cursor = _layouts.Peek().Cursor;
        var rect = new UiRect(cursor.X, cursor.Y, width, frameHeight);
        AddRectFilled(rect, _theme.MenuBarBg, _whiteTexture);

        _inMenuBar = true;
        BeginRow();
        return true;
    }

    public void EndMenuBar()
    {
        EndRow();
        _inMenuBar = false;
    }

    public bool BeginMenu(string label)
    {
        label ??= "Menu";

        var rawId = $"{label}##menu";
        var id = ResolveId(rawId);
        var openKey = $"{id}##open";

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + (ButtonPaddingX * 2f), GetFrameHeight());
        var cursor = AdvanceCursor(size);
        var buttonRect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(rawId, buttonRect, out var hovered, out var held);
        var isOpen = _state.GetBool(openKey, false);
        if (pressed)
        {
            isOpen = !isOpen;
            _state.SetBool(openKey, isOpen);
        }

        var bg = isOpen ? _theme.HeaderActive : held ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(buttonRect, bg, _whiteTexture);

        var textPos = new UiVector2(buttonRect.X + ButtonPaddingX, buttonRect.Y + (buttonRect.Height - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            textPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        if (!isOpen)
        {
            return false;
        }

        var popupWidth = MathF.Max(160f, textSize.X + 60f);
        var popupHeight = GetFrameHeight() * 6f;
        var popupOffsetY = _inMenuBar ? 2f : 0f;
        var popupRect = ClampRectToDisplay(new UiRect(buttonRect.X, buttonRect.Y + buttonRect.Height + popupOffsetY, popupWidth, popupHeight));

        if (_leftMousePressed && !IsHovering(popupRect) && !IsHovering(buttonRect))
        {
            _state.SetBool(openKey, false);
            return false;
        }

        PushOverlay();
        PushClipRect(IntersectRect(_clipRect, popupRect), false);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
        var start = new UiVector2(popupRect.X + 6f, popupRect.Y + 4f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _menuStack.Push(new UiMenuState(id, buttonRect, popupRect));
        return true;
    }

    public void EndMenu()
    {
        if (_menuStack.Count == 0)
        {
            return;
        }

        _layouts.Pop();
        PopClipRect();
        PopOverlay();
        _menuStack.Pop();
    }

    public bool MenuItem(string label, bool selected = false, bool enabled = true)
    {
        label ??= "Item";

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var height = GetFrameHeight();
        var width = MathF.Max(140f, textSize.X + (ButtonPaddingX * 2f));
        var size = new UiVector2(width, height);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = false;
        bool hovered;
        if (enabled)
        {
            pressed = ButtonBehavior(label, rect, out hovered, out _);
        }
        else
        {
            hovered = ItemHoverable(label, rect);
        }
        if (selected)
        {
            AddRectFilled(rect, _theme.HeaderActive, _whiteTexture);
        }
        else if (hovered)
        {
            AddRectFilled(rect, _theme.HeaderHovered, _whiteTexture);
        }

        var textColor = enabled ? _theme.Text : _theme.TextDisabled;
        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (rect.Height - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            textPos,
            textColor,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        return pressed;
    }
}
