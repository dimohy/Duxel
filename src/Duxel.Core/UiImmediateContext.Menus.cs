namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    internal void PaintFluentMenuShadow(UiRect bounds) => _ui.PaintFluentMenuShadow(bounds);
}

public sealed partial class UiImmediateContext
{
    private static readonly System.Collections.Immutable.ImmutableArray<UiPopupPosition> SubmenuFallbackPositions =
    [
        UiPopupPosition.After, UiPopupPosition.AfterBottom, UiPopupPosition.BeforeTop,
        UiPopupPosition.Before, UiPopupPosition.BeforeBottom
    ];
    private static readonly System.Collections.Immutable.ImmutableArray<UiPopupPosition> SubmenuFullWidthFallbackPositions =
        SubmenuFallbackPositions.Add(UiPopupPosition.Above);
    private const double SubmenuHoverDelay = .5;
    private UiMenuInputState? _activeMenu;
    private int _activeMenuFrame;
    private UiBoxShadow? _fluentMenuShadow;

    internal void PaintFluentMenuShadow(UiRect bounds)
    {
        var options = UiFluentShadow.Resolve(ControlFluentColorScheme, UiShadowElevation.Shadow16);
        var shadow = _fluentMenuShadow ??= new(options);
        shadow.Options = options;
        new UiControlContext(this).BoxShadow(shadow, bounds, 4);
    }

    private void EndMenuFrame()
    {
        if (_activeMenu is not { } active || _activeMenuFrame == GetFrameCount())
            return;
        if (active.Open)
            active.DismissalVersion++;
        CloseMenu(active, restoreFocus: false, dismissal: false);
    }

