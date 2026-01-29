using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Duxel.Core;

public sealed class UiState
{
    private const int DebugLogMaxEntries = 2048;
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _cursors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiTextSelection> _selections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiTextHistory> _histories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiTextEditBuffer> _editBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _textBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiTableSortState[]> _tableSort = new(StringComparer.Ordinal);
    private readonly HashSet<string> _tableSortDirty = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _scrollY = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _scrollX = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiRect> _windowRects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _windowCollapsed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _windowOpen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _windowExpandedSizes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _popupOpenMousePos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _menuSizes = new(StringComparer.Ordinal);
    private readonly List<string> _windowOrder = new();
    private readonly List<string> _debugLogEntries = new();
    private string? _activeWindowId;
    private readonly HashSet<UiKey> _keysDown = new();
    private readonly Dictionary<UiKey, double> _repeatNext = new();
    private string? _activeId;
    private string? _hoveredId;
    private string? _previousHoveredId;
    private string? _focusedId;
    private string? _previousActiveId;
    private bool _hoverLoggedThisFrame;
    private string? _openComboId;
    private string? _lastClickId;
    private double _lastClickTime;
    private UiVector2 _lastClickPos;
    private int _lastClickCount;
    private UiMouseCursor _mouseCursor;
    private bool _wantCaptureKeyboardNextFrame;
    private bool _wantCaptureMouseNextFrame;
    private bool _navCursorVisible = true;
    private double _timeSeconds;
    private double _caretBlinkStartSeconds;
    private KeyModifiers _modifiers;
    private UiTheme? _requestedTheme;
    private int _frameCount;

    public bool GetBool(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_bools.TryGetValue(key, out var value))
        {
            return value;
        }

