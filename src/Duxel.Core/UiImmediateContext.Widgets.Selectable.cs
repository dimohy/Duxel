namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool Selectable(string label, ref bool selected)
    {
        var pressed = Selectable(label, selected);
        if (pressed)
        {
            selected = !selected;
            _lastItemToggledSelection = true;
        }

        return pressed;
    }

    public bool Selectable(string label, ref bool selected, UiVector2 size)
    {
        var pressed = Selectable(label, selected, size);
        if (pressed)
        {
            selected = !selected;
            _lastItemToggledSelection = true;
        }

        return pressed;
    }

    public bool Selectable(string label, ref bool selected, UiSelectableFlags flags, UiVector2 size)
    {
        _ = flags;
        return Selectable(label, ref selected, size);
    }

    public bool Selectable(string label, bool selected)
    {
        label ??= "Selectable";

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var width = textSize.X + (ButtonPaddingX * 2f);
        var size = new UiVector2(width, height);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(label, rect, out var hovered, out _);

        if (selected)
        {
            AddRectFilled(rect, _theme.HeaderActive, _whiteTexture);
        }
        else if (hovered)
        {
            AddRectFilled(rect, _theme.HeaderHovered, _whiteTexture);
        }

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (height - textSize.Y) * 0.5f);
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

    public bool Selectable(string label, bool selected, UiVector2 size)
    {
        label ??= "Selectable";

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = size.Y > 0f ? size.Y : MathF.Max(textSize.Y, frameHeight);
        var width = size.X > 0f ? size.X : textSize.X + (ButtonPaddingX * 2f);
        var finalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(finalSize);
        var rect = new UiRect(cursor.X, cursor.Y, finalSize.X, finalSize.Y);

        var pressed = ButtonBehavior(label, rect, out var hovered, out _);

        if (selected)
        {
            AddRectFilled(rect, _theme.HeaderActive, _whiteTexture);
        }
        else if (hovered)
        {
            AddRectFilled(rect, _theme.HeaderHovered, _whiteTexture);
        }

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (height - textSize.Y) * 0.5f);
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

    public bool Selectable(string label, bool selected, UiSelectableFlags flags, UiVector2 size)
    {
        _ = flags;
        return Selectable(label, selected, size);
    }
}

