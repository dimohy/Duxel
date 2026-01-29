namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool IsKeyDown(UiKey key) => _state.IsKeyDown(key);

    public bool IsKeyPressed(UiKey key, bool repeat = true)
    {
        if (HasKeyDownEventThisFrame(key))
        {
            return true;
        }

        return repeat && IsRepeatTriggered(key);
    }

    public bool IsKeyReleased(UiKey key)
    {
        foreach (var keyEvent in _keyEvents)
        {
            if (!keyEvent.IsDown && keyEvent.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsKeyChordPressed(UiKey key, KeyModifiers modifiers, bool repeat = false)
    {
        if ((_state.Modifiers & modifiers) != modifiers)
        {
            return false;
        }

        return IsKeyPressed(key, repeat);
    }

    public int GetKeyPressedAmount(UiKey key, float repeatDelay, float repeatRate)
    {
        var count = 0;
        foreach (var keyEvent in _keyEvents)
        {
            if (keyEvent.IsDown && keyEvent.Key == key)
            {
                count++;
            }
        }

        if (count > 0)
        {
            return count;
        }

        if (repeatDelay <= 0f || repeatRate <= 0f)
        {
            return 0;
        }

        if (!_state.IsKeyDown(key))
        {
            return 0;
        }

        var now = _state.TimeSeconds;
        if (!_state.TryGetRepeatTime(key, out var nextTime))
        {
            _state.SetRepeatTime(key, now + repeatDelay);
            return 0;
        }

        if (now >= nextTime)
        {
            var elapsed = now - nextTime;
            var repeats = 1 + (int)Math.Floor(elapsed / repeatRate);
            _state.SetRepeatTime(key, now + repeatRate);
            return repeats;
        }

        return 0;
    }

    public string GetKeyName(UiKey key) => key.ToString();

    public int GetKeyIndex(UiKey key) => (int)key;

    public void SetNextFrameWantCaptureKeyboard(bool wantCapture)
    {
        _state.WantCaptureKeyboardNextFrame = wantCapture;
    }

    public bool Shortcut(UiKey key, KeyModifiers modifiers = KeyModifiers.None, bool repeat = false)
    {
        return IsKeyChordPressed(key, modifiers, repeat);
    }

    public void SetNextItemShortcut(UiKey key, KeyModifiers modifiers = KeyModifiers.None, bool repeat = false)
    {
        _nextItemShortcutKey = key;
        _nextItemShortcutModifiers = modifiers;
        _nextItemShortcutRepeat = repeat;
        _hasNextItemShortcut = true;
    }

    public void SetItemKeyOwner(UiKey key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _ = key;
        _ = modifiers;

        if (_lastItemId is null)
        {
            return;
        }

        _state.ActiveId = _lastItemId;
    }

    public bool IsMouseDown(int button)
    {
        return button == (int)UiMouseButton.Left && _leftMouseDown;
    }

    public bool IsMouseClicked(int button, bool repeat = false)
    {
        _ = repeat;
        return button == (int)UiMouseButton.Left && _leftMousePressed;
    }

    public bool IsMouseReleased(int button)
    {
        return button == (int)UiMouseButton.Left && _leftMouseReleased;
    }

    public bool IsMouseDoubleClicked(int button)
    {
        return button == (int)UiMouseButton.Left && GetMouseClickedCount(button) >= 2;
    }

    public bool IsMouseReleasedWithDelay(int button, float delaySeconds)
    {
        if (button != (int)UiMouseButton.Left || !_leftMouseReleased)
        {
            return false;
        }

        var heldDuration = _state.TimeSeconds - _state.LastClickTime;
        return heldDuration >= Math.Max(0f, delaySeconds);
    }

    public int GetMouseClickedCount(int button)
    {
        if (button != (int)UiMouseButton.Left)
        {
            return 0;
        }

        return _leftMousePressed ? _state.LastClickCount : 0;
    }

    public bool IsMouseHoveringRect(UiVector2 min, UiVector2 max, bool clip = true)
    {
        var rect = new UiRect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
        if (clip)
        {
            rect = IntersectRect(CurrentClipRect, rect);
        }

        return IsHovering(rect);
    }

    public bool IsMousePosValid()
    {
        return _mousePosition.X >= 0f && _mousePosition.Y >= 0f &&
               _mousePosition.X <= _displaySize.X && _mousePosition.Y <= _displaySize.Y;
    }

    public bool IsAnyMouseDown() => _leftMouseDown;

    public UiVector2 GetMousePos() => _mousePosition;

    public UiVector2 GetMousePosOnOpeningCurrentPopup()
    {
        if (_popupStack.Count == 0)
        {
            return _mousePosition;
        }

        var id = _popupStack.Peek().Id;
        return _state.GetPopupOpenMousePos($"{id}##popup", _mousePosition);
    }

    public bool IsMouseDragging(int button, float lockThreshold = 3f)
    {
        if (button != (int)UiMouseButton.Left || !_leftMouseDown)
        {
            return false;
        }

        var delta = GetMouseDragDelta(button, lockThreshold);
        return MathF.Abs(delta.X) > 0f || MathF.Abs(delta.Y) > 0f;
    }

    public UiVector2 GetMouseDragDelta(int button, float lockThreshold = 3f)
    {
        if (button != (int)UiMouseButton.Left || !_leftMouseDown)
        {
            return default;
        }

        var delta = new UiVector2(_mousePosition.X - _state.LastClickPos.X, _mousePosition.Y - _state.LastClickPos.Y);
        if (MathF.Abs(delta.X) < lockThreshold && MathF.Abs(delta.Y) < lockThreshold)
        {
            return default;
        }

        return delta;
    }

    public void ResetMouseDragDelta(int button)
    {
        if (button != (int)UiMouseButton.Left)
        {
            return;
        }

        _state.SetLastClick(_state.TimeSeconds, _mousePosition, _state.LastClickCount);
    }

    public UiMouseCursor GetMouseCursor() => _state.MouseCursor;

    public void SetMouseCursor(UiMouseCursor cursor)
    {
        _state.MouseCursor = cursor;
    }

    public void SetNextFrameWantCaptureMouse(bool wantCapture)
    {
        _state.WantCaptureMouseNextFrame = wantCapture;
    }

    public string GetClipboardText()
    {
        if (_clipboard is null)
        {
            throw new InvalidOperationException("Clipboard is not configured.");
        }

        return _clipboard.GetText();
    }

    public void SetClipboardText(string text)
    {
        if (_clipboard is null)
        {
            throw new InvalidOperationException("Clipboard is not configured.");
        }

        _clipboard.SetText(text ?? string.Empty);
    }
}

