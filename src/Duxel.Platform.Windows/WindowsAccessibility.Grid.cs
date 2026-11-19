using Duxel.Core;
using System.Runtime.InteropServices;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private void RaiseGridChanged(UiAccessibilityGridChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
            return;
        var source = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The grid event source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(source);
        try
        {
            void Raise(int property, UiaVariant before, UiaVariant after) =>
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, property, before, after));
            var grid = change.Element.Node.Grid;
            var item = change.Element.Node.GridItem;
            if (change.PreviousGrid.HasValue != grid.HasValue)
                Raise(30030, UiaVariant.Boolean(change.PreviousGrid.HasValue), UiaVariant.Boolean(grid.HasValue));
            if (change.PreviousItem.HasValue != item.HasValue)
                Raise(30029, UiaVariant.Boolean(change.PreviousItem.HasValue), UiaVariant.Boolean(item.HasValue));
            if (change.PreviousGrid is { } oldGrid && grid is { } newGrid)
            {
                if (oldGrid.RowCount != newGrid.RowCount)
                    Raise(30062, UiaVariant.Integer(oldGrid.RowCount), UiaVariant.Integer(newGrid.RowCount));
                if (oldGrid.ColumnCount != newGrid.ColumnCount)
                    Raise(30063, UiaVariant.Integer(oldGrid.ColumnCount), UiaVariant.Integer(newGrid.ColumnCount));
            }
            if (change.PreviousItem is { } oldItem && item is { } newItem)
            {
                if (oldItem.Row != newItem.Row)
                    Raise(30064, UiaVariant.Integer(oldItem.Row), UiaVariant.Integer(newItem.Row));
                if (oldItem.Column != newItem.Column)
                    Raise(30065, UiaVariant.Integer(oldItem.Column), UiaVariant.Integer(newItem.Column));
                if (oldItem.RowSpan != newItem.RowSpan)
                    Raise(30066, UiaVariant.Integer(oldItem.RowSpan), UiaVariant.Integer(newItem.RowSpan));
                if (oldItem.ColumnSpan != newItem.ColumnSpan)
                    Raise(30067, UiaVariant.Integer(oldItem.ColumnSpan), UiaVariant.Integer(newItem.ColumnSpan));
            }
        }
        finally { Marshal.Release(pointer); }
    }
}

internal abstract partial class UiaControlProvider
{
    public int GetItem(int row, int column, out nint value)
    {
        value = 0;
        if (!Read(out _))
            return UiaNative.Unavailable;
        var result = Host.Tree.GetGridItem(Id!, RuntimeId, row, column, out var reference);
        if (result != UiAccessibilityResult.Success)
            return WindowsAccessibility.Result(result);
        if (Host.Find(reference!.Value.Id) is not { } cell || cell.RuntimeId != reference.Value.RuntimeId)
            return UiaNative.Unavailable;
        value = UiaNative.Pointer<IUiaSimple>(cell);
        return 0;
    }

    public int GetRowCount(out int value) => ReadGridDimension(false, out value);
    public int GetColumnCount(out int value) => ReadGridDimension(true, out value);
    private int ReadGridDimension(bool column, out int value)
    {
        value = 0;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        if (entry.Node.Grid is not { } grid)
            return UiaNative.InvalidOperation;
        value = column ? grid.ColumnCount : grid.RowCount;
        return 0;
    }

    public int GetRow(out int value) => ReadGridItemCoordinate(0, out value);
    public int GetColumn(out int value) => ReadGridItemCoordinate(1, out value);
    public int GetRowSpan(out int value) => ReadGridItemCoordinate(2, out value);
    public int GetColumnSpan(out int value) => ReadGridItemCoordinate(3, out value);
    private int ReadGridItemCoordinate(int property, out int value)
    {
        value = 0;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        if (entry.Node.GridItem is not { } item)
            return UiaNative.InvalidOperation;
        value = property switch
        {
            0 => item.Row,
            1 => item.Column,
            2 => item.RowSpan,
            3 => item.ColumnSpan,
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
        return 0;
    }

    public int GetContainingGrid(out nint value)
    {
        value = 0;
        if (!Read(out _))
            return UiaNative.Unavailable;
        var result = Host.Tree.GetContainingGrid(Id!, RuntimeId, out var reference);
        if (result != UiAccessibilityResult.Success)
            return WindowsAccessibility.Result(result);
        if (Host.Find(reference!.Value.Id) is not { } grid || grid.RuntimeId != reference.Value.RuntimeId)
            return UiaNative.Unavailable;
        value = UiaNative.Pointer<IUiaSimple>(grid);
        return 0;
    }
}
