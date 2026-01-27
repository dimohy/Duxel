namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public void SetNextItemAllowOverlap()
    {
        _hasNextItemAllowOverlap = true;
    }

    public void SetItemAllowOverlap()
    {
        _lastItemFlags |= UiItemFlags.AllowOverlap;
    }

    public bool IsItemHovered()
    {
        return _lastItemId is not null && string.Equals(_state.HoveredId, _lastItemId, StringComparison.Ordinal);
    }

    public bool IsItemActive()
    {
        return _lastItemId is not null && string.Equals(_state.ActiveId, _lastItemId, StringComparison.Ordinal);
    }

    public bool IsItemFocused()
    {
        return _lastItemId is not null && string.Equals(_state.FocusedId, _lastItemId, StringComparison.Ordinal);
    }

    public bool IsItemClicked(int button = (int)UiMouseButton.Left)
    {
        return button == (int)UiMouseButton.Left && IsItemHovered() && _leftMousePressed;
    }

    public bool IsItemVisible()
    {
        var rect = new UiRect(_lastItemPos.X, _lastItemPos.Y, _lastItemSize.X, _lastItemSize.Y);
        var clipped = IntersectRect(CurrentClipRect, rect);
        return clipped.Width > 0f && clipped.Height > 0f;
    }

    public bool IsItemEdited() => _lastItemEdited;

    public bool IsItemActivated()
    {
        return _lastItemId is not null &&
               string.Equals(_state.ActiveId, _lastItemId, StringComparison.Ordinal) &&
               !string.Equals(_state.PreviousActiveId, _lastItemId, StringComparison.Ordinal);
    }

    public bool IsItemDeactivated()
    {
        return _lastItemId is not null &&
               string.Equals(_state.PreviousActiveId, _lastItemId, StringComparison.Ordinal) &&
               !string.Equals(_state.ActiveId, _lastItemId, StringComparison.Ordinal);
    }

    public bool IsItemDeactivatedAfterEdit()
    {
        return IsItemDeactivated() && _lastItemEdited;
    }

    public bool IsItemToggledOpen() => _lastItemToggledOpen;

    public bool IsItemToggledSelection() => _lastItemToggledSelection;

    public bool IsAnyItemHovered() => _state.HoveredId is not null;

    public bool IsAnyItemActive() => _state.ActiveId is not null;

    public bool IsAnyItemFocused() => _state.FocusedId is not null;

    public string? GetItemID() => _lastItemId;

    public UiVector2 GetItemRectMin() => _lastItemPos;

    public UiVector2 GetItemRectMax() => new(_lastItemPos.X + _lastItemSize.X, _lastItemPos.Y + _lastItemSize.Y);

    public UiVector2 GetItemRectSize() => _lastItemSize;

    public UiItemFlags GetItemFlags() => _lastItemFlags;

    public void SetItemDefaultFocus()
    {
        if (_state.FocusedId is null && _lastItemId is not null)
        {
            _state.FocusedId = _lastItemId;
        }
    }

    public void SetKeyboardFocusHere()
    {
        if (_lastItemId is not null)
        {
            _state.FocusedId = _lastItemId;
        }
    }

    public void SetNavCursorVisible(bool visible)
    {
        _state.NavCursorVisible = visible;
    }

    public bool BeginMultiSelect(UiMultiSelectFlags flags = UiMultiSelectFlags.None, int selectionSize = 0, int itemsCount = 0)
    {
        _multiSelectActive = true;
        _multiSelectFlags = flags;
        _multiSelectSelectionSize = selectionSize;
        _multiSelectItemsCount = itemsCount;
        return _multiSelectActive;
    }

    public void EndMultiSelect()
    {
        _multiSelectActive = false;
        _multiSelectFlags = UiMultiSelectFlags.None;
        _multiSelectSelectionSize = 0;
        _multiSelectItemsCount = 0;
    }

    public void SetNextItemSelectionUserData(long userData)
    {
        _nextItemSelectionUserData = userData;
        _hasNextItemSelectionUserData = true;
    }

    public bool TryGetItemSelectionUserData(out long userData)
    {
        if (_hasLastItemSelectionUserData)
        {
            userData = _lastItemSelectionUserData;
            return true;
        }

        userData = 0;
        return false;
    }
}
