namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private bool NavGroupContainsFocus<TPresentation>(UiNav<TPresentation> nav, int group) where TPresentation : IUiNavPresentation
    {
        for (var i = 0; i < nav.ItemIds[group].Length; i++)
            if (nav.ItemIds[group][i] == _state.FocusedId || nav.Secondary.IsItemFocused(group, i, _state.FocusedId)) return true;
        return false;
    }
    private static UiRect NavActionBounds<TPresentation>(UiRect bounds, UiNavOptions options) where TPresentation : IUiNavPresentation
    {
        var action = TPresentation.SecondaryActionBounds(bounds, options);
        if (!float.IsFinite(action.X) || !float.IsFinite(action.Y) || !float.IsFinite(action.Width) || !float.IsFinite(action.Height)
            || action.Width < 0 || action.Height < 0 || action.X < bounds.X || action.Y < bounds.Y
            || action.X + action.Width > bounds.X + bounds.Width || action.Y + action.Height > bounds.Y + bounds.Height)
            throw new InvalidOperationException("A navigation secondary action must fit its destination row.");
        return action;
    }
    private static UiRect NavPrimaryBounds<TPresentation>(UiRect row, bool hasAction, UiNavOptions options) where TPresentation : IUiNavPresentation
    {
        if (!hasAction) return row;
        var action = NavActionBounds<TPresentation>(row, options);
        return options.RightToLeft ? new(action.X + action.Width, row.Y, MathF.Max(0, row.X + row.Width - action.X - action.Width), row.Height)
            : new(row.X, row.Y, MathF.Max(0, action.X - row.X), row.Height);
    }
    private UiNavSecondaryAction? ProcessNavSecondaryInput<TPresentation>(UiNav<TPresentation> nav,
        UiNavSecondaryEntry entry, string itemId, UiRect row, UiVector2 size, UiVector2 origin) where TPresentation : IUiNavPresentation
    {
        var bounds = NavActionBounds<TPresentation>(row, nav.EffectiveOptions(RightToLeft));
        var state = nav.Secondary;
        BeginDisabled(!entry.Action.Enabled);
        try
        {
            _lastItemId = entry.Id;
            var pointer = ProcessControlPointer(entry.Name, bounds, UiButtonActivation.Release);
            var keyboardBounds = _state.FocusedId == entry.Id ? new UiRect(origin.X, origin.Y, size.X, size.Y) : bounds;
            var interaction = ProcessControlKeyboard<ActionControlKeys>(entry.Id, keyboardBounds, pointer);
            interaction = ApplyAccessibilityInvoke(entry.Id, bounds, interaction);
            entry.Interaction = interaction;
            var clipped = IntersectRect(CurrentClipRect, row);
            var available = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier && clipped.Width > 0 && clipped.Height > 0;
            if (!available && state.ContextPress == itemId) state.ContextPress = null;
            var hovered = available && !_layoutPointerBlocked && IsHovering(clipped)
                && _layoutPointerBlockedTier != _popupTierDepth;
            if (_rightMousePressed && hovered) state.ContextPress = itemId;
            var rightClick = _rightMouseReleased && state.ContextPress == itemId && hovered;
            if (_rightMouseReleased && state.ContextPress == itemId) state.ContextPress = null;
            var keyboard = (_state.FocusedId == itemId || _state.FocusedId == entry.Id) && _keyEvents.Any(key => key.IsDown
                && (key.Key == UiKey.ContextMenu && key.Modifiers == 0 || key.Key == UiKey.F10 && key.Modifiers == KeyModifiers.Shift));
            var expansion = _accessibility?.Take(entry.Id, UiAccessibilityAction.Expand)
                ?? _accessibility?.Take(entry.Id, UiAccessibilityAction.Collapse);
            if (expansion is { } request)
            {
                var actionClip = IntersectRect(CurrentClipRect, bounds);
                if (!IsControlEnabled) request.Result = UiAccessibilityResult.Disabled;
                else if (!(available || ReferenceEquals(state.MenuEntry, entry) && state.Owner.Menu.Open
                    && ReferenceEquals(_activeMenu, state.Owner.Menu) && _blockingKeyboardTier <= state.Owner.Menu.BlockingTier)
                    || actionClip.Width <= 0 || actionClip.Height <= 0) request.Result = UiAccessibilityResult.Unavailable;
                else if (request.Action == UiAccessibilityAction.Collapse && !ReferenceEquals(state.MenuEntry, entry))
                    request.Result = UiAccessibilityResult.Success;
                else
                {
                    if (!ReferenceEquals(state.MenuEntry, entry) || !state.Owner.Menu.Open)
                        PrepareNavContext(state, entry, entry.Id);
                    state.ExpansionRequest = request;
                }
            }
            if (available && (rightClick || keyboard))
            {
                var returnFocus = _state.FocusedId == entry.Id ? entry.Id : itemId;
                if (rightClick) FocusGroupControl(itemId, false);
                PrepareNavContext(state, entry, returnFocus, rightClick ? new(_mousePosition.X - row.X, _mousePosition.Y - row.Y) : null);
                state.OpenRequested = true;
            }
            return interaction.Activated ? entry.Action : null;
        }
        finally { EndDisabled(); }
    }
    private void PrepareNavContext(UiNavSecondaryState state, UiNavSecondaryEntry entry, string returnFocus, UiVector2? pointerOffset = null)
    {
        if (state.MenuEntry is { } old && !ReferenceEquals(old, entry)) CancelMenuPopup(state.Owner.Menu);
        state.MenuEntry = entry;
        state.ReturnFocus = returnFocus;
        state.PointerOffset = pointerOffset;
    }
    private void PaintNavSecondary<TPresentation>(UiNav<TPresentation> nav, UiNavSecondaryEntry entry,
        string itemId, string parent, UiRect row, bool ownerEnabled, UiButtonInteraction primary, string? oldFocus)
        where TPresentation : IUiNavPresentation
    {
        var context = new UiControlContext(this);
        var bounds = NavActionBounds<TPresentation>(row, nav.EffectiveOptions(RightToLeft));
        var clip = IntersectRect(CurrentClipRect, bounds);
        var focused = _state.FocusedId == entry.Id;
        var enabled = ownerEnabled && entry.Action.Enabled && IsControlEnabled;
        var available = enabled && _popupTierDepth >= _blockingKeyboardTier;
        var interaction = entry.Interaction with { Focused = focused, FocusVisible = focused && _state.NavCursorVisible };
        if (available && (focused || clip.Width > 0 && clip.Height > 0))
        {
            _currentTabStops.Add(new(entry.Id, _popupTierDepth));
            if (focused)
            {
                _focusedControlSeen = true;
                _focusedControlTier = _popupTierDepth;
                if (oldFocus != entry.Id) RequestScrollReveal(bounds);
            }
        }
        var tooltip = entry.Tooltip;
        var semantic = BeginTooltipAccessibility(tooltip, tooltip.Text, entry.Id + "##tooltip");
        try
        {
            BeginDisabled(!ownerEnabled || !entry.Action.Enabled);
            try
            {
                if (primary.Hovered || primary.Focused || focused || context.IsHovered(row)
                    || ReferenceEquals(nav.Secondary.MenuEntry, entry) && nav.Secondary.Owner.Menu.Open)
                    TPresentation.PaintSecondaryAction(context, entry.Action, bounds, interaction, nav.EffectiveOptions(RightToLeft));
            }
            finally { EndDisabled(); }
            ClaimTooltipAccessibilityTarget(entry.Id);
            _accessibility?.Add(new(entry.Id, parent, UiAccessibilityRole.Button, entry.Action.Label, clip,
                enabled, clip.Width <= 0 || clip.Height <= 0, focused, Focusable : enabled, ValuePattern : false)
            {
                Expansion = ReferenceEquals(nav.Secondary.MenuEntry, entry) && nav.Secondary.Owner.Menu.Open
                    ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed,
                Tooltip = TooltipAccessibility(entry.Id)
            }, bounds);
        }
        finally
        {
            var semantics = EndTooltipAccessibility(semantic);
            ObserveTooltipTarget<UiHoverFocusTooltipBehavior>(tooltip, tooltip.Text, tooltip.Options, semantics, bounds,
                available && clip.Width > 0 && clip.Height > 0, available && interaction.Hovered, focused, _tooltipTargetDepth + 1);
        }
    }
    private UiNavSecondaryAction? RenderNavSecondaryMenu<TPresentation>(UiNav<TPresentation> nav, UiNavLayout layout, UiVector2 origin)
        where TPresentation : IUiNavPresentation
    {
        var secondary = nav.Secondary;
        if (secondary.MenuEntry is not { } entry) return null;
        var menu = secondary.Owner.Menu;
        UiNavRow? owner = null;
        foreach (var row in layout.Rows)
            if (row.Item >= 0 && ReferenceEquals(nav.Secondary.Entries[row.Group][row.Item], entry)) { owner = row; break; }
        var restore = secondary.ReturnFocus;
        if (owner is not { } target || !nav.Groups[target.Group].Enabled || !nav.Groups[target.Group].Items.Span[target.Item].Enabled
            || !entry.Action.Enabled || !IsControlEnabled)
        {
            for (var g = 0; g < nav.Groups.Length; g++)
                if (nav.Secondary.Entries[g].Contains(entry)) restore = nav.GroupIds[g];
            CancelMenuPopup(menu);
            if (restore is not null && IsControlEnabled)
                foreach (var candidate in layout.Rows)
                    if (candidate.Item < 0 && nav.GroupIds[candidate.Group] == restore
                        && nav.Groups[candidate.Group].Enabled && nav.Groups[candidate.Group].Collapsible)
                    {
                        var clipped = IntersectRect(CurrentClipRect, OffsetFieldRect(candidate.Bounds, origin));
                        if (clipped.Width > 0 && clipped.Height > 0)
                        {
                            FocusGroupControl(restore, true);
                            ObserveControlFocus(restore);
                        }
                    }
            secondary.MenuEntry = null;
            return null;
        }
        var bounds = OffsetFieldRect(target.Bounds, origin);
        var visible = IntersectRect(CurrentClipRect, bounds);
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            CancelMenuPopup(menu);
            secondary.MenuEntry = null;
            return null;
        }
        menu.Prepare(ResolveCachedIdentifier(entry.Id, "##menu", false), entry.MenuItems);
        menu.TriggerId = entry.Id;
        menu.FocusOwnerId = secondary.ReturnFocus;
        var widget = _accessibilityWidget;
        _accessibilityWidget = null;
        try
        {
            var result = RenderMenuPopup<UiFluentMenuPresentation>(entry.Name, entry.Action.Label, entry.MenuItems,
                UiFluentMenuPresentation.DefaultOptions with { RightToLeft = nav.EffectiveOptions(RightToLeft).RightToLeft }, menu,
                secondary.PointerOffset is { } offset ? new(bounds.X + offset.X, bounds.Y + offset.Y, 1, 1)
                    : NavActionBounds<TPresentation>(bounds, nav.EffectiveOptions(RightToLeft)), default,
                controlledOpen : secondary.OpenRequested ? true : null, expansionRequest : secondary.ExpansionRequest);
            return result is null ? null : entry.Action;
        }
        finally { _accessibilityWidget = widget; }
    }
}