        _bools[key] = defaultValue;
        return defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _bools[key] = value;
    }

    public bool ToggleBool(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var next = !GetBool(key, defaultValue);
        _bools[key] = next;
        return next;
    }

    public Action<string>? DebugLog { get; set; }

    public IReadOnlyList<string> DebugLogEntries => _debugLogEntries;

    public string? ActiveId
    {
        get => _activeId;
        set
        {
            if (!string.Equals(_activeId, value, StringComparison.Ordinal))
            {
                LogDebug($"ActiveId: {_activeId ?? "<null>"} -> {value ?? "<null>"}");
            }
            _activeId = value;
        }
    }

    public string? PreviousActiveId => _previousActiveId;

    public string? FocusedId
    {
        get => _focusedId;
        set => _focusedId = value;
    }

    public string? HoveredId => _hoveredId;

    public string? ActiveWindowId
    {
        get => _activeWindowId;
        set => _activeWindowId = value;
    }

    public string? OpenComboId
    {
        get => _openComboId;
        set
        {
            if (!string.Equals(_openComboId, value, StringComparison.Ordinal))
            {
                LogDebug($"OpenComboId: {_openComboId ?? "<null>"} -> {value ?? "<null>"}");
            }
            _openComboId = value;
        }
    }

    public double TimeSeconds => _timeSeconds;
    public int FrameCount => _frameCount;
    public KeyModifiers Modifiers => _modifiers;
    public double CaretBlinkStartSeconds => _caretBlinkStartSeconds;
    public double LastClickTime => _lastClickTime;
    public UiVector2 LastClickPos => _lastClickPos;
    public int LastClickCount => _lastClickCount;
    public UiMouseCursor MouseCursor
    {
        get => _mouseCursor;
        set => _mouseCursor = value;
    }

    public bool WantCaptureKeyboardNextFrame
    {
        get => _wantCaptureKeyboardNextFrame;
        set => _wantCaptureKeyboardNextFrame = value;
    }

    public bool WantCaptureMouseNextFrame
    {
        get => _wantCaptureMouseNextFrame;
        set => _wantCaptureMouseNextFrame = value;
    }

    public bool NavCursorVisible
    {
        get => _navCursorVisible;
        set => _navCursorVisible = value;
    }

    public void SetLastClick(double timeSeconds, UiVector2 position, int count)
    {
        _lastClickTime = timeSeconds;
        _lastClickPos = position;
        _lastClickCount = count;
    }

    public void SetPopupOpenMousePos(string key, UiVector2 position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _popupOpenMousePos[key] = position;
    }

    public UiVector2 GetPopupOpenMousePos(string key, UiVector2 fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _popupOpenMousePos.TryGetValue(key, out var value) ? value : fallback;
    }

    public UiVector2 GetMenuSize(string key, UiVector2 fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _menuSizes.TryGetValue(key, out var value) ? value : fallback;
    }

    public void SetMenuSize(string key, UiVector2 size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _menuSizes[key] = size;
    }

    public void AdvanceTime(float deltaTime)
    {
        _timeSeconds += deltaTime;
    }

    public void BeginFrame()
    {
        _frameCount++;
        _previousActiveId = _activeId;
        _previousHoveredId = _hoveredId;
        _hoverLoggedThisFrame = false;
        _hoveredId = null;
        _mouseCursor = UiMouseCursor.Arrow;
    }

    public void EndFrame()
    {
        if (_hoverLoggedThisFrame)
        {
            return;
        }

        if (!string.Equals(_previousHoveredId, _hoveredId, StringComparison.Ordinal))
        {
            LogDebug($"HoveredId: {_previousHoveredId ?? "<null>"} -> {_hoveredId ?? "<null>"}");
            _hoverLoggedThisFrame = true;
        }
    }

    public void LogDebug(string? message)
    {
        message ??= string.Empty;
        _debugLogEntries.Add(message);
        if (_debugLogEntries.Count > DebugLogMaxEntries)
        {
            _debugLogEntries.RemoveRange(0, _debugLogEntries.Count - DebugLogMaxEntries);
        }

        DebugLog?.Invoke(message);
    }

    public void ClearDebugLog() => _debugLogEntries.Clear();

    public void RequestTheme(UiTheme theme) => _requestedTheme = theme;

    public bool TryConsumeTheme(out UiTheme theme)
    {
        if (_requestedTheme.HasValue)
        {
            theme = _requestedTheme.Value;
            _requestedTheme = null;
            return true;
        }

        theme = default;
        return false;
    }

    public void SetHoveredId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _hoveredId = id;
    }

    public void ResetCaretBlink()
    {
        _caretBlinkStartSeconds = _timeSeconds;
    }

    public void UpdateModifiers(IReadOnlyList<UiKeyEvent> keyEvents)
    {
        if (keyEvents.Count > 0)
        {
            _modifiers = keyEvents[^1].Modifiers;
        }
    }

    public void UpdateInput(IReadOnlyList<UiKeyEvent> keyEvents)
    {
        UpdateModifiers(keyEvents);
        if (keyEvents.Count == 0)
        {
            return;
        }

        foreach (var keyEvent in keyEvents)
        {
            if (keyEvent.IsDown)
            {
                _keysDown.Add(keyEvent.Key);
            }
            else
            {
                _keysDown.Remove(keyEvent.Key);
                _repeatNext.Remove(keyEvent.Key);
            }
        }
    }

    public void ClearInputKeys()
    {
        _keysDown.Clear();
        _repeatNext.Clear();
    }

    public bool IsKeyDown(UiKey key) => _keysDown.Contains(key);
    public bool HasRepeatTime(UiKey key) => _repeatNext.ContainsKey(key);
    public bool TryGetRepeatTime(UiKey key, out double nextTime) => _repeatNext.TryGetValue(key, out nextTime);
    public void SetRepeatTime(UiKey key, double nextTime) => _repeatNext[key] = nextTime;
    public void ClearRepeatTime(UiKey key) => _repeatNext.Remove(key);

    public int GetCursor(string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_cursors.TryGetValue(key, out var value))
        {
            return value;
        }

        _cursors[key] = defaultValue;
        return defaultValue;
    }

    public void SetCursor(string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _cursors[key] = value;
    }

    public int EnsureWindowOrder(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var index = _windowOrder.IndexOf(key);
        if (index >= 0)
        {
            return index;
        }

        _windowOrder.Add(key);
        return _windowOrder.Count - 1;
    }

    public void BringWindowToFront(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var index = _windowOrder.IndexOf(key);
        if (index < 0)
        {
            _windowOrder.Add(key);
            return;
        }

        if (index == _windowOrder.Count - 1)
        {
            return;
        }

        _windowOrder.RemoveAt(index);
        _windowOrder.Add(key);
    }

    public IReadOnlyList<string> WindowOrder => _windowOrder;

    public bool TryGetWindowRect(string key, out UiRect rect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _windowRects.TryGetValue(key, out rect);
    }

    public UiRect GetWindowRect(string key, UiRect defaultRect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_windowRects.TryGetValue(key, out var rect))
        {
            return rect;
        }

        _windowRects[key] = defaultRect;
        return defaultRect;
    }

    public void SetWindowRect(string key, UiRect rect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windowRects[key] = rect;
    }

    public bool GetWindowCollapsed(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_windowCollapsed.TryGetValue(key, out var value))
        {
            return value;
        }

        _windowCollapsed[key] = defaultValue;
        return defaultValue;
    }

    public void SetWindowCollapsed(string key, bool collapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windowCollapsed[key] = collapsed;
    }

    public UiVector2 GetWindowExpandedSize(string key, UiVector2 fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _windowExpandedSizes.TryGetValue(key, out var value) ? value : fallback;
    }

    public void SetWindowExpandedSize(string key, UiVector2 size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windowExpandedSizes[key] = size;
    }

    public bool GetWindowOpen(string key, bool defaultValue = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_windowOpen.TryGetValue(key, out var value))
        {
            return value;
        }

        _windowOpen[key] = defaultValue;
        return defaultValue;
    }

    public void SetWindowOpen(string key, bool open)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windowOpen[key] = open;
    }

    public UiTextSelection GetSelection(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_selections.TryGetValue(key, out var selection))
        {
            return selection;
        }

        selection = new UiTextSelection(0, 0);
        _selections[key] = selection;
        return selection;
    }

    public void SetSelection(string key, UiTextSelection selection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _selections[key] = selection;
    }

    public void ClearSelection(string key)
    {
        SetSelection(key, new UiTextSelection(0, 0));
    }

    public UiTextHistory GetHistory(string key, string initialValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_histories.TryGetValue(key, out var history))
        {
            return history;
        }

        history = new UiTextHistory(initialValue ?? string.Empty);
        _histories[key] = history;
        return history;
    }

    public UiTextEditBuffer GetEditBuffer(string key, string initialValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_editBuffers.TryGetValue(key, out var buffer))
        {
            buffer.Sync(initialValue ?? string.Empty);
            return buffer;
        }

        buffer = new UiTextEditBuffer(initialValue ?? string.Empty);
        _editBuffers[key] = buffer;
        return buffer;
    }

    public string GetTextBuffer(string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_textBuffers.TryGetValue(key, out var value))
        {
            return value;
        }

        _textBuffers[key] = fallback ?? string.Empty;
        return _textBuffers[key];
    }

    public void SetTextBuffer(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _textBuffers[key] = value ?? string.Empty;
    }

    public UiTableSortState GetTableSort(string key, int defaultColumn = 0, bool defaultAscending = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_tableSort.TryGetValue(key, out var state) && state.Length > 0)
        {
            return state[0];
        }

        var next = new UiTableSortState(defaultColumn, defaultAscending);
        _tableSort[key] = [next];
        return next;
    }

    public void SetTableSort(string key, UiTableSortState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_tableSort.TryGetValue(key, out var current) || current.Length == 0 || current[0] != state)
        {
            _tableSort[key] = [state];
            _tableSortDirty.Add(key);
        }
        else
        {
            _tableSort[key] = current;
        }
    }

    public IReadOnlyList<UiTableSortState> GetTableSortSpecs(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _tableSort.TryGetValue(key, out var state) ? state : Array.Empty<UiTableSortState>();
    }

    public void SetTableSortSpecs(string key, ReadOnlySpan<UiTableSortState> specs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (specs.Length == 0)
        {
            _tableSort[key] = Array.Empty<UiTableSortState>();
            _tableSortDirty.Add(key);
            return;
        }

        var next = specs.ToArray();
        if (!_tableSort.TryGetValue(key, out var current) || !current.AsSpan().SequenceEqual(next))
        {
            _tableSort[key] = next;
            _tableSortDirty.Add(key);
        }
        else
        {
            _tableSort[key] = current;
        }
    }

    public bool IsTableSortDirty(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _tableSortDirty.Contains(key);
    }

    public void ClearTableSortDirty(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _tableSortDirty.Remove(key);
    }

    public float GetScrollY(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_scrollY.TryGetValue(key, out var value))
        {
            return value;
        }

        _scrollY[key] = 0f;
        return 0f;
    }

    public void SetScrollY(string key, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _scrollY[key] = value;
    }

    public float GetScrollX(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_scrollX.TryGetValue(key, out var value))
        {
            return value;
        }

        _scrollX[key] = 0f;
        return 0f;
    }

    public void SetScrollX(string key, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _scrollX[key] = value;
    }

    public bool RegisterClick(string key, UiVector2 position, double doubleClickSeconds = 0.3, float maxDistance = 6f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var dx = position.X - _lastClickPos.X;
        var dy = position.Y - _lastClickPos.Y;
        var withinDistance = (dx * dx) + (dy * dy) <= maxDistance * maxDistance;
        var withinTime = (_timeSeconds - _lastClickTime) <= doubleClickSeconds;
        var isDouble = _lastClickId == key && withinDistance && withinTime;

        _lastClickId = key;
        _lastClickTime = _timeSeconds;
        _lastClickPos = position;

        return isDouble;
    }

    public void ReleasePooledBuffers()
    {
        foreach (var history in _histories.Values)
        {
            history.ReleasePooled();
        }

        _histories.Clear();
        _editBuffers.Clear();
    }
}

