namespace Duxel.Core;

public enum UiFocusGroupMode
{
    Off, NoTab, TabExit, TabOnly
}

public sealed partial class UiImmediateContext
{
    private readonly record struct ControlFocusGroup(string Id, UiFocusGroupMode Mode, int Tier, int Start, int End, string LastId);
    private List<ControlFocusGroup> _previousFocusGroups = [], _currentFocusGroups = [];
    private Dictionary<string, int>? _focusGroupStopMap;
    private List<UiKeyEvent>? _focusGroupFilteredKeys;
    private bool _focusGroupEnterConsumed, _focusGroupEscapeConsumed;
    private int _focusGroupPointerDepth, _smallFocusGroupFrames;
    private ulong _focusGroupPointerClaims;
    internal (int References, int Capacity) ControlFocusGroupStorage =>
        ((_previousFocusGroups.Count + _currentFocusGroups.Count) * 2 + (_focusGroupStopMap?.Count ?? 0),
            _previousFocusGroups.Capacity + _currentFocusGroups.Capacity + (_focusGroupStopMap?.EnsureCapacity(0) ?? 0));

    private void BeginFocusGroupNavigation()
    {
        _currentFocusGroups.Clear();
        _focusGroupFilteredKeys?.Clear();
        _focusGroupEnterConsumed = _focusGroupEscapeConsumed = false;
        _focusGroupPointerDepth = 0;
    }

    private int PreviousFocusIndex()
    {
        for (var index = 0; index < _previousTabStops.Count; index++)
            if (_previousTabStops[index].Id == _state.FocusedId)
                return index;
        return -1;
    }

