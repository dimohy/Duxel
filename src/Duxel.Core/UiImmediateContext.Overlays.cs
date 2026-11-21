namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private List<UiOverlayInputState>? _overlays;
    private UiOverlayInputState? _drawingOverlay;

    private void ObserveOverlayFocusReturnTarget(string id, UiRect bounds, bool allowDisabledFocus)
    {
        if (_overlays is null || _overlays.Count == 0 || !IsControlEnabled && !allowDisabledFocus)
            return;
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        foreach (var owner in _overlays)
            if (owner.Trigger == id && owner.TriggerTier == _popupTierDepth)
                owner.TriggerSeenFrame = GetFrameCount();
    }

    private bool OverlayContains(UiOverlayInputState owner, string? id)
    {
        if (id is null)
            return false;
        if (owner.Surface == id || owner.Members.Contains(id))
            return true;
        if (_overlays is null)
            return false;
        foreach (var child in _overlays)
        {
            if (child.Surface != id && !child.Members.Contains(id))
                continue;
            for (var parent = child.Parent; parent is not null; parent = parent.Parent)
                if (ReferenceEquals(parent, owner))
                    return true;
        }
        return false;
    }
    private bool OverlayPointerInside(UiOverlayInputState owner, UiRect? trigger = null)
    {
        var screenPoint = WidgetScreenPoint(_mousePosition);
        if (trigger is { } triggerBounds && Contains(triggerBounds, _mousePosition) && !IsMouseOverHigherTierPopup(owner.TriggerTier)
            || owner.ContainsScreenPoint(screenPoint) && !IsMouseOverHigherTierPopup(owner.Tier))
            return true;
        if (_activeMenu is { Open: true } menu && OverlayContains(owner, menu.TriggerId)
            && menu.BlockingFrame >= GetFrameCount() - 1 && Contains(menu.BlockingRect, screenPoint)
            && !IsMouseOverHigherTierPopup(menu.BlockingTier))
            return true;
        if (_overlays is null)
            return false;
        foreach (var child in _overlays)
        {
            if (!child.Open || !child.ContainsScreenPoint(screenPoint) || IsMouseOverHigherTierPopup(child.Tier))
                continue;
            for (var parent = child.Parent; parent is not null; parent = parent.Parent)
                if (ReferenceEquals(parent, owner))
                    return true;
        }
        return false;
    }
    internal bool RequestOverlayChange(UiOverlayInputState input, bool open, bool restoreFocus)
    {
        input.CancelPendingDismissal();
        if (input.Open == open)
            return true;
        if (input.CanChangeOpen is { } request && !request(open))
            return false;
        if (!open)
        {
            CancelOverlay(input, restoreFocus, false, true);
            return true;
        }
        input.PrepareOpening();
        CancelOverlayExit(input);
        input.Open = true;
        (_overlays ??= []).Add(input);
        input.Changed?.Invoke(true);
        _requestFrame?.Invoke();
        return true;
    }
    internal void CancelOverlay(UiOverlayInputState input, bool restoreFocus, bool releaseResources, bool notify)
    {
        if (_overlays is { } owners)
        {
            while (true)
            {
                UiOverlayInputState? child = null;
                foreach (var candidate in owners)
                {
                    if (ReferenceEquals(candidate.Parent, input))
                    {
                        child = candidate;
                        break;
                    }
                }
                if (child is null)
                    break;
                CancelOverlay(child, false, true, notify);
            }
        }
        var hadFocus = OverlayContains(input, _state.FocusedId);
        if (_activeMenu is { } menu && OverlayContains(input, menu.TriggerId))
            CancelMenuPopup(menu);
        if (input.RegisteredFrame == GetFrameCount())
        {
            _state.RemoveCurrentPopupBlockingRect(input.Bounds, input.Tier);
            if (input.AdditionalBlockingBounds is { } arrowBounds)
                _state.RemoveCurrentPopupBlockingRect(arrowBounds, input.Tier);
            input.RegisteredFrame = int.MinValue;
        }
        if (input.Surface is { } surface)
            _state.ReleasePreviousPopupKeyboard(surface);
        _blockingKeyboardTier = Math.Max(_state.PreviousPopupKeyboardTier, _state.CurrentPopupKeyboardTier);
        for (var i = _currentTabStops.Count - 1; i >= 0; i--)
            if (input.Members.Contains(_currentTabStops[i].Id) || _currentTabStops[i].Id == input.Surface)
                _currentTabStops.RemoveAt(i);
        var wasOpen = input.Open;
        if (releaseResources)
            CancelOverlayExit(input);
        else if (wasOpen)
            input.PrepareClosing();
        input.Open = false;
        input.ResetMotion();
        input.ResetInteraction(releaseResources);
        input.HadFocusWithin = false;
        input.HadFocusInRegion = false;
        _overlays?.Remove(input);
        if (restoreFocus && input.Trigger is { } trigger)
        {
            FocusGroupControl(trigger, keyboard : true, preserveFocusIndicator : true);
            _focusedControlSeen = input.TriggerSeenFrame == GetFrameCount();
            _focusedControlTier = input.TriggerTier;
        }
        else if (hadFocus)
        {
            _state.FocusedId = _ownedControlFocus = null;
            _state.ActiveId = null;
            _imeHandler?.SetCompositionOwner(null);
            CancelControlSpace();
        }
        input.Members.Clear();
        input.Parent = null;
        if (releaseResources)
        {
            input.ResetSurface();
        }
        if (wasOpen)
        {
            _requestFrame?.Invoke();
            if (notify)
                input.Changed?.Invoke(false);
        }
    }
    private void EndOverlayFrame()
    {
        EndOverlayExitFrame();
        if (_overlays is null)
            return;
        for (var i = _overlays.Count - 1; i >= 0; i--)
        {
            if (i >= _overlays.Count)
                continue;
            var owner = _overlays[i];
            if (owner.SeenFrame != GetFrameCount())
                CancelOverlay(owner, false, true, true);
        }
        foreach (var owner in _overlays)
        {
            if (owner.InteractionDeadline is { } deadline)
                RequestFrameAt(deadline);
            if (owner.MotionNextAt is { } motionDeadline)
                RequestFrameAt(motionDeadline);
        }
    }
    private void ReleaseOverlays()
    {
        ReleaseOverlayExits();
        if (_overlays is null)
            return;
        while (_overlays.Count > 0)
        {
            var owner = _overlays[^1];
            CancelOverlay(owner, false, true, false);
            owner.Detach();
        }
        _overlays = null;
        _drawingOverlay = null;
    }
    private bool OverlayOwnsEscape(UiOverlayInputState input, bool includeTrigger = true)
    {
        var focus = _state.FocusedId;
        if ((!includeTrigger || focus != input.Trigger) && focus != input.Surface && !input.Members.Contains(focus!))
            return false;
        if (_activeMenu is { Open: true } menu && OverlayContains(input, menu.TriggerId))
            return false;
        if (_overlays is not null)
        {
            foreach (var other in _overlays)
            {
                if (!ReferenceEquals(other, input) && other.Tier > input.Tier
                    && (focus == other.Trigger || focus == other.Surface || other.Members.Contains(focus!)))
                    return false;
            }
        }
        return true;
    }
    private void CompleteOverlayFocus(UiOverlayInputState input, string surfaceId, UiRect bounds, int start, bool opened, bool autoFocus,
        bool retainFocus = false, string? previousFocus = null)
    {
        if (!input.Open)
        {
            RetireOverlayAccessibility(surfaceId);
            for (var i = _currentTabStops.Count - 1; i >= start; i--)
                _currentTabStops.RemoveAt(i);
            return;
        }
        input.Members.Clear();
        for (var i = start; i < _currentTabStops.Count; i++)
            if (_currentTabStops[i].PopupTier == input.Tier)
                input.Members.Add(_currentTabStops[i].Id);
        if (input.Members.Count == 0)
        {
            _currentTabStops.Add(new(surfaceId, input.Tier));
            ApplyAccessibilityFocus(surfaceId, bounds);
        }
        if (opened && autoFocus || retainFocus && !OverlayContains(input, _state.FocusedId))
        {
            var target = retainFocus && previousFocus is not null && input.Members.Contains(previousFocus) ? previousFocus
                : input.Members.Count > 0 ? input.Members[0] : surfaceId;
            FocusGroupControl(target, keyboard : true, preserveFocusIndicator : true);
            _focusedControlSeen = true;
            _focusedControlTier = input.Tier;
        }
        if (_state.FocusedId == surfaceId)
        {
            _focusedControlSeen = true;
            _focusedControlTier = input.Tier;
        }
        _accessibility?.SetOverlayFocusTarget(surfaceId, input.Members.Count == 0, _state.FocusedId == surfaceId);
        input.HadFocusWithin = OverlayContains(input, _state.FocusedId);
        input.HadFocusInRegion = input.HadFocusWithin || _state.FocusedId == input.Trigger;
    }
}