public readonly record struct UiTextSelection(int Start, int End)
{
    public bool HasSelection => Start != End;
}

public sealed class UiTextHistory
{
    private const int DefaultCapacity = 8;
    private string[] _items;
    private int _count;
    private int _index;

    public UiTextHistory(string initialValue)
    {
        _items = ArrayPool<string>.Shared.Rent(DefaultCapacity);
        _items[0] = initialValue;
        _count = 1;
        _index = 0;
    }

    public bool Push(string value)
    {
        if (_count > 0 && string.Equals(_items[_index], value, StringComparison.Ordinal))
        {
            return false;
        }

        if (_index < _count - 1)
        {
            Array.Clear(_items, _index + 1, _count - _index - 1);
            _count = _index + 1;
        }

        EnsureCapacity(_count + 1);
        _items[_count] = value;
        _count++;
        _index = _count - 1;
        return true;
    }

    public bool Undo(out string value)
    {
        if (_index <= 0)
        {
            value = _items[_index];
            return false;
        }

        _index--;
        value = _items[_index];
        return true;
    }

    public bool Redo(out string value)
    {
        if (_index >= _count - 1)
        {
            value = _items[_index];
            return false;
        }

        _index++;
        value = _items[_index];
        return true;
    }

    public void ReleasePooled()
    {
        if (_items.Length == 0)
        {
            return;
        }

        Array.Clear(_items, 0, _count);
        ArrayPool<string>.Shared.Return(_items, clearArray: false);
        _items = [];
        _count = 0;
        _index = 0;
    }

