namespace Duxel.Core;

/// <summary>Keys identify model items. Bind updates an already owned widget without transferring ownership.</summary>
public interface IUiVirtualListSource
{
    string GetKey(int index);
    UiWidget Create(int index);
    void Bind(UiWidget widget, int index);
}

/// <summary>Provides model-backed accessibility metadata and indexed lookup for virtual items.</summary>
public interface IUiVirtualListAutomationSource : IUiVirtualListSource
{
    string GetAccessibleItemName(int index);

    /// <summary>Returns the first match from startIndex up to itemCount, or -1. AutomationId is the key from GetKey.</summary>
    int FindAccessibleItem(UiVirtualItemSearchProperty property, string value, int startIndex, int itemCount);
}

public readonly record struct UiVirtualListOptions(float ItemExtent = 32, float? Width = null, float? Height = null,
    int Overscan = 1, int MaxRealizedItems = 4096)
{
    public IUiVirtualListExtentIndex? ExtentIndex { get; init; }
    /// <summary>The scrolling axis.</summary>
    public UiScrollAxis Axis { get; init; }
    /// <summary>Horizontal item order. Inherit follows the enclosing control's flow direction.</summary>
    public UiFlowDirection FlowDirection { get; init; }
    public UiVirtualListOptions() : this(32, null, null, 1, 4096) { }
    public UiVirtualListOptions Validate()
    {
        if (Axis is not (UiScrollAxis.Vertical or UiScrollAxis.Horizontal)
            || FlowDirection is < UiFlowDirection.Inherit or > UiFlowDirection.RightToLeft
            || !float.IsFinite(ItemExtent) || ItemExtent <= 0 || Overscan < 0 || MaxRealizedItems <= 0
            || Width is { } w && (!float.IsFinite(w) || w < 0)
            || Height is { } h && (!float.IsFinite(h) || h < 0))
            throw new ArgumentOutOfRangeException(nameof(UiVirtualListOptions));
        return this;
    }
}

internal readonly record struct UiVirtualViewport(UiRect Bounds, UiRect Clip, double Offset, bool Horizontal, bool RightToLeft)
{
    internal double LeadingInset => Math.Max(0, RightToLeft
        ? ((double)Bounds.X + Bounds.Width) - ((double)Clip.X + Clip.Width)
        : Horizontal ? (double)Clip.X - Bounds.X : (double)Clip.Y - Bounds.Y);

    internal double TrailingInset => RightToLeft ? Math.Min(Bounds.Width, (double)Bounds.X + Bounds.Width - Clip.X)
        : Horizontal
        ? Math.Min(Bounds.Width, (double)Clip.X + Clip.Width - Bounds.X)
        : Math.Min(Bounds.Height, (double)Clip.Y + Clip.Height - Bounds.Y);

    internal UiRect Place(double position, float extent)
    {
        if (RightToLeft)
            return new((float)((double)Bounds.X + Bounds.Width - position - extent), Bounds.Y, extent, Bounds.Height);
        return Horizontal
            ? new(Bounds.X + (float)position, Bounds.Y, extent, Bounds.Height)
            : new(Bounds.X, Bounds.Y + (float)position, Bounds.Width, extent);
    }
}

/// <summary>Realizes a bounded keyed range and rebases rows near the visible viewport.</summary>
[UiComponent("VirtualList", UiComponentExposure.Infrastructure)]
public class UiVirtualList<TSource> : UiWidget where TSource : class, IUiVirtualListSource
{
    private TSource? _source;
    private int _itemCount;
    private UiVirtualListOptions _options;
    private double _offset;
    private bool _offsetChanged;
    private ulong _offsetRevision;
    private UiVirtualListGeometry _geometry;
    private int _first, _end, _creatingIndex, _visibleFirst;
    private bool _preserveAnchor;
    private (string Key, int Index, double WithinRow)? _anchor;
    private readonly List<(int Index, string Key, string? Name, UiWidget Widget)> _rows = [];
    private readonly Func<UiWidget> _create;
    private readonly IUiView _rowsView;
    private readonly Action<UiImmediateContext, UiVirtualViewport> _renderViewport;
    private Func<double, double>? _restoreAnchor;
    private readonly UiScrollOwner _owner = new();
    private UiVirtualItemContainer? _automation;
    private UiVirtualViewport _viewport;

    public UiVirtualList(TSource source, int itemCount, UiVirtualListOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        _source = source;
        if (source is IUiVirtualListAutomationSource automation)
            _automation = new(automation);
        _itemCount = itemCount;
        _options = (options ?? new()).Validate();
        _create = () => _source!.Create(_creatingIndex);
        _rowsView = Dux.View(RenderRows);
        _renderViewport = RenderViewport;
    }

