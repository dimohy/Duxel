namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal UiNavSecondaryAction? RenderNav<TPresentation>(UiNav<TPresentation> nav, string name) where TPresentation : IUiNavPresentation
    {
        EnsureNotMeasuring();
        UiNavSecondaryAction? secondaryAction = null;
        nav.Secondary.Attach(this);
        var frame = GetFrameCount();
        if (_rightMousePressed || !_rightMouseDown && !_rightMouseReleased
            || nav.Secondary.LastFrame != frame && nav.Secondary.LastFrame != frame - 1)
            nav.Secondary.ContextPress = null;
        nav.Secondary.LastFrame = frame;
        var context = new UiControlContext(this);
        var root = ResolveId(name);
        if (nav.RootId != root)
        {
            if (nav.RootId is not null)
            {
                CancelMenuPopup(nav.Secondary.Owner.Menu);
                nav.Secondary.MenuEntry = null;
                nav.Secondary.ContextPress = null;
            }
            nav.RootId = root;
            for (var g = 0; g < nav.Groups.Length; g++)
            {
                nav.GroupIds[g] = ResolveId(ResolveCachedIdentifier(name, nav.GroupNames[g], true));
                if (!nav.Groups[g].Items.IsEmpty)
                    nav.GroupItemsIds[g] = nav.GroupIds[g] + "##items";
                for (var i = 0; i < nav.Groups[g].Items.Length; i++)
                {
                    var itemName = ResolveCachedIdentifier(name, nav.ItemNames[g][i], true);
                    nav.ItemIds[g][i] = ResolveId(itemName);
                    if (nav.Secondary.Entries[g][i] is { } entry)
                    {
                        ReleaseTooltip(entry.Tooltip);
                        entry.Name = ResolveCachedIdentifier(itemName, "##action", true);
                        entry.Id = ResolveId(entry.Name);
                    }
                }
            }
        }
        var layout = nav.Prepare(context, true);
        var origin = GetCursorScreenPos();
        var oldFocus = _state.FocusedId;
        NavigateNav(nav, layout.Rows);
        // Collapsing a category retains its destination and moves hidden focus to the disclosure.
        for (var g = 0; g < nav.Groups.Length; g++)
            if (!nav.IsGroupExpanded(g) && NavGroupContainsFocus(nav, g))
                FocusGroupControl(nav.GroupIds[g], true);
        layout = nav.Prepare(context, true);
        var tabStart = _currentTabStops.Count;
        var focusedSeen = _focusedControlSeen;
        var interactions = nav.Interactions;
        interactions.Clear();
        string? invoked = null;
        foreach (var row in layout.Rows)
        {
            var group = nav.Groups[row.Group];
            if (group.IsSectionHeader) continue;
            var category = row.Item < 0;
            var enabled = group.Enabled && (category ? group.Collapsible : group.Items.Span[row.Item].Enabled);
            var id = category ? nav.GroupIds[row.Group] : nav.ItemIds[row.Group][row.Item];
            var scopedName = ResolveCachedIdentifier(name, category ? nav.GroupNames[row.Group] : nav.ItemNames[row.Group][row.Item], true);
            var bounds = OffsetFieldRect(row.Bounds, origin);
            var entry = category ? null : nav.Secondary.Entries[row.Group][row.Item];
            var primaryBounds = NavPrimaryBounds<TPresentation>(bounds, entry is not null, nav.EffectiveOptions(RightToLeft));
            BeginDisabled(!enabled);
            try
            {
                _lastItemId = id;
                // Arrow navigation can focus an offscreen row before ScrollView reveals its actual bounds.
                var keyboardBounds = _state.FocusedId == id ? new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y) : bounds;
                var interaction = nav.ProcessRowInput(new(this, scopedName, id, primaryBounds, keyboardBounds, category));
                interaction = ApplyAccessibilityInvoke(id, bounds, interaction);
                var expansion = category ? _accessibility?.Take(id, UiAccessibilityAction.Expand)
                    ?? _accessibility?.Take(id, UiAccessibilityAction.Collapse) : null;
                if (expansion is { } request)
                {
                    var visible = IntersectRect(CurrentClipRect, bounds);
                    request.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                        : visible.Width <= 0 || visible.Height <= 0 || _popupTierDepth < _blockingKeyboardTier
                            ? UiAccessibilityResult.Unavailable : UiAccessibilityResult.Success;
                    if (request.Result == UiAccessibilityResult.Success)
                        nav.SetExpanded(group.Value, request.Action == UiAccessibilityAction.Expand);
                }
                else if (interaction.Activated)
                {
                    if (category) nav.SetExpanded(group.Value, !nav.IsGroupExpanded(row.Group));
                    else invoked = group.Items.Span[row.Item].Value;
                }
                interactions[id] = interaction;
                if (entry is not null)
                    secondaryAction = ProcessNavSecondaryInput<TPresentation>(nav, entry, id, bounds, layout.Size, origin) ?? secondaryAction;
            }
            finally { EndDisabled(); }
        }
        _currentTabStops.RemoveRange(tabStart, _currentTabStops.Count - tabStart);
        _focusedControlSeen = focusedSeen;
        if (invoked is not null)
            nav.CommitDestination(invoked);
        var selected = nav.Value.Value;
        for (var g = 0; g < nav.Groups.Length; g++)
            if (!nav.IsGroupExpanded(g) && NavGroupContainsFocus(nav, g))
                FocusGroupControl(nav.GroupIds[g], true);
        layout = nav.Prepare(context, true);
        origin = AllocateLayoutBox(ConstrainToLayout(layout.Size));
        var rootBounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        using var clip = context.Clip(rootBounds);
        var parent = _accessibilityParent;
        var widget = _accessibilityWidget;
        ExposeAccessibility(root, parent, UiAccessibilityRole.Navigation, nav.AccessibleName ?? nav.Label, rootBounds,
            focusable : false, valuePattern : false);
        try
        {
            _accessibilityWidget = null;
            if (_accessibility is { } accessibility)
            {
                // Layout emits each disclosure before its contiguous destination rows.
                var rows = layout.Rows.AsSpan();
                var rowIndex = 0;
                while (rowIndex < rows.Length)
                {
                    ref readonly var first = ref rows[rowIndex++];
                    if (first.Item < 0) continue;
                    var groupIndex = first.Group;
                    while (rowIndex < rows.Length && rows[rowIndex].Group == groupIndex)
                        rowIndex++;
                    var start = first.Bounds;
                    var end = rows[rowIndex - 1].Bounds;
                    var bounds = new UiRect(origin.X, origin.Y + start.Y, layout.Size.X, end.Y + end.Height - start.Y);
                    var visible = IntersectRect(CurrentClipRect, bounds);
                    var group = nav.Groups[groupIndex];
                    accessibility.Add(new(nav.GroupItemsIds[groupIndex], root, UiAccessibilityRole.Group, group.Label,
                        visible, IsControlEnabled && group.Enabled, visible.Width <= 0 || visible.Height <= 0,
                        false, Focusable : false, ValuePattern : false) { LabeledBy = nav.GroupIds[groupIndex] }, bounds);
                }
            }
            string? preferred = null;
            string? focusedPrimary = null;
            string? focusedAction = null;
            foreach (var row in layout.Rows)
            {
                var group = nav.Groups[row.Group];
                var category = row.Item < 0;
                var expanded = nav.IsGroupExpanded(row.Group);
                var id = category ? nav.GroupIds[row.Group] : nav.ItemIds[row.Group][row.Item];
                var enabled = group.Enabled && (category ? group.Collapsible : group.Items.Span[row.Item].Enabled);
                var bounds = OffsetFieldRect(row.Bounds, origin);
                var entry = category ? null : nav.Secondary.Entries[row.Group][row.Item];
                var primaryBounds = NavPrimaryBounds<TPresentation>(bounds, entry is not null, nav.EffectiveOptions(RightToLeft));
                var visible = IntersectRect(CurrentClipRect, primaryBounds);
                var current = category ? !expanded && ContainsNavValue(group, selected)
                    : group.Items.Span[row.Item].Value == selected;
                var focused = _state.FocusedId == id;
                if (focused || entry?.Id == _state.FocusedId)
                {
                    focusedPrimary = id;
                    focusedAction = entry is { Action.Enabled : true } && enabled ? entry.Id : null;
                }
                if (current && enabled) preferred = id;
                if (enabled && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier
                    && (focused || visible.Width > 0 && visible.Height > 0))
                {
                    _currentTabStops.Add(new(id, _popupTierDepth));
                    if (focused)
                    {
                        _focusedControlSeen = true;
                        _focusedControlTier = _popupTierDepth;
                        if (oldFocus != id) RequestScrollReveal(bounds);
                    }
                }
                var interaction = interactions.GetValueOrDefault(id) with { Focused = focused,
                    FocusVisible = focused && _state.NavCursorVisible };
                BeginDisabled(!enabled && !category || !group.Enabled);
                try { TPresentation.Paint(context with { MotionKey = id }, row.Content, row.Layout, bounds, current, expanded, interaction, nav.EffectiveOptions(RightToLeft)); }
                finally { EndDisabled(); }
                var groupItemsId = nav.GroupItemsIds[row.Group];
                _accessibility?.Add(new(id, category ? root : groupItemsId,
                    category ? group.Collapsible ? UiAccessibilityRole.Button : UiAccessibilityRole.Label : UiAccessibilityRole.Link,
                    row.Content.Label, visible, IsControlEnabled && group.Enabled && (category || enabled), visible.Width <= 0 || visible.Height <= 0,
                    focused, Focusable : enabled, ValuePattern : false)
                {
                    IsCurrent = current,
                    Hint = category ? group.Description : row.Content.Badge?.Text,
                    ItemStatus = category ? null : row.Content.Badge?.Text,
                    Expansion = category && group.Collapsible ? expanded ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed : null,
                    ControlledElementId = category && expanded && group.Items.Length > 0 ? groupItemsId : null
                }, primaryBounds);
                if (entry is not null)
                    PaintNavSecondary<TPresentation>(nav, entry, id, groupItemsId, bounds,
                        enabled, interaction, oldFocus);
            }
            var keep = -1;
            for (var i = tabStart; i < _currentTabStops.Count; i++)
                if (_currentTabStops[i].Id == focusedPrimary) { keep = i; break; }
            if (keep < 0 && preferred is not null)
                for (var i = tabStart; i < _currentTabStops.Count; i++)
                    if (_currentTabStops[i].Id == preferred) { keep = i; break; }
            if (keep < 0 && _currentTabStops.Count > tabStart) keep = tabStart;
            for (var i = _currentTabStops.Count - 1; i >= tabStart; i--)
                if (i != keep && _currentTabStops[i].Id != focusedAction) _currentTabStops.RemoveAt(i);
        }
        finally { _accessibilityParent = parent; _accessibilityWidget = widget; }
        secondaryAction = RenderNavSecondaryMenu<TPresentation>(nav, layout, origin) ?? secondaryAction;
        FinishLayoutBox(origin, layout.Size);
        return secondaryAction;
    }

    internal UiButtonInteraction ProcessNavRowInput(string name, string id, UiRect bounds, UiRect keyboardBounds,
        bool category, UiButtonActivation activation)
    {
        var pointer = ProcessControlPointer(name, bounds, activation);
        return category
            ? ProcessControlKeyboard<ActionControlKeys>(id, keyboardBounds, pointer)
            : ProcessControlKeyboard<LinkControlKeys>(id, keyboardBounds, pointer);
    }

    private static bool ContainsNavValue(UiNavGroup group, string? value)
    {
        foreach (var item in group.Items.Span)
            if (item.Value == value) return true;
        return false;
    }

    private void NavigateNav<TPresentation>(UiNav<TPresentation> nav, UiNavRow[] rows) where TPresentation : IUiNavPresentation
    {
        if (_keyEvents.Count == 0 || !IsControlEnabled || _popupTierDepth < _blockingKeyboardTier) return;
        var current = Array.FindIndex(rows, row => (row.Item < 0 ? nav.GroupIds[row.Group] : nav.ItemIds[row.Group][row.Item]) == _state.FocusedId
            || row.Item >= 0 && nav.Secondary.IsItemFocused(row.Group, row.Item, _state.FocusedId));
        if (current < 0) return;
        foreach (var key in _keyEvents)
        {
            if (!key.IsDown || (key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0) continue;
            var row = rows[current];
            var group = nav.Groups[row.Group];
            var collapse = key.Key == (nav.EffectiveOptions(RightToLeft).RightToLeft ? UiKey.RightArrow : UiKey.LeftArrow);
            var expand = key.Key == (nav.EffectiveOptions(RightToLeft).RightToLeft ? UiKey.LeftArrow : UiKey.RightArrow);
            if (collapse || expand)
            {
                var entry = row.Item < 0 ? null : nav.Secondary.Entries[row.Group][row.Item];
                if (entry?.Id == _state.FocusedId)
                {
                    if (collapse) FocusGroupControl(nav.ItemIds[row.Group][row.Item], true);
                    break;
                }
                if (expand && entry is { Action.Enabled : true })
                {
                    FocusGroupControl(entry.Id, true);
                    break;
                }
                if (row.Item < 0 && group.Collapsible)
                {
                    if (expand && nav.IsGroupExpanded(row.Group))
                    {
                        var first = Array.FindIndex(group.Items.ToArray(), item => item.Enabled);
                        if (first >= 0) FocusGroupControl(nav.ItemIds[row.Group][first], true);
                    }
                    else nav.SetExpanded(group.Value, expand);
                }
                else if (collapse && group.Collapsible) FocusGroupControl(nav.GroupIds[row.Group], true);
                break;
            }
            var step = key.Key == UiKey.UpArrow ? -1 : key.Key == UiKey.DownArrow ? 1 : 0;
            if (key.Key is UiKey.Home or UiKey.End)
            {
                step = key.Key == UiKey.Home ? 1 : -1;
                current = key.Key == UiKey.Home ? -1 : rows.Length;
            }
            if (step == 0) continue;
            for (var attempt = 0; attempt < rows.Length; attempt++)
            {
                current = Math.Clamp(current + step, 0, rows.Length - 1);
                row = rows[current];
                group = nav.Groups[row.Group];
                if (!group.Enabled || (row.Item < 0 ? !group.Collapsible : !group.Items.Span[row.Item].Enabled)) continue;
                FocusGroupControl(row.Item < 0 ? nav.GroupIds[row.Group] : nav.ItemIds[row.Group][row.Item], true);
                break;
            }
        }
    }
}
