namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderDropdown<TPresentation>(string id, string label, UiOptionSelection selection,
        UiMenuInputState state, UiDropdownOptions options, Func<string> countText, bool commitClosedSelection = false) where TPresentation : IUiDropdownPresentation
    {
        var context = new UiControlContext(this);
        options = options.WithInheritedDirection(RightToLeft);
        var size = context.ConstrainToLayout(TPresentation.Measure(context, options));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        if (_currentField is { Invalid: true })
            options = options with
            {
                Invalid = true
            };
        ClaimAccessibilityField(root);
        ClaimFieldFocus(root, bounds);
        var parent = _accessibilityParent;
        var widget = _accessibilityWidget;
        using var clip = context.Clip(bounds);
        var interaction = ProcessMenuTrigger(id, label, selection.MenuItems, state, bounds,
            triggerRole: UiAccessibilityRole.ComboBox, triggerValue: selection.Text, multipleSelection: selection.Multiple,
            invalid: options.Invalid);
        var expandId = ResolveCachedIdentifier(root, "##expand", true);
        var expand = TPresentation.ExpandBounds(bounds, options);
        try
        {
            _accessibilityWidget = null;
            var available = IsControlEnabled && (_popupTierDepth >= _blockingKeyboardTier || ReferenceEquals(_activeMenu, state));
            if (_accessibility?.Take(expandId, UiAccessibilityAction.Invoke) is { } invoke)
            {
                var visible = IntersectRect(CurrentClipRect, expand);
                invoke.Result = !available ? UiAccessibilityResult.Disabled : visible.Width <= 0 || visible.Height <= 0
                    ? UiAccessibilityResult.Unavailable : UiAccessibilityResult.Success;
                if (invoke.Result == UiAccessibilityResult.Success)
                    interaction = interaction with
                    {
                        Activated = true
                    };
            }
            ProcessOptionSelectionRequests(selection, state, available);
            var visibleRoot = IntersectRect(CurrentClipRect, bounds);
            if (visibleRoot.Width <= 0 || visibleRoot.Height <= 0)
                CancelMenuPopup(state);
            if (commitClosedSelection && !state.Open && interaction.Focused && available
                && visibleRoot.Width > 0 && visibleRoot.Height > 0 && ProcessClosedSelectNavigation(selection, options))
                interaction = interaction with { Focused = false, Activated = false };
            _accessibilityParent = root;
            RenderMenuPopup<TPresentation>(id, label, selection.MenuItems,
                new UiMenuOptions(MathF.Max(1, bounds.Width), 32, 4, options.RightToLeft)
                {
                    MaximumHeight = options.MaximumPopupHeight ?? MathF.Max(40, _displaySize.Y * .8f)
                }, state, bounds, interaction, selection);
            if (state.PublishedFrame != GetFrameCount() || state.PublishedRoot != state.Root)
                PublishClosedOptionList(selection, state, root, label, available);
            _accessibility?.SetSelectionPolicy(root, selection.Multiple, selection.Required);
            _accessibility?.SetSelectionPolicy(state.Root!, selection.Multiple, selection.Required);
            _accessibility?.SetControlValue(root, selection.Text);
            _accessibility?.SetControlledElement(root, state.Root!);
            var expandClip = IntersectRect(CurrentClipRect, expand);
            _accessibility?.Add(new(expandId, root, UiAccessibilityRole.Button, options.ExpandLabel, expandClip,
                available, expandClip.Width <= 0 || expandClip.Height <= 0, false, Focusable: false, ValuePattern: false), expand);
            var text = selection.Count == 0 ? options.Placeholder ?? string.Empty : selection.Text;
            TPresentation.Paint(context with { MotionKey = root }, bounds, text, selection.Count == 0, options,
                interaction with
                {
                    Focused = _state.FocusedId == root
                }, state.Open, selection.Multiple ? countText() : string.Empty);
        }
        finally
        {
            _accessibilityWidget = widget;
            _accessibilityParent = parent;
            FinishLayoutBox(origin, size);
        }
    }
    private bool ProcessOptionSelectionRequests(UiOptionSelection selection, UiMenuInputState state, bool available)
    {
        var handled = false;
        for (var i = 0; i < state.ItemIds.Length; i++)
        {
            var request = _accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Select)
                ?? _accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.AddToSelection)
                ?? _accessibility?.Take(state.ItemIds[i], UiAccessibilityAction.Deselect);
            if (request is null)
                continue;
            request.Result = !available || !selection.Options[i].Enabled ? UiAccessibilityResult.Disabled
                : request.Action == UiAccessibilityAction.Deselect && selection.Required && selection.Selected[i] && selection.Count == 1
                    ? UiAccessibilityResult.InvalidOperation
                : request.Action == UiAccessibilityAction.AddToSelection && !selection.Multiple
                    && selection.Count > 0 && !selection.Selected[i] ? UiAccessibilityResult.InvalidOperation
                    : UiAccessibilityResult.Success;
            if (request.Result != UiAccessibilityResult.Success)
                continue;
            handled = true;
            if (request.Action == UiAccessibilityAction.Deselect)
                selection.Deselect(i);
            else
                selection.Select(i, request.Action == UiAccessibilityAction.AddToSelection);
            if (!selection.Multiple && request.Action == UiAccessibilityAction.Select && state.Open)
                CloseMenu(state, restoreFocus: true);
            _requestFrame?.Invoke();
        }
        return handled;
    }
    private void PublishClosedOptionList(UiOptionSelection selection, UiMenuInputState state, string root, string label, bool available)
    {
        if (_accessibility is not { } tree)
            return;
        tree.Add(new(state.Root!, root, UiAccessibilityRole.ListBox, label, default, available, true, false,
            Focusable: false, ValuePattern: false)
        {
            CanSelectMultiple = selection.Multiple
        });
        for (var i = 0; i < state.ItemIds.Length; i++)
            tree.Add(new(state.ItemIds[i], state.Root!, UiAccessibilityRole.Option, selection.Options[i].Text, default,
                available && selection.Options[i].Enabled, true, false, Hint: selection.Options[i].Description,
                Focusable: false, ValuePattern: false)
            {
                Selected = selection.Selected[i]
            });
    }
}
