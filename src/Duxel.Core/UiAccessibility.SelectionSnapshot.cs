namespace Duxel.Core;

/// <summary>One container's selection membership from a single published revision.</summary>
public sealed class UiAccessibilitySelectionSnapshot
{
    internal UiAccessibilitySelectionSnapshot(UiAccessibilityElement container, UiAccessibilityElement[] items)
    {
        Container = container;
        Items = Array.AsReadOnly(items);
        SelectedItems = Array.AsReadOnly(items.Where(item => item.Node.Selected == true).ToArray());
    }

    public UiAccessibilityElement Container { get; }
    public IReadOnlyList<UiAccessibilityElement> Items { get; }
    public IReadOnlyList<UiAccessibilityElement> SelectedItems { get; }
}

internal static class UiAccessibilitySelectionProjection
{
    internal static UiAccessibilityResult Resolve(IReadOnlyDictionary<string, UiAccessibilityElement> elements,
        string id, long runtimeId, out UiAccessibilityElement container)
    {
        container = default;
        if (!elements.TryGetValue(id, out var item) || item.RuntimeId != runtimeId)
            return UiAccessibilityResult.Unavailable;
        if (!item.Node.SupportsSelectionItem)
            return UiAccessibilityResult.InvalidOperation;
        var ownerId = item.Node.SelectionContainerId ?? item.Node.ParentId;
        var parentId = item.Node.ParentId;
        for (var depth = 0; depth < elements.Count && elements.TryGetValue(parentId, out var parent); depth++)
        {
            if (parent.Node.Id == ownerId)
            {
                if (!parent.Node.SupportsSelection) return UiAccessibilityResult.InvalidOperation;
                container = parent;
                return UiAccessibilityResult.Success;
            }
            if (parent.Node.SupportsSelection) break;
            parentId = parent.Node.ParentId;
        }
        return UiAccessibilityResult.InvalidOperation;
    }

    internal static UiAccessibilityResult Capture(IReadOnlyDictionary<string, UiAccessibilityElement> elements,
        string id, long runtimeId, out UiAccessibilitySelectionSnapshot? selection)
    {
        selection = null;
        if (!elements.TryGetValue(id, out var container) || container.RuntimeId != runtimeId)
            return UiAccessibilityResult.Unavailable;
        if (!container.Node.SupportsSelection) return UiAccessibilityResult.InvalidOperation;
        if (container.Node.Role == UiAccessibilityRole.ComboBox)
        {
            for (var childId = container.FirstChild; childId is not null;)
            {
                var child = elements[childId];
                if (child.Node.Role == UiAccessibilityRole.ListBox) { container = child; break; }
                childId = child.Next;
            }
        }
        List<UiAccessibilityElement> items = [];
        Stack<string> pending = [];
        if (container.FirstChild is { } first) pending.Push(first);
        while (pending.TryPop(out var childId))
        {
            var child = elements[childId];
            if (child.Next is { } next) pending.Push(next);
            // Traversal stays inside this container and prunes independent selection roots.
            // Membership therefore needs no repeated ancestor walk for each nested item.
            if (child.Node.SupportsSelectionItem
                && (child.Node.SelectionContainerId ?? child.Node.ParentId) == container.Node.Id)
                items.Add(child);
            if (container.Node.Role is UiAccessibilityRole.Tree or UiAccessibilityRole.TreeGrid && !child.Node.SupportsSelection && child.FirstChild is { } nested)
                pending.Push(nested);
        }
        selection = new(container, items.ToArray());
        return UiAccessibilityResult.Success;
    }
}

public sealed partial class UiAccessibilityTree
{
    public UiAccessibilityResult CaptureSelection(string id, long runtimeId, out UiAccessibilitySelectionSnapshot? selection)
    {
        _gate.EnterReadLock();
        try
        {
            selection = null;
            return _disposed ? UiAccessibilityResult.Unavailable
                : UiAccessibilitySelectionProjection.Capture(_published, id, runtimeId, out selection);
        }
        finally { _gate.ExitReadLock(); }
    }

    public UiAccessibilityResult ResolveSelectionContainer(string id, long runtimeId, out UiAccessibilityElement container)
    {
        _gate.EnterReadLock();
        try
        {
            container = default;
            return _disposed ? UiAccessibilityResult.Unavailable
                : UiAccessibilitySelectionProjection.Resolve(_published, id, runtimeId, out container);
        }
        finally { _gate.ExitReadLock(); }
    }
}

public sealed partial class UiAccessibilitySnapshot
{
    private IReadOnlyDictionary<string, UiAccessibilityElement>? _elementsById;
    public IReadOnlyDictionary<string, UiAccessibilityElement> ElementsById
        => System.Threading.LazyInitializer.EnsureInitialized(ref _elementsById,
            () => new System.Collections.ObjectModel.ReadOnlyDictionary<string, UiAccessibilityElement>(
                Elements.ToDictionary(element => element.Node.Id, StringComparer.Ordinal)));

    public UiAccessibilityResult CaptureSelection(string id, long runtimeId, out UiAccessibilitySelectionSnapshot? selection)
        => UiAccessibilitySelectionProjection.Capture(ElementsById, id, runtimeId, out selection);
    public UiAccessibilityResult ResolveSelectionContainer(string id, long runtimeId, out UiAccessibilityElement container)
        => UiAccessibilitySelectionProjection.Resolve(ElementsById, id, runtimeId, out container);
}
