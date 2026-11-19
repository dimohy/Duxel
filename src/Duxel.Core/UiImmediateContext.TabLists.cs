namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiTabListInteraction TabListInput(string id, UiRect bounds, ReadOnlySpan<UiTabOption> items,
        ReadOnlySpan<UiRect> layouts, string? selected, UiTabListOptions options, UiTabListInputState state) =>
        _ui.ProcessTabList(id, bounds, items, layouts, selected, options, state);
}

public sealed partial class UiImmediateContext
{
    internal UiTabListInteraction ProcessTabList(string id, UiRect bounds, ReadOnlySpan<UiTabOption> items,
        ReadOnlySpan<UiRect> layouts, string? selected, UiTabListOptions options, UiTabListInputState state)
    {
        EnsureNotMeasuring();
        state.Begin(selected);
        state.RequireSelection = options.RequireSelection;
        var root = ResolveId(id);
        state.CapturePanelContext(this, root);
        var first = -1;
        var focused = -1;
        var selectedIndex = -1;
        var hit = -1;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].Value == selected)
                selectedIndex = i;
            if (!items[i].Enabled || !state.IsVisible(i))
                continue;
            if (first < 0)
                first = i;
            if (items[i].Value == state.FocusedValue)
                focused = i;
            var local = layouts[i];
            var hitBounds = new UiRect(bounds.X + local.X, bounds.Y + local.Y, local.Width, local.Height);
            if (state.HeaderClip is { } headerClip)
                hitBounds = IntersectRect(hitBounds, headerClip);
            if (hitBounds.Width > 0 && hitBounds.Height > 0 && IsHovering(hitBounds))
                hit = i;
        }
        if (focused < 0)
            focused = selectedIndex >= 0 && items[selectedIndex].Enabled && state.IsVisible(selectedIndex) ? selectedIndex : first;
        BeginDisabled(first < 0);
        UiButtonInteraction control;
        try
        {
            var pointerBounds = hit < 0 ? default : layouts[hit] with
            {
                X = bounds.X + layouts[hit].X,
                Y = bounds.Y + layouts[hit].Y
            };
            var pressed = ButtonBehavior(id, pointerBounds, out var hovered, out var held, UiButtonActivation.Release);
            control = ProcessControlKeyboard<RangeControlKeys>(bounds, new(pressed, hovered, held));
        }
        finally { EndDisabled(); }
        if (!IsControlEnabled || first < 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            state.PressedValue = null;
            state.KeyboardPressed = state.WasFocused = false;
            PublishTabList(root, id, bounds, items, layouts, state, control, -1);
            return new(control, -1, -1, -1);
        }
        if (_leftMousePressed)
        {
            state.PressedValue = hit >= 0 ? items[hit].Value : null;
            if (hit >= 0)
            {
                focused = hit;
                state.OverflowFocused = false;
            }
        }
        else if (control.Focused && !state.WasFocused)
        {
            focused = selectedIndex >= 0 && items[selectedIndex].Enabled && state.IsVisible(selectedIndex) ? selectedIndex : first;
            state.OverflowFocused = false;
        }
        if (state.SuppressPointerActivation)
            state.PressedValue = null;
        if (!state.SuppressPointerActivation && control.Activated && hit >= 0 && state.PressedValue == items[hit].Value)
        {
            state.Select(items[hit].Value);
            focused = hit;
        }
        if (options.Overflow is not null && state.Visibility is not null && state.Visibility.AsSpan().Contains(false)
            && state.HeaderClip is { } onlyAction && (onlyAction.Width <= 0 || onlyAction.Height <= 0))
            state.OverflowFocused = true;
        var returnedFromOverflow = false;
        if (control.Focused && state.OverflowFocused)
            foreach (var key in _keyEvents)
            {
                if (!key.IsDown || (key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
                    continue;
                var direction = options.Vertical
                    ? key.Key == UiKey.UpArrow ? -1 : key.Key == UiKey.DownArrow ? 1 : 0
                    : key.Key == UiKey.LeftArrow ? -1 : key.Key == UiKey.RightArrow ? 1 : 0;
                if (!options.Vertical && options.RightToLeft)
                    direction = -direction;
                if (key.Key == UiKey.Home)
                    direction = 1;
                else if (key.Key == UiKey.End)
                    direction = -1;
                if (direction == 0)
                    continue;
                focused = direction > 0 ? first : items.Length - 1;
                while (!items[focused].Enabled || !state.IsVisible(focused))
                    focused--;
                state.OverflowFocused = false;
                CancelControlSpace();
                returnedFromOverflow = true;
                if (options.SelectOnFocus)
                    state.Select(items[focused].Value);
                _requestFrame?.Invoke();
                break;
            }
        if (control.Focused && !state.OverflowFocused && !returnedFromOverflow)
        {
            if (!state.WasFocused && options.SelectOnFocus && focused >= 0)
                state.Select(items[focused].Value);
            foreach (var key in _keyEvents)
            {
                if ((key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
                    continue;
                if (key.Key == UiKey.Space)
                {
                    if (key.IsDown)
                        state.KeyboardPressed = true;
                    else
                    {
                        if (state.KeyboardPressed)
                            state.Select(items[focused].Value);
                        state.KeyboardPressed = false;
                    }
                    continue;
                }
                if (!key.IsDown)
                    continue;
                if (key.Key == UiKey.Escape)
                {
                    state.KeyboardPressed = false;
                    state.PressedValue = null;
                    if (_state.ActiveId == root)
                        _state.ActiveId = null;
                    continue;
                }
                if (key.Key == UiKey.Enter)
                {
                    state.Select(items[focused].Value);
                    continue;
                }
                var direction = options.Vertical
                    ? key.Key == UiKey.UpArrow ? -1 : key.Key == UiKey.DownArrow ? 1 : 0
                    : key.Key == UiKey.LeftArrow ? -1 : key.Key == UiKey.RightArrow ? 1 : 0;
                if (!options.Vertical && options.RightToLeft)
                    direction = -direction;
                var next = focused;
                if (key.Key == UiKey.Home)
                    next = first;
                else if (key.Key == UiKey.End)
                {
                    next = items.Length - 1;
                    while (!items[next].Enabled || !state.IsVisible(next))
                        next--;
                }
                else if (direction != 0)
                {
                    var nextVisible = next + direction;
                    while (nextVisible >= 0 && nextVisible < items.Length && (!items[nextVisible].Enabled || !state.IsVisible(nextVisible)))
                        nextVisible += direction;
                    if (options.Overflow is not null && state.Visibility is not null && state.Visibility.AsSpan().Contains(false)
                        && (nextVisible < 0 || nextVisible >= items.Length))
                    {
                        state.OverflowFocused = true;
                        state.KeyboardPressed = false;
                        CancelControlSpace();
                        _requestFrame?.Invoke();
                        break;
                    }
                    do
                    {
                        next = (next + direction + items.Length) % items.Length;
                    } while (!items[next].Enabled || !state.IsVisible(next));
                }
                if (next == focused)
                    continue;
                focused = next;
                state.KeyboardPressed = false;
                if (options.SelectOnFocus)
                    state.Select(items[focused].Value);
                _requestFrame?.Invoke();
            }
        }
        else
            state.KeyboardPressed = false;
        if (_accessibility is { } tree)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (!state.IsVisible(i))
                    continue;
                var itemId = ResolveId(state.ItemId(id, i));
                var rect = layouts[i] with
                {
                    X = bounds.X + layouts[i].X,
                    Y = bounds.Y + layouts[i].Y
                };
                if (tree.Take(itemId, UiAccessibilityAction.Select) is { } selection)
                {
                    var clip = TabItemClip(rect, state);
                    selection.Result = !items[i].Enabled ? UiAccessibilityResult.Disabled
                        : clip.Width <= 0 || clip.Height <= 0 ? UiAccessibilityResult.Unavailable : UiAccessibilityResult.Success;
                    if (selection.Result == UiAccessibilityResult.Success)
                        state.Select(items[i].Value);
                }
                if (tree.Take(itemId, UiAccessibilityAction.Deselect) is { } deselection)
                {
                    var clip = TabItemClip(rect, state);
                    deselection.Result = !items[i].Enabled ? UiAccessibilityResult.Disabled
                        : clip.Width <= 0 || clip.Height <= 0 ? UiAccessibilityResult.Unavailable
                        : state.RequireSelection && state.Value == items[i].Value ? UiAccessibilityResult.InvalidOperation
                        : UiAccessibilityResult.Success;
                    if (deselection.Result == UiAccessibilityResult.Success && state.Value == items[i].Value)
                        state.Select(null);
                }
                if (tree.Take(itemId, UiAccessibilityAction.Focus) is { } focus)
                {
                    BeginDisabled(!items[i].Enabled);
                    try
                    {
                        ApplyAccessibilityFocus(root, TabItemClip(rect, state), focus);
                    }
                    finally { EndDisabled(); }
                    if (focus.Result == UiAccessibilityResult.Success)
                    {
                        focused = i;
                        state.OverflowFocused = false;
                        control = control with
                        {
                            Focused = true,
                            FocusVisible = true
                        };
                        _focusedControlSeen = true;
                        _focusedControlTier = _popupTierDepth;
                        state.KeyboardPressed = false;
                        if (options.SelectOnFocus)
                            state.Select(items[i].Value);
                    }
                }
            }
        }
        if (state.OverflowFocused)
            focused = -1;
        state.FocusedValue = focused >= 0 ? items[focused].Value : null;
        state.WasFocused = control.Focused;
        var heldIndex = control.Held && hit >= 0 && state.PressedValue == items[hit].Value ? hit
            : control.Focused && state.KeyboardPressed ? focused : -1;
        if (!_leftMouseDown)
            state.PressedValue = null;
        if (state.Changes.Length != 0)
            _requestFrame?.Invoke();
        PublishTabList(root, id, bounds, items, layouts, state, control, focused);
        return new(control, hit, heldIndex, focused);
    }

    internal void CommitTabList(string id, UiRect bounds, ReadOnlySpan<UiTabOption> items, ReadOnlySpan<UiRect> layouts,
        UiTabListInputState state, UiTabListInteraction interaction)
    {
        state.DeferPublication = false;
        PublishTabList(ResolveId(id), id, bounds, items, layouts, state, interaction.Control, interaction.FocusedIndex);
    }

    private void PublishTabList(string root, string id, UiRect bounds, ReadOnlySpan<UiTabOption> items,
        ReadOnlySpan<UiRect> layouts, UiTabListInputState state, UiButtonInteraction interaction, int focused)
    {
        if (state.DeferPublication || _accessibility is not { } tree)
            return;
        ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.TabList, _accessibilityWidget?.AccessibleName ?? id,
            bounds, focusable: false, valuePattern: false, selectionRequired: state.RequireSelection);
        for (var i = 0; i < items.Length; i++)
        {
            if (!state.IsVisible(i))
                continue;
            var rect = layouts[i] with
            {
                X = bounds.X + layouts[i].X,
                Y = bounds.Y + layouts[i].Y
            };
            var clip = TabItemClip(rect, state);
            var itemId = ResolveId(state.ItemId(id, i));
            var isFocused = interaction.Focused && focused == i;
            tree.Add(new(itemId, root, UiAccessibilityRole.Tab, items[i].Label, clip,
                IsControlEnabled && items[i].Enabled && _popupTierDepth >= _blockingKeyboardTier,
                clip.Width <= 0 || clip.Height <= 0, isFocused, ValuePattern: false)
            {
                Selected = state.Value == items[i].Value,
                Hint = items[i].Badge?.Text
            }, rect);
            if (isFocused)
                (_accessibilityFocusOwner, _accessibilityFocusTarget) = (root, itemId);
        }
    }

    private UiRect TabItemClip(UiRect rect, UiTabListInputState state)
    {
        var clip = IntersectRect(CurrentClipRect, rect);
        return state.HeaderClip is { } header ? IntersectRect(clip, header) : clip;
    }
}
