namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool InputText(string label,ref string value,int maxLength)=>
        InputText<UiLegacyTextInputBehavior,UiContextInputPresentation>(label,ref value,maxLength);

    public bool InputText(string label, ref string value, int maxLength, UiInputTextFlags flags)
    {
        return InputText<UiLegacyTextInputBehavior,UiContextInputPresentation>(label,ref value,maxLength,new UiInputOptions(flags));
    }

    public bool InputText(string label, ref string value, int maxLength, UiInputTextFlags flags, Func<string, string>? callback)
    {
        var changed = InputText<UiLegacyTextInputBehavior,UiContextInputPresentation>(label,ref value,maxLength,new UiInputOptions(flags));
        if (changed && callback is not null)
        {
            var next = callback(value) ?? value;
            if (!string.Equals(next, value, StringComparison.Ordinal))
            {
                value = next;
                changed = true;
            }
        }

        return changed;
    }

    public bool InputTextWithHint(string label, string hint, ref string value, int maxLength)
    {
        label ??= "Input";
        hint ??= string.Empty;

        var changed = InputText(label, ref value, maxLength);
        if (string.IsNullOrEmpty(value) && _state.ActiveId != label && !string.IsNullOrWhiteSpace(hint))
        {
            var textSize = MeasureTextInternal(label, _textSettings, _lineHeight);
            var frameHeight = GetFrameHeight();
            var height = MathF.Max(textSize.Y, frameHeight);
            var cursor = _lastItemPos;
            var inputWidth = MathF.Max(0f, _lastItemSize.X - (textSize.X + ItemSpacingX));
            var inputRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, inputWidth, frameHeight);

            var hintSize = MeasureTextInternal(hint, _textSettings, _lineHeight);
            var singleLineTextOffsetY = MathF.Min(3f, MathF.Max(1f, _designTokens.ControlBorderWidth + 1f));
            var hintPos = new UiVector2(inputRect.X + 6f, inputRect.Y + ((inputRect.Height - hintSize.Y) * 0.5f) + singleLineTextOffsetY);
            AddTextInternal(_builder,

                hint,
                hintPos,
                _theme.TextDisabled,
                IntersectRect(CurrentClipRect, inputRect),
                _textSettings,
                _lineHeight
            );
        }

        return changed;
    }

    public bool InputInt(string label, ref int value)
    {
        label ??= "InputInt";

        var buffer = _state.GetTextBuffer(label, FormattableString.Invariant($"{value}"));
        var changed = InputText(label, ref buffer, 16);
        if (changed)
        {
            if (int.TryParse(buffer, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                if (parsed != value)
                {
                    value = parsed;
                }
            }
        }

        _state.SetTextBuffer(label, buffer);
        return changed;
    }

    public bool InputScalar(string label, ref int value)
    {
        return InputInt(label, ref value);
    }

    public bool InputInt2(string label, ref int x, ref int y)
    {
        label ??= "InputInt2";

        BeginRow();
        var changedX = InputInt($"{label} X", ref x);
        var changedY = InputInt($"{label} Y", ref y);
        EndRow();

        return changedX || changedY;
    }

    public bool InputInt3(string label, ref int x, ref int y, ref int z)
    {
        label ??= "InputInt3";

        BeginRow();
        var changedX = InputInt($"{label} X", ref x);
        var changedY = InputInt($"{label} Y", ref y);
        var changedZ = InputInt($"{label} Z", ref z);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool InputInt4(string label, ref int x, ref int y, ref int z, ref int w)
    {
        label ??= "InputInt4";

        BeginRow();
        var changedX = InputInt($"{label} X", ref x);
        var changedY = InputInt($"{label} Y", ref y);
        var changedZ = InputInt($"{label} Z", ref z);
        var changedW = InputInt($"{label} W", ref w);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool InputFloat(string label, ref float value, string format = "0.###")
    {
        label ??= "InputFloat";
        format ??= "0.###";

        var buffer = _state.GetTextBuffer(label, FormattableString.Invariant($"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}"));
        var changed = InputText(label, ref buffer, 32);
        if (changed)
        {
            if (float.TryParse(buffer, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                if (!float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed != value)
                {
                    value = parsed;
                }
            }
        }

        _state.SetTextBuffer(label, buffer);
        return changed;
    }

    public bool InputScalar(string label, ref float value, string format = "0.###")
    {
        return InputFloat(label, ref value, format);
    }

    public bool InputDouble(string label, ref double value, string format = "0.###")
    {
        label ??= "InputDouble";
        format ??= "0.###";

        var buffer = _state.GetTextBuffer(label, FormattableString.Invariant($"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}"));
        var changed = InputText(label, ref buffer, 32);
        if (changed)
        {
            if (double.TryParse(buffer, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                if (!double.IsNaN(parsed) && !double.IsInfinity(parsed) && parsed != value)
                {
                    value = parsed;
                }
            }
        }

        _state.SetTextBuffer(label, buffer);
        return changed;
    }

    public bool InputScalar(string label, ref double value, string format = "0.###")
    {
        return InputDouble(label, ref value, format);
    }

    public bool InputScalarN(string label, int[] values)
    {
        label ??= "InputScalarN";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= InputInt($"{label} {i}", ref values[i]);
        }
        EndRow();

        return changed;
    }

    public bool InputScalarN(string label, float[] values, string format = "0.###")
    {
        label ??= "InputScalarN";
        format ??= "0.###";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= InputFloat($"{label} {i}", ref values[i], format);
        }
        EndRow();

        return changed;
    }

    public bool InputScalarN(string label, double[] values, string format = "0.###")
    {
        label ??= "InputScalarN";
        format ??= "0.###";
        ArgumentNullException.ThrowIfNull(values);

        BeginRow();
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            changed |= InputDouble($"{label} {i}", ref values[i], format);
        }
        EndRow();

        return changed;
    }

    public bool InputDouble2(string label, ref double x, ref double y, string format = "0.###")
    {
        label ??= "InputDouble2";
        format ??= "0.###";

        BeginRow();
        var changedX = InputDouble($"{label} X", ref x, format);
        var changedY = InputDouble($"{label} Y", ref y, format);
        EndRow();

        return changedX || changedY;
    }

    public bool InputDouble3(string label, ref double x, ref double y, ref double z, string format = "0.###")
    {
        label ??= "InputDouble3";
        format ??= "0.###";

        BeginRow();
        var changedX = InputDouble($"{label} X", ref x, format);
        var changedY = InputDouble($"{label} Y", ref y, format);
        var changedZ = InputDouble($"{label} Z", ref z, format);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool InputDouble4(string label, ref double x, ref double y, ref double z, ref double w, string format = "0.###")
    {
        label ??= "InputDouble4";
        format ??= "0.###";

        BeginRow();
        var changedX = InputDouble($"{label} X", ref x, format);
        var changedY = InputDouble($"{label} Y", ref y, format);
        var changedZ = InputDouble($"{label} Z", ref z, format);
        var changedW = InputDouble($"{label} W", ref w, format);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool InputFloat2(string label, ref float x, ref float y, string format = "0.###")
    {
        label ??= "InputFloat2";
        format ??= "0.###";

        BeginRow();
        var changedX = InputFloat($"{label} X", ref x, format);
        var changedY = InputFloat($"{label} Y", ref y, format);
        EndRow();

        return changedX || changedY;
    }

    public bool InputFloat3(string label, ref float x, ref float y, ref float z, string format = "0.###")
    {
        label ??= "InputFloat3";
        format ??= "0.###";

        BeginRow();
        var changedX = InputFloat($"{label} X", ref x, format);
        var changedY = InputFloat($"{label} Y", ref y, format);
        var changedZ = InputFloat($"{label} Z", ref z, format);
        EndRow();

        return changedX || changedY || changedZ;
    }

    public bool InputFloat4(string label, ref float x, ref float y, ref float z, ref float w, string format = "0.###")
    {
        label ??= "InputFloat4";
        format ??= "0.###";

        BeginRow();
        var changedX = InputFloat($"{label} X", ref x, format);
        var changedY = InputFloat($"{label} Y", ref y, format);
        var changedZ = InputFloat($"{label} Z", ref z, format);
        var changedW = InputFloat($"{label} W", ref w, format);
        EndRow();

        return changedX || changedY || changedZ || changedW;
    }

    public bool InputTextMultiline(string label, ref string value, int maxLength, float height)
    {
        label ??= "Input";
        value ??= string.Empty;
        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        var textLineHeight = GetTextLineHeight();
        if (!float.IsFinite(height) || height <= textLineHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than a single line.");
        }

        var displayLabel = GetDisplayLabel(label);
        var textSize = MeasureTextInternal(displayLabel, _textSettings, _lineHeight);
        var hasLabel = !string.IsNullOrEmpty(displayLabel);
        var labelWidth = hasLabel ? textSize.X + ItemSpacingX : 0f;
        var availableWidth = GetContentRegionAvail().X;
        var defaultInputWidth = availableWidth > 0f
            ? MathF.Max(1f, availableWidth - labelWidth)
            : InputWidth;
        var inputWidth = ResolveItemWidth(defaultInputWidth);
        var totalSize = new UiVector2(labelWidth + inputWidth, height);
        var cursor = AdvanceCursor(totalSize);

        if (hasLabel)
        {
            var labelPos = new UiVector2(cursor.X, cursor.Y + ButtonPaddingY);
            AddTextInternal(_builder,

                displayLabel,
                labelPos,
                _theme.InputText,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        var inputRect = new UiRect(cursor.X + labelWidth, cursor.Y, inputWidth, height);
        return EditMultilineInput(label, ResolveId(label), ref value, maxLength, new(inputRect, textLineHeight));
    }


    public bool InputTextMultiline(string label, ref string value, int maxLength, int visibleLines)
    {
        if (visibleLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleLines), "Visible line count must be positive.");
        }

        var height = (GetTextLineHeight() * visibleLines) + 8f;
        return InputTextMultiline(label, ref value, maxLength, height);
    }

    private void DrawImeCompositionInline(string compositionText, float startX, float startY, UiRect inputRect, UiRect inputClip)
        =>DrawImeCompositionInline(compositionText,startX,startY,inputRect,inputClip,_theme.InputText);

    private void DrawImeCompositionInline(string compositionText,float startX,float startY,UiRect inputRect,UiRect inputClip,UiColor color)
    {
        if (string.IsNullOrEmpty(compositionText))
        {
            return;
        }

        var textPos = new UiVector2(startX, startY);
        var compositionTextSettings = new UiTextSettings(
            _textSettings.Scale,
            _textSettings.LineHeightScale,
            _textSettings.PixelSnap,
            _textSettings.UseBaseline,
            true,
            _textSettings.MissingGlyphObserver
        );
        AddTextInternal(_builder,

            compositionText,
            textPos,
            color,
            inputClip,
            compositionTextSettings,
            _lineHeight
        );

        DrawImeCompositionUnderline(compositionText, startX, startY, inputRect, inputClip, _lineHeight,color);
    }

    private void DrawImeCompositionUnderline(string compositionText, float startX, float startY, UiRect inputRect, UiRect inputClip, float lineHeight)
        =>DrawImeCompositionUnderline(compositionText,startX,startY,inputRect,inputClip,lineHeight,_theme.InputText);

    private void DrawImeCompositionUnderline(string compositionText,float startX,float startY,UiRect inputRect,UiRect inputClip,float lineHeight,UiColor color)
    {
        if (string.IsNullOrEmpty(compositionText))
        {
            return;
        }

        var compositionWidth = MeasureTextWidth(compositionText, compositionText.Length);
        if (compositionWidth <= 0f)
        {
            return;
        }

        var underlineThickness = MathF.Max(1f, 1f / MathF.Max(1f, _textSettings.Scale));
        var underlineY = MathF.Min(startY + lineHeight - underlineThickness, inputRect.Y + inputRect.Height - underlineThickness);
        var underlineLeft = Math.Clamp(startX, inputRect.X, inputRect.X + inputRect.Width);
        var underlineRight = Math.Clamp(startX + compositionWidth, inputRect.X, inputRect.X + inputRect.Width);
        if (underlineRight <= underlineLeft)
        {
            return;
        }

        var underlineRect = new UiRect(underlineLeft, underlineY, underlineRight - underlineLeft, underlineThickness);
        _builder.AddRectFilled(underlineRect, color, _whiteTexture, inputClip);
    }
}

