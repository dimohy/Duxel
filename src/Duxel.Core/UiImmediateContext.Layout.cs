namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public void Indent()
    {
        PushIndent();
    }

    public void Indent(float width)
    {
        PushIndent(width);
    }

    public void Unindent()
    {
        PopIndent();
    }

    public void Unindent(float width)
    {
        _ = width;
        PopIndent();
    }

    public void AlignTextToFramePadding()
    {
        var current = _layouts.Pop();
        var offset = MathF.Max(0f, (GetFrameHeight() - _lineHeight) * 0.5f);
        current = current with { Cursor = new UiVector2(current.Cursor.X, current.Cursor.Y + offset) };
        _layouts.Push(current);
    }

    public UiVector2 GetCursorPos()
    {
        return _layouts.Peek().Cursor;
    }

    public void EnableRootViewportContentLayout(bool enabled = true)
    {
        if (_currentWindowId is not null)
        {
            return;
        }

        if (!enabled)
        {
            _windowRect = default;
            _hasWindowRect = false;
            _windowContentStart = default;
            _windowContentMax = default;
            return;
        }

        var viewport = GetMainViewport();
        _windowRect = new UiRect(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y);
        _hasWindowRect = true;
        _windowContentStart = new UiVector2(_windowRect.X + WindowPadding, _windowRect.Y + WindowPadding);
        _windowContentMax = new UiVector2(_windowRect.X + _windowRect.Width - WindowPadding, _windowRect.Y + _windowRect.Height - WindowPadding);
    }

    public void SetCursorPos(UiVector2 position)
    {
        var current = _layouts.Pop();
        current = current with { Cursor = position, LineStartX = position.X };
        _layouts.Push(current);
    }

    public UiVector2 GetContentRegionAvail()
    {
        var cursor = _layouts.Peek().Cursor;
        if (!_hasWindowRect)
        {
            return new UiVector2(0f, 0f);
        }

        var endX = _windowRect.X + _windowRect.Width - WindowPadding;
        var endY = _windowRect.Y + _windowRect.Height - WindowPadding;
        return new UiVector2(MathF.Max(0f, endX - cursor.X), MathF.Max(0f, endY - cursor.Y));
    }

    public UiVector2 GetContentRegionMax()
    {
        if (!_hasWindowRect)
        {
            return default;
        }

        return new UiVector2(
            _windowRect.X + _windowRect.Width - WindowPadding,
            _windowRect.Y + _windowRect.Height - WindowPadding
        );
    }

    public UiVector2 GetWindowContentRegionMin()
    {
        if (!_hasWindowRect)
        {
            return default;
        }

        return new UiVector2(_windowRect.X + WindowPadding, _windowRect.Y + WindowPadding);
    }

    public UiVector2 GetWindowContentRegionMax()
    {
        return GetContentRegionMax();
    }

    public bool IsRectVisible(UiVector2 size)
    {
        var cursor = _layouts.Peek().Cursor;
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);
        return IsRectVisible(rect);
    }

    public bool IsRectVisible(UiVector2 rectMin, UiVector2 rectMax)
    {
        var rect = new UiRect(rectMin.X, rectMin.Y, rectMax.X - rectMin.X, rectMax.Y - rectMin.Y);
        return IsRectVisible(rect);
    }

    public bool IsRectVisible(UiRect rect)
    {
        var clipped = IntersectRect(CurrentClipRect, rect);
        return clipped.Width > 0f && clipped.Height > 0f;
    }

    public void CalcListClipping(int itemsCount, float itemsHeight, out int outStart, out int outEnd)
    {
        if (itemsCount <= 0 || itemsHeight <= 0f || !_hasWindowRect)
        {
            outStart = 0;
            outEnd = 0;
            return;
        }

        var cursor = GetCursorScreenPos();
        var clipRect = CurrentClipRect;
        var clipMinY = clipRect.Y;
        var clipMaxY = clipRect.Y + clipRect.Height;
        var contentEndY = cursor.Y + (itemsCount * itemsHeight);
        if (clipMaxY <= cursor.Y || clipMinY >= contentEndY)
        {
            outStart = 0;
            outEnd = 0;
            return;
        }
        var start = (int)MathF.Floor((clipMinY - cursor.Y) / itemsHeight);
        var end = (int)MathF.Ceiling((clipMaxY - cursor.Y) / itemsHeight);

        outStart = Math.Clamp(start, 0, itemsCount);
        outEnd = Math.Clamp(end, outStart, itemsCount);
    }

    public UiVector2 GetCursorScreenPos()
    {
        return GetCursorPos();
    }

    public void SetCursorScreenPos(UiVector2 position)
    {
        SetCursorPos(position);
    }

    public float GetCursorPosX()
    {
        return GetCursorPos().X;
    }

    public float GetCursorPosY()
    {
        return GetCursorPos().Y;
    }

    public void SetCursorPosX(float x)
    {
        var current = _layouts.Pop();
        current = current with
        {
            Cursor = new UiVector2(x, current.Cursor.Y),
            LineStartX = x
        };
        _layouts.Push(current);
    }

    public void SetCursorPosY(float y)
    {
        var current = _layouts.Pop();
        current = current with { Cursor = new UiVector2(current.Cursor.X, y) };
        _layouts.Push(current);
    }

    public UiVector2 GetCursorStartPos()
    {
        if (!_hasWindowRect)
        {
            return default;
        }

        return _windowContentStart;
    }

    public bool IsWindowAppearing() => _windowAppearing;

    public bool IsWindowCollapsed() => _windowCollapsed;

    public bool IsWindowFocused()
    {
        return _currentWindowId is not null && string.Equals(_state.ActiveWindowId, _currentWindowId, StringComparison.Ordinal);
    }

    public bool IsWindowHovered()
    {
        return _hasWindowRect && IsHovering(_windowRect);
    }

    public UiDrawListBuilder GetWindowDrawList() => _builder;

    public UiVector2 GetWindowPos()
    {
        return _hasWindowRect ? new UiVector2(_windowRect.X, _windowRect.Y) : default;
    }

    public UiVector2 GetWindowSize()
    {
        return _hasWindowRect ? new UiVector2(_windowRect.Width, _windowRect.Height) : default;
    }

    public float GetWindowWidth() => _hasWindowRect ? _windowRect.Width : 0f;

    public float GetWindowHeight() => _hasWindowRect ? _windowRect.Height : 0f;

    public void SetNextWindowPos(UiVector2 position)
    {
        _nextWindowPos = position;
        _hasNextWindowPos = true;
    }

    public void SetNextWindowSize(UiVector2 size)
    {
        _nextWindowSize = size;
        _hasNextWindowSize = true;
    }

    public void SetNextWindowSizeConstraints(UiVector2 min, UiVector2 max)
    {
        _nextWindowMinSize = min;
        _nextWindowMaxSize = max;
        _hasNextWindowSizeConstraints = true;
    }

    public void SetNextWindowContentSize(UiVector2 size)
    {
        _nextWindowContentSize = size;
        _hasNextWindowContentSize = true;
    }

    public void SetNextWindowOpen(bool open)
    {
        _nextWindowOpen = open;
        _hasNextWindowOpen = true;
    }

    public void SetNextWindowCollapsed(bool collapsed)
    {
        _nextWindowCollapsed = collapsed;
        _hasNextWindowCollapsed = true;
    }

    public void SetNextWindowFocus()
    {
        _hasNextWindowFocus = true;
    }

    public void SetNextWindowScroll(float scrollX, float scrollY)
    {
        _nextWindowScrollX = MathF.Max(0f, scrollX);
        _nextWindowScrollY = MathF.Max(0f, scrollY);
        _hasNextWindowScrollX = true;
        _hasNextWindowScrollY = true;
    }

    public void SetNextWindowBgAlpha(float alpha)
    {
        _nextWindowBgAlpha = alpha;
        _hasNextWindowBgAlpha = true;
    }

    public void SetWindowPos(UiVector2 position)
    {
        if (_currentWindowId is null)
        {
            return;
        }

        var rect = _state.GetWindowRect(_currentWindowId, _windowRect);
        rect = rect with { X = position.X, Y = position.Y };
        _state.SetWindowRect(_currentWindowId, rect);
        _windowRect = rect;
    }

    public void SetWindowPos(string title, UiVector2 position)
    {
        title ??= "Window";
        var rect = _state.GetWindowRect(title, new UiRect(position.X, position.Y, WindowMinWidth, WindowMinHeight));
        rect = rect with { X = position.X, Y = position.Y };
        _state.SetWindowRect(title, rect);
        if (string.Equals(_currentWindowId, title, StringComparison.Ordinal))
        {
            _windowRect = rect;
        }
    }

    public void SetWindowSize(UiVector2 size)
    {
        if (_currentWindowId is null)
        {
            return;
        }

        var rect = _state.GetWindowRect(_currentWindowId, _windowRect);
        rect = rect with { Width = size.X, Height = size.Y };
        _state.SetWindowRect(_currentWindowId, rect);
        _windowRect = rect;
    }

    public void SetWindowSize(string title, UiVector2 size)
    {
        title ??= "Window";
        var rect = _state.GetWindowRect(title, new UiRect(0f, 0f, size.X, size.Y));
        rect = rect with { Width = size.X, Height = size.Y };
        _state.SetWindowRect(title, rect);
        if (string.Equals(_currentWindowId, title, StringComparison.Ordinal))
        {
            _windowRect = rect;
        }
    }

    public void SetWindowCollapsed(bool collapsed)
    {
        if (_currentWindowId is null)
        {
            return;
        }

        _state.SetWindowCollapsed(_currentWindowId, collapsed);
        _windowCollapsed = collapsed;
    }

    public void SetWindowCollapsed(string title, bool collapsed)
    {
        title ??= "Window";
        _state.SetWindowCollapsed(title, collapsed);
        if (string.Equals(_currentWindowId, title, StringComparison.Ordinal))
        {
            _windowCollapsed = collapsed;
        }
    }

    public void SetWindowOpen(string title, bool open)
    {
        title ??= "Window";
        _state.SetWindowOpen(title, open);
    }

    public void SetWindowFocus()
    {
        if (_currentWindowId is null)
        {
            return;
        }

        _state.ActiveWindowId = _currentWindowId;
        _state.BringWindowToFront(_currentWindowId);
    }

    public void SetWindowFocus(string title)
    {
        title ??= "Window";
        _state.ActiveWindowId = title;
        _state.BringWindowToFront(title);
    }

    public float GetScrollX() => _windowScrollX;

    public float GetScrollY() => _windowScrollY;

    public void SetScrollX(float scrollX)
    {
        if (_currentWindowId is null)
        {
            return;
        }

        var max = GetScrollMaxX();
        var next = Math.Clamp(scrollX, 0f, max);
        var delta = next - _windowScrollX;
        _windowScrollX = next;
        _state.SetScrollX(_currentWindowId, next);

        var current = _layouts.Pop();
        current = current with
        {
            Cursor = new UiVector2(current.Cursor.X - delta, current.Cursor.Y),
            LineStartX = current.LineStartX - delta
        };
        _layouts.Push(current);
    }

    public void SetScrollY(float scrollY)
    {
        if (_currentWindowId is null)
        {
            return;
        }

        var max = GetScrollMaxY();
        var next = Math.Clamp(scrollY, 0f, max);
        var delta = next - _windowScrollY;
        _windowScrollY = next;
        _state.SetScrollY(_currentWindowId, next);

        var current = _layouts.Pop();
        current = current with { Cursor = new UiVector2(current.Cursor.X, current.Cursor.Y - delta) };
        _layouts.Push(current);
    }

    public float GetScrollMaxX()
    {
        if (!_hasWindowRect)
        {
            return 0f;
        }

        var visibleMaxX = _windowRect.X + _windowRect.Width - WindowPadding;
        return MathF.Max(0f, _windowContentMax.X - visibleMaxX);
    }

    public float GetScrollMaxY()
    {
        if (!_hasWindowRect)
        {
            return 0f;
        }

        var visibleMaxY = _windowRect.Y + _windowRect.Height - WindowPadding;
        return MathF.Max(0f, _windowContentMax.Y - visibleMaxY);
    }

    public void SetScrollHereX(float centerXRatio = 0.5f)
    {
        if (!_hasWindowRect)
        {
            return;
        }

        var available = MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f));
        var cursorX = _layouts.Peek().Cursor.X + _windowScrollX;
        var localX = cursorX - _windowContentStart.X;
        var target = localX - (available * Math.Clamp(centerXRatio, 0f, 1f));
        SetScrollX(target);
    }

    public void SetScrollHereY(float centerYRatio = 0.5f)
    {
        if (!_hasWindowRect)
        {
            return;
        }

        var available = MathF.Max(0f, _windowRect.Height - (WindowPadding * 2f));
        var cursorY = _layouts.Peek().Cursor.Y + _windowScrollY;
        var localY = cursorY - _windowContentStart.Y;
        var target = localY - (available * Math.Clamp(centerYRatio, 0f, 1f));
        SetScrollY(target);
    }

    public void SetScrollFromPosX(float localX, float centerXRatio = 0.5f)
    {
        if (!_hasWindowRect)
        {
            return;
        }

        var available = MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f));
        var target = localX - (available * Math.Clamp(centerXRatio, 0f, 1f));
        SetScrollX(target);
    }

    public void SetScrollFromPosY(float localY, float centerYRatio = 0.5f)
    {
        if (!_hasWindowRect)
        {
            return;
        }

        var available = MathF.Max(0f, _windowRect.Height - (WindowPadding * 2f));
        var target = localY - (available * Math.Clamp(centerYRatio, 0f, 1f));
        SetScrollY(target);
    }

    public UiVector2 CalcTextSize(string text)
    {
        text ??= string.Empty;
        var baseFontSize = _lineHeight * _textSettings.Scale;
        var fontScale = baseFontSize > 0f
            ? GetFontSize() / baseFontSize
            : 1f;
        var scaledSettings = new UiTextSettings(
            _textSettings.Scale * fontScale,
            _textSettings.LineHeightScale,
            _textSettings.PixelSnap,
            _textSettings.UseBaseline,
            _textSettings.UseFallbackGlyph,
            _textSettings.MissingGlyphObserver
        );
        return UiTextBuilder.MeasureText(_fontAtlas, text, scaledSettings, _lineHeight);
    }

    public UiVector2 AlignRect(UiRect containerRect, UiVector2 contentSize, UiItemHorizontalAlign horizontalAlign = UiItemHorizontalAlign.Left, UiItemVerticalAlign verticalAlign = UiItemVerticalAlign.Top)
    {
        var x = horizontalAlign switch
        {
            UiItemHorizontalAlign.Center => containerRect.X + MathF.Max(0f, (containerRect.Width - contentSize.X) * 0.5f),
            UiItemHorizontalAlign.Right => containerRect.X + MathF.Max(0f, containerRect.Width - contentSize.X),
            _ => containerRect.X,
        };

        var y = verticalAlign switch
        {
            UiItemVerticalAlign.Center => containerRect.Y + MathF.Max(0f, (containerRect.Height - contentSize.Y) * 0.5f),
            UiItemVerticalAlign.Bottom => containerRect.Y + MathF.Max(0f, containerRect.Height - contentSize.Y),
            _ => containerRect.Y,
        };

        return new UiVector2(x, y);
    }

    public void BeginGroup()
    {
        var current = _layouts.Peek();
        _layouts.Push(new UiLayoutState(current.Cursor, current.IsRow, 0f, current.LineStartX));
    }

    public void EndGroup()
    {
        if (_layouts.Count <= 1)
        {
            return;
        }

        var group = _layouts.Pop();
        var parent = _layouts.Pop();
        var nextCursor = new UiVector2(parent.LineStartX, MathF.Max(parent.Cursor.Y, group.Cursor.Y));
        parent = parent with { Cursor = nextCursor };
        _layouts.Push(parent);
    }

    public void SetNextItemWidth(float width)
    {
        _nextItemWidth = MathF.Max(0f, width);
    }

    public float CalcItemWidth()
    {
        if (_nextItemWidth > 0f)
        {
            return _nextItemWidth;
        }

        return _itemWidthStack.Count > 0 ? _itemWidthStack.Peek() : InputWidth;
    }

    public void PushItemWidth(float width)
    {
        _itemWidthStack.Push(MathF.Max(0f, width));
    }

    public void PopItemWidth()
    {
        if (_itemWidthStack.Count > 0)
        {
            _itemWidthStack.Pop();
        }
    }

    public void BeginWindow(string title)
    {
        title ??= "Window";

        if (_layouts.Count > 1)
        {
            var labelSnapshot = new string[_tableColumnLabels.Count];
            _tableColumnLabels.CopyTo(labelSnapshot, 0);
            _windowStateStack.Push(new UiWindowState(
                _windowRect,
                _hasWindowRect,
                _currentWindowId,
                _windowContentStart,
                _windowContentMax,
                _windowAppearing,
                _windowCollapsed,
                _windowScrollX,
                _windowScrollY,
                _columnsActive,
                _columnsCount,
                _columnsIndex,
                _columnsStartX,
                _columnsStartY,
                _columnsWidth,
                _columnsMaxY,
                _columnYs,
                _columnWidths,
                _tableActive,
                _tableColumns,
                _tableColumn,
                _tableStartX,
                _tableRowY,
                _tableRowMaxY,
                _tableColumnWidth,
                labelSnapshot,
                _tableSetupIndex,
                _tableRowIndex,
                _tableColumnWidths,
                _tableColumnAlign,
                _tableId,
                _tableFlags,
                _tableStartY,
                _tableRect,
                _inMenuBar
            ));

            _columnsActive = false;
            _columnsCount = 0;
            _columnsIndex = 0;
            _columnsStartX = 0f;
            _columnsStartY = 0f;
            _columnsWidth = 0f;
            _columnsMaxY = 0f;
            _columnYs = [];
            _columnWidths = [];

            _tableActive = false;
            _tableColumns = 0;
            _tableColumn = 0;
            _tableStartX = 0f;
            _tableRowY = 0f;
            _tableRowMaxY = 0f;
            _tableColumnWidth = 0f;
            _tableColumnLabels.Clear();
            _tableSetupIndex = 0;
            _tableRowIndex = 0;
            _tableColumnWidths = [];
            _tableColumnAlign = [];
            _tableId = null;
            _tableFlags = UiTableFlags.None;
            _tableStartY = 0f;
            _tableRect = default;

            _inMenuBar = false;
        }

        var root = _windowRootLayout;
        var maxWidth = MathF.Max(WindowMinWidth, _displaySize.X - 40f);
        var maxHeight = MathF.Max(WindowMinHeight, _displaySize.Y - 40f);
        var defaultWidth = MathF.Max(WindowMinWidth, MathF.Min(maxWidth, _displaySize.X * 0.5f));
        var defaultHeight = MathF.Max(WindowMinHeight, MathF.Min(maxHeight, _displaySize.Y * 0.5f));
        _state.EnsureWindowOrder(title);
        var defaultRect = new UiRect(root.Cursor.X, root.Cursor.Y, defaultWidth, defaultHeight);

        var hasStoredRect = _state.TryGetWindowRect(title, out var storedRect);
        var rect = hasStoredRect
            ? storedRect
            : _state.GetWindowRect(title, defaultRect);

        _windowAppearing = !hasStoredRect;
        _currentWindowId = title;

        var isOpen = _state.GetWindowOpen(title, true);
        if (_hasNextWindowOpen)
        {
            isOpen = _nextWindowOpen;
            _state.SetWindowOpen(title, isOpen);
            _hasNextWindowOpen = false;
        }

        if (!isOpen)
        {
            _hasNextWindowPos = false;
            _hasNextWindowSize = false;
            _hasNextWindowSizeConstraints = false;
            _hasNextWindowContentSize = false;
            _hasNextWindowCollapsed = false;
            _hasNextWindowFocus = false;
            _hasNextWindowScrollX = false;
            _hasNextWindowScrollY = false;
            _hasNextWindowBgAlpha = false;

            _windowRect = default;
            _hasWindowRect = false;
            _windowCollapsed = true;
            _windowScrollX = 0f;
            _windowScrollY = 0f;
            _windowOverlayStack.Push(false);
            PushClipRect(new UiRect(0f, 0f, 0f, 0f), true);
            _layouts.Push(new UiLayoutState(root.Cursor, false, 0f, root.Cursor.X));
            return;
        }

        if (_hasNextWindowPos)
        {
            rect = rect with { X = _nextWindowPos.X, Y = _nextWindowPos.Y };
            _hasNextWindowPos = false;
        }

        if (_hasNextWindowSize)
        {
            rect = rect with { Width = _nextWindowSize.X, Height = _nextWindowSize.Y };
            _hasNextWindowSize = false;
        }

        if (_hasNextWindowSizeConstraints)
        {
            var width = rect.Width;
            var height = rect.Height;
            if (_nextWindowMinSize.X > 0f)
            {
                width = MathF.Max(width, _nextWindowMinSize.X);
            }
            if (_nextWindowMinSize.Y > 0f)
            {
                height = MathF.Max(height, _nextWindowMinSize.Y);
            }
            if (_nextWindowMaxSize.X > 0f)
            {
                width = MathF.Min(width, _nextWindowMaxSize.X);
            }
            if (_nextWindowMaxSize.Y > 0f)
            {
                height = MathF.Min(height, _nextWindowMaxSize.Y);
            }

            rect = rect with { Width = width, Height = height };
            _hasNextWindowSizeConstraints = false;
        }

        if (_hasNextWindowCollapsed)
        {
            _state.SetWindowCollapsed(title, _nextWindowCollapsed);
            _hasNextWindowCollapsed = false;
        }

        _windowCollapsed = _state.GetWindowCollapsed(title);

        if (_hasNextWindowFocus)
        {
            _state.ActiveWindowId = title;
            _state.BringWindowToFront(title);
            _hasNextWindowFocus = false;
        }

        if (_hasNextWindowScrollX)
        {
            _state.SetScrollX(title, _nextWindowScrollX);
            _hasNextWindowScrollX = false;
        }

        if (_hasNextWindowScrollY)
        {
            _state.SetScrollY(title, _nextWindowScrollY);
            _hasNextWindowScrollY = false;
        }

        _windowScrollX = _state.GetScrollX(title);
        _windowScrollY = _state.GetScrollY(title);

        const float collapsedContentPeekHeight = 3f;
        var titleBarHeight = GetFrameHeight() + WindowPadding;
        var expandedSize = _state.GetWindowExpandedSize(title, new UiVector2(rect.Width, rect.Height));
        if (_windowCollapsed)
        {
            rect = rect with { Height = titleBarHeight + collapsedContentPeekHeight };
        }
        var titleBarRect = new UiRect(rect.X, rect.Y, rect.Width, titleBarHeight);
        var dragId = $"{title}##window_drag";
        var resizeId = $"{title}##window_resize";

        // Set window rect early so close/collapse buttons get z-order checks via ItemHoverable
        _windowRect = rect;
        _hasWindowRect = true;

        var collapseSize = MathF.Min(titleBarHeight - 2f, _lineHeight + 4f);
        var collapseRect = new UiRect(
            rect.X + WindowPadding,
            rect.Y + (titleBarHeight - collapseSize) * 0.5f,
            collapseSize,
            collapseSize
        );
        var collapseId = $"{title}##window_collapse";
        var collapsePressed = ButtonBehavior(collapseId, collapseRect, out var collapseHovered, out var collapseHeld);
        if (collapsePressed)
        {
            if (_windowCollapsed)
            {
                _windowCollapsed = false;
                rect = rect with { Width = expandedSize.X, Height = expandedSize.Y };
            }
            else
            {
                _state.SetWindowExpandedSize(title, new UiVector2(rect.Width, rect.Height));
                _windowCollapsed = true;
                rect = rect with { Height = titleBarHeight + collapsedContentPeekHeight };
            }
            _state.SetWindowCollapsed(title, _windowCollapsed);
        }
        else if (!_windowCollapsed)
        {
            _state.SetWindowExpandedSize(title, new UiVector2(rect.Width, rect.Height));
        }
        var closeSize = MathF.Min(titleBarHeight - 2f, _lineHeight + 4f);
        var closeRect = new UiRect(
            rect.X + rect.Width - WindowPadding - closeSize,
            rect.Y + (titleBarHeight - closeSize) * 0.5f,
            closeSize,
            closeSize
        );
        var closeId = $"{title}##window_close";
        var closePressed = ButtonBehavior(closeId, closeRect, out var closeHovered, out var closeHeld);
        if (closePressed)
        {
            _state.SetWindowOpen(title, false);
        }
        var blockDrag = closeHovered || closeHeld || collapseHovered || collapseHeld;

        var isTopmost = IsWindowTopmostAtMouse(title, rect);
        var popupBlocking = _popupTierDepth == 0 && IsMouseOverAnyBlockingPopup();
        if (_leftMousePressed && isTopmost && IsHovering(rect) && !popupBlocking)
        {
            _state.ActiveWindowId = title;
            _state.BringWindowToFront(title);
        }

        if (_leftMousePressed && isTopmost && IsHovering(titleBarRect) && !blockDrag && !popupBlocking)
        {
            _state.ActiveId = dragId;
            _state.SetScrollX(dragId, _mousePosition.X - rect.X);
            _state.SetScrollY(dragId, _mousePosition.Y - rect.Y);
        }

        if (_state.ActiveId == dragId && _leftMouseDown)
        {
            var offsetX = _state.GetScrollX(dragId);
            var offsetY = _state.GetScrollY(dragId);
            rect = new UiRect(_mousePosition.X - offsetX, _mousePosition.Y - offsetY, rect.Width, rect.Height);
        }
        else if (_state.ActiveId == dragId && !_leftMouseDown)
        {
            _state.ActiveId = null;
        }

        var resizeBorder = MathF.Max(4f, ResizeGripSize * 0.6f - 2f);
        var gripInset = _windowCollapsed ? 0f : MathF.Min(ResizeGripSize * 0.5f, GetWindowCornerRadius());
        const float gripOffset = 3f;
        var gripRect = new UiRect(
            rect.X + rect.Width - ResizeGripSize - gripInset + gripOffset,
            rect.Y + rect.Height - ResizeGripSize - gripInset + gripOffset,
            ResizeGripSize,
            ResizeGripSize
        );
        var canResize = !_windowCollapsed;
        var resizeMask = 0;
        var hoverResize = false;

        if (canResize && isTopmost && !blockDrag)
        {
            var hoverGrip = IsHovering(gripRect);
            if (hoverGrip)
            {
                resizeMask = 2 | 8;
            }
            else
            {
                var hoverLeft = _mousePosition.X >= rect.X - resizeBorder && _mousePosition.X <= rect.X + resizeBorder && _mousePosition.Y >= rect.Y && _mousePosition.Y <= rect.Y + rect.Height;
                var hoverRight = _mousePosition.X >= rect.X + rect.Width - resizeBorder && _mousePosition.X <= rect.X + rect.Width + resizeBorder && _mousePosition.Y >= rect.Y && _mousePosition.Y <= rect.Y + rect.Height;
                var hoverTop = _mousePosition.Y >= rect.Y - resizeBorder && _mousePosition.Y <= rect.Y + resizeBorder && _mousePosition.X >= rect.X && _mousePosition.X <= rect.X + rect.Width;
                var hoverBottom = _mousePosition.Y >= rect.Y + rect.Height - resizeBorder && _mousePosition.Y <= rect.Y + rect.Height + resizeBorder && _mousePosition.X >= rect.X && _mousePosition.X <= rect.X + rect.Width;

                if (hoverLeft)
                {
                    resizeMask |= 1;
                }
                if (hoverRight)
                {
                    resizeMask |= 2;
                }
                if (hoverTop)
                {
                    resizeMask |= 4;
                }
                if (hoverBottom)
                {
                    resizeMask |= 8;
                }
            }

            hoverResize = resizeMask != 0;
            if (hoverResize)
            {
                var resizeCursor = resizeMask switch
                {
                    1 or 2 => UiMouseCursor.ResizeEW,
                    4 or 8 => UiMouseCursor.ResizeNS,
                    1 | 4 => UiMouseCursor.ResizeNWSE,
                    2 | 8 => UiMouseCursor.ResizeNWSE,
                    2 | 4 => UiMouseCursor.ResizeNESW,
                    1 | 8 => UiMouseCursor.ResizeNESW,
                    _ => UiMouseCursor.ResizeAll,
                };
                SetMouseCursor(resizeCursor);
            }
        }

        if (canResize && _leftMousePressed && isTopmost && hoverResize)
        {
            _state.ActiveId = resizeId;
            _state.SetCursor(resizeId, resizeMask);
            _state.SetScrollX($"{resizeId}##ox", rect.X);
            _state.SetScrollY($"{resizeId}##oy", rect.Y);
            _state.SetScrollX($"{resizeId}##ow", rect.Width);
            _state.SetScrollY($"{resizeId}##oh", rect.Height);
            _state.SetScrollX($"{resizeId}##mx", _mousePosition.X);
            _state.SetScrollY($"{resizeId}##my", _mousePosition.Y);
        }

        if (_state.ActiveId == resizeId && _leftMouseDown)
        {
            var mask = _state.GetCursor(resizeId, 0);
            var ox = _state.GetScrollX($"{resizeId}##ox");
            var oy = _state.GetScrollY($"{resizeId}##oy");
            var ow = _state.GetScrollX($"{resizeId}##ow");
            var oh = _state.GetScrollY($"{resizeId}##oh");
            var mx = _state.GetScrollX($"{resizeId}##mx");
            var my = _state.GetScrollY($"{resizeId}##my");

            var dx = _mousePosition.X - mx;
            var dy = _mousePosition.Y - my;

            var newX = ox;
            var newY = oy;
            var newW = ow;
            var newH = oh;

            if ((mask & 2) != 0)
            {
                newW = MathF.Max(WindowMinWidth, ow + dx);
            }
            if ((mask & 1) != 0)
            {
                newW = MathF.Max(WindowMinWidth, ow - dx);
                newX = ox + (ow - newW);
            }
            if ((mask & 8) != 0)
            {
                newH = MathF.Max(WindowMinHeight, oh + dy);
            }
            if ((mask & 4) != 0)
            {
                newH = MathF.Max(WindowMinHeight, oh - dy);
                newY = oy + (oh - newH);
            }

            rect = new UiRect(newX, newY, newW, newH);
        }
        else if (_state.ActiveId == resizeId && !_leftMouseDown)
        {
            _state.ActiveId = null;
        }

        _state.SetWindowRect(title, rect);

        titleBarRect = new UiRect(rect.X, rect.Y, rect.Width, titleBarHeight);
        collapseRect = new UiRect(
            rect.X + WindowPadding,
            rect.Y + (titleBarHeight - collapseSize) * 0.5f,
            collapseSize,
            collapseSize
        );
        closeRect = new UiRect(
            rect.X + rect.Width - WindowPadding - closeSize,
            rect.Y + (titleBarHeight - closeSize) * 0.5f,
            closeSize,
            closeSize
        );

        var nextCursorY = root.Cursor.Y + rect.Height + ItemSpacingY;
        _windowRootLayout = root with { Cursor = new UiVector2(root.LineStartX, nextCursorY) };

        _windowRect = rect;
        _hasWindowRect = true;

        var isActiveWindow = string.Equals(_state.ActiveWindowId, title, StringComparison.Ordinal);
        if (isActiveWindow)
        {
            PushOverlay();
            _windowOverlayStack.Push(true);
        }
        else
        {
            _windowOverlayStack.Push(false);
        }

        var windowBg = _theme.WindowBg;
        if (_hasNextWindowBgAlpha)
        {
            windowBg = ApplyAlpha(windowBg, _nextWindowBgAlpha);
            _hasNextWindowBgAlpha = false;
        }

        const float windowBorderThickness = 1f;
        var windowCornerRadius = GetWindowCornerRadius();
        if (_windowCollapsed)
        {
            AddRoundedTopRectFilled(rect, _theme.Border, windowCornerRadius);
        }
        else
        {
            AddRoundedRectFilled(rect, _theme.Border, windowCornerRadius);
        }

        var innerRect = new UiRect(
            rect.X + windowBorderThickness,
            rect.Y + windowBorderThickness,
            MathF.Max(0f, rect.Width - (windowBorderThickness * 2f)),
            MathF.Max(0f, rect.Height - (windowBorderThickness * 2f))
        );
        var innerRadius = MathF.Max(0f, windowCornerRadius - windowBorderThickness);
        if (_windowCollapsed)
        {
            AddRoundedTopRectFilled(innerRect, windowBg, innerRadius);
        }
        else
        {
            AddRoundedRectFilled(innerRect, windowBg, innerRadius);
        }

        var titleInnerRect = new UiRect(innerRect.X, innerRect.Y, innerRect.Width, MathF.Max(0f, titleBarHeight - windowBorderThickness));
        AddRoundedTopRectFilled(titleInnerRect, _theme.TitleBgActive, innerRadius);

        if (collapseHovered || collapseHeld)
        {
            var collapseBg = collapseHeld ? _theme.ButtonActive : _theme.ButtonHovered;
            AddRoundedRectFilled(collapseRect, collapseBg, MathF.Min(4f, collapseRect.Width * 0.3f));
        }

        var collapseRotationDegrees = AnimateToggleRotationDegrees(
            $"{title}##window_collapse",
            expanded: _windowCollapsed,
            collapsedDegrees: 0f,
            expandedDegrees: 180f,
            durationSeconds: 0.14f,
            easing: UiAnimationEasing.OutCubic
        );
        DrawChevronIcon(collapseRect, collapseRotationDegrees, scale: 2f / 3f, thickness: 1.2f, color: _theme.Text);

        if (closeHovered || closeHeld)
        {
            var closeBg = closeHeld ? _theme.ButtonActive : _theme.ButtonHovered;
            AddRoundedRectFilled(closeRect, closeBg, MathF.Min(4f, closeRect.Width * 0.3f));
        }

        var closeInset = MathF.Max(2f, closeRect.Width * 0.28f);
        var closeX1 = closeRect.X + closeInset;
        var closeY1 = closeRect.Y + closeInset;
        var closeX2 = closeRect.X + closeRect.Width - closeInset;
        var closeY2 = closeRect.Y + closeRect.Height - closeInset;
        _builder.AddLine(new UiVector2(closeX1, closeY1), new UiVector2(closeX2, closeY2), _theme.Text, 1.5f, _whiteTexture);
        _builder.AddLine(new UiVector2(closeX1, closeY2), new UiVector2(closeX2, closeY1), _theme.Text, 1.5f, _whiteTexture);

        var titlePos = new UiVector2(collapseRect.X + collapseRect.Width + 6f, rect.Y + (titleBarHeight - _lineHeight) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            title,
            titlePos,
            _theme.Text,
            _fontTexture,
            rect,
            _textSettings,
            _lineHeight
        );

        var contentHeight = MathF.Max(0f, rect.Height - titleBarHeight - WindowPadding);
        if (_windowCollapsed)
        {
            contentHeight = collapsedContentPeekHeight;
        }

        var clip = new UiRect(
            rect.X + WindowPadding,
            rect.Y + titleBarHeight,
            rect.Width - (WindowPadding * 2f),
            contentHeight
        );
        PushClipRect(clip, true);

        _windowContentStart = new UiVector2(rect.X + WindowPadding, rect.Y + titleBarHeight + ItemSpacingY);
        _windowContentMax = _windowContentStart;
        if (_hasNextWindowContentSize)
        {
            _windowContentMax = new UiVector2(
                _windowContentStart.X + _nextWindowContentSize.X,
                _windowContentStart.Y + _nextWindowContentSize.Y
            );
            _hasNextWindowContentSize = false;
        }

        var cursor = new UiVector2(
            _windowContentStart.X - _windowScrollX,
            _windowContentStart.Y - _windowScrollY
        );
        _layouts.Push(new UiLayoutState(cursor, false, 0f, cursor.X));
        // Grip rendering is deferred to EndWindow (after scrollbars) for correct z-order
    }

    private static UiVector2 RotatePoint(UiVector2 point, UiVector2 center, float sin, float cos)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return new UiVector2(
            center.X + (dx * cos) - (dy * sin),
            center.Y + (dx * sin) + (dy * cos)
        );
    }

    private float GetWindowCornerRadius()
    {
        return MathF.Min(12f, MathF.Max(6f, (_lineHeight * 0.45f) + 2f)) * 0.5f;
    }

    private void AddRoundedRectFilled(UiRect rect, UiColor color, float radius)
    {
        var w = MathF.Max(0f, rect.Width);
        var h = MathF.Max(0f, rect.Height);
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var r = MathF.Min(radius, MathF.Min(w, h) * 0.5f);
        if (r <= 0.01f)
        {
            AddRectFilled(rect, color, _whiteTexture);
            return;
        }

        AddRectFilled(new UiRect(rect.X + r, rect.Y, w - (r * 2f), h), color, _whiteTexture);
        AddRectFilled(new UiRect(rect.X, rect.Y + r, r, h - (r * 2f)), color, _whiteTexture);
        AddRectFilled(new UiRect(rect.X + w - r, rect.Y + r, r, h - (r * 2f)), color, _whiteTexture);

        AddCircleFilled(new UiVector2(rect.X + r, rect.Y + r), r, color, _whiteTexture, 10);
        AddCircleFilled(new UiVector2(rect.X + w - r, rect.Y + r), r, color, _whiteTexture, 10);
        AddCircleFilled(new UiVector2(rect.X + r, rect.Y + h - r), r, color, _whiteTexture, 10);
        AddCircleFilled(new UiVector2(rect.X + w - r, rect.Y + h - r), r, color, _whiteTexture, 10);
    }

    private void AddRoundedTopRectFilled(UiRect rect, UiColor color, float radius)
    {
        var w = MathF.Max(0f, rect.Width);
        var h = MathF.Max(0f, rect.Height);
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var r = MathF.Min(radius, MathF.Min(w * 0.5f, h));
        if (r <= 0.01f)
        {
            AddRectFilled(rect, color, _whiteTexture);
            return;
        }

        AddRectFilled(new UiRect(rect.X, rect.Y + r, w, h - r), color, _whiteTexture);
        AddRectFilled(new UiRect(rect.X + r, rect.Y, w - (r * 2f), r), color, _whiteTexture);

        AddCircleFilled(new UiVector2(rect.X + r, rect.Y + r), r, color, _whiteTexture, 10);
        AddCircleFilled(new UiVector2(rect.X + w - r, rect.Y + r), r, color, _whiteTexture, 10);
    }

    private bool IsWindowTopmostAtMouse(string title, UiRect rect)
    {
        if (!IsHovering(rect))
        {
            return false;
        }

        var order = _state.WindowOrder;
        var targetIndex = -1;
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], title, StringComparison.Ordinal))
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            return true;
        }

        for (var i = order.Count - 1; i > targetIndex; i--)
        {
            var other = order[i];
            if (!_state.TryGetWindowRect(other, out var otherRect))
            {
                continue;
            }

            if (IsHovering(otherRect))
            {
                return false;
            }
        }

        return true;
    }

    public void EndWindow()
    {
        if (_layouts.Count <= 1)
        {
            throw new InvalidOperationException("Layout stack underflow.");
        }

        // Scrollbar + mouse wheel handling (before popping layout/clip/overlay)
        if (_hasWindowRect && !_windowCollapsed && _currentWindowId is not null)
        {
            var titleBarHeight = _lineHeight + (WindowPadding * 2f);
            var contentAreaRect = new UiRect(
                _windowRect.X + 1f,
                _windowRect.Y + titleBarHeight,
                _windowRect.Width - 2f,
                _windowRect.Height - titleBarHeight - 1f
            );

            var visibleMaxX = _windowRect.X + _windowRect.Width - WindowPadding;
            var visibleMaxY = _windowRect.Y + _windowRect.Height - WindowPadding;
            var maxScrollY = MathF.Max(0f, _windowContentMax.Y - visibleMaxY);
            var maxScrollX = MathF.Max(0f, _windowContentMax.X - visibleMaxX);
            var contentHeight = MathF.Max(1f, _windowContentMax.Y - _windowContentStart.Y);
            var contentWidth = MathF.Max(1f, _windowContentMax.X - _windowContentStart.X);

            // Clamp persisted scroll to current content/viewport range.
            // This is required when window size increases and maxScroll shrinks.
            _windowScrollY = Math.Clamp(_windowScrollY, 0f, maxScrollY);
            _windowScrollX = Math.Clamp(_windowScrollX, 0f, maxScrollX);

            var hasVScroll = maxScrollY > 0f;
            var hasHScroll = maxScrollX > 0f;

            // Mouse wheel
            var isTopmost = IsWindowTopmostAtMouse(_currentWindowId, _windowRect);
            var hovered = IsHovering(_windowRect) && isTopmost;
            var popupBlocking = _popupTierDepth == 0 && IsMouseOverAnyBlockingPopup();
            if (hovered && !popupBlocking)
            {
                if (MathF.Abs(_mouseWheel) > 0.001f && maxScrollY > 0f)
                {
                    _windowScrollY = Math.Clamp(_windowScrollY - (_mouseWheel * _lineHeight * 3f), 0f, maxScrollY);
                }

                if (MathF.Abs(_mouseWheelHorizontal) > 0.001f && maxScrollX > 0f)
                {
                    _windowScrollX = Math.Clamp(_windowScrollX - (_mouseWheelHorizontal * _lineHeight * 3f), 0f, maxScrollX);
                }
            }

            // Vertical scrollbar (always reserve grip area at bottom)
            if (hasVScroll)
            {
                var bottomReserve = MathF.Max(hasHScroll ? ScrollbarSize : 0f, ResizeGripSize);
                var trackHeight = contentAreaRect.Height - bottomReserve;
                var trackRect = new UiRect(
                    contentAreaRect.X + contentAreaRect.Width - ScrollbarSize,
                    contentAreaRect.Y,
                    ScrollbarSize,
                    trackHeight
                );
                _windowScrollY = RenderScrollbarV(
                    $"{_currentWindowId}##wscrollv", trackRect, _windowScrollY, maxScrollY, contentHeight, _windowRect);
            }

            // Horizontal scrollbar (always reserve grip area at right)
            if (hasHScroll)
            {
                var rightReserve = MathF.Max(hasVScroll ? ScrollbarSize : 0f, ResizeGripSize);
                var trackWidth = contentAreaRect.Width - rightReserve;
                var trackRect = new UiRect(
                    contentAreaRect.X,
                    contentAreaRect.Y + contentAreaRect.Height - ScrollbarSize,
                    trackWidth,
                    ScrollbarSize
                );
                _windowScrollX = RenderScrollbarH(
                    $"{_currentWindowId}##wscrollh", trackRect, _windowScrollX, maxScrollX, contentWidth, _windowRect);
            }

            // Persist scroll for next frame
            _state.SetScrollX(_currentWindowId, _windowScrollX);
            _state.SetScrollY(_currentWindowId, _windowScrollY);

            // Render resize grip AFTER scrollbars (correct z-order)
            // 6 dots in triangular pattern: · / · · / · · ·
            var gripInset = _windowCollapsed ? 0f : MathF.Min(ResizeGripSize * 0.5f, GetWindowCornerRadius());
            const float gripOffset = 3f;
            var gripRect = new UiRect(
                _windowRect.X + _windowRect.Width - ResizeGripSize - gripInset + gripOffset,
                _windowRect.Y + _windowRect.Height - ResizeGripSize - gripInset + gripOffset,
                ResizeGripSize, ResizeGripSize);
            var resId = $"{_currentWindowId}##window_resize";
            var gripColor = _state.ActiveId == resId ? _theme.ButtonActive : IsHovering(gripRect) ? _theme.ButtonHovered : _theme.Button;
            const float dotSize = 2f;
            const float dotSpacing = 4f; // center-to-center distance
            // Pattern bounding box: 3 cols x 3 rows (but triangular), total = 2*dotSpacing + dotSize
            var patternSize = 2f * dotSpacing + dotSize;
            // Center pattern in gripRect (with 1px border offset)
            var patternStartX = gripRect.X + (gripRect.Width - 1f - patternSize) * 0.5f;
            var patternStartY = gripRect.Y + (gripRect.Height - 1f - patternSize) * 0.5f;
            PushClipRect(_windowRect, false);
            // row 0: 1 dot (col 2)
            // row 1: 2 dots (col 1, 2)
            // row 2: 3 dots (col 0, 1, 2)
            for (var row = 0; row < 3; row++)
            {
                for (var col = 2 - row; col < 3; col++)
                {
                    var dx = patternStartX + col * dotSpacing;
                    var dy = patternStartY + row * dotSpacing;
                    _builder.AddRectFilled(new UiRect(dx, dy, dotSize, dotSize), gripColor, _whiteTexture, _windowRect);
                }
            }
            PopClipRect();
        }

        _layouts.Pop();
        PopClipRect();
        if (_windowOverlayStack.Count > 0 && _windowOverlayStack.Pop())
        {
            PopOverlay();
        }

        if (_windowStateStack.Count > 0)
        {
            var state = _windowStateStack.Pop();
            _windowRect = state.WindowRect;
            _hasWindowRect = state.HasWindowRect;
            _currentWindowId = state.CurrentWindowId;
            _windowContentStart = state.WindowContentStart;
            _windowContentMax = state.WindowContentMax;
            _windowAppearing = state.WindowAppearing;
            _windowCollapsed = state.WindowCollapsed;
            _windowScrollX = state.WindowScrollX;
            _windowScrollY = state.WindowScrollY;

            _columnsActive = state.ColumnsActive;
            _columnsCount = state.ColumnsCount;
            _columnsIndex = state.ColumnsIndex;
            _columnsStartX = state.ColumnsStartX;
            _columnsStartY = state.ColumnsStartY;
            _columnsWidth = state.ColumnsWidth;
            _columnsMaxY = state.ColumnsMaxY;
            _columnYs = state.ColumnYs;
            _columnWidths = state.ColumnWidths;

            _tableActive = state.TableActive;
            _tableColumns = state.TableColumns;
            _tableColumn = state.TableColumn;
            _tableStartX = state.TableStartX;
            _tableRowY = state.TableRowY;
            _tableRowMaxY = state.TableRowMaxY;
            _tableColumnWidth = state.TableColumnWidth;
            _tableColumnLabels.Clear();
            if (state.TableColumnLabels.Length > 0)
            {
                _tableColumnLabels.AddRange(state.TableColumnLabels);
            }
            _tableSetupIndex = state.TableSetupIndex;
            _tableRowIndex = state.TableRowIndex;
            _tableColumnWidths = state.TableColumnWidths;
            _tableColumnAlign = state.TableColumnAlign;
            _tableId = state.TableId;
            _tableFlags = state.TableFlags;
            _tableStartY = state.TableStartY;
            _tableRect = state.TableRect;

            _inMenuBar = state.InMenuBar;
        }
        else
        {
            _hasWindowRect = false;
            _currentWindowId = null;
        }
    }

    public void BeginRow()
    {
        var current = _layouts.Peek();
        _layouts.Push(new UiLayoutState(current.Cursor, true, 0f, current.Cursor.X));
    }

    public void EndRow()
    {
        var row = _layouts.Pop();
        var parent = _layouts.Pop();
        var nextCursor = new UiVector2(parent.LineStartX, row.Cursor.Y + row.RowMaxHeight + RowSpacing);
        parent = parent with { Cursor = nextCursor };
        _layouts.Push(parent);
    }

    public void SetNextItemVerticalAlign(UiItemVerticalAlign align)
    {
        _nextItemVerticalAlign = align;
        _hasNextItemVerticalAlign = align != UiItemVerticalAlign.Top;
    }

    public void SameLine(float spacing = -1f, UiItemVerticalAlign verticalAlign = UiItemVerticalAlign.Top)
    {
        var current = _layouts.Pop();
        var gap = spacing < 0f ? ItemSpacingX : spacing;
        var cursor = new UiVector2(_lastItemPos.X + _lastItemSize.X + gap, _lastItemPos.Y);
        current = current with { Cursor = cursor };
        _layouts.Push(current);

        if (verticalAlign != UiItemVerticalAlign.Top)
        {
            SetNextItemVerticalAlign(verticalAlign);
        }
    }

    public void NewLine()
    {
        var current = _layouts.Pop();
        var y = _lastItemPos.Y + _lastItemSize.Y + ItemSpacingY;
        current = current with { Cursor = new UiVector2(current.LineStartX, y) };
        _layouts.Push(current);
    }

    public void Spacing()
    {
        var current = _layouts.Pop();
        current = current with { Cursor = new UiVector2(current.LineStartX, current.Cursor.Y + ItemSpacingY) };
        _layouts.Push(current);
    }

    public void Dummy(UiVector2 size)
    {
        _ = AdvanceCursor(size);
    }

    public void Separator()
    {
        if (!_hasWindowRect)
        {
            return;
        }

        var current = _layouts.Peek();
        var startX = _windowRect.X + WindowPadding;
        var endX = _windowRect.X + _windowRect.Width - WindowPadding;
        var y = current.Cursor.Y + ItemSpacingY * 0.5f;
        var rect = new UiRect(startX, y, MathF.Max(0f, endX - startX), 1f);
        AddRectFilled(rect, _theme.Separator, _whiteTexture);
        _ = AdvanceCursor(new UiVector2(0f, ItemSpacingY));
    }
}

