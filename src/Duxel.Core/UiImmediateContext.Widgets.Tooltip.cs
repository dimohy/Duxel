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
        _tooltipOrigin = start;
        _tooltipMaxRight = start.X + _tooltipPadding;

        PushPopup();
        _builder.Split(2);
        _builder.SetCurrentChannel(1); // content channel (rendered on top of background)

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

        var layout = _layouts.Pop();

        // Calculate tooltip content bounds
        var contentEndY = layout.Cursor.Y - ItemSpacingY;
        var tooltipWidth = MathF.Max(_tooltipMaxRight - _tooltipOrigin.X + _tooltipPadding, _tooltipPadding * 2f);
        var tooltipHeight = MathF.Max(contentEndY - _tooltipOrigin.Y + _tooltipPadding, _tooltipPadding * 2f);
        var rect = ClampRectToDisplay(new UiRect(_tooltipOrigin.X, _tooltipOrigin.Y, tooltipWidth, tooltipHeight));

        // Draw background behind content
        _builder.SetCurrentChannel(0);
        PushClipRect(IntersectRect(_clipRect, rect), false);
        AddRectFilled(rect, _theme.PopupBg, _whiteTexture);
        PopClipRect();

        _builder.Merge();
        PopPopup();
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

        PushPopup();
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
        PopPopup();
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

