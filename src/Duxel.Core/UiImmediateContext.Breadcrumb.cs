namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal string? RenderBreadcrumb<TPresentation>(string id, string label, UiBreadcrumbOption[] items,
        UiBreadcrumbLayout layout, UiBreadcrumbInputState input) where TPresentation : IUiBreadcrumbPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var origin = AllocateLayoutBox(layout.Size);
        var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        using var clip = context.Clip(bounds);
        var root = ResolveId(id);
        if (input.Root != root)
        {
            input.Root = root;
            input.ScopedNames = input.ItemNames.Select(name => ResolveCachedIdentifier(id, name, true)).ToArray();
            input.ItemIds = input.ScopedNames.Select(ResolveId).ToArray();
            input.IndexById.Clear();
            for (var index = 0; index < input.ItemIds.Length; index++)
                input.IndexById.Add(input.ItemIds[index], index);
        }
        var list = ResolveCachedIdentifier(root, "##path", false);
        var overflowName = ResolveCachedIdentifier(id, "##breadcrumb-overflow", true);
        var overflowId = ResolveId(overflowName);
        var overflowVisible = layout.Indices.AsSpan().Contains(-1);
        if (!overflowVisible)
            CancelMenuPopup(input.Menu, input.ScopedNames[0]);
        var focused = _state.FocusedId is { } focusedId && input.IndexById.TryGetValue(focusedId, out var found) ? found : -1;
        if (focused >= 0 && !layout.Indices.AsSpan().Contains(focused))
            FocusGroupControl(overflowVisible ? overflowId : input.ItemIds[0], true);
        if (layout.Options.FocusMode == UiBreadcrumbFocusMode.Arrow && !input.Menu.Open)
            NavigateBreadcrumb(items, layout, input, overflowId);
        var parent = _accessibilityParent;
        var ownerWidget = _accessibilityWidget;
        var visibleRoot = IntersectRect(CurrentClipRect, bounds);
        ExposeAccessibility(root, parent, UiAccessibilityRole.Navigation, label, bounds, focusable: false, valuePattern: false);
        _accessibility?.Add(new(list, root, UiAccessibilityRole.OrderedList, string.Empty, visibleRoot,
            IsControlEnabled, visibleRoot.Width <= 0 || visibleRoot.Height <= 0, false, Focusable: false, ValuePattern: false), bounds);
        var tabStart = _currentTabStops.Count;
        string? invoked = null;
        UiButtonInteraction overflowInteraction = default;
        UiRect overflowBounds = default;
        try
        {
            _accessibilityWidget = null;
            for (var slot = 0; slot < layout.Indices.Length; slot++)
            {
                var index = layout.Indices[slot];
                var local = layout.Bounds[slot];
                var rect = local with
                {
                    X = origin.X + local.X,
                    Y = origin.Y + local.Y
                };
                if (slot > 0)
                {
                    var width = TPresentation.DividerWidth(layout.Options);
                    var divider = new UiRect(layout.Options.RightToLeft ? rect.X + rect.Width : rect.X - width, rect.Y, width, rect.Height);
                    TPresentation.PaintDivider(context, divider, layout.Options);
                }
                var itemId = index < 0 ? overflowId : input.ItemIds[index];
                var itemName = index < 0 ? layout.Options.OverflowLabel : items[index].Label;
                var listItem = ResolveCachedIdentifier(itemId, "##level", false);
                var visible = IntersectRect(CurrentClipRect, rect);
                var isCurrent = index == items.Length - 1;
                _accessibility?.Add(new(listItem, list, UiAccessibilityRole.ListItem, isCurrent ? itemName : string.Empty, visible,
                    IsControlEnabled, visible.Width <= 0 || visible.Height <= 0, false, Focusable: false, ValuePattern: false)
                {
                    PositionInSet = index < 0 ? 2 : index + 1,
                    SizeOfSet = items.Length,
                    IsCurrent = isCurrent,
                    ItemStatus = isCurrent ? layout.Options.CurrentLabel : null
                }, rect);
                _accessibilityParent = listItem;
                var tooltip = input.Tooltips[index < 0 ? items.Length : index];
                tooltip.Text = index < 0 ? layout.OverflowTooltip : layout.TooltipText[index];
                var semantic = tooltip.Text.Length > 0 && BeginTooltipAccessibility(tooltip, tooltip.Text,
                    ResolveCachedIdentifier(itemId, "##tooltip", false));
                UiButtonInteraction interaction;
                try
                {
                    if (index < 0)
                    {
                        interaction = ProcessMenuTrigger(overflowName, layout.Options.OverflowLabel, layout.Overflow, input.Menu, rect);
                        overflowInteraction = interaction;
                        overflowBounds = rect;
                        TPresentation.PaintItem(context, new(string.Empty, UiFluentBreadcrumbPresentation.More, false), rect, interaction, layout.Options);
                    }
                    else
                    {
                        var current = index == items.Length - 1;
                        var parentEnabled = IsControlEnabled;
                        BeginDisabled(!items[index].Enabled || current);
                        try
                        {
                            _lastItemId = itemId;
                            var pointer = items[index].Enabled && !current
                                ? ProcessControlPointer(input.ScopedNames[index], rect, UiButtonActivation.Release) : default;
                            _lastItemId = itemId;
                            interaction = current ? default : ProcessControlKeyboard<ActionControlKeys>(rect, pointer,
                                allowDisabledFocus: parentEnabled && items[index].DisabledFocusable && !items[index].Enabled);
                            if (items[index].Enabled && !current)
                                interaction = ApplyAccessibilityInvoke(itemId, rect, interaction);
                            if (interaction.Activated)
                                invoked = items[index].Value;
                            // Current items retain their regular foreground while remaining passive.
                            if (current)
                                EndDisabled();
                            try
                            {
                                TPresentation.PaintItem(context, layout.Content[index], rect, interaction, layout.Options);
                            }
                            finally { if (current) BeginDisabled(true); }
                            ClaimTooltipAccessibilityTarget(itemId);
                            _accessibility?.Add(new(itemId, listItem, current ? UiAccessibilityRole.Label : UiAccessibilityRole.Button,
                                itemName, visible, parentEnabled && items[index].Enabled, visible.Width <= 0 || visible.Height <= 0,
                                !current && _state.FocusedId == itemId, Focusable: !current, ValuePattern: false)
                            {
                                DisabledFocusable = !current && parentEnabled && !items[index].Enabled && items[index].DisabledFocusable,
                                Tooltip = TooltipAccessibility(itemId)
                            }, rect);
                        }
                        finally { EndDisabled(); }
                    }
                }
                finally
                {
                    var semantics = EndTooltipAccessibility(semantic);
                    if (tooltip.Text.Length > 0)
                    {
                        var available = visible.Width > 0 && visible.Height > 0 && _layoutPointerBlockedTier != _popupTierDepth && _popupTierDepth >= _blockingKeyboardTier;
                        ObserveTooltipTarget<UiHoverFocusTooltipBehavior>(tooltip, tooltip.Text, tooltip.Options, semantics, rect,
                            available, available && !_layoutPointerBlocked && IsHovering(visible), _state.FocusedId == itemId, _tooltipTargetDepth + 1);
                    }
                }
            }
            if (layout.Options.FocusMode == UiBreadcrumbFocusMode.Arrow)
            {
                var keep = -1;
                for (var index = tabStart; index < _currentTabStops.Count; index++)
                    if (_currentTabStops[index].Id == _state.FocusedId)
                    {
                        keep = index;
                        break;
                    }
                if (keep < 0 && _currentTabStops.Count > tabStart)
                    keep = tabStart;
                for (var i = _currentTabStops.Count - 1; i >= tabStart; i--)
                    if (i != keep)
                        _currentTabStops.RemoveAt(i);
            }
            if (overflowVisible)
                invoked = RenderMenuPopup<UiFluentMenuPresentation>(overflowName, layout.Options.OverflowLabel,
                    layout.Overflow, new UiMenuOptions { RightToLeft = layout.Options.RightToLeft }, input.Menu, overflowBounds, overflowInteraction) ?? invoked;
        }
        finally { _accessibilityParent = parent; _accessibilityWidget = ownerWidget; }
        FinishLayoutBox(origin, layout.Size);
        return invoked;
    }

    private void NavigateBreadcrumb(UiBreadcrumbOption[] items, UiBreadcrumbLayout layout, UiBreadcrumbInputState input, string overflowId)
    {
        if (!IsControlEnabled || _popupTierDepth < _blockingKeyboardTier)
            return;
        var current = -1;
        for (var slot = 0; slot < layout.Indices.Length; slot++)
        {
            var index = layout.Indices[slot];
            if ((index < 0 ? overflowId : input.ItemIds[index]) == _state.FocusedId)
            {
                current = slot;
                break;
            }
        }
        if (current < 0)
            return;
        foreach (var key in _keyEvents)
        {
            if (!key.IsDown || (key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
                continue;
            var step = key.Key == UiKey.LeftArrow ? -1 : key.Key == UiKey.RightArrow ? 1 : 0;
            if (layout.Options.RightToLeft)
                step = -step;
            var edge = key.Key is UiKey.Home or UiKey.End;
            if (edge)
            {
                step = key.Key == UiKey.Home ? 1 : -1;
                current = key.Key == UiKey.Home ? -1 : layout.Indices.Length;
            }
            if (step == 0)
                continue;
            for (var attempt = 0; attempt < layout.Indices.Length; attempt++)
            {
                current = (current + step + layout.Indices.Length) % layout.Indices.Length;
                var index = layout.Indices[current];
                if (index >= 0 && (index == items.Length - 1 || !items[index].Enabled && !items[index].DisabledFocusable))
                    continue;
                FocusGroupControl(index < 0 ? overflowId : input.ItemIds[index], true);
                break;
            }
        }
    }
}
