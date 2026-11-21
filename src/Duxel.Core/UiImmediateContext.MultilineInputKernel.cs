namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal UiMultilineEditResult ProcessMultilineInput(string resolvedId, ref string value, int maxLength,
        in UiMultilineInputLayout layout, bool navigable = false)
    {
        EnsureNotMeasuring();
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedId);
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        layout.Validate();
        var inputRect = layout.Field;
        var textLineHeight = layout.LineHeight;
        var content = layout.ContentBounds;
        var readOnly = layout.Options.ReadOnly;
        var normalizedValue = readOnly ? value : SanitizeText(value, true);
        var normalizedExistingValue = !string.Equals(value, normalizedValue, StringComparison.Ordinal);
        if (normalizedExistingValue)
        {
            value = normalizedValue;
        }

        var id = resolvedId;
        if (!IsControlEnabled && _state.ActiveId == id)
        {
            _state.ActiveId = null;
            _state.PendingTextInputFocusId = null;
            _imeHandler?.SetCompositionOwner(null);
        }
        var buffer = _state.GetEditBuffer(id, normalizedValue);
        var text = buffer.Text;
        var history = _state.GetHistory(id, text);
        var accessibilityChanged = ApplyAccessibilityValue(id, ref value, maxLength, readOnly, layout.Field, multiline: true);
        if (accessibilityChanged)
        {
            value = SanitizeText(value, true);
            buffer.Sync(value);
            text = buffer.Text;
            history.Push(text);
        }
        var selection = _state.GetSelection(id);

        var caretIndex = ClampCaret(text, _state.GetCursor(id, text.Length));
        var preedit = !readOnly && _state.ActiveId == id ? _imeHandler?.GetCompositionText() : null;
        var useVisualLines = layout.Wrap || readOnly || navigable || !string.IsNullOrEmpty(preedit);
        if (useVisualLines && preedit is not null)
            preedit = SanitizeText(preedit, true);
        var visual = useVisualLines ? layout.VisualLines ?? layout.InputOwner?.Lines ?? new UiVisualTextLines() : null;
        if (!useVisualLines)
            layout.InputOwner?.ClearLines();
        var initialVisualRevision = visual?.LayoutRevision ?? 0;
        if (visual is not null)
            content = PrepareMultilineVisual(layout, text, visual, caretIndex, preedit, pointerHitPhase: true);
        var hovered = ItemHoverable(id, inputRect);

        caretIndex = visual is not null ? visual.Snap(caretIndex) : buffer.SnapTextElement(caretIndex);
        ApplyAccessibilityTextSelection(id, buffer, layout.Field, ref caretIndex, ref selection, visual);
        var rangeEdited = ApplyAccessibilityTextEdit(id, buffer, ref text, ref caretIndex, ref selection, layout.Field, maxLength, readOnly, multiline: true);
        accessibilityChanged |= rangeEdited;
        if (rangeEdited && visual is not null)
            content = PrepareMultilineVisual(layout, text, visual, caretIndex, preedit, pointerHitPhase: true);
        var initialCaretIndex = caretIndex;
        var initialVisualLine = visual?.FindLine(caretIndex) ?? -1;
        var caretMoved = false;
        var scrollY = _state.GetScrollY(id);
        var scrollX = _state.GetScrollX(id);
        const float scrollActivationEpsilon = 0.5f;
        var visibleTextHeight = content.Height;
        var visibleTextWidth = content.Width;
        var contentHeight = visual is null ? (text.AsSpan().Count('\n') + 1) * textLineHeight
            : string.IsNullOrEmpty(preedit) ? visual.Height : visual.Composition.Height;
        if (!float.IsFinite(contentHeight))
            throw new InvalidOperationException("Multiline content height cannot be represented.");
        var rawMaxScroll = contentHeight - visibleTextHeight;
        var maxScroll = rawMaxScroll > scrollActivationEpsilon ? rawMaxScroll : 0f;
        scrollY = Math.Clamp(scrollY, 0, maxScroll);
        var wheelBlocked = _popupTierDepth == 0 && IsMouseOverAnyBlockingPopup();
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f && maxScroll > 0f && !wheelBlocked)
        {
            scrollY = ApplyScrollWheel(scrollY, maxScroll, textLineHeight * 3f, ref _mouseWheel);
        }
        var maxLineWidth = visual is null ? MeasureMaxLineWidth(text) : string.IsNullOrEmpty(preedit) ? visual.Width : visual.Composition.Width;
        bool HasUnwrappedOverflow() => visual is not null && (string.IsNullOrEmpty(preedit) ? visual : visual.Composition).HasUnwrappedOverflow;
        var rawMaxScrollX = layout.Wrap && !HasUnwrappedOverflow() ? 0 : maxLineWidth - visibleTextWidth;
        var maxScrollX = rawMaxScrollX > scrollActivationEpsilon ? rawMaxScrollX : 0f;
        scrollX = Math.Clamp(scrollX, 0, maxScrollX);
        if (hovered && MathF.Abs(_mouseWheelHorizontal) > 0.001f && maxScrollX > 0f && !wheelBlocked)
        {
            scrollX = ApplyScrollWheel(scrollX, maxScrollX, textLineHeight * 3f, ref _mouseWheelHorizontal);
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
        var textHovered = hovered && !interactionBlocked && IsHovering(textInteractionRect);
        var visibleText = IntersectRect(CurrentClipRect, textInteractionRect);
        var dragAvailable = IsControlEnabled && !_layoutPointerBlocked && !interactionBlocked
            && visibleText.Width > 0 && visibleText.Height > 0
            && _blockingKeyboardTier <= _popupTierDepth && !IsMouseOverHigherTierPopup(_popupTierDepth);
        if (!dragAvailable)
            _state.ReleaseTextDrag(id);
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
        var scrollbarHovered = hovered && !interactionBlocked && ((hasVScroll && IsHovering(verticalScrollbarRect)) || (hasHScroll && IsHovering(horizontalScrollbarRect)));

        if (_leftMousePressed && scrollbarHovered && _state.ActiveId == id)
        {
            _state.ActiveId = null;
        }

        if (_leftMousePressed)
        {
            if (textHovered)
            {
                var newlyFocused = _state.ActiveId != id;
                _state.ActiveId = id;
                if (dragAvailable)
                {
                    _state.FocusedId = id;
                    ObserveControlFocus(id);
                }
                _state.PendingTextInputFocusId = null;
                var caret = visual is null ? HitMultilineSourceElements(buffer, text, content.X - scrollX, content.Y - scrollY, textLineHeight)
                    : HitMultilineVisual(visual, caretIndex, preedit, _mousePosition.X - content.X + scrollX,
                        _mousePosition.Y - content.Y + scrollY, textLineHeight);
                var clickCount = _state.RegisterClick(id, _mousePosition);
                if (dragAvailable && _leftMouseDown && !_leftMouseReleased)
                    _state.BeginTextDrag(id);
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
                            var (lineStart, lineEnd) = visual?.HardLineRange(caret)
                                ?? (GetLineStartIndex(text, caret), GetLineEndIndex(text, caret));
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

                if (newlyFocused && (layout.Options.Flags & UiInputTextFlags.AutoSelectAll) != 0)
                {
                    selection = new(0, text.Length);
                    caret = text.Length;
                }
                caretIndex = caret;
                caretMoved = true;
                _state.SetCursor(id, caretIndex);
            }
            else if (!hovered && _state.ActiveId == id
                && (readOnly || !navigable || _fieldFocusedControlId != id && _accessibilityFocus != id))
            {
                _state.ActiveId = null;
                _state.PendingTextInputFocusId = null;
                _imeHandler?.SetCompositionOwner(null);
            }
        }

        var active = _state.ActiveId == id;
        var changed = normalizedExistingValue || accessibilityChanged;
        var historyChanged = false;
        selection = ClampSelection(selection, text.Length, caretIndex);
        NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual);

        var committedText = _imeHandler?.ConsumeCommittedText(id);
        if (!IsControlEnabled || readOnly)
            committedText = null;
        if (!string.IsNullOrEmpty(committedText))
        {
            var committedInsert = SanitizeText(committedText, true);
            if (!string.IsNullOrEmpty(committedInsert))
            {
                changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, committedInsert, maxLength);
                NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual, forward: true);
            }
        }

        var dragSelecting = false;
        if (active)
        {
            _imeHandler?.SetCompositionOwner(readOnly ? null : id);

            if (dragAvailable && _leftMouseDown && !_leftMousePressed
                && _state.ContinueTextDrag(id, out var deltaTime) && _state.WidgetClickCount <= 1)
            {
                dragSelecting = true;
                var visibleContent = IntersectRect(visibleText, content);
                var viewport = visibleContent is { Width: > 0, Height: > 0 } ? visibleContent : visibleText;
                var offset = ApplyTextDragScroll(viewport, new(scrollX, scrollY), new(maxScrollX, maxScroll), deltaTime, out var pointer);
                var autoScrolled = offset.X != scrollX || offset.Y != scrollY;
                (scrollX, scrollY) = (offset.X, offset.Y);

                var dragCaret = visual is null
                    ? HitMultilineSourceElements(buffer, text, content.X - scrollX + (_mousePosition.X - pointer.X),
                        content.Y - scrollY + (_mousePosition.Y - pointer.Y), textLineHeight)
                    : HitMultilineVisual(visual, caretIndex, preedit, pointer.X - content.X + scrollX,
                        pointer.Y - content.Y + scrollY, textLineHeight);
                selection = selection with
                {
                    End = dragCaret
                };
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

                if (_state.KeyRepeatSource == UiKeyRepeatSource.Engine && IsRepeatableKey(keyEvent.Key) && !_state.HasRepeatTime(keyEvent.Key))
                {
                    _state.SetRepeatTime(keyEvent.Key, _state.InputTimeSeconds + _keyRepeatDelaySeconds);
                }

                NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual, forward: changed);
                var ctrl = (keyEvent.Modifiers & KeyModifiers.Ctrl) != 0;
                var shift = (keyEvent.Modifiers & KeyModifiers.Shift) != 0;

                if (navigable && keyEvent.Key == UiKey.Tab)
                    continue;
                if (readOnly && (keyEvent.Key is UiKey.Backspace or UiKey.Delete or UiKey.Enter or UiKey.Tab
                    || ctrl && keyEvent.Key is UiKey.X or UiKey.V or UiKey.Z or UiKey.Y))
                    continue;
                if (keyEvent.Key is UiKey.LeftArrow or UiKey.RightArrow or UiKey.UpArrow or UiKey.DownArrow
                    or UiKey.Home or UiKey.End or UiKey.PageUp or UiKey.PageDown)
                    caretMoved = true;

                if (ctrl && keyEvent.Key == UiKey.A)
                {
                    caretMoved = true;
                    visual?.ClearAffinity();
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
                        var previous = ctrl ? buffer.SnapTextElement(FindWordBoundaryLeft(text, caretIndex))
                            : visual?.Previous(caretIndex) ?? buffer.PreviousTextElement(caretIndex);
                        buffer.Replace(previous, caretIndex - previous, string.Empty);
                        text = buffer.Text;
                        caretIndex = previous;
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
                        var next = ctrl ? buffer.SnapTextElement(FindWordBoundaryRight(text, caretIndex), forward: true)
                            : visual?.Next(caretIndex) ?? buffer.NextTextElement(caretIndex);
                        buffer.Replace(caretIndex, next - caretIndex, string.Empty);
                        text = buffer.Text;
                        changed = true;
                    }
                }
                else if (keyEvent.Key == UiKey.LeftArrow)
                {
                    var next = MoveMultilineHorizontal(buffer, text, caretIndex, selection, visual, ctrl, shift, right: false);
                    next = visual is not null ? visual.Snap(next) : buffer.SnapTextElement(next);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.RightArrow)
                {
                    var next = MoveMultilineHorizontal(buffer, text, caretIndex, selection, visual, ctrl, shift, right: true);
                    next = visual is not null ? visual.SnapUp(next) : buffer.SnapTextElement(next, forward: true);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.UpArrow)
                {
                    var next = visual is not null ? visual.MoveVertical(caretIndex, -1)
                        : shift ? GetShiftLineUpTarget(text, caretIndex) : MoveCaretVertical(text, caretIndex, -1);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.DownArrow)
                {
                    var next = visual is not null ? visual.MoveVertical(caretIndex, 1)
                        : shift ? GetShiftLineDownTarget(text, caretIndex) : MoveCaretVertical(text, caretIndex, 1);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Home)
                {
                    if (ctrl)
                        visual?.ClearAffinity();
                    var next = ctrl ? 0 : visual?.Home(caretIndex) ?? GetLineStartIndex(text, caretIndex);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.End)
                {
                    if (ctrl)
                        visual?.ClearAffinity();
                    var next = ctrl ? text.Length : visual?.End(caretIndex) ?? GetLineEndIndex(text, caretIndex);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Tab)
                {
                    var tabText = new string(' ', TabSpaces);
                    changed |= InsertText(ref buffer, ref text, ref caretIndex, ref selection, tabText, maxLength);
                }
                else if (keyEvent.Key == UiKey.PageUp)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((content.Height) / textLineHeight));
                    var next = visual?.StyledSource is not null ? visual.MovePage(caretIndex, -1, content.Height)
                        : visual?.MoveVertical(caretIndex, -visibleLines) ?? MoveCaretVertical(text, caretIndex, -visibleLines);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.PageDown)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((content.Height) / textLineHeight));
                    var next = visual?.StyledSource is not null ? visual.MovePage(caretIndex, 1, content.Height)
                        : visual?.MoveVertical(caretIndex, visibleLines) ?? MoveCaretVertical(text, caretIndex, visibleLines);
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
                else if (keyEvent.Key == UiKey.Escape && !navigable)
                {
                    _state.ActiveId = null;
                    _state.PendingTextInputFocusId = null;
                    _imeHandler?.SetCompositionOwner(null);
                }
            }

            NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual, forward: changed);
            if (HandleRepeatMultiline(buffer, ref text, ref caretIndex, ref selection,
                visual is null ? inputRect.Height : content.Height + 8, textLineHeight,
                out var repeatedNavigation, visual, readOnly))
            {
                changed = true;
            }
            caretMoved |= repeatedNavigation;
            NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual, forward: changed);

            var skipCharEventsForIme = _imeHandler is not null;
            if (!skipCharEventsForIme && !readOnly)
            {
                foreach (var charEvent in _charEvents)
                {
                    if (text.Length >= maxLength && !selection.HasSelection)
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
                        NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual, forward: true);
                    }
                }
            }
        }

        preedit = active && !readOnly ? _imeHandler?.GetCompositionText() : null;
        if (useVisualLines && preedit is not null)
            preedit = SanitizeText(preedit, true);
        if (visual is not null)
        {
            content = PrepareMultilineVisual(layout, text, visual, caretIndex, preedit);
        }
        NormalizeMultilineElements(buffer, ref caretIndex, ref selection, visual, forward: changed);

        if (active || changed)
        {
            _state.SetCursor(id, caretIndex);
        }

        if (active && (caretMoved || caretIndex != initialCaretIndex
            || visual is not null && visual.FindLine(caretIndex) != initialVisualLine))
        {
            ResetCaretBlink();
        }

        _state.SetSelection(id, selection);

        if (changed && !historyChanged)
        {
            history.Push(text);
        }

        if (changed || visual is not null)
        {
            visibleTextWidth = content.Width;
            visibleTextHeight = content.Height;
            contentHeight = visual is null ? (text.AsSpan().Count('\n') + 1) * textLineHeight
                : string.IsNullOrEmpty(preedit) ? visual.Height : visual.Composition.Height;
            if (!float.IsFinite(contentHeight))
                throw new InvalidOperationException("Multiline content height cannot be represented.");
            maxLineWidth = visual is null ? MeasureMaxLineWidth(text) : string.IsNullOrEmpty(preedit) ? visual.Width : visual.Composition.Width;
            rawMaxScroll = contentHeight - visibleTextHeight;
            rawMaxScrollX = layout.Wrap && !HasUnwrappedOverflow() ? 0 : maxLineWidth - visibleTextWidth;
            maxScroll = rawMaxScroll > scrollActivationEpsilon ? rawMaxScroll : 0;
            maxScrollX = rawMaxScrollX > scrollActivationEpsilon ? rawMaxScrollX : 0;
        }
        scrollY = Math.Clamp(scrollY, 0, maxScroll);
        scrollX = Math.Clamp(scrollX, 0, maxScrollX);

        var ensureCaretVisibility = !dragSelecting
            && active
            && (!string.Equals(_state.PreviousActiveId, id, StringComparison.Ordinal)
                || caretMoved
                || (visual is not null && visual.FindLine(caretIndex) != initialVisualLine)
                || (visual is not null && visual.LayoutRevision != initialVisualRevision)
                || caretIndex != initialCaretIndex
                || changed);

        if (ensureCaretVisibility && maxScroll > 0f)
        {
            if (visual is null)
                scrollY = EnsureCaretVisibleMultiline(text, caretIndex, scrollY, content.Height, textLineHeight);
            else
            {
                var caretLine = visual.FindLine(caretIndex);
                var y = visual.Y(caretLine);
                var caretLineHeight = visual.LineHeight(caretLine);
                if (y < scrollY)
                    scrollY = y;
                else if (y + caretLineHeight > scrollY + content.Height)
                    scrollY = y + caretLineHeight - content.Height;
            }
            scrollY = Math.Clamp(scrollY, 0f, maxScroll);
        }

        if (ensureCaretVisibility && maxScrollX > 0f)
        {
            scrollX = visual is null ? EnsureCaretVisibleHorizontal(text, caretIndex, scrollX, content.Width)
                : EnsureVisualCaretHorizontal(visual, caretIndex, scrollX, content.Width);
            scrollX = Math.Clamp(scrollX, 0f, maxScrollX);
        }

        var compositionText = preedit;
        var compositionWidth = !string.IsNullOrEmpty(compositionText)
            ? MeasureTextWidth(compositionText, compositionText.Length)
            : 0f;

        UiVisualTextLines? compositionLines = null;
        if (visual is not null && !string.IsNullOrEmpty(compositionText))
        {
            compositionLines = visual.Composition;
            var compositionLine = compositionLines.FindLine(caretIndex + compositionText.Length);
            var y = compositionLines.Y(compositionLine);
            var compositionLineHeight = compositionLines.LineHeight(compositionLine);
            if (y < scrollY)
                scrollY = y;
            else if (y + compositionLineHeight > scrollY + content.Height)
                scrollY = y + compositionLineHeight - content.Height;
            scrollY = Math.Clamp(scrollY, 0, maxScroll);
            if (!layout.Wrap || HasUnwrappedOverflow())
                scrollX = Math.Clamp(EnsureVisualCaretHorizontal(compositionLines,
                    caretIndex + compositionText.Length, scrollX, content.Width), 0, maxScrollX);
        }
        if (_state.ActiveId == id && _state.FocusedId == id && _ownedControlFocus == id)
            ObserveControlFocus(id);
        value = text;
        return new(id, text, selection, caretIndex, scrollX, scrollY, maxScrollX, maxScroll,
            maxLineWidth, contentHeight, compositionText, compositionWidth, active, hovered, changed)
        {
            AccessibilityText = CaptureAccessibilityText(id, text, caretIndex, selection.Start),
            VisualLines = visual,
            VisualVersion = visual?.Version ?? 0,
            VisualContent = visual is null ? null : content,
            CompositionLines = compositionLines,
            CompositionVersion = compositionLines?.Version ?? 0
        };
    }
    private void NormalizeMultilineElements(UiTextEditBuffer buffer, ref int caretIndex,
        ref UiTextSelection selection, UiVisualTextLines? visual, bool forward = false)
    {
        if (visual is null)
        {
            NormalizeSingleLineElements(buffer, ref caretIndex, ref selection, forward);
            return;
        }
        // The previous layout's active source offset may be outside a newly shortened buffer.
        visual.Refresh(this, buffer.Text, caretIndex);
        if (selection.HasSelection)
        {
            selection = visual.AlignSelection(selection);
            caretIndex = selection.End;
        }
        else
        {
            caretIndex = visual.SnapCaret(caretIndex, forward);
            selection = new(caretIndex, caretIndex);
        }
    }

    private int HitMultilineSourceElements(UiTextEditBuffer buffer, string text, float x, float y, float lineHeight)
    {
        var hit = GetCaretIndexFromMouseMultiline(text, x, y, lineHeight);
        var start = buffer.SnapTextElement(hit);
        var end = buffer.SnapTextElement(hit, forward: true);
        if (start == end)
            return hit;
        var line = GetLineStartIndex(text, start);
        var middle = (MeasureTextWidthSpan(text.AsSpan(line, start - line))
            + MeasureTextWidthSpan(text.AsSpan(line, end - line))) * 0.5f;
        return _mousePosition.X - x < middle ? start : end;
    }

    internal void PaintMultilineInput(in UiMultilineInputLayout layout, in UiMultilineEditResult edit,
        in UiInputTextPaint paint, UiColor? placeholderColor = null)
    {
        EnsureNotMeasuring();
        layout.Validate();
        var inputRect = layout.Field;
        var visual = edit.VisualLines;
        if (visual is not null && visual.Version != edit.VisualVersion)
            throw new InvalidOperationException("A wrapped edit snapshot cannot be painted after its line map changes.");
        var content = edit.VisualContent ?? layout.ContentBounds;
        var textLineHeight = layout.LineHeight;
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        if (visual is not null)
            inputClip = IntersectRect(inputClip, content);
        var text = edit.Text;
        var selection = edit.Selection;
        var caretIndex = edit.CaretIndex;
        var scrollX = edit.ScrollX;
        var scrollY = edit.ScrollY;
        var active = edit.Active;
        var compositionText = edit.CompositionText;
        if (edit.AccessibilityText is { } info && string.IsNullOrEmpty(compositionText))
        {
            if (visual?.StyledSource is not null)
                CaptureStyledTextPaint(edit.Id, info, visual, paint.Text);
            else
                CaptureAccessibilityTextAppearance(edit.Id, info, paint);
        }
        var compositionWidth = edit.CompositionWidth;
        var textPos = new UiVector2(content.X - scrollX, content.Y - scrollY);
        if (text.Length == 0 && string.IsNullOrEmpty(compositionText) && layout.Options.Placeholder is { Length: > 0 } placeholder)
        {
            var hint = SanitizeText(placeholder, true);
            var color = placeholderColor ?? paint.Text;
            if (visual is not null)
            {
                var lines = visual.Placeholder;
                lines.Prepare(this, hint, content.Width, content.Height, textLineHeight, 0, layout.Wrap);
                PaintVisualLines(lines, new(content.X, content.Y), textLineHeight, color, inputClip, paint.Typography);
            }
            else
                PaintMultilineRun(hint, new(content.X, content.Y), color,
                    IntersectRect(inputClip, content), layout, paint.Typography);
        }
        else
            visual?.ClearPlaceholder();
        var displayedLines = edit.CompositionLines ?? visual;
        if (edit.CompositionLines is { } compositionLines && compositionLines.Version != edit.CompositionVersion)
            throw new InvalidOperationException("An IME edit snapshot cannot be painted after its line map changes.");
        var compositionMap = new UiCompositionTextMap(caretIndex, compositionText?.Length ?? 0);
        var displayText = edit.CompositionLines is { } prepared ? prepared.Text
            : compositionMap.Length > 0 ? string.Concat(text.AsSpan(0, caretIndex), compositionText, text.AsSpan(caretIndex)) : text;
        var displaySelection = compositionMap.Display(selection);
        var styled = displayedLines?.StyledSource is not null;
        // Style backgrounds stay below selection; ordinary glyphs are painted once.
        if (styled && displayedLines!.StyledSource is not UiCodeDocument)
            PaintVisualLines(displayedLines!, textPos, textLineHeight, paint.Text, inputClip, paint.Typography,
                phase: UiStyledTextPaintPhase.Background);
        var selectedForeground = active && selection.HasSelection ? _theme.ForcedColors?.HighlightText : null;
        if (active && selection.HasSelection)
        {
            if (visual is null)
                DrawSelectionMultiline(displayText, displaySelection, textPos.X, textPos.Y, textLineHeight, inputRect, paint.Selection);
            else
                PaintVisualSelection(displayedLines!, displayText, displaySelection, textPos, textLineHeight, inputClip, paint.Selection);
        }

        if (visual is not null)
        {
            var displayed = edit.CompositionLines ?? visual;
            PaintVisualLines(displayed, textPos, textLineHeight, paint.Text, inputClip, paint.Typography,
                displaySelection, selectedForeground, styled ? UiStyledTextPaintPhase.Content : UiStyledTextPaintPhase.All);
            if (active && selection.HasSelection)
                PaintMarkdownImageSelection(displayed, selection, textPos, inputClip, paint.Selection,
                    caretIndex, compositionText?.Length ?? 0);
            if (edit.CompositionLines is not null)
                PaintVisualComposition(displayed, caretIndex, caretIndex + (compositionText?.Length ?? 0),
                    textPos, textLineHeight, inputClip, paint.Text);
        }
        else if (!string.IsNullOrEmpty(compositionText))
        {
            PaintMultilineRun(displayText, textPos, paint.Text, inputClip, layout, paint.Typography,
                displaySelection, selectedForeground);
        }
        else
        {
            PaintMultilineRun(text, textPos, paint.Text, inputClip, layout, paint.Typography,
                selection, selectedForeground);
        }

        if (active)
        {
            var position = visual is null
                ? default : VisualCaret(edit.CompositionLines ?? visual,
                    edit.CompositionLines is null ? caretIndex : caretIndex + (compositionText?.Length ?? 0), textPos, textLineHeight);
            var (caretX, caretY) = visual is null
                ? GetCaretPositionMultiline(text, caretIndex, textPos.X, textPos.Y, textLineHeight) : (position.X, position.Y);
            caretX = MathF.Round(caretX);
            caretY = MathF.Round(caretY);
            var drawCaretX = caretX;
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            var actualLineHeight = displayedLines is null ? textLineHeight
                : displayedLines.LineHeight(displayedLines.FindLine(edit.CompositionLines is null ? caretIndex : caretIndex + (compositionText?.Length ?? 0)));
            var caretHeight = actualLineHeight - 4f;
            if (visual is not null && !layout.Wrap && content.Width > 0
                && MathF.Abs(position.X - (content.X + content.Width)) <= 0.5f)
                drawCaretX = MathF.Min(drawCaretX, MathF.Max(content.X, content.X + content.Width - caretWidth));
            var caretSourceIndex = edit.CompositionLines is null ? caretIndex : caretIndex + (compositionText?.Length ?? 0);
            using var caretTypography = displayedLines?.StyledSource is null ? default
                : BeginStyledTypography(displayedLines.CaretStyle(caretSourceIndex));
            GetImeFontMetrics(text, caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            if (!layout.Options.ReadOnly)
                _imeHandler?.SetCaretRect(new UiRect(drawCaretX, caretY, caretWidth, actualLineHeight), inputRect, fontPixelHeight, fontPixelWidth);
            if (visual is null && !string.IsNullOrEmpty(compositionText))
            {
                DrawImeCompositionUnderline(compositionText, caretX, caretY, inputRect, inputClip, textLineHeight, paint.Text);
                drawCaretX = MathF.Round(caretX + compositionWidth);
            }

            var caretRect = new UiRect(drawCaretX, caretY + 2f, caretWidth, caretHeight);
            var visibleCaretRect = IntersectRect(inputClip, caretRect);
            if (visibleCaretRect.Width > 0f && visibleCaretRect.Height > 0f && IsCaretBlinkOn())
            {
                _builder.AddRectFilled(caretRect, paint.Caret, _whiteTexture, inputClip);
            }
        }

    }

    internal void CompleteMultilineInput(string label, in UiMultilineInputLayout layout, in UiMultilineEditResult edit,
        UiScrollOwner? owner = null)
    {
        var inputRect = layout.Field;
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        var hasVScroll = edit.MaximumScrollY > 0;
        var hasHScroll = edit.MaximumScrollX > 0;
        var scrollX = edit.ScrollX;
        var scrollY = edit.ScrollY;
        var maxScroll = edit.MaximumScrollY;
        var maxScrollX = edit.MaximumScrollX;
        var contentHeight = edit.ContentHeight;
        var maxLineWidth = edit.ContentWidth;
        var id = edit.Id;
        var barWidth = MathF.Min(ScrollbarSize, inputRect.Width);
        var barHeight = MathF.Min(ScrollbarSize, inputRect.Height);

        if (!hasVScroll || barWidth <= 0 || inputRect.Height - (hasHScroll ? barHeight : 0) <= 0)
            owner?.ReleaseAxis(horizontal: false);
        if (!hasHScroll || barHeight <= 0 || inputRect.Width - (hasVScroll ? barWidth : 0) <= 0)
            owner?.ReleaseAxis(horizontal: true);

        if (hasVScroll && barWidth > 0 && inputRect.Height - (hasHScroll ? barHeight : 0) > 0)
        {
            var barHeightAdjusted = inputRect.Height - (hasHScroll ? barHeight : 0f);
            var barRect = new UiRect(inputRect.X + inputRect.Width - barWidth, inputRect.Y, barWidth, barHeightAdjusted);
            owner?.UseAxis(horizontal: false);
            scrollY = RenderScrollbarV(owner?.VerticalId ?? $"{label}##scroll", barRect, scrollY, maxScroll, contentHeight, inputClip,
                interactive: IsControlEnabled);
        }

        if (hasHScroll && barHeight > 0 && inputRect.Width - (hasVScroll ? barWidth : 0) > 0)
        {
            var barWidthAdjusted = inputRect.Width - (hasVScroll ? barWidth : 0f);
            var barRect = new UiRect(inputRect.X, inputRect.Y + inputRect.Height - barHeight, barWidthAdjusted, barHeight);
            owner?.UseAxis(horizontal: true);
            scrollX = RenderScrollbarH(owner?.HorizontalId ?? $"{label}##scrollx", barRect, scrollX, maxScrollX, maxLineWidth, inputClip,
                interactive: IsControlEnabled);
        }

        if (hasVScroll && hasHScroll)
        {
            var cornerRect = new UiRect(inputRect.X + inputRect.Width - barWidth, inputRect.Y + inputRect.Height - barHeight, barWidth, barHeight);
            _builder.AddRectFilled(cornerRect, _theme.ScrollbarBg, _whiteTexture, inputClip);
        }

        _state.SetScrollY(id, scrollY);
        _state.SetScrollX(id, scrollX);

    }

    internal bool EditMultilineInput(string label, string resolvedId, ref string value, int maxLength,
        in UiMultilineInputLayout layout)
    {
        var edit = ProcessMultilineInput(resolvedId, ref value, maxLength, layout);
        var border = edit.Active ? _theme.InputBorderActive : edit.Hovered ? _theme.InputBorderHovered : _theme.InputBorder;
        DrawTextFieldFrame(layout.Field, border, edit.Active, multiline: true);
        PaintMultilineInput(layout, edit, new(_theme.InputText, _theme.InputSelectionBg, _theme.InputText));
        CompleteMultilineInput(label, layout, edit);
        return edit.Changed;
    }

    private void PaintMultilineRun(string text, UiVector2 position, UiColor color, UiRect clip,
        in UiMultilineInputLayout layout, bool typography, UiTextSelection selection = default,
        UiColor? selectedForeground = null)
    {
        if (!typography && !selectedForeground.HasValue)
        {
            AddTextMultilineInternal(_builder, text, position, color, clip, _textSettings,
                layout.LineHeight, _lineHeight, layout.Field);
            return;
        }
        var (lines, starts) = GetLineInfo(text);
        var (selectionStart, selectionEnd) = GetSelectionRange(selection);
        for (var line = 0; line < lines.Length; line++)
        {
            var y = position.Y + line * layout.LineHeight;
            if (y + layout.LineHeight > clip.Y && y < clip.Y + clip.Height)
            {
                var start = Math.Max(selectionStart, starts[line]) - starts[line];
                var end = Math.Min(selectionEnd, starts[line] + lines[line].Length) - starts[line];
                if (selectedForeground is { } selectedColor && end > start)
                {
                    var left = MeasureTextWidth(lines[line], start);
                    var right = MeasureTextWidth(lines[line], end);
                    PaintSelectedInputRun(lines[line], new(position.X, y), color, selectedColor,
                        IntersectRect(clip, new(clip.X, y, clip.Width, layout.LineHeight)), typography,
                        [new(left, MathF.Max(0, right - left))]);
                }
                else
                    PaintInputRun(lines[line], new(position.X, y), color, clip, typography);
            }
        }
    }
}
