namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool InputText<TBehavior, TPresentation>(string label, ref string value, int maxLength, float width = 0, UiTextInputOwner? owner = null)
        where TBehavior : IUiTextInputBehavior where TPresentation : IUiInputPresentation =>
        InputText<TBehavior, TPresentation>(label, label, ref value, maxLength, width, owner);

    internal bool InputText<TBehavior, TPresentation>(string id, string label, ref string value, int maxLength, float width, UiTextInputOwner? owner)
        where TBehavior : IUiTextInputBehavior where TPresentation : IUiInputPresentation =>
        InputText<TBehavior, TPresentation>(id, label, ref value, maxLength, width, owner, default);

    public bool InputText<TBehavior, TPresentation>(string label, ref string value, int maxLength, UiInputOptions options, float width = 0, UiTextInputOwner? owner = null)
        where TBehavior : IUiTextInputBehavior where TPresentation : IUiInputPresentation =>
        InputText<TBehavior, TPresentation>(label, label, ref value, maxLength, width, owner, options);

    public bool InputText<TBehavior, TPresentation>(string label, ref string value, int maxLength, UiInputOptions options, float width, bool constrainLayout, UiTextInputOwner? owner = null)
        where TBehavior : IUiTextInputBehavior where TPresentation : IUiInputPresentation =>
        InputText<TBehavior, TPresentation>(label, label, ref value, maxLength, width, owner, options, null, constrainLayout);

    internal bool InputText<TBehavior, TPresentation>(string id, string label, ref string value, int maxLength, float width, UiTextInputOwner? owner, UiInputOptions options, UiInputChildren? children = null, bool constrainLayout = false)
        where TBehavior : IUiTextInputBehavior where TPresentation : IUiInputPresentation
    {
        options.Validate();
        if (_currentField is { Invalid: true })
            options = options with
            {
                Invalid = true
            };
        var before = children?.Before;
        var after = children?.After;
        if ((before.HasValue || after.HasValue) && !TPresentation.SupportsChildren)
            throw new NotSupportedException("This input presentation does not support child slots.");
        if (before.HasValue && options.ContentBefore is not null || after.HasValue && options.ContentAfter is not null)
            throw new InvalidOperationException("A slot cannot contain both text data and an owned child.");
        label ??= "Input";
        id ??= label;
        value ??= string.Empty;
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        owner?.Validate();
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        var displayLabel = GetDisplayLabel(label);
        var local = MeasureInputLocal<TPresentation>(context, displayLabel, width, options, children, constrainLayout);
        var position = AdvanceCursor(local.Size);
        var layout = TPresentation.Place(context, local, position) with
        {
            Options = options
        };
        layout.Validate();
        if (constrainLayout && layout.Size != local.Size)
            throw new InvalidOperationException("Input placement changed the assigned control size.");
        var resolvedId = ResolveId(id);
        owner?.Bind(this, resolvedId);
        ClaimAccessibilityField(resolvedId);
        var accessibilityParent = _accessibilityParent;
        _accessibilityParent = resolvedId;
        try
        {
            ClaimFieldFocus(resolvedId, layout.EditingBounds);
            TPresentation.PaintBeforeEdit(context, displayLabel, layout);
            if (!before.HasValue && !after.HasValue)
            {
                var plain = TBehavior.Process(context, resolvedId, ref value, maxLength, layout);
                TPresentation.Paint(context, displayLabel, layout, plain);
                ExposeAccessibility(resolvedId, accessibilityParent, UiAccessibilityRole.Edit, label, layout.Field, value, (options.Flags & UiInputTextFlags.ReadOnly) != 0, maxLength, TBehavior.AccessibilityFocus, TBehavior.AccessibilityValue,
                    textInfo: TBehavior.AccessibilityValue ? plain.AccessibilityText : null,
                    textLayout: TBehavior.AccessibilityValue ? CaptureInputTextLayout(resolvedId, plain.AccessibilityText, layout, plain) : null);
                return plain.Changed;
            }
            var deferred = BeginInputSlotCapture();
            var completed = false;
            try
            {
                var beforeFocused = before is { } beforeSlot && RenderAllocatedChild(deferred, beforeSlot.Content, layout.Before!.Value, layout.Field);
                var edit = TBehavior.Process(context, resolvedId, ref value, maxLength, layout);
                var afterFocused = after is { } afterSlot && RenderAllocatedChild(deferred, afterSlot.Content, layout.After!.Value, layout.Field);
                var focusWithin = beforeFocused || afterFocused;
                edit = edit with
                {
                    FocusWithin = focusWithin,
                    Held = edit.Held || (focusWithin && _leftMouseDown)
                };
                TPresentation.Paint(context, displayLabel, layout, edit);
                ExposeAccessibility(resolvedId, accessibilityParent, UiAccessibilityRole.Edit, label, layout.Field, value, (options.Flags & UiInputTextFlags.ReadOnly) != 0, maxLength, TBehavior.AccessibilityFocus, TBehavior.AccessibilityValue,
                    textInfo: TBehavior.AccessibilityValue ? edit.AccessibilityText : null,
                    textLayout: TBehavior.AccessibilityValue ? CaptureInputTextLayout(resolvedId, edit.AccessibilityText, layout, edit) : null);
                _builder.AppendDeferredFrom(deferred);
                completed = true;
                return edit.Changed;
            }
            finally { EndInputSlotCapture(deferred, completed); }
        }
        finally { _accessibilityParent = accessibilityParent; }
    }
    internal bool LegacyInputSelectAllOnFocus => _controlSkins.TextField.SelectAllOnFocus;
    internal UiInputLayout MeasureContextInput(string displayLabel, float requestedWidth)
    {
        var textSize = MeasureTextInternal(displayLabel, _textSettings, _lineHeight);
        var inputTextLineHeight = GetTextLineHeight();
        var frameHeight = MathF.Max(GetFrameHeight(), _controlSkins.TextField.MinimumHeight);
        var height = MathF.Max(textSize.Y, frameHeight);
        var hasLabel = !string.IsNullOrEmpty(displayLabel);
        var labelWidth = hasLabel ? textSize.X + ItemSpacingX : 0f;
        var availableWidth = GetContentRegionAvail().X;
        var defaultInputWidth = float.IsFinite(availableWidth) && availableWidth > 0f
            ? MathF.Max(1f, availableWidth - labelWidth)
            : InputWidth;
        var inputWidth = ResolveItemWidth(requestedWidth > 0 ? requestedWidth : defaultInputWidth);
        var totalSize = new UiVector2(labelWidth + inputWidth, height);
        var field = new UiRect(labelWidth, (height - frameHeight) * .5f, inputWidth, frameHeight);
        var offset = MathF.Min(3f, MathF.Max(1f, _designTokens.ControlBorderWidth + 1f));
        return new(totalSize, field, new(field.X + 6, field.Y + (frameHeight - inputTextLineHeight) * .5f + offset, inputWidth - 12, inputTextLineHeight),
            new(0, (height - textSize.Y) * .5f), inputTextLineHeight);
    }
    internal UiInputLayout ArrangeContextInput(in UiInputLayout local, UiVector2 allocatedSize)
    {
        var field = local.Field with
        {
            Y = 0,
            Width = MathF.Max(0, allocatedSize.X - local.Field.X),
            Height = allocatedSize.Y
        };
        var offset = MathF.Min(3f, MathF.Max(1f, _designTokens.ControlBorderWidth + 1f));
        return local with
        {
            Size = allocatedSize,
            Field = field,
            Content = new(field.X + 6, (field.Height - local.LineHeight) / 2 + offset, MathF.Max(0, field.Width - 12), local.LineHeight),
            LabelPosition = local.LabelPosition with
            {
                Y = local.LabelPosition.Y + (allocatedSize.Y - local.Size.Y) / 2
            }
        };
    }
    internal UiInputLayout PlaceContextInput(in UiInputLayout local, UiVector2 origin)
    {
        var placed = local.At(origin);
        var field = placed.Field;
        var offset = MathF.Min(3f, MathF.Max(1f, _designTokens.ControlBorderWidth + 1f));
        return placed with
        {
            Content = new(field.X + 6f, field.Y + ((field.Height - local.LineHeight) * .5f) + offset, field.Width - 12f, local.LineHeight)
        };
    }
    internal UiInputEditResult EditSingleLineInput(string id, ref string value, int maxLength, in UiInputLayout layout, bool selectAllOnFocus)
        => EditSingleLineInput(id, ref value, maxLength, layout, selectAllOnFocus, false);

    internal UiInputEditResult EditSingleLineInput(string id, ref string value, int maxLength, in UiInputLayout layout, bool selectAllOnFocus, bool keepFocus)
    {
        EnsureNotMeasuring();
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (_accessibility?.Take(id, UiAccessibilityAction.Focus) is { } focusRequest)
        {
            ApplyAccessibilityFocus(id, layout.EditingBounds, focusRequest);
            if (focusRequest.Result == UiAccessibilityResult.Success)
            {
                _accessibility.ClearInputTextScroll(id);
                _state.ActiveId = id;
                ResetCaretBlink();
            }
        }
        var readOnly = (layout.Options.Flags & UiInputTextFlags.ReadOnly) != 0;
        selectAllOnFocus |= (layout.Options.Flags & UiInputTextFlags.AutoSelectAll) != 0;
        var buffer = _state.GetEditBuffer(id, value);
        var text = buffer.Text;
        var history = _state.GetHistory(id, text);
        var accessibilityChanged = ApplyAccessibilityValue(id, ref value, maxLength, readOnly, layout.Field);
        if (accessibilityChanged)
        {
            buffer.Sync(value);
            text = buffer.Text;
            history.Push(text);
        }
        var selection = _state.GetSelection(id);

        var inputRect = layout.EditingBounds;
        var hovered = ItemHoverable(id, inputRect);
        var visibleInput = IntersectRect(CurrentClipRect, inputRect);
        var dragAvailable = IsControlEnabled && !_layoutPointerBlocked
            && visibleInput.Width > 0 && visibleInput.Height > 0 && layout.Content.Width > 0
            && _blockingKeyboardTier <= _popupTierDepth
            && !IsMouseOverHigherTierPopup(_popupTierDepth)
            && (_popupTierDepth != 0 || !IsMouseOverAnyOpenMenuPopup());
        if (!dragAvailable)
            _state.ReleaseTextDrag(id);
        var caretIndex = buffer.SnapTextElement(ClampCaret(text, _state.GetCursor(id, text.Length)));
        ApplyAccessibilityTextSelection(id, buffer, layout.Field, ref caretIndex, ref selection);
        accessibilityChanged |= ApplyAccessibilityTextEdit(id, buffer, ref text, ref caretIndex, ref selection, layout.EditingBounds, maxLength, readOnly);
        var pointerPreedit = _state.ActiveId == id && !readOnly ? _imeHandler?.GetCompositionText() : null;
        var pointerComposition = string.IsNullOrEmpty(pointerPreedit) ? null
            : buffer.SingleLine.PrepareComposition(this, text, caretIndex, pointerPreedit);
        var initialCaretIndex = caretIndex;
        var caretMoved = false;
        var scrollX = _state.GetScrollX(id);
        var initialScrollX = scrollX;
        if (_leftMousePressed)
        {
            if (hovered)
            {
                var newlyFocused = _state.ActiveId != id;
                _state.ActiveId = id;
                if (dragAvailable)
                {
                    _state.FocusedId = id;
                    ObserveControlFocus(id);
                }
                _state.PendingTextInputFocusId = null;
                var caret = GetSingleLineElementHit(buffer, text, layout.Content.X - scrollX, pointerComposition);
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
                        case >= 3:
                            selection = new UiTextSelection(0, text.Length);
                            caret = text.Length;
                            break;
                        default:
                            selection = new UiTextSelection(caret, caret);
                            break;
                    }
                }

                if (newlyFocused && selectAllOnFocus)
                {
                    selection = new UiTextSelection(0, text.Length);
                    caret = text.Length;
                }
                caretIndex = caret;
                caretMoved = true;
                _state.SetCursor(id, caretIndex);
            }
            else if (_state.ActiveId == id && _fieldFocusedControlId != id && _accessibilityFocus != id)
            {
                _state.ActiveId = null;
                _state.PendingTextInputFocusId = null;
                _imeHandler?.SetCompositionOwner(null);
            }
        }

        var active = _state.ActiveId == id;
        if (active && !IsControlEnabled)
        {
            _state.ActiveId = null;
            _state.PendingTextInputFocusId = null;
            _imeHandler?.SetCompositionOwner(null);
            active = false;
        }
        var changed = accessibilityChanged;
        var historyChanged = false;
        selection = ClampSelection(selection, text.Length, caretIndex);
        NormalizeSingleLineElements(buffer, ref caretIndex, ref selection);

        var committedText = _imeHandler?.ConsumeCommittedText(id);
        if (IsControlEnabled && !readOnly && !string.IsNullOrEmpty(committedText))
        {
            var committedInsert = SanitizeText(committedText, false);
            if (!string.IsNullOrEmpty(committedInsert))
            {
                if (InsertText(ref buffer, ref text, ref caretIndex, ref selection, committedInsert, maxLength))
                {
                    changed = true;
                    CompleteSingleLineInsertion(buffer, text, ref caretIndex, ref selection);
                }
            }
        }

        var dragging = false;
        if (active)
        {
            _imeHandler?.SetCompositionOwner(readOnly ? null : id);

            if (dragAvailable && _leftMouseDown && !_leftMousePressed
                && _state.ContinueTextDrag(id, out var dragDelta) && _state.WidgetClickCount <= 1)
            {
                dragging = true;
                var left = layout.Content.X;
                buffer.SingleLine.Prepare(this, text);
                var dragLayout = pointerComposition?.Layout.Geometry is not null ? pointerComposition.Layout : buffer.SingleLine;
                var maxScroll = MathF.Max(0, (dragLayout.Geometry is not null ? dragLayout.Width
                    : MeasureTextWidth(text, text.Length)) - layout.Content.Width);
                scrollX = Math.Clamp(scrollX, 0, maxScroll);
                var visibleContent = IntersectRect(CurrentClipRect, layout.Content);
                var viewport = visibleContent is { Width: > 0, Height: > 0 } ? visibleContent : visibleInput;
                scrollX = ApplyTextDragScroll(viewport, new(scrollX, 0), new(maxScroll, 0), dragDelta, out var pointer).X;
                var dragCaret = GetSingleLineElementHit(buffer, text, left - scrollX, pointer.X, pointerComposition);
                selection = selection with
                {
                    End = dragCaret
                };
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
                if (keyEvent.Key is UiKey.Home or UiKey.End or UiKey.PageUp or UiKey.PageDown)
                    buffer.SingleLine.ClearAffinity();
                NormalizeSingleLineElements(buffer, ref caretIndex, ref selection, forward: changed);
                if (keyEvent.Key is UiKey.LeftArrow or UiKey.RightArrow or UiKey.Home or UiKey.End or UiKey.PageUp or UiKey.PageDown)
                    caretMoved = true;

                if (ctrl && keyEvent.Key == UiKey.A)
                {
                    buffer.SingleLine.ClearAffinity();
                    caretMoved = true;
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

                if (readOnly && ((ctrl && keyEvent.Key is UiKey.X or UiKey.V or UiKey.Z or UiKey.Y) || keyEvent.Key is UiKey.Backspace or UiKey.Delete))
                    continue;

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
                        if (InsertText(ref buffer, ref text, ref caretIndex, ref selection, paste, maxLength))
                        {
                            changed = true;
                            CompleteSingleLineInsertion(buffer, text, ref caretIndex, ref selection);
                        }
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
                            : buffer.PreviousTextElement(caretIndex);
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
                            : buffer.NextTextElement(caretIndex);
                        buffer.Replace(caretIndex, next - caretIndex, string.Empty);
                        text = buffer.Text;
                        changed = true;
                    }
                }
                else if (keyEvent.Key == UiKey.LeftArrow)
                {
                    var next = MoveSingleLineHorizontal(buffer, text, caretIndex, selection, ctrl, shift, right: false);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.RightArrow)
                {
                    var next = MoveSingleLineHorizontal(buffer, text, caretIndex, selection, ctrl, shift, right: true);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Home)
                {
                    var next = ctrl ? 0 : buffer.SnapTextElement(GetLineStartIndex(text, caretIndex));
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.End)
                {
                    var next = ctrl ? text.Length : buffer.SnapTextElement(GetLineEndIndex(text, caretIndex));
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.PageUp)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((inputRect.Height - 8f) / _lineHeight));
                    var next = buffer.SnapTextElement(MoveCaretVertical(text, caretIndex, -visibleLines));
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.PageDown)
                {
                    var visibleLines = Math.Max(1, (int)MathF.Floor((inputRect.Height - 8f) / _lineHeight));
                    var next = buffer.SnapTextElement(MoveCaretVertical(text, caretIndex, visibleLines), forward: true);
                    ApplyCaretMove(ref caretIndex, ref selection, next, shift);
                }
                else if (keyEvent.Key == UiKey.Enter || keyEvent.Key == UiKey.Escape)
                {
                    if (!keepFocus)
                    {
                        _state.ActiveId = null;
                        _state.PendingTextInputFocusId = null;
                        _imeHandler?.SetCompositionOwner(null);
                    }
                }
            }

            NormalizeSingleLineElements(buffer, ref caretIndex, ref selection, forward: changed);
            if (HandleRepeatSingleLine(buffer, ref text, ref caretIndex, ref selection, out var repeatedNavigation, readOnly))
            {
                changed = true;
            }
            caretMoved |= repeatedNavigation;

            var skipCharEventsForIme = _imeHandler is not null;
            if (!readOnly && !skipCharEventsForIme)
            {
                var insertedCharacters = false;
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

                    var insert = SanitizeText(rune.ToString(), false);
                    if (!string.IsNullOrEmpty(insert))
                    {
                        if (InsertText(ref buffer, ref text, ref caretIndex, ref selection, insert, maxLength))
                        {
                            changed = true;
                            insertedCharacters = true;
                            NormalizeSingleLineElements(buffer, ref caretIndex, ref selection, forward: true);
                        }
                    }
                }
                if (insertedCharacters)
                    CompleteSingleLineInsertion(buffer, text, ref caretIndex, ref selection);
            }
        }

        NormalizeSingleLineElements(buffer, ref caretIndex, ref selection, forward: changed);
        if (active || changed)
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

        var compositionText = active && !readOnly ? _imeHandler?.GetCompositionText() : null;
        var compositionWidth = !string.IsNullOrEmpty(compositionText)
            ? MeasureTextWidth(compositionText, compositionText.Length)
            : 0f;

        var visibleWidth = layout.Content.Width;
        var lineLayout = buffer.SingleLine;
        lineLayout.Prepare(this, text);
        var compositionLayout = string.IsNullOrEmpty(compositionText) ? null
            : lineLayout.PrepareComposition(this, text, caretIndex, compositionText);
        if (compositionLayout is null)
            lineLayout.ClearComposition();
        var nativeComposition = compositionLayout?.Layout.Geometry is not null;
        var keepAccessibilityScroll = _accessibility?.KeepInputTextScroll(id, text, caretIndex, selection.Start,
            visibleWidth, scrollX, LayoutMeasurementKey, active,
            !string.IsNullOrEmpty(compositionText) || active && (caretMoved || changed || _leftMousePressed || _keyEvents.Any(key => key.IsDown))) == true;
        var caretPixel = active && visibleWidth > 0f
            ? nativeComposition ? compositionLayout!.CaretX : (lineLayout.Geometry is not null && string.IsNullOrEmpty(compositionText)
                ? lineLayout.X(caretIndex) : MeasureTextWidth(text, caretIndex)) + compositionWidth : 0f;
        var maxInputScroll = active || scrollX != 0
            ? MathF.Max(0f, (nativeComposition ? compositionLayout!.Layout.Width : (lineLayout.Geometry is not null && string.IsNullOrEmpty(compositionText)
                ? lineLayout.Width : MeasureTextWidth(text, text.Length)) + compositionWidth) - visibleWidth) : 0f;
        scrollX = Math.Clamp(scrollX, 0f, maxInputScroll);
        if (active && visibleWidth > 0f)
        {
            if (!dragging && !keepAccessibilityScroll && caretPixel - scrollX > visibleWidth)
            {
                scrollX = MathF.Min(maxInputScroll, caretPixel - visibleWidth + 4f);
            }
            else if (!dragging && !keepAccessibilityScroll && caretPixel - scrollX < 0f)
            {
                scrollX = MathF.Max(0f, caretPixel - 4f);
            }
            scrollX = Math.Clamp(scrollX, 0f, maxInputScroll);
        }
        if (active || scrollX != initialScrollX)
            _state.SetScrollX(id, scrollX);

        if (_state.ActiveId == id && _state.FocusedId == id && _ownedControlFocus == id)
            ObserveControlFocus(id);
        value = text;
        return ApplyInputAccessibilityScroll(id, layout, new(id, text, selection, caretIndex, scrollX, compositionText, compositionWidth, active, hovered, changed)
        {
            LineLayout = lineLayout.Geometry is not null ? lineLayout : null,
            CompositionLayout = nativeComposition ? compositionLayout : null,
            Held = active && _leftMouseDown,
            AccessibilityText = CaptureAccessibilityText(id, text, caretIndex, selection.Start)
        });
    }
    private void CompleteSingleLineInsertion(UiTextEditBuffer buffer, string text, ref int caretIndex, ref UiTextSelection selection)
    {
        NormalizeSingleLineElements(buffer, ref caretIndex, ref selection, forward: true);
        buffer.SingleLine.Prepare(this, text);
        buffer.SingleLine.RememberTrailingCaret(caretIndex);
    }

    private static void NormalizeSingleLineElements(UiTextEditBuffer buffer, ref int caretIndex,
        ref UiTextSelection selection, bool forward = false)
    {
        if (selection.HasSelection)
        {
            selection = buffer.AlignTextElements(selection);
            caretIndex = selection.End;
        }
        else
        {
            caretIndex = buffer.SnapTextElement(caretIndex, forward);
            selection = new(caretIndex, caretIndex);
        }
    }

    private int GetSingleLineElementHit(UiTextEditBuffer buffer, string text, float origin, UiSingleLineComposition? composition = null)
        => GetSingleLineElementHit(buffer, text, origin, _mousePosition.X, composition);

    private int GetSingleLineElementHit(UiTextEditBuffer buffer, string text, float origin, float pointerX, UiSingleLineComposition? composition = null)
    {
        if (composition?.Layout.Geometry is not null)
            return buffer.SnapTextElement(composition.Source(composition.Layout.Hit(pointerX - origin)));
        var layout = buffer.SingleLine;
        layout.Prepare(this, text);
        if (layout.Geometry is not null)
            return layout.Hit(pointerX - origin);
        var hit = GetCaretIndexFromText(text.AsSpan(), MathF.Max(0, pointerX - origin));
        var start = buffer.SnapTextElement(hit);
        var end = buffer.SnapTextElement(hit, forward: true);
        if (start == end)
            return hit;
        var middle = (MeasureTextWidth(text, start) + MeasureTextWidth(text, end)) * 0.5f;
        return pointerX - origin < middle ? start : end;
    }

    internal void PaintContextInputLabel(string label, in UiInputLayout layout)
    {
        if (label.Length != 0)
            AddTextInternal(_builder, label, layout.LabelPosition, _theme.InputText, CurrentClipRect, _textSettings, _lineHeight);
    }
    internal void PaintContextInput(in UiInputLayout layout, in UiInputEditResult edit)
    {
        var inputRect = layout.Field;
        var active = edit.Active;
        var hovered = edit.Hovered;
        var borderColor = active ? _theme.InputBorderActive : hovered ? _theme.InputBorderHovered : _theme.InputBorder;
        if (active && _controlSkins.TextField.Appearance == UiTextFieldAppearance.Frame)
        {
            AddFocusRing(inputRect, _theme.InputBorderActive, _designTokens.InputCornerRadius);
        }

        var innerInputRect = DrawTextFieldFrame(inputRect, borderColor, active);

        PaintEditableInputText(layout, edit, new(_theme.InputText, _theme.InputSelectionBg, _theme.InputText));
    }
    internal void PaintEditableInputText(in UiInputLayout layout, in UiInputEditResult edit, in UiInputTextPaint paint)
    {
        if (edit.AccessibilityText is { } info && string.IsNullOrEmpty(edit.CompositionText))
            CaptureAccessibilityTextAppearance(edit.Id, info, paint);
        var inputRect = layout.EditingBounds;
        var inputClip = IntersectRect(CurrentClipRect, inputRect);
        if (layout.Editing.HasValue && (inputRect.Width <= 0 || inputRect.Height <= 0))
            return;
        var inputTextLineHeight = layout.LineHeight;
        var text = edit.Text;
        var lineLayout = edit.LineLayout;
        lineLayout?.Prepare(this, text);
        if (lineLayout?.Geometry is null)
            lineLayout = null;
        var active = edit.Active;
        var selection = edit.Selection;
        var caretIndex = edit.CaretIndex;
        var compositionText = edit.CompositionText;
        var compositionWidth = edit.CompositionWidth;
        var composition = edit.CompositionLayout;
        if (composition is not null && !string.IsNullOrEmpty(compositionText))
            composition.Prepare(this, text, caretIndex, compositionText);
        var nativeComposition = composition?.Layout.Geometry is not null;
        var displayLayout = nativeComposition ? composition!.Layout : lineLayout;
        var compositionMap = new UiCompositionTextMap(caretIndex, compositionText?.Length ?? 0);
        var displayText = nativeComposition ? composition!.Text
            : active && selection.HasSelection && compositionMap.Length > 0
                ? string.Concat(text.AsSpan(0, caretIndex), compositionText, text.AsSpan(caretIndex)) : text;
        var textPos = new UiVector2(layout.Content.X - edit.ScrollX, layout.Content.Y);
        var selectedForeground = active && selection.HasSelection ? _theme.ForcedColors?.HighlightText : null;
        PlatformTextSelectionFragment[]? selectedFragments = null;
        var selectedScale = 1f;
        if (active && selection.HasSelection)
        {
            var (start, end) = GetSelectionRange(selection);
            var top = paint.Typography ? layout.Content.Y : inputRect.Y + 2f;
            var height = paint.Typography ? layout.LineHeight : inputRect.Height - 4f;
            if (displayLayout is { Geometry: { } geometry } native)
            {
                selectedFragments = geometry.SelectionFragments(nativeComposition ? composition!.Display(start) : start,
                    nativeComposition ? composition!.Display(end, true) : end);
                selectedScale = native.Scale;
                foreach (var fragment in selectedFragments)
                    _builder.AddRectFilled(new(textPos.X + fragment.Left * native.Scale, top, fragment.Width * native.Scale, height),
                        paint.Selection, _whiteTexture, inputClip);
            }
            else
            {
                var startX = textPos.X + MeasureTextWidth(displayText, compositionMap.Display(start));
                var endX = textPos.X + MeasureTextWidth(displayText, compositionMap.Display(end, end: true));
                _builder.AddRectFilled(new(startX, top, endX - startX, height), paint.Selection, _whiteTexture, inputClip);
                if (selectedForeground.HasValue)
                    selectedFragments = [new(startX - textPos.X, endX - startX)];
            }
        }

        if (selectedForeground is { } selectedColor)
            PaintSelectedInputRun(displayText, textPos, paint.Text, selectedColor, inputClip, paint.Typography,
                selectedFragments, selectedScale);
        else if (nativeComposition)
            PaintInputRun(composition!.Text, textPos, paint.Text, inputClip, paint.Typography);
        else if (!string.IsNullOrEmpty(compositionText))
        {
            if (caretIndex > 0)
            {
                PaintInputRun(text[..caretIndex], textPos, paint.Text, inputClip, paint.Typography);
            }
            if (caretIndex < text.Length)
            {
                var afterX = InputCompositionSuffixX(textPos.X, text, caretIndex, compositionWidth);
                PaintInputRun(text[caretIndex..], new(afterX, textPos.Y), paint.Text, inputClip, paint.Typography);
            }
        }
        else
        {
            PaintInputRun(text, textPos, paint.Text, inputClip, paint.Typography);
        }

        if (active)
        {
            var caretX = MathF.Round(textPos.X + (nativeComposition ? composition!.CaretX
                : lineLayout?.X(caretIndex) ?? MeasureTextWidth(text, caretIndex)));
            var drawCaretX = caretX;
            var caretWidth = MathF.Max(0.75f, 1f / MathF.Max(1f, _textSettings.Scale));
            var caretTop = MathF.Round(textPos.Y + 1f);
            var caretHeight = MathF.Max(1f, inputTextLineHeight - 2f);
            GetImeFontMetrics(nativeComposition ? composition!.Text : text,
                nativeComposition ? caretIndex + composition!.Length : caretIndex, out var fontPixelHeight, out var fontPixelWidth);
            if ((layout.Options.Flags & UiInputTextFlags.ReadOnly) == 0)
                _imeHandler?.SetCaretRect(new UiRect(caretX, textPos.Y, caretWidth, inputTextLineHeight), inputRect, fontPixelHeight, fontPixelWidth);
            if (nativeComposition)
            {
                var thickness = MathF.Max(1, 1 / MathF.Max(1, _textSettings.Scale));
                var y = MathF.Min(textPos.Y + layout.LineHeight - thickness, inputRect.Y + inputRect.Height - thickness);
                foreach (var fragment in composition!.Layout.Geometry!.SelectionFragments(caretIndex, caretIndex + composition.Length))
                    _builder.AddRectFilled(new(textPos.X + fragment.Left * composition.Layout.Scale, y,
                        fragment.Width * composition.Layout.Scale, thickness), paint.Text, _whiteTexture, inputClip);
            }
            else if (!string.IsNullOrEmpty(compositionText))
            {
                if (paint.Typography || selectedForeground.HasValue)
                {
                    if (!selectedForeground.HasValue)
                        PaintInputRun(compositionText, new(caretX, textPos.Y), paint.Text, inputClip, true);
                    DrawImeCompositionUnderline(compositionText, caretX, textPos.Y, inputRect, inputClip,
                        paint.Typography ? layout.LineHeight : _lineHeight, paint.Text);
                }
                else
                    DrawImeCompositionInline(compositionText, caretX, textPos.Y, inputRect, inputClip, paint.Text);
                drawCaretX = MathF.Round(caretX + compositionWidth);
            }

            var caretLeft = Math.Clamp(drawCaretX, inputRect.X, inputRect.X + inputRect.Width - caretWidth);
            caretTop = Math.Clamp(caretTop, inputRect.Y, inputRect.Y + inputRect.Height - caretHeight);
            var caretRect = new UiRect(caretLeft, caretTop, caretWidth, caretHeight);
            if (IsCaretBlinkOn())
            {
                _builder.AddRectFilled(caretRect, paint.Caret, _whiteTexture, inputClip);
            }
        }

    }
    private float InputCompositionSuffixX(float origin, string text, int caret, float width)
        => MathF.Round(origin + MeasureTextWidth(text, caret) + width);

    private void PaintInputRun(string text, UiVector2 position, UiColor color, UiRect clip, bool typography)
    {
        if (text.Length == 0)
            return;
        if (!typography)
        {
            AddTextInternal(_builder, text, position, color, clip, _textSettings, _lineHeight);
            return;
        }
        var leading = (_controlTypographyHeight - (_controlTypographyMetrics.Ascent + _controlTypographyMetrics.Descent) / _contentScale) / 2;
        PaintControlTypographyRun(text, position with
        {
            Y = position.Y + leading
        }, color, clip);
    }
    internal UiInputEditResult EditNavigableInput(string id, ref string value, int maxLength, in UiInputLayout layout, string? retainedPointerOwner = null)
    {
        EnsureNotMeasuring();
        var hovered = ItemHoverable(id, layout.Field);
        _lastItemId = id;
        _lastItemFlags = _currentItemFlags;
        _lastItemEdited = false;
        var editableHovered = layout.Editing.HasValue ? ItemHoverable(id, layout.EditingBounds) : hovered;
        var nav = ProcessControlKeyboard<RangeControlKeys>(layout.EditingBounds, new(false, editableHovered, false));
        var retainingPointer = retainedPointerOwner is not null && _state.ActiveId == retainedPointerOwner && _leftMouseDown;
        if (!retainingPointer && nav.Focused && (!_leftMousePressed || _fieldFocusedControlId == id || _accessibilityFocus == id) && _state.ActiveId != id)
        {
            _state.ActiveId = id;
            if ((layout.Options.Flags & UiInputTextFlags.AutoSelectAll) != 0)
            {
                _state.SetSelection(id, new(0, value.Length));
                _state.SetCursor(id, value.Length);
            }
            ResetCaretBlink();
        }
        else if (!nav.Focused && !_leftMousePressed && _state.ActiveId == id)
        {
            _state.ActiveId = null;
            _imeHandler?.SetCompositionOwner(null);
        }
        if (editableHovered && IsControlEnabled)
            SetMouseCursor(UiMouseCursor.TextInput);
        var edit = EditSingleLineInput(id, ref value, maxLength, layout, false, true);
        _lastItemEdited = edit.Changed;
        return edit with
        {
            Hovered = hovered
        };
    }
    internal void RestartInputCaretBlink()
    {
        EnsureNotMeasuring();
        ResetCaretBlink();
    }
    internal void ReleaseOwnedTextInput(string id)
    {
        var hadInput = _state.HasTextInputStorage(id);
        var active = _state.ActiveId == id;
        _state.ReleaseTextInput(id);
        if (hadInput && active)
            _imeHandler?.SetCompositionOwner(null);
        _imeHandler?.ReleaseInput(id);
    }
}
