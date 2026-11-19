namespace Duxel.Core;

public sealed partial class UiVirtualGrid<TSource>
{
    private readonly record struct AccessibleCell(int Row, int Column, string Key, string Name);
    private AccessibleCell? _placeholder;
    private bool _reconcilePlaceholder;

    internal UiAccessibilityTree.Request? PrepareAccessibility(UiAccessibilityTree tree, string id, bool enabled,
        UiVirtualViewport horizontal, UiVirtualViewport vertical, double maximumX, double maximumY, ref double x, ref double y)
    {
        if (_reconcilePlaceholder)
        {
            _reconcilePlaceholder = false;
            if (_placeholder is { } item)
                _placeholder = Matches(item.Row, item.Column, item.Key)
                    ? item with { Name = ReadCellName(item.Row, item.Column) } : null;
        }
        if (tree.Take(id, UiAccessibilityAction.FindVirtualItem) is { VirtualItem.GridCoordinates : { } coordinates } lookup)
        {
            if (!Contains(coordinates.Row, coordinates.Column))
            {
                lookup.Result = UiAccessibilityResult.Unavailable;
                return null;
            }
            var cell = ReadCell(coordinates.Row, coordinates.Column);
            if (Volatile.Read(ref lookup.Canceled))
            {
                lookup.Result = UiAccessibilityResult.Timeout;
                return null;
            }
            _placeholder = cell;
            lookup.FoundVirtualItemId = UiVirtualItemContainer.Id(id, cell.Key);
            return lookup;
        }

        if (tree.Take(id, UiAccessibilityAction.RealizeVirtualItem) is not { VirtualItem.Reference : { } target } request)
            return null;
        if (!enabled)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return null;
        }
        if (!tree.TryGet(target.Id, out var entry) || entry.RuntimeId != target.RuntimeId
            || entry.Node.VirtualItem is not { } metadata || entry.Node.GridItem is not { } area
            || area.GridId != id || !Matches(area.Row, area.Column, metadata.Key))
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return null;
        }
        var visible = horizontal.Clip is { Width : > 0, Height : > 0 };
        var nextX = Math.Clamp(_columns.Item(area.Column).Start - (visible ? horizontal.LeadingInset : 0), 0, maximumX);
        var nextY = Math.Clamp(_rows.Item(area.Row).Start - (visible ? vertical.LeadingInset : 0), 0, maximumY);
        var bounds = PlaceCell(area.Row, area.Column, horizontal with { Offset = nextX }, vertical with { Offset = nextY });
        var slice = UiImmediateContext.IntersectRect(bounds, horizontal.Bounds);
        if (Volatile.Read(ref request.Canceled))
        {
            request.Result = UiAccessibilityResult.Timeout;
            return null;
        }
        if (request.VirtualRevealBounds is not null
            || UiImmediateContext.IntersectRect(slice, horizontal.Clip) is not { Width : > 0, Height : > 0 })
        {
            request.VirtualRevealBounds = slice;
            request.Consumed = false;
            return null;
        }
        x = nextX;
        y = nextY;
        request.FoundVirtualItemId = target.Id;
        return request;
    }

    internal void CompleteAccessibility(UiAccessibilityTree tree, string gridId, UiRect viewport, bool enabled,
        UiAccessibilityTree.Request? request)
    {
        if (_placeholder is { } cell)
        {
            var id = UiVirtualItemContainer.Id(gridId, cell.Key);
            if (!tree.TryGetStaged(id, out var existing))
            {
                tree.Add(new(id, gridId, UiAccessibilityRole.GridCell, cell.Name, new(viewport.X, viewport.Y, 0, 0),
                    enabled, Offscreen : true, Focused : false, Focusable : false, ValuePattern : false)
                {
                    GridItem = new(gridId, cell.Row, cell.Column),
                    VirtualItem = new(cell.Row, cell.Key, IsPlaceholder : true)
                });
            }
            else
            {
                _placeholder = null;
                if (existing.GridItem is not { } area || area.Row != cell.Row || area.Column != cell.Column)
                {
                    if (request is not null)
                        request.Result = UiAccessibilityResult.InvalidOperation;
                    return;
                }
            }
        }
        if (request is not null)
            request.Result = tree.TryGetStaged(request.FoundVirtualItemId!, out var target)
                && (request.Action == UiAccessibilityAction.FindVirtualItem || !target.Offscreen)
                ? UiAccessibilityResult.Success : UiAccessibilityResult.Unavailable;
    }

    private bool Contains(int row, int column) => (uint)row < (uint)RowCount && (uint)column < (uint)ColumnCount;

    private bool Matches(int row, int column, string key) =>
        Contains(row, column) && _source!.GetKey(row, column) == key;

    private AccessibleCell ReadCell(int row, int column)
    {
        var key = _source!.GetKey(row, column);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new(row, column, key, ReadCellName(row, column));
    }

    private string ReadCellName(int row, int column) => _source!.GetAccessibleCellName(row, column)
        ?? throw new InvalidOperationException("A virtual grid cell accessibility name cannot be null.");
}
