using Duxel.Core;

namespace Duxel.Platform.Linux;

/// <summary>Table coordinates, providers and selection from a single accessibility revision.</summary>
internal sealed class AtSpiTable(UiAccessibilityGridInfo dimensions)
{
    internal readonly record struct Cell(UiAccessibilityElement Element, UiAccessibilityGridItemInfo Area, bool Selected);
    private readonly Dictionary<(int Row, int Column), Cell> _cells = [];
    private readonly List<Cell> _spans = [];
    private readonly Dictionary<int, UiAccessibilityElement> _rows = [];
    internal UiAccessibilityGridInfo Dimensions { get; } = dimensions;
    internal IEnumerable<int> SelectedRows => _rows.Where(entry => entry.Value.Node.Selected == true).Select(entry => entry.Key).Order();
    internal UiAccessibilityElement? Row(int index) => _rows.TryGetValue(index, out var row) ? row : null;

    internal static Dictionary<string, AtSpiTable> Capture(UiAccessibilitySnapshot snapshot)
    {
        Dictionary<string, AtSpiTable> tables = new(StringComparer.Ordinal);
        foreach (var element in snapshot.Elements)
            if (element.Node.Grid is { } grid)
                tables.Add(element.Node.Id, new(grid));
        if (tables.Count == 0)
            return tables;
        var elements = snapshot.ElementsById;
        foreach (var element in snapshot.Elements)
        {
            if (element.Node.GridItem is not { } area)
                continue;
            var table = tables[area.GridId];
            if (element.Node.Role == UiAccessibilityRole.Row)
            {
                table._rows[area.Row] = element;
                continue;
            }
            UiAccessibilityElement? owner = null;
            for (var parentId = element.Node.ParentId; parentId != area.GridId; )
            {
                var parent = elements[parentId];
                if (parent.Node.Role == UiAccessibilityRole.TreeGridRow
                    || parent.Node.Role == UiAccessibilityRole.Row && parent.Node.GridItem?.GridId == area.GridId)
                {
                    owner = parent;
                    table._rows[area.Row] = parent;
                    break;
                }
                parentId = parent.Node.ParentId;
            }
            var cell = new Cell(element, area, element.Node.Selected == true || owner?.Node.Selected == true);
            if (area.RowSpan == 1 && area.ColumnSpan == 1)
                table._cells.Add((area.Row, area.Column), cell);
            else
                table._spans.Add(cell);
        }
        return tables;
    }

    internal Cell? At(int row, int column)
    {
        if ((uint)row >= (uint)Dimensions.RowCount || (uint)column >= (uint)Dimensions.ColumnCount)
            return null;
        if (_cells.TryGetValue((row, column), out var cell))
            return cell;
        foreach (var span in _spans)
            if (row >= span.Area.Row && row - span.Area.Row < span.Area.RowSpan
                && column >= span.Area.Column && column - span.Area.Column < span.Area.ColumnSpan)
                return span;
        return null;
    }

    internal Cell? AtIndex(int index) => index >= 0 && Dimensions.ColumnCount > 0
        ? At(index / Dimensions.ColumnCount, index % Dimensions.ColumnCount) : null;

    internal int Index(Cell? cell)
    {
        if (cell is not { } value)
            return -1;
        var index = (long)value.Area.Row * Dimensions.ColumnCount + value.Area.Column;
        return index <= int.MaxValue ? (int)index : -1;
    }
}
