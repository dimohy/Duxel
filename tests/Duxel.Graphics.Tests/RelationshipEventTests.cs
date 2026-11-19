using Duxel.Core;

internal static class RelationshipEventTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        var changes = new List<UiAccessibilityRelationshipChange>();
        var published = true;
        tree.RelationshipChanged += change =>
        {
            changes.Add(change);
            published &= tree.TryGet(change.Element.Node.Id, out var source) && source == change.Element;
        };
        static UiAccessibilityNode Node(string id, string parent = "", UiAccessibilityRole role = UiAccessibilityRole.Button) =>
            new(id, parent, role, id, new(0, 0, 100, 30), true, false, false, ValuePattern : false);
        void Frame(string? label, string? controlled, bool oldExists = true, bool newExists = true, bool source = true)
        {
            tree.BeginFrame();
            if (source) tree.Add(Node("source", role : UiAccessibilityRole.Pane) with { LabeledBy = label, ControlledElementId = controlled });
            if (oldExists) { tree.Add(Node("old-parent", role : UiAccessibilityRole.Pane)); tree.Add(Node("old", "old-parent")); }
            if (newExists) { tree.Add(Node("new-parent", role : UiAccessibilityRole.Pane)); tree.Add(Node("new", "new-parent")); }
            tree.EndFrame();
        }
        Frame("old", "old");
        check(changes.Count == 0, "initial relationship discovery does not fabricate property changes");
        Frame("old", "old");
        check(changes.Count == 0, "unchanged relationships do not repeat property notifications");
        Frame("new", "new", oldExists : false);
        check(changes.Count == 2 && changes.Select(x => x.Property).ToHashSet().SetEquals(
            new[] { UiAccessibilityRelationshipProperty.LabeledBy, UiAccessibilityRelationshipProperty.ControllerFor }),
            "replacing both relationship fields produces exactly their two property changes");
        check(published && !tree.TryGet("old", out _) && changes.All(x => x.Previous!.Element.Node.Id == "old" && x.Current!.Element.Node.Id == "new"),
            "relationship observers read the new published tree while retaining the removed previous target");
        var captured = changes[0].Previous!;
        check(captured.Ancestry.Length == 2 && captured.Ancestry[1].Node.Id == "old-parent"
            && !tree.TryGet("old-parent", out _), "relationship snapshots preserve removed target ancestry independently of live storage");
        var previousNew = changes[0].Current!.Element;
        changes.Clear();
        Frame("new", "new", oldExists : false);
        check(changes.Count == 0, "stable replacement relationships remain quiet on following frames");
        Frame(null, null, oldExists : false, newExists : false);
        check(changes.Count == 2 && changes.All(x => x.Previous!.Element.Node.Id == "new" && x.Current is null),
            "clearing relationships emits actual null targets without dereferencing removed live controls");
        changes.Clear();
        Frame("new", null, oldExists : false);
        check(changes.Count == 1 && changes[0].Property == UiAccessibilityRelationshipProperty.LabeledBy
            && changes[0].Previous is null && changes[0].Current!.Element.RuntimeId != previousNew.RuntimeId,
            "a restored relationship uses its live target's new runtime identity");
        changes.Clear();
        Frame("new", "new", oldExists : false);
        check(changes.Count == 1 && changes[0].Property == UiAccessibilityRelationshipProperty.ControllerFor,
            "controller changes do not invent an unrelated label event");
        changes.Clear();
        Frame(null, null, oldExists : false, newExists : false, source : false);
        check(changes.Count == 0, "removing the property source emits no event from an unavailable element");
        Frame("new", "new", oldExists : false);
        check(changes.Count == 0, "a reintroduced source is discovered without replaying its prior lifetime's events");
        var rejected = false;
        void Reenter(UiAccessibilityRelationshipChange change)
        {
            try { tree.BeginFrame(); }
            catch (InvalidOperationException) { rejected = true; }
        }
        tree.RelationshipChanged += Reenter;
        Frame(null, "new", oldExists : false);
        tree.RelationshipChanged -= Reenter;
        check(rejected, "relationship callbacks cannot reenter publication");
        changes.Clear();
        var revision = tree.Revision;
        var danglingRejected = false;
        try { Frame("missing", "new", oldExists : false); }
        catch (InvalidOperationException) { danglingRejected = true; tree.AbortFrame(); }
        check(danglingRejected && tree.Revision == revision && changes.Count == 0,
            "a dangling relationship fails before changing published state or notifying readers");
        Frame(null, "new", oldExists : false);
        check(changes.Count == 0, "aborted relationship staging cannot leak into the next valid publication");
        var cycleRejected = false;
        try
        {
            tree.BeginFrame();
            tree.Add(Node("source", role : UiAccessibilityRole.Pane) with { LabeledBy = "cycle-a" });
            tree.Add(Node("cycle-a", "cycle-b"));
            tree.Add(Node("cycle-b", "cycle-a"));
            tree.EndFrame();
        }
        catch (InvalidOperationException) { cycleRejected = true; tree.AbortFrame(); }
        check(cycleRejected && tree.Revision == revision,
            "cyclic relationship ancestry is rejected without recursion or partial publication");
        Frame(null, "new", oldExists : false);
        check(captured.Element.Node.Id == "old" && captured.Ancestry[1].Node.Id == "old-parent",
            "captured relationship targets remain immutable after later publications and aborts");
        using var quiet = new UiAccessibilityTree();
        var events = 0;
        quiet.RelationshipChanged += _ => events++;
        UiAccessibilityNode[] stable = [Node("label"), Node("owner", role : UiAccessibilityRole.Pane) with { LabeledBy = "label", ControlledElementId = "label" }];
        void Quiet() { quiet.BeginFrame(); foreach (var node in stable) quiet.Add(node); quiet.EndFrame(); }
        for (var i = 0; i < 32; i++) Quiet();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 32; i++) Quiet();
        check(GC.GetAllocatedBytesForCurrentThread() == before && events == 0,
            "unchanged relationships allocate zero bytes after warmup with a real observer attached");
    }
}
