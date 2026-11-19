// Frozen pre-extraction reference, compiled only with DuxelValidationHooks.
namespace Duxel.Core;
public sealed partial class UiImmediateContext
{
    internal bool InputTextLegacyReference(string label, ref string value, int maxLength)
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
        var inputTextLineHeight = GetTextLineHeight();
        var frameHeight = MathF.Max(GetFrameHeight(), _controlSkins.TextField.MinimumHeight);
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
                _theme.InputText,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        var inputRect = new UiRect(cursor.X + labelWidth, cursor.Y + (height - frameHeight) * 0.5f, inputWidth, frameHeight);
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
                var newlyFocused = _state.ActiveId != id;
                _state.ActiveId = id;
                _state.PendingTextInputFocusId = null;
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

                if (newlyFocused && _controlSkins.TextField.SelectAllOnFocus)
                {
                    selection = new UiTextSelection(0, text.Length);
                    caret = text.Length;
                }
                caretIndex = caret;
                caretMoved = true;
                _state.SetCursor(id, caretIndex);
            }
            else if (_state.ActiveId == id)
            {
                _state.ActiveId = null;
                _state.PendingTextInputFocusId = null;
                _imeHandler?.SetCompositionOwner(null);
            }
        }

        var active = _state.ActiveId == id;
        var borderColor = active ? _theme.InputBorderActive : hovered ? _theme.InputBorderHovered : _theme.InputBorder;
        if (active && _controlSkins.TextField.Appearance == UiTextFieldAppearance.Frame)
        {
            AddFocusRing(inputRect, _theme.InputBorderActive, _designTokens.InputCornerRadius);
        }

        var innerInputRect = DrawTextFieldFrame(inputRect, borderColor, active);

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
                    _state.PendingTextInputFocusId = null;
                    _imeHandler?.SetCompositionOwner(null);
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

        var singleLineTextOffsetY = MathF.Min(3f, MathF.Max(1f, _designTokens.ControlBorderWidth + 1f));
        var textPos = new UiVector2(inputRect.X + 6f - scrollX, inputRect.Y + ((inputRect.Height - inputTextLineHeight) * 0.5f) + singleLineTextOffsetY);
        if (active && selection.HasSelection)
        {
            var (start, end) = GetSelectionRange(selection);
            var startX = textPos.X + MeasureTextWidth(text, start);
            var endX = textPos.X + MeasureTextWidth(text, end);
            var selectionRect = new UiRect(startX, inputRect.Y + 2f, endX - startX, inputRect.Height - 4f);
            _builder.AddRectFilled(selectionRect, _theme.InputSelectionBg, _whiteTexture, inputClip);
        }

        if (!string.IsNullOrEmpty(compositionText))
        {
            if (caretIndex > 0)
            {
                AddTextInternal(_builder, text[..caretIndex], textPos, _theme.InputText, inputClip, _textSettings, _lineHeight);
            }
            if (caretIndex < text.Length)
            {
                var afterX = MathF.Round(textPos.X + MeasureTextWidth(text, caretIndex) + compositionWidth);
                AddTextInternal(_builder, text[caretIndex..], new UiVector2(afterX, textPos.Y), _theme.InputText, inputClip, _textSettings, _lineHeight);
            }
        }
        else
        {
            AddTextInternal(_builder, text, textPos, _theme.InputText, inputClip, _textSettings, _lineHeight);
        }

        if (active)
        {
            var caretX = MathF.Round(textPos.X + MeasureTextWidth(text, caretIndex));
            var drawCaretX = caretX;
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            var caretTop = MathF.Round(textPos.Y + 1f);
            var caretHeight = MathF.Max(1f, inputTextLineHeight - 2f);
            GetImeFontMetrics(text, caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            _imeHandler?.SetCaretRect(new UiRect(caretX, textPos.Y, caretWidth, inputTextLineHeight), inputRect, fontPixelHeight, fontPixelWidth);
            if (!string.IsNullOrEmpty(compositionText))
            {
                DrawImeCompositionInline(compositionText, caretX, textPos.Y, inputRect, inputClip);
                drawCaretX = MathF.Round(caretX + compositionWidth);
            }

            var caretLeft = Math.Clamp(drawCaretX, inputRect.X, inputRect.X + inputRect.Width - caretWidth);
            caretTop = Math.Clamp(caretTop, inputRect.Y, inputRect.Y + inputRect.Height - caretHeight);
            var caretRect = new UiRect(caretLeft, caretTop, caretWidth, caretHeight);
            if (IsCaretBlinkOn())
            {
                _builder.AddRectFilled(caretRect, _theme.InputText, _whiteTexture, inputClip);
            }
        }

        value = text;

        return changed;
    }

}
