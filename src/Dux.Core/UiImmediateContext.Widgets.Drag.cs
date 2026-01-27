namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")
    {
        label ??= "DragFloat";
        format ??= "0.###";

        var id = ResolveId(label);
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var dragWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + dragWidth, height);
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

        var dragRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, dragWidth, frameHeight);
        var hovered = ItemHoverable(id, dragRect);

        if (hovered && _leftMousePressed)
        {
            _state.ActiveId = id;
            _state.SetScrollX(id, _mousePosition.X);
            _state.SetScrollY(id, value);
        }

        var active = _state.ActiveId == id;
        if (!_leftMouseDown && active)
        {
            _state.ActiveId = null;
            active = false;
        }

        var changed = false;
        if (active && _leftMouseDown)
        {
            var startX = _state.GetScrollX(id);
            var startValue = _state.GetScrollY(id);
            var delta = (_mousePosition.X - startX) * speed;
            var next = startValue + delta;
            if (!float.IsNaN(next) && !float.IsInfinity(next))
            {
                next = Math.Clamp(next, min, max);
                if (MathF.Abs(next - value) > float.Epsilon)
                {
                    value = next;
                    changed = true;
                }
            }
        }

        var bg = active ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(dragRect, bg, _whiteTexture);

        var valueText = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        var valueSize = UiTextBuilder.MeasureText(_fontAtlas, valueText, _textSettings, _lineHeight);
        var valuePos = new UiVector2(dragRect.X + 6f, dragRect.Y + (dragRect.Height - valueSize.Y) * 0.5f);
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

    public bool DragFloat2(string label, ref float x, ref float y, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")
    {
        label ??= "DragFloat2";
        format ??= "0.###";

        BeginRow();
        var changedX = DragFloat($"{label} X", ref x, speed, min, max, format);
        var changedY = DragFloat($"{label} Y", ref y, speed, min, max, format);
        EndRow();

        return changedX || changedY;
    }

    public bool DragFloat3(string label, ref float x, ref float y, ref float z, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")
    {
        label ??= "DragFloat3";
        format ??= "0.###";

        BeginRow();
        var changedX = DragFloat($"{label} X", ref x, speed, min, max, format);
        var changedY = DragFloat($"{label} Y", ref y, speed, min, max, format);
        var changedZ = DragFloat($"{label} Z", ref z, speed, min, max, format);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool DragFloat4(string label, ref float x, ref float y, ref float z, ref float w, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")
    {
        label ??= "DragFloat4";
        format ??= "0.###";

        BeginRow();
        var changedX = DragFloat($"{label} X", ref x, speed, min, max, format);
        var changedY = DragFloat($"{label} Y", ref y, speed, min, max, format);
        var changedZ = DragFloat($"{label} Z", ref z, speed, min, max, format);
        var changedW = DragFloat($"{label} W", ref w, speed, min, max, format);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool DragFloatRange2(string label, ref float minValue, ref float maxValue, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###", string? formatMax = null)
    {
        label ??= "DragFloatRange2";
        format ??= "0.###";
        formatMax ??= format;

        BeginRow();
        var changedMin = DragFloat($"{label} Min", ref minValue, speed, min, max, format);
        var changedMax = DragFloat($"{label} Max", ref maxValue, speed, min, max, formatMax);
        EndRow();

        return changedMin || changedMax;
    }

    public bool DragScalar(string label, ref float value, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")
    {
        return DragFloat(label, ref value, speed, min, max, format);
    }

    public bool DragScalar(string label, ref int value, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)
    {
        return DragInt(label, ref value, speed, min, max);
    }

    public bool DragScalar(string label, ref double value, float speed = 0.01f, double min = double.NegativeInfinity, double max = double.PositiveInfinity, string format = "0.###")
    {
        var floatValue = (float)value;
        var changed = DragFloat(label, ref floatValue, speed, (float)min, (float)max, format);
        var next = Math.Clamp((double)floatValue, min, max);
        if (!double.IsNaN(next) && !double.IsInfinity(next) && Math.Abs(next - value) > double.Epsilon)
        {
            value = next;
            changed = true;
        }

        return changed;
    }

    public bool DragInt(string label, ref int value, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)
    {
        label ??= "DragInt";

        var floatValue = (float)value;
        var changed = DragFloat(label, ref floatValue, speed, min, max, "0");
        var next = Math.Clamp((int)MathF.Round(floatValue), min, max);
        if (next != value)
        {
            value = next;
            changed = true;
        }

        return changed;
    }

    public bool DragInt2(string label, ref int x, ref int y, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)
    {
        label ??= "DragInt2";

        BeginRow();
        var changedX = DragInt($"{label} X", ref x, speed, min, max);
        var changedY = DragInt($"{label} Y", ref y, speed, min, max);
        EndRow();

        return changedX || changedY;
    }

    public bool DragInt3(string label, ref int x, ref int y, ref int z, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)
    {
        label ??= "DragInt3";

        BeginRow();
        var changedX = DragInt($"{label} X", ref x, speed, min, max);
        var changedY = DragInt($"{label} Y", ref y, speed, min, max);
        var changedZ = DragInt($"{label} Z", ref z, speed, min, max);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool DragInt4(string label, ref int x, ref int y, ref int z, ref int w, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)
    {
        label ??= "DragInt4";

        BeginRow();
        var changedX = DragInt($"{label} X", ref x, speed, min, max);
        var changedY = DragInt($"{label} Y", ref y, speed, min, max);
        var changedZ = DragInt($"{label} Z", ref z, speed, min, max);
        var changedW = DragInt($"{label} W", ref w, speed, min, max);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool DragIntRange2(string label, ref int minValue, ref int maxValue, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue, string? format = null, string? formatMax = null)
    {
        _ = format;
        _ = formatMax;
        label ??= "DragIntRange2";

        BeginRow();
        var changedMin = DragInt($"{label} Min", ref minValue, speed, min, max);
        var changedMax = DragInt($"{label} Max", ref maxValue, speed, min, max);
        EndRow();

        return changedMin || changedMax;
    }

    public bool DragScalarN(string label, int[] values, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)
    {
        label ??= "DragScalarN";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= DragInt($"{label} {i}", ref values[i], speed, min, max);
        }
        EndRow();

        return changed;
    }

    public bool DragScalarN(string label, float[] values, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")
    {
        label ??= "DragScalarN";
        format ??= "0.###";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= DragFloat($"{label} {i}", ref values[i], speed, min, max, format);
        }
        EndRow();

        return changed;
    }

    public bool DragScalarN(string label, double[] values, float speed = 0.01f, double min = double.NegativeInfinity, double max = double.PositiveInfinity, string format = "0.###")
    {
        label ??= "DragScalarN";
        format ??= "0.###";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            changed |= DragScalar($"{label} {i}", ref value, speed, min, max, format);
            values[i] = value;
        }
        EndRow();

        return changed;
    }
}
