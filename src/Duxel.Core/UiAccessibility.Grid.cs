namespace Duxel.Core;

/// <summary>Loaded logical rows and columns, including offscreen cells.</summary>
public readonly record struct UiAccessibilityGridInfo(int RowCount, int ColumnCount)
{
    public bool IsVirtualized { get; init; }
}

/// <summary>Zero-based logical coordinates. Column zero is the leading column in either text direction.</summary>
public readonly record struct UiAccessibilityGridItemInfo(string GridId, int Row, int Column, int RowSpan = 1, int ColumnSpan = 1);

public readonly record struct UiAccessibilityGridChange(UiAccessibilityElement Element,
    UiAccessibilityGridInfo? PreviousGrid, UiAccessibilityGridItemInfo? PreviousItem);

public sealed partial class UiAccessibilityTree
{
    public event Action<UiAccessibilityGridChange>? GridChanged;
    private List<UiAccessibilityGridChange>? _gridChanges;
    private sealed class GridIndex
    {
        internal readonly Dictionary<(int Row, int Column), UiAccessibilityItemReference> Cells = [];
        internal readonly HashSet<int> Rows = [];
        internal readonly List<(UiAccessibilityGridItemInfo Area, UiAccessibilityItemReference Item)> Spans = [];
        internal long Area;

        internal void Add(UiAccessibilityGridItemInfo area, UiAccessibilityItemReference item)
        {
            static bool Overlaps(UiAccessibilityGridItemInfo a, UiAccessibilityGridItemInfo b) =>
                a.Row < (long)b.Row + b.RowSpan && b.Row < (long)a.Row + a.RowSpan
                && a.Column < (long)b.Column + b.ColumnSpan && b.Column < (long)a.Column + a.ColumnSpan;
            foreach (var span in Spans)
                if (Overlaps(area, span.Area))
                    throw new InvalidOperationException("Accessibility grid cells cannot overlap.");
            if (area.RowSpan == 1 && area.ColumnSpan == 1)
            {
                if (!Cells.TryAdd((area.Row, area.Column), item))
                    throw new InvalidOperationException("Accessibility grid cells must have unique coordinates.");
            }
            else
            {
                foreach (var cell in Cells.Keys)
                    if (Overlaps(area, new(area.GridId, cell.Row, cell.Column)))
                        throw new InvalidOperationException("Accessibility grid cells cannot overlap.");
                Spans.Add((area, item));
            }
            Area = checked(Area + (long)area.RowSpan * area.ColumnSpan);
        }
    }

    private Dictionary<string, GridIndex> _publishedGrids = new(StringComparer.Ordinal);
    private Dictionary<string, GridIndex> _stagedGrids = new(StringComparer.Ordinal);

    private void PrepareGridPublication()
    {
        _stagedGrids.Clear();
        foreach (var id in _order)
            if (_entries[id].Staged.Grid is { } grid)
            {
                if (grid.RowCount < 0 || grid.ColumnCount < 0)
                    throw new InvalidOperationException("Accessibility grid dimensions cannot be negative.");
                _stagedGrids.Add(id, new());
            }
        foreach (var id in _order)
        {
            var entry = _entries[id];
            var node = entry.Staged;
            if (node.GridItem is not { } area)
                continue;
            if (string.IsNullOrEmpty(area.GridId) || !_stagedGrids.TryGetValue(area.GridId, out var index))
                throw new InvalidOperationException("A grid item requires its containing grid in the same frame.");
            var grid = _entries[area.GridId].Staged.Grid!.Value;
            if (area.Row < 0 || area.Column < 0 || area.RowSpan <= 0 || area.ColumnSpan <= 0
                || (long)area.Row + area.RowSpan > grid.RowCount || (long)area.Column + area.ColumnSpan > grid.ColumnCount)
                throw new InvalidOperationException("A grid item's extent must be inside its containing grid.");
            var parent = node.ParentId;
            var remaining = _order.Count;
            while (parent != area.GridId && parent.Length != 0 && --remaining > 0)
            {
                var ancestor = _entries[parent].Staged;
                if (ancestor.Role == UiAccessibilityRole.Row && ancestor.GridItem is { } row && row.GridId == area.GridId
                    && (area.Row != row.Row || area.RowSpan != 1))
                    throw new InvalidOperationException("A grid cell's coordinates must agree with its containing row.");
                parent = ancestor.ParentId;
            }
            if (parent != area.GridId)
                throw new InvalidOperationException("A grid item must descend from its containing grid.");
            // A row describes its children's aggregate extent; GetItem returns its cells.
            if (node.Role == UiAccessibilityRole.Row)
            {
                if (area.RowSpan != 1 || area.Column != 0 || area.ColumnSpan != grid.ColumnCount)
                    throw new InvalidOperationException("A grid row must describe exactly one complete row.");
                if (!index.Rows.Add(area.Row))
                    throw new InvalidOperationException("Accessibility grid rows must have unique coordinates.");
            }
            else
                index.Add(area, new(id, entry.RuntimeId));
        }
        foreach (var pair in _stagedGrids)
        {
            var grid = _entries[pair.Key].Staged.Grid!.Value;
            if (!grid.IsVirtualized && pair.Value.Area != (long)grid.RowCount * grid.ColumnCount)
                throw new InvalidOperationException("Every loaded grid coordinate requires a cell provider, including empty content.");
        }
    }

