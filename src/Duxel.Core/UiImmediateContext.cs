using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private float WindowPadding = 8f;
    private float ItemSpacingX = 8f;
    private float ItemSpacingY = 4f;
    private float RowSpacing = 4f;
    private float ButtonPaddingX = 4f;
    private float ButtonPaddingY = 4f;
    private float CheckboxSpacing = 4f;
    private float InputWidth = 220f;
    private float SliderWidth = 220f;
    private float TreeIndent = 16f;
    private float ScrollbarSize = 14f;
    private float _windowFontScale = 1f;
    private const int TabSpaces = 4;
    private static readonly string TabSpacesString = new(' ', TabSpaces);
    private const float WindowMinWidth = 240f;
    private const float WindowMinHeight = 140f;
    private const float ResizeGripSize = 12f;

    private readonly UiState _state;
    private UiFontAtlas _fontAtlas;
    private readonly UiTextSettings _textSettings;
    private float _lineHeight;
    private readonly UiTextureId _fontTexture;
    private readonly UiTextureId _whiteTexture;
    private UiTheme _theme;
    private readonly UiRect _clipRect;
    private readonly UiVector2 _mousePosition;
    private readonly bool _leftMouseDown;
    private readonly bool _leftMousePressed;
    private readonly bool _leftMouseReleased;
    private readonly float _mouseWheel;
    private readonly float _mouseWheelHorizontal;
    private readonly IReadOnlyList<UiKeyEvent> _keyEvents;
    private readonly IReadOnlyList<UiCharEvent> _charEvents;
    private readonly IUiClipboard? _clipboard;
    private readonly UiVector2 _displaySize;
    private readonly float _keyRepeatDelaySeconds;
    private readonly float _keyRepeatRateSeconds;
    private readonly IUiImeHandler? _imeHandler;

    private readonly UiDrawListBuilder _baseBuilder;
    private readonly UiDrawListBuilder _overlayBuilder;
    private readonly UiDrawListBuilder _popupBuilder;
    private UiDrawListBuilder _builder;
    private readonly Stack<UiDrawListBuilder> _builderStack = new();
    private readonly Stack<UiRect> _clipStack = new();
    private UiStateStorage _stateStorage = new();
    private readonly Stack<UiFontState> _fontStack = new();
    private readonly Stack<UiLayoutState> _layouts = new();
    private UiLayoutState _windowRootLayout;
    private readonly Stack<UiWindowState> _windowStateStack = new();
    private readonly Stack<float> _indentStack = new();
    private readonly Stack<UiListBoxState> _listBoxStack = new();
    private readonly Stack<UiChildState> _childStack = new();
    private readonly Stack<UiMenuState> _menuStack = new();
    private readonly Stack<UiPopupState> _popupStack = new();
    private readonly Stack<UiRect> _comboStack = new();
    private readonly Stack<float> _itemWidthStack = new();
    private readonly Stack<UiItemFlags> _itemFlagStack = new();
    private readonly List<UiRect> _openMenuPopupRects = [];
    private readonly List<UiRect> _openMenuButtonRects = [];
    private int _popupTierDepth;
    private UiItemFlags _currentItemFlags;
    private readonly Stack<StyleColorEntry> _styleColorStack = new();
    private readonly Stack<StyleVarEntry> _styleVarStack = new();
    private readonly Stack<float> _textWrapStack = new();
    private readonly Stack<string> _idStack = new();
    private string _idPrefix = string.Empty;
    private readonly Stack<bool> _disabledStack = new();
    private readonly Stack<UiTheme> _themeStack = new();
    private readonly Stack<bool> _windowOverlayStack = new();
    private float _nextItemWidth;
    private string? _currentTabBarId;
    private string? _currentTabBarActiveKey;
    private bool _inMenuBar;
    private bool _tooltipActive;
    private float _tooltipPadding;
    private UiVector2 _tooltipOrigin;
    private float _tooltipMaxRight;
    private bool _columnsActive;
    private int _columnsCount;
    private int _columnsIndex;
    private float _columnsStartX;
    private float _columnsStartY;
    private float _columnsWidth;
    private float _columnsMaxY;
    private float[] _columnYs = [];
    private float[] _columnWidths = [];
    private bool _tableActive;
    private int _tableColumns;
    private int _tableColumn;
    private float _tableStartX;
    private float _tableRowY;
    private float _tableRowMaxY;
    private float _tableColumnWidth;
    private readonly List<string> _tableColumnLabels = [];
    private int _tableSetupIndex;
    private int _tableRowIndex;
    private float[] _tableColumnWidths = [];
    private float[] _tableColumnAlign = [];
    private string? _tableId;
    private UiTableFlags _tableFlags;
    private float _tableStartY;
    private UiRect _tableRect;

    private readonly record struct UiFontState(UiFontAtlas FontAtlas, float LineHeight);
    private readonly record struct StyleColorEntry(UiStyleColor Color, UiColor Previous);
    private readonly record struct StyleVarEntry(UiStyleVar Var, float PrevX, float PrevY, bool IsVector);
    private readonly record struct UiWindowState(
        UiRect WindowRect,
        bool HasWindowRect,
        string? CurrentWindowId,
        UiVector2 WindowContentStart,
        UiVector2 WindowContentMax,
        bool WindowAppearing,
        bool WindowCollapsed,
        float WindowScrollX,
        float WindowScrollY,
        bool ColumnsActive,
        int ColumnsCount,
        int ColumnsIndex,
        float ColumnsStartX,
        float ColumnsStartY,
        float ColumnsWidth,
        float ColumnsMaxY,
        float[] ColumnYs,
        float[] ColumnWidths,
        bool TableActive,
        int TableColumns,
        int TableColumn,
        float TableStartX,
        float TableRowY,
        float TableRowMaxY,
        float TableColumnWidth,
        string[] TableColumnLabels,
        int TableSetupIndex,
        int TableRowIndex,
        float[] TableColumnWidths,
        float[] TableColumnAlign,
        string? TableId,
        UiTableFlags TableFlags,
        float TableStartY,
        UiRect TableRect,
        bool InMenuBar
    );

    private UiVector2 _lastItemPos;
    private UiVector2 _lastItemSize;
    private string? _lastItemId;
    private bool _lastItemEdited;
    private bool _lastItemToggledOpen;
    private bool _lastItemToggledSelection;
    private long _lastItemSelectionUserData;
    private bool _hasLastItemSelectionUserData;
    private UiItemFlags _lastItemFlags;
    private UiRect _windowRect;
    private bool _hasWindowRect;
    private string? _currentWindowId;
    private UiVector2 _windowContentStart;
    private UiVector2 _windowContentMax;
    private bool _windowAppearing;
    private bool _windowCollapsed;
    private float _windowScrollX;
    private float _windowScrollY;
    private UiVector2 _nextWindowPos;
    private bool _hasNextWindowPos;
    private UiVector2 _nextWindowSize;
    private bool _hasNextWindowSize;
    private UiVector2 _nextWindowContentSize;
    private bool _hasNextWindowContentSize;
    private UiVector2 _nextWindowMinSize;
    private UiVector2 _nextWindowMaxSize;
    private bool _hasNextWindowSizeConstraints;
    private bool _nextWindowOpen = true;
    private bool _hasNextWindowOpen;
    private bool _nextWindowCollapsed;
    private bool _hasNextWindowCollapsed;
    private bool _hasNextWindowFocus;
    private float _nextWindowScrollX;
    private float _nextWindowScrollY;
    private bool _hasNextWindowScrollX;
    private bool _hasNextWindowScrollY;
    private float _nextWindowBgAlpha;
    private bool _hasNextWindowBgAlpha;
    private UiKey _nextItemShortcutKey;
    private KeyModifiers _nextItemShortcutModifiers;
    private bool _nextItemShortcutRepeat;
    private bool _hasNextItemShortcut;
    private bool _hasNextItemAllowOverlap;
    private long _nextItemSelectionUserData;
    private bool _hasNextItemSelectionUserData;
    private bool _hasNextItemOpen;
    private bool _nextItemOpen;
    private string? _nextItemStorageId;

    private bool _multiSelectActive;
    private UiMultiSelectFlags _multiSelectFlags;
    private int _multiSelectSelectionSize;
    private int _multiSelectItemsCount;

    private bool _dragDropActive;
    private bool _dragDropWithinSource;
    private bool _dragDropWithinTarget;
    private UiDragDropFlags _dragDropSourceFlags;
    private UiDragDropFlags _dragDropAcceptFlags;
    private string? _dragDropSourceId;
    private string? _dragDropTargetId;
    private int _dragDropMouseButton = (int)UiMouseButton.Left;
    private int _dragDropSourceFrame;
    private int _dragDropAcceptFrame;
    private UiRect _dragDropTargetRect;
    private UiRect _dragDropTargetClipRect;
    private bool _dragDropPayloadSet;
    private readonly UiDragDropPayload _dragDropPayload = new();
    private byte[]? _dragDropPayloadBuffer;

    private string? _lineInfoCacheValue;
    private string[]? _lineInfoCacheLines;
    private int[]? _lineInfoCacheStarts;

    public UiImmediateContext(
        UiState state,
        UiFontAtlas fontAtlas,
        UiTextSettings textSettings,
        float lineHeight,
        UiTextureId fontTexture,
        UiTextureId whiteTexture,
        UiTheme theme,
        UiStyle style,
        UiRect clipRect,
        UiVector2 mousePosition,
        bool leftMouseDown,
        bool leftMousePressed,
        bool leftMouseReleased,
        float mouseWheel,
        float mouseWheelHorizontal,
        IReadOnlyList<UiKeyEvent> keyEvents,
        IReadOnlyList<UiCharEvent> charEvents,
        IUiClipboard? clipboard,
        UiVector2 displaySize,
        UiKeyRepeatSettings keyRepeatSettings,
        IUiImeHandler? imeHandler,
        int reserveVertices,
        int reserveIndices,
        int reserveCommands
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fontAtlas);
        ArgumentNullException.ThrowIfNull(keyEvents);
        ArgumentNullException.ThrowIfNull(charEvents);
        ArgumentNullException.ThrowIfNull(style);

        _state = state;
        _fontAtlas = fontAtlas;
        _textSettings = textSettings;
        _lineHeight = lineHeight;
        _fontTexture = fontTexture;
        _whiteTexture = whiteTexture;
        _theme = theme;
        WindowPadding = style.WindowPadding.X;
        ItemSpacingX = style.ItemSpacing.X;
        ItemSpacingY = style.ItemSpacing.Y;
        ButtonPaddingX = style.ButtonPadding.X;
        ButtonPaddingY = style.ButtonPadding.Y;
        RowSpacing = style.RowSpacing;
        CheckboxSpacing = style.CheckboxSpacing;
        InputWidth = style.InputWidth;
        SliderWidth = style.SliderWidth;
        TreeIndent = style.TreeIndent;
        ScrollbarSize = style.ScrollbarSize;
        _clipRect = clipRect;
        _mousePosition = mousePosition;
        _leftMouseDown = leftMouseDown;
        _leftMousePressed = leftMousePressed;
        _leftMouseReleased = leftMouseReleased;
        // Shift+Wheel → horizontal scroll
        if ((_state.Modifiers & KeyModifiers.Shift) != 0 && MathF.Abs(mouseWheel) > 0.001f && MathF.Abs(mouseWheelHorizontal) < 0.001f)
        {
            _mouseWheel = 0f;
            _mouseWheelHorizontal = mouseWheel;
        }
        else
        {
            _mouseWheel = mouseWheel;
            _mouseWheelHorizontal = mouseWheelHorizontal;
        }
        _keyEvents = keyEvents;
        _charEvents = charEvents;
        _clipboard = clipboard;
        _displaySize = displaySize;
        _keyRepeatDelaySeconds = (float)keyRepeatSettings.InitialDelaySeconds;
        _keyRepeatRateSeconds = (float)keyRepeatSettings.RepeatIntervalSeconds;
        _imeHandler = imeHandler;

        _baseBuilder = new UiDrawListBuilder(clipRect);
        _overlayBuilder = new UiDrawListBuilder(clipRect);
        _popupBuilder = new UiDrawListBuilder(clipRect);
        _builder = _baseBuilder;
        _baseBuilder._SetDrawListSharedData(GetDrawListSharedData());
        _overlayBuilder._SetDrawListSharedData(GetDrawListSharedData());
        _popupBuilder._SetDrawListSharedData(GetDrawListSharedData());
        _baseBuilder.PushTexture(_fontTexture);
        _overlayBuilder.PushTexture(_fontTexture);
        _popupBuilder.PushTexture(_fontTexture);
        if (reserveVertices > 0 || reserveIndices > 0 || reserveCommands > 0)
        {
            _baseBuilder.Reserve(reserveVertices, reserveIndices, reserveCommands);
        }
        _clipStack.Push(clipRect);

        UpdateMouseClickState();

        var start = new UiVector2(clipRect.X + WindowPadding, clipRect.Y + WindowPadding);
        var rootLayout = new UiLayoutState(start, false, 0f, start.X);
        _layouts.Push(rootLayout);
        _windowRootLayout = rootLayout;
    }

    public UiPooledList<UiDrawList> BuildDrawLists()
    {
        var baseLists = _baseBuilder.Build();
        var overlayLists = _overlayBuilder.Build();
        var popupLists = _popupBuilder.Build();

        var totalCount = baseLists.Count + overlayLists.Count + popupLists.Count;
        if (totalCount == 0)
        {
            baseLists.Return();
            overlayLists.Return();
            popupLists.Return();
            return UiPooledList<UiDrawList>.FromArray(Array.Empty<UiDrawList>());
        }

        // Single-tier fast path
        if (overlayLists.Count == 0 && popupLists.Count == 0) return baseLists;
        if (baseLists.Count == 0 && popupLists.Count == 0) return overlayLists;
        if (baseLists.Count == 0 && overlayLists.Count == 0) return popupLists;

        // Merge: base → overlay (active window) → popup (menus/tooltips)
        var mergedBuffer = ArrayPool<UiDrawList>.Shared.Rent(totalCount);
        var offset = 0;
        if (baseLists.Count > 0)
        {
            baseLists.CopyTo(mergedBuffer.AsSpan(offset, baseLists.Count));
            offset += baseLists.Count;
        }
        if (overlayLists.Count > 0)
        {
            overlayLists.CopyTo(mergedBuffer.AsSpan(offset, overlayLists.Count));
            offset += overlayLists.Count;
        }
        if (popupLists.Count > 0)
        {
            popupLists.CopyTo(mergedBuffer.AsSpan(offset, popupLists.Count));
        }

        baseLists.Return();
        overlayLists.Return();
        popupLists.Return();

        return new UiPooledList<UiDrawList>(mergedBuffer, totalCount, pooled: true);
    }

    private UiVector2 AdvanceCursor(UiVector2 size)
    {
        var current = _layouts.Pop();
        var cursor = current.Cursor;
        if (_columnsActive && !current.IsRow)
        {
            var columnX = GetColumnsColumnX(_columnsIndex);
            var columnY = _columnYs[_columnsIndex];
            cursor = new UiVector2(columnX, columnY);
        }
        if (_tableActive && !current.IsRow)
        {
            var columnX = GetTableColumnX(_tableColumn);
            cursor = new UiVector2(columnX, _tableRowY);
        }
        UiVector2 next;
        if (current.IsRow)
        {
            next = new UiVector2(cursor.X + size.X + ItemSpacingX, cursor.Y);
            current = current with
            {
                Cursor = next,
                RowMaxHeight = MathF.Max(current.RowMaxHeight, size.Y)
            };
        }
        else
        {
            next = new UiVector2(current.LineStartX, cursor.Y + size.Y + ItemSpacingY);
            current = current with { Cursor = next };
            if (_columnsActive)
            {
                var nextY = cursor.Y + size.Y + ItemSpacingY;
                _columnYs[_columnsIndex] = nextY;
                _columnsMaxY = MathF.Max(_columnsMaxY, nextY);
                current = current with { Cursor = new UiVector2(current.Cursor.X, nextY) };
            }
            if (_tableActive)
            {
                var nextY = cursor.Y + size.Y + ItemSpacingY;
                _tableRowMaxY = MathF.Max(_tableRowMaxY, nextY);
                current = current with { Cursor = new UiVector2(current.LineStartX, _tableRowMaxY) };
            }
        }

        _layouts.Push(current);
        _lastItemPos = cursor;
        _lastItemSize = size;
        if (_tooltipActive)
        {
            _tooltipMaxRight = MathF.Max(_tooltipMaxRight, cursor.X + size.X);
        }
        if (_hasWindowRect && _childStack.Count == 0)
        {
            var unscrolledMax = new UiVector2(
                cursor.X + size.X + _windowScrollX,
                cursor.Y + size.Y + _windowScrollY
            );
            _windowContentMax = new UiVector2(
                MathF.Max(_windowContentMax.X, unscrolledMax.X),
                MathF.Max(_windowContentMax.Y, unscrolledMax.Y)
            );
        }
        return cursor;
    }

    private float GetTableColumnWidth(int index)
    {
        if ((uint)index < (uint)_tableColumnWidths.Length)
        {
            var width = _tableColumnWidths[index];
            if (width > 0f)
            {
                return width;
            }
        }

        return _tableColumnWidth;
    }

    private float GetTableColumnX(int index)
    {
        var x = _tableStartX;
        for (var i = 0; i < index; i++)
        {
            x += GetTableColumnWidth(i) + ItemSpacingX;
        }

        return x;
    }

    private float GetColumnsColumnWidth(int index)
    {
        if ((uint)index < (uint)_columnWidths.Length)
        {
            var width = _columnWidths[index];
            if (width > 0f)
            {
                return width;
            }
        }

        return _columnsWidth;
    }

    private float GetColumnsColumnX(int index)
    {
        var x = _columnsStartX;
        for (var i = 0; i < index; i++)
        {
            x += GetColumnsColumnWidth(i) + ItemSpacingX;
        }

        return x;
    }

    private float GetTableTotalWidth()
    {
        if (_tableColumns <= 0)
        {
            return 0f;
        }

        var width = 0f;
        for (var i = 0; i < _tableColumns; i++)
        {
            width += GetTableColumnWidth(i);
            if (i < _tableColumns - 1)
            {
                width += ItemSpacingX;
            }
        }

        return width;
    }

    private float ResolveItemWidth(float defaultWidth)
    {
        if (_nextItemWidth > 0f)
        {
            var width = _nextItemWidth;
            _nextItemWidth = 0f;
            return width;
        }

        return _itemWidthStack.Count > 0 ? _itemWidthStack.Peek() : defaultWidth;
    }

    private void PushIndent()
    {
        PushIndent(TreeIndent);
    }

    private void PushIndent(float width)
    {
        var current = _layouts.Pop();
        current = current with
        {
            Cursor = new UiVector2(current.Cursor.X + width, current.Cursor.Y),
            LineStartX = current.LineStartX + width
        };
        _layouts.Push(current);
        _indentStack.Push(width);
    }

    private void PopIndent()
    {
        if (_indentStack.Count == 0)
        {
            return;
        }

        var width = _indentStack.Pop();
        var current = _layouts.Pop();
        current = current with
        {
            Cursor = new UiVector2(current.Cursor.X - width, current.Cursor.Y),
            LineStartX = current.LineStartX - width
        };
        _layouts.Push(current);
    }

    private void PushListBoxLayout(string id, UiRect listRect, float scrollY)
    {
        PushClipRect(listRect, true);
        var cursor = new UiVector2(listRect.X + 2f, listRect.Y + 2f - scrollY);
        _layouts.Push(new UiLayoutState(cursor, false, 0f, listRect.X + 2f));
        _listBoxStack.Push(new UiListBoxState(id, listRect, scrollY));
    }

    private void PopListBoxLayout()
    {
        if (_listBoxStack.Count == 0)
        {
            return;
        }

        var cursorY = _layouts.Peek().Cursor.Y;
        _layouts.Pop();

        var state = _listBoxStack.Pop();

        // Clamp scroll to actual content height
        var contentHeight = cursorY - state.Rect.Y - 2f + state.ScrollY;
        var visibleHeight = state.Rect.Height - 4f;
        var maxScroll = MathF.Max(0f, contentHeight - visibleHeight);
        var clampedScrollY = Math.Clamp(state.ScrollY, 0f, maxScroll);

        // Render scrollbar before PopClipRect (respects parent clip)
        if (maxScroll > 0f)
        {
            var trackRect = new UiRect(
                state.Rect.X + state.Rect.Width - ScrollbarSize,
                state.Rect.Y,
                ScrollbarSize,
                state.Rect.Height
            );
            clampedScrollY = RenderScrollbarV($"{state.Id}##lbscroll", trackRect, clampedScrollY, maxScroll, contentHeight, CurrentClipRect);
        }

        PopClipRect();
        _state.SetScrollY(state.Id, clampedScrollY);
    }

    private bool IsHovering(UiRect rect)
    {
        var pos = _mousePosition;
        return pos.X >= rect.X && pos.X <= rect.X + rect.Width && pos.Y >= rect.Y && pos.Y <= rect.Y + rect.Height;
    }

    private bool IsMouseOverAnyOpenMenuPopup()
    {
        for (var i = 0; i < _openMenuPopupRects.Count; i++)
        {
            if (IsHovering(_openMenuPopupRects[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMouseOverAnyOpenMenuButton()
    {
        for (var i = 0; i < _openMenuButtonRects.Count; i++)
        {
            if (IsHovering(_openMenuButtonRects[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMouseOverAnyBlockingPopup()
    {
        var rects = _state.PreviousPopupBlockingRects;
        for (var i = 0; i < rects.Count; i++)
        {
            if (IsHovering(rects[i]))
            {
                return true;
            }
        }

        return false;
    }

    public float GetTextLineHeight() => _lineHeight * _textSettings.Scale * _windowFontScale;

    public float GetTextLineHeightWithSpacing() => GetTextLineHeight() + ItemSpacingY;

    public float GetFrameHeight() => GetTextLineHeight() + (ButtonPaddingY * 2f);

    public float GetFrameHeightWithSpacing() => GetFrameHeight() + ItemSpacingY;

    private UiRect CurrentClipRect => _clipStack.Count > 0 ? _clipStack.Peek() : _clipRect;

    private UiRect ResolveItemClipRect()
    {
        var clip = CurrentClipRect;
        if (!_tableActive || _tableColumns <= 0)
        {
            return clip;
        }

        var cellX = GetTableColumnX(_tableColumn);
        var cellWidth = GetTableColumnWidth(_tableColumn);
        var rowHeight = MathF.Max(GetFrameHeight(), _tableRowMaxY - _tableRowY);
        var cellRect = new UiRect(cellX, _tableRowY, cellWidth, rowHeight);
        return IntersectRect(clip, cellRect);
    }

    private void UpdateMouseClickState()
    {
        if (!_leftMousePressed)
        {
            return;
        }

        const float doubleClickMaxDistance = 6f;
        const double doubleClickTimeSeconds = 0.3;

        var now = _state.TimeSeconds;
        var elapsed = now - _state.LastClickTime;
        var dx = _mousePosition.X - _state.LastClickPos.X;
        var dy = _mousePosition.Y - _state.LastClickPos.Y;
        var distanceSq = (dx * dx) + (dy * dy);
        var withinDistance = distanceSq <= (doubleClickMaxDistance * doubleClickMaxDistance);
        var withinTime = elapsed >= 0 && elapsed <= doubleClickTimeSeconds;
        var count = withinTime && withinDistance ? _state.LastClickCount + 1 : 1;
        _state.SetLastClick(now, _mousePosition, count);
    }

    private static UiColor ApplyAlpha(UiColor color, float alpha)
    {
        var clamped = Math.Clamp(alpha, 0f, 1f);
        var rgba = color.Rgba;
        var baseAlpha = (byte)(rgba >> 24);
        var finalAlpha = (byte)Math.Clamp(baseAlpha * clamped, 0f, 255f);
        var rgb = rgba & 0x00FFFFFFu;
        return new UiColor((uint)(finalAlpha << 24) | rgb);
    }

    public void PushClipRect(UiRect rect, bool intersect)
    {
        var next = rect;
        if (intersect && _clipStack.Count > 0)
        {
            next = IntersectRect(CurrentClipRect, rect);
        }

        _clipStack.Push(next);
        _builder.PushClipRect(next, false);
    }

    public void PopClipRect()
    {
        if (_clipStack.Count > 1)
        {
            _clipStack.Pop();
            _builder.PopClipRect();
        }
    }

    private void PushOverlay()
    {
        _builderStack.Push(_builder);
        _builder = _overlayBuilder;
    }

    private void PopOverlay()
    {
        if (_builderStack.Count > 0)
        {
            _builder = _builderStack.Pop();
        }
        else
        {
            _builder = _baseBuilder;
        }
    }

    private void PushPopup()
    {
        _builderStack.Push(_builder);
        _builder = _popupBuilder;
        _popupTierDepth++;
    }

    private void PopPopup()
    {
        _popupTierDepth--;
        if (_builderStack.Count > 0)
        {
            _builder = _builderStack.Pop();
        }
        else
        {
            _builder = _baseBuilder;
        }
    }

    private void AddRectFilled(UiRect rect, UiColor color, UiTextureId textureId) =>
        _builder.AddRectFilled(rect, color, textureId, CurrentClipRect);

    private void AddCircleFilled(UiVector2 center, float radius, UiColor color, UiTextureId textureId, int segments = 12) =>
        _builder.AddCircleFilled(center, radius, color, textureId, CurrentClipRect, segments);

    private static string GetDisplayLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        var index = label.IndexOf("##", StringComparison.Ordinal);
        return index >= 0 ? label[..index] : label;
    }

    private string ResolveId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(_idPrefix) ? id : $"{_idPrefix}/{id}";
    }

    public void PushID(string id)
    {
        id ??= string.Empty;
        _idStack.Push(_idPrefix);
        if (!string.IsNullOrEmpty(id))
        {
            _idPrefix = string.IsNullOrEmpty(_idPrefix) ? id : $"{_idPrefix}/{id}";
        }
    }

    public void PushID(int id) => PushID(id.ToString(CultureInfo.InvariantCulture));

    public void PushID(nint id) => PushID(id.ToString(CultureInfo.InvariantCulture));

    public void PopID()
    {
        if (_idStack.Count == 0)
        {
            return;
        }

        _idPrefix = _idStack.Pop();
    }

    public string GetID(string id) => ResolveId(id ?? string.Empty);

    public string GetID(int id) => ResolveId(id.ToString(CultureInfo.InvariantCulture));

    public string GetID(nint id) => ResolveId(id.ToString(CultureInfo.InvariantCulture));

    public void PushFont(UiFontAtlas font)
    {
        ArgumentNullException.ThrowIfNull(font);
        _fontStack.Push(new UiFontState(_fontAtlas, _lineHeight));
        _fontAtlas = font;
        _lineHeight = font.LineHeight;
    }

    public void PopFont()
    {
        if (_fontStack.Count == 0)
        {
            return;
        }

        var state = _fontStack.Pop();
        _fontAtlas = state.FontAtlas;
        _lineHeight = state.LineHeight;
    }

    public UiFontAtlas GetFont() => _fontAtlas;

    public float GetFontSize() => _lineHeight * _textSettings.Scale * _windowFontScale;

    public void SetWindowFontScale(float scale)
    {
        _windowFontScale = MathF.Max(0.1f, scale);
    }

    public bool GetFontBaked() => _fontAtlas.Glyphs.Count > 0;

    public UiVector2 GetFontTexUvWhitePixel() => _fontAtlas.GetWhitePixelUv();

    public void PushStyleColor(UiStyleColor color, UiColor value)
    {
        var previous = GetStyleColor(color);
        _styleColorStack.Push(new StyleColorEntry(color, previous));
        SetStyleColor(color, value);
    }

    public void PushStyleColor(UiStyleColor color, UiVector4 value)
    {
        PushStyleColor(color, Ui.ColorConvertFloat4ToU32(value));
    }

    public void PopStyleColor(int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        while (count-- > 0 && _styleColorStack.Count > 0)
        {
            var entry = _styleColorStack.Pop();
            SetStyleColor(entry.Color, entry.Previous);
        }
    }

    public void PushStyleVar(UiStyleVar styleVar, float value)
    {
        if (styleVar == UiStyleVar.WindowPadding || styleVar == UiStyleVar.ItemSpacing || styleVar == UiStyleVar.FramePadding)
        {
            PushStyleVar(styleVar, new UiVector2(value, value));
            return;
        }

        var previous = GetStyleVarFloat(styleVar);
        _styleVarStack.Push(new StyleVarEntry(styleVar, previous, 0f, false));
        SetStyleVar(styleVar, value);
    }

    public void PushStyleVar(UiStyleVar styleVar, UiVector2 value)
    {
        var previous = GetStyleVarVector(styleVar);
        _styleVarStack.Push(new StyleVarEntry(styleVar, previous.X, previous.Y, true));
        SetStyleVar(styleVar, value);
    }

    public void PushStyleVarX(UiStyleVar styleVar, float value)
    {
        var current = GetStyleVarVector(styleVar);
        PushStyleVar(styleVar, new UiVector2(value, current.Y));
    }

    public void PushStyleVarY(UiStyleVar styleVar, float value)
    {
        var current = GetStyleVarVector(styleVar);
        PushStyleVar(styleVar, new UiVector2(current.X, value));
    }

    public void PopStyleVar(int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        while (count-- > 0 && _styleVarStack.Count > 0)
        {
            var entry = _styleVarStack.Pop();
            if (entry.IsVector)
            {
                SetStyleVar(entry.Var, new UiVector2(entry.PrevX, entry.PrevY));
            }
            else
            {
                SetStyleVar(entry.Var, entry.PrevX);
            }
        }
    }

    public void PushItemFlag(UiItemFlags flags, bool enabled)
    {
        _itemFlagStack.Push(_currentItemFlags);
        if (enabled)
        {
            _currentItemFlags |= flags;
        }
        else
        {
            _currentItemFlags &= ~flags;
        }
    }

    public void PopItemFlag()
    {
        if (_itemFlagStack.Count == 0)
        {
            return;
        }

        _currentItemFlags = _itemFlagStack.Pop();
    }

    public void BeginDisabled(bool disabled = true)
    {
        _disabledStack.Push(disabled);
        if (!disabled)
        {
            return;
        }

        _themeStack.Push(_theme);
        PushItemFlag(UiItemFlags.Disabled, true);
        _theme = ApplyThemeAlpha(_theme, 0.5f);
    }

    public void EndDisabled()
    {
        if (_disabledStack.Count == 0)
        {
            return;
        }

        var applied = _disabledStack.Pop();
        if (!applied)
        {
            return;
        }

        PopItemFlag();
        if (_themeStack.Count > 0)
        {
            _theme = _themeStack.Pop();
        }
    }

    public void PushTextWrapPos(float wrapPosX = 0f)
    {
        _textWrapStack.Push(wrapPosX);
    }

    public void PopTextWrapPos()
    {
        if (_textWrapStack.Count == 0)
        {
            return;
        }

        _textWrapStack.Pop();
    }

    private static UiRect IntersectRect(UiRect a, UiRect b)
    {
        var left = MathF.Max(a.X, b.X);
        var top = MathF.Max(a.Y, b.Y);
        var right = MathF.Min(a.X + a.Width, b.X + b.Width);
        var bottom = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        var width = MathF.Max(0f, right - left);
        var height = MathF.Max(0f, bottom - top);
        return new UiRect(left, top, width, height);
    }

    private UiColor GetStyleColor(UiStyleColor color) => color switch
    {
        UiStyleColor.Text => _theme.Text,
        UiStyleColor.TextDisabled => _theme.TextDisabled,
        UiStyleColor.WindowBg => _theme.WindowBg,
        UiStyleColor.TitleBg => _theme.TitleBg,
        UiStyleColor.TitleBgActive => _theme.TitleBgActive,
        UiStyleColor.MenuBarBg => _theme.MenuBarBg,
        UiStyleColor.PopupBg => _theme.PopupBg,
        UiStyleColor.Border => _theme.Border,
        UiStyleColor.FrameBg => _theme.FrameBg,
        UiStyleColor.FrameBgHovered => _theme.FrameBgHovered,
        UiStyleColor.FrameBgActive => _theme.FrameBgActive,
        UiStyleColor.Header => _theme.Header,
        UiStyleColor.HeaderHovered => _theme.HeaderHovered,
        UiStyleColor.HeaderActive => _theme.HeaderActive,
        UiStyleColor.Button => _theme.Button,
        UiStyleColor.ButtonHovered => _theme.ButtonHovered,
        UiStyleColor.ButtonActive => _theme.ButtonActive,
        UiStyleColor.Tab => _theme.Tab,
        UiStyleColor.TabHovered => _theme.TabHovered,
        UiStyleColor.TabActive => _theme.TabActive,
        UiStyleColor.CheckMark => _theme.CheckMark,
        UiStyleColor.SliderGrab => _theme.SliderGrab,
        UiStyleColor.SliderGrabActive => _theme.SliderGrabActive,
        UiStyleColor.PlotLines => _theme.PlotLines,
        UiStyleColor.PlotHistogram => _theme.PlotHistogram,
        UiStyleColor.Separator => _theme.Separator,
        UiStyleColor.TableHeaderBg => _theme.TableHeaderBg,
        UiStyleColor.TableRowBg0 => _theme.TableRowBg0,
        UiStyleColor.TableRowBg1 => _theme.TableRowBg1,
        UiStyleColor.TableBorder => _theme.TableBorder,
        UiStyleColor.TextSelectedBg => _theme.TextSelectedBg,
        UiStyleColor.ScrollbarBg => _theme.ScrollbarBg,
        UiStyleColor.ScrollbarGrab => _theme.ScrollbarGrab,
        UiStyleColor.ScrollbarGrabHovered => _theme.ScrollbarGrabHovered,
        UiStyleColor.ScrollbarGrabActive => _theme.ScrollbarGrabActive,
        _ => _theme.Text,
    };

    public string GetStyleColorName(UiStyleColor color) => color.ToString();

    public UiColor GetColorU32(UiStyleColor color) => GetStyleColor(color);

    public UiColor GetColorU32(UiVector4 color) => Ui.ColorConvertFloat4ToU32(color);

    public UiVector4 GetStyleColorVec4(UiStyleColor color) => Ui.ColorConvertU32ToFloat4(GetStyleColor(color));

    private static UiTheme ApplyThemeAlpha(UiTheme theme, float alpha)
    {
        return theme with
        {
            Text = ApplyAlpha(theme.Text, alpha),
            TextDisabled = ApplyAlpha(theme.TextDisabled, alpha),
            WindowBg = ApplyAlpha(theme.WindowBg, alpha),
            TitleBg = ApplyAlpha(theme.TitleBg, alpha),
            TitleBgActive = ApplyAlpha(theme.TitleBgActive, alpha),
            MenuBarBg = ApplyAlpha(theme.MenuBarBg, alpha),
            PopupBg = ApplyAlpha(theme.PopupBg, alpha),
            Border = ApplyAlpha(theme.Border, alpha),
            FrameBg = ApplyAlpha(theme.FrameBg, alpha),
            FrameBgHovered = ApplyAlpha(theme.FrameBgHovered, alpha),
            FrameBgActive = ApplyAlpha(theme.FrameBgActive, alpha),
            Header = ApplyAlpha(theme.Header, alpha),
            HeaderHovered = ApplyAlpha(theme.HeaderHovered, alpha),
            HeaderActive = ApplyAlpha(theme.HeaderActive, alpha),
            Button = ApplyAlpha(theme.Button, alpha),
            ButtonHovered = ApplyAlpha(theme.ButtonHovered, alpha),
            ButtonActive = ApplyAlpha(theme.ButtonActive, alpha),
            Tab = ApplyAlpha(theme.Tab, alpha),
            TabHovered = ApplyAlpha(theme.TabHovered, alpha),
            TabActive = ApplyAlpha(theme.TabActive, alpha),
            CheckMark = ApplyAlpha(theme.CheckMark, alpha),
            SliderGrab = ApplyAlpha(theme.SliderGrab, alpha),
            SliderGrabActive = ApplyAlpha(theme.SliderGrabActive, alpha),
            PlotLines = ApplyAlpha(theme.PlotLines, alpha),
            PlotHistogram = ApplyAlpha(theme.PlotHistogram, alpha),
            Separator = ApplyAlpha(theme.Separator, alpha),
            TableHeaderBg = ApplyAlpha(theme.TableHeaderBg, alpha),
            TableRowBg0 = ApplyAlpha(theme.TableRowBg0, alpha),
            TableRowBg1 = ApplyAlpha(theme.TableRowBg1, alpha),
            TableBorder = ApplyAlpha(theme.TableBorder, alpha),
            TextSelectedBg = ApplyAlpha(theme.TextSelectedBg, alpha),
        };
    }

    private void SetStyleColor(UiStyleColor color, UiColor value)
    {
        _theme = color switch
        {
            UiStyleColor.Text => _theme with { Text = value },
            UiStyleColor.TextDisabled => _theme with { TextDisabled = value },
            UiStyleColor.WindowBg => _theme with { WindowBg = value },
            UiStyleColor.TitleBg => _theme with { TitleBg = value },
            UiStyleColor.TitleBgActive => _theme with { TitleBgActive = value },
            UiStyleColor.MenuBarBg => _theme with { MenuBarBg = value },
            UiStyleColor.PopupBg => _theme with { PopupBg = value },
            UiStyleColor.Border => _theme with { Border = value },
            UiStyleColor.FrameBg => _theme with { FrameBg = value },
            UiStyleColor.FrameBgHovered => _theme with { FrameBgHovered = value },
            UiStyleColor.FrameBgActive => _theme with { FrameBgActive = value },
            UiStyleColor.Header => _theme with { Header = value },
            UiStyleColor.HeaderHovered => _theme with { HeaderHovered = value },
            UiStyleColor.HeaderActive => _theme with { HeaderActive = value },
            UiStyleColor.Button => _theme with { Button = value },
            UiStyleColor.ButtonHovered => _theme with { ButtonHovered = value },
            UiStyleColor.ButtonActive => _theme with { ButtonActive = value },
            UiStyleColor.Tab => _theme with { Tab = value },
            UiStyleColor.TabHovered => _theme with { TabHovered = value },
            UiStyleColor.TabActive => _theme with { TabActive = value },
            UiStyleColor.CheckMark => _theme with { CheckMark = value },
            UiStyleColor.SliderGrab => _theme with { SliderGrab = value },
            UiStyleColor.SliderGrabActive => _theme with { SliderGrabActive = value },
            UiStyleColor.PlotLines => _theme with { PlotLines = value },
            UiStyleColor.PlotHistogram => _theme with { PlotHistogram = value },
            UiStyleColor.Separator => _theme with { Separator = value },
            UiStyleColor.TableHeaderBg => _theme with { TableHeaderBg = value },
            UiStyleColor.TableRowBg0 => _theme with { TableRowBg0 = value },
            UiStyleColor.TableRowBg1 => _theme with { TableRowBg1 = value },
            UiStyleColor.TableBorder => _theme with { TableBorder = value },
            UiStyleColor.TextSelectedBg => _theme with { TextSelectedBg = value },
            UiStyleColor.ScrollbarBg => _theme with { ScrollbarBg = value },
            UiStyleColor.ScrollbarGrab => _theme with { ScrollbarGrab = value },
            UiStyleColor.ScrollbarGrabHovered => _theme with { ScrollbarGrabHovered = value },
            UiStyleColor.ScrollbarGrabActive => _theme with { ScrollbarGrabActive = value },
            _ => _theme,
        };
    }

    public double GetTime() => _state.TimeSeconds;

    public int GetFrameCount() => _state.FrameCount;

    public UiViewport GetMainViewport()
    {
        var pos = new UiVector2(0f, 0f);
        return new UiViewport(pos, _displaySize, pos, _displaySize);
    }

    public UiTextureId WhiteTextureId => _whiteTexture;

    public UiTextureId FontTextureId => _fontTexture;

    public bool GetVSync() => _state.VSync;

    public void SetVSync(bool enable) => _state.VSync = enable;

    public float GetNewFrameTimeMs() => _state.NewFrameTimeMs;
    public float GetRenderTimeMs() => _state.RenderTimeMs;
    public float GetSubmitTimeMs() => _state.SubmitTimeMs;

    public UiDrawListBuilder GetBackgroundDrawList() => _baseBuilder;

    public UiDrawListBuilder GetForegroundDrawList() => _overlayBuilder;

    public UiDrawListSharedData GetDrawListSharedData()
    {
        var scaledSettings = new UiTextSettings(
            _textSettings.Scale * _windowFontScale,
            _textSettings.LineHeightScale,
            _textSettings.PixelSnap,
            _textSettings.UseBaseline
        );
        return new UiDrawListSharedData(_fontAtlas, scaledSettings, _lineHeight * _windowFontScale);
    }

    public void SetStateStorage(UiStateStorage storage)
    {
        _stateStorage = storage ?? new UiStateStorage();
    }

    public UiStateStorage GetStateStorage() => _stateStorage;

    private UiVector2 GetStyleVarVector(UiStyleVar styleVar) => styleVar switch
    {
        UiStyleVar.WindowPadding => new UiVector2(WindowPadding, WindowPadding),
        UiStyleVar.ItemSpacing => new UiVector2(ItemSpacingX, ItemSpacingY),
        UiStyleVar.FramePadding => new UiVector2(ButtonPaddingX, ButtonPaddingY),
        _ => default,
    };

    private float GetStyleVarFloat(UiStyleVar styleVar) => styleVar switch
    {
        UiStyleVar.IndentSpacing => TreeIndent,
        _ => 0f,
    };

    private void SetStyleVar(UiStyleVar styleVar, UiVector2 value)
    {
        switch (styleVar)
        {
            case UiStyleVar.WindowPadding:
                WindowPadding = MathF.Max(0f, (value.X + value.Y) * 0.5f);
                break;
            case UiStyleVar.ItemSpacing:
                ItemSpacingX = MathF.Max(0f, value.X);
                ItemSpacingY = MathF.Max(0f, value.Y);
                break;
            case UiStyleVar.FramePadding:
                ButtonPaddingX = MathF.Max(0f, value.X);
                ButtonPaddingY = MathF.Max(0f, value.Y);
                break;
        }
    }

    private void SetStyleVar(UiStyleVar styleVar, float value)
    {
        if (styleVar == UiStyleVar.IndentSpacing)
        {
            TreeIndent = MathF.Max(0f, value);
        }
    }

    private float GetWrapWidth(UiVector2 cursor)
    {
        if (!_hasWindowRect || _textWrapStack.Count == 0)
        {
            return 0f;
        }

        var wrapPos = _textWrapStack.Peek();
        var wrapX = wrapPos <= 0f
            ? _windowRect.X + _windowRect.Width - WindowPadding
            : _windowRect.X + wrapPos;
        return MathF.Max(0f, wrapX - cursor.X);
    }

    private UiRect ClampRectToDisplay(UiRect rect)
    {
        var maxX = MathF.Max(0f, _displaySize.X - rect.Width);
        var maxY = MathF.Max(0f, _displaySize.Y - rect.Height);
        var x = Math.Clamp(rect.X, 0f, maxX);
        var y = Math.Clamp(rect.Y, 0f, maxY);
        return new UiRect(x, y, rect.Width, rect.Height);
    }

    private bool ItemHoverable(string id, UiRect rect)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if ((_currentItemFlags & UiItemFlags.Disabled) != 0)
        {
            return false;
        }

        // Block input for items not in popup tier when mouse is over any popup
        if (_popupTierDepth == 0 && IsMouseOverAnyBlockingPopup())
        {
            return false;
        }

        if (_hasWindowRect && _currentWindowId is not null && IsHovering(_windowRect) && !IsWindowTopmostAtMouse(_currentWindowId, _windowRect))
        {
            return false;
        }

        var clip = CurrentClipRect;
        if (_mousePosition.X < clip.X || _mousePosition.X > clip.X + clip.Width || _mousePosition.Y < clip.Y || _mousePosition.Y > clip.Y + clip.Height)
        {
            return false;
        }

        var clipped = IntersectRect(clip, rect);
        if (clipped.Width <= 0f || clipped.Height <= 0f)
        {
            return false;
        }

        var hovered = IsHovering(rect);
        if (hovered)
        {
            _state.SetHoveredId(id);
        }

        return hovered;
    }

    private bool ButtonBehavior(string id, UiRect rect, out bool hovered, out bool held)
    {
        var resolvedId = ResolveId(id);
        if ((_currentItemFlags & UiItemFlags.Disabled) != 0)
        {
            hovered = false;
            held = false;
            _lastItemId = resolvedId;
            _lastItemFlags = _currentItemFlags;
            if (_hasNextItemAllowOverlap)
            {
                _lastItemFlags |= UiItemFlags.AllowOverlap;
            }
            _hasNextItemAllowOverlap = false;
            _lastItemEdited = false;
            _lastItemToggledOpen = false;
            _lastItemToggledSelection = false;
            if (_hasNextItemSelectionUserData)
            {
                _lastItemSelectionUserData = _nextItemSelectionUserData;
                _hasLastItemSelectionUserData = true;
                _hasNextItemSelectionUserData = false;
            }
            else
            {
                _hasLastItemSelectionUserData = false;
            }
            return false;
        }
        hovered = ItemHoverable(resolvedId, rect);
        _lastItemId = resolvedId;
        _lastItemFlags = _currentItemFlags;
        if (_hasNextItemAllowOverlap)
        {
            _lastItemFlags |= UiItemFlags.AllowOverlap;
        }
        _hasNextItemAllowOverlap = false;
        _lastItemToggledSelection = false;
        if (_hasNextItemSelectionUserData)
        {
            _lastItemSelectionUserData = _nextItemSelectionUserData;
            _hasLastItemSelectionUserData = true;
            _hasNextItemSelectionUserData = false;
        }
        else
        {
            _hasLastItemSelectionUserData = false;
        }

        var shortcutPressed = false;
        if (_hasNextItemShortcut)
        {
            shortcutPressed = IsKeyChordPressed(_nextItemShortcutKey, _nextItemShortcutModifiers, _nextItemShortcutRepeat);
            _hasNextItemShortcut = false;
        }

        if (hovered && _leftMousePressed)
        {
            _state.ActiveId = resolvedId;
        }

        held = _state.ActiveId == resolvedId && _leftMouseDown;
        var pressed = false;
        if (_state.ActiveId == resolvedId && !_leftMouseDown)
        {
            pressed = hovered;
            _state.ActiveId = null;
        }

        if (shortcutPressed)
        {
            pressed = true;
        }

        _lastItemEdited = pressed;
        _lastItemToggledOpen = false;

        return pressed;
    }

    private int ClampCaret(string value, int caretIndex)
    {
        if (caretIndex < 0)
        {
            return 0;
        }

        if (caretIndex > value.Length)
        {
            return value.Length;
        }

        return caretIndex;
    }

    private int GetCaretIndexFromMouse(string value, float textStartX)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var localX = MathF.Max(0f, _mousePosition.X - textStartX);
        return GetCaretIndexFromText(value.AsSpan(), localX);
    }

    private float MeasureTextWidth(string value, int length)
    {
        if (length <= 0 || string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        var safeLength = Math.Min(length, value.Length);
        return MeasureTextWidthSpan(value.AsSpan(0, safeLength));
    }

    private float MeasureTextWidthSpan(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return 0f;
        }

        var scale = _textSettings.Scale * _windowFontScale;
        var lineHeight = _lineHeight * scale;
        var hasKerning = _fontAtlas.Kerning.Count > 0;
        var width = 0f;
        var hasPrev = false;
        var prevChar = 0;
        var hasSurrogate = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsSurrogate(text[i]))
            {
                hasSurrogate = true;
                break;
            }
        }

        if (!hasSurrogate)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var code = text[i];
                if (code == '\n')
                {
                    break;
                }

                if (hasPrev && hasKerning)
                {
                    width += _fontAtlas.GetKerning(prevChar, code) * scale;
                }

                var hasGlyph = IsHangulCodepoint(code)
                    ? _fontAtlas.TryGetGlyph(code, out var glyph)
                    : _fontAtlas.GetGlyphOrFallback(code, out glyph);
                if (!hasGlyph)
                {
                    width += lineHeight * 0.5f;
                    hasPrev = false;
                }
                else
                {
                    width += glyph.AdvanceX * scale;
                    hasPrev = true;
                    prevChar = code;
                }
            }
        }
        else
        {
            var index = 0;
            while (index < text.Length)
            {
                var status = Rune.DecodeFromUtf16(text[index..], out var rune, out var consumed);
                if (status != OperationStatus.Done)
                {
                    rune = Rune.ReplacementChar;
                    consumed = Math.Max(consumed, 1);
                }

                if (rune.Value == '\n')
                {
                    break;
                }

                if (hasPrev && hasKerning)
                {
                    width += _fontAtlas.GetKerning(prevChar, rune.Value) * scale;
                }

                var hasGlyph = IsHangulCodepoint(rune.Value)
                    ? _fontAtlas.TryGetGlyph(rune.Value, out var glyph)
                    : _fontAtlas.GetGlyphOrFallback(rune.Value, out glyph);
                if (!hasGlyph)
                {
                    width += lineHeight * 0.5f;
                    hasPrev = false;
                }
                else
                {
                    width += glyph.AdvanceX * scale;
                    hasPrev = true;
                    prevChar = rune.Value;
                }

                index += consumed;
            }
        }

        return width;
    }

    private float MeasureMaxLineWidth(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        var max = 0f;
        var span = value.AsSpan();
        var start = 0;
        for (var i = 0; i <= span.Length; i++)
        {
            if (i != span.Length && span[i] != '\n')
            {
                continue;
            }

            var width = MeasureTextWidthSpan(span.Slice(start, i - start));
            if (width > max)
            {
                max = width;
            }

            start = i + 1;
        }

        return max;
    }

    private static string SanitizeText(string text, bool multiline)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (multiline)
        {
            if (text.AsSpan().IndexOfAny('\r', '\t') < 0)
            {
                return text;
            }

            return text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Replace("\t", TabSpacesString, StringComparison.Ordinal);
        }

        if (text.AsSpan().IndexOfAny('\r', '\n', '\t') < 0)
        {
            return text;
        }

        return text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\t", TabSpacesString, StringComparison.Ordinal);
    }

    private (string[] Lines, int[] Starts) GetLineInfo(string value)
    {
        if (ReferenceEquals(value, _lineInfoCacheValue) && _lineInfoCacheLines is not null && _lineInfoCacheStarts is not null)
        {
            return (_lineInfoCacheLines, _lineInfoCacheStarts);
        }

        var lines = value.Split('\n');
        var starts = new int[lines.Length];
        var index = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            starts[i] = index;
            index += lines[i].Length + 1;
        }

        _lineInfoCacheValue = value;
        _lineInfoCacheLines = lines;
        _lineInfoCacheStarts = starts;
        return (lines, starts);
    }

    private int GetCaretIndexFromMouseMultiline(string value, float textStartX, float textStartY, float lineHeight)
    {
        var (lines, starts) = GetLineInfo(value);
        var localY = _mousePosition.Y - textStartY;
        var lineIndex = Math.Clamp((int)MathF.Floor(localY / lineHeight), 0, lines.Length - 1);
        var lineText = lines[lineIndex];
        var localX = MathF.Max(0f, _mousePosition.X - textStartX);
        return starts[lineIndex] + GetCaretIndexFromText(lineText.AsSpan(), localX);
    }

    private int GetCaretIndexFromText(ReadOnlySpan<char> text, float localX)
    {
        if (text.IsEmpty)
        {
            return 0;
        }

        var scale = _textSettings.Scale * _windowFontScale;
        var lineHeight = _lineHeight * scale;
        var hasKerning = _fontAtlas.Kerning.Count > 0;
        var width = 0f;
        var hasPrev = false;
        var prevChar = 0;
        var index = 0;

        while (index < text.Length)
        {
            var start = index;
            var status = Rune.DecodeFromUtf16(text[start..], out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = Math.Max(consumed, 1);
            }

            if (rune.Value == '\n')
            {
                return start;
            }

            if (hasPrev && hasKerning)
            {
                width += _fontAtlas.GetKerning(prevChar, rune.Value) * scale;
            }

            var hasGlyph = IsHangulCodepoint(rune.Value)
                ? _fontAtlas.TryGetGlyph(rune.Value, out var glyph)
                : _fontAtlas.GetGlyphOrFallback(rune.Value, out glyph);
            if (!hasGlyph)
            {
                width += lineHeight * 0.5f;
                hasPrev = false;
            }
            else
            {
                width += glyph.AdvanceX * scale;
                hasPrev = true;
                prevChar = rune.Value;
            }

            if (width >= localX)
            {
                return start;
            }

            index = start + consumed;
        }

        return text.Length;
    }

    private int GetLineStartIndex(string value, int caretIndex)
    {
        if (caretIndex <= 0)
        {
            return 0;
        }

        var index = Math.Min(caretIndex, value.Length);
        for (var i = index - 1; i >= 0; i--)
        {
            if (value[i] == '\n')
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static bool IsHangulCodepoint(int codepoint)
    {
        return (codepoint >= 0xAC00 && codepoint <= 0xD7A3)
            || (codepoint >= 0x1100 && codepoint <= 0x11FF)
            || (codepoint >= 0x3130 && codepoint <= 0x318F);
    }

    private int GetLineEndIndex(string value, int caretIndex)
    {
        if (caretIndex >= value.Length)
        {
            return value.Length;
        }

        for (var i = caretIndex; i < value.Length; i++)
        {
            if (value[i] == '\n')
            {
                return i;
            }
        }

        return value.Length;
    }

    private int MoveCaretVertical(string value, int caretIndex, int lineDelta)
    {
        var (lines, starts) = GetLineInfo(value);
        var lineIndex = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineStart = starts[i];
            var lineEnd = lineStart + lines[i].Length;
            if (caretIndex <= lineEnd || i == lines.Length - 1)
            {
                lineIndex = i;
                break;
            }
        }

        var column = Math.Max(0, caretIndex - starts[lineIndex]);
        var nextLine = Math.Clamp(lineIndex + lineDelta, 0, lines.Length - 1);
        var nextColumn = Math.Min(column, lines[nextLine].Length);
        return starts[nextLine] + nextColumn;
    }

    private float EnsureCaretVisibleMultiline(string value, int caretIndex, float scrollY, float viewHeight, float lineHeight)
    {
        var (lines, starts) = GetLineInfo(value);
        var lineIndex = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineStart = starts[i];
            var lineEnd = lineStart + lines[i].Length;
            if (caretIndex <= lineEnd || i == lines.Length - 1)
            {
                lineIndex = i;
                break;
            }
        }

        var caretY = lineIndex * lineHeight;
        var caretBottom = caretY + lineHeight;
        if (caretY < scrollY)
        {
            return caretY;
        }

        if (caretBottom > scrollY + viewHeight)
        {
            return caretBottom - viewHeight;
        }

        return scrollY;
    }

    private (float X, float Y) GetCaretPositionMultiline(string value, int caretIndex, float textStartX, float textStartY, float lineHeight)
    {
        var (lines, starts) = GetLineInfo(value);
        var lineIndex = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineStart = starts[i];
            var lineEnd = lineStart + lines[i].Length;
            if (caretIndex <= lineEnd || i == lines.Length - 1)
            {
                lineIndex = i;
                break;
            }
        }

        var column = Math.Max(0, caretIndex - starts[lineIndex]);
        var x = textStartX + MeasureTextWidth(lines[lineIndex], column);
        var y = textStartY + (lineIndex * lineHeight);
        return (x, y);
    }

    private float EnsureCaretVisibleHorizontal(string value, int caretIndex, float scrollX, float viewWidth)
    {
        var (caretX, _) = GetCaretPositionMultiline(value, caretIndex, 0f, 0f, _lineHeight);
        if (caretX < scrollX)
        {
            return caretX;
        }

        if (caretX > scrollX + viewWidth)
        {
            return caretX - viewWidth;
        }

        return scrollX;
    }

    private void DrawSelectionMultiline(string value, UiTextSelection selection, float textStartX, float textStartY, float lineHeight, UiRect inputRect)
    {
        if (!selection.HasSelection)
        {
            return;
        }

        var (start, end) = GetSelectionRange(selection);
        var (lines, starts) = GetLineInfo(value);
        for (var i = 0; i < lines.Length; i++)
        {
            var lineStart = starts[i];
            var lineEnd = lineStart + lines[i].Length;
            if (end < lineStart || start > lineEnd)
            {
                continue;
            }

            var selStart = Math.Max(start, lineStart);
            var selEnd = Math.Min(end, lineEnd);
            var startX = textStartX + MeasureTextWidth(lines[i], selStart - lineStart);
            var endX = textStartX + MeasureTextWidth(lines[i], selEnd - lineStart);
            if (end > lineEnd)
            {
                endX = textStartX + MeasureTextWidth(lines[i], lines[i].Length);
            }

            var y = textStartY + (i * lineHeight);
            var rect = new UiRect(startX, y + 2f, MathF.Max(0f, endX - startX), lineHeight - 4f);
            if (rect.Width > 0f && rect.Y >= inputRect.Y && rect.Y <= inputRect.Y + inputRect.Height)
            {
                _builder.AddRectFilled(rect, _theme.TextSelectedBg, _whiteTexture, IntersectRect(CurrentClipRect, inputRect));
            }
        }
    }

    private UiTextSelection ClampSelection(UiTextSelection selection, int length, int caretIndex)
    {
        var start = Math.Clamp(selection.Start, 0, length);
        var end = Math.Clamp(selection.End, 0, length);
        if (start == end && caretIndex != start)
        {
            start = caretIndex;
            end = caretIndex;
        }

        return new UiTextSelection(start, end);
    }

    private (int Start, int End) GetSelectionRange(UiTextSelection selection)
    {
        return selection.Start <= selection.End
            ? (selection.Start, selection.End)
            : (selection.End, selection.Start);
    }

    private static bool IsWordChar(char c) => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';
    private static bool IsPunctuationChar(char c) => !IsWordChar(c) && !char.IsWhiteSpace(c);

    private int FindWordBoundaryLeft(string value, int caretIndex)
    {
        if (caretIndex <= 0 || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var i = Math.Min(caretIndex - 1, value.Length - 1);
        while (i > 0 && char.IsWhiteSpace(value[i]))
        {
            i--;
        }

        if (i <= 0)
        {
            return 0;
        }

        if (IsWordChar(value[i]))
        {
            while (i > 0 && IsWordChar(value[i - 1]))
            {
                i--;
            }

            return i;
        }

        while (i > 0 && IsPunctuationChar(value[i - 1]))
        {
            i--;
        }

        return i;
    }

    private int FindWordBoundaryRight(string value, int caretIndex)
    {
        if (caretIndex >= value.Length || string.IsNullOrEmpty(value))
        {
            return value.Length;
        }

        var i = caretIndex;
        while (i < value.Length && char.IsWhiteSpace(value[i]))
        {
            i++;
        }

        if (i >= value.Length)
        {
            return value.Length;
        }

        if (IsWordChar(value[i]))
        {
            while (i < value.Length && IsWordChar(value[i]))
            {
                i++;
            }

            return i;
        }

        while (i < value.Length && IsPunctuationChar(value[i]))
        {
            i++;
        }

        return i;
    }

    private void SelectWordAt(string value, ref int caretIndex, ref UiTextSelection selection)
    {
        if (string.IsNullOrEmpty(value))
        {
            selection = new UiTextSelection(caretIndex, caretIndex);
            return;
        }

        var index = Math.Clamp(caretIndex, 0, value.Length - 1);
        if (IsWordChar(value[index]))
        {
            var start = index;
            while (start > 0 && IsWordChar(value[start - 1]))
            {
                start--;
            }

            var end = index + 1;
            while (end < value.Length && IsWordChar(value[end]))
            {
                end++;
            }

            selection = new UiTextSelection(start, end);
            caretIndex = end;
            return;
        }

        if (IsPunctuationChar(value[index]))
        {
            var start = index;
            while (start > 0 && IsPunctuationChar(value[start - 1]))
            {
                start--;
            }

            var end = index + 1;
            while (end < value.Length && IsPunctuationChar(value[end]))
            {
                end++;
            }

            selection = new UiTextSelection(start, end);
            caretIndex = end;
            return;
        }

        selection = new UiTextSelection(caretIndex, caretIndex);
    }

    private void ApplyCaretMove(ref int caretIndex, ref UiTextSelection selection, int next, bool shift)
    {
        if (shift)
        {
            if (!selection.HasSelection)
            {
                selection = new UiTextSelection(caretIndex, caretIndex);
            }

            selection = selection with { End = next };
        }
        else
        {
            selection = new UiTextSelection(next, next);
        }

        caretIndex = next;
    }

    private bool DeleteSelection(ref string value, ref int caretIndex, ref UiTextSelection selection)
    {
        if (!selection.HasSelection)
        {
            return false;
        }

        var (start, end) = GetSelectionRange(selection);
        value = ReplaceRange(value, start, end - start, string.Empty);
        caretIndex = start;
        selection = new UiTextSelection(start, start);
        return true;
    }

    private bool DeleteSelection(ref UiTextEditBuffer buffer, ref string text, ref int caretIndex, ref UiTextSelection selection)
    {
        if (!selection.HasSelection)
        {
            return false;
        }

        var (start, end) = GetSelectionRange(selection);
        buffer.Replace(start, end - start, string.Empty);
        text = buffer.Text;
        caretIndex = start;
        selection = new UiTextSelection(start, start);
        return true;
    }

    private bool InsertText(ref string value, ref int caretIndex, ref UiTextSelection selection, string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var (start, end) = GetSelectionRange(selection);
        var selectionLength = selection.HasSelection ? end - start : 0;
        var allowed = Math.Max(0, maxLength - (value.Length - selectionLength));
        if (allowed <= 0)
        {
            return false;
        }

        if (text.Length > allowed)
        {
            text = text[..allowed];
        }

        var insertPos = selection.HasSelection ? start : caretIndex;
        value = ReplaceRange(value, insertPos, selectionLength, text);
        caretIndex = insertPos + text.Length;
        selection = new UiTextSelection(caretIndex, caretIndex);
        return true;
    }

    private bool InsertText(ref UiTextEditBuffer buffer, ref string text, ref int caretIndex, ref UiTextSelection selection, string insert, int maxLength)
    {
        if (string.IsNullOrEmpty(insert))
        {
            return false;
        }

        var (start, end) = GetSelectionRange(selection);
        var selectionLength = selection.HasSelection ? end - start : 0;
        var allowed = Math.Max(0, maxLength - (text.Length - selectionLength));
        if (allowed <= 0)
        {
            return false;
        }

        if (insert.Length > allowed)
        {
            insert = insert[..allowed];
        }

        var insertPos = selection.HasSelection ? start : caretIndex;
        buffer.Replace(insertPos, selectionLength, insert);
        text = buffer.Text;
        caretIndex = insertPos + insert.Length;
        selection = new UiTextSelection(caretIndex, caretIndex);
        return true;
    }

    private static string ReplaceRange(string value, int start, int length, string insert)
    {
        var newLength = value.Length - length + insert.Length;
        return string.Create(newLength, (value, start, length, insert), static (span, state) =>
        {
            var (source, s, len, ins) = state;
            source.AsSpan(0, s).CopyTo(span);
            ins.AsSpan().CopyTo(span[s..]);
            source.AsSpan(s + len).CopyTo(span[(s + ins.Length)..]);
        });
    }

    private int GetShiftLineDownTarget(string value, int caretIndex)
    {
        var lineEnd = GetLineEndIndex(value, caretIndex);
        if (caretIndex < lineEnd)
        {
            return lineEnd;
        }

        var nextLine = MoveCaretVertical(value, caretIndex, 1);
        return GetLineEndIndex(value, nextLine);
    }

    private int GetShiftLineUpTarget(string value, int caretIndex)
    {
        var lineStart = GetLineStartIndex(value, caretIndex);
        if (caretIndex > lineStart)
        {
            return lineStart;
        }

        var prevLine = MoveCaretVertical(value, caretIndex, -1);
        return GetLineStartIndex(value, prevLine);
    }

    private static bool IsRepeatableKey(UiKey key)
    {
        return key is UiKey.Backspace or UiKey.Delete or UiKey.LeftArrow or UiKey.RightArrow or UiKey.UpArrow or UiKey.DownArrow or UiKey.Home or UiKey.End or UiKey.PageUp or UiKey.PageDown;
    }

    private bool IsCaretBlinkOn()
    {
        const double blinkIntervalSeconds = 0.5;
        var elapsed = _state.TimeSeconds - _state.CaretBlinkStartSeconds;
        if (elapsed <= 0)
        {
            return true;
        }
        return ((int)Math.Floor(elapsed / blinkIntervalSeconds) & 1) == 0;
    }

    private void GetImeFontMetrics(string value, int caretIndex, out float pixelHeight, out float pixelWidth)
    {
        _ = value;
        _ = caretIndex;

        var scale = _textSettings.Scale * _windowFontScale;
        pixelHeight = MathF.Max(1f, (_fontAtlas.Ascent - _fontAtlas.Descent) * scale);

        pixelWidth = 0f;
    }

    private void ResetCaretBlink()
    {
        _state.ResetCaretBlink();
    }

    private bool HasKeyDownEventThisFrame(UiKey key)
    {
        foreach (var keyEvent in _keyEvents)
        {
            if (keyEvent.IsDown && keyEvent.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRepeatTriggered(UiKey key)
    {
        if (HasKeyDownEventThisFrame(key))
        {
            return false;
        }

        if (!_state.IsKeyDown(key))
        {
            _state.ClearRepeatTime(key);
            return false;
        }

        var now = _state.TimeSeconds;
        if (!_state.TryGetRepeatTime(key, out var nextTime))
        {
            _state.SetRepeatTime(key, now + _keyRepeatDelaySeconds);
            return false;
        }

        if (now >= nextTime)
        {
            _state.SetRepeatTime(key, now + _keyRepeatRateSeconds);
            return true;
        }

        return false;
    }

    private bool HandleRepeatSingleLine(UiTextEditBuffer buffer, ref string text, ref int caretIndex, ref UiTextSelection selection)
    {
        var changed = false;
        var ctrl = (_state.Modifiers & KeyModifiers.Ctrl) != 0;
        var shift = (_state.Modifiers & KeyModifiers.Shift) != 0;

        if (IsRepeatTriggered(UiKey.Backspace))
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

        if (IsRepeatTriggered(UiKey.Delete))
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

        if (IsRepeatTriggered(UiKey.LeftArrow))
        {
            var next = ctrl ? FindWordBoundaryLeft(text, caretIndex) : Math.Max(caretIndex - 1, 0);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.RightArrow))
        {
            var next = ctrl ? FindWordBoundaryRight(text, caretIndex) : Math.Min(caretIndex + 1, text.Length);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.Home))
        {
            var next = ctrl ? 0 : GetLineStartIndex(text, caretIndex);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.End))
        {
            var next = ctrl ? text.Length : GetLineEndIndex(text, caretIndex);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        return changed;
    }

    private bool HandleRepeatMultiline(UiTextEditBuffer buffer, ref string text, ref int caretIndex, ref UiTextSelection selection, float inputHeight)
    {
        var changed = false;
        var ctrl = (_state.Modifiers & KeyModifiers.Ctrl) != 0;
        var shift = (_state.Modifiers & KeyModifiers.Shift) != 0;

        if (IsRepeatTriggered(UiKey.Backspace))
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

        if (IsRepeatTriggered(UiKey.Delete))
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

        if (IsRepeatTriggered(UiKey.LeftArrow))
        {
            var next = ctrl ? FindWordBoundaryLeft(text, caretIndex) : Math.Max(caretIndex - 1, 0);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.RightArrow))
        {
            var next = ctrl ? FindWordBoundaryRight(text, caretIndex) : Math.Min(caretIndex + 1, text.Length);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.UpArrow))
        {
            var next = shift
                ? GetShiftLineUpTarget(text, caretIndex)
                : MoveCaretVertical(text, caretIndex, -1);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.DownArrow))
        {
            var next = shift
                ? GetShiftLineDownTarget(text, caretIndex)
                : MoveCaretVertical(text, caretIndex, 1);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.Home))
        {
            var next = ctrl ? 0 : GetLineStartIndex(text, caretIndex);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.End))
        {
            var next = ctrl ? text.Length : GetLineEndIndex(text, caretIndex);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.PageUp))
        {
            var visibleLines = Math.Max(1, (int)MathF.Floor((inputHeight - 8f) / _lineHeight));
            var next = MoveCaretVertical(text, caretIndex, -visibleLines);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        if (IsRepeatTriggered(UiKey.PageDown))
        {
            var visibleLines = Math.Max(1, (int)MathF.Floor((inputHeight - 8f) / _lineHeight));
            var next = MoveCaretVertical(text, caretIndex, visibleLines);
            ApplyCaretMove(ref caretIndex, ref selection, next, shift);
        }

        return changed;
    }

    private IUiClipboard GetClipboardOrThrow()
    {
        return _clipboard ?? throw new InvalidOperationException("Clipboard is not configured.");
    }

    private readonly record struct UiLayoutState(UiVector2 Cursor, bool IsRow, float RowMaxHeight, float LineStartX);
    private readonly record struct UiListBoxState(string Id, UiRect Rect, float ScrollY);
    private readonly record struct UiChildState(string Id, UiRect Rect, UiVector2 Cursor, float ScrollY);
    private readonly record struct UiMenuState(
        string Id,
        string MenuSetKey,
        string SubmenuSetKey,
        UiRect ButtonRect,
        UiRect PopupRect,
        UiVector2 StartPos,
        float MinContentWidth,
        float MaxContentWidth
    );
    private readonly record struct UiPopupState(string Id, UiRect Rect);
}

