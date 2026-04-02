namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public void SeparatorText(string text)
    {
        text ??= string.Empty;

        var textSize = MeasureTextInternal(text, _textSettings, _lineHeight);
        var frameHeight = MathF.Max(_lineHeight, textSize.Y);
        var cursor = AdvanceCursor(new UiVector2(0f, frameHeight));

        var startX = _childStack.Count > 0
            ? _childStack.Peek().Rect.X + 2f
            : _columnsActive && _columnsCount > 0
            ? GetColumnsColumnX(_columnsIndex)
            : _hasWindowRect ? _windowRect.X + WindowPadding : cursor.X;
        var endX = _childStack.Count > 0
            ? _childStack.Peek().Rect.X + _childStack.Peek().Rect.Width - 2f
            : _columnsActive && _columnsCount > 0
            ? GetColumnsColumnX(_columnsIndex) + GetColumnsColumnWidth(_columnsIndex)
            : _hasWindowRect ? _windowRect.X + _windowRect.Width - WindowPadding : cursor.X + MathF.Max(textSize.X + 40f, InputWidth);
        var centerY = cursor.Y + (frameHeight * 0.5f);

        var leftEnd = startX + 6f;
        var rightStart = leftEnd + textSize.X + 12f;

        var leftRect = new UiRect(startX, centerY, MathF.Max(0f, leftEnd - startX), 1f);
        var rightRect = new UiRect(rightStart, centerY, MathF.Max(0f, endX - rightStart), 1f);
        AddRectFilled(leftRect, _theme.Separator, _whiteTexture);
        AddRectFilled(rightRect, _theme.Separator, _whiteTexture);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var textPos = new UiVector2(leftEnd + 6f, cursor.Y + (frameHeight - textSize.Y) * 0.5f);
            AddTextInternal(_builder,

                text,
                textPos,
                _theme.SeparatorLabelText,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }
}

