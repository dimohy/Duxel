namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginTabBar(string id)
    {
        id ??= "TabBar";

        _currentTabBarId = id;
        _currentTabBarActiveKey = $"{id}##active";

        var frameHeight = GetFrameHeight() + 2f;
        var width = _hasWindowRect ? MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f)) : InputWidth;
        var cursor = _layouts.Peek().Cursor;
        var rect = new UiRect(cursor.X, cursor.Y, width, frameHeight);
        AddRectFilled(rect, _theme.MenuBarBg, _whiteTexture);

        BeginRow();
        return true;
    }

    public bool BeginTabBar(string id, UiTabBarFlags flags)
    {
        _ = flags;
        return BeginTabBar(id);
    }

    public bool BeginTabItem(string label)
    {
        label ??= "Tab";
        if (_currentTabBarId is null || _currentTabBarActiveKey is null)
        {
            return false;
        }

        var activeLabel = _state.GetTextBuffer(_currentTabBarActiveKey, label);
        var isActive = string.Equals(activeLabel, label, StringComparison.Ordinal);

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + (ButtonPaddingX * 2f), GetFrameHeight() + 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior($"{_currentTabBarId}##{label}", rect, out var hovered, out var held);
        if (pressed)
        {
            _state.SetTextBuffer(_currentTabBarActiveKey, label);
            isActive = true;
        }

        var bg = isActive ? _theme.TabActive : held ? _theme.ButtonActive : hovered ? _theme.TabHovered : _theme.Tab;
        AddRectFilled(rect, bg, _whiteTexture);

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (rect.Height - textSize.Y) * 0.5f);
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

        return isActive;
    }

    public bool BeginTabItem(string label, UiTabItemFlags flags)
    {
        _ = flags;
        return BeginTabItem(label);
    }

    public void SetTabItemClosed(string label)
    {
        if (_currentTabBarActiveKey is null || string.IsNullOrEmpty(label))
        {
            return;
        }

        var activeLabel = _state.GetTextBuffer(_currentTabBarActiveKey, string.Empty);
        if (string.Equals(activeLabel, label, StringComparison.Ordinal))
        {
            _state.SetTextBuffer(_currentTabBarActiveKey, string.Empty);
        }
    }

    public bool TabItemButton(string label)
    {
        label ??= "Tab";

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + (ButtonPaddingX * 2f), GetFrameHeight() + 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior($"{_currentTabBarId ?? "tab"}##btn{label}", rect, out var hovered, out var held);
        var bg = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, bg, _whiteTexture);

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (rect.Height - textSize.Y) * 0.5f);
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

        return pressed;
    }

    public void EndTabItem()
    {
    }

    public void EndTabBar()
    {
        EndRow();
        _currentTabBarId = null;
        _currentTabBarActiveKey = null;
    }
}
