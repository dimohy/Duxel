namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    /// <summary>Committed changes to Tree/TreeItem children, including virtual-to-real view transitions.</summary>
    public event Action<UiAccessibilityElement>? TreeChildrenChanged;
    private List<UiAccessibilityElement>? _treeChildrenChanges;
    private readonly HashSet<string> _treeChangedParents = new(StringComparer.Ordinal);

    private void CollectTreeChildrenChanges()
    {
        void Mark(string id)
        {
            if (_entries.TryGetValue(id, out var parent) && parent.Seen == _generation && parent.HasPublished
                && parent.Candidate.Node.Role is UiAccessibilityRole.Tree or UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGrid or UiAccessibilityRole.TreeGridRow
                && _treeChangedParents.Add(id))
                (_treeChildrenChanges ??= []).Add(parent.Candidate);
        }
        try
        {
            foreach (var entry in _updates)
            {
                var current = entry.Candidate;
                if (entry.HasPublished && (entry.Published.FirstChild != current.FirstChild || entry.Published.LastChild != current.LastChild))
                    Mark(current.Node.Id);
                if (current.Node.Role is not (UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGridRow)) continue;
                if (!entry.HasPublished || entry.Published.Node.ParentId != current.Node.ParentId
                    || entry.Published.Previous != current.Previous || entry.Published.Next != current.Next
                    || entry.Published.Node.VirtualItem?.IsPlaceholder != current.Node.VirtualItem?.IsPlaceholder)
                {
                    Mark(current.Node.ParentId);
                    if (entry.HasPublished) Mark(entry.Published.Node.ParentId);
                }
            }
            foreach (var id in _removed)
                if (_entries[id].Published.Node.Role is UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGridRow)
                    Mark(_entries[id].Published.Node.ParentId);
        }
        finally { _treeChangedParents.Clear(); }
    }

    internal void CompleteTreeRealization(Request request, string itemId)
    {
        if (_entries.TryGetValue(itemId, out var entry) && entry.Seen == _generation
            && entry.RevealBounds is { Width : > 0, Height : > 0 })
            request.Result = UiAccessibilityResult.Success;
        else if (request.RevealAfterGeneration == 0)
        {
            request.RevealAfterGeneration = _generation + 1;
            request.Consumed = false;
        }
        else
            request.Result = UiAccessibilityResult.InvalidOperation;
    }

    internal void ContinueTreeReveal(Request request)
    {
        request.Consumed = false;
        if (!_entries.TryGetValue(request.Id, out var entry) || entry.Seen != _generation) return;
        if (entry.RevealBounds is not { Width : > 0, Height : > 0 })
        {
            // Expansion invalidates measurement, but assigned children keep their parent's geometry for this frame.
            request.RevealAfterGeneration = _generation + 1;
            return;
        }
        if (!entry.Staged.Offscreen && FindScrollParent(entry) is null)
            request.Result = UiAccessibilityResult.Success;
    }

    internal void SetTreeRoot(string id, bool single, bool multiple)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with { ListSelection = single, CanSelectMultiple = multiple };
    }
    internal void SetTreeItem(string id, string root, UiTreeViewState state, int index)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Selected = state.Single is not null ? state.Single.Selected[index] : null,
                ToggleState = state.Multiple is null ? null : state.Multiple.Checked(index) switch
                {
                    true => UiAccessibilityToggleState.On,
                    false => UiAccessibilityToggleState.Off,
                    null => UiAccessibilityToggleState.Indeterminate
                },
                SelectionContainerId = state.Single is not null ? root : null,
                PositionInSet = state.Model.Hierarchy.PositionInSet[index],
                SizeOfSet = state.Model.Hierarchy.SizeOfSet[index],
                Expansion = state.Model.Hierarchy.IsBranch(index)
                    ? state.Model.IsExpanded(index) ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed
                    : UiAccessibilityExpansionState.LeafNode,
                VirtualItem = new(index, state.Items[index].Value, !state.Model.IsVisible(index)) { ContainerId = root },
                InvokePattern = state.Items[index].Invoked is not null
            };
    }
}
