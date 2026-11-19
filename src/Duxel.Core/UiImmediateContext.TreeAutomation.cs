namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private void PublishHiddenTreeItems(UiTreeViewState state)
    {
        if (_accessibility is not { } tree) return;
        var graph = state.Model.Hierarchy;
        foreach (var index in state.Automation.Hidden(state))
        {
            var id = state.RowIds[index] ??= state.Items[index].ResolvePrimaryControlIdentity(_idPrefix);
            if (tree.TryGetStaged(id, out _)) continue;
            var parent = graph.Parent[index] is var parentIndex && parentIndex >= 0
                ? state.RowIds[parentIndex] ?? throw new InvalidOperationException("A virtual Tree path requires its ancestors.") : state.Root!;
            tree.Add(new(id, parent, state.RowRole, state.Items[index].Label, default,
                IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, Offscreen : true, Focused : false,
                Focusable : false, ValuePattern : false)
            {
                VirtualItem = new(index, state.Items[index].Value, IsPlaceholder : true) { ContainerId = state.Root },
                CanScrollIntoView = true
            });
            tree.SetTreeItem(id, state.Root!, state, index);
        }
        state.Automation.Complete(state, tree);
    }
}
