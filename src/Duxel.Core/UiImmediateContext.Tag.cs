namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal readonly record struct TagGroupScope(string Parent, UiWidget? Widget, int TabStart);
    internal TagGroupScope BeginTagGroup(string label, UiRect bounds, UiOptionSelection selection,
        UiTagGroupInputState input, string[] names, bool rightToLeft, bool dismissible)
    {
        var root = ResolveId(UiControlWidget.PrimaryControlId);
        if (input.Root != root)
        {
            input.Root = root;
            input.Ids = names.Select(ResolveId).ToArray();
        }
        var current = Array.IndexOf(input.Ids, _state.FocusedId);
        bool Enabled(int index) => dismissible && selection.Selected[input.OptionIndices[index]] && selection.Options[input.OptionIndices[index]].Enabled;
        if (current >= 0 && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
        {
            if (!Enabled(current))
            {
                var next = -1;
                for (var i = current + 1; i < input.Ids.Length; i++)
                {
                    if (Enabled(i))
                    {
                        next = i;
                        break;
                    }
                }
                if (next < 0)
                {
                    for (var i = current - 1; i >= 0; i--)
                    {
                        if (Enabled(i))
                        {
                            next = i;
                            break;
                        }
                    }
                }
                if (next >= 0)
                    FocusGroupControl(input.Ids[next], true);
                else
                    FocusAfterTagGroup(input);
                current = next;
            }
            foreach (var key in _keyEvents)
            {
                if (current < 0 || !key.IsDown || key.Modifiers != 0)
                    continue;
                var step = key.Key is UiKey.LeftArrow or UiKey.UpArrow ? -1 : key.Key is UiKey.RightArrow or UiKey.DownArrow ? 1 : 0;
                if (rightToLeft && key.Key is UiKey.LeftArrow or UiKey.RightArrow)
                    step = -step;
                if (key.Key is UiKey.Home or UiKey.End)
                {
                    step = key.Key == UiKey.Home ? 1 : -1;
                    current = key.Key == UiKey.Home ? -1 : input.Ids.Length;
                }
                if (step == 0)
                    continue;
                for (var attempt = 0; attempt < input.Ids.Length; attempt++)
                {
                    current = (current + step + input.Ids.Length) % input.Ids.Length;
                    if (!Enabled(current))
                        continue;
                    FocusGroupControl(input.Ids[current], true);
                    break;
                }
            }
        }
        var scope = new TagGroupScope(_accessibilityParent, _accessibilityWidget, _currentTabStops.Count);
        ExposeAccessibility(root, scope.Parent, UiAccessibilityRole.Toolbar, label, bounds, focusable: false, valuePattern: false);
        _accessibilityParent = root;
        _accessibilityWidget = null;
        return scope;
    }
    internal void EndTagGroup(TagGroupScope scope, UiTagGroupInputState input, UiOptionSelection selection, bool dismissible)
    {
        var keep = -1;
        for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
            if (_currentTabStops[i].Id == _state.FocusedId)
            {
                keep = i;
                input.RememberedIndex = Array.IndexOf(input.Ids, _currentTabStops[i].Id);
                break;
            }
        var remembered = input.RememberedIndex;
        if (keep < 0 && dismissible && remembered >= 0 && remembered < input.OptionIndices.Length
            && selection.Selected[input.OptionIndices[remembered]] && selection.Options[input.OptionIndices[remembered]].Enabled)
        {
            for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
            {
                if (_currentTabStops[i].Id != input.Ids[remembered])
                    continue;
                keep = i;
                break;
            }
        }
        if (keep < 0 && dismissible)
            for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
            {
                var index = Array.IndexOf(input.Ids, _currentTabStops[i].Id);
                if (index >= 0 && selection.Selected[input.OptionIndices[index]] && selection.Options[input.OptionIndices[index]].Enabled)
                {
                    keep = i;
                    break;
                }
            }
        for (var i = _currentTabStops.Count - 1; i >= scope.TabStart; i--)
            if (i != keep)
                _currentTabStops.RemoveAt(i);
        _accessibilityParent = scope.Parent;
        _accessibilityWidget = scope.Widget;
    }
    private void FocusAfterTagGroup(UiTagGroupInputState input)
    {
        for (var i = 0; i < _previousTabStops.Count; i++)
        {
            if (_previousTabStops[i].Id != _state.FocusedId)
                continue;
            var next = _previousTabStops[NextControlTabStop(i, reverse: false)].Id;
            if (Array.IndexOf(input.Ids, next) >= 0)
                break;
            FocusGroupControl(next, keyboard: true);
            foreach (var stop in _currentTabStops)
            {
                if (stop.Id != next)
                    continue;
                _focusedControlSeen = true;
                _focusedControlTier = stop.PopupTier;
                break;
            }
            return;
        }
        _state.FocusedId = _state.ActiveId = _ownedControlFocus = null;
        CancelControlSpace();
    }
    internal bool RenderTag<TPresentation>(string id, UiTagContent content, UiTagOptions options, bool dismissible, bool enabled,
        UiButtonActivation pointerActivation)
        where TPresentation : IUiTagPresentation
    {
        options = options.WithInheritedDirection(RightToLeft);
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.Measure(context, content, options, dismissible));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        BeginDisabled(!enabled);
        try
        {
            using var clip = context.Clip(bounds);
            var root = ResolveId(id);
            var interaction = dismissible ? ProcessButton(id, bounds, pointerActivation) : default;
            if (dismissible)
            {
                interaction = ApplyAccessibilityInvoke(root, bounds, interaction);
                if (IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier && _state.FocusedId == root
                    && _keyEvents.Any(key => key.IsDown && key.Modifiers == 0 && key.Key is UiKey.Delete or UiKey.Backspace))
                    interaction = interaction with
                    {
                        Activated = true
                    };
            }
            var name = content.SecondaryText is null ? content.Text : content.Text + ", " + content.SecondaryText;
            ExposeAccessibility(root, _accessibilityParent, dismissible ? UiAccessibilityRole.Button : UiAccessibilityRole.Label,
                dismissible ? options.DismissLabel + " " + name : name, bounds, focusable: dismissible, valuePattern: false,
                literalLabel: true);
            TPresentation.Paint(context, content, bounds, options, interaction, dismissible);
            return interaction.Activated;
        }
        finally { EndDisabled(); FinishLayoutBox(origin, size); }
    }
}
