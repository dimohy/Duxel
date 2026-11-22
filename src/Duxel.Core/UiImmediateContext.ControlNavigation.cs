namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal interface IControlKeys
    {
        static abstract bool Enter
        {
            get;
        }
        static virtual bool Space => true;
    }
    internal readonly struct RangeControlKeys : IControlKeys
    {
        public static bool Enter => false; public static bool Space => false;
    }
    internal readonly struct ActionControlKeys : IControlKeys
    {
        public static bool Enter => true;
    }
    internal readonly struct LinkControlKeys : IControlKeys
    {
        public static bool Enter => true; public static bool Space => false;
    }
    internal readonly struct SpaceControlKeys : IControlKeys
    {
        public static bool Enter => false;
    }
    private readonly record struct ControlTabStop(string Id, int PopupTier, bool ConsumesArrows = false);
    private List<ControlTabStop> _previousTabStops = [];
    private List<ControlTabStop> _currentTabStops = [];
    private string? _ownedControlFocus;
    private string? _pointerActivatedFocus;
    private string? _ownedPointerControl;
    private bool _ownedPointerControlSeen;
    private string? _spacePressOwner;
    private string? _previousSpacePressOwner;
    private bool _spaceCanceled;
    private bool _focusedControlSeen;
    private int _focusedControlTier;
    private int _smallNavigationFrames;
    private int _blockingKeyboardTier;
    internal (int References, int Capacity) ControlNavigationStorage =>
        (_previousTabStops.Count + _currentTabStops.Count, _previousTabStops.Capacity + _currentTabStops.Capacity);

    internal void BeginControlNavigation()
    {
        _presenceRemovedFocus = null;
        _scrollRevealVersion = 0;
        _scrollRevealDepth = 0;
        _scrollRevealBounds = default;
        BeginFocusGroupNavigation();
        _ownedPointerControlSeen = false;
        _fieldFocusedControlId = null;
        _currentTabStops.Clear();
        _focusedControlSeen = false;
        _previousSpacePressOwner = _spacePressOwner;
        _blockingKeyboardTier = 0;
        for (var i = 0; i < _state.PreviousPopupBlockingRects.Count; i++)
        {
            if (_state.PreviousPopupBlocksKeyboard(i))
                _blockingKeyboardTier = Math.Max(_blockingKeyboardTier, _state.PreviousPopupBlockingRects[i].Tier);
        }
        if (!_state.IsKeyDown(UiKey.Space))
            _spaceCanceled = false;
        if (_spacePressOwner != _state.FocusedId)
            CancelControlSpace();
        if (_leftMousePressed || _rightMousePressed || _middleMousePressed)
            _state.NavCursorVisible = false;
        if (_leftMousePressed)
        {
            _pointerActivatedFocus = null;
            if (_state.FocusedId == _ownedControlFocus)
                _state.ClearControlFocusForPointer();
            _ownedControlFocus = null;
            CancelControlSpace();
        }
        var sourceKeys = _keyEvents;
        List<UiKeyEvent>? remainingKeys = null;
        for (var keyIndex = 0; keyIndex < sourceKeys.Count; keyIndex++)
        {
            var input = sourceKeys[keyIndex];
            if (TryNavigateFocusGroup(input))
            {
                if (remainingKeys is null)
                {
                    remainingKeys = _focusGroupFilteredKeys ??= [];
                    for (var prior = 0; prior < keyIndex; prior++)
                        remainingKeys.Add(sourceKeys[prior]);
                }
                continue;
            }
            remainingKeys?.Add(input);
            if (!input.IsDown || input.Key != UiKey.Tab ||
                (input.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
                continue;
            if (_previousTabStops.Count == 0)
                continue;
            var previous = -1;
            for (var i = 0; i < _previousTabStops.Count; i++)
                if (_previousTabStops[i].Id == _state.FocusedId)
                {
                    previous = i;
                    break;
                }
            var reverse = (input.Modifiers & KeyModifiers.Shift) != 0;
            var next = NextControlTabStop(previous, reverse);
            _state.FocusedId = _previousTabStops[next].Id;
            if (_state.ActiveId is { } textId && _state.HasTextInputStorage(textId))
                _imeHandler?.SetCompositionOwner(null);
            _state.ActiveId = null;
            _state.PendingTextInputFocusId = null;
            _ownedControlFocus = _state.FocusedId;
            _state.NavCursorVisible = true;
            CancelControlSpace();
            _requestFrame?.Invoke();
        }
        if (remainingKeys is not null)
            _keyEvents = remainingKeys;
    }

    internal void EndControlNavigation()
    {
        EndMenuFrame();
        EndOverlayFrame();
        if (_ownedPointerControl is not null)
        {
            if (_state.ActiveId == _ownedPointerControl && !_ownedPointerControlSeen)
            {
                _state.ActiveId = null;
                _requestFrame?.Invoke();
            }
            if (_state.ActiveId != _ownedPointerControl)
                _ownedPointerControl = null;
        }
        if (!_leftMouseDown)
            _editedSliderId = null;
        var tier = _state.CurrentPopupKeyboardTier;
        var count = 0;
        PrepareFocusGroupStopMap(tier);
        for (var i = 0; i < _currentTabStops.Count; i++)
            if (_currentTabStops[i].PopupTier >= tier)
                _currentTabStops[count++] = _currentTabStops[i];
        if (count < _currentTabStops.Count)
            _currentTabStops.RemoveRange(count, _currentTabStops.Count - count);
        EndFocusGroupNavigation(tier);
        CompletePresenceFocusRetirement();
        if (_ownedControlFocus is not null && _ownedControlFocus == _state.FocusedId && (!_focusedControlSeen || _focusedControlTier < tier))
        {
            if (_state.ActiveId == _ownedControlFocus)
            {
                if (_state.HasTextInputStorage(_ownedControlFocus))
                    _imeHandler?.SetCompositionOwner(null);
                _state.ActiveId = null;
            }
            if (_editedSliderId == _ownedControlFocus)
                _editedSliderId = null;
            _state.FocusedId = null;
            _ownedControlFocus = null;
            CancelControlSpace();
            _requestFrame?.Invoke();
        }
        if (_pointerActivatedFocus != _state.FocusedId || _state.NavCursorVisible)
            _pointerActivatedFocus = null;
        _state.CompleteControlPointerFocus();
        (_previousTabStops, _currentTabStops) = (_currentTabStops, _previousTabStops);
        _currentTabStops.Clear();
        if (Math.Max(_currentTabStops.Capacity, _previousTabStops.Capacity) > Math.Max(64, count * 4))
        {
            if (++_smallNavigationFrames >= 120)
            {
                _currentTabStops.Capacity = 0;
                _previousTabStops.Capacity = Math.Max(count, 64);
                _smallNavigationFrames = 0;
            }
        }
        else
            _smallNavigationFrames = 0;
    }

    internal void CancelControlPointerFocus()
    {
        if (_state.CancelControlPointerFocus())
            _ownedControlFocus = _state.FocusedId;
    }

    internal void ReleaseControlNavigation()
    {
        _state.CancelControlPointerFocus();
        ReleaseTextFocusOwners();
        _state.ReleaseScrollbarMotions();
        _tooltipScrollbarMotions = null;
        ReleaseOverlays();
        ReleaseControlFocusGroups();
        if (_activeMenu is { } menu)
        {
            menu.Open = false;
            menu.ReturnStops = [];
            _activeMenu = null;
        }
        _editedSliderId = null;
        _previousTabStops.Clear();
        _previousTabStops.Capacity = 0;
        _currentTabStops.Clear();
        _currentTabStops.Capacity = 0;
        _ownedControlFocus = null;
        _pointerActivatedFocus = null;
        _spacePressOwner = null;
        _ownedPointerControl = null;
        _ownedPointerControlSeen = false;
        _previousSpacePressOwner = null;
        _spaceCanceled = false;
    }

    private void CancelControlSpace()
    {
        if (_spacePressOwner is not null)
            _spaceCanceled = true;
        _spacePressOwner = null;
    }

    private void ObserveControlFocus(string id)
    {
        _focusedControlSeen = true;
        _focusedControlTier = _popupTierDepth;
        _ownedControlFocus = id;
    }

    private UiButtonInteraction ProcessControlKeyboard<TKeys>(UiRect bounds, UiButtonInteraction pointer, bool allowDisabledFocus = false)
        where TKeys : IControlKeys
        => ProcessControlKeyboard<TKeys>(_lastItemId!, bounds, pointer, allowDisabledFocus);

    private UiButtonInteraction ProcessControlKeyboard<TKeys>(string id, UiRect bounds, UiButtonInteraction pointer, bool allowDisabledFocus = false)
        where TKeys : IControlKeys
    {
        ApplyAccessibilityFocus(id, bounds, allowDisabledFocus);
        ClaimFieldFocus(id, bounds);
        var clipped = IntersectRect(CurrentClipRect, bounds);
        if ((!IsControlEnabled && !allowDisabledFocus) || clipped.Width <= 0 || clipped.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            if (_spacePressOwner == id)
                CancelControlSpace();
            if (!IsControlEnabled && _state.ActiveId == id)
            {
                if (_state.HasTextInputStorage(id))
                    _imeHandler?.SetCompositionOwner(null);
                _state.ActiveId = null;
            }
            if (_ownedControlFocus == id && _state.FocusedId == id)
            {
                _state.FocusedId = null;
                _ownedControlFocus = null;
                _requestFrame?.Invoke();
            }
            return pointer;
        }
        _currentTabStops.Add(new(id, _popupTierDepth, !TKeys.Enter && !TKeys.Space));
        ClaimFocusGroupPointer(bounds);
        if (pointer.Hovered && _leftMousePressed)
        {
            _state.FocusedId = id;
            _ownedControlFocus = id;
            CancelControlSpace();
        }
        var focused = _state.FocusedId == id;
        if (focused)
        {
            ObserveControlFocus(id);
        }
        if (!focused)
            return pointer;
        var activated = pointer.Activated;
        foreach (var input in _keyEvents)
        {
            if (!IsControlEnabled)
            {
                CancelControlSpace();
                continue;
            }
            if (TKeys.Enter && input.Key == UiKey.Enter && input.IsDown)
                activated = true;
            else if (TKeys.Space && input.Key == UiKey.Space)
            {
                if (input.IsDown)
                {
                    if (!_spaceCanceled)
                        _spacePressOwner = id;
                }
                else
                {
                    activated |= _spacePressOwner == id;
                    _spacePressOwner = null;
                }
            }
            else if (input.Key == UiKey.Escape && input.IsDown)
                CancelControlSpace();
        }
        if (activated)
            _pointerActivatedFocus = pointer.Activated ? id : null;
        if (activated && !pointer.Activated)
        {
            _lastItemEdited = true;
            _requestFrame?.Invoke();
        }
        return new(activated, pointer.Hovered, pointer.Held || _spacePressOwner == id)
        {
            Focused = true,
            FocusVisible = _state.NavCursorVisible
        };
    }
}
