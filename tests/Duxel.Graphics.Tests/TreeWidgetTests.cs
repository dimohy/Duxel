using System.Runtime.InteropServices;
using Duxel.Platform.Windows;
using Duxel.Platform.Linux;
using Duxel.Core;

internal static class TreeWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var host = new WindowsAccessibility(0);
        var tree = host.Tree;
        context.SetAccessibilityTree(tree);
        var wakes = 0; context.SetRequestFrameCallback(() => wakes++);
        var expanded = new UiState<string[]>([]);
        UiTreeItem[] Items(Action<string>? action = null) => [new("a", "root", "Alpha", Fluent.Body1("Alpha"), action),
            new("root", null, "Root", Fluent.Body1("Root"), action), new("nested", "root", "Nested", Fluent.Body1("Nested"), action),
            new("b", "nested", "Beta", Fluent.Body1("Beta"), action), new("other", null, "Other", Fluent.Body1("Other"), action)];
        using var information = new UiTreeWidget("Folders", expanded.Binding, Items());
        var leaves = new UiState<string[]>([]);
        using var multiple = new UiTreeWidget("Checked folders", new UiState<string[]>(["root", "nested"]).Binding, Items(), multiple : leaves.Binding);
        var selected = new UiState<string?>(null);
        var calls = new List<string>();
        using var navigation = new UiTreeWidget("Navigation", new UiState<string[]>(["root"]).Binding, Items(calls.Add),
            single : selected.Binding, options : new(Navigation : true));
        UiTreeWidget current = information;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, float delta = .016f)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.BeginDisabled(disabled);
                try { current.Render(ui); } finally { ui.EndDisabled(); }
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.TreeItem)
            => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        UiAccessibilityResult Send(string name, UiAccessibilityAction action)
        {
            var item = Find(name); return tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait : false);
        }
        void Key(UiKey key) => Frame(idle with { KeyEvents = [new(key, true, default)] });
        void Click(string name, float? x = null)
        {
            var bounds = Find(name).Node.Bounds;
            var point = new UiVector2(x ?? bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true }); Frame();
        }
        Frame();
        check(tree.CaptureSnapshot().Elements.Count(e => e.Node.Role == UiAccessibilityRole.TreeItem) == 2
            && Find("Root").Node.ParentId == Find("Folders", UiAccessibilityRole.Tree).Node.Id,
            "actual Tree consumer renders collapsed roots with Tree/TreeItem roles rather than a flat List");
        check(Find("Root").Node.Expansion == UiAccessibilityExpansionState.Collapsed && Find("Other").Node.Expansion == UiAccessibilityExpansionState.LeafNode,
            "branches expose expansion while leaves expose the native LeafNode state");
        Frame();
        check(information.RetainsMeasurement, "collapsed Tree retains its layout without measuring hidden descendants");
        Click("Root"); Frame();
        check(expanded.Value.SequenceEqual(["root"]) && Find("Alpha").Node.ParentId == Find("Root").Node.Id
            && Find("Nested").Node.PositionInSet == 2 && Find("Nested").Node.SizeOfSet == 2,
            "actual branch body click expands and exposes real hierarchical sibling metadata");
        Send("Root", UiAccessibilityAction.Focus); Frame(); Key(UiKey.RightArrow);
        check(Find("Alpha").Node.Focused, "Tree Right enters the first child of an open branch");
        Key(UiKey.DownArrow); Key(UiKey.RightArrow); Frame();
        check(expanded.Value.Contains("nested") && Find("Nested").Node.Focused,
            "Tree Right opens a focused closed nested branch without moving its focus");
        Key(UiKey.RightArrow);
        check(Find("Beta").Node.Focused && Find("Beta").Node.ParentId == Find("Nested").Node.Id,
            "Tree second Right enters the nested child with its true accessibility parent");
        Key(UiKey.LeftArrow); check(Find("Nested").Node.Focused, "Tree Left from a leaf moves to its parent");
        Key(UiKey.LeftArrow); Frame();
        check(!expanded.Value.Contains("nested") && !tree.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.TreeItem && e.Node.Name == "Beta"),
            "Tree Left closes a branch and removes collapsed descendants from the exposed view");
        var wakeCount = wakes;
        Frame(); Frame();
        check(wakes == wakeCount, "focused idle Tree does not request frames or cancel a pending pointer activation");
        Key(UiKey.End); check(Find("Other").Node.Focused, "Tree End moves to the final visible root");
        Key(UiKey.Home); check(Find("Root").Node.Focused, "Tree Home moves to the first visible root");
        check(Send("Nested", UiAccessibilityAction.Expand) == UiAccessibilityResult.Success,
            "Tree branch accepts an actual accessibility expansion request");
        Frame(); Frame();
        check(Find("Beta").Node.ParentId == Find("Nested").Node.Id, "accessibility expansion uses the same hierarchy model as pointer and keys");
        Send("Beta", UiAccessibilityAction.Focus); Frame();
        expanded.Value = []; Frame(); Frame();
        check(Find("Root").Node.Focused, "external ancestor collapse repairs actual keyboard focus to the visible ancestor");
        current = multiple; Frame();
        check(Find("Root").Node.ToggleState == UiAccessibilityToggleState.Off && !Find("Root").Node.Selected.HasValue,
            "multiselect Tree exposes checked state separately from selection and focus");
        Send("Root", UiAccessibilityAction.Toggle); Frame();
        check(leaves.Value.SequenceEqual(["a", "b"]) && Find("Root").Node.ToggleState == UiAccessibilityToggleState.On,
            "Tree accessibility parent toggle cascades through the real terminal selection binding");
        Send("Beta", UiAccessibilityAction.Toggle); Frame();
        check(Find("Root").Node.ToggleState == UiAccessibilityToggleState.Indeterminate && Find("Nested").Node.ToggleState == UiAccessibilityToggleState.Off,
            "Tree publishes final parent mixed state after a descendant is toggled");
        Send("Alpha", UiAccessibilityAction.Focus); Frame(); Key(UiKey.Space);
        check(leaves.Value.Length == 0 && Find("Root").Node.ToggleState == UiAccessibilityToggleState.Off,
            "Tree Space toggles the focused checkbox without moving focus");
        current = navigation; Frame(); Click("Root");
        check(selected.Value == "root" && calls.SequenceEqual(["root"]) && navigation.State.Model.IsExpanded(navigation.State.Model.Hierarchy.Index("root")),
            "navigation mode body selection invokes the item while preserving branch expansion");
        Click("Alpha");
        check(selected.Value == "a" && Find("Alpha").Node.Selected == true
            && Find("Alpha").Node.SelectionContainerId == Find("Navigation", UiAccessibilityRole.Tree).Node.Id,
            "navigation Tree binds its selected child to the true root selection container");
        var rootSelection = Find("Navigation", UiAccessibilityRole.Tree);
        var alpha = Find("Alpha");
        check(tree.ResolveSelectionContainer(alpha.Node.Id, alpha.RuntimeId, out var resolvedRoot) == UiAccessibilityResult.Success
            && resolvedRoot.RuntimeId == rootSelection.RuntimeId,
            "nested Tree selection resolves its root rather than the immediate branch");
        check(tree.CaptureSelection(rootSelection.Node.Id, rootSelection.RuntimeId, out var projection) == UiAccessibilityResult.Success
            && projection!.SelectedItems.Single().RuntimeId == alpha.RuntimeId,
            "native selection projection includes selected descendants in their original hierarchy");
        var retainedSnapshot = tree.CaptureSnapshot();
        check(retainedSnapshot.CaptureSelection(rootSelection.Node.Id, rootSelection.RuntimeId, out var retainedSelection) == UiAccessibilityResult.Success
            && retainedSelection!.Items.Select(item => item.Node.Name).SequenceEqual(["Root", "Alpha", "Nested", "Other"]),
            "selection membership uses stable preorder without flattening the published hierarchy");
        check(tree.CaptureSelection(rootSelection.Node.Id, rootSelection.RuntimeId + 1, out _) == UiAccessibilityResult.Unavailable,
            "selection capture rejects a stale provider runtime identity");
        Click("Root");
        check(retainedSelection!.SelectedItems.Single().RuntimeId == alpha.RuntimeId
            && tree.CaptureSelection(rootSelection.Node.Id, rootSelection.RuntimeId, out var changedSelection) == UiAccessibilityResult.Success
            && changedSelection!.SelectedItems.Single().Node.Name == "Root",
            "retained selection remains atomic when a later frame changes the binding");
        Click("Alpha");
        Click("Root", 8);
        check(!navigation.State.Model.IsExpanded(navigation.State.Model.Hierarchy.Index("root")) && selected.Value == "a" && calls.Count == 4,
            "navigation chevron collapses without invoking or changing the selected item");
        current = information; disabled = true; Frame();
        check(Send("Root", UiAccessibilityAction.Expand) == UiAccessibilityResult.Disabled,
            "disabled Tree rejects expansion through its published accessibility node");
        disabled = false; Frame();
        check(!context.State.HasActiveAnimations, "settled Tree does not introduce an idle animation loop");

        var actionCalls = 0;
        var actionExpansion = new UiState<string[]>(["parent"]);
        var gridSelection = new UiState<string?>("child");
        using var actionTree = new UiTreeWidget("Tree actions", actionExpansion.Binding,
            [new("parent", null, "Parent"),
             new("child", "parent", "Document", Dux.Row(new(Spacing: 8),
                 Fluent.Button("Open", () => actionCalls++), Fluent.Button("Rename", () => actionCalls++))),
             new("other", null, "Unrelated")], single: gridSelection.Binding,
            options: new UiTreeOptions(Width: 340) { RowActions = true });
        current = actionTree;
        Frame();
        UiAccessibilityElement GridRow(string name) => Find(name, UiAccessibilityRole.TreeGridRow);
        var open = Find("Open", UiAccessibilityRole.Button);
        var grid = Find("Tree actions", UiAccessibilityRole.TreeGrid);
        var cell = Find("Document", UiAccessibilityRole.GridCell);
        var document = GridRow("Document");
        var table = AtSpiTable.Capture(tree.CaptureSnapshot())[grid.Node.Id];
        check(table.Dimensions == new UiAccessibilityGridInfo(3, 1)
            && table.At(1, 0)?.Element.RuntimeId == cell.RuntimeId
            && table.Row(1)?.RuntimeId == document.RuntimeId && table.At(1, 1) is null,
            "AT-SPI table coordinates resolve the cell and owning row in the same snapshot");
        check(table.SelectedRows.SequenceEqual([1]) && table.AtIndex(1)?.Selected == true
            && table.Index(table.At(1, 0)) == 1 && table.AtIndex(-1) is null,
            "AT-SPI table selection and logical index use the loaded grid coordinates");
        check(open.Node.ParentId == cell.Node.Id && cell.Node.ParentId == document.Node.Id
            && document.Node.ParentId == GridRow("Parent").Node.Id,
            "treegrid exposes the action through its cell and preserves hierarchical row ancestry");
        check(grid.Node.Grid == new UiAccessibilityGridInfo(3, 1)
            && tree.GetGridItem(grid.Node.Id, grid.RuntimeId, 1, 0, out var reference) == UiAccessibilityResult.Success
            && reference?.RuntimeId == cell.RuntimeId,
            "treegrid lookup returns the visible preorder cell");
        check(tree.CaptureSelection(grid.Node.Id, grid.RuntimeId, out var selection) == UiAccessibilityResult.Success
            && selection!.SelectedItems.Single().RuntimeId == document.RuntimeId,
            "treegrid selection resolves a nested row through the shared hierarchy projection");
        var gridProvider = host.Find(grid.Node.Id)!;
        gridProvider.GetPatternProvider(10006, out var nativeGrid);
        try
        {
            check(nativeGrid != 0 && gridProvider.GetRowCount(out var rows) == 0 && rows == 3
                && gridProvider.GetColumnCount(out var columns) == 0 && columns == 1,
                "Windows exposes the treegrid through the native Grid pattern");
        }
        finally
        {
            if (nativeGrid != 0) Marshal.Release(nativeGrid);
        }
        var cellProvider = host.Find(cell.Node.Id)!;
        cellProvider.GetPatternProvider(10007, out var nativeCell);
        try
        {
            check(nativeCell != 0 && cellProvider.GetRow(out var cellRow) == 0 && cellRow == 1,
                "Windows exposes the cell through the native GridItem pattern");
        }
        finally
        {
            if (nativeCell != 0) Marshal.Release(nativeCell);
        }
        tree.RequestAction(document.Node.Id, document.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        Key(UiKey.F2);
        check(Find("Open", UiAccessibilityRole.Button).Node.Focused && actionCalls == 0,
            "F2 enters row controls without invoking the first action");
        Key(UiKey.RightArrow);
        check(Find("Rename", UiAccessibilityRole.Button).Node.Focused, "horizontal navigation moves among row controls");
        Key(UiKey.Home);
        check(Find("Open", UiAccessibilityRole.Button).Node.Focused, "Home moves to the first action");
        Key(UiKey.End);
        check(Find("Rename", UiAccessibilityRole.Button).Node.Focused, "End moves to the last action");
        Key(UiKey.DownArrow);
        check(GridRow("Unrelated").Node.Focused, "Down from a child moves to the adjacent row when it has no corresponding action");
        Key(UiKey.UpArrow);
        Key(UiKey.F2);
        Key(UiKey.RightArrow);
        Key(UiKey.Enter);
        check(actionCalls == 1, "Enter invokes the focused action once");
        Key(UiKey.Escape);
        check(GridRow("Document").Node.Focused, "Escape returns to the owning row");
        actionTree.Options = actionTree.Options with { RightToLeft = true };
        Frame();
        Key(UiKey.F2);
        Key(UiKey.LeftArrow);
        check(Find("Rename", UiAccessibilityRole.Button).Node.Focused, "row action navigation mirrors in RTL");
        var button = Find("Open", UiAccessibilityRole.Button).Node.Bounds;
        var actionPoint = new UiVector2(button.X + button.Width / 2, button.Y + button.Height / 2);
        Frame(idle with { MousePosition = actionPoint, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = actionPoint, LeftMouseReleased = true });
        check(actionCalls == 2 && Find("Open", UiAccessibilityRole.Button).Node.Focused,
            "pointer press and release belong to the button rather than the enclosing row");
        actionExpansion.Value = [];
        Frame();
        var collapsedTable = AtSpiTable.Capture(tree.CaptureSnapshot())[grid.Node.Id];
        check(collapsedTable.Dimensions.RowCount == 2 && !collapsedTable.SelectedRows.Any()
            && collapsedTable.At(1, 0)?.Element.Node.Name == "Unrelated" && table.At(1, 0)?.Element.Node.Name == "Document",
            "AT-SPI excludes collapsed cells and keeps older snapshots coherent");
        check(GridRow("Parent").Node.Focused && !tree.CaptureSnapshot().Elements.Any(item => item.Node.Name == "Open"),
            "collapsing a focused action repairs focus to its visible ancestor");
        check(Find("Tree actions", UiAccessibilityRole.TreeGrid).Node.Grid == new UiAccessibilityGridInfo(2, 1)
            && GridRow("Document").Node.VirtualItem is { IsPlaceholder: true }
            && tree.GetGridItem(grid.Node.Id, grid.RuntimeId, 1, 0, out var second) == UiAccessibilityResult.Success
            && second?.Id == Find("Unrelated", UiAccessibilityRole.GridCell).Node.Id,
            "collapsed selected rows retain native identity while their cells leave the loaded grid");
        current = information;
        expanded.Value = [];
        Frame(delta: .2f);
        Frame(delta: .2f);
        var branch = information.State.Items.Single(item => item.Value == "root");
        expanded.Value = ["root"];
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds.HasValue && branch.ExpansionMotion.Sample(context.State.TimeSeconds) == 0
            && Find("Root").Node.Expansion == UiAccessibilityExpansionState.Expanded,
            "Tree schedules a finite chevron transition while expansion semantics update immediately");
        Frame(delta: .06f);
        var fraction = branch.ExpansionMotion.Sample(context.State.TimeSeconds);
        check(fraction > 0 && fraction < 1, "Tree samples an intermediate chevron angle");
        expanded.Value = [];
        Frame(delta: 0);
        check(Math.Abs(branch.ExpansionMotion.Sample(context.State.TimeSeconds) - fraction) < .001f,
            "reversing the chevron continues from its current angle");
        Frame(delta: .2f);
        check(context.State.NextScheduledFrameTimeSeconds is null && branch.ExpansionMotion.Sample(context.State.TimeSeconds) == 0,
            "settled chevron clears its scheduled frame deadline");
        information.Options = information.Options with { ReducedMotion = true, Width = 160 };
        expanded.Value = ["root"];
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds is null && branch.ExpansionMotion.Sample(context.State.TimeSeconds) == 1,
            "reduced motion immediately reaches the expanded angle without scheduling");
        var beforeTurn = Find("Root");
        information.Transform = new(RotationDegrees: 90);
        Frame();
        var afterTurn = Find("Root");
        check(afterTurn.RuntimeId == beforeTurn.RuntimeId && afterTurn.Node.Bounds.Width == beforeTurn.Node.Bounds.Height
            && afterTurn.Node.Bounds.Height == beforeTurn.Node.Bounds.Width,
            "quarter-turned Tree transforms the row bounds and preserves accessibility identity");
        var point = afterTurn.Node.Bounds;
        var turnedChevron = new UiVector2(point.X + point.Width / 2, point.Y + 12);
        Frame(idle with { MousePosition = turnedChevron, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = turnedChevron, LeftMouseReleased = true });
        check(expanded.Value.Length == 0, "pointer input reaches the transformed Tree chevron");

        const string wrapped = "Several words wrap over more than two lines inside this narrow tree.";
        var wrappedSelection = new UiState<string[]>([]);
        var wrappedItem = new UiTreeItem("wrapped", null, wrapped);
        using var alignmentTree = new UiTreeWidget("Selector alignment", new UiState<string[]>([]).Binding,
            [wrappedItem], multiple: wrappedSelection.Binding, options: new(Width: 153));
        current = alignmentTree;
        Frame();
        var centered = alignmentTree.State.Indicators[0].Bounds;
        check(centered.Height > 32 && centered.Height == Find(wrapped).Node.Bounds.Height,
            "wrapped Tree rows center their selector by default");
        alignmentTree.Options = alignmentTree.Options with { AlignSelectorToTop = true, RightToLeft = true };
        Frame();
        var top = alignmentTree.State.Indicators[0].Bounds;
        check(top.Y == centered.Y && top.Height == 32 && top.X > centered.X,
            "top-aligned Tree selectors use the first row slot and follow RTL");
        var topPoint = new UiVector2(top.X + top.Width / 2, top.Y + top.Height / 2);
        Frame(idle with { MousePosition = topPoint, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = topPoint, LeftMouseReleased = true });
        var bottomPoint = topPoint with { Y = centered.Y + centered.Height - 5 };
        Frame(idle with { MousePosition = bottomPoint, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = bottomPoint, LeftMouseReleased = true });
        check(wrappedSelection.Value.SequenceEqual(["wrapped"]),
            "top alignment moves the selector hit area with the painted indicator");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
