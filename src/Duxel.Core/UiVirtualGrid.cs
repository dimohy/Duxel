namespace Duxel.Core;

/// <summary>A model-backed cell factory. Keys must be unique throughout the grid.</summary>
public interface IUiVirtualGridSource
{
    string GetKey(int row, int column);
    UiWidget Create(int row, int column);
    void Bind(UiWidget widget, int row, int column);
    string GetAccessibleCellName(int row, int column) => FormattableString.Invariant($"Row {row + 1}, column {column + 1}");
}

/// <summary>Distance from the leading horizontal edge and the top of a virtual grid, in UI units.</summary>
public readonly record struct UiVirtualGridOffset(double X, double Y);

public readonly record struct UiVirtualGridOptions(float RowHeight = 32, float ColumnWidth = 120, float? Width = null,
    float? Height = null, int Overscan = 1, int MaxRealizedCells = 4096)
{
    public IUiVirtualListExtentIndex? RowExtents { get; init; }
    public IUiVirtualListExtentIndex? ColumnExtents { get; init; }
    public UiFlowDirection FlowDirection { get; init; }

    public UiVirtualGridOptions() : this(32, 120, null, null, 1, 4096) { }

    internal UiVirtualGridOptions Validate()
    {
        if (!float.IsFinite(RowHeight) || RowHeight <= 0 || !float.IsFinite(ColumnWidth) || ColumnWidth <= 0
            || Width is { } width && (!float.IsFinite(width) || width < 0)
            || Height is { } height && (!float.IsFinite(height) || height < 0)
            || Overscan < 0 || MaxRealizedCells <= 0 || FlowDirection is < UiFlowDirection.Inherit or > UiFlowDirection.RightToLeft)
            throw new ArgumentOutOfRangeException(nameof(UiVirtualGridOptions));
        return this;
    }
}

/// <summary>Realizes only the viewport's rectangular cell range plus overscan on both axes.</summary>
[UiComponent("VirtualGrid", UiComponentExposure.Infrastructure)]
public sealed partial class UiVirtualGrid<TSource> : UiWidget where TSource : class, IUiVirtualGridSource
{
    private TSource? _source;
    private readonly UiVirtualGridOptions _options;
    private readonly UiScrollOwner _owner = new();
    private readonly List<(int Row, int Column, string Key, string Name, UiWidget Widget)> _cells = [];
    private readonly Func<UiWidget> _create;
    private readonly IUiView _cellView;
    private (int Row, int Column) _creating;
    private (int First, int End) _rowRange, _columnRange;
    private UiVirtualListGeometry _rows, _columns;
    private UiVirtualViewport _horizontal, _vertical;
    private UiVirtualGridOffset _offset;
    private ulong _offsetRevision;
    private bool _offsetChanged;

