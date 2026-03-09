namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool InputText(string label, ref string value, int maxLength)
    {
        label ??= "Input";
        value ??= string.Empty;
        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        var id = ResolveId(label);
        var buffer = _state.GetEditBuffer(id, value);
        var text = buffer.Text;
        var history = _state.GetHistory(id, text);
        var selection = _state.GetSelection(id);

        var displayLabel = GetDisplayLabel(label);
        var textSize = MeasureTextInternal(displayLabel, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
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
            var labelPos = new UiVector2(cursor.X, cursor.Y + (height - textSize.Y) * 0.5f);
            AddTextInternal(_builder,

                displayLabel,
                labelPos,
                _theme.Text,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        var inputRect = new UiRect(cursor.X + labelWidth, cursor.Y + (height - frameHeight) * 0.5f, inputWidth, frameHeight);
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        var hovered = ItemHoverable(id, inputRect);
        var compositionOwner = _state.ActiveId ?? _state.PreviousActiveId;
        var compositionTextAtFocus = _imeHandler?.GetCompositionText();
        var compositionActive = !string.IsNullOrEmpty(compositionTextAtFocus)
            || (_charEvents.Count > 0 && !string.IsNullOrEmpty(_state.PreviousActiveId));
        var lockFocusForIme = compositionActive
            && !string.IsNullOrEmpty(compositionOwner)
            && !string.Equals(compositionOwner, id, StringComparison.Ordinal);
        var lockFocusClearForIme = compositionActive && !string.IsNullOrEmpty(compositionOwner);

        if (!compositionActive
            && !string.IsNullOrEmpty(_state.PendingTextInputFocusId)
            && string.Equals(_state.PendingTextInputFocusId, id, StringComparison.Ordinal))
        {
            _state.ActiveId = id;
            _state.PendingTextInputFocusId = null;
        }

        var caretIndex = ClampCaret(text, _state.GetCursor(id, text.Length));
        var initialCaretIndex = caretIndex;
        var caretMoved = false;
        var scrollX = _state.GetScrollX(id);
        if (_leftMousePressed)
        {
            if (hovered)
            {
                var allowActivate = !lockFocusForIme || _charEvents.Count > 0;
                if (allowActivate)
                {
                    _state.ActiveId = id;
                    var caret = GetCaretIndexFromMouse(text, inputRect.X + 6f - scrollX);
                    var clickCount = _state.RegisterClick(id, _mousePosition);
                    var shiftClick = (_state.Modifiers & KeyModifiers.Shift) != 0;

                    if (shiftClick)
                    {
                        selection = new UiTextSelection(caretIndex, caret);
                    }
                    else
                    {
                        switch (clickCount)
                        {
                            case 2:
                                SelectWordAt(text, ref caret, ref selection);
                                break;
                            case >= 3:
                                selection = new UiTextSelection(0, text.Length);
                                caret = text.Length;
                                break;
                            default:
                                selection = new UiTextSelection(caret, caret);
                                break;
                        }
                    }

                    caretIndex = caret;
                    caretMoved = true;
                    _state.SetCursor(id, caretIndex);

                    if (string.Equals(_state.PendingTextInputFocusId, id, StringComparison.Ordinal))
                    {
                        _state.PendingTextInputFocusId = null;
                    }
                }
                else
                {
                    _state.PendingTextInputFocusId = id;
                }
            }
            else if (_state.ActiveId == id)
            {
                if (!lockFocusClearForIme)
                {
                    _state.ActiveId = null;
                }
            }
        }

        var active = _state.ActiveId == id;
        var background = active ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(inputRect, background, _whiteTexture);

        var changed = false;
        var historyChanged = false;
        selection = ClampSelection(selection, text.Length, caretIndex);

        var committedText = _imeHandler?.ConsumeCommittedText(id);
        if (!string.IsNullOrEmpty(committedText))
        {
            var committedInsert = SanitizeText(committedText, false);
            if (!string.IsNullOrEmpty(committedInsert))
            {
                changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, committedInsert, maxLength);
            }
        }

        if (active)
        {
            _imeHandler?.SetCompositionOwner(id);

            if (_leftMouseDown && !_leftMousePressed && hovered && _state.WidgetClickCount <= 1)
            {
                var dragCaret = GetCaretIndexFromMouse(text, inputRect.X + 6f - scrollX);
                selection = selection with { End = dragCaret };
                caretIndex = dragCaret;
                caretMoved = true;
            }

            foreach (var keyEvent in _keyEvents)
            {
                if (!keyEvent.IsDown)
                {
                    continue;
                }

                var ctrl = (keyEvent.Modifiers & KeyModifiers.Ctrl) != 0;
                var shift = (keyEvent.Modifiers & KeyModifiers.Shift) != 0;

                if (ctrl && keyEvent.Key == UiKey.A)
                {
                    selection = new UiTextSelection(0, text.Length);
                    caretIndex = text.Length;
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.C)
                {
                    if (selection.HasSelection)
                    {
                        var (start, end) = GetSelectionRange(selection);
                        var clip = GetClipboardOrThrow();
                        clip.SetText(text[start..end]);
                    }
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.X)
                {
                    if (selection.HasSelection)
                    {
                        var (start, end) = GetSelectionRange(selection);
                        var clip = GetClipboardOrThrow();
                        clip.SetText(text[start..end]);
                        buffer.Replace(start, end - start, string.Empty);
                        text = buffer.Text;
                        caretIndex = start;
                        selection = new UiTextSelection(caretIndex, caretIndex);
                        changed = true;
                    }
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.V)
                {
                    var clip = GetClipboardOrThrow();
                    var paste = SanitizeText(clip.GetText() ?? string.Empty, false);
                    if (!string.IsNullOrEmpty(paste))
                    {
                        changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, paste, maxLength);
                    }
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.Z)
                {
                    if (history.Undo(out var undoValue))
                    {
                        buffer.Sync(undoValue);
                        text = buffer.Text;
                        caretIndex = text.Length;
                        selection = new UiTextSelection(caretIndex, caretIndex);
                        changed = true;
                    }
                    historyChanged = true;
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.Y)
                {
                    if (history.Redo(out var redoValue))
                    {
                        buffer.Sync(redoValue);
                        text = buffer.Text;
                        caretIndex = text.Length;
                        selection = new UiTextSelection(caretIndex, caretIndex);
                        changed = true;
                    }
                    historyChanged = true;
                    continue;
                }

                if (keyEvent.Key == UiKey.Backspace)
                {
                    if (selection.HasSelection)
                    {
                        changed |= DeleteSelection(ref buffer, ref text, ref caretIndex, ref selection);
                    }
                    else if (caretIndex > 0)
                    {
                        buffer.Replace(caretIndex - 1, 1, string.Empty);
                        text = buffer.Text;
                        caretIndex = Math.Max(caretIndex - 1, 0);
                        changed = true;
                    }
                }
                else if (keyEvent.Key == UiKey.Delete)
                {
                    if (selection.HasSelection)
                    {
                        changed |= DeleteSelection(ref buffer, ref text, ref caretIndex, ref selection);
                    }
                    else if (caretIndex < text.Length)
                    {
                        buffer.Replace(caretIndex, 1, string.Empty);
                        text = buffer.Text;
                        changed = true;
                    }
                }
                else if (keyEvent.Key == UiKey.LeftArrow)
                {
                    var next = ctrl ? FindWordBoundaryLeft(text, caretIndex) : Math.Max(caretIndex - 1, 0);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.RightArrow)
                {
                    var next = ctrl ? FindWordBoundaryRight(text, caretIndex) : Math.Min(caretIndex + 1, text.Length);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Home)
                {
                    var next = ctrl ? 0 : GetLineStartIndex(text, caretIndex);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.End)
                {
                    var next = ctrl ? text.Length : GetLineEndIndex(text, caretIndex);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.PageUp)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((inputRect.Height - 8f) / _lineHeight));
                    var next = MoveCaretVertical(text, caretIndex, -visibleLines);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.PageDown)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((inputRect.Height - 8f) / _lineHeight));
                    var next = MoveCaretVertical(text, caretIndex, visibleLines);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Enter || keyEvent.Key == UiKey.Escape)
                {
                    _state.ActiveId = null;
                }
            }

            if (HandleRepeatSingleLine(buffer, ref text, ref caretIndex, ref selection))
            {
                changed = true;
            }

            var skipCharEventsForIme = _imeHandler is not null;
            if (!skipCharEventsForIme)
            {
                foreach (var charEvent in _charEvents)
                {
                    if (text.Length >= maxLength)
                    {
                        break;
                    }

                    var rune = new System.Text.Rune((int)charEvent.CodePoint);
                    if (rune.Value < 0x20)
                    {
                        continue;
                    }

                    var insert = SanitizeText(rune.ToString(), false);
                    if (!string.IsNullOrEmpty(insert))
                    {
                        changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, insert, maxLength);
                    }
                }
            }
        }

        if (active)
        {
            _state.SetCursor(id, caretIndex);
        }

        if (active && (caretMoved || caretIndex != initialCaretIndex))
        {
            ResetCaretBlink();
        }

        _state.SetSelection(id, selection);

        if (changed && !historyChanged)
        {
            history.Push(text);
        }

        var compositionText = active ? _imeHandler?.GetCompositionText() : null;
        var compositionWidth = !string.IsNullOrEmpty(compositionText)
            ? MeasureTextWidth(compositionText, compositionText.Length)
            : 0f;

        var visibleWidth = inputRect.Width - 12f;
        if (active && visibleWidth > 0f)
        {
            var caretPixel = MeasureTextWidth(text, caretIndex) + compositionWidth;
            var maxScroll = MathF.Max(0f, MeasureTextWidth(text, text.Length) + compositionWidth - visibleWidth);
            if (caretPixel - scrollX > visibleWidth)
            {
                scrollX = MathF.Min(maxScroll, caretPixel - visibleWidth + 4f);
            }
            else if (caretPixel - scrollX < 0f)
            {
                scrollX = MathF.Max(0f, caretPixel - 4f);
            }
            scrollX = Math.Clamp(scrollX, 0f, maxScroll);
            _state.SetScrollX(id, scrollX);
        }

        var textPos = new UiVector2(inputRect.X + 6f - scrollX, inputRect.Y + (inputRect.Height - textSize.Y) * 0.5f);
        if (active && selection.HasSelection)
        {
            var (start, end) = GetSelectionRange(selection);
            var startX = textPos.X + MeasureTextWidth(text, start);
            var endX = textPos.X + MeasureTextWidth(text, end);
            var selectionRect = new UiRect(startX, inputRect.Y + 2f, endX - startX, inputRect.Height - 4f);
            _builder.AddRectFilled(selectionRect, _theme.TextSelectedBg, _whiteTexture, inputClip);
        }

        if (!string.IsNullOrEmpty(compositionText))
        {
            if (caretIndex > 0)
            {
                AddTextInternal(_builder, text[..caretIndex], textPos, _theme.Text, inputClip, _textSettings, _lineHeight);
            }
            if (caretIndex < text.Length)
            {
                var afterX = MathF.Round(textPos.X + MeasureTextWidth(text, caretIndex) + compositionWidth);
                AddTextInternal(_builder, text[caretIndex..], new UiVector2(afterX, textPos.Y), _theme.Text, inputClip, _textSettings, _lineHeight);
            }
        }
        else
        {
            AddTextInternal(_builder, text, textPos, _theme.Text, inputClip, _textSettings, _lineHeight);
        }

        if (active)
        {
            var caretX = MathF.Round(textPos.X + MeasureTextWidth(text, caretIndex));
            var drawCaretX = caretX;
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            GetImeFontMetrics(text, caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            _imeHandler?.SetCaretRect(new UiRect(caretX, inputRect.Y + 3f, caretWidth, inputRect.Height - 6f), inputRect, fontPixelHeight, fontPixelWidth);
            if (!string.IsNullOrEmpty(compositionText))
            {
                DrawImeCompositionInline(compositionText, caretX, textPos.Y, inputRect, inputClip);
                drawCaretX = MathF.Round(caretX + compositionWidth);
            }

            var caretLeft = Math.Clamp(drawCaretX, inputRect.X, inputRect.X + inputRect.Width - caretWidth);
            var caretTop = Math.Clamp(inputRect.Y + 3f, inputRect.Y, inputRect.Y + inputRect.Height - (inputRect.Height - 6f));
            var caretRect = new UiRect(caretLeft, caretTop, caretWidth, inputRect.Height - 6f);
            if (IsCaretBlinkOn())
            {
                _builder.AddRectFilled(caretRect, _theme.Text, _whiteTexture, inputClip);
            }
        }

        value = text;

        return changed;
    }

    public bool InputText(string label, ref string value, int maxLength, UiInputTextFlags flags)
    {
        _ = flags;
        return InputText(label, ref value, maxLength);
    }

    public bool InputText(string label, ref string value, int maxLength, UiInputTextFlags flags, Func<string, string>? callback)
    {
        _ = flags;
        var changed = InputText(label, ref value, maxLength);
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
            var hintPos = new UiVector2(inputRect.X + 6f, inputRect.Y + (inputRect.Height - hintSize.Y) * 0.5f);
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
        if (height <= textLineHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than a single line.");
        }

        var normalizedValue = SanitizeText(value, true);
        var normalizedExistingValue = !string.Equals(value, normalizedValue, StringComparison.Ordinal);
        if (normalizedExistingValue)
        {
            value = normalizedValue;
        }

        var id = ResolveId(label);
        var buffer = _state.GetEditBuffer(id, normalizedValue);
        var text = buffer.Text;
        var history = _state.GetHistory(id, text);
        var selection = _state.GetSelection(id);

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
                _theme.Text,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        var inputRect = new UiRect(cursor.X + labelWidth, cursor.Y, inputWidth, height);
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        var hovered = ItemHoverable(id, inputRect);
        var compositionOwner = _state.ActiveId ?? _state.PreviousActiveId;
        var compositionTextAtFocus = _imeHandler?.GetCompositionText();
        var compositionActive = !string.IsNullOrEmpty(compositionTextAtFocus)
            || (_charEvents.Count > 0 && !string.IsNullOrEmpty(_state.PreviousActiveId));
        var lockFocusForIme = compositionActive
            && !string.IsNullOrEmpty(compositionOwner)
            && !string.Equals(compositionOwner, id, StringComparison.Ordinal);
        var lockFocusClearForIme = compositionActive && !string.IsNullOrEmpty(compositionOwner);

        if (!compositionActive
            && !string.IsNullOrEmpty(_state.PendingTextInputFocusId)
            && string.Equals(_state.PendingTextInputFocusId, id, StringComparison.Ordinal))
        {
            _state.ActiveId = id;
            _state.PendingTextInputFocusId = null;
        }

        var caretIndex = ClampCaret(text, _state.GetCursor(id, text.Length));
        var initialCaretIndex = caretIndex;
        var caretMoved = false;
        var scrollY = _state.GetScrollY(id);
        var scrollX = _state.GetScrollX(id);
        var lineCount = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineCount++;
            }
        }

        const float scrollActivationEpsilon = 0.5f;
        var visibleTextHeight = inputRect.Height - 8f;
        var visibleTextWidth = inputRect.Width - 12f;
        var contentHeight = lineCount * textLineHeight;
        var rawMaxScroll = contentHeight - visibleTextHeight;
        var maxScroll = rawMaxScroll > scrollActivationEpsilon ? rawMaxScroll : 0f;
        var wheelBlocked = _popupTierDepth == 0 && IsMouseOverAnyBlockingPopup();
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f && !wheelBlocked)
        {
            scrollY = Math.Clamp(scrollY - (_mouseWheel * textLineHeight * 3f), 0f, maxScroll);
            _mouseWheel = 0f;
        }
        var maxLineWidth = MeasureMaxLineWidth(text);
        var rawMaxScrollX = maxLineWidth - visibleTextWidth;
        var maxScrollX = rawMaxScrollX > scrollActivationEpsilon ? rawMaxScrollX : 0f;
        if (hovered && MathF.Abs(_mouseWheelHorizontal) > 0.001f && maxScrollX > 0f && !wheelBlocked)
        {
            scrollX = Math.Clamp(scrollX - (_mouseWheelHorizontal * textLineHeight * 3f), 0f, maxScrollX);
            _mouseWheelHorizontal = 0f;
        }
        var interactionBlocked = _popupTierDepth == 0 && (IsMouseOverAnyBlockingPopup() || IsMouseOverAnyOpenMenuPopup());
        var hasVScroll = maxScroll > 0f;
        var hasHScroll = maxScrollX > 0f;
        var textInteractionRect = new UiRect(
            inputRect.X,
            inputRect.Y,
            MathF.Max(0f, inputRect.Width - (hasVScroll ? ScrollbarSize : 0f)),
            MathF.Max(0f, inputRect.Height - (hasHScroll ? ScrollbarSize : 0f))
        );
        var textHovered = !interactionBlocked && IsHovering(textInteractionRect);
        var verticalScrollbarRect = hasVScroll
            ? new UiRect(
                inputRect.X + inputRect.Width - ScrollbarSize,
                inputRect.Y,
                ScrollbarSize,
                inputRect.Height - (hasHScroll ? ScrollbarSize : 0f))
            : default;
        var horizontalScrollbarRect = hasHScroll
            ? new UiRect(
                inputRect.X,
                inputRect.Y + inputRect.Height - ScrollbarSize,
                inputRect.Width - (hasVScroll ? ScrollbarSize : 0f),
                ScrollbarSize)
            : default;
        var scrollbarHovered = !interactionBlocked && ((hasVScroll && IsHovering(verticalScrollbarRect)) || (hasHScroll && IsHovering(horizontalScrollbarRect)));

        if (_leftMousePressed && scrollbarHovered && _state.ActiveId == id)
        {
            _state.ActiveId = null;
        }

        if (_leftMousePressed)
        {
            if (textHovered)
            {
                var allowActivate = !lockFocusForIme || _charEvents.Count > 0;
                if (allowActivate)
                {
                    _state.ActiveId = id;
                    var caret = GetCaretIndexFromMouseMultiline(text, inputRect.X + 6f - scrollX, inputRect.Y + 4f - scrollY, textLineHeight);
                    var clickCount = _state.RegisterClick(id, _mousePosition);
                    var shiftClick = (_state.Modifiers & KeyModifiers.Shift) != 0;

                    if (shiftClick)
                    {
                        selection = new UiTextSelection(caretIndex, caret);
                    }
                    else
                    {
                        switch (clickCount)
                        {
                            case 2:
                                SelectWordAt(text, ref caret, ref selection);
                                break;
                            case 3:
                                var lineStart = GetLineStartIndex(text, caret);
                                var lineEnd = GetLineEndIndex(text, caret);
                                selection = new UiTextSelection(lineStart, lineEnd);
                                caret = lineEnd;
                                break;
                            case >= 4:
                                selection = new UiTextSelection(0, text.Length);
                                caret = text.Length;
                                break;
                            default:
                                selection = new UiTextSelection(caret, caret);
                                break;
                        }
                    }

                    caretIndex = caret;
                    caretMoved = true;
                    _state.SetCursor(id, caretIndex);

                    if (string.Equals(_state.PendingTextInputFocusId, id, StringComparison.Ordinal))
                    {
                        _state.PendingTextInputFocusId = null;
                    }
                }
                else
                {
                    _state.PendingTextInputFocusId = id;
                }
            }
            else if (!hovered && _state.ActiveId == id)
            {
                if (!lockFocusClearForIme)
                {
                    _state.ActiveId = null;
                }
            }
        }

        var active = _state.ActiveId == id;
        var background = active ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(inputRect, background, _whiteTexture);

        var changed = normalizedExistingValue;
        var historyChanged = false;
        selection = ClampSelection(selection, text.Length, caretIndex);

        var committedText = _imeHandler?.ConsumeCommittedText(id);
        if (!string.IsNullOrEmpty(committedText))
        {
            var committedInsert = SanitizeText(committedText, true);
            if (!string.IsNullOrEmpty(committedInsert))
            {
                changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, committedInsert, maxLength);
            }
        }

        if (active)
        {
            _imeHandler?.SetCompositionOwner(id);

            if (_leftMouseDown && !_leftMousePressed && _state.WidgetClickCount <= 1)
            {
                var nextScrollY = scrollY;
                var nextScrollX = scrollX;
                var autoScrolled = false;
                var currentTime = (float)_state.TimeSeconds;
                var lastAutoScrollTime = _state.GetScrollY($"{id}##dragAutoScrollTime");
                var deltaTime = lastAutoScrollTime > 0f
                    ? Math.Clamp(currentTime - lastAutoScrollTime, 1f / 240f, 1f / 30f)
                    : 1f / 60f;
                _state.SetScrollY($"{id}##dragAutoScrollTime", currentTime);

                if (maxScroll > 0f)
                {
                    if (_mousePosition.Y < textInteractionRect.Y)
                    {
                        var delta = textInteractionRect.Y - _mousePosition.Y;
                        var overshoot = Math.Clamp(delta / 160f, 0f, 1f);
                        var scrollSpeed = 6f + ((MathF.Max(48f, textLineHeight * 3.5f) - 6f) * overshoot * overshoot * overshoot);
                        var scrollStep = scrollSpeed * deltaTime;
                        nextScrollY = Math.Clamp(scrollY - scrollStep, 0f, maxScroll);
                    }
                    else if (_mousePosition.Y > textInteractionRect.Y + textInteractionRect.Height)
                    {
                        var delta = _mousePosition.Y - (textInteractionRect.Y + textInteractionRect.Height);
                        var overshoot = Math.Clamp(delta / 160f, 0f, 1f);
                        var scrollSpeed = 6f + ((MathF.Max(48f, textLineHeight * 3.5f) - 6f) * overshoot * overshoot * overshoot);
                        var scrollStep = scrollSpeed * deltaTime;
                        nextScrollY = Math.Clamp(scrollY + scrollStep, 0f, maxScroll);
                    }
                }

                if (maxScrollX > 0f)
                {
                    if (_mousePosition.X < textInteractionRect.X)
                    {
                        var delta = textInteractionRect.X - _mousePosition.X;
                        var overshoot = Math.Clamp(delta / 160f, 0f, 1f);
                        var scrollSpeed = 6f + ((84f - 6f) * overshoot * overshoot * overshoot);
                        var scrollStep = scrollSpeed * deltaTime;
                        nextScrollX = Math.Clamp(scrollX - scrollStep, 0f, maxScrollX);
                    }
                    else if (_mousePosition.X > textInteractionRect.X + textInteractionRect.Width)
                    {
                        var delta = _mousePosition.X - (textInteractionRect.X + textInteractionRect.Width);
                        var overshoot = Math.Clamp(delta / 160f, 0f, 1f);
                        var scrollSpeed = 6f + ((84f - 6f) * overshoot * overshoot * overshoot);
                        var scrollStep = scrollSpeed * deltaTime;
                        nextScrollX = Math.Clamp(scrollX + scrollStep, 0f, maxScrollX);
                    }
                }

                if (MathF.Abs(nextScrollY - scrollY) > 0.001f || MathF.Abs(nextScrollX - scrollX) > 0.001f)
                {
                    scrollY = nextScrollY;
                    scrollX = nextScrollX;
                    autoScrolled = true;
                    _requestFrame?.Invoke();
                }

                var dragCaret = GetCaretIndexFromMouseMultiline(text, inputRect.X + 6f - scrollX, inputRect.Y + 4f - scrollY, textLineHeight);
                selection = selection with { End = dragCaret };
                caretIndex = dragCaret;
                caretMoved = true;

                if (autoScrolled)
                {
                    ResetCaretBlink();
                }
            }

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

                var ctrl = (keyEvent.Modifiers & KeyModifiers.Ctrl) != 0;
                var shift = (keyEvent.Modifiers & KeyModifiers.Shift) != 0;

                if (ctrl && keyEvent.Key == UiKey.A)
                {
                    selection = new UiTextSelection(0, text.Length);
                    caretIndex = text.Length;
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.C)
                {
                    if (selection.HasSelection)
                    {
                        var (start, end) = GetSelectionRange(selection);
                        var clip = GetClipboardOrThrow();
                        clip.SetText(text[start..end]);
                    }
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.X)
                {
                    if (selection.HasSelection)
                    {
                        var (start, end) = GetSelectionRange(selection);
                        var clip = GetClipboardOrThrow();
                        clip.SetText(text[start..end]);
                        buffer.Replace(start, end - start, string.Empty);
                        text = buffer.Text;
                        caretIndex = start;
                        selection = new UiTextSelection(caretIndex, caretIndex);
                        changed = true;
                    }
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.V)
                {
                    var clip = GetClipboardOrThrow();
                    var paste = SanitizeText(clip.GetText() ?? string.Empty, true);
                    if (!string.IsNullOrEmpty(paste))
                    {
                        changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, paste, maxLength);
                    }
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.Z)
                {
                    if (history.Undo(out var undoValue))
                    {
                        buffer.Sync(undoValue);
                        text = buffer.Text;
                        caretIndex = text.Length;
                        selection = new UiTextSelection(caretIndex, caretIndex);
                        changed = true;
                    }
                    historyChanged = true;
                    continue;
                }

                if (ctrl && keyEvent.Key == UiKey.Y)
                {
                    if (history.Redo(out var redoValue))
                    {
                        buffer.Sync(redoValue);
                        text = buffer.Text;
                        caretIndex = text.Length;
                        selection = new UiTextSelection(caretIndex, caretIndex);
                        changed = true;
                    }
                    historyChanged = true;
                    continue;
                }

                if (keyEvent.Key == UiKey.Backspace)
                {
                    if (selection.HasSelection)
                    {
                        changed |= DeleteSelection(ref buffer, ref text, ref caretIndex, ref selection);
                    }
                    else if (caretIndex > 0)
                    {
                        buffer.Replace(caretIndex - 1, 1, string.Empty);
                        text = buffer.Text;
                        caretIndex = Math.Max(caretIndex - 1, 0);
                        changed = true;
                    }
                }
                else if (keyEvent.Key == UiKey.Delete)
                {
                    if (selection.HasSelection)
                    {
                        changed |= DeleteSelection(ref buffer, ref text, ref caretIndex, ref selection);
                    }
                    else if (caretIndex < text.Length)
                    {
                        buffer.Replace(caretIndex, 1, string.Empty);
                        text = buffer.Text;
                        changed = true;
                    }
                }
                else if (keyEvent.Key == UiKey.LeftArrow)
                {
                    var next = ctrl ? FindWordBoundaryLeft(text, caretIndex) : Math.Max(caretIndex - 1, 0);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.RightArrow)
                {
                    var next = ctrl ? FindWordBoundaryRight(text, caretIndex) : Math.Min(caretIndex + 1, text.Length);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.UpArrow)
                {
                    var next = shift
                        ? GetShiftLineUpTarget(text, caretIndex)
                        : MoveCaretVertical(text, caretIndex, -1);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.DownArrow)
                {
                    var next = shift
                        ? GetShiftLineDownTarget(text, caretIndex)
                        : MoveCaretVertical(text, caretIndex, 1);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Home)
                {
                    var next = ctrl ? 0 : GetLineStartIndex(text, caretIndex);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.End)
                {
                    var next = ctrl ? text.Length : GetLineEndIndex(text, caretIndex);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Tab)
                {
                    var tabText = new string(' ', TabSpaces);
                    changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, tabText, maxLength);
                }
                else if (keyEvent.Key == UiKey.PageUp)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((inputRect.Height - 8f) / textLineHeight));
                    var next = MoveCaretVertical(text, caretIndex, -visibleLines);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.PageDown)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((inputRect.Height - 8f) / textLineHeight));
                    var next = MoveCaretVertical(text, caretIndex, visibleLines);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Enter)
                {
                    var insert = SanitizeText("\n", true);
                    if (!string.IsNullOrEmpty(insert))
                    {
                        changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, insert, maxLength);
                    }
                }
                else if (keyEvent.Key == UiKey.Escape)
                {
                    _state.ActiveId = null;
                }
            }

            if (HandleRepeatMultiline(buffer, ref text, ref caretIndex, ref selection, inputRect.Height, textLineHeight))
            {
                changed = true;
            }

            var skipCharEventsForIme = _imeHandler is not null;
            if (!skipCharEventsForIme)
            {
                foreach (var charEvent in _charEvents)
                {
                    if (text.Length >= maxLength)
                    {
                        break;
                    }

                    var rune = new System.Text.Rune((int)charEvent.CodePoint);
                    if (rune.Value < 0x20)
                    {
                        continue;
                    }

                    var insert = SanitizeText(rune.ToString(), true);
                    if (!string.IsNullOrEmpty(insert))
                    {
                        changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, insert, maxLength);
                    }
                }
            }
        }

        if (active)
        {
            _state.SetCursor(id, caretIndex);
        }

        if (active && (caretMoved || caretIndex != initialCaretIndex))
        {
            ResetCaretBlink();
        }

        _state.SetSelection(id, selection);

        if (changed && !historyChanged)
        {
            history.Push(text);
        }

        var dragSelecting = active && _leftMouseDown && !_leftMousePressed && _state.WidgetClickCount <= 1;
        var ensureCaretVisibility = !dragSelecting
            && active
            && (!string.Equals(_state.PreviousActiveId, id, StringComparison.Ordinal)
                || caretMoved
                || caretIndex != initialCaretIndex
                || changed);

        if (ensureCaretVisibility && maxScroll > 0f)
        {
            scrollY = EnsureCaretVisibleMultiline(text, caretIndex, scrollY, inputRect.Height - 8f, textLineHeight);
            scrollY = Math.Clamp(scrollY, 0f, maxScroll);
        }

        if (ensureCaretVisibility && maxScrollX > 0f)
        {
            scrollX = EnsureCaretVisibleHorizontal(text, caretIndex, scrollX, inputRect.Width - 12f);
            scrollX = Math.Clamp(scrollX, 0f, maxScrollX);
        }

        var textPos = new UiVector2(inputRect.X + 6f - scrollX, inputRect.Y + 4f - scrollY);
        if (active && selection.HasSelection)
        {
            DrawSelectionMultiline(text, selection, textPos.X, textPos.Y, textLineHeight, inputRect);
        }

        var compositionText = active ? _imeHandler?.GetCompositionText() : null;
        var compositionWidth = !string.IsNullOrEmpty(compositionText)
            ? MeasureTextWidth(compositionText, compositionText.Length)
            : 0f;

        if (!string.IsNullOrEmpty(compositionText))
        {
            var displayText = string.Concat(text.AsSpan(0, caretIndex), compositionText, text.AsSpan(caretIndex));
            AddTextMultilineInternal(_builder, displayText, textPos, _theme.Text, inputClip, _textSettings, textLineHeight, inputRect);
        }
        else
        {
            AddTextMultilineInternal(_builder, text, textPos, _theme.Text, inputClip, _textSettings, textLineHeight, inputRect);
        }

        if (active)
        {
            var (caretX, caretY) = GetCaretPositionMultiline(text, caretIndex, textPos.X, textPos.Y, textLineHeight);
            caretX = MathF.Round(caretX);
            caretY = MathF.Round(caretY);
            var drawCaretX = caretX;
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            var caretHeight = textLineHeight - 4f;
            GetImeFontMetrics(text, caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            _imeHandler?.SetCaretRect(new UiRect(caretX, caretY + 2f, caretWidth, caretHeight), inputRect, fontPixelHeight, fontPixelWidth);
            if (!string.IsNullOrEmpty(compositionText))
            {
                DrawImeCompositionUnderline(compositionText, caretX, caretY, inputRect, inputClip, textLineHeight);
                drawCaretX = MathF.Round(caretX + compositionWidth);
            }

            var caretRect = new UiRect(drawCaretX, caretY + 2f, caretWidth, caretHeight);
            var visibleCaretRect = IntersectRect(inputRect, caretRect);
            if (visibleCaretRect.Width > 0f && visibleCaretRect.Height > 0f && IsCaretBlinkOn())
            {
                _builder.AddRectFilled(caretRect, _theme.Text, _whiteTexture, inputClip);
            }
        }

        value = text;

        var barWidth = ScrollbarSize;
        var barHeight = ScrollbarSize;

        if (hasVScroll)
        {
            var barHeightAdjusted = inputRect.Height - (hasHScroll ? barHeight : 0f);
            var barRect = new UiRect(inputRect.X + inputRect.Width - barWidth, inputRect.Y, barWidth, barHeightAdjusted);
            scrollY = RenderScrollbarV($"{label}##scroll", barRect, scrollY, maxScroll, contentHeight, inputClip);
        }

        if (hasHScroll)
        {
            var barWidthAdjusted = inputRect.Width - (hasVScroll ? barWidth : 0f);
            var barRect = new UiRect(inputRect.X, inputRect.Y + inputRect.Height - barHeight, barWidthAdjusted, barHeight);
            scrollX = RenderScrollbarH($"{label}##scrollx", barRect, scrollX, maxScrollX, maxLineWidth, inputClip);
        }

        if (hasVScroll && hasHScroll)
        {
            var cornerRect = new UiRect(inputRect.X + inputRect.Width - barWidth, inputRect.Y + inputRect.Height - barHeight, barWidth, barHeight);
            _builder.AddRectFilled(cornerRect, _theme.ScrollbarBg, _whiteTexture, inputClip);
        }

        _state.SetScrollY(id, scrollY);
        _state.SetScrollX(id, scrollX);

        return changed;
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
            _theme.Text,
            inputClip,
            compositionTextSettings,
            _lineHeight
        );

        DrawImeCompositionUnderline(compositionText, startX, startY, inputRect, inputClip, _lineHeight);
    }

    private void DrawImeCompositionUnderline(string compositionText, float startX, float startY, UiRect inputRect, UiRect inputClip, float lineHeight)
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
        _builder.AddRectFilled(underlineRect, _theme.Text, _whiteTexture, inputClip);
    }
}

