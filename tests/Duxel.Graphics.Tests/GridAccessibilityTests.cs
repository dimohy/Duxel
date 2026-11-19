using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

internal static class GridAccessibilityTests
{
    internal static unsafe void Run(Action<bool, string> check)
    {
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        UiAccessibilityNode Node(string id, string parent = "", UiAccessibilityRole role = UiAccessibilityRole.GridCell)
            => new(id, parent, role, id, new(0, 0, 20, 20), true, false, false, Focusable: false, ValuePattern: false);
        void Publish(bool extra = false)
        {
            tree.BeginFrame();
            tree.Add(Node("grid", role: UiAccessibilityRole.Grid) with
            {
                Grid = new(extra ? 3 : 2, 2)
            });
            tree.Add(Node("row", "grid", UiAccessibilityRole.Row) with
            {
                GridItem = new("grid", 0, 0, ColumnSpan: 2)
            });
            tree.Add(Node("a", "row") with
            {
                GridItem = new("grid", 0, 0)
            });
            tree.Add(Node("b", "row") with
            {
                GridItem = new("grid", 0, 1),
                Enabled = false,
                Offscreen = true
            });
            tree.Add(Node("merged", "grid") with
            {
                GridItem = new("grid", 1, 0, ColumnSpan: 2)
            });
            if (extra)
                tree.Add(Node("extra", "grid") with
                {
                    GridItem = new("grid", 2, 0, ColumnSpan: 2)
                });
            tree.Add(Node("button", role: UiAccessibilityRole.Button));
            tree.EndFrame();
        }
        UiaNodeProvider Returned(nint pointer)
        {
            try
            {
                return (UiaNodeProvider)ComInterfaceMarshaller<IUiaSimple>.ConvertToManaged((void*)pointer)!;
            }
            finally { Marshal.Release(pointer); }
        }
        Publish();
        var provider = native.Find("grid")!;
        check(provider.GetRowCount(out var rows) == 0 && rows == 2
            && provider.GetColumnCount(out var columns) == 0 && columns == 2,
            "Grid publishes loaded row and column counts");
        check(provider.GetItem(0, 1, out var cellPointer) == 0 && Returned(cellPointer) == native.Find("b"),
            "Grid GetItem returns the offscreen disabled cell, independently of interaction availability");
        check(provider.GetItem(1, 0, out cellPointer) == 0 && Returned(cellPointer) == native.Find("merged")
            && provider.GetItem(1, 1, out cellPointer) == 0 && Returned(cellPointer) == native.Find("merged"),
            "Grid merged coordinates return one anchor provider");
        check(provider.GetItem(-1, 0, out cellPointer) == UiaNative.InvalidArgument && cellPointer == 0
            && provider.GetItem(2, 0, out cellPointer) == UiaNative.InvalidArgument && cellPointer == 0
            && provider.GetItem(0, 2, out cellPointer) == UiaNative.InvalidArgument && cellPointer == 0,
            "Grid rejects negative and out-of-range coordinates without returning a pointer");
        var cell = native.Find("b")!;
        check(cell.GetRow(out var row) == 0 && row == 0 && cell.GetColumn(out var column) == 0 && column == 1
            && cell.GetRowSpan(out var rowSpan) == 0 && rowSpan == 1 && cell.GetColumnSpan(out var columnSpan) == 0 && columnSpan == 1,
            "GridItem exposes logical row, column and both spans");
        check(cell.GetContainingGrid(out var gridPointer) == 0 && Returned(gridPointer) == provider,
            "GridItem resolves its containing grid through the published hierarchy");
        var rowProvider = native.Find("row")!;
        check(rowProvider.GetColumnSpan(out columnSpan) == 0 && columnSpan == 2,
            "Grid row exposes its complete aggregate extent while GetItem resolves individual cells");

        check(provider.GetPatternProvider(10006, out gridPointer) == 0 && gridPointer != 0,
            "Grid exposes its native Grid pattern");
        try
        {
            var iid = new Guid("b17d6187-0907-464b-a168-0ef17a1572b1");
            check(Marshal.QueryInterface(gridPointer, in iid, out var queried) == 0 && queried != 0,
                "Grid QueryInterface accepts the SDK IGridProvider IID");
            Marshal.Release(queried);
            var vtable = *(nint**)gridPointer;
            check(((delegate* unmanaged[Stdcall]<nint, int*, int>)vtable[4])(gridPointer, &rows) == 0 && rows == 2
                && ((delegate* unmanaged[Stdcall]<nint, int*, int>)vtable[5])(gridPointer, &columns) == 0 && columns == 2,
                "Grid native ABI keeps GetItem, RowCount and ColumnCount in SDK order");
            nint value;
            check(((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)vtable[3])(gridPointer, 0, 1, &value) == 0
                && Returned(value) == native.Find("b"), "Grid native ABI GetItem transfers one simple-provider reference");
        }
        finally { Marshal.Release(gridPointer); }
        check(cell.GetPatternProvider(10007, out cellPointer) == 0 && cellPointer != 0,
            "GridItem exposes its native GridItem pattern");
        try
        {
            var iid = new Guid("d02541f1-fb81-4d64-ae32-f520f8a6dbd1");
            check(Marshal.QueryInterface(cellPointer, in iid, out var queried) == 0 && queried != 0,
                "GridItem QueryInterface accepts the SDK IGridItemProvider IID");
            Marshal.Release(queried);
            var vtable = *(nint**)cellPointer;
            var values = stackalloc int[4];
            var success = true;
            for (var i = 0; i < 4; i++)
                success &= ((delegate* unmanaged[Stdcall]<nint, int*, int>)vtable[3 + i])(cellPointer, values + i) == 0;
            nint value;
            check(success && values[0] == 0 && values[1] == 1 && values[2] == 1 && values[3] == 1
                && ((delegate* unmanaged[Stdcall]<nint, nint*, int>)vtable[7])(cellPointer, &value) == 0
                && Returned(value) == provider, "GridItem native ABI matches all five SDK getters");
        }
        finally { Marshal.Release(cellPointer); }
        provider.GetPropertyValue(30030, out var available);
        provider.GetPropertyValue(30062, out var count);
        cell.GetPropertyValue(30065, out var coordinate);
        check(available.Type == 11 && available.Data.Boolean == -1 && count.Data.Integer == 2 && coordinate.Data.Integer == 1,
            "Grid and GridItem property IDs return typed availability, count and coordinate values");
        cell.GetPropertyValue(30068, out var containing);
        check(containing.Type == 13 && Returned(containing.Data.Pointer) == provider,
            "GridItem ContainingGrid property returns a simple-provider reference");
        var button = native.Find("button")!;
        check(button.GetPatternProvider(10006, out var unsupported) == 0 && unsupported == 0
            && button.GetRowCount(out _) == UiaNative.InvalidOperation && button.GetRow(out _) == UiaNative.InvalidOperation,
            "Ordinary controls neither advertise nor serve Grid patterns");

        var notifications = new List<UiAccessibilityGridChange>();
        var committed = true;
        tree.GridChanged += change =>
        {
            notifications.Add(change);
            committed &= tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element
                && tree.GetGridItem("grid", provider.RuntimeId, 2, 1, out var item) == UiAccessibilityResult.Success && item?.Id == "extra";
        };
        Publish(extra: true);
        check(notifications.Count == 1 && notifications[0].PreviousGrid?.RowCount == 2
            && notifications[0].Element.Node.Grid?.RowCount == 3 && committed,
            "Grid count notifications observe the committed metadata and cell index together");
        notifications.Clear();
        Publish(extra: true);
        check(notifications.Count == 0, "Unchanged grid metadata emits no repeated property notification");

        void Reject(Action invalid)
        {
            var revision = tree.Revision;
            var rejected = false;
            tree.BeginFrame();
            try
            {
                invalid();
                tree.EndFrame();
            }
            catch (InvalidOperationException) { rejected = true; }
            finally { tree.AbortFrame(); }
            check(rejected && tree.Revision == revision && provider.GetItem(2, 1, out var p) == 0 && Returned(p) == native.Find("extra"),
                "Invalid grid publication aborts atomically and preserves the previous cell index");
        }
        Reject(() => tree.Add(Node("grid") with { Grid = new(-1, 2) }));
        Reject(() => tree.Add(Node("grid") with { Grid = new(1, 1) }));
        Reject(() =>
        {
            tree.Add(Node("grid") with
            {
                Grid = new(1, 1)
            });
            tree.Add(Node("a", "grid") with
            {
                GridItem = new("grid", 0, 0)
            });
            tree.Add(Node("b", "grid") with
            {
                GridItem = new("grid", 0, 0)
            });
        });
        Reject(() =>
        {
            tree.Add(Node("grid") with
            {
                Grid = new(1, 1)
            });
            tree.Add(Node("a", "grid") with
            {
                GridItem = new("grid", 0, 0, int.MaxValue, int.MaxValue)
            });
        });
        Reject(() =>
        {
            tree.Add(Node("grid") with
            {
                Grid = new(1, 1)
            });
            tree.Add(Node("a") with
            {
                GridItem = new("grid", 0, 0)
            });
        });
        Reject(() =>
        {
            tree.Add(Node("grid") with
            {
                Grid = new(1, 2)
            });
            tree.Add(Node("a", "grid") with
            {
                GridItem = new("grid", 0, 0, ColumnSpan: 2)
            });
            tree.Add(Node("b", "grid") with
            {
                GridItem = new("grid", 0, 1)
            });
        });
        Reject(() =>
        {
            tree.Add(Node("grid") with
            {
                Grid = new(2, 1)
            });
            tree.Add(Node("row", "grid", UiAccessibilityRole.Row) with
            {
                GridItem = new("grid", 0, 0)
            });
            tree.Add(Node("a", "row") with
            {
                GridItem = new("grid", 1, 0)
            });
        });
        tree.BeginFrame();
        tree.EndFrame();
        check(provider.GetRowCount(out _) == UiaNative.Unavailable && cell.GetContainingGrid(out _) == UiaNative.Unavailable,
            "Removing a grid retires retained Grid and GridItem providers");
        check(provider.GetPropertyValue(30062, out var retiredCount) == UiaNative.Unavailable && retiredCount.Type == 0,
            "Retired Grid provider returns unavailable rather than publishing a fabricated zero row count");
        Publish();
        check(native.Find("grid")!.RuntimeId != provider.RuntimeId && provider.GetItem(0, 0, out _) == UiaNative.Unavailable,
            "Reintroduced grid IDs cannot revive retired provider identities");
        LargeGrid(check);
        ContainingGridChanges(check);
    }

