namespace Duxel.Core;

public readonly record struct UiUniformGridOptions(int CrossAxisCount = 1, float CrossAxisSpacing = 0,
    float MainAxisSpacing = 0, float ChildAspectRatio = 1, float? MainAxisExtent = null)
{
    public UiUniformGridOptions() : this(1, 0, 0, 1, null) { }
    public UiUniformGridOptions Validate()
    {
        if (CrossAxisCount <= 0 || !float.IsFinite(CrossAxisSpacing) || CrossAxisSpacing < 0
            || !float.IsFinite(MainAxisSpacing) || MainAxisSpacing < 0 || !float.IsFinite(ChildAspectRatio) || ChildAspectRatio <= 0
            || MainAxisExtent is {} extent && (!float.IsFinite(extent) || extent < 0))
            throw new ArgumentOutOfRangeException(nameof(UiUniformGridOptions));
        return this;
    }
}

/// <summary>Allocates equal fixed-count cross-axis cells without providing scrolling or virtualization.</summary>
public class UiUniformGridWidget<TAxis> : UiArrangedChildrenWidget where TAxis : IUiLayoutAxis
{
    private UiUniformGridOptions _options;
    protected override bool CanRetainArrangement => TAxis.CanRetainMeasurement;
    public UiUniformGridOptions Options
    {
        get => _options;
        set { ThrowIfDisposed(); value.Validate(); if (_options == value) return; ThrowIfBuilding(); _options = value; RequestRebuild(); }
    }
    public UiUniformGridWidget(UiWidget[] children, UiUniformGridOptions options, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(ValidateOptions(children, options), identity) => _options = options;
    private static UiWidget[] ValidateOptions(UiWidget[] children, UiUniformGridOptions options) { options.Validate(); return children; }
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var constraints = context.LayoutConstraints.Validate(); var horizontal = TAxis.Horizontal;
        var maximumCross = horizontal ? constraints.MaxHeight : constraints.MaxWidth;
        if (!float.IsFinite(maximumCross)) throw new InvalidOperationException("A uniform Grid requires a bounded cross axis.");
        var spacing = (double)(_options.CrossAxisCount - 1) * _options.CrossAxisSpacing;
        if (spacing > maximumCross) throw new InvalidOperationException("Grid cross-axis spacing exceeds the bounded cross axis.");
        var cellCross = UiLayoutDistribution.Finite((maximumCross - spacing) / _options.CrossAxisCount);
        var cellMain = _options.MainAxisExtent ?? UiLayoutDistribution.Finite((double)cellCross / _options.ChildAspectRatio);
        var cellSize = horizontal ? new UiVector2(cellMain, cellCross) : new UiVector2(cellCross, cellMain);
        var cellConstraints = UiBoxConstraints.Tight(cellSize);
        var lines = Children.Length / _options.CrossAxisCount + (Children.Length % _options.CrossAxisCount > 0 ? 1 : 0);
        var mainExtent = UiLayoutDistribution.Finite((double)lines * cellMain + Math.Max(0, lines - 1) * (double)_options.MainAxisSpacing);
        var result = constraints.Constrain(horizontal ? new(mainExtent, maximumCross) : new(maximumCross, mainExtent));
        for (var i = 0; i < Children.Length; i++)
        {
            context.MeasureParentDataChild(Children[i], null, cellConstraints);
            var main = UiLayoutDistribution.Finite((i / _options.CrossAxisCount) * ((double)cellMain + _options.MainAxisSpacing));
            var cross = UiLayoutDistribution.Finite((i % _options.CrossAxisCount) * ((double)cellCross + _options.CrossAxisSpacing));
            bounds[i] = new(horizontal ? main : cross, horizontal ? cross : main, cellSize.X, cellSize.Y);
        }
        return result;
    }
}

[UiWidgetStyle(typeof(UiUniformGridWidget<>), typeof(UiVerticalAxis))]
[UiComponent("Grid", UiComponentExposure.Infrastructure)]
public partial class UiGrid;
[UiWidgetStyle(typeof(UiUniformGridWidget<>), typeof(UiHorizontalAxis))]
[UiComponent("HorizontalGrid", UiComponentExposure.Infrastructure)]
public partial class UiHorizontalGrid;
public static partial class Dux
{
    public static UiGrid UniformGrid(UiUniformGridOptions options, params UiWidget[] children) => new(children, options);
    public static UiHorizontalGrid HorizontalGrid(UiUniformGridOptions options, params UiWidget[] children) => new(children, options);
}
