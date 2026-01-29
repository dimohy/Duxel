namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginTooltip()
    {
        if (_tooltipActive)
        {
            return false;
        }

        _tooltipActive = true;
        _tooltipPadding = 6f;
        var start = new UiVector2(_mousePosition.X + 16f, _mousePosition.Y + 16f);
        var cursor = new UiVector2(start.X + _tooltipPadding, start.Y + _tooltipPadding);
        _layouts.Push(new UiLayoutState(cursor, false, 0f, cursor.X));
        return true;
    }

    public void EndTooltip()
    {
        if (!_tooltipActive)
        {
            return;
        }

        _layouts.Pop();
        _tooltipActive = false;
    }

    public void SetTooltip(string text)
    {
        text ??= string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
        var padding = 6f;
        var rect = ClampRectToDisplay(new UiRect(
            _mousePosition.X + 16f,
            _mousePosition.Y + 16f,
            textSize.X + (padding * 2f),
            textSize.Y + (padding * 2f)
        ));

        PushOverlay();
        PushClipRect(IntersectRect(_clipRect, rect), false);
        AddRectFilled(rect, _theme.PopupBg, _whiteTexture);

        var textPos = new UiVector2(rect.X + padding, rect.Y + padding);
        _builder.AddText(
            _fontAtlas,
            text,
            textPos,
            _theme.Text,
            _fontTexture,
            rect,
            _textSettings,
            _lineHeight
        );
        PopClipRect();
        PopOverlay();
    }

    public void SetTooltipV(string format, params object[] args)
    {
        SetTooltip(string.Format(System.Globalization.CultureInfo.InvariantCulture, format ?? string.Empty, args));
    }

    public bool BeginItemTooltip()
    {
        return IsItemHovered() && BeginTooltip();
    }

    public void SetItemTooltip(string text)
    {
        if (IsItemHovered())
        {
            SetTooltip(text);
        }
    }

    public void SetItemTooltipV(string format, params object[] args)
    {
        if (IsItemHovered())
        {
            SetTooltipV(format, args);
        }
    }
}

