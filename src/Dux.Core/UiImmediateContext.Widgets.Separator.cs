namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public void SeparatorText(string text)
    {
        text ??= string.Empty;

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
        var frameHeight = MathF.Max(_lineHeight, textSize.Y);
        var cursor = AdvanceCursor(new UiVector2(0f, frameHeight));

        var startX = _hasWindowRect ? _windowRect.X + WindowPadding : cursor.X;
        var endX = _hasWindowRect ? _windowRect.X + _windowRect.Width - WindowPadding : cursor.X + MathF.Max(textSize.X + 40f, InputWidth);
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
            _builder.AddText(
                _fontAtlas,
                text,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }
}
