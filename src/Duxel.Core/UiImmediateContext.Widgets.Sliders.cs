namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool SliderFloat(string label, ref float value, float min, float max)
    {
        return SliderFloat(label, ref value, min, max, 0f, "0.00");
    }

    public bool SliderInt(string label, ref int value, int min, int max)
    {
        var floatValue = (float)value;
        var changed = SliderFloat(label, ref floatValue, min, max, 1f, "0");
        var next = Math.Clamp((int)MathF.Round(floatValue), min, max);
        if (next != value)
        {
            value = next;
            changed = true;
        }

        return changed;
    }

    public bool SliderFloat2(string label, ref float x, ref float y, float min, float max)
    {
        label ??= "SliderFloat2";

        BeginRow();
        var changedX = SliderFloat($"{label} X", ref x, min, max);
        var changedY = SliderFloat($"{label} Y", ref y, min, max);
        EndRow();

        return changedX || changedY;
    }

    public bool SliderFloat3(string label, ref float x, ref float y, ref float z, float min, float max)
    {
        label ??= "SliderFloat3";

        BeginRow();
        var changedX = SliderFloat($"{label} X", ref x, min, max);
        var changedY = SliderFloat($"{label} Y", ref y, min, max);
        var changedZ = SliderFloat($"{label} Z", ref z, min, max);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool SliderFloat4(string label, ref float x, ref float y, ref float z, ref float w, float min, float max)
    {
        label ??= "SliderFloat4";

        BeginRow();
        var changedX = SliderFloat($"{label} X", ref x, min, max);
        var changedY = SliderFloat($"{label} Y", ref y, min, max);
        var changedZ = SliderFloat($"{label} Z", ref z, min, max);
        var changedW = SliderFloat($"{label} W", ref w, min, max);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool SliderScalar(string label, ref float value, float min, float max, float step = 0f, string format = "0.00")
    {
        return SliderFloat(label, ref value, min, max, step, format);
    }

    public bool SliderScalar(string label, ref int value, int min, int max)
    {
        return SliderInt(label, ref value, min, max);
    }

    public bool SliderScalar(string label, ref double value, double min, double max, double step = 0d, string format = "0.00")
    {
        var floatValue = (float)value;
        var changed = SliderFloat(label, ref floatValue, (float)min, (float)max, (float)step, format);
        var next = Math.Clamp((double)floatValue, min, max);
        if (!double.IsNaN(next) && !double.IsInfinity(next) && Math.Abs(next - value) > double.Epsilon)
        {
            value = next;
            changed = true;
        }

        return changed;
    }

    public bool SliderInt2(string label, ref int x, ref int y, int min, int max)
    {
        label ??= "SliderInt2";

        BeginRow();
        var changedX = SliderInt($"{label} X", ref x, min, max);
        var changedY = SliderInt($"{label} Y", ref y, min, max);
        EndRow();

        return changedX || changedY;
    }

    public bool SliderInt3(string label, ref int x, ref int y, ref int z, int min, int max)
    {
        label ??= "SliderInt3";

        BeginRow();
        var changedX = SliderInt($"{label} X", ref x, min, max);
        var changedY = SliderInt($"{label} Y", ref y, min, max);
        var changedZ = SliderInt($"{label} Z", ref z, min, max);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool SliderInt4(string label, ref int x, ref int y, ref int z, ref int w, int min, int max)
    {
        label ??= "SliderInt4";

        BeginRow();
        var changedX = SliderInt($"{label} X", ref x, min, max);
        var changedY = SliderInt($"{label} Y", ref y, min, max);
        var changedZ = SliderInt($"{label} Z", ref z, min, max);
        var changedW = SliderInt($"{label} W", ref w, min, max);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool SliderScalarN(string label, float[] values, float min, float max, float step = 0f, string format = "0.00")
    {
        label ??= "SliderScalarN";
        format ??= "0.00";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= SliderFloat($"{label} {i}", ref values[i], min, max, step, format);
        }
        EndRow();

        return changed;
    }

    public bool SliderScalarN(string label, int[] values, int min, int max)
    {
        label ??= "SliderScalarN";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= SliderInt($"{label} {i}", ref values[i], min, max);
        }
        EndRow();

        return changed;
    }

    public bool SliderScalarN(string label, double[] values, double min, double max, double step = 0d, string format = "0.00")
    {
        label ??= "SliderScalarN";
        format ??= "0.00";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            changed |= SliderScalar($"{label} {i}", ref value, min, max, step, format);
            values[i] = value;
        }
        EndRow();

        return changed;
    }

    public bool SliderAngle(string label, ref float radians, float minDegrees = -360f, float maxDegrees = 360f, string format = "0.0")
    {
        var degrees = radians * (180f / MathF.PI);
        var changed = SliderFloat(label, ref degrees, minDegrees, maxDegrees, 0f, format);
        if (changed)
        {
            radians = degrees * (MathF.PI / 180f);
        }

        return changed;
    }

    public bool VSliderFloat(string label, UiVector2 size, ref float value, float min, float max, string format = "0.00")
    {
        label ??= "VSlider";
        format ??= "0.00";
        if (max <= min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be greater than min.");
        }

        var frame = GetFrameHeight();
        var width = size.X > 0f ? size.X : frame;
        var height = size.Y > 0f ? size.Y : frame * 4f;
        var cursor = AdvanceCursor(new UiVector2(width, height));
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        var hovered = ItemHoverable(label, rect);
        if (hovered && _leftMousePressed)
        {
            _state.ActiveId = label;
        }

        var active = _state.ActiveId == label;
        if (!_leftMouseDown && active)
        {
            _state.ActiveId = null;
            active = false;
        }

        var normalized = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var changed = false;
        if (active && _leftMouseDown)
        {
            var t = (rect.Y + rect.Height - Math.Clamp(_mousePosition.Y, rect.Y, rect.Y + rect.Height)) / rect.Height;
            var newValue = min + t * (max - min);
            if (MathF.Abs(newValue - value) > float.Epsilon)
            {
                value = newValue;
                normalized = t;
                changed = true;
            }
        }

        var backColor = hovered || active ? _theme.FrameBgHovered : _theme.FrameBg;
        var barWidth = MathF.Max(6f, rect.Width * 0.35f);
        var barX = rect.X + (rect.Width - barWidth) * 0.5f;
        var barRect = new UiRect(barX, rect.Y, barWidth, rect.Height);
        AddRectFilled(barRect, backColor, _whiteTexture);

        var fillHeight = barRect.Height * normalized;
        var fillRect = new UiRect(barRect.X, barRect.Y + (barRect.Height - fillHeight), barRect.Width, fillHeight);
        AddRectFilled(fillRect, _theme.SliderGrabActive, _whiteTexture);

        var grabHeight = MathF.Max(12f, barRect.Width + 6f);
        var grabY = barRect.Y + (barRect.Height - fillHeight) - grabHeight * 0.5f;
        var grabRect = new UiRect(barRect.X - 2f, Math.Clamp(grabY, barRect.Y, barRect.Y + barRect.Height - grabHeight), barRect.Width + 4f, grabHeight);
        AddRectFilled(grabRect, active ? _theme.SliderGrabActive : _theme.SliderGrab, _whiteTexture);

        var valueText = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, valueText, _textSettings, _lineHeight);
        var valuePos = new UiVector2(rect.X + (rect.Width - valueSize.X) * 0.5f, rect.Y + (rect.Height - valueSize.Y) * 0.5f);
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

        return changed;
    }

    public bool VSliderScalar(string label, UiVector2 size, ref float value, float min, float max, string format = "0.###")
    {
        return VSliderFloat(label, size, ref value, min, max, format);
    }

    public bool VSliderInt(string label, UiVector2 size, ref int value, int min, int max)
    {
        var floatValue = (float)value;
        var changed = VSliderFloat(label, size, ref floatValue, min, max, "0");
        var next = Math.Clamp((int)MathF.Round(floatValue), min, max);
        if (next != value)
        {
            value = next;
            changed = true;
        }

        return changed;
    }

    public bool VSliderScalar(string label, UiVector2 size, ref int value, int min, int max)
    {
        return VSliderInt(label, size, ref value, min, max);
    }

    public bool SliderFloat(string label, ref float value, float min, float max, float step, string format)
    {
        label ??= "Slider";
        format ??= "0.00";
        if (max <= min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be greater than min.");
        }

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var sliderWidth = ResolveItemWidth(SliderWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + sliderWidth, height);
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

        var sliderX = cursor.X + textSize.X + ItemSpacingX;
        var sliderRect = new UiRect(sliderX, cursor.Y + (height - frameHeight) * 0.5f, sliderWidth, frameHeight);
        var hovered = ItemHoverable(label, sliderRect);

        if (hovered && _leftMousePressed)
        {
            _state.ActiveId = label;
        }

        var active = _state.ActiveId == label;
        if (!_leftMouseDown && active)
        {
            _state.ActiveId = null;
            active = false;
        }

        var normalized = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var changed = false;
        if (active && _leftMouseDown)
        {
            var t = (Math.Clamp(_mousePosition.X, sliderRect.X, sliderRect.X + sliderRect.Width) - sliderRect.X) / sliderRect.Width;
            var newValue = min + t * (max - min);
            if (MathF.Abs(newValue - value) > float.Epsilon)
            {
                value = newValue;
                normalized = t;
                changed = true;
            }
        }

        if (active && step > 0f)
        {
            foreach (var keyEvent in _keyEvents)
            {
                if (!keyEvent.IsDown)
                {
                    continue;
                }

                if (IsRepeatableKey(keyEvent.Key) && !_state.HasRepeatTime(keyEvent.Key))
                {
                    _state.SetRepeatTime(keyEvent.Key, _state.TimeSeconds + _keyRepeatDelaySeconds);
                }

                if (keyEvent.Key == UiKey.LeftArrow)
                {
                    value = MathF.Max(min, value - step);
                    changed = true;
                }
                else if (keyEvent.Key == UiKey.RightArrow)
                {
                    value = MathF.Min(max, value + step);
                    changed = true;
                }
                else if (keyEvent.Key == UiKey.Home)
                {
                    value = min;
                    changed = true;
                }
                else if (keyEvent.Key == UiKey.End)
                {
                    value = max;
                    changed = true;
                }
            }

            normalized = Math.Clamp((value - min) / (max - min), 0f, 1f);
        }

        var backColor = hovered || active ? _theme.FrameBgHovered : _theme.FrameBg;
        var barHeight = MathF.Max(4f, sliderRect.Height * 0.35f);
        var barY = sliderRect.Y + (sliderRect.Height - barHeight) * 0.5f;
        var barRect = new UiRect(sliderRect.X, barY, sliderRect.Width, barHeight);
        AddRectFilled(barRect, backColor, _whiteTexture);

        var fillRect = new UiRect(barRect.X, barRect.Y, barRect.Width * normalized, barRect.Height);
        AddRectFilled(fillRect, _theme.SliderGrabActive, _whiteTexture);

        var grabWidth = MathF.Max(12f, barHeight * 1.6f);
        var grabX = barRect.X + barRect.Width * normalized - grabWidth * 0.5f;
        var grabRect = new UiRect(Math.Clamp(grabX, barRect.X, barRect.X + barRect.Width - grabWidth), barRect.Y - 2f, grabWidth, barRect.Height + 4f);
        AddRectFilled(grabRect, active ? _theme.SliderGrabActive : _theme.SliderGrab, _whiteTexture);

        var valueText = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, valueText, _textSettings, _lineHeight);
        var valueX = sliderRect.X + sliderRect.Width - valueSize.X - ButtonPaddingX;
        if (valueX < sliderRect.X + ButtonPaddingX)
        {
            valueX = sliderRect.X + ButtonPaddingX;
        }
        var valuePos = new UiVector2(valueX, sliderRect.Y + (sliderRect.Height - valueSize.Y) * 0.5f);
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

        return changed;
    }
}

