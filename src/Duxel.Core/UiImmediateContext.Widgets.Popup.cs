namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public void OpenPopupOnItemClick(string id)
    {
        id ??= "Popup";
        if (!_leftMousePressed)
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
        if (_leftMousePressed && _hasWindowRect && IsHovering(_windowRect))
        {
            OpenPopup(id);
        }
        return BeginPopup(id);
    }

    public bool BeginPopupContextVoid(string id)
    {
        id ??= "ContextVoid";
        if (_leftMousePressed && (!_hasWindowRect || !IsHovering(_windowRect)))
        {
            OpenPopup(id);
        }
        return BeginPopup(id);
    }

    public void OpenPopup(string id)
    {
        id ??= "Popup";
        var resolvedId = ResolveId(id);
        var key = $"{resolvedId}##popup";
        _state.SetBool(key, true);
        _state.SetPopupOpenMousePos(key, _mousePosition);
    }

    public bool IsPopupOpen(string id)
    {
        id ??= "Popup";
        var resolvedId = ResolveId(id);
        return _state.GetBool($"{resolvedId}##popup", false);
    }

    public bool BeginPopup(string id)
    {
        id ??= "Popup";
        var resolvedId = ResolveId(id);
        var key = $"{resolvedId}##popup";
        if (!_state.GetBool(key, false))
        {
            return false;
        }

        var frameHeight = GetFrameHeight();
        var popupWidth = MathF.Max(180f, InputWidth);
        var popupHeight = frameHeight * 6f;
        var popupRect = ClampRectToDisplay(new UiRect(_mousePosition.X + 8f, _mousePosition.Y + 8f, popupWidth, popupHeight));

        if (_leftMousePressed && !IsHovering(popupRect))
        {
            _state.SetBool(key, false);
            return false;
        }

        PushOverlay();
        PushClipRect(IntersectRect(_clipRect, popupRect), false);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
        var start = new UiVector2(popupRect.X + 6f, popupRect.Y + 4f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _popupStack.Push(new UiPopupState(resolvedId, popupRect));
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
        PopOverlay();
        _popupStack.Pop();
    }

    public bool BeginPopupModal(string id, ref bool open)
    {
        id ??= "Modal";
        var resolvedId = ResolveId(id);
        var key = $"{resolvedId}##popup";
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

        PushOverlay();
        PushClipRect(IntersectRect(_clipRect, popupRect), false);
        AddRectFilled(popupRect, _theme.PopupBg, _whiteTexture);
        var start = new UiVector2(popupRect.X + 6f, popupRect.Y + 4f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _popupStack.Push(new UiPopupState(resolvedId, popupRect));
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
        PopOverlay();
        _popupStack.Pop();

        if (!open)
        {
            _state.SetBool($"{id}##popup", false);
        }
    }

    public void CloseCurrentPopup()
    {
        if (_popupStack.Count == 0)
        {
            return;
        }

        var id = _popupStack.Peek().Id;
        _state.SetBool($"{id}##popup", false);
    }
}