    private void EnsureCapacity(int needed)
    {
        if (_items.Length >= needed)
        {
            return;
        }

        var newSize = Math.Max(needed, _items.Length * 2);
        var next = ArrayPool<string>.Shared.Rent(newSize);
        Array.Copy(_items, next, _count);
        ArrayPool<string>.Shared.Return(_items, clearArray: false);
        _items = next;
    }
}

public sealed class UiTextEditBuffer
{
    private readonly StringBuilder _builder;
    private string _cached;
    private bool _dirty;

    public UiTextEditBuffer(string initialValue)
    {
        initialValue ??= string.Empty;
        _builder = new StringBuilder(initialValue);
        _cached = initialValue;
        _dirty = false;
    }

    public int Length => _builder.Length;

    public string Text
    {
        get
        {
            if (!_dirty)
            {
                return _cached;
            }

            _cached = _builder.ToString();
            _dirty = false;
            return _cached;
        }
    }

    public void Sync(string value)
    {
        value ??= string.Empty;
        if (string.Equals(_cached, value, StringComparison.Ordinal))
        {
            return;
        }

        _builder.Clear();
        _builder.Append(value);
        _cached = value;
        _dirty = false;
    }

    public void Replace(int start, int length, string insert)
    {
        _builder.Remove(start, length);
        if (!string.IsNullOrEmpty(insert))
        {
            _builder.Insert(start, insert);
        }
        _dirty = true;
    }
}

public readonly record struct UiTableSortState(int Column, bool Ascending);

