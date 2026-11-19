using Duxel.Core;

internal static class SelectionProjectionTests
{
    internal static void Run(Action<bool, string> check)
    {
        UiAccessibilityNode Node(string id, string parent, UiAccessibilityRole role, bool? selected = null, string? owner = null)
            => new(id, parent, role, id, new(0, 0, 20, 20), true, false, false)
            { Selected = selected, SelectionContainerId = owner, ListSelection = role == UiAccessibilityRole.Tree };
        UiAccessibilityElement Element(UiAccessibilityNode node, long runtime, string? next = null, string? first = null)
            => new(node, runtime, null, next, first, first);
        var root = Element(Node("root", "", UiAccessibilityRole.Tree), 1, first : "branch");
        var branch = Element(Node("branch", "root", UiAccessibilityRole.TreeItem, false, "root"), 2, next : "list", first : "leaf");
        var leaf = Element(Node("leaf", "branch", UiAccessibilityRole.TreeItem, true, "root"), 3);
        var list = Element(Node("list", "root", UiAccessibilityRole.ListBox), 4, first : "option");
        var option = Element(Node("option", "list", UiAccessibilityRole.Option, true), 5);
        var snapshot = new UiAccessibilitySnapshot(1, "root", "root", null, [root, branch, leaf, list, option]);
        check(snapshot.CaptureSelection("root", 1, out var tree) == UiAccessibilityResult.Success
            && tree!.Items.Select(item => item.Node.Id).SequenceEqual(["branch", "leaf"]),
            "Tree owns nested rows but excludes a nested independent ListBox selection");
        check(tree!.SelectedItems.Single().RuntimeId == 3, "Tree selection reaches a selected grandchild");
        check(snapshot.CaptureSelection("list", 4, out var nested) == UiAccessibilityResult.Success
            && nested!.SelectedItems.Single().RuntimeId == 5, "nested ListBox retains its own selection");
        check(snapshot.ResolveSelectionContainer("option", 5, out var owner) == UiAccessibilityResult.Success
            && owner.RuntimeId == 4, "ordinary option ownership remains its immediate ListBox");
        var invalid = new UiAccessibilitySnapshot(2, "root", "root", null,
            [root, branch, leaf, list, option with { Node = option.Node with { SelectionContainerId = "root" } }]);
        check(invalid.ResolveSelectionContainer("option", 5, out _) == UiAccessibilityResult.InvalidOperation,
            "explicit ownership cannot bypass an intervening independent selection container");
        invalid = new(3, "root", "root", null,
            [root, branch, leaf with { Node = leaf.Node with { SelectionContainerId = "list" } }, list, option]);
        check(invalid.ResolveSelectionContainer("leaf", 3, out _) == UiAccessibilityResult.InvalidOperation,
            "an unrelated sibling cannot become a selection owner");
        check(snapshot.ResolveSelectionContainer("leaf", 99, out _) == UiAccessibilityResult.Unavailable
            && snapshot.CaptureSelection("missing", 1, out _) == UiAccessibilityResult.Unavailable,
            "selection queries reject missing or stale provider identities");
        check(snapshot.CaptureSelection("branch", 2, out _) == UiAccessibilityResult.InvalidOperation,
            "a Tree branch does not fabricate a separate selection container");
        var combo = Element(Node("combo", "", UiAccessibilityRole.ComboBox), 6, first : "list");
        var popup = list with { Node = list.Node with { ParentId = "combo" } };
        var comboSnapshot = new UiAccessibilitySnapshot(4, "combo", "combo", null, [combo, popup, option]);
        check(comboSnapshot.CaptureSelection("combo", 6, out var comboSelection) == UiAccessibilityResult.Success
            && comboSelection!.Container.RuntimeId == 4 && comboSelection.SelectedItems.Single().RuntimeId == 5,
            "ComboBox selection retains its actual popup ListBox owner");
        const int depth = 10000;
        UiAccessibilityElement[] deep = new UiAccessibilityElement[depth + 1];
        deep[0] = root with { FirstChild = "n1", LastChild = "n1" };
        for (var i = 1; i <= depth; i++)
            deep[i] = Element(Node("n" + i, i == 1 ? "root" : "n" + (i - 1), UiAccessibilityRole.TreeItem,
                i == depth, "root"), i + 10, first : i < depth ? "n" + (i + 1) : null);
        var deepSnapshot = new UiAccessibilitySnapshot(5, "root", "root", null, deep);
        check(deepSnapshot.CaptureSelection("root", 1, out var deepSelection) == UiAccessibilityResult.Success
            && deepSelection!.Items.Count == depth && deepSelection.SelectedItems.Single().Node.Id == "n10000",
            "deep selection capture traverses 10000 levels iteratively without recursive stack growth");
        using var publication = new UiAccessibilityTree();
        void Publish(bool includeLeaf)
        {
            publication.BeginFrame();
            publication.Add(root.Node with { SelectionRequired = true });
            publication.Add(branch.Node);
            if (includeLeaf) publication.Add(leaf.Node);
            publication.EndFrame(null);
        }
        Publish(true);
        var current = publication.CaptureSnapshot().ElementsById["leaf"];
        check(publication.RequestAction("leaf", current.RuntimeId, UiAccessibilityAction.Deselect, wait : false)
            == UiAccessibilityResult.InvalidOperation, "required Tree selection cannot remove its only selected descendant");
        UiAccessibilitySelectionChange? removed = null;
        publication.SelectionChanged += change => removed = change;
        Publish(false);
        check(removed?.Element.Node.Id == "root" && removed?.Event == UiAccessibilitySelectionEventKind.Invalidated,
            "removing the selected descendant invalidates the actual Tree root");
        check(snapshot.Elements.Count(element => element.Node.Selected == true) == 2, "retained revision remains independent of subsequent publications");
    }

}
