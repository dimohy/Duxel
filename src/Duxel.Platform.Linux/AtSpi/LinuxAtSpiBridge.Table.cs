using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private const string TableInterface = "org.a11y.atspi.Table";
    private const string TableCellInterface = "org.a11y.atspi.TableCell";

    private GVariant TableProperty(Node node, string @interface, string property)
    {
        if (@interface == TableInterface && node.Table is { } table)
            return property switch
            {
                "NRows" => GVariant.Int(table.Dimensions.RowCount),
                "NColumns" => GVariant.Int(table.Dimensions.ColumnCount),
                "Caption" or "Summary" => ExternalReference(new("", NullPath)),
                "NSelectedRows" => GVariant.Int(table.SelectedRows.Count()),
                "NSelectedColumns" => GVariant.Int(0),
                _ => throw Invalid("Unknown table property: " + property)
            };
        if (@interface == TableCellInterface && node.TablePath is { } parent && node.Element?.Node.GridItem is { } cell)
            return property switch
            {
                "Position" => GVariant.Tuple(GVariant.Int(cell.Row), GVariant.Int(cell.Column)),
                "RowSpan" => GVariant.Int(cell.RowSpan),
                "ColumnSpan" => GVariant.Int(cell.ColumnSpan),
                "Table" => ObjectReference(parent),
                _ => throw Invalid("Unknown table cell property: " + property)
            };
        throw Invalid("The accessible does not expose the requested table interface.");
    }

    private GVariant TableMethod(Node node, string @interface, string method, GVariant input)
    {
        if (@interface == TableCellInterface && node.TablePath is not null && node.Element?.Node.GridItem is { } area)
            return method switch
            {
                "GetRowColumnSpan" => GVariant.Tuple(GVariant.Bool(true), GVariant.Int(area.Row), GVariant.Int(area.Column),
                    GVariant.Int(area.RowSpan), GVariant.Int(area.ColumnSpan)),
                "GetRowHeaderCells" or "GetColumnHeaderCells" => GVariant.Tuple(GVariant.Array("(so)")),
                _ => throw Invalid("Unknown table cell method: " + method)
            };
        if (@interface != TableInterface || node.Table is not { } table)
            throw Invalid("The accessible does not expose the requested table interface.");
        return method switch
        {
            "GetAccessibleAt" => GVariant.Tuple(TableCellReference(node, table, Int(input, 0), Int(input, 1))),
            "GetIndexAt" => GVariant.Tuple(GVariant.Int(table.Index(table.At(Int(input, 0), Int(input, 1))))),
            "GetRowAtIndex" => GVariant.Tuple(GVariant.Int(table.AtIndex(Int(input, 0))?.Area.Row ?? -1)),
            "GetColumnAtIndex" => GVariant.Tuple(GVariant.Int(table.AtIndex(Int(input, 0))?.Area.Column ?? -1)),
            "GetRowColumnExtentsAtIndex" => TableExtents(table.AtIndex(Int(input, 0))),
            "GetRowExtentAt" => GVariant.Tuple(GVariant.Int(table.At(Int(input, 0), Int(input, 1))?.Area.RowSpan ?? 0)),
            "GetColumnExtentAt" => GVariant.Tuple(GVariant.Int(table.At(Int(input, 0), Int(input, 1))?.Area.ColumnSpan ?? 0)),
            "GetRowDescription" => GVariant.Tuple(GVariant.String(table.Row(Int(input, 0))?.Node.Name ?? "")),
            "GetColumnDescription" => GVariant.Tuple(GVariant.String("")),
            "GetRowHeader" or "GetColumnHeader" => GVariant.Tuple(ExternalReference(new("", NullPath))),
            "GetSelectedRows" => GVariant.Tuple(GVariant.Array("i", table.SelectedRows.Select(GVariant.Int).ToArray())),
            "GetSelectedColumns" => GVariant.Tuple(GVariant.Array("i")),
            "IsSelected" => GVariant.Tuple(GVariant.Bool(table.At(Int(input, 0), Int(input, 1))?.Selected == true)),
            "IsRowSelected" => GVariant.Tuple(GVariant.Bool(table.Row(Int(input, 0))?.Node.Selected == true)),
            "AddRowSelection" or "RemoveRowSelection" => GVariant.Tuple(GVariant.Bool(
                table.Row(Int(input, 0)) is { } row && _nodes.TryGetValue(NodePath(row.RuntimeId), out var child)
                && SetSelected(node, child, method == "AddRowSelection"))),
            "IsColumnSelected" or "AddColumnSelection" or "RemoveColumnSelection" => GVariant.Tuple(GVariant.Bool(false)),
            _ => throw Invalid("Unknown table method: " + method)
        };
    }

    private GVariant TableCellReference(Node node, AtSpiTable table, int row, int column)
    {
        if (table.At(row, column) is { } cell)
            return ObjectReference(NodePath(cell.Element.RuntimeId));
        if (table.Dimensions.IsVirtualized && node.Element is { } grid
            && _tree.GetGridItem(grid.Node.Id, grid.RuntimeId, row, column, out var item) == UiAccessibilityResult.Success)
        {
            Refresh();
            return ObjectReference(NodePath(item!.Value.RuntimeId));
        }
        return ExternalReference(new("", NullPath));
    }

    private static GVariant TableExtents(AtSpiTable.Cell? cell) => GVariant.Tuple(GVariant.Bool(cell.HasValue),
        GVariant.Int(cell?.Area.Row ?? -1), GVariant.Int(cell?.Area.Column ?? -1),
        GVariant.Int(cell?.Area.RowSpan ?? 0), GVariant.Int(cell?.Area.ColumnSpan ?? 0),
        GVariant.Bool(cell?.Selected == true));
}
