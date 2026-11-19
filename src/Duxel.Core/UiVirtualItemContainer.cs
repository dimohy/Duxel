namespace Duxel.Core;

internal sealed class UiVirtualItemContainer(IUiVirtualListAutomationSource source)
{
    private readonly record struct Item(int Index, string Key, string Name);

    private Item? _placeholder;
    private bool _refresh;

    internal void Refresh() => _refresh = true;

    private void ReconcilePlaceholder(int count)
    {
        if (!_refresh)
            return;
        _refresh = false;
        if (_placeholder is { } item)
        {
            var index = ResolveIndex(item.Key, item.Index, count);
            _placeholder = index < 0 ? null : new(index, item.Key, Name(index));
        }
    }

    internal static string Id(string paneId, string key) => paneId + "\u001fvirtual-item:" + key;

    internal string Name(int index) => source.GetAccessibleItemName(index)
        ?? throw new InvalidOperationException("A virtual item accessibility name cannot be null.");

    internal UiAccessibilityTree.Request? Prepare(UiAccessibilityTree tree, string paneId, in UiVirtualListGeometry geometry,
        UiVirtualViewport viewport, double maximum, bool enabled, ref double offset)
    {
        var count = geometry.Count;
        ReconcilePlaceholder(count);
        if (tree.Take(paneId, UiAccessibilityAction.FindVirtualItem) is { VirtualItem : { } find } lookup)
        {
            var start = 0;
            if (find.Reference is { } reference)
            {
                if (!TryResolveReference(tree, reference, count, out var previous))
                {
                    lookup.Result = UiAccessibilityResult.Unavailable;
                    return null;
                }
                start = previous.Index + 1;
            }
            var index = start >= count ? -1 : find.Property == UiVirtualItemSearchProperty.Next
                ? start : source.FindAccessibleItem(find.Property, lookup.Value!, start, count);
            if (index == -1)
            {
                _placeholder = null;
                lookup.Result = UiAccessibilityResult.Success;
                return null;
            }
            if (index < start || index >= count)
            {
                lookup.Result = UiAccessibilityResult.InvalidOperation;
                return null;
            }
            var item = new Item(index, source.GetKey(index), Name(index));
            if (Volatile.Read(ref lookup.Canceled))
            {
                lookup.Result = UiAccessibilityResult.Timeout;
                return null;
            }
            if (string.IsNullOrWhiteSpace(item.Key)
                || find.Property == UiVirtualItemSearchProperty.AutomationId && item.Key != lookup.Value
                || find.Property == UiVirtualItemSearchProperty.Name && item.Name != lookup.Value)
            {
                lookup.Result = UiAccessibilityResult.InvalidOperation;
                return null;
            }
            _placeholder = item;
            lookup.FoundVirtualItemId = Id(paneId, item.Key);
            return lookup;
        }

        if (tree.Take(paneId, UiAccessibilityAction.RealizeVirtualItem) is not { VirtualItem.Reference : { } target } realize)
            return null;

        if (!enabled)
        {
            realize.Result = UiAccessibilityResult.Disabled;
            return null;
        }
        if (!TryResolveReference(tree, target, count, out var current))
        {
            if (_placeholder is { } placeholder && Id(paneId, placeholder.Key) == target.Id)
                _placeholder = null;
            realize.Result = UiAccessibilityResult.Unavailable;
            return null;
        }
        var itemToRealize = new Item(current.Index, current.Key, Name(current.Index));
        if (Volatile.Read(ref realize.Canceled))
        {
            realize.Result = UiAccessibilityResult.Timeout;
            return null;
        }
        _placeholder = itemToRealize;
        var metrics = geometry.Item(current.Index);
        var rowStart = metrics.Start;
        var visibleInset = viewport.Clip is { Width : > 0, Height : > 0 } ? viewport.LeadingInset : 0;
        var desiredOffset = Math.Clamp(rowStart - visibleInset, 0, maximum);
        var row = viewport.Place(rowStart - desiredOffset, metrics.Length);
        var slice = UiImmediateContext.IntersectRect(row, viewport.Bounds);
        var visibleRow = UiImmediateContext.IntersectRect(slice, viewport.Clip);
        if (realize.VirtualRevealBounds is not null || visibleRow is not { Width : > 0, Height : > 0 })
        {
            // The row is not yet present in the tree. Reveal its intended slice through the pane's ancestors first.
            realize.VirtualRevealBounds = slice;
            realize.Consumed = false;
            return null;
        }

        offset = desiredOffset;
        realize.FoundVirtualItemId = target.Id;
        return realize;
    }

    private bool TryResolveReference(UiAccessibilityTree tree, UiAccessibilityItemReference reference, int count,
        out UiAccessibilityVirtualItem current)
    {
        if (!tree.TryReadVirtualItem(reference, out current))
            return false;
        var index = ResolveIndex(current.Key, current.Index, count);
        if (index < 0)
            return false;
        current = current with { Index = index };
        return true;
    }

    private int ResolveIndex(string key, int previousIndex, int count)
    {
        if (previousIndex >= 0 && previousIndex < count && source.GetKey(previousIndex) == key)
            return previousIndex;
        var index = source.FindAccessibleItem(UiVirtualItemSearchProperty.AutomationId, key, 0, count);
        return index >= 0 && index < count && source.GetKey(index) == key ? index : -1;
    }

    internal void Complete(UiAccessibilityTree tree, string paneId, UiRect bounds, UiAccessibilityTree.Request? request)
    {
        if (_placeholder is { } item)
        {
            var id = Id(paneId, item.Key);
            if (tree.TryGetStaged(id, out var existing))
            {
                _placeholder = null;
                if (existing.VirtualItem?.Index != item.Index && request is not null)
                {
                    request.Result = UiAccessibilityResult.InvalidOperation;
                    return;
                }
            }
            else
                tree.Add(new(id, paneId, UiAccessibilityRole.ListItem, item.Name, new(bounds.X, bounds.Y, 0, 0),
                    Enabled : true, Offscreen : true, Focused : false, Focusable : false, ValuePattern : false)
                {
                    VirtualItem = new(item.Index, item.Key, IsPlaceholder : true)
                });
        }
        if (request is null)
            return;
        var published = tree.TryGetStaged(request.FoundVirtualItemId!, out var node);
        request.Result = published && (request.Action == UiAccessibilityAction.FindVirtualItem || !node.Offscreen)
            ? UiAccessibilityResult.Success : UiAccessibilityResult.Unavailable;
    }
}
