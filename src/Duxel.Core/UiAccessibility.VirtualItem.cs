namespace Duxel.Core;

public enum UiVirtualItemSearchProperty { Next, Name, AutomationId }

public readonly record struct UiAccessibilityVirtualItem(int Index, string Key, bool IsPlaceholder)
{
    /// <summary>An explicit ancestor container for a hierarchical virtual item; null keeps direct-parent ownership.</summary>
    public string? ContainerId { get; init; }
}

public readonly record struct UiAccessibilityItemReference(string Id, long RuntimeId);

public sealed partial class UiAccessibilityTree
{
    internal readonly record struct VirtualItemAction(UiVirtualItemSearchProperty Property, UiAccessibilityItemReference? Reference)
    {
        internal (int Row, int Column)? GridCoordinates { get; init; }
    }

    public UiAccessibilityResult FindVirtualItem(string paneId, long runtimeId, UiVirtualItemSearchProperty property,
        string? value, UiAccessibilityItemReference? startAfter, out string? foundId)
    {
        foundId = null;
        if ((uint)property > (uint)UiVirtualItemSearchProperty.AutomationId || property != UiVirtualItemSearchProperty.Next && value is null)
            return UiAccessibilityResult.InvalidArgument;
        return QueueAction(paneId, runtimeId, UiAccessibilityAction.FindVirtualItem, value, null, wait : true,
            new VirtualItemAction(property, startAfter), out foundId);
    }

    public UiAccessibilityResult RealizeVirtualItem(string paneId, long runtimeId, UiAccessibilityItemReference item) =>
        QueueAction(paneId, runtimeId, UiAccessibilityAction.RealizeVirtualItem, null, null, wait : true,
            new VirtualItemAction(UiVirtualItemSearchProperty.Next, item), out _);

    // The caller holds the publication read lock. Explicit ownership may cross only non-container ancestors.
    private bool OwnsVirtualItem(string containerId, UiAccessibilityElement item)
    {
        if (item.Node.VirtualItem is not { } virtualItem || (virtualItem.ContainerId ?? item.Node.ParentId) != containerId)
            return false;
        var parentId = item.Node.ParentId;
        for (var depth = 0; depth < _published.Count && _published.TryGetValue(parentId, out var parent); depth++)
        {
            var ownsVirtualItems = parent.Node.VirtualItemContainer || parent.Node.Grid is { IsVirtualized : true };
            if (parent.Node.Id == containerId) return ownsVirtualItems;
            if (ownsVirtualItems) break;
            parentId = parent.Node.ParentId;
        }
        return false;
    }

    internal bool TryReadVirtualItem(UiAccessibilityItemReference reference, out UiAccessibilityVirtualItem item)
    {
        if (TryGet(reference.Id, out var current) && current.RuntimeId == reference.RuntimeId && current.Node.VirtualItem is { } value)
        {
            item = value;
            return true;
        }
        item = default;
        return false;
    }
}
