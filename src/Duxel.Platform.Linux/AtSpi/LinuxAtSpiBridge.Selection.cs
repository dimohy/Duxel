using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    // Combo boxes retain their popup ListBox child. Selection belongs to that
    // actual child container, rather than inventing different child indices.
    private static bool HasSelection(UiAccessibilityNode node) => node.SupportsSelection && node.Role != UiAccessibilityRole.ComboBox;
    private static Node[] SelectionItems(Node parent, Dictionary<string, Node> nodes, bool selectedOnly)
    {
        var items = selectedOnly ? parent.Selection?.SelectedItems : parent.Selection?.Items;
        return items is null ? [] : items.Select(item => nodes[NodePath(item.RuntimeId)]).ToArray();
    }
    private static Node[] SelectedChildren(Node parent, Dictionary<string, Node> nodes)
        => SelectionItems(parent, nodes, selectedOnly : true);
    private static bool OwnsSelection(Node parent, Node child)
        => parent.Selection?.Items.Any(item => item.RuntimeId == child.Element?.RuntimeId) == true;

    private GVariant SelectionMethod(Node parent, string method, GVariant input)
    {
        if (parent.Element is not { } owner || !HasSelection(owner.Node))
            throw Invalid("The accessible does not expose a child selection model.");
        if (method is "SelectAll" or "ClearSelection")
            return GVariant.Tuple(GVariant.Bool(SetAllSelected(parent, method == "SelectAll")));
        var index = Int(input, 0);
        if (method is "GetSelectedChild" or "DeselectSelectedChild")
        {
            var selected = SelectedChildren(parent, _nodes);
            var item = index >= 0 && index < selected.Length ? selected[index] : null;
            return method == "GetSelectedChild"
                ? GVariant.Tuple(item is null ? ExternalReference(new("", NullPath)) : ObjectReference(item.Path))
                : GVariant.Tuple(GVariant.Bool(item is not null && SetSelected(parent, item, false)));
        }
        var child = index >= 0 && index < parent.Children.Length ? _nodes[parent.Children[index]] : null;
        return method switch
        {
            "IsChildSelected" => GVariant.Tuple(GVariant.Bool(child is not null && OwnsSelection(parent, child) && child.Element?.Node.Selected == true)),
            "SelectChild" => GVariant.Tuple(GVariant.Bool(child is not null && SetSelected(parent, child, true))),
            "DeselectChild" => GVariant.Tuple(GVariant.Bool(child is not null && SetSelected(parent, child, false))),
            _ => throw Invalid("Unknown selection method: " + method)
        };
    }

    private bool CanChangeSelection(Node parent, Node child, bool selected)
    {
        if (!OwnsSelection(parent, child) || parent.Element?.Node.Enabled != true || child.Element?.Node is not { SupportsSelectionItem: true, Enabled: true } value)
            return false;
        if (value.Offscreen && value.Role != UiAccessibilityRole.Option)
            return false;
        return selected || value.Selected != true || (value.Role is not (UiAccessibilityRole.RadioButton or UiAccessibilityRole.MenuItem)
            && (parent.Element?.Node.SelectionRequired != true || SelectedChildren(parent, _nodes).Length > 1));
    }

    private bool SetSelected(Node parent, Node child, bool selected)
    {
        if (!CanChangeSelection(parent, child, selected))
            return false;
        if (child.Element!.Value.Node.Selected == selected)
            return true;
        var action = selected
            ? parent.Element!.Value.Node.CanSelectMultiple ? UiAccessibilityAction.AddToSelection : UiAccessibilityAction.Select
            : UiAccessibilityAction.Deselect;
        if (!Invoke(child, action))
            return false;
        return _nodes.TryGetValue(parent.Path, out var currentParent) && _nodes.TryGetValue(child.Path, out var current)
            && OwnsSelection(currentParent, current) && current.Element?.Node.Selected == selected;
    }

    private bool SetAllSelected(Node parent, bool selected)
    {
        var owner = parent.Element!.Value.Node;
        if (!owner.Enabled)
            return false;
        var items = SelectionItems(parent, _nodes, selectedOnly : false);
        if (!selected && owner.SelectionRequired && items.Any(child => child.Element?.Node.Selected == true))
            return false;
        if (selected && !owner.CanSelectMultiple && items.Length > 1)
            return false;
        var changes = items.Where(child => child.Element!.Value.Node.Selected != selected).ToArray();
        if (changes.Any(child => !CanChangeSelection(parent, child, selected)))
            return false;
        // Each action commits through the existing live binding authority. A
        // changed/removed target or a binding refusal stops the operation.
        foreach (var child in changes)
        {
            if (!_nodes.TryGetValue(parent.Path, out var liveParent) || !_nodes.TryGetValue(child.Path, out var liveChild)
                || !OwnsSelection(liveParent, liveChild) || !SetSelected(liveParent, liveChild, selected))
                return false;
        }
        return true;
    }

    private void PublishSelectionChanges(Dictionary<string, Node> previous, Dictionary<string, Node> next)
    {
        foreach (var current in next.Values)
        {
            if (current.Element is not { } element || !HasSelection(element.Node) || !previous.TryGetValue(current.Path, out var old))
                continue;
            var before = SelectedChildren(old, previous).Select(node => node.Path);
            var after = SelectedChildren(current, next);
            if (before.SequenceEqual(after.Select(node => node.Path), StringComparer.Ordinal))
                continue;
            Event(current.Path, "SelectionChanged", "", 0, GVariant.Int(0));
            using var properties = GVariant.Tuple(GVariant.String(SelectionInterface), GVariant.Array("{sv}",
                GVariant.DictEntry(GVariant.String("NSelectedChildren"), GVariant.Box(GVariant.Int(after.Length)))), Strings([]));
            Emit(current.Path, "org.freedesktop.DBus.Properties", "PropertiesChanged", properties);
        }
    }
}
