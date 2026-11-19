using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe class TreeVirtualSelectionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var host = new WindowsAccessibility(0);
        var accessibility = host.Tree; context.SetAccessibilityTree(accessibility);
        using var wake = new ManualResetEventSlim(); accessibility.SetRequestFrameCallback(wake.Set);
        var expanded = new UiState<string[]>([]);
        var selected = new UiState<string?>("b");
        UiTreeItem[] Items() => [new("root", null, "Root", Fluent.Body1("Root")),
            new("a", "root", "Alpha", Fluent.Body1("Alpha")), new("nested", "root", "Nested", Fluent.Body1("Nested")),
            new("b", "nested", "Beta", Fluent.Body1("Beta")), new("other", null, "Other", Fluent.Body1("Other")),
            new("hidden", "other", "Undemanded", Fluent.Body1("Undemanded"))];
        using var widget = new UiTreeWidget("Navigation", expanded.Binding, Items(), single : selected.Binding, options : new(Navigation : true));
        UiWidget current = widget;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.BeginDisabled(disabled);
                try { current.Render(ui); } finally { ui.EndDisabled(); }
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => accessibility.CaptureSnapshot().Elements.Single(e => e.Node.Name == name
            && e.Node.Role is UiAccessibilityRole.Tree or UiAccessibilityRole.TreeItem);
        T Request<T>(Func<T> action)
        {
            wake.Reset();
            var task = Task.Run(action);
            for (var frame = 0; !task.IsCompleted && frame < 8; frame++)
            {
                var signaled = WaitHandle.WaitAny([wake.WaitHandle, ((IAsyncResult)task).AsyncWaitHandle], TimeSpan.FromSeconds(1));
                if (signaled == WaitHandle.WaitTimeout) throw new TimeoutException("Tree request neither completed nor woke its writer.");
                if (signaled == 0) { wake.Reset(); Frame(); }
            }
            if (!task.IsCompleted) throw new TimeoutException("Tree request exceeded its frame budget.");
            return task.GetAwaiter().GetResult();
        }
        Frame();
        var root = Find("Navigation");
        var beta = Find("Beta");
        check(beta.Node is { Selected : true, Offscreen : true, VirtualItem.IsPlaceholder : true }
            && Find("Nested").Node.Offscreen && !Find("Root").Node.Offscreen,
            "a never-rendered selected descendant publishes only its hidden ancestry as virtual Tree items");
        check(!accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Name is "Alpha" or "Undemanded"),
            "collapsed selection does not publish unrelated hidden rows");
        check(accessibility.CaptureSelection(root.Node.Id, root.RuntimeId, out var selection) == UiAccessibilityResult.Success
            && selection!.SelectedItems.Single().RuntimeId == beta.RuntimeId,
            "selection capture preserves a selected descendant before its parents have ever expanded");
        check(accessibility.ResolveSelectionContainer(beta.Node.Id, beta.RuntimeId, out var owner) == UiAccessibilityResult.Success
            && owner.RuntimeId == root.RuntimeId, "a virtual selected descendant retains the actual Tree root owner");
        List<UiAccessibilityElement> structure = [];
        var atomicEvents = true;
        accessibility.TreeChildrenChanged += change =>
        {
            structure.Add(change);
            atomicEvents &= accessibility.CaptureSnapshot().ElementsById.TryGetValue(change.Node.Id, out var published)
                && published.RuntimeId == change.RuntimeId;
        };
        var reference = new UiAccessibilityItemReference(beta.Node.Id, beta.RuntimeId);
        var nativeLeaf = host.Find(beta.Node.Id)!;
        nativeLeaf.GetPropertyValue(30016, out var control);
        nativeLeaf.GetPropertyValue(30017, out var content);
        check(control.Data.Boolean == 0 && content.Data.Boolean == 0,
            "collapsed virtual Tree items remain in raw UIA view and are excluded from control/content views");
        nativeLeaf.GetPatternProvider(10020, out var virtualized);
        check(virtualized != 0, "selected collapsed descendants expose the native VirtualizedItem pattern");
        int nativeResult;
        try
        {
            nativeResult = Request(() =>
            {
                var table = *(nint**)virtualized;
                return ((delegate* unmanaged[Stdcall]<nint, int>)table[3])(virtualized);
            });
        }
        finally { if (virtualized != 0) Marshal.Release(virtualized); }
        beta = Find("Beta");
        check(nativeResult == 0 && expanded.Value.SequenceEqual(["root", "nested"])
            && beta.Node is { Offscreen : false, VirtualItem.IsPlaceholder : false } && beta.RuntimeId == reference.RuntimeId,
            "realization opens all ancestors and replaces the placeholder on the same provider identity");
        nativeLeaf.GetPropertyValue(30016, out control);
        nativeLeaf.GetPropertyValue(30017, out content);
        check(control.Data.Boolean == -1 && content.Data.Boolean == -1 && ReferenceEquals(nativeLeaf, host.Find(beta.Node.Id)),
            "native realization upgrades the same provider into control/content views");
        check(atomicEvents && structure.Any(change => change.Node.Name == "Nested")
            && structure.Select(change => change.RuntimeId).Distinct().Count() == structure.Count,
            "virtual-to-real Tree transitions notify each actual parent once after atomic publication");
        var eventCount = structure.Count;
        Frame();
        check(structure.Count == eventCount, "unchanged Tree frames do not repeat native structure invalidation");
        check(selected.Value == "b" && !beta.Node.Focused, "realization preserves selection and does not steal keyboard focus");
        expanded.Value = []; Frame();
        check(Find("Beta").RuntimeId == reference.RuntimeId && Find("Beta").Node.VirtualItem?.IsPlaceholder == true,
            "recollapsing a selected path preserves its provider identity");
        var result = Request(() => accessibility.RequestAction(reference.Id, reference.RuntimeId, UiAccessibilityAction.ScrollIntoView));
        check(result == UiAccessibilityResult.Success && !Find("Beta").Node.Offscreen && selected.Value == "b",
            "ScrollIntoView expands a selected hidden path without changing selection");
        expanded.Value = []; Frame();
        (UiAccessibilityResult Result, string? Id) Lookup(string key)
        {
            var found = accessibility.FindVirtualItem(root.Node.Id, root.RuntimeId, UiVirtualItemSearchProperty.AutomationId,
                key, null, out var id);
            return (found, id);
        }
        var found = Request(() => Lookup("hidden"));
        var undemanded = Find("Undemanded");
        check(found.Result == UiAccessibilityResult.Success && found.Id == undemanded.Node.Id
            && undemanded.Node.VirtualItem is { IsPlaceholder : true, Key : "hidden" },
            "ItemContainer lookup exposes an unselected hidden item only when requested");
        check(accessibility.CaptureSelection(root.Node.Id, root.RuntimeId, out selection) == UiAccessibilityResult.Success
            && selection!.SelectedItems.Single().RuntimeId == reference.RuntimeId,
            "an unselected lookup does not replace the selected placeholder");
        disabled = true; Frame();
        result = Request(() => accessibility.RealizeVirtualItem(root.Node.Id, root.RuntimeId,
            new(undemanded.Node.Id, undemanded.RuntimeId)));
        check(result == UiAccessibilityResult.Disabled && expanded.Value.Length == 0,
            "disabled virtual Tree realization cannot change the expansion binding");
        disabled = false; Frame();
        result = Request(() => accessibility.RealizeVirtualItem(root.Node.Id, root.RuntimeId,
            new(undemanded.Node.Id, undemanded.RuntimeId)));
        check(result == UiAccessibilityResult.Success && expanded.Value.SequenceEqual(["other"])
            && Find("Undemanded").RuntimeId == undemanded.RuntimeId,
            "realizing an unselected lookup opens only its required ancestry");
        var scrollItems = Enumerable.Range(0, 30).Select(i => new UiTreeItem("p" + i, null, "Row " + i, Fluent.Body1("Row " + i)))
            .Append(new UiTreeItem("tail", null, "Tail", Fluent.Body1("Tail")))
            .Append(new UiTreeItem("end", "tail", "End", Fluent.Body1("End"))).ToArray();
        var scrollExpanded = new UiState<string[]>([]);
        var scrollSelected = new UiState<string?>("end");
        using var scrollingTree = new UiTreeWidget("Scrolling", scrollExpanded.Binding, scrollItems, single : scrollSelected.Binding);
        using var scrollView = new UiScrollView(scrollingTree, new(Height : 160));
        current = scrollView; Frame();
        var end = Find("End");
        result = Request(() => accessibility.RequestScrollIntoView(end.Node.Id, end.RuntimeId,
            new UiAccessibilityScrollAlignment(UiAccessibilityScrollPlacement.BottomEdge)));
        check(result == UiAccessibilityResult.Success && !Find("End").Node.Offscreen && scrollExpanded.Value.SequenceEqual(["tail"])
            && scrollSelected.Value == "end", "a hidden descendant expands and scrolls through its actual ancestor viewport");
        scrollExpanded.Value = []; scrollView.Offset = default; Frame();
        end = Find("End"); root = Find("Scrolling");
        result = Request(() => accessibility.RealizeVirtualItem(root.Node.Id, root.RuntimeId, new(end.Node.Id, end.RuntimeId)));
        check(result == UiAccessibilityResult.Success && scrollingTree.State.RowBounds[31].Height > 0
            && Find("End").RuntimeId == end.RuntimeId && Find("End").Node.VirtualItem?.IsPlaceholder == false,
            "Realize waits for actual row geometry after expansion inside a retained parent layout");
        const int depth = 10000;
        var entries = Enumerable.Range(0, depth).Select(i => new UiTreeEntry("n" + i, i == 0 ? null : "n" + (i - 1), "N" + i)).ToArray();
        string[] openValues = [];
        var writes = 0;
        var model = new UiTreeModel(new(entries), new(() => openValues, value => { writes++; openValues = value; }));
        check(model.Reveal(depth - 1) && model.Visible.Length == depth && writes == 1 && model.Focused == -1,
            "deep realization opens 10000 levels with one controlled write and preserves focus");
        check(model.Reveal(depth - 1) && writes == 1, "realizing an already visible model does not rewrite expansion state");
        var rejected = new UiBinding<string[]>(() => [], _ => { });
        using var controlled = new UiTreeWidget("Controlled", rejected, Items(), single : selected.Binding);
        current = controlled; Frame(); root = Find("Controlled"); beta = Find("Beta");
        result = Request(() => accessibility.RealizeVirtualItem(root.Node.Id, root.RuntimeId, new(beta.Node.Id, beta.RuntimeId)));
        check(result == UiAccessibilityResult.InvalidOperation && Find("Beta").Node.VirtualItem?.IsPlaceholder == true,
            "a rejected expansion write keeps the virtual item hidden and reports realization failure");
        current = widget; expanded.Value = []; Frame();
        var previousRoot = Find("Navigation"); var previousBeta = Find("Beta");
        using var reparented = new UiScrollView(widget, new(Height : 160));
        current = reparented; Frame();
        check(Find("Navigation").RuntimeId != previousRoot.RuntimeId && Find("Beta").Node.Id != previousBeta.Node.Id
            && accessibility.ResolveSelectionContainer(previousBeta.Node.Id, previousBeta.RuntimeId, out _) == UiAccessibilityResult.Unavailable,
            "moving a Tree under another identity scope rebuilds virtual IDs and retires old providers");
        Frame();
        check(!context.State.HasActiveAnimations, "settled virtual Tree exposure creates no animation loop");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
