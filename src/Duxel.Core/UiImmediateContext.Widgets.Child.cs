namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginChild(string id, UiVector2 size, bool border = false)
        => BeginChild(id, size, border, coalesceDocumentScrollInputs: false);

    /// <summary>Begins a child viewport, optionally combining wheel input that stays over the same code block and captured thumb moves.</summary>
    public bool BeginChild(string id, UiVector2 size, bool border, bool coalesceDocumentScrollInputs)
    {
        id ??= "Child";
        var resolvedId = ResolveId(id);

        var available = GetContentRegionAvail();
        var width = size.X > 0f ? size.X : MathF.Max(1f, available.X > 0f ? available.X : InputWidth);
        var height = size.Y > 0f ? size.Y : GetFrameHeight() * 6f;
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);
        var contentRect = new UiRect(rect.X + 2f, rect.Y + 2f, MathF.Max(0f, rect.Width - 4f), MathF.Max(0f, rect.Height - 4f));

        if (border)
        {
            AddRectFilled(rect, _theme.FrameBg, _whiteTexture);
        }

        var scrollY = _state.GetScrollY(resolvedId);
        var scrollX = _state.GetScrollX(resolvedId);
        if (coalesceDocumentScrollInputs && _leftMouseDown
            && _state.PreciseScrollbarDrag is { DocumentTrack: { } track } drag
            && track.Viewport == rect && _state.ActiveId == drag.Id
            && drag.Id.Length == resolvedId.Length + "##childscroll".Length
            && drag.Id.AsSpan(0, resolvedId.Length).SequenceEqual(resolvedId)
            && drag.Id.AsSpan(resolvedId.Length).SequenceEqual("##childscroll"))
        {
            // Draw the current thumb position in this input frame, before traversing the document.
            scrollY = (float)GetPreciseThumbOffset(drag, _mousePosition.Y, track.Start, track.Travel, track.Maximum, 1);
        }
        // Reveal offsets must be applied before drawing descendants. Publishing a new
        // offset with their old geometry would apply the same reveal delta twice.
        if (_accessibility is { } accessibility && accessibility.TryGet(resolvedId, out var published)
            && published.Node.Scroll is { } range && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
        {
            var offset = accessibility.ApplyScrollIntoView(resolvedId, new(scrollX, scrollY),
                new((float)range.HorizontalMaximum, (float)range.VerticalMaximum));
            scrollX = offset.X;
            scrollY = offset.Y;
        }

        var enclosingClip = CurrentClipRect;
        PushClipRect(contentRect, true);
        var start = new UiVector2(contentRect.X - scrollX, contentRect.Y - scrollY);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _childStack.Push(new UiChildState(
            resolvedId,
            _currentWindowId,
            rect,
            start,
            scrollY,
            scrollX,
            contentRect.X,
            _lastItemPos,
            _lastItemSize,
            _lastItemId,
            _lastItemFlags,
            _columnsActive,
            _columnsCount,
            _columnsIndex,
            _columnsStartX,
            _columnsStartY,
            _columnsWidth,
            _columnsMaxY,
            _columnYs,
            _columnWidths,
            _accessibilityParent,
            GetDisplayLabel(id),
            enclosingClip,
            coalesceDocumentScrollInputs) { TextDragScrollRevision = _textDragScrollRevision });
        _accessibilityParent = resolvedId;
        _columnsActive = false;
        _columnsCount = 0;
        _columnsIndex = 0;
        _columnsStartX = 0f;
        _columnsStartY = 0f;
        _columnsWidth = 0f;
        _columnsMaxY = 0f;
        _columnYs = Array.Empty<float>();
        _columnWidths = Array.Empty<float>();
        return true;
    }

    public void EndChild()
    {
        if (_childStack.Count == 0)
        {
            return;
        }

        var cursorY = _layouts.Peek().Cursor.Y;
        _layouts.Pop();

        var state = _childStack.Pop();
        _accessibilityParent = state.AccessibilityParent;

        // Calculate content height and max scroll
        var contentHeight = cursorY - state.Rect.Y - 2f + state.ScrollY;
        var contentWidth = MathF.Max(0f, state.ContentMaxX - (state.Rect.X + 2f));
        var visibleWidth = MathF.Max(0f, state.Rect.Width - 4f);
        var visibleHeight = MathF.Max(0f, state.Rect.Height - 4f);
        var maxScrollX = 0f;
        var maxScrollY = 0f;
        var hasHScroll = false;
        var hasVScroll = false;

        for (var i = 0; i < 2; i++)
        {
            maxScrollX = MathF.Max(0f, contentWidth - visibleWidth);
            maxScrollY = MathF.Max(0f, contentHeight - visibleHeight);
            hasHScroll = maxScrollX > 0f;
            hasVScroll = maxScrollY > 0f;

            var nextVisibleWidth = MathF.Max(0f, state.Rect.Width - 4f - (hasVScroll ? ScrollbarSize : 0f));
            var nextVisibleHeight = MathF.Max(0f, state.Rect.Height - 4f - (hasHScroll ? ScrollbarSize : 0f));
            if (MathF.Abs(nextVisibleWidth - visibleWidth) < 0.001f && MathF.Abs(nextVisibleHeight - visibleHeight) < 0.001f)
            {
                break;
            }

            visibleWidth = nextVisibleWidth;
            visibleHeight = nextVisibleHeight;
        }

        var scrolled = ConsumeTextDragScroll(state.TextDragScrollRevision,
            new(Math.Clamp(state.ScrollX, 0f, maxScrollX), Math.Clamp(state.ScrollY, 0f, maxScrollY)),
            new(maxScrollX, maxScrollY));
        var (scrollX, scrollY) = (scrolled.X, scrolled.Y);
        var beforeWheel = scrollY;
        var wheel = _mouseWheel;
        var step = GetFrameHeight() * 3f;

        // Mouse wheel
        if (IsScrollHoverable(state.Rect, CurrentClipRect))
        {
            scrollY = ApplyScrollWheel(scrollY, maxScrollY, step, ref _mouseWheel);
            scrollX = ApplyScrollWheel(scrollX, maxScrollX, step, ref _mouseWheelHorizontal);
        }
        var afterWheel = scrollY;

        // Render scrollbar before PopClipRect (respects parent clip)
        if (maxScrollY > 0f)
        {
            var trackRect = new UiRect(
                state.Rect.X + state.Rect.Width - ScrollbarSize,
                state.Rect.Y,
                ScrollbarSize,
                state.Rect.Height - (hasHScroll ? ScrollbarSize : 0f)
            );
            scrollY = RenderScrollbarV($"{state.Id}##childscroll", trackRect, scrollY, maxScrollY, contentHeight, CurrentClipRect,
                documentViewport: state.CoalesceDocumentScrollInputs ? state.Rect : null);
        }

        if (maxScrollX > 0f)
        {
            var trackRect = new UiRect(
                state.Rect.X,
                state.Rect.Y + state.Rect.Height - ScrollbarSize,
                state.Rect.Width - (hasVScroll ? ScrollbarSize : 0f),
                ScrollbarSize
            );
            scrollX = RenderScrollbarH($"{state.Id}##childscrollx", trackRect, scrollX, maxScrollX, contentWidth, CurrentClipRect);
        }

        if (_accessibility is { } tree)
        {
            var viewport = new UiRect(state.Rect.X + 2, state.Rect.Y + 2, visibleWidth, visibleHeight);
            var offset = new UiVector2(scrollX, scrollY);
            var enabled = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier;
            var range = ApplyAccessibilityScrollRequest(tree, state.Id, new(visibleWidth, visibleHeight),
                IntersectRect(CurrentClipRect, viewport), new(contentWidth, contentHeight), GetFrameHeight() * 3,
                enabled, false, ref offset);
            scrollX = offset.X;
            scrollY = offset.Y;
            ExposeLegacyChildViewport(state.Id, state.AccessibilityParent, state.AccessibilityLabel, state.Rect, range);
            tree.CaptureScrollViewport(state.Id, viewport, state.EnclosingClip,
                range ?? new(new(visibleWidth, visibleHeight), new(contentWidth, contentHeight), offset));
        }

        if (afterWheel != beforeWheel)
        {
            if (state.CoalesceDocumentScrollInputs && scrollY == afterWheel && state.ScrollY == beforeWheel
                && !_leftMouseDown && _state.PreviousPopupBlockingRects.Count == 0 && step > 0
                && _state.DocumentWheelRegion is { } code && (_state.ActiveId is null || _state.ActiveId == code.CodeId))
            {
                // A released, focused read-only code view cannot consume this wheel or continue a selection drag.
                // Leave boundary events to the usual traversal, including handoff to an enclosing viewport.
                var bounds = code.Bounds;
                var room = wheel < 0 ? bounds.Y + bounds.Height - _mousePosition.Y : _mousePosition.Y - bounds.Y;
                room -= MathF.Abs(afterWheel - beforeWheel);
                var distance = MathF.Min(room, wheel < 0 ? maxScrollY - afterWheel : afterWheel);
                if (distance > 0)
                    _state.WheelCoalescing = (wheel, distance / step, false);
            }
            _state.DocumentWheelRegion = null;
        }
        PopClipRect();
        _state.SetScrollX(state.Id, scrollX);
        _state.SetScrollY(state.Id, scrollY);
        _lastItemPos = new UiVector2(state.Rect.X, state.Rect.Y);
        _lastItemSize = new UiVector2(state.Rect.Width, state.Rect.Height);
        _lastItemId = state.Id;
        _lastItemFlags = state.LastItemFlags;
        _columnsActive = state.ColumnsActive;
        _columnsCount = state.ColumnsCount;
        _columnsIndex = state.ColumnsIndex;
        _columnsStartX = state.ColumnsStartX;
        _columnsStartY = state.ColumnsStartY;
        _columnsWidth = state.ColumnsWidth;
        _columnsMaxY = state.ColumnsMaxY;
        _columnYs = state.ColumnYs;
        _columnWidths = state.ColumnWidths;

        if (MathF.Abs(scrollY - state.ScrollY) > 0.001f || MathF.Abs(scrollX - state.ScrollX) > 0.001f)
        {
            _requestFrame?.Invoke();
        }
    }
    private void ExposeLegacyChildViewport(string id, string parent, string label, UiRect bounds, UiAccessibilityScrollInfo? scroll)
    {
        var widget = SwapAccessibilityWidget(null);
        try
        {
            ExposeAccessibility(id, parent, UiAccessibilityRole.Pane, label, bounds,
                focusable : false, valuePattern : false, scroll : scroll, literalLabel : true);
        }
        finally { SwapAccessibilityWidget(widget); }
    }

}

