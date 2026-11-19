namespace Duxel.Core;

/// <summary>Controls keyboard isolation independently of popup paint order and pointer occlusion.</summary>
public enum UiPopupInputMode
{
    Exclusive, NonModal
}

public sealed partial class UiImmediateContext
{
    private UiVector2? _nextPopupPosition;

    /// <summary>Places the next BeginPopup in display coordinates, independently of its parent camera.</summary>
    public void SetNextPopupPosition(UiVector2 position) => _nextPopupPosition = position;

    public void OpenPopupOnItemClick(string id)
    {
        id ??= "Popup";
        if (!_rightMousePressed)
        {
            return;
        }

        var rect = new UiRect(_lastItemPos.X, _lastItemPos.Y, _lastItemSize.X, _lastItemSize.Y);
        if (IsHovering(rect))
        {
            OpenPopup(id);
        }
    }

    public bool BeginPopupContextItem(string id)
    {
        id ??= "ContextItem";
        OpenPopupOnItemClick(id);
        return BeginPopup(id);
    }

    public bool BeginPopupContextWindow(string id)
    {
        id ??= "ContextWindow";
        if (_rightMousePressed && _hasWindowRect && IsHovering(_windowRect))
        {
            OpenPopup(id);
        }
        return BeginPopup(id);
    }

    public bool BeginPopupContextVoid(string id)
    {
        id ??= "ContextVoid";
        if (_rightMousePressed && (!_hasWindowRect || !IsHovering(_windowRect)))
        {
            OpenPopup(id);
        }
        return BeginPopup(id);
    }

    public void OpenPopup(string id)
    {
        id ??= "Popup";
        var resolvedId = ResolveId(id);
        var key = PopupStateKey(resolvedId);
        _state.SetBool(key, true);
        _state.SetPopupOpenMousePos(key, WidgetScreenPoint(_mousePosition));
    }

    public bool IsPopupOpen(string id)
    {
        id ??= "Popup";
        var resolvedId = ResolveId(id);
        return _state.GetBool(PopupStateKey(resolvedId), false);
    }

    public bool BeginPopup(string id) => BeginPopup(id, UiPopupInputMode.Exclusive);

    public bool BeginPopup(string id, UiPopupInputMode inputMode)
    {
        if ((uint)inputMode > 1)
            throw new ArgumentOutOfRangeException(nameof(inputMode));
        var nextPosition = _nextPopupPosition;
        _nextPopupPosition = null;
        id ??= "Popup";
        var resolvedId = ResolveId(id);
        var key = PopupStateKey(resolvedId);
        if (!_state.GetBool(key, false))
        {
            return false;
        }

        var frameHeight = GetFrameHeight();
        var popupWidth = MathF.Max(180f, InputWidth);
        var popupHeight = frameHeight * 6f;
        var openMousePos = _state.GetPopupOpenMousePos(key, WidgetScreenPoint(_mousePosition));
        var position = nextPosition ?? new UiVector2(openMousePos.X + 8f, openMousePos.Y + 8f);
        var popupRect = ClampRectToDisplay(new UiRect(position.X, position.Y, popupWidth, popupHeight));

        if (_leftMousePressed && !IsScreenHovering(popupRect))
        {
            _state.SetBool(key, false);
            return false;
        }

        PushPopup();
        _state.AddPopupBlockingRect(popupRect, _popupTierDepth, inputMode, resolvedId);
        PushClipRect(IntersectRect(_clipRect, popupRect), false);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
        var start = new UiVector2(popupRect.X + 6f, popupRect.Y + 4f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _popupStack.Push(new UiPopupState(resolvedId, popupRect, _popupTierDepth));
        return true;
    }

    public void EndPopup()
    {
        if (_popupStack.Count == 0)
        {
            return;
        }

        _layouts.Pop();
        PopClipRect();
        PopPopup();
        _popupStack.Pop();
    }

    public bool BeginPopupModal(string id, ref bool open)
    {
        id ??= "Modal";
        var resolvedId = ResolveId(id);
        var key = PopupStateKey(resolvedId);
        if (open)
        {
            _state.SetBool(key, true);
        }

        if (!_state.GetBool(key, false))
        {
            return false;
        }

        var frameHeight = GetFrameHeight();
        var popupWidth = MathF.Max(240f, InputWidth);
        var popupHeight = frameHeight * 8f;
        var popupRect = ClampRectToDisplay(new UiRect(
            (_displaySize.X - popupWidth) * 0.5f,
            (_displaySize.Y - popupHeight) * 0.5f,
            popupWidth,
            popupHeight
        ));

        if (_leftMousePressed && !IsHovering(popupRect))
        {
            // modal stays open until closed explicitly
        }

        PushPopup();
        _state.AddPopupBlockingRect(popupRect, _popupTierDepth, UiPopupInputMode.Exclusive, resolvedId);
        PushClipRect(IntersectRect(_clipRect, popupRect), false);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
        var start = new UiVector2(popupRect.X + 6f, popupRect.Y + 4f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _popupStack.Push(new UiPopupState(resolvedId, popupRect, _popupTierDepth));
        return true;
    }

    public void EndPopupModal(ref bool open)
    {
        if (_popupStack.Count == 0)
        {
            return;
        }

        var id = _popupStack.Peek().Id;

        _layouts.Pop();
        PopClipRect();
        PopPopup();
        _popupStack.Pop();

        if (!open)
        {
            _state.SetBool(PopupStateKey(id), false);
        }
    }

    public void CloseCurrentPopup()
    {
        if (_popupStack.Count == 0)
        {
            return;
        }

        var popup = _popupStack.Peek();
        var key = PopupStateKey(popup.Id);
        if (!_state.GetBool(key, false))
            return;
        _state.SetBool(key, false);
        _state.RemoveCurrentPopupBlockingRect(popup.Rect, popup.Tier);
        _state.ReleasePreviousPopupKeyboard(popup.Id);
        _blockingKeyboardTier = Math.Max(_state.PreviousPopupKeyboardTier, _state.CurrentPopupKeyboardTier);
        _requestFrame?.Invoke();
    }
}