    public UiVirtualGrid(TSource source, int rowCount, int columnCount, UiVirtualGridOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);
        RowCount = rowCount;
        ColumnCount = columnCount;
        _source = source;
        _options = (options ?? new()).Validate();
        _create = () => _source!.Create(_creating.Row, _creating.Column);
        _cellView = Dux.View(RenderCells);
    }

    public int RowCount { get; private set; }
    public int ColumnCount { get; private set; }
    public int RealizedCount => _cells.Count;

    public UiVirtualGridOffset Offset
    {
        get { ThrowIfDisposed(); return _offset; }
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value.X) || value.X < 0 || !double.IsFinite(value.Y) || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value == _offset)
                return;
            RequestRebuild();
            _offset = value;
            _offsetChanged = true;
            unchecked { _offsetRevision++; }
        }
    }

    public void SetDimensions(int rowCount, int columnCount)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);
        if (rowCount == RowCount && columnCount == ColumnCount)
            return;
        RequestRebuild();
        RowCount = rowCount;
        ColumnCount = columnCount;
        _reconcilePlaceholder = true;
    }

    /// <summary>Rebinds realized cells after an in-place model change.</summary>
    public void Refresh()
    {
        RequestRebuild();
        _reconcilePlaceholder = true;
    }

    internal override UiWidget ResolveLayoutRoot(UiImmediateContext ui)
    {
        ThrowIfDisposed();
        return this;
    }

    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureVirtualViewport(_options.Width, _options.Height);

    protected override IUiView Build(UiBuildContext context)
    {
        _cells.Clear();
        try
        {
            for (var row = _rowRange.First; row < _rowRange.End; row++)
            for (var column = _columnRange.First; column < _columnRange.End; column++)
            {
                var key = _source!.GetKey(row, column);
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
                _creating = (row, column);
                var widget = context.Child(key, _create);
                _source.Bind(widget, row, column);
                _cells.Add((row, column, key, ReadCellName(row, column), widget));
            }
        }
        catch
        {
            _cells.Clear();
            throw;
        }
        return _cellView;
    }

    protected override void RenderContent(UiImmediateContext ui)
    {
        var size = MeasureContent(new UiControlContext(ui));
        var empty = RowCount == 0 || ColumnCount == 0;
        _rows = new(empty ? 0 : RowCount, _options.RowHeight, empty ? null : _options.RowExtents);
        _columns = new(empty ? 0 : ColumnCount, _options.ColumnWidth, empty ? null : _options.ColumnExtents);
        var rtl = _options.FlowDirection == UiFlowDirection.RightToLeft
            || _options.FlowDirection == UiFlowDirection.Inherit && ui.RightToLeft;
        if (rtl != _horizontal.RightToLeft)
            _owner.Release();
        var revision = _offsetRevision;
        var next = ui.RenderVirtualGrid(size, _columns.ContentExtent, _rows.ContentExtent, _offset, _offsetChanged,
            rtl, _owner, this);
        if (_offsetRevision == revision)
        {
            _offset = next;
            _offsetChanged = false;
        }
    }

    internal void RenderViewport(UiImmediateContext ui, UiVirtualViewport horizontal, UiVirtualViewport vertical)
    {
        var rows = _rows.OverscanRange(vertical, _options.Overscan, out _);
        var columns = _columns.OverscanRange(horizontal, _options.Overscan, out _);
        var count = (long)(rows.End - rows.First) * (columns.End - columns.First);
        if (count > _options.MaxRealizedCells)
            throw new InvalidOperationException("Visible virtual cells exceed MaxRealizedCells.");
        if (rows != _rowRange || columns != _columnRange)
        {
            _rowRange = rows;
            _columnRange = columns;
            RequestRebuild();
        }
        _horizontal = horizontal;
        _vertical = vertical;
        RenderBuiltContent(ui);
    }

    private void RenderCells(UiImmediateContext ui)
    {
        foreach (var (row, column, key, name, widget) in _cells)
        {
            var bounds = PlaceCell(row, column, _horizontal, _vertical);
            if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y))
                throw new NotSupportedException("A virtual cell exceeds local coordinate capacity.");
            if (ui.AccessibilityTree is not null)
                ui.RenderVirtualGridCell(widget, row, column, key, name, bounds, _horizontal.Bounds);
            else
                ui.RenderLayoutChild(widget, bounds, _horizontal.Bounds);
        }
    }

    private UiRect PlaceCell(int row, int column, UiVirtualViewport horizontal, UiVirtualViewport vertical)
    {
        var x = _columns.Item(column);
        var y = _rows.Item(row);
        return horizontal.Place(x.Start - horizontal.Offset, x.Length) with
        {
            Y = vertical.Place(y.Start - vertical.Offset, y.Length).Y,
            Height = y.Length
        };
    }

    private protected override void ReleaseOwnedResources()
    {
        _owner.Release();
        _placeholder = null;
        _cells.Clear();
        _source = null;
        base.ReleaseOwnedResources();
    }
}

public static partial class Dux
{
    public static UiVirtualGrid<TSource> VirtualGrid<TSource>(TSource source, int rowCount, int columnCount,
        UiVirtualGridOptions? options = null) where TSource : class, IUiVirtualGridSource => new(source, rowCount, columnCount, options);
}
