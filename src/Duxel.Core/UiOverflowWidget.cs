namespace Duxel.Core;

/// <summary>Owns all children but renders only the priority-preserving subset that fits its bounded main axis.</summary>
public class UiOverflowWidget<TAxis> : UiArrangedChildrenWidget where TAxis : IUiLayoutAxis
{
    private int _count;
    private int[] _priorities;
    private UiVector2[] _sizes;
    private UiOverflowItem[] _items;
    private bool[] _visible;
    private UiOverflowOptions _options;
    private UiOverflowResult? _result;
    protected override bool CanRetainArrangement => TAxis.CanRetainMeasurement;
    public int ItemCount => _count;
    public ReadOnlySpan<UiWidget> Items => Children[.._count];
    public ReadOnlySpan<int> Priorities => _priorities;
    public ReadOnlySpan<bool> Visibility => _result is null ? [] : _visible;
    public UiOverflowResult? Result => _result;
    public UiOverflowOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }

    public UiOverflowWidget(UiWidget[] children, UiWidget indicator, UiOverflowOptions options = default,
        int[]? priorities = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(Join(children, indicator, options, priorities), identity)
    {
        _count = children.Length;
        _priorities = priorities is null ? new int[_count] : (int[])priorities.Clone();
        _sizes = new UiVector2[_count + 1];
        _items = new UiOverflowItem[_count];
        _visible = new bool[_count];
        _options = options;
    }
    private static UiWidget[] Join(UiWidget[] children, UiWidget indicator, UiOverflowOptions options, int[]? priorities)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(indicator);
        options.Validate();
        if (priorities is not null && priorities.Length != children.Length)
            throw new ArgumentException("Each overflow child requires one priority.", nameof(priorities));
        var all = new UiWidget[checked(children.Length + 1)];
        children.CopyTo(all, 0);
        all[^1] = indicator;
        return all;
    }
    public void SetPriorities(ReadOnlySpan<int> priorities)
    {
        ThrowIfDisposed();
        if (priorities.Length != _count) throw new ArgumentException("Each overflow child requires one priority.", nameof(priorities));
        if (_priorities.AsSpan().SequenceEqual(priorities)) return;
        ThrowIfBuilding();
        priorities.CopyTo(_priorities);
        RequestRebuild();
    }
    protected override bool ShouldRenderChild(int index) => index == _count
        ? _result is { IsOverflowing : true, IndicatorExtent : > 0 } : _visible[index];

    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var constraints = context.LayoutConstraints.Validate();
        var horizontal = TAxis.Horizontal;
        var main = horizontal ? constraints.MaxWidth : constraints.MaxHeight;
        if (!float.IsFinite(main)) throw new InvalidOperationException("Overflow requires a bounded main axis.");
        var cross = horizontal ? constraints.MaxHeight : constraints.MaxWidth;
        var childConstraints = horizontal ? new UiBoxConstraints(0, float.PositiveInfinity, 0, cross)
            : new(0, cross, 0, float.PositiveInfinity);
        double total = Math.Max(0, _count - 1) * (double)_options.Spacing;
        for (var i = 0; i < _count; i++)
        {
            _sizes[i] = context.MeasureParentDataChild(Children[i], null, childConstraints);
            var extent = horizontal ? _sizes[i].X : _sizes[i].Y;
            _items[i] = new(extent, _priorities[i]);
            total += extent;
        }
        _sizes[_count] = total > main ? context.MeasureParentDataChild(Children[_count], null, childConstraints) : default;
        var indicator = horizontal ? _sizes[_count].X : _sizes[_count].Y;
        _result = UiOverflowLayout.Resolve(_items, main, indicator, _visible, _options);
        var indicatorCross = horizontal ? _sizes[_count].Y : _sizes[_count].X;
        var crossExtent = _result.Value.IsOverflowing ? indicatorCross : 0;
        for (var i = 0; i < _count; i++)
            if (_visible[i]) crossExtent = MathF.Max(crossExtent, horizontal ? _sizes[i].Y : _sizes[i].X);
        var size = constraints.Constrain(horizontal ? new UiVector2(main, crossExtent) : new(crossExtent, main));
        var allocatedCross = horizontal ? size.Y : size.X;
        double cursor = 0;
        var laidOut = 0;
        for (var i = 0; i <= _count; i++)
        {
            if (!ShouldRenderChild(i))
            {
                bounds[i] = default;
                continue;
            }
            if (laidOut++ != 0) cursor += _options.Spacing;
            var extent = i == _count ? _result.Value.IndicatorExtent : horizontal ? _sizes[i].X : _sizes[i].Y;
            var childCross = horizontal ? _sizes[i].Y : _sizes[i].X;
            var mainPosition = horizontal && _options.RightToLeft ? main - cursor - extent : cursor;
            var crossPosition = MathF.Max(0, (allocatedCross - childCross) * .5f);
            if (!double.IsFinite(mainPosition) || mainPosition < -float.MaxValue || mainPosition > float.MaxValue)
                throw new InvalidOperationException("Overflow child position exceeds finite coordinate capacity.");
            bounds[i] = horizontal ? new((float)mainPosition, crossPosition, extent, childCross)
                : new(crossPosition, (float)mainPosition, childCross, extent);
            cursor += extent;
        }
        return size;
    }
    private protected override void ReleaseOwnedResources()
    {
        _count = 0;
        _priorities = [];
        _sizes = [];
        _items = [];
        _visible = [];
        _result = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiOverflowWidget<>), typeof(UiHorizontalAxis))]
[UiComponent("Overflow", UiComponentExposure.Infrastructure)]
public partial class UiOverflow;
[UiWidgetStyle(typeof(UiOverflowWidget<>), typeof(UiVerticalAxis))]
[UiComponent("VerticalOverflow", UiComponentExposure.Infrastructure)]
public partial class UiVerticalOverflow;

public static partial class Dux
{
    public static UiOverflow Overflow(UiWidget[] children, UiWidget indicator, UiOverflowOptions options = default, int[]? priorities = null) =>
        new(children, indicator, options, priorities);
    public static UiVerticalOverflow VerticalOverflow(UiWidget[] children, UiWidget indicator, UiOverflowOptions options = default, int[]? priorities = null) =>
        new(children, indicator, options, priorities);
}
