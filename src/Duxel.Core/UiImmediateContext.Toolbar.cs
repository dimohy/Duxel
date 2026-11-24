namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private int _toolbarDepth;
    internal readonly record struct ToolbarScope(string Parent, UiWidget? Widget, int TabStart, int Tier, IReadOnlyList<UiKeyEvent> Keys, string? FocusAtStart);
    internal ToolbarScope BeginToolbar<TBehavior>(string label, UiRect bounds, UiToolbarInputState input, UiToolbarOptions options)
        where TBehavior : IUiToolbarBehavior
    {
        if (_toolbarDepth != 0)
            throw new NotSupportedException("A Toolbar cannot contain another Toolbar. Use ordinary child layout groups.");
        var root = ResolveId(UiControlWidget.PrimaryControlId);
        input.Bind(this, root);
        var scope = new ToolbarScope(_accessibilityParent, _accessibilityWidget, _currentTabStops.Count, _popupTierDepth, _keyEvents, _state.FocusedId);
        var index = input.IndexOf(_state.FocusedId);
        input.FilteredKeys.Clear();
        if (index >= 0 && !input.Members[index].ConsumesArrows && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
        {
            foreach (var key in _keyEvents)
            {
                // A preceding event in this frame may have moved focus into an editor.
                var action = key.IsDown && key.Modifiers == 0 && !input.Members[index].ConsumesArrows
                    ? TBehavior.GetNavigation(key.Key, options) : UiToolbarNavigationAction.None;
                if (action == UiToolbarNavigationAction.None)
                {
                    input.FilteredKeys.Add(key);
                    continue;
                }
                var last = input.Members.Count - 1;
                index = action switch
                {
                    UiToolbarNavigationAction.Previous => index == 0 ? TBehavior.WrapNavigation ? last : 0 : index - 1,
                    UiToolbarNavigationAction.Next => index == last ? TBehavior.WrapNavigation ? 0 : last : index + 1,
                    UiToolbarNavigationAction.First => 0,
                    UiToolbarNavigationAction.Last => last,
                    _ => throw new InvalidOperationException("Unsupported Toolbar navigation action.")
                };
                FocusGroupControl(input.Members[index].Id, keyboard: true);
            }
            _keyEvents = input.FilteredKeys;
        }
        ExposeAccessibility(root, scope.Parent, UiAccessibilityRole.Toolbar, label, bounds, focusable: false, valuePattern: false);
        _accessibility?.SetOrientation(root, options.Vertical ? UiAccessibilityOrientation.Vertical : UiAccessibilityOrientation.Horizontal);
        _accessibilityParent = root;
        _accessibilityWidget = null;
        _toolbarDepth++;
        return scope;
    }
    internal void EndToolbar(ToolbarScope scope, UiToolbarInputState input)
    {
        try
        {
            var oldFocused = input.IndexOf(scope.FocusAtStart);
            input.Members.Clear();
            var keep = -1;
            var remembered = -1;
            for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
            {
                var stop = _currentTabStops[i];
                if (stop.PopupTier != scope.Tier)
                    continue;
                input.Members.Add(new(stop.Id, stop.ConsumesArrows));
                if (stop.Id == _state.FocusedId)
                    keep = i;
                if (stop.Id == input.Remembered)
                    remembered = i;
            }
            if (keep < 0)
                keep = remembered;
            if (keep < 0 && input.Members.Count > 0)
            {
                for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
                    if (_currentTabStops[i].PopupTier == scope.Tier)
                    {
                        keep = i;
                        break;
                    }
            }
            if (oldFocused >= 0 && input.IndexOf(_state.FocusedId) < 0 && (_state.FocusedId is null || _state.FocusedId == scope.FocusAtStart))
            {
                if (keep >= 0)
                {
                    FocusGroupControl(_currentTabStops[keep].Id, keyboard: true);
                    _focusedControlSeen = true;
                    _focusedControlTier = scope.Tier;
                }
                else
                {
                    _state.FocusedId = _ownedControlFocus = null;
                    _state.ActiveId = null;
                    CancelControlSpace();
                }
            }
            input.Remembered = keep < 0 ? null : _currentTabStops[keep].Id;
            for (var i = _currentTabStops.Count - 1; i >= scope.TabStart; i--)
                if (i != keep && _currentTabStops[i].PopupTier == scope.Tier)
                    _currentTabStops.RemoveAt(i);
        }
        finally
        {
            _toolbarDepth--;
            _keyEvents = scope.Keys;
            _accessibilityParent = scope.Parent;
            _accessibilityWidget = scope.Widget;
        }
    }
}