    /// <summary>Resolves the current item's containing grid and runtime identity atomically.</summary>
    public UiAccessibilityResult GetContainingGrid(string id, long runtimeId, out UiAccessibilityItemReference? grid)
    {
        grid = null;
        _gate.EnterReadLock();
        try
        {
            if (_disposed || !_published.TryGetValue(id, out var item) || item.RuntimeId != runtimeId)
                return UiAccessibilityResult.Unavailable;
            if (item.Node.GridItem is not { } coordinates)
                return UiAccessibilityResult.InvalidOperation;
            var container = _published[coordinates.GridId];
            grid = new(container.Node.Id, container.RuntimeId);
            return UiAccessibilityResult.Success;
        }
        finally { _gate.ExitReadLock(); }
    }

    /// <summary>Reads a cell and its runtime identity from one committed grid snapshot.</summary>
    public UiAccessibilityResult GetGridItem(string id, long runtimeId, int row, int column,
        out UiAccessibilityItemReference? item)
    {
        item = null;
        _gate.EnterReadLock();
        try
        {
            if (_disposed || !_published.TryGetValue(id, out var entry) || entry.RuntimeId != runtimeId)
                return UiAccessibilityResult.Unavailable;
            if (entry.Node.Grid is not { } grid)
                return UiAccessibilityResult.InvalidOperation;
            if (row < 0 || column < 0 || row >= grid.RowCount || column >= grid.ColumnCount)
                return UiAccessibilityResult.InvalidArgument;
            var index = _publishedGrids[id];
            if (index.Cells.TryGetValue((row, column), out var cell))
                item = cell;
            else
                foreach (var span in index.Spans)
                    if (row >= span.Area.Row && row < (long)span.Area.Row + span.Area.RowSpan
                        && column >= span.Area.Column && column < (long)span.Area.Column + span.Area.ColumnSpan)
                    {
                        item = span.Item;
                        break;
                    }
            if (item is null && !grid.IsVirtualized)
                throw new InvalidOperationException("The committed grid index is incomplete.");
            if (item is not null)
                return UiAccessibilityResult.Success;
        }
        finally { _gate.ExitReadLock(); }
        var result = QueueAction(id, runtimeId, UiAccessibilityAction.FindVirtualItem, null, null, wait : true,
            new VirtualItemAction(UiVirtualItemSearchProperty.Next, null) { GridCoordinates = (row, column) }, out var found);
        if (result != UiAccessibilityResult.Success)
            return result;
        if (found is null || !TryGet(found, out var element) || element.Node.GridItem is not { } area
            || area.GridId != id || area.Row != row || area.Column != column)
            return UiAccessibilityResult.Unavailable;
        item = new(found, element.RuntimeId);
        return UiAccessibilityResult.Success;
    }
}
