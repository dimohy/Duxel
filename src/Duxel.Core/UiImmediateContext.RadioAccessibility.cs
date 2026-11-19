namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiRadioGroupInteraction ApplyRadioAccessibility<TPresentation>(string id, string label, UiRect bounds,
        UiRadioGroupContent<TPresentation> content, UiRadioGroupInteraction interaction)
        where TPresentation : IUiRadioGroupPresentation
        => ApplyRadioAccessibility(id, label, bounds, content.Items, content.Layouts, content.Input, interaction);

    private UiRadioGroupInteraction ApplyRadioAccessibility(string id, string label, UiRect bounds,
        ReadOnlySpan<UiRadioOption> items, ReadOnlySpan<UiRadioOptionLayout> layouts,
        UiRadioGroupInputState state, UiRadioGroupInteraction interaction, bool selectionRequired = true,
        UiBinding<string?>? valueBinding = null, Action? beforePublication = null)
    {
        if (valueBinding is not null)
        {
            foreach (var change in state.Changes) valueBinding.Value = change;
            state.Reconcile(valueBinding.Value);
        }
        if (_accessibility is not { } tree)
        {
            beforePublication?.Invoke();
            return interaction;
        }
        var root = ResolveId(id);
        ClaimAccessibilityField(root);
        for (var i = 0; i < items.Length; i++)
        {
            var itemId = state.AccessibilityId(root, i, items);
            var local = layouts[i].Bounds;
            var rect = local with
            {
                X = bounds.X + local.X,
                Y = bounds.Y + local.Y
            };
            var clip = IntersectRect(CurrentClipRect, rect);
            var available = RadioAccessibilityAvailability(items[i].Enabled, clip);
            if (tree.Take(itemId, UiAccessibilityAction.Select) is { } selection)
            {
                selection.Result = available;
                if (available == UiAccessibilityResult.Success)
                {
                    if (valueBinding is not null) valueBinding.Value = items[i].Value;
                    if (valueBinding is null || valueBinding.Value == items[i].Value) state.Select(items[i].Value);
                    else selection.Result = UiAccessibilityResult.InvalidOperation;
                }
            }
            if (tree.Take(itemId, UiAccessibilityAction.Deselect) is { } deselection)
                deselection.Result = available != UiAccessibilityResult.Success ? available
                    : state.Value == items[i].Value ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
            if (tree.Take(itemId, UiAccessibilityAction.Focus) is { } focus)
            {
                focus.Result = available;
                if (available != UiAccessibilityResult.Success)
                    continue;
                ApplyAccessibilityFocus(root, clip, focus);
                state.AccessibilityFocusedValue = items[i].Value;
                interaction = interaction with
                {
                    FocusedIndex = i,
                    Control = interaction.Control with
                    {
                        Focused = true,
                        FocusVisible = true
                    }
                };
                _focusedControlSeen = true;
                _focusedControlTier = _popupTierDepth;
            }
        }
        _lastItemEdited = state.Changes.Length != 0;
        _lastItemToggledSelection = _lastItemEdited;
        if (_lastItemEdited)
            _requestFrame?.Invoke();
        beforePublication?.Invoke();
        ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.RadioGroup, label, bounds,
            focusable: false, valuePattern: false, selectionRequired: selectionRequired);
        for (var i = 0; i < items.Length; i++)
        {
            var local = layouts[i].Bounds;
            var rect = local with
            {
                X = bounds.X + local.X,
                Y = bounds.Y + local.Y
            };
            var clip = IntersectRect(CurrentClipRect, rect);
            var itemId = state.AccessibilityId(root, i, items);
            var focused = interaction.Control.Focused && interaction.FocusedIndex == i;
            tree.Add(new(itemId, root, UiAccessibilityRole.RadioButton, items[i].Label, clip,
                IsControlEnabled && items[i].Enabled && _popupTierDepth >= _blockingKeyboardTier,
                clip.Width <= 0 || clip.Height <= 0, focused, Hint: items[i].Description, ValuePattern: false)
            {
                Selected = state.Value == items[i].Value
            }, rect);
            if (focused)
                (_accessibilityFocusOwner, _accessibilityFocusTarget) = (root, itemId);
        }
        return interaction;
    }

    private UiAccessibilityResult RadioAccessibilityAvailability(bool enabled, UiRect clip) =>
        !IsControlEnabled || !enabled ? UiAccessibilityResult.Disabled
        : clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier
            ? UiAccessibilityResult.Unavailable : UiAccessibilityResult.Success;
}