    public int ItemCount
    {
        get => _itemCount;
        set
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (value == _itemCount)
                return;
            ThrowIfBuilding();
            _itemCount = value;
            _preserveAnchor |= _options.ExtentIndex is not null;
            _automation?.Refresh();
            RequestRebuild();
        }
    }

    public UiVirtualListOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (value == _options)
                return;
            ThrowIfBuilding();
            if (_options.ExtentIndex is null && value.ExtentIndex is not null)
                CaptureAnchor();
            if (_options.Axis != value.Axis || _options.FlowDirection != value.FlowDirection)
                _owner.Release();
            _preserveAnchor |= value.ExtentIndex is not null || _options.ExtentIndex is not null;
            _options = value;
            RequestRebuild();
        }
    }

    public double Offset
    {
        get
        {
            ThrowIfDisposed();
            return _offset;
        }
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value == _offset)
                return;
            ThrowIfBuilding();
            _offset = value;
            _offsetChanged = true;
            unchecked
            {
                _offsetRevision++;
            }
            RequestRebuild();
        }
    }

    public int RealizedCount => _rows.Count;

    /// <summary>Rebinds the current range after keys or model data change.</summary>
    public void Refresh()
    {
        RequestRebuild();
        _preserveAnchor |= _options.ExtentIndex is not null;
        _automation?.Refresh();
    }

    internal override UiWidget? ResolveLayoutRoot(UiImmediateContext ui)
    {
        ThrowIfDisposed();
        return this;
    }

    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureVirtualViewport(_options);

    protected override IUiView Build(UiBuildContext context)
    {
        _rows.Clear();
        try
        {
            for (var index = _first; index < _end; index++)
            {
                var key = _source!.GetKey(index);
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
                _creatingIndex = index;
                var widget = context.Child(key, _create);
                _source.Bind(widget, index);
                _rows.Add((index, key, _automation?.Name(index), widget));
            }
        }
        catch
        {
            _rows.Clear();
            throw;
        }
        return _rowsView;
    }

    protected override void RenderContent(UiImmediateContext ui)
    {
        var size = new UiControlContext(ui).MeasureVirtualViewport(_options);
        _geometry = new(_itemCount, _options.ItemExtent, _options.ExtentIndex);
        var revision = _offsetRevision;
        var preserve = _preserveAnchor;
        _preserveAnchor = false;
        double offset;
        try
        {
            var restoreAnchor = preserve && !_offsetChanged && _anchor is not null && _itemCount > 0
                ? _restoreAnchor ??= GetAnchoredOffset
                : null;
            var horizontal = _options.Axis == UiScrollAxis.Horizontal;
            var rightToLeft = horizontal && (_options.FlowDirection == UiFlowDirection.RightToLeft
                || _options.FlowDirection == UiFlowDirection.Inherit && ui.RightToLeft);
            if (_viewport.RightToLeft != rightToLeft)
                _owner.Release();
            offset = ui.RenderVirtualViewport("VirtualList", size, _geometry,
                _offset, _offsetChanged, horizontal, rightToLeft, _owner, _renderViewport, _automation,
                restoreAnchor, out var anchorDeferred);
            _preserveAnchor |= anchorDeferred;
            if (!anchorDeferred && _options.ExtentIndex is null)
                _anchor = null;
        }
        catch
        {
            _preserveAnchor |= preserve;
            throw;
        }
        if (_offsetRevision == revision)
        {
            _offset = offset;
            _offsetChanged = false;
        }
    }

    private double GetAnchoredOffset(double clipInset)
    {
        if (_anchor is not { } anchor)
            return _offset;
        var index = anchor.Index;
        if (index >= _geometry.Count || _source!.GetKey(index) != anchor.Key)
        {
            index = _options.ExtentIndex?.FindIndexByKey(anchor.Key, _geometry.Count) ?? -1;
            if (index < 0 || index >= _geometry.Count || _source!.GetKey(index) != anchor.Key)
                return _offset;
        }
        var row = _geometry.Item(index);
        var position = Math.Min(row.Start + anchor.WithinRow, Math.BitDecrement(row.End));
        return Math.Max(0, position - clipInset);
    }

    private void CaptureAnchor()
    {
        var rowIndex = _visibleFirst - _first;
        if (_viewport.Clip is not { Width : > 0, Height : > 0 } || rowIndex < 0 || rowIndex >= _rows.Count)
            return;
        var entry = _rows[rowIndex];
        var within = Math.Max(0, _viewport.Offset + _viewport.LeadingInset - _geometry.Item(entry.Index).Start);
        _anchor = (entry.Key, entry.Index, within);
    }

    private void RenderViewport(UiImmediateContext ui, UiVirtualViewport viewport)
    {
        var (first, end) = _geometry.OverscanRange(viewport, _options.Overscan, out _visibleFirst);
        if ((long)end - first > _options.MaxRealizedItems)
            throw new InvalidOperationException("Visible virtual rows exceed MaxRealizedItems; increase the explicit realization budget or item extent.");
        if (first != _first || end != _end)
        {
            _first = first;
            _end = end;
            RequestRebuild();
        }
        _viewport = viewport;
        RenderBuiltContent(ui);
        if (_geometry.Count == 0)
            _anchor = null;
        else if (_options.ExtentIndex is not null)
            CaptureAnchor();
    }

    private void RenderRows(UiImmediateContext ui)
    {
        foreach (var (index, key, name, widget) in _rows)
        {
            var row = _geometry.Item(index);
            var local = row.Start - _viewport.Offset;
            var bounds = _viewport.Place(local, row.Length);
            if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y))
                throw new NotSupportedException("A virtual row's own extent exceeds local coordinate capacity.");
            if (name is not null)
                ui.RenderVirtualItem(widget, index, key, name, bounds, _viewport.Bounds);
            else
                ui.RenderLayoutChild(widget, bounds, _viewport.Bounds);
        }
    }

    private protected override void ReleaseOwnedResources()
    {
        _owner.Release();
        _automation = null;
        _rows.Clear();
        _source = null;
        base.ReleaseOwnedResources();
    }
}

public static partial class Dux
{
    public static UiVirtualList<TSource> VirtualList<TSource>(TSource source, int itemCount, UiVirtualListOptions? options = null)
        where TSource : class, IUiVirtualListSource => new(source, itemCount, options);
}
