namespace Dux.Core;

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

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var inputWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + inputWidth, height);
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

        var inputRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, inputWidth, frameHeight);
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        var hovered = ItemHoverable(id, inputRect);

        var caretIndex = ClampCaret(text, _state.GetCursor(id, text.Length));
        var initialCaretIndex = caretIndex;
        var caretMoved = false;
        var scrollX = _state.GetScrollX(id);
        if (_leftMousePressed)
        {
            if (hovered)
            {
                _state.ActiveId = id;
                var caret = GetCaretIndexFromMouse(text, inputRect.X + 6f - scrollX);
                var anchor = caretIndex;
                var shiftClick = (_state.Modifiers & KeyModifiers.Shift) != 0;
                selection = shiftClick ? new UiTextSelection(anchor, caret) : new UiTextSelection(caret, caret);
                caretIndex = caret;
                _state.SetCursor(id, caret);
                caretMoved = true;
                if (_state.RegisterClick(id, _mousePosition))
                {
                    SelectWordAt(text, ref caret, ref selection);
                    caretIndex = caret;
                    caretMoved = true;
                }
            }
            else if (_state.ActiveId == id)
            {
                _state.ActiveId = null;
            }
        }

        var active = _state.ActiveId == id;
        var background = active ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(inputRect, background, _whiteTexture);

        var changed = false;
        var historyChanged = false;
        selection = ClampSelection(selection, text.Length, caretIndex);
        if (active)
        {
            if (_leftMouseDown && hovered)
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

        var visibleWidth = inputRect.Width - 12f;
        if (active && visibleWidth > 0f)
        {
            var caretPixel = MeasureTextWidth(text, caretIndex);
            var maxScroll = MathF.Max(0f, MeasureTextWidth(text, text.Length) - visibleWidth);
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
        _builder.AddText(
            _fontAtlas,
            text,
            textPos,
            _theme.Text,
            _fontTexture,
            inputClip,
            _textSettings,
            _lineHeight
        );

        if (active)
        {
            var caretX = MathF.Round(textPos.X + MeasureTextWidth(text, caretIndex));
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            var caretLeft = Math.Clamp(caretX, inputRect.X, inputRect.X + inputRect.Width - caretWidth);
            var caretTop = Math.Clamp(inputRect.Y + 3f, inputRect.Y, inputRect.Y + inputRect.Height - (inputRect.Height - 6f));
            var caretRect = new UiRect(caretLeft, caretTop, caretWidth, inputRect.Height - 6f);
            GetImeFontMetrics(text, caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            _imeHandler?.SetCaretRect(caretRect, inputRect, fontPixelHeight, fontPixelWidth);
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
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
            var frameHeight = GetFrameHeight();
            var height = MathF.Max(textSize.Y, frameHeight);
            var cursor = _lastItemPos;
            var inputWidth = MathF.Max(0f, _lastItemSize.X - (textSize.X + ItemSpacingX));
            var inputRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (height - frameHeight) * 0.5f, inputWidth, frameHeight);

            var hintSize = UiTextBuilder.MeasureText(_fontAtlas, hint, _textSettings, _lineHeight);
            var hintPos = new UiVector2(inputRect.X + 6f, inputRect.Y + (inputRect.Height - hintSize.Y) * 0.5f);
            _builder.AddText(
                _fontAtlas,
                hint,
                hintPos,
                _theme.TextDisabled,
                _fontTexture,
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

        if (height <= _lineHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than a single line.");
        }

        var id = ResolveId(label);
        var buffer = _state.GetEditBuffer(id, value);
        var text = buffer.Text;
        var history = _state.GetHistory(id, text);
        var selection = _state.GetSelection(id);

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var inputWidth = ResolveItemWidth(InputWidth);
        var totalSize = new UiVector2(textSize.X + ItemSpacingX + inputWidth, height);
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + ButtonPaddingY);
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

        var inputRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y, inputWidth, height);
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        var hovered = ItemHoverable(id, inputRect);

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

        var contentHeight = lineCount * _lineHeight;
        var maxScroll = MathF.Max(0f, contentHeight - (inputRect.Height - 8f));
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f)
        {
            scrollY = Math.Clamp(scrollY - (_mouseWheel * _lineHeight * 3f), 0f, maxScroll);
        }
        var maxLineWidth = MeasureMaxLineWidth(text);
        var maxScrollX = MathF.Max(0f, maxLineWidth - (inputRect.Width - 12f));
        if (hovered && MathF.Abs(_mouseWheelHorizontal) > 0.001f && maxScrollX > 0f)
        {
            scrollX = Math.Clamp(scrollX - (_mouseWheelHorizontal * _lineHeight * 3f), 0f, maxScrollX);
        }
        if (_leftMousePressed)
        {
            if (hovered)
            {
                _state.ActiveId = id;
                var caret = GetCaretIndexFromMouseMultiline(text, inputRect.X + 6f - scrollX, inputRect.Y + 4f - scrollY, _lineHeight);
                var anchor = caretIndex;
                var shiftClick = (_state.Modifiers & KeyModifiers.Shift) != 0;
                selection = shiftClick ? new UiTextSelection(anchor, caret) : new UiTextSelection(caret, caret);
                caretIndex = caret;
                _state.SetCursor(id, caret);
                caretMoved = true;
                if (_state.RegisterClick(id, _mousePosition))
                {
                    SelectWordAt(text, ref caret, ref selection);
                    caretIndex = caret;
                    caretMoved = true;
                }
            }
            else if (_state.ActiveId == id)
            {
                _state.ActiveId = null;
            }
        }

        var active = _state.ActiveId == id;
        var background = active ? _theme.FrameBgActive : hovered ? _theme.FrameBgHovered : _theme.FrameBg;
        AddRectFilled(inputRect, background, _whiteTexture);

        var changed = false;
        var historyChanged = false;
        selection = ClampSelection(selection, text.Length, caretIndex);
        if (active)
        {
            if (_leftMouseDown && hovered)
            {
                var dragCaret = GetCaretIndexFromMouseMultiline(text, inputRect.X + 6f - scrollX, inputRect.Y + 4f - scrollY, _lineHeight);
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

            if (HandleRepeatMultiline(buffer, ref text, ref caretIndex, ref selection, inputRect.Height))
            {
                changed = true;
            }

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

        if (active && maxScroll > 0f)
        {
            scrollY = EnsureCaretVisibleMultiline(text, caretIndex, scrollY, inputRect.Height - 8f, _lineHeight);
            scrollY = Math.Clamp(scrollY, 0f, maxScroll);
        }

        if (active && maxScrollX > 0f)
        {
            scrollX = EnsureCaretVisibleHorizontal(text, caretIndex, scrollX, inputRect.Width - 12f);
            scrollX = Math.Clamp(scrollX, 0f, maxScrollX);
        }

        var textPos = new UiVector2(inputRect.X + 6f - scrollX, inputRect.Y + 4f - scrollY);
        if (active && selection.HasSelection)
        {
            DrawSelectionMultiline(text, selection, textPos.X, textPos.Y, _lineHeight, inputRect);
        }

        _builder.AddText(
            _fontAtlas,
            text,
            textPos,
            _theme.Text,
            _fontTexture,
            inputClip,
            _textSettings,
            _lineHeight
        );

        if (active)
        {
            var (caretX, caretY) = GetCaretPositionMultiline(text, caretIndex, textPos.X, textPos.Y, _lineHeight);
            caretX = MathF.Round(caretX);
            caretY = MathF.Round(caretY);
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            var caretHeight = _lineHeight - 4f;
            var caretLeft = Math.Clamp(caretX, inputRect.X, inputRect.X + inputRect.Width - caretWidth);
            var caretTop = Math.Clamp(caretY + 2f, inputRect.Y, inputRect.Y + inputRect.Height - caretHeight);
            var caretRect = new UiRect(caretLeft, caretTop, caretWidth, caretHeight);
            GetImeFontMetrics(text, caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            _imeHandler?.SetCaretRect(caretRect, inputRect, fontPixelHeight, fontPixelWidth);
            if (IsCaretBlinkOn())
            {
                _builder.AddRectFilled(caretRect, _theme.Text, _whiteTexture, inputClip);
            }
        }

        value = text;

        var hasVScroll = maxScroll > 0f;
        var hasHScroll = maxScrollX > 0f;
        var barWidth = 8f;
        var barHeight = 8f;

        if (hasVScroll)
        {
            var scrollId = $"{label}##scroll";
            var barHeightAdjusted = inputRect.Height - (hasHScroll ? barHeight : 0f);
            var barRect = new UiRect(inputRect.X + inputRect.Width - barWidth, inputRect.Y, barWidth, barHeightAdjusted);
            var handleHeight = MathF.Max(16f, barHeightAdjusted * (barHeightAdjusted / contentHeight));
            var handleY = inputRect.Y + (scrollY / maxScroll) * (barHeightAdjusted - handleHeight);
            var handleRect = new UiRect(barRect.X + 1f, handleY, barRect.Width - 2f, handleHeight);
            var handleHover = IsHovering(handleRect);

            if (_leftMousePressed && handleHover)
            {
                _state.ActiveId = scrollId;
            }

            var scrollActive = _state.ActiveId == scrollId;
            if (!_leftMouseDown && scrollActive)
            {
                _state.ActiveId = null;
            }

            if (scrollActive && _leftMouseDown)
            {
                var t = Math.Clamp((_mousePosition.Y - inputRect.Y - (handleHeight * 0.5f)) / (barHeightAdjusted - handleHeight), 0f, 1f);
                scrollY = t * maxScroll;
            }

            _builder.AddRectFilled(barRect, _theme.FrameBg, _whiteTexture, inputClip);
            var handleColor = scrollActive ? _theme.SliderGrabActive : handleHover ? _theme.SliderGrab : _theme.FrameBgActive;
            _builder.AddRectFilled(handleRect, handleColor, _whiteTexture, inputClip);
        }

        if (hasHScroll)
        {
            var scrollId = $"{label}##scrollx";
            var barWidthAdjusted = inputRect.Width - (hasVScroll ? barWidth : 0f);
            var barRect = new UiRect(inputRect.X, inputRect.Y + inputRect.Height - barHeight, barWidthAdjusted, barHeight);
            var handleWidth = MathF.Max(24f, barWidthAdjusted * (barWidthAdjusted / maxLineWidth));
            var handleX = inputRect.X + (scrollX / maxScrollX) * (barWidthAdjusted - handleWidth);
            var handleRect = new UiRect(handleX, barRect.Y + 1f, handleWidth, barRect.Height - 2f);
            var handleHover = IsHovering(handleRect);

            if (_leftMousePressed && handleHover)
            {
                _state.ActiveId = scrollId;
            }

            var scrollActive = _state.ActiveId == scrollId;
            if (!_leftMouseDown && scrollActive)
            {
                _state.ActiveId = null;
            }

            if (scrollActive && _leftMouseDown)
            {
                var t = Math.Clamp((_mousePosition.X - inputRect.X - (handleWidth * 0.5f)) / (barWidthAdjusted - handleWidth), 0f, 1f);
                scrollX = t * maxScrollX;
            }

            _builder.AddRectFilled(barRect, _theme.FrameBg, _whiteTexture, inputClip);
            var handleColor = scrollActive ? _theme.SliderGrabActive : handleHover ? _theme.SliderGrab : _theme.FrameBgActive;
            _builder.AddRectFilled(handleRect, handleColor, _whiteTexture, inputClip);
        }

        if (hasVScroll && hasHScroll)
        {
            var cornerRect = new UiRect(inputRect.X + inputRect.Width - barWidth, inputRect.Y + inputRect.Height - barHeight, barWidth, barHeight);
            _builder.AddRectFilled(cornerRect, _theme.FrameBg, _whiteTexture, inputClip);
        }

        _state.SetScrollY(id, scrollY);
        _state.SetScrollX(id, scrollX);

        return changed;
    }
}
