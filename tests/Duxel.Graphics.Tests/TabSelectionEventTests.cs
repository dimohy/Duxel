using Duxel.Core;

internal static class TabSelectionEventTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        var changes = new List<UiAccessibilitySelectionChange>();
        var snapshotsMatch = true;
        tree.SelectionChanged += change =>
        {
            changes.Add(change);
            snapshotsMatch &= tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element;
        };
        void Frame(string? selected, bool includeFirst = true, bool includeSecond = true, bool secondFirst = false)
        {
            tree.BeginFrame();
            tree.Add(new("tabs", "", UiAccessibilityRole.TabList, "Tabs", new(0, 0, 200, 40), true, false, false, Focusable : false, ValuePattern : false));
            void Tab(string key) => tree.Add(new UiAccessibilityNode(key, "tabs", UiAccessibilityRole.Tab, key,
                new(0, 0, 100, 40), true, false, false, ValuePattern : false) { Selected = selected == key });
            if (secondFirst && includeSecond) Tab("second");
            if (includeFirst) Tab("first");
            if (!secondFirst && includeSecond) Tab("second");
            tree.EndFrame();
        }
        Frame("first");
        check(changes.Count == 0, "initial tab discovery does not fabricate a selection change event");
        Frame("first");
        check(changes.Count == 0, "unchanged selected tabs do not repeat selection notifications");
        Frame("second");
        check(changes.Count == 2 && changes[0] is { Previous : true, Event : null }
            && changes[1] is { Previous : false, Event : UiAccessibilitySelectionEventKind.Selected },
            "replacement selection reports both property changes and only the final ElementSelected event");
        check(snapshotsMatch && tree.TryGet("second", out var second) && second.Node.Selected == true,
            "selection notification observers read the fully published new selection snapshot");
        changes.Clear();
        Frame("second", secondFirst : true);
        check(changes.Count == 0, "reordering stable selected keys does not fabricate a selection change");
        Frame(null);
        check(changes.Count == 1 && changes[0] is { Previous : true, Event : UiAccessibilitySelectionEventKind.Removed }
            && changes[0].Element.Node.Selected == false, "explicit empty selection emits a real removal from selection");
        changes.Clear();
        Frame("second");
        changes.Clear();
        Frame(null, includeSecond : false);
        check(changes.Count == 1 && changes[0].Event == UiAccessibilitySelectionEventKind.Invalidated
            && changes[0].Element.Node.Role == UiAccessibilityRole.TabList,
            "removing the selected element invalidates its surviving container rather than emitting from a dead item");
        changes.Clear();
        Frame("second");
        check(changes.Count == 1 && changes[0] is { Previous : null, Event : UiAccessibilitySelectionEventKind.Selected },
            "inserting a selected tab into a live list emits selection without inventing a previous property value");
        changes.Clear();
        var reentryRejected = false;
        void Reenter(UiAccessibilitySelectionChange change)
        {
            try { tree.BeginFrame(); }
            catch (InvalidOperationException) { reentryRejected = true; }
        }
        tree.SelectionChanged += Reenter;
        Frame("first");
        tree.SelectionChanged -= Reenter;
        check(reentryRejected, "selection publication cannot reenter through a notification callback");
        changes.Clear();
        tree.BeginFrame();
        tree.Add(new("aborted", "", UiAccessibilityRole.Button, "Aborted", new(0, 0, 1, 1), true, false, false));
        tree.AbortFrame();
        Frame("first");
        check(changes.Count == 0, "an aborted frame does not leak selection notifications into the next committed frame");
        changes.Clear();
        tree.BeginFrame(); tree.EndFrame();
        check(changes.Count == 0, "removing the complete tab list does not notify an unavailable selection container");
        Frame("first");
        changes.Clear();
        Frame("second", includeFirst : false);
        check(changes.Count == 1 && changes[0] is { Previous : false, Event : UiAccessibilitySelectionEventKind.Selected }
            && changes[0].Element.Node.Id == "second",
            "removing a selected tab while selecting its existing sibling emits only the replacement selection event");
        changes.Clear();
        Frame("first", includeSecond : false);
        check(changes.Count == 1 && changes[0] is { Previous : null, Event : UiAccessibilitySelectionEventKind.Selected }
            && changes[0].Element.Node.Id == "first",
            "replacing the selected key with a newly inserted key does not invalidate the surviving selected container");
    }
}
