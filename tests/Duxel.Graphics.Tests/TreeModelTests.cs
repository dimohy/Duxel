using Duxel.Core;

internal static class TreeModelTests
{
    internal static void Run(Action<bool, string> check)
    {
        UiTreeEntry[] entries = [new("a", "root", "Alpha"), new("root", null, "Root"),
            new("nested", "root", "Nested"), new("b", "nested", "Beta"), new("c", "nested", "Charlie"), new("other", null, "Other")];
        var graph = new UiTreeHierarchy(entries);
        var root = graph.Index("root"); var nested = graph.Index("nested");
        var a = graph.Index("a"); var b = graph.Index("b"); var c = graph.Index("c"); var other = graph.Index("other");
        string[] Values(ReadOnlySpan<int> indices) => indices.ToArray().Select(i => graph.Entries[i].Value).ToArray();
        check(Values(graph.Preorder).SequenceEqual(["root", "a", "nested", "b", "c", "other"]),
            "Tree indexes unordered flat parents into stable root/sibling preorder");
        check(graph.Depth[root] == 1 && graph.Depth[nested] == 2 && graph.Depth[b] == 3
            && graph.PositionInSet[b] == 1 && graph.PositionInSet[c] == 2 && graph.SizeOfSet[c] == 2,
            "Tree computes accessibility level and actual sibling positions independently of flat input order");
        check(graph.SubtreeEnd[root] - graph.PreorderIndex[root] == 5
            && graph.SubtreeEnd[nested] - graph.PreorderIndex[nested] == 3,
            "Tree caches exact contiguous descendant ranges");
        check(graph.LeafCount[root] == 3 && graph.LeafCount[nested] == 2 && graph.LeafCount[other] == 1,
            "Tree retains terminal counts in immutable hierarchy metadata instead of recounting on selection changes");
        entries[0] = new("mutated", null, "Mutated");
        check(graph.Entries[a].Value == "a", "Tree snapshots caller entry arrays before retaining hierarchy metadata");
        void Invalid(UiTreeEntry[] source, string reason)
        {
            var rejected = false;
            try { _ = new UiTreeHierarchy(source); } catch (ArgumentException) { rejected = true; }
            check(rejected, reason);
        }
        Invalid([new("x", null, "X"), new("x", null, "Other")], "Tree rejects duplicate stable values");
        Invalid([new("x", "missing", "X")], "Tree rejects missing parent values");
        Invalid([new("x", "x", "X")], "Tree rejects self parenting");
        Invalid([new("x", "y", "X"), new("y", "x", "Y")], "Tree rejects rootless cycles");
        Invalid([new("valid", null, "Valid"), new("x", "y", "X"), new("y", "x", "Y")],
            "Tree also rejects disconnected cycles beside a valid rooted component");
        Invalid([new("x", null, "Bad\nlabel")], "Tree rejects multiline accessibility labels");
        var expanded = new UiState<string[]>([]);
        var model = new UiTreeModel(graph, expanded.Binding);
        check(Values(model.Visible).SequenceEqual(["root", "other"]), "collapsed Tree exposes only root rows");
        model.Boundary(false); model.Inward();
        check(model.Focused == root && model.IsExpanded(root)
            && Values(model.Visible).SequenceEqual(["root", "a", "nested", "other"]),
            "Tree inward opens a closed branch without moving focus");
        model.Inward(); check(model.Focused == a, "Tree inward on an open branch focuses its first child");
        model.Outward(); check(model.Focused == root, "Tree outward from a leaf returns to its parent");
        model.Focus(nested); model.Inward(); model.Inward();
        check(model.Focused == b && Values(model.Visible).SequenceEqual(["root", "a", "nested", "b", "c", "other"]),
            "Tree nested expansion and first-child navigation share the same visible projection");
        model.Move(1); check(model.Focused == c, "Tree next navigation advances through visible preorder");
        model.Move(int.MaxValue); check(model.Focused == other, "Tree next boundary clamps without integer overflow");
        model.Move(int.MinValue); check(model.Focused == root, "Tree previous boundary clamps without integer overflow");
        model.Focus(c); model.SetExpanded(root, false);
        check(model.Focused == root && !model.Focus(c), "collapsing an ancestor restores focus to the nearest visible ancestor");
        check(model.IsExpanded(nested), "collapsing a parent preserves the descendant expansion state");
        model.SetExpanded(root, true); model.Focus(c); model.Outward();
        check(model.Focused == nested, "Tree outward from a nested leaf preserves expanded ancestry");
        model.Outward(); check(!model.IsExpanded(nested) && model.Focused == nested,
            "Tree outward first closes an open branch and keeps its focus");
        check(!model.SetExpanded(a, true), "leaf expansion requests never fabricate branches");
        var revision = model.VisibilityRevision;
        for (var i = 0; i < 8; i++) model.Synchronize();
        check(model.VisibilityRevision == revision, "unchanged expansion reuses the visible hierarchy projection");
        expanded.Value = ["nested"]; model.Synchronize();
        check(Values(model.Visible).SequenceEqual(["root", "other"]), "an expanded descendant stays hidden beneath a closed ancestor");
        expanded.Value = ["root"]; model.Synchronize(); expanded.Value[0] = "nested"; model.Synchronize();
        check(Values(model.Visible).SequenceEqual(["root", "other"]), "Tree detects in-place expansion array edits");
        var proposals = 0;
        var readonlyModel = new UiTreeModel(graph, new UiBinding<string[]>(() => [], _ => proposals++));
        readonlyModel.Boundary(false); readonlyModel.Inward();
        check(proposals == 1 && !readonlyModel.IsExpanded(root) && readonlyModel.Focused == root,
            "a rejected expansion binding retains its real state without optimistic focus or retry polling");
        var checkedLeaves = new UiState<string[]>([]);
        var selection = new UiTreeSelection(graph, checkedLeaves.Binding);
        check(selection.Checked(root) == false && selection.Checked(other) == false, "Tree starts with unchecked parent and leaf projections");
        check(selection.SetChecked(root, true) && checkedLeaves.Value.SequenceEqual(["a", "b", "c"])
            && selection.Checked(root) == true && selection.Checked(nested) == true,
            "checking a branch selects all descendant terminal values exactly once in hierarchy order");
        selection.SetChecked(b, false);
        check(selection.Checked(root) is null && selection.Checked(nested) is null && selection.Checked(a) == true,
            "deselecting one child derives mixed states for every affected ancestor");
        selection.SetChecked(nested, true);
        check(selection.Checked(root) == true, "checking a mixed branch completes its subtree and ancestors");
        selection.SetChecked(root, false);
        check(checkedLeaves.Value.Length == 0 && selection.Checked(nested) == false,
            "unchecking a parent clears its descendant selections");
        checkedLeaves.Value = ["b", "other"]; selection.Synchronize();
        check(selection.Checked(other) == true && selection.Checked(nested) is null,
            "independent roots and partially checked subtrees remain independent");
        model.SetExpanded(root, false);
        check(selection.Checked(b) == true, "collapsing a branch never discards hidden selections");
        checkedLeaves.Value[0] = "c"; selection.Synchronize();
        check(selection.Checked(b) == false && selection.Checked(c) == true,
            "Tree detects in-place terminal selection array edits");
        var selectionRevision = selection.Revision;
        for (var i = 0; i < 8; i++) selection.Synchronize();
        check(selection.Revision == selectionRevision, "unchanged checks reuse cached aggregate states");
        var noOpWrites = 0;
        string[] reversedLeaves = ["c", "b", "a"];
        var noOp = new UiTreeSelection(graph, new UiBinding<string[]>(() => reversedLeaves, next => { noOpWrites++; reversedLeaves = next; }));
        noOp.SetChecked(root, true);
        check(noOpWrites == 0 && reversedLeaves.SequenceEqual(["c", "b", "a"]),
            "checking an already checked branch neither rewrites nor reorders the caller binding");
        var checkProposals = 0;
        var readonlyChecks = new UiTreeSelection(graph, new UiBinding<string[]>(() => [], _ => checkProposals++));
        check(!readonlyChecks.SetChecked(root, true) && readonlyChecks.Checked(root) == false && checkProposals == 1,
            "a rejecting checked binding keeps branches unchecked without retrying or inventing selected leaves");
        foreach (var invalid in new[] { new[] { "root" }, new[] { "b", "b" }, new[] { "missing" } })
        {
            var rejected = false;
            try { _ = new UiTreeSelection(graph, new UiState<string[]>(invalid).Binding); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
            check(rejected, "Tree rejects branch, duplicate or unknown terminal selection values : " + string.Join(',', invalid));
        }
        foreach (var invalid in new[] { new[] { "a" }, new[] { "root", "root" }, new[] { "missing" } })
        {
            var rejected = false;
            try { _ = new UiTreeModel(graph, new UiState<string[]>(invalid).Binding); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
            check(rejected, "Tree rejects leaf, duplicate or unknown expansion values : " + string.Join(',', invalid));
        }
        for (var i = 0; i < 8; i++) { model.Synchronize(); selection.Synchronize(); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++) { model.Synchronize(); selection.Synchronize(); }
        check(GC.GetAllocatedBytesForCurrentThread() == before, "unchanged Tree expansion and aggregate selection allocate0B across128 synchronizations");
        var emptyGraph = new UiTreeHierarchy([]);
        var empty = new UiTreeModel(emptyGraph, new UiState<string[]>([]).Binding);
        empty.Move(1); empty.Inward(); empty.Outward(); empty.Boundary(true);
        check(empty.Visible.Length == 0 && empty.Focused == -1, "an empty Tree model has no phantom focus or row");
        const int depth = 10000;
        var deep = new UiTreeEntry[depth];
        for (var i = 0; i < depth; i++) deep[i] = new("n" + i, i == 0 ? null : "n" + (i - 1), "Node " + i);
        var deepGraph = new UiTreeHierarchy(deep);
        var deepChecks = new UiTreeSelection(deepGraph, new UiState<string[]>([]).Binding);
        deepChecks.SetChecked(0, true);
        check(deepGraph.Depth[^1] == depth && deepGraph.SubtreeEnd[0] == depth && deepChecks.Checked(0) == true,
            "10000 nested levels build and aggregate iteratively without recursive stack growth");
    }
}
