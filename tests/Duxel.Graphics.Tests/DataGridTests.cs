using Duxel.Core;

internal static class DataGridTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.InputProfilingEnabled = true;
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var rows = Enumerable.Range(0, 2_000).Select(i => new Row($"key-{i}", $"File {i}", 2_000 - i)).ToArray();
        var textReads = 0;
        UiDataGridColumn<Row>[] columns =
        [
            new("Name", row => { textReads++; return row.Name; }, 2,
                Edit: (Row row, string text, out Row replacement) =>
                {
                    replacement = row with { Name = text };
                    return text.Length != 0;
                }),
            new("Size", row => $"{row.Size} KB", Compare: (a, b) => a.Size.CompareTo(b.Size), Alignment: UiItemHorizontalAlign.Right)
        ];
        using var grid = Fluent.DataGrid(rows, row => row.Key, columns,
            new(Width: 400, Height: 180, Selection: UiDataGridSelection.Multiple));
        grid.AccessibleName = "Files";
        using var single = Fluent.DataGrid(rows, row => row.Key, columns, new(Selection: UiDataGridSelection.Single));
        single.SetSelected(rows[0].Key, true);
        single.SetSelected(rows[1].Key, true);
        check(single.SelectedCount == 1 && single.IsSelected(rows[1].Key) && !single.IsSelected(rows[0].Key),
            "DataGrid single selection replaces the previously selected key");
        var duplicateRejected = false;
        try { grid.SetRows([rows[0], rows[0]]); }
        catch (ArgumentException) { duplicateRejected = true; }
        check(duplicateRejected && grid.RowCount == 2_000, "DataGrid rejects duplicate keys without replacing its snapshot");

        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        using var provider = Fluent.FluentProvider(grid);
        var screen = new Screen(provider);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(520, 260), new(1, 1)), input ?? idle, new(0, 0, 520, 260), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        List<UiAccessibilityNode> Nodes()
        {
            var nodes = new List<UiAccessibilityNode>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    nodes.Add(element.Node);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        UiAccessibilityNode Find(string name) => Nodes().Single(node => node.Name == name);
        void Click(string name)
        {
            var bounds = Find(name).Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }

        Frame();
        check(Find("Files") is { Role: UiAccessibilityRole.Grid, Grid: { RowCount: 2_001, ColumnCount: 3, IsVirtualized: true },
            SupportsSelection: true, CanSelectMultiple: true } && textReads == 4,
            "DataGrid exposes its dimensions and formats only the four visible rows");
        var nameHeader = Find("Name");
        check(nameHeader is { Role: UiAccessibilityRole.GridCell, Focusable: false }
            && Find("Size") is { Role: UiAccessibilityRole.Button, Focusable: true },
            "only sortable DataGrid headers are actionable");
        Click("File 0");
        check(grid.IsSelected(rows[0].Key) && grid.SelectedCount == 1
            && Find("Select all rows") is { Role: UiAccessibilityRole.Checkbox, ToggleState: UiAccessibilityToggleState.Indeterminate },
            "DataGrid pointer input selects a row and exposes mixed checkbox state on the header");
        Click("Size");
        check(grid.SortColumn == 1 && !grid.SortDescending && Find("File 1999").GridItem?.Row == 1
            && grid.IsSelected(rows[0].Key), "DataGrid sorts numerically and retains selection by row key");
        Click("Size, ascending");
        check(grid.SortDescending && Find("File 0").GridItem?.Row == 1,
            "activating the same header reverses DataGrid sort direction");
        Click("File 0");
        Key(UiKey.DownArrow);
        check(Find("File 1").Focused, "DataGrid arrow keys move focus to the next row");
        Key(UiKey.Space);
        check(grid.IsSelected(rows[1].Key) && grid.SelectedCount == 1, "DataGrid Space toggles the focused row");
        Key(UiKey.PageDown);
        check(Find("File 4").Focused && Find("Name").Bounds == nameHeader.Bounds,
            "DataGrid Page Down reveals the focused row and keeps its headers fixed");
        Click("Select all rows");
        check(grid.SelectedCount == grid.RowCount && Find("Clear selection").ToggleState == UiAccessibilityToggleState.On,
            "DataGrid select all includes rows outside the viewport and checks its header");
        Click("Clear selection");
        check(grid.SelectedCount == 0 && Find("Select all rows").ToggleState == UiAccessibilityToggleState.Off,
            "DataGrid clears selection across its entire snapshot and unchecks its header");
        var before = Find("Name").Bounds;
        var beforeOffset = Find("Virtual list").Scroll!.Value.VerticalOffset;
        var wheel = idle with { MousePosition = new(130, 95), MouseWheel = -.25f };
        context.NewInputBatch(new(1f / 60, new(520, 260), new(1, 1)), Enumerable.Repeat(wheel, 48).ToArray(),
            new(0, 0, 520, 260), UiTextSettings.Default);
        context.Render(screen);
        using (var draw = context.GetDrawData()) { }
        var metrics = context.InputProcessingMetrics;
        var afterOffset = Find("Virtual list").Scroll!.Value.VerticalOffset;
        check(metrics is { RequestedSteps: 48, CompletedSteps: 2 }
            && Find("Name").Bounds == before && afterOffset > beforeOffset
            && Nodes().Where(node => node.GridItem?.Row > 0).Min(node => node.GridItem!.Value.Row) == (int)(afterOffset / 36) + 1,
            "DataGrid combines wheel bursts and presents the final rows under the unchanged header");
        grid.Sort(1);
        Frame();
        Click("File 1999");
        Key(UiKey.F2);
        var editor = Nodes().Single(node => node is { Role: UiAccessibilityRole.Edit, Name: "Name" });
        check(grid.IsEditing && editor.Focused && editor.Value == "File 1999",
            "DataGrid F2 opens and focuses the Fluent editor with the current cell text");
        Key(UiKey.Backspace);
        Key(UiKey.Enter);
        check(grid.IsEditing, "DataGrid keeps the editor open when validation rejects the draft");
        Key(UiKey.Escape);
        check(!grid.IsEditing && Find("File 1999").Focused, "DataGrid Escape discards the draft and returns focus to the cell");
        (string Key, int Column, Row Item)? change = null;
        grid.CellEdited += (key, column, row) => change = (key, column, row);
        Key(UiKey.Enter);
        Frame(idle with { CharEvents = [new('X')] });
        Key(UiKey.Enter);
        check(!grid.IsEditing && change is { Key: "key-1999", Column: 0, Item.Name: "X" }
            && Find("X").Focused, "DataGrid commits a validated edit, publishes the replacement and restores cell focus");
        var selectedBeforeDirection = grid.IsSelected(rows[^1].Key);
        var ltrHeader = Find("Name").Bounds;
        var ltrCell = Find("X").Bounds;
        provider.Direction = UiFlowDirection.RightToLeft;
        Frame();
        var rtlName = Find("Name");
        var rtlSize = Find("Size, ascending");
        var rtlValue = Find("X");
        check(Find("Select all rows").Bounds.X > rtlName.Bounds.X && rtlName.Bounds.X > rtlSize.Bounds.X
            && rtlName.Bounds.X == rtlValue.Bounds.X && rtlName.Bounds.Width == rtlValue.Bounds.Width,
            "DataGrid mirrors the selection column and aligns headers and rows inside an RTL FluentProvider");
        check(rtlValue.Focused && rtlValue.GridItem?.Column == 1 && grid.IsSelected(rows[^1].Key) == selectedBeforeDirection,
            "DataGrid preserves the logical column, selected key and focused cell when direction changes");
        Key(UiKey.LeftArrow);
        check(Find("1 KB").Focused, "DataGrid Left moves to the displayed cell on the left in RTL");
        Key(UiKey.RightArrow);
        check(Find("X").Focused, "DataGrid Right moves to the displayed cell on the right in RTL");
        provider.Direction = UiFlowDirection.LeftToRight;
        Frame();
        check(Find("Name").Bounds == ltrHeader && Find("X").Bounds == ltrCell && Find("X").Focused,
            "DataGrid restores LTR bounds without losing the focused cell");
        grid.SetSelected(rows[1].Key, true);
        grid.SetRows([rows[1], rows[2]]);
        check(grid.IsSelected(rows[1].Key) && grid.SortColumn == 1 && grid.RowCount == 2, "DataGrid row replacement preserves sorting and surviving selection");
    }

    private sealed record Row(string Key, string Name, int Size);

    private sealed class Screen(UiWidget grid) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            grid.Render(ui);
        }
    }
}
