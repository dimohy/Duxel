using System.Diagnostics;
using System.Globalization;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool Button(string label)
    {
        return Button(label, default);
    }

    public bool Button(string label, UiVector2 size)
    {
        label ??= "Button";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var width = size.X > 0f ? size.X : textSize.X + ButtonPaddingX * 2f;
        var height = size.Y > 0f ? size.Y : textSize.Y + ButtonPaddingY * 2f;
        var cursor = AdvanceCursor(new UiVector2(width, height));
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        var pressed = ButtonBehavior(label, rect, out var hovered, out var held);
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;

        AddRectFilled(rect, color, _whiteTexture);

        var textPos = new UiVector2(
            rect.X + (rect.Width - textSize.X) * 0.5f,
            rect.Y + (rect.Height - textSize.Y) * 0.5f
        );
        if (!string.IsNullOrEmpty(displayLabel))
        {
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool SmallButton(string label)
    {
        label ??= "Button";

        const float smallPadX = 4f;
        const float smallPadY = 2f;
        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + smallPadX * 2f, textSize.Y + smallPadY * 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(label, rect, out var hovered, out var held);
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, color, _whiteTexture);

        var textPos = new UiVector2(
            rect.X + (rect.Width - textSize.X) * 0.5f,
            rect.Y + (rect.Height - textSize.Y) * 0.5f
        );
        if (!string.IsNullOrEmpty(displayLabel))
        {
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool InvisibleButton(string id, UiVector2 size)
    {
        id ??= "InvisibleButton";

        var width = MathF.Max(1f, size.X);
        var height = MathF.Max(1f, size.Y);
        var cursor = AdvanceCursor(new UiVector2(width, height));
        var rect = new UiRect(cursor.X, cursor.Y, width, height);
        return ButtonBehavior(id, rect, out _, out _);
    }

    public bool ArrowButton(string id, UiDir dir)
    {
        id ??= "Arrow";

        var size = GetFrameHeight();
        var cursor = AdvanceCursor(new UiVector2(size, size));
        var rect = new UiRect(cursor.X, cursor.Y, size, size);

        var pressed = ButtonBehavior(id, rect, out var hovered, out var held);
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, color, _whiteTexture);

        var arrow = dir switch
        {
            UiDir.Left => "<",
            UiDir.Right => ">",
            UiDir.Up => "^",
            UiDir.Down => "v",
            _ => ">",
        };
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, arrow, _textSettings, _lineHeight);
        var textPos = new UiVector2(
            rect.X + (rect.Width - textSize.X) * 0.5f,
            rect.Y + (rect.Height - textSize.Y) * 0.5f
        );
        _builder.AddText(
            _fontAtlas,
            arrow,
            textPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        return pressed;
    }

    public void Text(string text)
    {
        RenderText(text, _theme.Text);
    }

    public void TextV(string format, params object[] args)
    {
        Text(FormatInvariant(format, args));
    }

    public void TextColored(UiColor color, string text)
    {
        RenderText(text, color);
    }

    public void TextColoredV(UiColor color, string format, params object[] args)
    {
        TextColored(color, FormatInvariant(format, args));
    }

    public void TextDisabled(string text)
    {
        RenderText(text, _theme.TextDisabled);
    }

    public void TextDisabledV(string format, params object[] args)
    {
        TextDisabled(FormatInvariant(format, args));
    }

    public void TextWrapped(string text)
    {
        text ??= string.Empty;

        var current = _layouts.Peek();
        var availableWidth = GetWrapWidth(current.Cursor);
        if (availableWidth <= 0f && _hasWindowRect)
        {
            availableWidth = MathF.Max(0f, (_windowRect.X + _windowRect.Width - WindowPadding) - current.Cursor.X);
        }

        var wrapped = availableWidth > 0f ? WrapText(text, availableWidth) : text;
        RenderText(wrapped, _theme.Text);
    }

    public void TextWrappedV(string format, params object[] args)
    {
        TextWrapped(FormatInvariant(format, args));
    }

    public void TextUnformatted(string text)
    {
        RenderText(text, _theme.Text);
    }

    public bool TextLink(string label)
    {
        label ??= "Link";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X, textSize.Y);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(label, rect, out var hovered, out _);
        var color = hovered ? _theme.SliderGrabActive : _theme.SliderGrab;
        if (!string.IsNullOrEmpty(displayLabel))
        {
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                cursor,
                color,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool TextLinkOpenURL(string label, string url)
    {
        var pressed = TextLink(label);
        if (pressed && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore failures to open URLs.
            }
        }

        return pressed;
    }

    public void LabelText(string label, string text)
    {
        label ??= string.Empty;
        text ??= string.Empty;
        var displayLabel = GetDisplayLabel(label);

        var labelText = string.IsNullOrEmpty(displayLabel) ? text : FormattableString.Invariant($"{displayLabel}: ");
        var valueText = string.IsNullOrEmpty(displayLabel) ? string.Empty : text;

        var labelSize = UiTextBuilder.MeasureText(_fontAtlas, labelText, _textSettings, _lineHeight);
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, valueText, _textSettings, _lineHeight);
        var totalSize = new UiVector2(labelSize.X + valueSize.X, MathF.Max(labelSize.Y, valueSize.Y));
        var cursor = AdvanceCursor(totalSize);

        if (!string.IsNullOrEmpty(labelText))
        {
            _builder.AddText(
                _fontAtlas,
                labelText,
                cursor,
                _theme.TextDisabled,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        if (!string.IsNullOrEmpty(valueText))
        {
            var valuePos = new UiVector2(cursor.X + labelSize.X, cursor.Y);
            _builder.AddText(
                _fontAtlas,
                valueText,
                valuePos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }

    public void LabelTextV(string label, string format, params object[] args)
    {
        LabelText(label, FormatInvariant(format, args));
    }

    public void Bullet()
    {
        var size = new UiVector2(_lineHeight, _lineHeight);
        var cursor = AdvanceCursor(size);
        var radius = MathF.Max(2f, _lineHeight * 0.15f);
        var center = new UiVector2(cursor.X + radius + 2f, cursor.Y + (_lineHeight * 0.5f));
        AddCircleFilled(center, radius, _theme.Text, _whiteTexture, 12);
    }

    public void BulletText(string text)
    {
        Bullet();
        SameLine();
        Text(text);
    }

    public void BulletTextV(string format, params object[] args)
    {
        Bullet();
        SameLine();
        Text(FormatInvariant(format, args));
    }

    public void Value(string prefix, bool value)
    {
        var display = value ? "true" : "false";
        Text(FormatValue(prefix, display));
    }

    public void Value(string prefix, int value)
    {
        Text(FormatValue(prefix, value.ToString(CultureInfo.InvariantCulture)));
    }

    public void Value(string prefix, uint value)
    {
        Text(FormatValue(prefix, value.ToString(CultureInfo.InvariantCulture)));
    }

    public void Value(string prefix, float value, string? format = null)
    {
        format = string.IsNullOrWhiteSpace(format) ? "0.000" : format;
        Text(FormatValue(prefix, value.ToString(format, CultureInfo.InvariantCulture)));
    }

    private void RenderText(string? text, UiColor color)
    {
        text ??= string.Empty;

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
        var cursor = AdvanceCursor(new UiVector2(textSize.X, textSize.Y));
        if (_tableActive && _tableColumns > 0)
        {
            var columnWidth = GetTableColumnWidth(_tableColumn);
            var align = _tableColumnAlign.Length > _tableColumn ? _tableColumnAlign[_tableColumn] : 0f;
            var padding = ButtonPaddingX;
            var available = MathF.Max(0f, columnWidth - padding * 2f);
            var alignedX = cursor.X + padding + MathF.Max(0f, available - textSize.X) * align;
            cursor = new UiVector2(alignedX, cursor.Y);
        }

        var clipRect = ResolveItemClipRect();
        var clipped = IntersectRect(clipRect, new UiRect(cursor.X, cursor.Y, textSize.X, textSize.Y));
        if (clipped.Width <= 0f || clipped.Height <= 0f)
        {
            return;
        }

        _builder.AddText(
            _fontAtlas,
            text,
            cursor,
            color,
            _fontTexture,
            clipRect,
            _textSettings,
            _lineHeight
        );
    }

    private static string FormatInvariant(string? format, object[] args)
    {
        var safeFormat = format ?? string.Empty;
        if (args.Length == 0)
        {
            return safeFormat;
        }

        return string.Format(CultureInfo.InvariantCulture, safeFormat, args);
    }

    private static string FormatValue(string? prefix, string value)
    {
        var label = prefix ?? string.Empty;
        return string.IsNullOrWhiteSpace(label) ? value : string.Format(CultureInfo.InvariantCulture, "{0}: {1}", label, value);
    }

    private string WrapText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return text;
        }

        var textSpan = text.AsSpan();
        var spaceWidth = MeasureTextWidth(" ", 1);
        var builder = new System.Text.StringBuilder(text.Length + 16);
        var lineWidth = 0f;
        var first = true;

        var index = 0;
        while (index < textSpan.Length)
        {
            while (index < textSpan.Length && textSpan[index] == ' ')
            {
                index++;
            }

            if (index >= textSpan.Length)
            {
                break;
            }

            var wordStart = index;
            while (index < textSpan.Length && textSpan[index] != ' ')
            {
                index++;
            }

            var wordLength = index - wordStart;
            var wordWidth = MeasureTextWidthSpan(textSpan.Slice(wordStart, wordLength));
            if (!first && lineWidth + spaceWidth + wordWidth > maxWidth)
            {
                builder.Append('\n');
                lineWidth = 0f;
            }

            if (lineWidth > 0f)
            {
                builder.Append(' ');
                lineWidth += spaceWidth;
            }

            builder.Append(text, wordStart, wordLength);
            lineWidth += wordWidth;
            first = false;
        }

        return builder.ToString();
    }

    public bool Checkbox(string label, bool defaultValue = false)
    {
        label ??= "Checkbox";

        var id = ResolveId(label);
        var value = _state.GetBool(id, defaultValue);
        _ = Checkbox(label, ref value);
        _state.SetBool(id, value);
        return value;
    }

    public bool Checkbox(string label, ref bool value)
    {
        label ??= "Checkbox";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var glyphHeight = (_fontAtlas.Ascent - _fontAtlas.Descent) * _textSettings.Scale;
        var frameHeight = GetFrameHeight();
        var checkboxSize = frameHeight;
        var height = MathF.Max(checkboxSize, glyphHeight);
        var totalSize = new UiVector2(checkboxSize + (string.IsNullOrEmpty(displayLabel) ? 0f : CheckboxSpacing + textSize.X), height);
        var cursor = AdvanceCursor(totalSize);

        var totalRect = new UiRect(cursor.X, cursor.Y, totalSize.X, totalSize.Y);
        var textTop = cursor.Y + (height - glyphHeight) * 0.5f;
        var boxY = cursor.Y + (height - checkboxSize) * 0.5f;
        if (_textSettings.PixelSnap)
        {
            boxY = MathF.Round(boxY);
        }
        var boxRect = new UiRect(cursor.X, boxY, checkboxSize, checkboxSize);
        var pressed = ButtonBehavior(label, totalRect, out var hovered, out _);
        if (pressed)
        {
            value = !value;
        }

        var boxColor = hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(boxRect, boxColor, _whiteTexture);

        if (value)
        {
            var inset = 4f;
            var checkRect = new UiRect(boxRect.X + inset, boxRect.Y + inset, boxRect.Width - inset * 2f, boxRect.Height - inset * 2f);
            AddRectFilled(checkRect, _theme.CheckMark, _whiteTexture);
        }

        if (!string.IsNullOrEmpty(displayLabel))
        {
            var textPos = new UiVector2(cursor.X + checkboxSize + CheckboxSpacing, textTop);
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool CheckboxFlags(string label, ref int flags, int flagsValue)
    {
        var value = (flags & flagsValue) != 0;
        var pressed = Checkbox(label, ref value);
        if (pressed)
        {
            if (value)
            {
                flags |= flagsValue;
            }
            else
            {
                flags &= ~flagsValue;
            }
        }

        return pressed;
    }

    public bool RadioButton(string label, bool active)
    {
        label ??= "Radio";

        var displayLabel = GetDisplayLabel(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, displayLabel, _textSettings, _lineHeight);
        var glyphHeight = (_fontAtlas.Ascent - _fontAtlas.Descent) * _textSettings.Scale;
        var frameHeight = GetFrameHeight();
        var radioSize = frameHeight;
        var height = MathF.Max(radioSize, glyphHeight);
        var totalSize = new UiVector2(radioSize + (string.IsNullOrEmpty(displayLabel) ? 0f : CheckboxSpacing + textSize.X), height);
        var cursor = AdvanceCursor(totalSize);

        var totalRect = new UiRect(cursor.X, cursor.Y, totalSize.X, totalSize.Y);
        var textTop = cursor.Y + (height - glyphHeight) * 0.5f;
        var center = new UiVector2(cursor.X + (radioSize * 0.5f), cursor.Y + (height * 0.5f));

        var pressed = ButtonBehavior(label, totalRect, out var hovered, out _);

        var outerColor = hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddCircleFilled(center, radioSize * 0.5f, outerColor, _whiteTexture, 16);

        if (active)
        {
            AddCircleFilled(center, radioSize * 0.25f, _theme.CheckMark, _whiteTexture, 12);
        }

        if (!string.IsNullOrEmpty(displayLabel))
        {
            var textPos = new UiVector2(cursor.X + radioSize + CheckboxSpacing, textTop);
            _builder.AddText(
                _fontAtlas,
                displayLabel,
                textPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        return pressed;
    }

    public bool RadioButton(string label, ref int value, int buttonValue)
    {
        var pressed = RadioButton(label, value == buttonValue);
        if (pressed)
        {
            value = buttonValue;
        }

        return pressed;
    }

    public void ProgressBar(float fraction, UiVector2 size, string? overlay = null)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);

        var frameHeight = GetFrameHeight();
        var width = size.X > 0f ? size.X : 220f;
        var height = size.Y > 0f ? size.Y : frameHeight;

        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        AddRectFilled(rect, _theme.FrameBg, _whiteTexture);

        var fillWidth = rect.Width * fraction;
        if (fillWidth > 0f)
        {
            var fillRect = new UiRect(rect.X, rect.Y, fillWidth, rect.Height);
            AddRectFilled(fillRect, _theme.SliderGrab, _whiteTexture);
        }

        var text = overlay ?? FormattableString.Invariant($"{fraction * 100f:0}%");
        if (!string.IsNullOrWhiteSpace(text))
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, text, _textSettings, _lineHeight);
            var textPos = new UiVector2(
                rect.X + (rect.Width - textSize.X) * 0.5f,
                rect.Y + (rect.Height - textSize.Y) * 0.5f
            );
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

    public void PlotLines(string label, float[] values, int valuesCount = -1, int valuesOffset = 0, string? overlayText = null, float scaleMin = float.NaN, float scaleMax = float.NaN, UiVector2 size = default)
    {
        PlotInternal(label, values, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax, size, false);
    }

    public void PlotHistogram(string label, float[] values, int valuesCount = -1, int valuesOffset = 0, string? overlayText = null, float scaleMin = float.NaN, float scaleMax = float.NaN, UiVector2 size = default)
    {
        PlotInternal(label, values, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax, size, true);
    }

    private void PlotInternal(string label, float[] values, int valuesCount, int valuesOffset, string? overlayText, float scaleMin, float scaleMax, UiVector2 size, bool histogram)
    {
        label ??= "Plot";
        ArgumentNullException.ThrowIfNull(values);

        var count = valuesCount > 0 ? Math.Min(valuesCount, values.Length) : values.Length;
        if (count <= 0)
        {
            return;
        }

        var width = size.X > 0f ? size.X : ResolveItemWidth(InputWidth);
        var height = size.Y > 0f ? size.Y : GetFrameHeight() * 2f;
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        AddRectFilled(rect, _theme.FrameBg, _whiteTexture);

        var min = float.IsNaN(scaleMin) ? float.MaxValue : scaleMin;
        var max = float.IsNaN(scaleMax) ? float.MinValue : scaleMax;
        if (float.IsNaN(scaleMin) || float.IsNaN(scaleMax))
        {
            for (var i = 0; i < count; i++)
            {
                var v = values[(valuesOffset + i) % values.Length];
                min = MathF.Min(min, v);
                max = MathF.Max(max, v);
            }
        }

        if (MathF.Abs(max - min) < float.Epsilon)
        {
            max = min + 1f;
        }

        var step = histogram ? rect.Width / count : rect.Width / Math.Max(1, count - 1);
        var prevX = rect.X;
        var prevY = rect.Y + rect.Height;

        for (var i = 0; i < count; i++)
        {
            var value = values[(valuesOffset + i) % values.Length];
            var t = Math.Clamp((value - min) / (max - min), 0f, 1f);
            var y = rect.Y + rect.Height - (t * rect.Height);
            var x = rect.X + (i * step);

            if (histogram)
            {
                var barRect = new UiRect(x, y, MathF.Max(1f, step - 1f), rect.Y + rect.Height - y);
                AddRectFilled(barRect, _theme.PlotHistogram, _whiteTexture);
            }
            else if (i > 0)
            {
                var lineMinY = MathF.Min(prevY, y);
                var lineHeight = MathF.Max(1f, MathF.Abs(prevY - y));
                var lineRect = new UiRect(prevX, lineMinY, MathF.Max(1f, step), lineHeight);
                AddRectFilled(lineRect, _theme.PlotLines, _whiteTexture);
            }

            prevX = x;
            prevY = y;
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
            var textPos = new UiVector2(rect.X + 4f, rect.Y + 2f);
            _builder.AddText(
                _fontAtlas,
                label,
                textPos,
                _theme.TextDisabled,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        if (!string.IsNullOrWhiteSpace(overlayText))
        {
            var overlaySize = UiTextBuilder.MeasureText(_fontAtlas, overlayText, _textSettings, _lineHeight);
            var overlayPos = new UiVector2(rect.X + (rect.Width - overlaySize.X) * 0.5f, rect.Y + (rect.Height - overlaySize.Y) * 0.5f);
            _builder.AddText(
                _fontAtlas,
                overlayText,
                overlayPos,
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }
    }
}

