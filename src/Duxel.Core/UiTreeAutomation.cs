namespace Duxel.Core;

/// <summary>Publishes only demanded hidden paths and realizes them through the expansion binding.</summary>
internal sealed class UiTreeAutomation
{
    private int _target = -1;
    private int[] _hidden = [];
    private long _visibility = -1, _selection = -1;
    private int _cachedTarget = -2;
    private UiAccessibilityTree.Request? _request, _scroll;

    internal void Reset()
    {
        _target = -1; _hidden = []; _visibility = _selection = -1; _cachedTarget = -2;
        _request = _scroll = null;
    }

    internal void Prepare(UiTreeViewState state, UiAccessibilityTree accessibility, bool enabled)
    {
        _request = _scroll = null;
        if (state.Root is not { } root) return;
        var request = accessibility.Take(root, UiAccessibilityAction.FindVirtualItem)
            ?? accessibility.Take(root, UiAccessibilityAction.RealizeVirtualItem);
        if (request is not null)
        {
            if (request.Action != UiAccessibilityAction.FindVirtualItem && !enabled)
                request.Result = UiAccessibilityResult.Disabled;
            else if (request.VirtualItem is not { } action)
                request.Result = UiAccessibilityResult.InvalidArgument;
            else
            {
                var graph = state.Model.Hierarchy;
                var start = 0;
                if (action.Reference is { } reference)
                {
                    if (!accessibility.TryReadVirtualItem(reference, out var item) || item.ContainerId != root
                        || item.Index < 0 || item.Index >= graph.Count || graph.Entries[item.Index].Value != item.Key)
                    {
                        request.Result = UiAccessibilityResult.Unavailable;
                        return;
                    }
                    start = graph.PreorderIndex[item.Index] + 1;
                    if (request.Action == UiAccessibilityAction.RealizeVirtualItem)
                    {
                        _target = item.Index;
                        if (!Reveal(state, _target)) request.Result = UiAccessibilityResult.InvalidOperation;
                    }
                }
                if (request.Action == UiAccessibilityAction.FindVirtualItem)
                {
                    _target = -1;
                    for (var position = start; position < graph.Count; position++)
                    {
                        var index = graph.Preorder[position];
                        var entry = graph.Entries[index];
                        if (action.Property == UiVirtualItemSearchProperty.Next
                            || (action.Property == UiVirtualItemSearchProperty.Name ? entry.Label : entry.Value) == request.Value)
                        { _target = index; break; }
                    }
                }
                _request = request;
            }
        }
        foreach (var index in _hidden)
        {
            if (state.RowIds[index] is not { } id || accessibility.Take(id, UiAccessibilityAction.ScrollIntoView) is not { } scroll)
                continue;
            if (!enabled) scroll.Result = UiAccessibilityResult.Disabled;
            else if (!Reveal(state, index)) scroll.Result = UiAccessibilityResult.InvalidOperation;
            else _scroll = scroll;
            break;
        }
    }

    private static bool Reveal(UiTreeViewState state, int index)
    {
        var revision = state.Model.VisibilityRevision;
        var result = state.Model.Reveal(index);
        if (state.Model.VisibilityRevision != revision) state.Invalidate?.Invoke();
        return result;
    }

    internal ReadOnlySpan<int> Hidden(UiTreeViewState state)
    {
        var revision = state.Single?.Revision ?? -1;
        if (_visibility == state.Model.VisibilityRevision && _selection == revision && _cachedTarget == _target)
            return _hidden;
        var paths = new HashSet<int>();
        void AddPath(int index)
        {
            for (; index >= 0 && !state.Model.IsVisible(index) && paths.Add(index); index = state.Model.Hierarchy.Parent[index]) { }
        }
        if (state.Single is { } single)
            AddPath(single.FirstSelected());
        AddPath(_target);
        _hidden = paths.OrderBy(index => state.Model.Hierarchy.PreorderIndex[index]).ToArray();
        _visibility = state.Model.VisibilityRevision; _selection = revision; _cachedTarget = _target;
        return _hidden;
    }

    internal void Complete(UiTreeViewState state, UiAccessibilityTree accessibility)
    {
        if (_request is { Result : null } request)
        {
            if (_target < 0) request.Result = UiAccessibilityResult.Success;
            else if (state.RowIds[_target] is { } id && accessibility.TryGetStaged(id, out var node))
            {
                request.FoundVirtualItemId = id;
                if (request.Action == UiAccessibilityAction.FindVirtualItem)
                    request.Result = UiAccessibilityResult.Success;
                else if (node.VirtualItem is { IsPlaceholder : false })
                    accessibility.CompleteTreeRealization(request, id);
                else request.Result = UiAccessibilityResult.Unavailable;
            }
            else request.Result = UiAccessibilityResult.Unavailable;
        }
        if (_scroll is { Result : null } scroll)
            accessibility.ContinueTreeReveal(scroll);
        _request = _scroll = null;
    }
}
