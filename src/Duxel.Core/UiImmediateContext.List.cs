namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal readonly record struct ListScope(string Parent, UiWidget? Widget, int TabStart, int Tier,
        IReadOnlyList<UiKeyEvent> Keys, string Root, string? FocusAtStart);

    internal ListScope BeginList(string label, UiRect bounds, UiListState list)
    {
        var input = list.Input;
        input.Bind(this);
        list.Selection?.Synchronize();
        var root = ResolveId(UiControlWidget.PrimaryControlId);
        var scope = new ListScope(_accessibilityParent, _accessibilityWidget, _currentTabStops.Count,
            _popupTierDepth, _keyEvents, root, _state.FocusedId);
        var row = input.Find(_state.FocusedId);
        input.Keys.Clear();
        if (list.Navigation != UiListNavigation.Off && row >= 0 && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
        {
            foreach (var key in _keyEvents)
            {
                var consumed = false;
                if (key.IsDown && key.Modifiers == 0)
                {
                    var child = input.Children[row].FindIndex(member => member.Id == _state.FocusedId);
                    var editing = child >= 0 && input.Children[row][child].ConsumesArrows;
                    var horizontal = UiCompositeMember.Horizontal(key.Key, list.Options.RightToLeft);
                    if (!editing && key.Key is UiKey.UpArrow or UiKey.DownArrow or UiKey.Home or UiKey.End)
                    {
                        var step = key.Key is UiKey.UpArrow or UiKey.End ? -1 : 1;
                        var target = key.Key == UiKey.Home ? -1 : key.Key == UiKey.End ? input.Rows.Length : row;
                        for (var next = target + step; next >= 0 && next < input.Rows.Length; next += step)
                            if (input.Available[next] && input.Rows[next] is { } id && input.Frames[next] == _state.FrameCount - 1)
                            {
                                row = next;
                                FocusGroupControl(id, keyboard: true);
                                break;
                            }
                        consumed = true;
                    }
                    else if (list.Navigation == UiListNavigation.Composite && !editing && horizontal != 0)
                    {
                        var next = child + horizontal;
                        if (horizontal < 0 && child < 0)
                            next = -1;
                        if (next < 0)
                            FocusGroupControl(input.Rows[row]!, keyboard: true);
                        else if (next < input.Children[row].Count)
                            FocusGroupControl(input.Children[row][next].Id, keyboard: true);
                        consumed = true;
                    }
                    else if (child >= 0 && key.Key == UiKey.Escape && !_focusGroupEscapeConsumed && !_tooltipEscapeConsumed
                        && string.IsNullOrEmpty(_imeHandler?.GetCompositionText()))
                    {
                        FocusGroupControl(input.Rows[row]!, keyboard: true);
                        consumed = true;
                    }
                }
                if (!consumed)
                    input.Keys.Add(key);
            }
            _keyEvents = input.Keys;
        }
        var role = list.Navigation == UiListNavigation.Composite ? UiAccessibilityRole.Grid
            : list.Selectable ? UiAccessibilityRole.ListBox : UiAccessibilityRole.List;
        ExposeAccessibility(root, scope.Parent, role, label, bounds, focusable: false, valuePattern: false);
        _accessibility?.SetListContainer(root, list.Selection?.Multiple);
        _accessibilityParent = root;
        _accessibilityWidget = null;
        return scope;
    }

    internal void EndList(ListScope scope, UiListState list)
    {
        try
        {
            list.Selection?.Synchronize();
            var input = list.Input;
            var formerlyFocused = input.Find(scope.FocusAtStart);
            var gridRows = 0;
            var columns = list.Selectable ? 2 : 1;
            for (var i = 0; i < input.Rows.Length; i++)
            {
                if (input.Frames[i] != _state.FrameCount)
                {
                    input.Rows[i] = null;
                    input.Available[i] = false;
                    input.Children[i].Clear();
                    continue;
                }
                _accessibility?.SetListItem(input.Rows[i]!, list.Selectable ? list.Selected(i) : null,
                    _state.FocusedId == input.Rows[i], list.Items[i].Action is not null);
                if (list.Navigation == UiListNavigation.Composite)
                {
                    _accessibility?.SetGridItem(input.Rows[i]!, new(scope.Root, gridRows, 0, ColumnSpan: columns));
                    _accessibility?.SetGridItem(input.ContentCellIds[i]!, new(scope.Root, gridRows, columns - 1));
                    if (list.Selectable)
                        _accessibility?.SetGridItem(input.CheckCellIds[i]!, new(scope.Root, gridRows, 0));
                    gridRows++;
                }
                if (input.CheckIds[i] is { } checkId)
                {
                    _accessibility?.SetListCheckmark(checkId, list.Selected(i));
                    list.PaintCheckmark(this, input.Checkmarks[i], list.Selected(i));
                }
            }
            if (list.Navigation == UiListNavigation.Composite)
                _accessibility?.SetGrid(scope.Root, new(gridRows, columns));
            if (list.Navigation == UiListNavigation.Off)
                return;
            var keep = -1;
            var remembered = -1;
            for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
            {
                if (_currentTabStops[i].PopupTier != scope.Tier)
                    continue;
                if (_currentTabStops[i].Id == _state.FocusedId)
                    keep = i;
                if (_currentTabStops[i].Id == input.Remembered)
                    remembered = i;
            }
            if (keep < 0)
                keep = remembered;
            if (keep < 0)
                for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
                    if (_currentTabStops[i].PopupTier == scope.Tier)
                    {
                        keep = i;
                        break;
                    }
            var currentRow = input.Find(_state.FocusedId);
            if (formerlyFocused >= 0 && (currentRow < 0 || !input.Available[currentRow]) && _state.FocusedId == scope.FocusAtStart)
            {
                if (keep >= 0)
                    FocusGroupControl(_currentTabStops[keep].Id, keyboard: true);
                else
                    _state.FocusedId = _ownedControlFocus = null;
            }
            input.Remembered = keep >= 0 ? _currentTabStops[keep].Id : null;
            for (var i = _currentTabStops.Count - 1; i >= scope.TabStart; i--)
                if (i != keep && _currentTabStops[i].PopupTier == scope.Tier)
                    _currentTabStops.RemoveAt(i);
            var focusedRow = input.Find(_state.FocusedId);
            if (_state.NavCursorVisible && focusedRow >= 0 && input.Available[focusedRow]
                && input.Frames[focusedRow] == _state.FrameCount && input.Rows[focusedRow] == _state.FocusedId)
                list.PaintFocus(this, input.FocusPaints[focusedRow]);
        }
        finally { _keyEvents = scope.Keys; _accessibilityParent = scope.Parent; _accessibilityWidget = scope.Widget; }
    }

    internal void RenderListItem(UiListState list, UiListItem item)
    {
        var context = new UiControlContext(this);
        var size = context.AllocatedLayoutSize;
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(UiControlWidget.PrimaryControlId);
        var parent = _accessibilityParent;
        var owner = _accessibilityWidget;
        var composite = list.Navigation == UiListNavigation.Composite;
        var navigable = list.Navigation != UiListNavigation.Off;
        var disabled = list.Selectable && item.DisabledSelection && item.Action is null;
        var childStart = _currentTabStops.Count;
        BeginDisabled(disabled);
        try
        {
            using var clip = context.Clip(bounds);
            var interaction = navigable ? ProcessButton<RangeControlKeys>(UiControlWidget.PrimaryControlId, bounds, UiButtonActivation.Release)
                : item.Action is not null ? ProcessControlPointer(UiControlWidget.PrimaryControlId, bounds, UiButtonActivation.Release) : default;
            var invoke = interaction.Activated;
            var kind = UiListActionKind.Pointer;
            if (_accessibility?.Take(root, UiAccessibilityAction.Invoke) is { } invocation)
            {
                invocation.Result = item.Action is null ? UiAccessibilityResult.InvalidOperation
                    : RadioAccessibilityAvailability(true, IntersectRect(CurrentClipRect, bounds));
                if (invocation.Result == UiAccessibilityResult.Success)
                {
                    invoke = true;
                    kind = UiListActionKind.Accessibility;
                }
            }
            if (_state.FocusedId == root && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
                foreach (var key in _keyEvents)
                    if (key.IsDown && key.Modifiers == 0 && key.Key is UiKey.Enter or UiKey.Space)
                    {
                        invoke = true;
                        kind = key.Key == UiKey.Enter ? UiListActionKind.Enter : UiListActionKind.Space;
                    }
            var role = composite ? UiAccessibilityRole.Row : list.Selectable ? UiAccessibilityRole.Option : UiAccessibilityRole.ListItem;
            ExposeAccessibility(root, parent, role, item.Label, bounds, focusable: navigable, valuePattern: false);
            _accessibilityParent = root;
            _accessibilityWidget = null;
            var extent = MathF.Min(size.X, item.CheckExtent);
            var checkBounds = new UiRect(list.Options.RightToLeft ? bounds.X + bounds.Width - extent : bounds.X, bounds.Y + (bounds.Height - MathF.Min(bounds.Height, extent)) / 2, extent, MathF.Min(bounds.Height, extent));
            var contentBounds = bounds with
            {
                X = bounds.X + (list.Options.RightToLeft ? 0 : extent),
                Width = bounds.Width - extent
            };
            if (composite)
            {
                var cell = ResolveId("##content-cell");
                list.Input.ContentCellIds[item.Index] = cell;
                ExposeAccessibility(cell, root, UiAccessibilityRole.GridCell, item.Label, contentBounds, focusable: false, valuePattern: false);
                _accessibilityParent = cell;
            }
            var contentStart = _currentTabStops.Count;
            var contentSize = context.MeasureConstrained(item.Content,
                new(0, contentBounds.Width, 0, contentBounds.Height));
            var childBounds = contentBounds with
            {
                Y = contentBounds.Y + (contentBounds.Height - contentSize.Y) / 2,
                Width = contentSize.X,
                Height = contentSize.Y
            };
            if (list.Options.RightToLeft)
                childBounds = childBounds with
                {
                    X = contentBounds.X + contentBounds.Width - contentSize.X
                };
            RenderLayoutChild(item.Content, childBounds, bounds);
            list.Input.Children[item.Index].Clear();
            for (var i = contentStart; i < _currentTabStops.Count; i++)
                if (_currentTabStops[i].PopupTier == _popupTierDepth)
                    list.Input.Children[item.Index].Add(new(_currentTabStops[i].Id, _currentTabStops[i].ConsumesArrows));
            if (list.Navigation == UiListNavigation.Items && list.Input.Children[item.Index].Count != 0)
                throw new InvalidOperationException("Interactive List content requires Composite navigation, or Off for independent children.");
            var check = default(UiButtonInteraction);
            string? checkboxId = null;
            if (list.Selectable)
            {
                _accessibilityParent = root;
                if (composite)
                {
                    var cell = ResolveId("##check-cell");
                    list.Input.CheckCellIds[item.Index] = cell;
                    ExposeAccessibility(cell, root, UiAccessibilityRole.GridCell, item.SelectionLabel, checkBounds, focusable: false, valuePattern: false);
                    _accessibilityParent = cell;
                }
                var checkId = ResolveId("##checkmark");
                checkboxId = list.Input.CheckIds[item.Index] = checkId;
                BeginDisabled(item.DisabledSelection);
                try
                {
                    check = ProcessButton<RangeControlKeys>("##checkmark", checkBounds, UiButtonActivation.Release);
                    _currentTabStops.RemoveAll(stop => stop.Id == checkId);
                    if (_state.FocusedId == checkId)
                        FocusGroupControl(root, keyboard: false);
                    if (_accessibility?.Take(checkId, UiAccessibilityAction.Toggle) is { } toggle)
                    {
                        toggle.Result = RadioAccessibilityAvailability(!item.DisabledSelection, IntersectRect(CurrentClipRect, checkBounds));
                        if (toggle.Result == UiAccessibilityResult.Success)
                            check = check with
                            {
                                Activated = true
                            };
                    }
                    if (check.Activated)
                    {
                        list.Toggle(item.Index);
                        _requestFrame?.Invoke();
                        invoke = false;
                    }
                }
                finally { EndDisabled(); }
                foreach (var action in (ReadOnlySpan<UiAccessibilityAction>)[UiAccessibilityAction.Select, UiAccessibilityAction.Deselect, UiAccessibilityAction.AddToSelection])
                    if (_accessibility?.Take(root, action) is { } request)
                    {
                        request.Result = RadioAccessibilityAvailability(!item.DisabledSelection, IntersectRect(CurrentClipRect, bounds));
                        if (request.Result == UiAccessibilityResult.Success)
                        {
                            if (action == UiAccessibilityAction.AddToSelection && !list.Selection!.Multiple && list.Selection.Count != 0 && !list.Selected(item.Index))
                                request.Result = UiAccessibilityResult.InvalidOperation;
                            else
                                list.SetSelected(item.Index, action != UiAccessibilityAction.Deselect, action == UiAccessibilityAction.AddToSelection);
                        }
                    }
            }
            if (list.Selectable && kind == UiListActionKind.Pointer && IsHovering(IntersectRect(CurrentClipRect, checkBounds)))
                invoke = false;
            if (invoke && IsControlEnabled)
            {
                list.Activate(item.Index, kind);
                _requestFrame?.Invoke();
            }
            if (checkboxId is not null)
            {
                BeginDisabled(item.DisabledSelection);
                try
                {
                    // Sealed, list-owned rows share the list's paint environment. Keep
                    // their clip and disabled state until all selection writes settle.
                    list.Input.Checkmarks[item.Index] = new(checkBounds, CurrentClipRect, check, IsControlEnabled);
                    ExposeAccessibility(checkboxId, _accessibilityParent, UiAccessibilityRole.Checkbox, item.SelectionLabel, checkBounds,
                        focusable: false, valuePattern: false, toggleState: list.Selected(item.Index) ? UiAccessibilityToggleState.On : UiAccessibilityToggleState.Off);
                }
                finally { EndDisabled(); }
            }
            if (navigable && interaction.Hovered && IsControlEnabled)
                SetMouseCursor(UiMouseCursor.Hand);
            list.Input.FocusPaints[item.Index] = new(bounds, CurrentClipRect);
            list.Input.Rows[item.Index] = root;
            var registered = false;
            for (var i = childStart; i < _currentTabStops.Count; i++)
                registered |= _currentTabStops[i].Id == root;
            list.Input.Available[item.Index] = navigable && IsControlEnabled && registered;
            list.Input.Frames[item.Index] = _state.FrameCount;
            _accessibility?.SetListItem(root, list.Selectable ? list.Selected(item.Index) : null, _state.FocusedId == root, item.Action is not null);
        }
        finally { EndDisabled(); _accessibilityParent = parent; _accessibilityWidget = owner; FinishLayoutBox(origin, size); }
    }
}

public sealed partial class UiImmediateContext
{
    internal void PaintListCheckmark<TPresentation>(UiListCheckmarkPaint paint, bool selected) where TPresentation : IUiListPresentation
    {
        var context = new UiControlContext(this);
        using var clip = context.Clip(paint.Clip);
        BeginDisabled(!paint.Enabled);
        try
        {
            TPresentation.PaintCheckmark(context, paint.Bounds, paint.Interaction, selected);
        }
        finally { EndDisabled(); }
    }
    internal void PaintListFocus<TPresentation>(UiListFocusPaint paint) where TPresentation : IUiListPresentation
    {
        var context = new UiControlContext(this);
        using var clip = context.Clip(paint.Clip);
        TPresentation.PaintFocus(context, paint.Bounds);
    }
}

public sealed partial class UiAccessibilityTree
{
    internal void SetListCheckmark(string id, bool selected)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                ToggleState = selected ? UiAccessibilityToggleState.On : UiAccessibilityToggleState.Off
            };
    }
    internal void SetListContainer(string id, bool? multiple)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                CanSelectMultiple = multiple == true,
                ListSelection = multiple.HasValue
            };
    }
    internal void SetGrid(string id, UiAccessibilityGridInfo grid)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Grid = grid
            };
    }
    internal void SetGridItem(string id, UiAccessibilityGridItemInfo item)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                GridItem = item
            };
    }
    internal void SetListItem(string id, bool? selected, bool focused, bool invoke)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Selected = selected,
                Focused = focused,
                InvokePattern = invoke
            };
    }
}
