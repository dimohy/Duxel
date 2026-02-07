namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const float MenuPaddingX = 6f;
    private const float MenuPaddingY = 4f;
    private const float MenuMinWidth = 160f;

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
        var menuSetKey = GetMenuSetKey();
        var menuSetOpenKey = $"{menuSetKey}##open";
        var openMenuId = _state.GetTextBuffer(menuSetOpenKey, string.Empty);
        var parentMenuOpen = _menuStack.Count > 0;
        var menuSetOpen = parentMenuOpen || !string.IsNullOrEmpty(openMenuId);
        var isOpen = string.Equals(openMenuId, id, StringComparison.Ordinal);

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        UiVector2 size;
        if (_inMenuBar)
        {
            size = new UiVector2(textSize.X + (ButtonPaddingX * 2f), GetFrameHeight());
        }
        else
        {
            var arrowSize = UiTextBuilder.MeasureText(_fontAtlas, ">", _textSettings, _lineHeight);
            var minWidth = textSize.X + arrowSize.X + (ButtonPaddingX * 4f);
            var targetWidth = MathF.Max(MenuMinWidth, minWidth);
            if (_menuStack.Count > 0)
            {
                targetWidth = MathF.Max(targetWidth, _menuStack.Peek().PopupRect.Width - (MenuPaddingX * 2f));
            }
            size = new UiVector2(targetWidth, GetFrameHeight());
        }
        var cursor = AdvanceCursor(size);
        var buttonRect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);
        if (_menuStack.Count > 0)
        {
            UpdateCurrentMenuMaxWidth(size.X);
        }

        var pressed = ButtonBehavior(rawId, buttonRect, out var hovered, out var held);
        var wantOpen = false;
        var wantClose = false;
        if (_inMenuBar)
        {
            if (isOpen && pressed && menuSetOpen)
            {
                wantClose = true;
            }
            else if (pressed || (hovered && menuSetOpen && !isOpen))
            {
                wantOpen = true;
            }
        }
        else
        {
            if (pressed)
            {
                wantOpen = true;
            }
            else if (hovered && menuSetOpen && !isOpen)
            {
                wantOpen = true;
            }
        }

        if (wantClose)
        {
            _state.SetTextBuffer(menuSetOpenKey, string.Empty);
            isOpen = false;
        }
        if (wantOpen)
        {
            _state.SetTextBuffer(menuSetOpenKey, id);
            isOpen = true;
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

        if (!_inMenuBar)
        {
            var arrowText = ">";
            var arrowSize = UiTextBuilder.MeasureText(_fontAtlas, arrowText, _textSettings, _lineHeight);
            var arrowPos = new UiVector2(
                buttonRect.X + buttonRect.Width - ButtonPaddingX - arrowSize.X,
                buttonRect.Y + (buttonRect.Height - arrowSize.Y) * 0.5f
            );
            _builder.AddText(
                _fontAtlas,
                arrowText,
                arrowPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        if (!isOpen)
        {
            return false;
        }
        var defaultPopupWidth = MathF.Max(MenuMinWidth, textSize.X + 60f);
        var defaultPopupHeight = (GetFrameHeight() * 6f) + (MenuPaddingY * 2f);
        var cachedPopupSize = _state.GetMenuSize(id, new UiVector2(defaultPopupWidth, defaultPopupHeight));
        UiRect popupRect;
        if (_inMenuBar)
        {
            var popupOffsetY = 2f;
            popupRect = new UiRect(buttonRect.X, buttonRect.Y + buttonRect.Height + popupOffsetY, cachedPopupSize.X, cachedPopupSize.Y);
        }
        else
        {
            var popupOffsetX = MathF.Max(4f, ItemSpacingX);
            popupRect = new UiRect(buttonRect.X + buttonRect.Width - popupOffsetX, buttonRect.Y - ButtonPaddingY, cachedPopupSize.X, cachedPopupSize.Y);
        }
        popupRect = ClampRectToDisplay(popupRect);

        _openMenuPopupRects.Add(popupRect);
        _openMenuButtonRects.Add(buttonRect);
        _state.AddPopupBlockingRect(popupRect);

        if (_leftMousePressed && !IsMouseOverAnyOpenMenuPopup() && !IsMouseOverAnyOpenMenuButton())
        {
            _state.SetTextBuffer(menuSetOpenKey, string.Empty);
            return false;
        }

        PushPopup();
        PushClipRect(popupRect, false);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
        var start = new UiVector2(popupRect.X + MenuPaddingX, popupRect.Y + MenuPaddingY);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        var submenuSetKey = GetSubmenuSetKey(id);
        var minContentWidth = MathF.Max(0f, defaultPopupWidth - (MenuPaddingX * 2f));
        _menuStack.Push(new UiMenuState(id, menuSetKey, submenuSetKey, buttonRect, popupRect, start, minContentWidth, size.X));
        return true;
    }

    public void EndMenu()
    {
        if (_menuStack.Count == 0)
        {
            return;
        }

        var menu = _menuStack.Pop();
        var layout = _layouts.Pop();
        var contentHeight = MathF.Max(0f, layout.Cursor.Y - menu.StartPos.Y - ItemSpacingY);
        var contentWidth = MathF.Max(menu.MinContentWidth, menu.MaxContentWidth);
        var popupWidth = contentWidth + (MenuPaddingX * 2f);
        var popupHeight = MathF.Max(GetFrameHeight(), contentHeight) + (MenuPaddingY * 2f);
        _state.SetMenuSize(menu.Id, new UiVector2(popupWidth, popupHeight));

        PopClipRect();
        PopPopup();
    }

    public bool MenuItem(string label, bool selected = false, bool enabled = true)
    {
        label ??= "Item";

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var height = GetFrameHeight();
        var checkText = "✓";
        var checkSize = UiTextBuilder.MeasureText(_fontAtlas, checkText, _textSettings, _lineHeight);
        var checkSpace = checkSize.X + 4f;
        var width = MathF.Max(140f, textSize.X + checkSpace + (ButtonPaddingX * 2f));
        if (_menuStack.Count > 0)
        {
            width = MathF.Max(width, _menuStack.Peek().PopupRect.Width - (MenuPaddingX * 2f));
        }
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
        var textPos = new UiVector2(rect.X + ButtonPaddingX + checkSpace, rect.Y + (rect.Height - textSize.Y) * 0.5f);
        if (selected)
        {
            var checkPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (rect.Height - checkSize.Y) * 0.5f);
            _builder.AddText(
                _fontAtlas,
                checkText,
                checkPos,
                textColor,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
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

        UpdateCurrentMenuMaxWidth(width);

        if (pressed)
        {
            CloseMenuStack();
        }

        return pressed;
    }

    private string GetMenuSetKey()
    {
        if (_menuStack.Count > 0)
        {
            return _menuStack.Peek().SubmenuSetKey;
        }

        var root = _currentWindowId ?? "global";
        return _inMenuBar ? $"{root}##menubar" : $"{root}##menu";
    }

    private static string GetSubmenuSetKey(string menuId)
    {
        return $"{menuId}##submenu";
    }

    private void CloseMenuStack()
    {
        if (_menuStack.Count == 0)
        {
            return;
        }

        foreach (var menu in _menuStack)
        {
            _state.SetTextBuffer($"{menu.MenuSetKey}##open", string.Empty);
            _state.SetTextBuffer($"{menu.SubmenuSetKey}##open", string.Empty);
        }
    }

    private void UpdateCurrentMenuMaxWidth(float width)
    {
        if (_menuStack.Count == 0)
        {
            return;
        }

        var menu = _menuStack.Pop();
        if (width > menu.MaxContentWidth)
        {
            menu = menu with { MaxContentWidth = width };
        }
        _menuStack.Push(menu);
    }
}