    internal string? RenderMenu<TBehavior, TPresentation>(string id, string label, UiMenuOption[] items,
        UiMenuOptions options, UiMenuInputState state)
        where TBehavior : IUiMenuBehavior
        where TPresentation : IUiMenuPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.MeasureTrigger(context, label));
        var origin = AllocateLayoutBox(size);
        var trigger = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var button = ProcessMenuTrigger(id, label, items, state, trigger, activation: TBehavior.TriggerActivation);
        TPresentation.PaintTrigger(context, label, trigger, button);
        FinishLayoutBox(origin, size);
        return RenderMenuPopup<TPresentation>(id, label, items, options, state, trigger, button, optionActivation: TBehavior.OptionActivation);
    }

    internal UiButtonInteraction ProcessMenuTrigger(string id, string label, UiMenuOption[] items, UiMenuInputState state,
        UiRect trigger, string? parent = null, bool tab = false, bool publish = true, string? focusOwner = null, bool ownerFocused = false,
        UiAccessibilityRole triggerRole = UiAccessibilityRole.Button, string? triggerValue = null, bool multipleSelection = false,
        bool invalid = false, UiButtonActivation activation = UiButtonActivation.Release)
    {
        var triggerId = ResolveId(id);
        var root = ResolveCachedIdentifier(triggerId, "##menu", false);
        state.Prepare(root, items);
        state.TriggerId = triggerId;
        state.FocusOwnerId = focusOwner;
        if (ownerFocused && _state.FocusedId == focusOwner)
        {
            _state.FocusedId = _ownedControlFocus = triggerId;
            if (_spacePressOwner == focusOwner)
                _spacePressOwner = triggerId;
        }
        var previousBlocking = _blockingKeyboardTier;
        if (state.Open && ReferenceEquals(_activeMenu, state))
            _blockingKeyboardTier = _popupTierDepth;
        UiButtonInteraction button;
        try
        {
            button = ProcessButton(id, trigger, activation);
            button = ApplyAccessibilityInvoke(triggerId, trigger, button);
            if (tab)
            {
                if (_accessibility?.Take(triggerId, UiAccessibilityAction.Select) is { } select)
                {
                    var clip = IntersectRect(CurrentClipRect, trigger);
                    select.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled : clip.Width <= 0 || clip.Height <= 0
                        ? UiAccessibilityResult.Unavailable : UiAccessibilityResult.Success;
                    if (select.Result == UiAccessibilityResult.Success)
                        button = button with
                        {
                            Activated = true
                        };
                }
                if (_accessibility?.Take(triggerId, UiAccessibilityAction.Deselect) is { } deselect)
                    deselect.Result = UiAccessibilityResult.Success;
                if (publish)
                    PublishMenuTabTrigger(id, label, trigger, parent!, button);
            }
            else if (publish)
            {
                ExposeAccessibility(triggerId, _accessibilityParent, triggerRole, label, trigger, triggerValue,
                    readOnly: triggerRole == UiAccessibilityRole.ComboBox, valuePattern: triggerRole == UiAccessibilityRole.ComboBox,
                    invalid: invalid);
                if (triggerRole == UiAccessibilityRole.ComboBox)
                    _accessibility?.SetSelectionPolicy(triggerId, multipleSelection);
                UpdateMenuExpansion(state);
            }
        }
        finally
        {
            _blockingKeyboardTier = previousBlocking;
            if (focusOwner is not null)
            {
                var registered = _currentTabStops.Exists(stop => stop.Id == triggerId);
                _currentTabStops.RemoveAll(stop => stop.Id == triggerId);
                if (registered && !_currentTabStops.Exists(stop => stop.Id == focusOwner))
                    _currentTabStops.Add(new(focusOwner, _popupTierDepth));
                if (_state.FocusedId == triggerId)
                    _state.FocusedId = _ownedControlFocus = focusOwner;
                if (_spacePressOwner == triggerId)
                    _spacePressOwner = focusOwner;
            }
        }
        return button;
    }

    internal void PublishMenuTabTrigger(string id, string label, UiRect trigger, string parent, UiButtonInteraction button, string? focusOwner = null)
    {
        var clipped = IntersectRect(CurrentClipRect, trigger);
        _accessibility?.Add(new(ResolveId(id), parent, UiAccessibilityRole.Tab, label, clipped, IsControlEnabled,
            clipped.Width <= 0 || clipped.Height <= 0, button.Focused, ValuePattern: false)
        {
            Selected = false,
            InvokePattern = true,
            Expansion = _activeMenu is { Open: true } menu && menu.TriggerId == ResolveId(id)
                ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed
        }, trigger);
        if (focusOwner is not null)
            (_accessibilityFocusOwner, _accessibilityFocusTarget) = (focusOwner, ResolveId(id));
    }

    internal bool IsLogicalControlFocused(string id) => _state.FocusedId == ResolveId(id);

    internal void CancelMenuPopup(UiMenuInputState state, string? restoreOwner = null)
    {
        var restore = _state.FocusedId is { } focused && (focused == state.Root || focused == state.TriggerId || focused == state.FocusOwnerId);
        if (state.Open)
            CloseMenu(state, restoreFocus: false, dismissal: false);
        if (restoreOwner is not null && restore)
        {
            _state.FocusedId = _ownedControlFocus = ResolveId(restoreOwner);
            CancelControlSpace();
        }
    }

    internal string? RenderMenuPopup<TPresentation>(string id, string label, UiMenuOption[] items,
        UiMenuOptions options, UiMenuInputState state, UiRect trigger, UiButtonInteraction button,
        IUiOptionSelectionView? selection = null, bool? controlledOpen = null, UiAccessibilityTree.Request? expansionRequest = null,
        UiButtonActivation optionActivation = UiButtonActivation.Release)
        where TPresentation : IUiMenuPresentation
    {
        if (RightToLeft)
            options = options with { RightToLeft = true };
        try
        {
            var context = new UiControlContext(this);
            var root = state.Root ?? throw new InvalidOperationException("Prepare the menu trigger identity before rendering its popup.");

            var first = Array.FindIndex(items, static item => item.Enabled);
            var last = Array.FindLastIndex(items, static item => item.Enabled);
            expansionRequest ??= _accessibility?.Take(state.TriggerId!, UiAccessibilityAction.Expand)
                ?? _accessibility?.Take(state.TriggerId!, UiAccessibilityAction.Collapse);
            var expandRequested = false;
            if (expansionRequest is { } request)
            {
                var clip = IntersectRect(CurrentClipRect, trigger);
                request.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled : clip.Width <= 0 || clip.Height <= 0
                    ? UiAccessibilityResult.Unavailable : request.Action == UiAccessibilityAction.Expand && first < 0 && selection is not { Editable: true }
                    ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
                if (request.Result == UiAccessibilityResult.Success)
                {
                    if (request.Action == UiAccessibilityAction.Collapse)
                    {
                        if (!state.Open || CloseMenu(state, restoreFocus: true))
                            return null;
                        request.Result = UiAccessibilityResult.InvalidOperation;
                    }
                    else
                        expandRequested = true;
                    button = button with
                    {
                        Activated = false
                    };
                }
            }
            if (controlledOpen == false && state.Open && CloseMenu(state, restoreFocus: false))
                return null;
            if (controlledOpen == true && !state.Open)
                expandRequested = true;
            var openKey = false;
            var openLast = false;
            if (button.Focused)
                foreach (var key in _keyEvents)
                    if (key.IsDown && (key.Modifiers == 0 || selection is not null && key.Modifiers == KeyModifiers.Alt) && key.Key is UiKey.DownArrow or UiKey.UpArrow)
                    {
                        openKey = true;
                        openLast = key.Key == UiKey.UpArrow;
                    }
            if (selection is { Editable: false } && button.Focused)
                foreach (var character in _charEvents)
                    if (System.Text.Rune.IsValid(character.CodePoint))
                    {
                        var rune = new System.Text.Rune((int)character.CodePoint);
                        if (!System.Text.Rune.IsControl(rune) && !System.Text.Rune.IsWhiteSpace(rune))
                            openKey = true;
                    }
            if (selection is not null && state.Open)
                foreach (var key in _keyEvents)
                    if (key.IsDown && key.Key == UiKey.UpArrow && key.Modifiers == KeyModifiers.Alt)
                    {
                        if (CloseMenu(state, restoreFocus: true))
                            return null;
                    }
            if (!IsControlEnabled || first < 0 && selection is not { Editable: true })
            {
                if (state.Open)
                    CloseMenu(state, restoreFocus: IsControlEnabled, dismissal: false);
                return null;
            }
            if (button.Activated && state.Open)
            {
                if (CloseMenu(state, restoreFocus: true))
                    return null;
                button = button with
                {
                    Activated = false
                };
            }
            var opened = !state.Open && (button.Activated || openKey || expandRequested);
            if (opened)
            {
                if (state.CanChangeOpen is { } authorize && !authorize(true)
                    || state.Parent is null && _activeMenu is { } previous && !ReferenceEquals(previous, state) && !CloseMenu(previous, restoreFocus: false))
                {
                    if (expansionRequest is { } denied)
                        denied.Result = UiAccessibilityResult.InvalidOperation;
                    return null;
                }
                state.Open = true;
                if (state.Entrance is { } entrance)
                    entrance.StartedAt = _state.TimeSeconds;
                var selected = selection?.FirstSelected() ?? -1;
                state.Focused = selected >= 0 ? selected : openLast ? last : first;
                selection?.ResetSearch();
                if (selection is { Editable: true })
                    selection.Navigated = openKey;
                state.FocusOnOpen = expandRequested || openKey || !button.Hovered;
                state.LastPointer = _mousePosition;
                state.KeyboardPressed = false;
                state.PressedValue = null;
                state.ScrollOffset = 0;
                state.ReturnStops = _previousTabStops.Where(stop => stop.PopupTier == _popupTierDepth).Select(stop => stop.Id).ToArray();
                if (state.Parent is null)
                    _activeMenu = state;
                _requestFrame?.Invoke();
            }
            if (!state.Open)
                return null;
            trigger = WidgetScreenBounds(trigger);
            using var screenSpace = BeginWidgetScreenSpace();
            if (state.Parent is null)
            {
                _activeMenu = state;
                _activeMenuFrame = GetFrameCount();
            }

            var childIndex = state.Child is { Open: true }
                ? Array.FindIndex(items, item => item.Value == state.ChildValue && item.Enabled && item.SubmenuItems is not null)
                : -1;
            if (childIndex < 0 && state.Child is { Open: true } removedChild)
                CloseMenu(removedChild, restoreFocus: OwnsMenuFocus(removedChild));
            var openSubmenu = -1;
            var openFromHover = false;
            var openArrow = options.RightToLeft ? UiKey.LeftArrow : UiKey.RightArrow;
            var closeArrow = options.RightToLeft ? UiKey.RightArrow : UiKey.LeftArrow;

            var hasCheckmarks = false;
            var hasIcons = false;
            var hasSupportingText = false;
            var hasSubmenus = false;
            for (var i = 0; i < state.Groups.Length; i++)
                state.Selection[i] = state.Groups[i].Selection.Value;
            for (var i = 0; i < items.Length; i++)
            {
                hasIcons |= !items[i].Icon.IsEmpty;
                hasSupportingText |= items[i].SecondaryContent is not null || items[i].SubText is not null;
                hasSubmenus |= items[i].SubmenuItems is not null;
                var binding = items[i].Checked;
                state.Checked[i] = null;
                if (state.GroupIndex[i] is var group && group >= 0)
                {
                    hasCheckmarks = true;
                    state.Checked[i] = items[i].Value == state.Selection[group];
                    continue;
                }
                if (binding is null)
                    continue;
                hasCheckmarks = true;
                var previous = -1;
                for (var j = 0; j < i; j++)
                    if (ReferenceEquals(items[j].Checked, binding))
                    {
                        previous = j;
                        break;
                    }
                state.Checked[i] = previous < 0 ? binding.Value : state.Checked[previous];
            }
            if (selection is not null)
            {
                selection.Synchronize();
                hasCheckmarks = true;
                for (var i = 0; i < items.Length; i++)
                    state.Checked[i] = selection.Selected[i];
            }
            var hasContent = hasCheckmarks || hasIcons || hasSupportingText || hasSubmenus;
            UiMenuItemContent Content(int index) => new(items[index].Label, state.Checked[index], hasCheckmarks)
            {
                Icon = items[index].Icon,
                HasIcons = hasIcons,
                SecondaryContent = items[index].SecondaryContent,
                SubText = items[index].SubText,
                HasSubmenu = items[index].SubmenuItems is not null,
                MultipleSelection = selection?.Multiple == true
            };
            var width = options.MinimumWidth;
            var emptyText = items.Length == 0 ? selection?.EmptyText : null;
            if (emptyText is not null)
                width = MathF.Max(width, TPresentation.MeasureItem(context, emptyText) + 2 * options.Padding);
            for (var i = 0; i < items.Length; i++)
                width = MathF.Max(width, (hasContent
                    ? TPresentation.MeasureItem(context, Content(i))
                    : TPresentation.MeasureItem(context, items[i].Label)) + 2 * options.Padding);
            if (options.MaximumWidth is { } maximum)
                width = MathF.Min(width, maximum);
            if (state.Parent is not null || hasSubmenus)
                width = MathF.Min(width, _displaySize.X);
            var widthInsidePadding = MathF.Max(0, width - 2 * options.Padding);
            var heightLimit = MathF.Min(options.MaximumHeight ?? _displaySize.Y, _displaySize.Y);
            double MeasureRows(float rowWidth)
            {
                double offset = 0;
                for (var i = 0; i < items.Length; i++)
                {
                    var itemHeight = TPresentation.MeasureItemHeight(context, Content(i), rowWidth, options);
                    if (!float.IsFinite(itemHeight) || itemHeight <= 0)
                        throw new InvalidOperationException("A menu presentation must measure a finite positive row height.");
                    state.ItemOffsets[i] = offset;
                    state.ItemHeights[i] = itemHeight;
                    offset += itemHeight;
                    if (i + 1 < items.Length)
                        offset += options.ItemSpacing;
                }
                return offset + 2 * options.Padding + (emptyText is null ? 0 : options.ItemHeight);
            }
            var height = MeasureRows(widthInsidePadding);
            var gutter = height > heightLimit ? MathF.Min(ScrollbarSize, widthInsidePadding) : 0;
            if (gutter > 0)
                height = MeasureRows(widthInsidePadding - gutter);
            if (!double.IsFinite(height) || height > float.MaxValue || !float.IsFinite(width))
                throw new InvalidOperationException("Menu geometry exceeds finite coordinate capacity.");
            var surfaceHeight = MathF.Min((float)height, heightLimit);
            var submenuFallbacks = width == _displaySize.X && trigger.Y >= surfaceHeight
                ? SubmenuFullWidthFallbackPositions : SubmenuFallbackPositions;
            var layout = UiPopupPlacement.Arrange(trigger, new(width, surfaceHeight), new(0, 0, _displaySize.X, _displaySize.Y),
                new()
                {
                    Position = state.Parent is null ? UiPopupPosition.BelowStart : UiPopupPosition.AfterTop,
                    FallbackPositions = state.Parent is null ? default : submenuFallbacks,
                    TetherMode = state.Parent is null ? UiPopupTetherMode.Default : UiPopupTetherMode.DisabledAllAxes,
                    RightToLeft = options.RightToLeft
                });
            var bounds = layout.Bounds;
            if (!opened && _leftMousePressed && !Contains(bounds, _mousePosition) && !Contains(trigger, _mousePosition)
                && !ContainsOpenChild(state, _mousePosition))
            {
                if (CloseMenu(state, restoreFocus: false))
                    return null;
            }
            if (!opened)
                foreach (var key in _keyEvents)
                    if (key.IsDown && (key.Key == UiKey.Tab && state.Parent is null
                        || key.Key == UiKey.Escape && childIndex < 0
                        || state.Parent is not null && OwnsMenuFocus(state) && key.Key == closeArrow))
                    {
                        if (!CloseMenu(state, restoreFocus: key.Key != UiKey.Tab))
                            continue;
                        if (key.Key == UiKey.Tab && state.ReturnStops.Length > 0)
                        {
                            var previous = Array.IndexOf(state.ReturnStops, state.FocusOwnerId ?? state.TriggerId);
                            var reverse = (key.Modifiers & KeyModifiers.Shift) != 0;
                            var next = previous < 0 ? 0 : (previous + (reverse ? -1 : 1) + state.ReturnStops.Length) % state.ReturnStops.Length;
                            _state.FocusedId = _ownedControlFocus = state.ReturnStops[next];
                            _state.NavCursorVisible = true;
                            _focusedControlSeen = true;
                        }
                        return null;
                    }

            PushPopup();
            PushClipRect(new(0, 0, _displaySize.X, _displaySize.Y), false);
            var oldParent = _accessibilityParent;
            string? invoked = null;
            var keepOpen = false;
            InteractivePaintCapture? entranceCapture = null;
            var opacity = 1f;
            var paintCompleted = false;
            try
            {
                _state.AddPopupBlockingRect(bounds, _popupTierDepth);
                state.BlockingRect = bounds;
                state.BlockingTier = _popupTierDepth;
                state.BlockingFrame = GetFrameCount();
                _blockingKeyboardTier = Math.Max(_blockingKeyboardTier, _popupTierDepth);
                if (button.Focused)
                {
                    _focusedControlSeen = true;
                    _focusedControlTier = _popupTierDepth;
                }
                if (IsPresentationPass && state.Entrance is { StartedAt: { } started } entrance)
                {
                    var end = started + UiMenuEntrance.DurationSeconds;
                    var now = _state.TimeSeconds;
                    if (now < end)
                    {
                        var remaining = (float)Math.Clamp((end - now) / UiMenuEntrance.DurationSeconds, 0, 1);
                        opacity = 1 - remaining * remaining * remaining;
                        RequestFrameAt(Math.Min(end, now + 1d / 60));
                        var shadow = UiFluentShadow.Resolve(ControlFluentColorScheme, UiShadowElevation.Shadow16);
                        var captureBounds = IntersectRect(shadow.PaintBounds(bounds), CurrentClipRect);
                        if (captureBounds is { Width: > 0, Height: > 0 })
                            entranceCapture = BeginInteractivePaintGroup(entrance.Paint ??= new(), captureBounds);
                    }
                    else
                        entrance.Reset();
                }
                TPresentation.PaintSurface(context, bounds);
                var contentHeight = (float)height - 2 * options.Padding;
                var viewportHeight = MathF.Max(0, bounds.Height - 2 * options.Padding);
                var maximumScroll = MathF.Max(0, contentHeight - viewportHeight);
                state.ScrollOffset = Math.Clamp(state.ScrollOffset, 0, maximumScroll);
                var viewport = new UiRect(bounds.X + options.Padding + (options.RightToLeft ? gutter : 0), bounds.Y + options.Padding,
                    MathF.Max(0, bounds.Width - 2 * options.Padding - gutter), viewportHeight);
                var contentSize = new UiVector2(viewport.Width, contentHeight);
                var viewportSize = new UiVector2(viewport.Width, viewport.Height);
                if (_accessibility is { } scrollTree)
                {
                    var offset = scrollTree.ApplyScrollIntoView(root, new(0, state.ScrollOffset), new(0, maximumScroll));
                    ApplyAccessibilityScrollRequest(scrollTree, root, viewportSize, IntersectRect(CurrentClipRect, viewport),
                        contentSize, options.ItemHeight * 3, IsControlEnabled, options.RightToLeft, ref offset);
                    state.ScrollOffset = offset.Y;
                }
                state.ScrollOwner.Bind(this, root);
                var track = new UiRect(options.RightToLeft ? bounds.X + options.Padding : viewport.X + viewport.Width,
                    viewport.Y, gutter, viewport.Height);
                if (maximumScroll > 0)
                {
                    if (IsScrollHoverable(bounds, CurrentClipRect))
                        state.ScrollOffset = ApplyScrollWheel(state.ScrollOffset, maximumScroll, options.ItemHeight * 3, ref _mouseWheel);
                    state.ScrollOwner.UseAxis(horizontal: false);
                }
                else
                    state.ScrollOwner.ReleaseAxis(horizontal: false);
                if ((uint)state.Focused >= (uint)items.Length || !items[state.Focused].Enabled)
                {
                    state.Focused = first;
                    state.RevealFocused = true;
                }
                if (opened || _leftMousePressed && button.Hovered)
                {
                    _state.FocusedId = root;
                    _ownedControlFocus = root;
                    _state.NavCursorVisible = state.FocusOnOpen;
                }
                void Reveal(int index)
                {
                    if ((uint)index >= (uint)items.Length)
                        return;
                    var top = (float)state.ItemOffsets[index];
                    state.ScrollOffset = Math.Clamp(MathF.Min(top, MathF.Max(state.ScrollOffset, top + state.ItemHeights[index] - viewport.Height)), 0, maximumScroll);
                }
                if (opened && state.FocusOnOpen || state.RevealFocused)
                    Reveal(state.Focused);
                state.RevealFocused = false;
                var hit = -1;
                var held = false;
                UiButtonInteraction interaction;
                UiRect Row(int i) => new(viewport.X, (float)(viewport.Y + state.ItemOffsets[i] - state.ScrollOffset),
                    viewport.Width, state.ItemHeights[i]);
                using (context.Clip(viewport))
                {
                    if (!ContainsOpenChild(state, _mousePosition))
                        for (var i = 0; i < items.Length; i++)
                            if (items[i].Enabled && IsHovering(Row(i)))
                                hit = i;
                    var pressed = ButtonBehavior(id + "##menu", hit < 0 ? default : Row(hit), out var hovered, out held,
                        optionActivation);
                    interaction = state.Parent is null
                        ? ProcessControlKeyboard<RangeControlKeys>(bounds, new(pressed, hovered, held))
                        : ProcessControlKeyboard<RangeControlKeys>(root, bounds, new(pressed, hovered, held));
                    if (_leftMousePressed)
                    {
                        state.PressedValue = hit >= 0 ? items[hit].Value : null;
                        if (hit >= 0)
                            state.Focused = hit;
                    }
                    if (interaction.Activated && hit >= 0 && state.PressedValue == items[hit].Value)
                        invoked = items[hit].Value;
                    if (interaction.Hovered && hit >= 0 && !opened && state.Focused != hit)
                    {
                        state.Focused = hit;
                        state.KeyboardPressed = false;
                    }
                    if (interaction.Focused && !opened && first >= 0)
                        foreach (var key in _keyEvents)
                        {
                            if ((key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
                                continue;
                            if (key.Key == UiKey.Space)
                            {
                                if (selection is { Editable: true, Navigated: false })
                                    continue;
                                if (selection?.HasSearch(GetTime()) == true)
                                {
                                    state.KeyboardPressed = false;
                                    continue;
                                }
                                if (key.IsDown)
                                    state.KeyboardPressed = true;
                                else
                                {
                                    if (state.KeyboardPressed)
                                        invoked = items[state.Focused].Value;
                                    state.KeyboardPressed = false;
                                }
                                continue;
                            }
                            if (!key.IsDown)
                                continue;
                            if (key.Key == openArrow && items[state.Focused].SubmenuItems is not null)
                            {
                                openSubmenu = state.Focused;
                                continue;
                            }
                            if (key.Key == UiKey.Enter && selection is not { Editable: true, Navigated: false })
                                invoked = items[state.Focused].Value;
                            var next = state.Focused;
                            if (key.Key is UiKey.Home or UiKey.End or UiKey.UpArrow or UiKey.DownArrow or UiKey.PageUp or UiKey.PageDown)
                            {
                                selection?.ResetSearch();
                                if (selection is { Editable: true })
                                    selection.Navigated = true;
                            }
                            if (key.Key == UiKey.Home)
                                next = first;
                            else if (key.Key == UiKey.End)
                                next = last;
                            else if (key.Key is UiKey.UpArrow or UiKey.DownArrow)
                            {
                                var direction = key.Key == UiKey.UpArrow ? -1 : 1;
                                if (selection is null)
                                    do
                                    {
                                        next = (next + direction + items.Length) % items.Length;
                                    } while (!items[next].Enabled);
                                else
                                {
                                    var candidate = next + direction;
                                    while (candidate >= 0 && candidate < items.Length)
                                    {
                                        if (items[candidate].Enabled)
                                        {
                                            next = candidate;
                                            break;
                                        }
                                        candidate += direction;
                                    }
                                }
                            }
                            if (selection is not null && key.Key is UiKey.PageUp or UiKey.PageDown)
                            {
                                var direction = key.Key == UiKey.PageUp ? -1 : 1;
                                var steps = Math.Max(1, (int)(viewport.Height / (options.ItemHeight + options.ItemSpacing)));
                                for (var step = 0; step < steps; step++)
                                {
                                    var candidate = next + direction;
                                    while (candidate >= 0 && candidate < items.Length && !items[candidate].Enabled)
                                        candidate += direction;
                                    if (candidate < 0 || candidate >= items.Length)
                                        break;
                                    next = candidate;
                                }
                            }
                            if (next != state.Focused)
                            {
                                state.Focused = next;
                                state.KeyboardPressed = false;
                                Reveal(next);
                                _requestFrame?.Invoke();
                            }
                        }
                    if (selection is not { Editable: true } && interaction.Focused && (!opened || selection is not null) && (_state.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) == 0)
                        foreach (var character in _charEvents)
                        {
                            if (!System.Text.Rune.IsValid(character.CodePoint))
                                continue;
                            var rune = new System.Text.Rune((int)character.CodePoint);
                            if (System.Text.Rune.IsControl(rune) || System.Text.Rune.IsWhiteSpace(rune) && selection?.HasSearch(GetTime()) != true)
                                continue;
                            var prefix = rune.ToString();
                            if (selection is not null)
                            {
                                var next = selection.Search(prefix, GetTime(), state.Focused);
                                if (next != state.Focused)
                                {
                                    state.Focused = next;
                                    state.KeyboardPressed = false;
                                    state.PressedValue = null;
                                    Reveal(next);
                                    _state.NavCursorVisible = true;
                                    interaction = interaction with
                                    {
                                        FocusVisible = true
                                    };
                                    _requestFrame?.Invoke();
                                }
                                continue;
                            }
                            for (var step = 1; step <= items.Length; step++)
                            {
                                var next = (state.Focused + step) % items.Length;
                                if (!items[next].Enabled || !items[next].Label.AsSpan().TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                state.Focused = next;
                                state.KeyboardPressed = false;
                                state.PressedValue = null;
                                Reveal(next);
                                _state.NavCursorVisible = true;
                                interaction = interaction with
                                {
                                    FocusVisible = true
                                };
                                _requestFrame?.Invoke();
                                break;
                            }
                        }
                    for (var i = 0; i < items.Length; i++)
                    {
                        if (_accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Focus) is not { } focus)
                            continue;
                        BeginDisabled(!items[i].Enabled);
                        try
                        {
                            ApplyAccessibilityFocus(root, Row(i), focus);
                        }
                        finally { EndDisabled(); }
                        if (focus.Result == UiAccessibilityResult.Success)
                        {
                            state.Focused = i;
                            state.KeyboardPressed = false;
                            interaction = interaction with
                            {
                                Focused = true,
                                FocusVisible = true
                            };
                            _focusedControlSeen = true;
                            _focusedControlTier = _popupTierDepth;
                        }
                    }
                }
                if (maximumScroll > 0)
                    state.ScrollOffset = RenderScrollbarV(state.ScrollOwner.VerticalId, track, state.ScrollOffset,
                        maximumScroll, contentHeight, CurrentClipRect);
                var scrollRange = new UiAccessibilityScrollInfo(viewportSize, contentSize, new(0, state.ScrollOffset))
                {
                    RightToLeft = options.RightToLeft
                };
                ExposeAccessibility(root, oldParent, selection is null ? UiAccessibilityRole.Menu : UiAccessibilityRole.ListBox,
                    label, bounds, focusable: false, valuePattern: false,
                    scroll: CanPublishScroll(IntersectRect(CurrentClipRect, viewport), scrollRange) ? scrollRange : null);
                if (selection is not null)
                    _accessibility?.SetSelectionPolicy(root, selection.Multiple);
                state.PublishedFrame = GetFrameCount();
                state.PublishedRoot = root;
                _accessibility?.CaptureScrollViewport(root, viewport, CurrentClipRect, scrollRange, independentRoot: true);
                UiRect focusedBounds = default;
                UiButtonInteraction focusedVisual = default;
                var paintFocus = false;
                using (context.Clip(viewport))
                {
                    if (emptyText is not null)
                    {
                        var emptyBounds = new UiRect(viewport.X, viewport.Y, viewport.Width, options.ItemHeight);
                        TPresentation.PaintItem(context, emptyText, emptyBounds, default, options);
                        ExposeAccessibility(ResolveCachedIdentifier(root, "##empty", false), root, UiAccessibilityRole.Label,
                            emptyText, emptyBounds, focusable: false, valuePattern: false);
                    }
                    // Resolve all activation before painting/publishing so shared bindings agree in this frame.
                    for (var i = 0; i < items.Length; i++)
                    {
                        if (invoked is not null)
                            break;
                        BeginDisabled(!items[i].Enabled);
                        try
                        {
                            if (items[i].SubmenuItems is { } children
                                && (_accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Expand)
                                    ?? _accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Collapse)) is { } branchRequest)
                            {
                                var clip = IntersectRect(CurrentClipRect, Row(i));
                                branchRequest.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                                    : clip.Width <= 0 || clip.Height <= 0 ? UiAccessibilityResult.Unavailable
                                    : branchRequest.Action == UiAccessibilityAction.Expand && !HasEnabledMenuItem(children)
                                        ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
                                if (branchRequest.Result == UiAccessibilityResult.Success)
                                {
                                    if (branchRequest.Action == UiAccessibilityAction.Expand)
                                        openSubmenu = i;
                                    else if (childIndex == i)
                                    {
                                        CloseMenu(state.Child!, restoreFocus: OwnsMenuFocus(state.Child!));
                                        childIndex = -1;
                                    }
                                }
                            }
                            if (ApplyAccessibilityInvoke(state.ItemIds[i], Row(i), default).Activated)
                                invoked = items[i].Value;
                            else if (_accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Toggle) is { } toggle)
                            {
                                var clip = IntersectRect(CurrentClipRect, Row(i));
                                toggle.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                                    : clip.Width <= 0 || clip.Height <= 0 ? UiAccessibilityResult.Unavailable
                                    : items[i].Checked is null ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
                                if (toggle.Result == UiAccessibilityResult.Success)
                                    invoked = items[i].Value;
                            }
                            else if ((_accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Select)
                                ?? _accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Deselect)) is { } selectionRequest)
                            {
                                var clip = IntersectRect(CurrentClipRect, Row(i));
                                selectionRequest.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                                    : clip.Width <= 0 || clip.Height <= 0 ? UiAccessibilityResult.Unavailable
                                    : items[i].RadioGroup is null || selectionRequest.Action == UiAccessibilityAction.Deselect && state.Checked[i] == true
                                        ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
                                if (selectionRequest.Result == UiAccessibilityResult.Success && selectionRequest.Action == UiAccessibilityAction.Select)
                                    invoked = items[i].Value;
                            }
                        }
                        finally { EndDisabled(); }
                    }
                    if (invoked is not null)
                        for (var i = 0; i < items.Length; i++)
                        {
                            if (items[i].Value != invoked)
                                continue;
                            if (items[i].SubmenuItems is not null)
                            {
                                if (childIndex == i && state.Child is { Open: true } currentChild)
                                {
                                    CloseMenu(currentChild, restoreFocus: OwnsMenuFocus(currentChild));
                                    childIndex = -1;
                                }
                                else
                                    openSubmenu = i;
                                invoked = null;
                            }
                            else if (selection is not null)
                            {
                                selection.Activate(i);
                                for (var j = 0; j < items.Length; j++)
                                    state.Checked[j] = selection.Selected[j];
                                keepOpen = selection.Multiple;
                            }
                            else if (items[i].Checked is { } binding)
                            {
                                var next = !state.Checked[i]!.Value;
                                binding.Value = next;
                                for (var j = 0; j < items.Length; j++)
                                    if (ReferenceEquals(items[j].Checked, binding))
                                        state.Checked[j] = next;
                                keepOpen = true;
                            }
                            else if (items[i].RadioGroup is { } radio)
                            {
                                if (state.Selection[state.GroupIndex[i]] != items[i].Value)
                                    radio.Selection.Value = items[i].Value;
                                state.Selection[state.GroupIndex[i]] = items[i].Value;
                                for (var j = 0; j < items.Length; j++)
                                    if (ReferenceEquals(items[j].RadioGroup, radio))
                                        state.Checked[j] = items[j].Value == items[i].Value;
                            }
                            state.KeyboardPressed = false;
                            _requestFrame?.Invoke();
                            break;
                        }
                    if (hasSubmenus && invoked is null && openSubmenu < 0 && !_leftMouseDown && _keyEvents.Count == 0
                        && (options.SubmenuPointerProtectionSeconds is null || !_leftMousePressed && !_leftMouseReleased))
                    {
                        var branch = childIndex >= 0 ? IntersectRect(viewport, Row(childIndex)) : default;
                        openSubmenu = UpdateSubmenuHover(state, items, hit, childIndex, branch,
                            options.SubmenuPointerProtectionSeconds, out var closeHoveredChild, out var protecting);
                        openFromHover = openSubmenu >= 0;
                        if (protecting)
                        {
                            state.Focused = childIndex;
                            hit = -1;
                        }
                        if (closeHoveredChild && state.Child is { Open: true } hoveredChild)
                        {
                            CloseMenu(hoveredChild, restoreFocus: OwnsMenuFocus(hoveredChild));
                            childIndex = -1;
                        }
                    }
                    else
                    {
                        state.LastPointer = _mousePosition;
                        state.HoverDeadline = null;
                        ResetSubmenuPointerProtection(state);
                    }
                    if (openSubmenu >= 0 && items[openSubmenu].SubmenuItems is { } nextItems && HasEnabledMenuItem(nextItems))
                    {
                        state.Focused = openSubmenu;
                        if (openSubmenu != childIndex)
                        {
                            if (state.Child is { Open: true } previousChild)
                                CloseMenu(previousChild, restoreFocus: false);
                            state.Child ??= new() { Parent = state };
                            state.ChildValue = items[openSubmenu].Value;
                            childIndex = openSubmenu;
                            ResetSubmenuPointerProtection(state);
                        }
                    }
                    if (childIndex >= 0 && state.Child is { } childState)
                    {
                        var anchor = IntersectRect(viewport, Row(childIndex));
                        if (anchor.Width <= 0 || anchor.Height <= 0)
                        {
                            CloseMenu(childState, restoreFocus: OwnsMenuFocus(childState));
                            childIndex = -1;
                        }
                    }
                    for (var i = 0; i < items.Length; i++)
                    {
                        var row = Row(i);
                        var itemId = state.ItemIds[i];
                        var group = state.GroupIndex[i];
                        var parentId = group < 0 ? root : state.GroupIds[group];
                        if (group >= 0 && i == state.GroupStarts[group])
                        {
                            var lastRow = Row(state.GroupEnds[group]);
                            var groupBounds = new UiRect(row.X, row.Y, row.Width, lastRow.Y + lastRow.Height - row.Y);
                            var groupClip = IntersectRect(CurrentClipRect, groupBounds);
                            var required = false;
                            for (var j = i; j <= state.GroupEnds[group]; j++)
                                required |= state.Checked[j] == true;
                            _accessibility?.Add(new(parentId, root, UiAccessibilityRole.MenuGroup, state.Groups[group].Name, groupClip,
                                IsControlEnabled, groupClip.Width <= 0 || groupClip.Height <= 0, false, Focusable: false, ValuePattern: false)
                            {
                                SelectionRequired = required
                            });
                        }
                        BeginDisabled(!items[i].Enabled);
                        try
                        {
                            var visual = new UiButtonInteraction(false, hit == i || childIndex == i, held && state.PressedValue == items[i].Value || state.KeyboardPressed && state.Focused == i)
                            {
                                Focused = interaction.Focused && state.Focused == i && childIndex < 0,
                                FocusVisible = interaction.FocusVisible && state.Focused == i && childIndex < 0
                            };
                            if (hasContent)
                                TPresentation.PaintItem(context, Content(i), row, visual, options);
                            else
                                TPresentation.PaintItem(context, items[i].Label, row, visual, options);
                            var clip = IntersectRect(CurrentClipRect, row);
                            if (IsControlEnabled && visual.FocusVisible && clip.Width > 0 && clip.Height > 0)
                            {
                                focusedBounds = row;
                                focusedVisual = visual;
                                paintFocus = true;
                            }
                            var name = state.SubTextNames?[i] ?? items[i].Label;
                            _accessibility?.Add(new(itemId, parentId, selection is null ? UiAccessibilityRole.MenuItem : UiAccessibilityRole.Option, name, clip,
                                IsControlEnabled, clip.Width <= 0 || clip.Height <= 0, visual.Focused, Hint: items[i].SecondaryContent, ValuePattern: false)
                            {
                                Checked = items[i].Checked is null ? null : state.Checked[i],
                                Selected = selection is not null ? state.Checked[i] : group < 0 ? null : state.Checked[i],
                                Expansion = items[i].SubmenuItems is null ? null
                                    : childIndex == i ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed
                            }, row);
                            if (visual.Focused)
                                (_accessibilityFocusOwner, _accessibilityFocusTarget) = (root, itemId);
                        }
                        finally { EndDisabled(); }
                    }
                }
                if (paintFocus)
                    TPresentation.PaintFocus(context, focusedBounds, focusedVisual, options);
                if (childIndex >= 0 && state.Child is { } activeChild && invoked is null)
                {
                    var item = items[childIndex];
                    var itemId = state.ItemIds[childIndex];
                    activeChild.Prepare(ResolveCachedIdentifier(itemId, "##menu", false), item.SubmenuItems!);
                    activeChild.TriggerId = itemId;
                    _accessibilityParent = itemId;
                    invoked = RenderMenuPopup<TPresentation>(itemId, state.SubTextNames?[childIndex] ?? item.Label, item.SubmenuItems!, options, activeChild,
                        IntersectRect(viewport, Row(childIndex)), new(!activeChild.Open, openFromHover, false),
                        optionActivation: optionActivation);
                    keepOpen = activeChild.Open;
                }
                if (!_leftMouseDown)
                    state.PressedValue = null;
                paintCompleted = true;
            }
            finally
            {
                try
                {
                    if (entranceCapture is { } capture)
                        EndInteractivePaintGroup(capture, paintCompleted, opacity);
                }
                finally
                {
                    _accessibilityParent = oldParent;
                    PopClipRect();
                    PopPopup();
                }
            }
            if (invoked is not null && !keepOpen && CloseMenu(state, restoreFocus: true))
                _accessibility?.MarkDismissingMenu(root);
            return invoked;
        }
        finally { UpdateMenuExpansion(state); }
    }

    private bool CloseMenu(UiMenuInputState state, bool restoreFocus, bool dismissal = true)
    {
        if (state.Open && dismissal && state.CanChangeOpen is { } authorize && !authorize(false))
            return false;
        if (state.Child is { Open: true } child)
            CloseMenu(child, restoreFocus: false, dismissal: dismissal);
        state.Child = null;
        state.ChildValue = null;
        if (state.Open && dismissal)
            state.DismissalVersion++;
        state.Open = false;
        state.Entrance?.Reset();
        state.HoverDeadline = null;
        ResetSubmenuPointerProtection(state);
        UpdateMenuExpansion(state);
        state.PressedValue = null;
        state.KeyboardPressed = false;
        state.ScrollOwner.Release();
        if (state.BlockingFrame == GetFrameCount())
        {
            _state.RemoveCurrentPopupBlockingRect(state.BlockingRect, state.BlockingTier);
            state.BlockingFrame = -1;
        }
        if (ReferenceEquals(_activeMenu, state))
            _activeMenu = null;
        _currentTabStops.RemoveAll(stop => stop.Id == state.Root);
        if (_state.FocusedId == state.Root || restoreFocus)
        {
            _state.FocusedId = restoreFocus ? state.Parent?.Root ?? state.FocusOwnerId ?? state.TriggerId : null;
            _ownedControlFocus = _state.FocusedId;
            _focusedControlSeen = restoreFocus;
            _focusedControlTier = _popupTierDepth;
        }
        _blockingKeyboardTier = _popupTierDepth;
        _requestFrame?.Invoke();
        return true;
    }

    private static bool Contains(UiRect rect, UiVector2 point) => point.X >= rect.X && point.X < rect.X + rect.Width
        && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;

    private static bool HasEnabledMenuItem(UiMenuOption[] items) => Array.Exists(items, static item => item.Enabled);

    private int UpdateSubmenuHover(UiMenuInputState state, UiMenuOption[] items, int hit, int childIndex,
        UiRect branch, double? protection, out bool close, out bool protecting)
    {
        close = protecting = false;
        var moved = state.LastPointer != _mousePosition;
        state.LastPointer = _mousePosition;
        if (childIndex >= 0 && (hit == childIndex || ContainsOpenChild(state, _mousePosition)))
        {
            state.HoverDeadline = null;
            ResetSubmenuPointerProtection(state);
            if (protection.HasValue && hit == childIndex)
                state.SubmenuPointerOrigin = _mousePosition;
            return -1;
        }
        var target = hit >= 0 && items[hit].SubmenuItems is not null ? items[hit].Value : null;
        if (childIndex >= 0 && protection is { } timeout && state.SubmenuPointerOrigin is { } origin
            && ContainsSubmenuPointerApproach(branch, state.Child!.BlockingRect, origin, _mousePosition))
        {
            state.HoverDeadline = null;
            // Continue protecting a moving pointer without delaying a stationary one indefinitely.
            if (moved || state.SubmenuPointerDeadline is null)
                state.SubmenuPointerDeadline = GetTime() + timeout;
            if (GetTime() < state.SubmenuPointerDeadline.Value)
            {
                protecting = true;
                RequestFrameAt(state.SubmenuPointerDeadline.Value);
                return -1;
            }
            ResetSubmenuPointerProtection(state);
            close = target is null || !HasEnabledMenuItem(items[hit].SubmenuItems!);
            return close ? -1 : hit;
        }
        ResetSubmenuPointerProtection(state);
        if (moved && (target is not null || childIndex >= 0)
            && (state.HoverDeadline is null || state.HoverBranch != target))
        {
            state.HoverBranch = target;
            state.HoverDeadline = GetTime() + SubmenuHoverDelay;
        }
        if (state.HoverDeadline is not { } deadline)
            return -1;
        if (state.HoverBranch != target)
        {
            state.HoverDeadline = null;
            return -1;
        }
        if (GetTime() < deadline)
        {
            RequestFrameAt(deadline);
            return -1;
        }
        state.HoverDeadline = null;
        close = target is null;
        return target is null ? -1 : hit;
    }

    private static void ResetSubmenuPointerProtection(UiMenuInputState state)
    {
        state.SubmenuPointerOrigin = null;
        state.SubmenuPointerDeadline = null;
    }

    private static bool ContainsSubmenuPointerApproach(UiRect branch, UiRect child, UiVector2 origin, UiVector2 pointer)
    {
        if (branch.Width <= 0 || branch.Height <= 0 || child.Width <= 0 || child.Height <= 0
            || !Contains(branch, origin))
            return false;
        Span<UiVector2> corners =
        [
            new(child.X, child.Y), new(child.X + child.Width, child.Y),
            new(child.X + child.Width, child.Y + child.Height), new(child.X, child.Y + child.Height)
        ];
        static float Cross(UiVector2 from, UiVector2 to, UiVector2 point) =>
            (to.X - from.X) * (point.Y - from.Y) - (to.Y - from.Y) * (point.X - from.X);

        // A corner approach can see two adjacent edges. Their outer vertices
        // and the pointer's last position on the row bound the approach area.
        for (var i = 0; i < corners.Length; i++)
        {
            var first = corners[i];
            var end = corners[(i + 1) % corners.Length];
            if (Cross(first, end, origin) >= 0 || Cross(corners[(i + 3) % corners.Length], first, origin) < 0)
                continue;
            if (Cross(origin, first, pointer) > 0 || Cross(first, end, pointer) > 0)
                return false;
            var next = corners[(i + 2) % corners.Length];
            if (Cross(end, next, origin) < 0)
            {
                if (Cross(end, next, pointer) > 0)
                    return false;
                end = next;
            }
            return Cross(end, origin, pointer) <= 0;
        }
        return false;
    }

    private static bool ContainsOpenChild(UiMenuInputState state, UiVector2 pointer)
    {
        for (var child = state.Child; child is { Open: true }; child = child.Child)
            if (Contains(child.BlockingRect, pointer))
                return true;
        return false;
    }

    private bool OwnsMenuFocus(UiMenuInputState state)
    {
        for (UiMenuInputState? menu = state; menu is { Open: true }; menu = menu.Child)
            if (menu.Root == _state.FocusedId)
                return true;
        return false;
    }

    private void UpdateMenuExpansion(UiMenuInputState state)
    {
        if (state.TriggerId is { } id)
            _accessibility?.SetExpansion(id, state.Open ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed);
    }
}