    private static void LargeGrid(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        var bounds = new UiRect(0, 0, 1, 1);
        tree.BeginFrame();
        tree.Add(new("large", "", UiAccessibilityRole.Grid, "Large", bounds, true, false, false)
        {
            Grid = new(int.MaxValue, int.MaxValue)
        });
        tree.Add(new("span", "large", UiAccessibilityRole.GridCell, "Span", bounds, true, false, false)
        {
            GridItem = new("large", 0, 0, int.MaxValue, int.MaxValue)
        });
        tree.EndFrame();
        tree.TryGet("large", out var grid);
        check(tree.GetGridItem("large", grid.RuntimeId, int.MaxValue - 1, int.MaxValue - 1, out var cell) == UiAccessibilityResult.Success
            && cell?.Id == "span", "Merged grid extents use wide arithmetic and do not materialize their covered area");
        tree.BeginFrame();
        tree.Add(new("large", "", UiAccessibilityRole.Grid, "Empty", bounds, true, false, false)
        {
            Grid = new(0, 2)
        });
        tree.EndFrame();
        check(tree.GetGridItem("large", grid.RuntimeId, 0, 0, out _) == UiAccessibilityResult.InvalidArgument,
            "Empty grids retain their columns and reject nonexistent rows");
    }

    private static void ContainingGridChanges(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        void Publish(bool move)
        {
            var bounds = new UiRect(0, 0, 1, 1);
            tree.BeginFrame();
            tree.Add(new("first", "", UiAccessibilityRole.Grid, "First", bounds, true, false, false)
            {
                Grid = new(move ? 0 : 1, 1)
            });
            tree.Add(new("second", "", UiAccessibilityRole.Grid, "Second", bounds, true, false, false)
            {
                Grid = new(move ? 1 : 0, 1)
            });
            var parent = move ? "second" : "first";
            tree.Add(new("cell", parent, UiAccessibilityRole.GridCell, "Cell", bounds, true, false, false)
            {
                GridItem = new(parent, 0, 0)
            });
            tree.EndFrame();
        }
        Publish(false);
        tree.TryGet("cell", out var cell);
        var observed = false;
        tree.RelationshipChanged += change =>
        {
            observed = change.Property == UiAccessibilityRelationshipProperty.ContainingGrid
                && change.Previous?.Element.Node.Id == "first" && change.Current?.Element.Node.Id == "second"
                && tree.GetContainingGrid("cell", cell.RuntimeId, out var grid) == UiAccessibilityResult.Success && grid?.Id == "second";
        };
        Publish(true);
        check(observed, "ContainingGrid changes preserve old ancestry and expose the committed new runtime identity");
    }
}
