using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Duxel.Core;

public sealed partial class UiState
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
    private readonly Dictionary<string, bool> _windowTopMost = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _windowExpandedSizes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _popupOpenMousePos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _menuSizes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiFloatAnimationTrack> _floatAnimations = new(StringComparer.Ordinal);
    private readonly List<string> _windowOrder = new();
    private readonly List<string> _debugLogEntries = new();
    private string? _activeWindowId;
    private readonly HashSet<UiKey> _keysDown = new();
    private readonly Dictionary<UiKey, double> _repeatNext = new();
    private string? _activeId;
    private string? _hoveredId;
    private string? _previousHoveredId;
    private string? _focusedId;
    private string? _pointerFocusOrigin;
    private bool _pointerFocusPending;
    private string? _previousActiveId;
    private bool _hoverLoggedThisFrame;
    private string? _openComboId;
    private double _lastClickTime;
    private UiVector2 _lastClickPos;
    private int _lastClickCount;
    private string? _widgetClickId;
    private double _widgetClickTime;
    private UiVector2 _widgetClickPos;
    private int _widgetClickCount;
    private UiMouseCursor _mouseCursor;
    private bool _wantCaptureKeyboardNextFrame;
    private bool _wantCaptureMouseNextFrame;
    private bool _navCursorVisible;
    private double _timeSeconds;
    private double _caretBlinkStartSeconds;
    private KeyModifiers _modifiers;
    private UiTheme? _requestedTheme;
    private int _frameCount;
    private bool _vSync = true;
    private bool _vSyncDirty;
    private int _msaaSamples = 4;
    private bool _rendererSettingsDirty;
    private bool _hasActiveAnimations;
    private List<(UiRect Rect, int Tier)> _popupBlockingRects = [];
    private List<(UiRect Rect, int Tier)> _prevPopupBlockingRects = [];
    private readonly record struct PopupInputRegistration(bool BlocksKeyboard, string? Owner);
    private List<PopupInputRegistration> _popupKeyboardBlocking = [], _prevPopupKeyboardBlocking = [];
    private string? _pendingTextInputFocusId;
    private string? _textDragId;
    private int _textDragFrame;
    private double _textDragTime;

    private readonly record struct UiFloatAnimationTrack(
        float From,
        float To,
        double StartTimeSeconds,
        double DurationSeconds
    );

    // Frame timing (set by the app loop, read by UI)
    public float NewFrameTimeMs
    {
        get; set;
    }
    public float RenderTimeMs
    {
        get; set;
    }
    public float SubmitTimeMs
    {
        get; set;
    }
    private readonly UiPresentationHistory _presentationHistory = new();
    public UiRendererFrameMetrics RendererFrameMetrics
    {
        get => _presentationHistory.Latest;
        set => _presentationHistory.Publish(in value);
    }
    internal int DrainPresentedFrames(Span<UiRendererFrameMetrics> destination) => _presentationHistory.Drain(destination);
    internal ulong OverwrittenPresentationSamples => _presentationHistory.OverwrittenSamples;
    internal IUiPresentationReader PresentationReader => _presentationHistory;

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

    public Action<string>? DebugLog
    {
        get; set;
    }

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
            if (_textDragId != value)
                _textDragId = null;
        }
    }

    public string? PreviousActiveId => _previousActiveId;

    public string? PendingTextInputFocusId
    {
        get => _pendingTextInputFocusId;
        set => _pendingTextInputFocusId = value;
    }

    public string? FocusedId
    {
        get => _focusedId;
        set
        {
            if (_focusedId == value)
                return;
            var previous = _focusedId;
            _focusedId = value;
            if (_pointerFocusPending)
            {
                if (value is null)
                    return;
                previous = _pointerFocusOrigin;
                _pointerFocusOrigin = null;
                _pointerFocusPending = false;
            }
            if (previous != value)
                ControlFocusChanged?.Invoke(previous, value);
        }
    }
    internal event Action<string?, string?>? ControlFocusChanged;

    // Pointer dispatch clears provisional focus before discovering its actual target.
    // Observers see one logical transition before a destination command can run.
    internal void ClearControlFocusForPointer()
    {
        _pointerFocusOrigin = _focusedId;
        _pointerFocusPending = _focusedId is not null;
        _focusedId = null;
    }
    internal bool IsControlFocusLeaving(string id) => _focusedId != id
        && !(_pointerFocusPending && _pointerFocusOrigin == id && _focusedId is null);
    internal void CompleteControlPointerFocus()
    {
        if (!_pointerFocusPending)
            return;
        var previous = _pointerFocusOrigin;
        _pointerFocusOrigin = null;
        _pointerFocusPending = false;
        if (previous != _focusedId)
            ControlFocusChanged?.Invoke(previous, _focusedId);
    }
    internal bool CancelControlPointerFocus()
    {
        if (!_pointerFocusPending)
            return false;
        _focusedId = _pointerFocusOrigin;
        _pointerFocusOrigin = null;
        _pointerFocusPending = false;
        return true;
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
    public int WidgetClickCount => _widgetClickCount;
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

    public bool VSync
    {
        get => _vSync;
        set
        {
            if (_vSync != value)
            {
                _vSync = value;
                _vSyncDirty = true;
            }
        }
    }

    public bool ConsumeVSyncDirty()
    {
        if (!_vSyncDirty)
        {
            return false;
        }

        _vSyncDirty = false;
        return true;
    }

    public int MsaaSamples
    {
        get => _msaaSamples;
        set
        {
            var normalized = NormalizeMsaaSamples(value);
            if (_msaaSamples == normalized)
            {
                return;
            }

            _msaaSamples = normalized;
            _rendererSettingsDirty = true;
        }
    }

    public bool ConsumeRendererSettingsDirty()
    {
        if (!_rendererSettingsDirty)
        {
            return false;
        }

        _rendererSettingsDirty = false;
        return true;
    }

    private static int NormalizeMsaaSamples(int value) => value switch
    {
        <= 1 => 1,
        <= 2 => 2,
        <= 4 => 4,
        _ => 8,
    };

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

    public float AnimateFloat(string key, float target, float durationSeconds = 0.16f, UiAnimationEasing easing = UiAnimationEasing.OutCubic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var duration = Math.Clamp(durationSeconds, 0.001f, 10f);
        var now = _timeSeconds;

        if (!_floatAnimations.TryGetValue(key, out var track))
        {
            track = new UiFloatAnimationTrack(target, target, now, duration);
            _floatAnimations[key] = track;
            return target;
        }

        var current = EvaluateTrack(track, now, easing);
        if (MathF.Abs(track.To - target) > 0.0001f)
        {
            track = new UiFloatAnimationTrack(current, target, now, duration);
            _floatAnimations[key] = track;
            MarkControlAnimation();
            return current;
        }

        if ((now - track.StartTimeSeconds) >= track.DurationSeconds)
        {
            if (MathF.Abs(current - track.To) > 0.0001f)
            {
                _floatAnimations[key] = track with
                {
                    From = track.To,
                    StartTimeSeconds = now
                };
            }
            return track.To;
        }

        MarkControlAnimation();
        return current;
    }

    public bool HasActiveAnimations => _hasActiveAnimations;
    internal ulong UnboundedAnimationRequestSerial
    {
        get; private set;
    }
    internal void MarkControlAnimation(bool active = true, bool bounded = false)
    {
        _hasActiveAnimations |= active;
        if (active)
            ScheduleFrameAt(_timeSeconds + UiTransitionOptions.DefaultFrameIntervalSeconds);
        if (!bounded)
            UnboundedAnimationRequestSerial++;
    }

    private static float EvaluateTrack(UiFloatAnimationTrack track, double nowSeconds, UiAnimationEasing easing)
    {
        if (track.DurationSeconds <= 0d)
        {
            return track.To;
        }

        var t = (float)((nowSeconds - track.StartTimeSeconds) / track.DurationSeconds);
        t = Math.Clamp(t, 0f, 1f);
        var eased = Ease(t, easing);
        return track.From + ((track.To - track.From) * eased);
    }

    private static float Ease(float t, UiAnimationEasing easing)
        => UiAnimationCurve.Evaluate(t, easing);

    public void AdvanceTime(float deltaTime)
    {
        _timeSeconds += deltaTime;
        if (_inputTimeSeconds is { } inputTime)
            _inputTimeSeconds = inputTime + deltaTime;
    }

    public void BeginFrame()
    {
        PointerMoveCoalescing = null;
        SliderPointerMoveCoalescing = null;
        WheelCoalescing = null;
        DocumentWheelRegion = null;
        _frameCount++;
        _hasActiveAnimations = false;
        NextScheduledFrameTimeSeconds = null;
        _previousActiveId = _activeId;
        _previousHoveredId = _hoveredId;
        _hoverLoggedThisFrame = false;
        _hoveredId = null;
        _mouseCursor = UiMouseCursor.Arrow;

        // Swap popup blocking rects: current → previous, clear current for new frame
        (_prevPopupBlockingRects, _popupBlockingRects) = (_popupBlockingRects, _prevPopupBlockingRects);
        _popupBlockingRects.Clear();
        (_prevPopupKeyboardBlocking, _popupKeyboardBlocking) = (_popupKeyboardBlocking, _prevPopupKeyboardBlocking);
        _popupKeyboardBlocking.Clear();
    }

    public IReadOnlyList<(UiRect Rect, int Tier)> PreviousPopupBlockingRects => _prevPopupBlockingRects;
    internal IReadOnlyList<(UiRect Rect, int Tier)> CurrentPopupBlockingRects => _popupBlockingRects;

    public void AddPopupBlockingRect(UiRect rect, int tier) => AddPopupBlockingRect(rect, tier, UiPopupInputMode.Exclusive);
    internal void AddPopupBlockingRect(UiRect rect, int tier, UiPopupInputMode inputMode, string? owner = null)
    {
        _popupBlockingRects.Add((rect, tier));
        _popupKeyboardBlocking.Add(new(inputMode == UiPopupInputMode.Exclusive, owner));
    }
    internal bool PreviousPopupBlocksKeyboard(int index) => _prevPopupKeyboardBlocking[index].BlocksKeyboard;
    internal void ReleasePreviousPopupKeyboard(string owner)
    {
        for (var i = 0; i < _prevPopupKeyboardBlocking.Count; i++)
        {
            if (_prevPopupKeyboardBlocking[i].Owner == owner)
                _prevPopupKeyboardBlocking[i] = _prevPopupKeyboardBlocking[i] with
                {
                    BlocksKeyboard = false
                };
        }
    }
    internal int PreviousPopupKeyboardTier
    {
        get
        {
            var tier = 0;
            for (var i = 0; i < _prevPopupBlockingRects.Count; i++)
            {
                if (_prevPopupKeyboardBlocking[i].BlocksKeyboard)
                    tier = Math.Max(tier, _prevPopupBlockingRects[i].Tier);
            }
            return tier;
        }
    }
    internal int CurrentPopupKeyboardTier
    {
        get
        {
            var tier = 0;
            for (var i = 0; i < _popupBlockingRects.Count; i++)
            {
                if (_popupKeyboardBlocking[i].BlocksKeyboard)
                    tier = Math.Max(tier, _popupBlockingRects[i].Tier);
            }
            return tier;
        }
    }

    internal void RemoveCurrentPopupBlockingRect(UiRect rect, int tier)
    {
        var index = _popupBlockingRects.LastIndexOf((rect, tier));
        if (index < 0)
            throw new InvalidOperationException("The current popup blocking registration is missing.");
        _popupBlockingRects.RemoveAt(index);
        _popupKeyboardBlocking.RemoveAt(index);
    }

    public void EndFrame()
    {
        if (_textDragFrame != _frameCount)
            _textDragId = null;
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

    internal UiTheme? RequestedTheme => _requestedTheme;

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

    public void UpdateModifiers(KeyModifiers modifiers)
    {
        _modifiers = modifiers;
    }

    public void UpdateInput(IReadOnlyList<UiKeyEvent> keyEvents, KeyModifiers modifiers)
    {
        _modifiers = modifiers;
        if (keyEvents.Count == 0)
        {
            return;
        }

        for (var i = 0; i < keyEvents.Count; i++)
        {
            var keyEvent = keyEvents[i];
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
    public void SetRepeatTime(UiKey key, double nextTime)
    {
        if (_keyRepeatSource != UiKeyRepeatSource.Engine)
            throw new InvalidOperationException("Synthetic key repeat timers require engine ownership.");
        _repeatNext[key] = nextTime;
    }
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

        var insertIndex = GetWindowInsertIndex(GetWindowTopMost(key));
        _windowOrder.Insert(insertIndex, key);
        return insertIndex;
    }

    public void BringWindowToFront(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var index = _windowOrder.IndexOf(key);
        if (index < 0)
        {
            var insertIndex = GetWindowInsertIndex(GetWindowTopMost(key));
            _windowOrder.Insert(insertIndex, key);
            return;
        }

        _windowOrder.RemoveAt(index);
        var targetIndex = GetWindowInsertIndex(GetWindowTopMost(key));
        _windowOrder.Insert(targetIndex, key);
    }

    public void RemoveWindowFromOrder(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windowOrder.Remove(key);
        if (string.Equals(_activeWindowId, key, StringComparison.Ordinal))
        {
            _activeWindowId = null;
        }
    }

    public IReadOnlyList<string> WindowOrder => _windowOrder;

    public bool GetWindowTopMost(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_windowTopMost.TryGetValue(key, out var value))
        {
            return value;
        }

        _windowTopMost[key] = defaultValue;
        return defaultValue;
    }

    public void SetWindowTopMost(string key, bool topMost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windowTopMost[key] = topMost;

        var index = _windowOrder.IndexOf(key);
        if (index < 0)
        {
            return;
        }

        _windowOrder.RemoveAt(index);
        var targetIndex = GetWindowInsertIndex(topMost);
        _windowOrder.Insert(targetIndex, key);
    }

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
        if (_windowExpandedSizes.TryGetValue(key, out var value))
        {
            return value;
        }

        _windowExpandedSizes[key] = fallback;
        return fallback;
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
        if (!open)
        {
            RemoveWindowFromOrder(key);
        }
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
        initialValue ??= string.Empty;

        if (_editBuffers.TryGetValue(key, out var buffer))
        {
            if (!string.Equals(buffer.Text, initialValue, StringComparison.Ordinal))
            {
                buffer.Sync(initialValue);
                if (_histories.TryGetValue(key, out var history))
                    history.Reset(initialValue);
            }
            return buffer;
        }

        buffer = new UiTextEditBuffer(initialValue ?? string.Empty);
        _editBuffers[key] = buffer;
        return buffer;
    }

    public bool HasActiveTextInput()
    {
        if (!string.IsNullOrEmpty(_activeId) && _editBuffers.ContainsKey(_activeId))
        {
            return true;
        }

        return !string.IsNullOrEmpty(_previousActiveId) && _editBuffers.ContainsKey(_previousActiveId);
    }

    internal bool HasTextInputStorage(string key) => _editBuffers.ContainsKey(key) || _histories.ContainsKey(key);
    internal (int EditBuffers, int Histories) TextInputStorage => (_editBuffers.Count, _histories.Count);
    internal void UpdateTextDragPointer(bool down, bool pressed, bool released)
    {
        if (!down || pressed || released)
            _textDragId = null;
    }
    internal void BeginTextDrag(string id)
    {
        _textDragId = id;
        _textDragFrame = _frameCount;
        _textDragTime = InputTimeSeconds;
    }
    internal bool OwnsTextDrag(string id) => _textDragId == id && _activeId == id;
    internal void ReleaseTextDrag(string id)
    {
        if (_textDragId == id)
            _textDragId = null;
    }
    internal bool ContinueTextDrag(string id, out float deltaTime)
    {
        deltaTime = 0;
        if (_textDragId != id || _activeId != id)
            return false;
        deltaTime = (float)Math.Clamp(InputTimeSeconds - _textDragTime, 0, 1d / 30);
        _textDragFrame = _frameCount;
        _textDragTime = InputTimeSeconds;
        return true;
    }
    internal void ReleaseTextInput(string key)
    {
        ReleaseTextDrag(key);
        if (_histories.Remove(key, out var history))
            history.ReleasePooled();
        _editBuffers.Remove(key);
        _textBuffers.Remove(key);
        _selections.Remove(key);
        _cursors.Remove(key);
        _scrollX.Remove(key);
        _scrollY.Remove(key);
        if (_activeId == key)
            _activeId = null;
        if (FocusedId == key)
            FocusedId = null;
        if (PendingTextInputFocusId == key)
            PendingTextInputFocusId = null;
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

    public int RegisterClick(string key, UiVector2 position, double doubleClickSeconds = 0.3, float maxDistance = 4f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var dx = position.X - _widgetClickPos.X;
        var dy = position.Y - _widgetClickPos.Y;
        var withinDistance = (dx * dx) + (dy * dy) <= maxDistance * maxDistance;
        var elapsed = InputTimeSeconds - _widgetClickTime;
        var withinTime = elapsed >= 0 && elapsed <= doubleClickSeconds;
        var isChained = _widgetClickId == key && withinDistance && withinTime;

        _widgetClickId = key;
        _widgetClickTime = InputTimeSeconds;
        _widgetClickPos = position;
        _widgetClickCount = isChained ? _widgetClickCount + 1 : 1;

        return _widgetClickCount;
    }

    public void ReleasePooledBuffers()
    {
        foreach (var history in _histories.Values)
        {
            history.ReleasePooled();
        }

        _histories.Clear();
        _editBuffers.Clear();
        _textDragId = null;
    }

    private int GetWindowInsertIndex(bool topMost)
    {
        if (topMost)
        {
            return _windowOrder.Count;
        }

        for (var i = 0; i < _windowOrder.Count; i++)
        {
            if (GetWindowTopMost(_windowOrder[i]))
            {
                return i;
            }
        }

        return _windowOrder.Count;
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
        Array.Clear(_items);
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

    internal void Reset(string value)
    {
        if (_items.Length == 0 || _items.Length > DefaultCapacity * 4)
        {
            if (_items.Length > 0)
                ArrayPool<string>.Shared.Return(_items, clearArray: true);
            _items = ArrayPool<string>.Shared.Rent(DefaultCapacity);
        }
        Array.Clear(_items);
        _items[0] = value;
        _count = 1;
        _index = 0;
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

        ArrayPool<string>.Shared.Return(_items, clearArray: true);
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
        Array.Clear(next, _count, next.Length - _count);
        ArrayPool<string>.Shared.Return(_items, clearArray: true);
        _items = next;
    }
}

public sealed class UiTextEditBuffer
{
    private UiSingleLineTextLayout? _singleLine;
    internal UiSingleLineTextLayout SingleLine => _singleLine ??= new();
    private readonly StringBuilder _builder;
    private string _cached;
    private bool _dirty;
    private int[]? _elementStarts;
    private byte _elementKind;

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
        if (!_dirty && string.Equals(_cached, value, StringComparison.Ordinal))
        {
            return;
        }

        _builder.Clear();
        _builder.Append(value);
        _cached = value;
        _dirty = false;
        InvalidateTextElements();
    }

    public void Replace(int start, int length, string insert)
    {
        _builder.Remove(start, length);
        if (!string.IsNullOrEmpty(insert))
        {
            _builder.Insert(start, insert);
        }
        _dirty = true;
        InvalidateTextElements();
    }

    private void InvalidateTextElements()
    {
        _elementStarts = null;
        _elementKind = 0;
    }
    private void EnsureTextElements()
    {
        if (_elementKind != 0)
            return;
        var text = Text;
        if (Ascii.IsValid(text) && !text.Contains('\r'))
            _elementKind = 1;
        else
        {
            _elementStarts = StringInfo.ParseCombiningCharacters(text);
            _elementKind = 2;
        }
    }
    internal int SnapTextElement(int index, bool forward = false)
    {
        index = Math.Clamp(index, 0, Length);
        if (index == 0 || index == Length)
            return index;
        EnsureTextElements();
        if (_elementKind == 1)
            return index;
        var position = Array.BinarySearch(_elementStarts!, index);
        if (position >= 0)
            return index;
        position = ~position;
        return forward ? position < _elementStarts!.Length ? _elementStarts[position] : Length
            : _elementStarts![Math.Max(0, position - 1)];
    }
    internal int PreviousTextElement(int index)
    {
        index = Math.Clamp(index, 0, Length);
        if (index == 0)
            return 0;
        EnsureTextElements();
        if (_elementKind == 1)
            return index - 1;
        var position = Array.BinarySearch(_elementStarts!, index);
        return _elementStarts![Math.Max(0, position >= 0 ? position - 1 : ~position - 1)];
    }
    internal int NextTextElement(int index)
    {
        index = Math.Clamp(index, 0, Length);
        if (index == Length)
            return Length;
        EnsureTextElements();
        if (_elementKind == 1)
            return index + 1;
        var position = Array.BinarySearch(_elementStarts!, index);
        position = position >= 0 ? position + 1 : ~position;
        return position < _elementStarts!.Length ? _elementStarts[position] : Length;
    }
    internal UiTextSelection AlignTextElements(UiTextSelection selection) => selection.Start <= selection.End
        ? new(SnapTextElement(selection.Start), SnapTextElement(selection.End, forward: true))
        : new(SnapTextElement(selection.Start, forward: true), SnapTextElement(selection.End));
}

public readonly record struct UiTableSortState(int Column, bool Ascending);