    private bool TryNavigateFocusGroup(UiKeyEvent input)
    {
        if (_previousFocusGroups.Count == 0 || !input.IsDown
            || (input.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0
            || input.Key is not (UiKey.Enter or UiKey.Escape))
            return false;
        var focused = PreviousFocusIndex();
        if (focused < 0)
            return false;
        if (input.Key == UiKey.Escape && !string.IsNullOrEmpty(_imeHandler?.GetCompositionText()))
            return false;
        for (var index = _previousFocusGroups.Count - 1; index >= 0; index--)
        {
            var group = _previousFocusGroups[index];
            if (group.Tier < _blockingKeyboardTier || _previousTabStops[focused].PopupTier != group.Tier)
                continue;
            if (input.Key == UiKey.Enter && focused == group.Start)
            {
                for (var child = group.Start + 1; child < group.End; child++)
                    if (_previousTabStops[child].PopupTier == group.Tier && GroupAllowsTabStop(child, focused, group.Id))
                    {
                        FocusGroupControl(_previousTabStops[child].Id, keyboard: true);
                        break;
                    }
                _focusGroupEnterConsumed = true;
                return true;
            }
            if (input.Key == UiKey.Escape && focused > group.Start && focused < group.End)
            {
                FocusGroupControl(group.Id, keyboard: true);
                _focusGroupEscapeConsumed = true;
                return true;
            }
        }
        return false;
    }

    private bool GroupAllowsTabStop(int candidate, int focused, string? entering = null)
    {
        foreach (var group in _previousFocusGroups)
            if (_previousTabStops[candidate].PopupTier == group.Tier && group.Id != entering && (group.Mode is UiFocusGroupMode.NoTab or UiFocusGroupMode.TabExit)
                && candidate > group.Start && candidate < group.End
                && !(focused >= 0 && _previousTabStops[focused].PopupTier == group.Tier && focused > group.Start && focused < group.End))
                return false;
        return true;
    }

    private int NextControlTabStop(int focused, bool reverse)
    {
        if (_previousFocusGroups.Count == 0)
            return focused < 0 ? reverse ? _previousTabStops.Count - 1 : 0
                : (focused + (reverse ? -1 : 1) + _previousTabStops.Count) % _previousTabStops.Count;
        var first = 0;
        var end = _previousTabStops.Count;
        var restrictedTier = -1;
        for (var index = _previousFocusGroups.Count - 1; index >= 0; index--)
        {
            var group = _previousFocusGroups[index];
            if (focused >= 0 && _previousTabStops[focused].PopupTier == group.Tier && group.Mode == UiFocusGroupMode.NoTab && focused > group.Start && focused < group.End)
            {
                first = group.Start + 1;
                end = group.End;
                restrictedTier = group.Tier;
                break;
            }
        }
        var candidate = focused < first || focused >= end ? reverse ? first : end - 1 : focused;
        for (var attempts = 0; attempts < end - first; attempts++)
        {
            candidate = first + (candidate - first + (reverse ? -1 : 1) + end - first) % (end - first);
            if (restrictedTier >= 0 && _previousTabStops[candidate].PopupTier != restrictedTier)
                continue;
            if (GroupAllowsTabStop(candidate, focused))
                return candidate;
        }
        return focused;
    }

    private void FocusGroupControl(string id, bool keyboard, bool preserveFocusIndicator = false,
        bool preservePointerCapture = false)
    {
        if (keyboard)
        {
            if (_state.ActiveId is { } active && _state.HasTextInputStorage(active))
                _imeHandler?.SetCompositionOwner(null);
            if (!preservePointerCapture)
                _state.ActiveId = null;
            _state.PendingTextInputFocusId = null;
            if (!preserveFocusIndicator)
                _state.NavCursorVisible = true;
        }
        _ownedControlFocus = id;
        _state.SetControlFocus(id, programmaticWithoutKeyboard : preserveFocusIndicator && !_state.NavCursorVisible);
        CancelControlSpace();
        _requestFrame?.Invoke();
    }

    private int BeginControlFocusGroup(string id, UiRect bounds, UiFocusGroupMode mode)
    {
        var index = _currentFocusGroups.Count;
        _currentFocusGroups.Add(new(id, mode, _popupTierDepth, _currentTabStops.Count, _currentTabStops.Count, id));
        var previous = _lastItemId;
        _lastItemId = id;
        try
        {
            ProcessControlKeyboard<RangeControlKeys>(bounds, default);
        }
        finally { _lastItemId = previous; }
        _focusGroupPointerDepth++;
        return index;
    }

    private void EndControlFocusGroup(int index, UiRect bounds, bool childClaimedPointer)
    {
        var group = _currentFocusGroups[index] with
        {
            End = _currentTabStops.Count,
            LastId = _currentTabStops[^1].Id
        };
        _currentFocusGroups[index] = group;
        if (!childClaimedPointer && !_layoutPointerBlocked && _popupTierDepth >= _blockingKeyboardTier
            && IsHovering(IntersectRect(CurrentClipRect, bounds)))
        {
            ClaimFocusGroupPointer(bounds);
            if (_leftMousePressed)
            {
                if (_state.ActiveId is { } active && _state.HasTextInputStorage(active))
                    _imeHandler?.SetCompositionOwner(null);
                _state.ActiveId = null;
                _state.PendingTextInputFocusId = null;
                FocusGroupControl(group.Id, keyboard: false);
            }
        }
        var seen = false;
        for (var stop = group.Start; stop < group.End; stop++)
            seen |= _currentTabStops[stop].Id == _state.FocusedId;
        if (!seen)
        {
            var previousFocus = PreviousFocusIndex();
            foreach (var old in _previousFocusGroups)
                if (old.Id == group.Id && previousFocus > old.Start && previousFocus < old.End && _previousTabStops[previousFocus].PopupTier == old.Tier)
                {
                    FocusGroupControl(group.Id, keyboard: true);
                    break;
                }
        }
        if (_state.FocusedId == group.Id)
        {
            _focusedControlSeen = true;
            _focusedControlTier = _popupTierDepth;
        }
    }

    private void ClaimFocusGroupPointer(UiRect bounds)
    {
        if (_focusGroupPointerDepth > 0 && !_layoutPointerBlocked && _popupTierDepth >= _blockingKeyboardTier
            && IsHovering(IntersectRect(CurrentClipRect, bounds)))
            _focusGroupPointerClaims++;
    }

    private void PrepareFocusGroupStopMap(int tier)
    {
        if (_currentFocusGroups.Count == 0)
            return;
        var map = _focusGroupStopMap ??= new(StringComparer.Ordinal);
        map.Clear();
        var count = 0;
        foreach (var stop in _currentTabStops)
            if (stop.PopupTier >= tier)
                map.TryAdd(stop.Id, count++);
    }

    private void EndFocusGroupNavigation(int tier)
    {
        var count = 0;
        for (var index = 0; index < _currentFocusGroups.Count; index++)
        {
            var group = _currentFocusGroups[index];
            if (group.Tier >= tier)
            {
                if (!_focusGroupStopMap!.TryGetValue(group.Id, out var start)
                    || !_focusGroupStopMap.TryGetValue(group.LastId, out var last) || last < start)
                    throw new InvalidOperationException("Focus group anchors are missing from the final navigation order.");
                _currentFocusGroups[count++] = group with
                {
                    Start = start,
                    End = last + 1
                };
            }
        }
        if (count < _currentFocusGroups.Count)
            _currentFocusGroups.RemoveRange(count, _currentFocusGroups.Count - count);
        (_previousFocusGroups, _currentFocusGroups) = (_currentFocusGroups, _previousFocusGroups);
        _currentFocusGroups.Clear();
        _focusGroupStopMap?.Clear();
        if (Math.Max(_currentFocusGroups.Capacity, _previousFocusGroups.Capacity) > Math.Max(16, count * 4)
            || (_focusGroupStopMap?.EnsureCapacity(0) ?? 0) > Math.Max(64, _currentTabStops.Count * 4))
        {
            if (++_smallFocusGroupFrames >= 120)
            {
                _currentFocusGroups.Capacity = 0;
                _previousFocusGroups.Capacity = Math.Max(count, 16);
                _focusGroupStopMap?.Clear();
                _focusGroupStopMap?.TrimExcess();
                _smallFocusGroupFrames = 0;
            }
        }
        else
            _smallFocusGroupFrames = 0;
    }

    private void ReleaseControlFocusGroups()
    {
        _previousFocusGroups.Clear();
        _previousFocusGroups.Capacity = 0;
        _currentFocusGroups.Clear();
        _currentFocusGroups.Capacity = 0;
        _focusGroupStopMap = null;
        _focusGroupFilteredKeys = null;
    }
}
