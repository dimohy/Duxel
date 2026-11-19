namespace Duxel.Core;

public enum UiWrapCrossAlignment
{
    Start, Center, End
}
public readonly record struct UiWrapOptions(float Spacing = 0, float RunSpacing = 0,
    UiMainAxisAlignment Alignment = UiMainAxisAlignment.Start, UiMainAxisAlignment RunAlignment = UiMainAxisAlignment.Start,
    UiWrapCrossAlignment CrossAlignment = UiWrapCrossAlignment.Start)
{
    public bool RightToLeft
    {
        get; init;
    }

    public UiWrapOptions Validate()
    {
        if (!float.IsFinite(Spacing) || Spacing < 0 || !float.IsFinite(RunSpacing) || RunSpacing < 0
            || (uint)Alignment > 5 || (uint)RunAlignment > 5 || (uint)CrossAlignment > 2)
            throw new ArgumentOutOfRangeException(nameof(UiWrapOptions));
        return this;
    }
}

/// <summary>Forms runs from measured children using a statically selected main axis.</summary>
public class UiWrapWidget<TAxis> : UiArrangedChildrenWidget where TAxis : IUiLayoutAxis
{
    private readonly record struct Run(int Start, int Count, float Main, float Cross);
    private Run[] _runs;
    private int[] _included;
    private UiWrapOptions _options;
    protected override bool CanRetainArrangement => TAxis.CanRetainMeasurement;
    public UiWrapOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiWrapWidget(UiWidget[] children, UiWrapOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(ValidateOptions(children, options), identity)
    {
        _options = options;
        _runs = new Run[children.Length];
        _included = new int[children.Length];
    }
    protected virtual bool ShouldArrangeChild(int index) => true;
    private static UiWidget[] ValidateOptions(UiWidget[] children, UiWrapOptions options)
    {
        options.Validate();
        return children;
    }
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var constraints = context.LayoutConstraints.Validate();
        var horizontal = TAxis.Horizontal;
        var maxMain = horizontal ? constraints.MaxWidth : constraints.MaxHeight;
        var childConstraints = horizontal ? new UiBoxConstraints(0, maxMain, 0, float.PositiveInfinity)
            : new UiBoxConstraints(0, float.PositiveInfinity, 0, maxMain);
        var runCount = 0;
        var start = 0;
        var count = 0;
        var main = 0f;
        var cross = 0f;
        var maximumMain = 0f;
        double totalCross = 0;
        var includedCount = 0;
        bounds.Clear();
        for (var i = 0; i < Children.Length; i++)
        {
            if (!ShouldArrangeChild(i))
                continue;
            _included[includedCount++] = i;
            var size = context.MeasureParentDataChild(Children[i], null, childConstraints);
            bounds[i] = new(0, 0, size.X, size.Y);
            var childMain = horizontal ? size.X : size.Y;
            var childCross = horizontal ? size.Y : size.X;
            var nextMain = (double)main + (count > 0 ? _options.Spacing : 0) + childMain;
            if (count > 0 && nextMain > maxMain)
            {
                _runs[runCount++] = new(start, count, main, cross);
                maximumMain = MathF.Max(maximumMain, main);
                totalCross += cross + (runCount > 1 ? (double)_options.RunSpacing : 0);
                start = includedCount - 1;
                count = 0;
                main = cross = 0;
                nextMain = childMain;
            }
            main = UiLayoutDistribution.Finite(nextMain);
            cross = MathF.Max(cross, childCross);
            count++;
        }
        if (count > 0)
        {
            _runs[runCount++] = new(start, count, main, cross);
            maximumMain = MathF.Max(maximumMain, main);
            totalCross += cross + (runCount > 1 ? (double)_options.RunSpacing : 0);
        }
        var crossExtent = UiLayoutDistribution.Finite(totalCross);
        var result = constraints.Constrain(horizontal ? new(maximumMain, crossExtent) : new(crossExtent, maximumMain));
        var availableMain = horizontal ? result.X : result.Y;
        var availableCross = horizontal ? result.Y : result.X;
        var (runPosition, runGap) = UiLayoutDistribution.Distribute(_options.RunAlignment, MathF.Max(0, availableCross - crossExtent), runCount, _options.RunSpacing);
        for (var r = 0; r < runCount; r++)
        {
            var run = _runs[r];
            var (position, gap) = UiLayoutDistribution.Distribute(_options.Alignment, MathF.Max(0, availableMain - run.Main), run.Count, _options.Spacing);
            for (var slot = run.Start; slot < run.Start + run.Count; slot++)
            {
                var i = _included[slot];
                var child = bounds[i];
                var childCross = horizontal ? child.Height : child.Width;
                var crossFree = run.Cross - childCross;
                var offset = _options.CrossAlignment switch
                {
                    UiWrapCrossAlignment.Center => crossFree * .5f,
                    UiWrapCrossAlignment.End => crossFree,
                    _ => 0
                };
                var childCrossPosition = UiLayoutDistribution.Finite((double)runPosition + offset);
                bounds[i] = child with
                {
                    X = horizontal ? position : childCrossPosition,
                    Y = horizontal ? childCrossPosition : position
                };
                if (slot + 1 < run.Start + run.Count)
                    position = UiLayoutDistribution.Finite((double)position + (horizontal ? child.Width : child.Height) + gap);
            }
            if (r + 1 < runCount)
                runPosition = UiLayoutDistribution.Finite((double)runPosition + run.Cross + runGap);
        }
        if (_options.RightToLeft || context.RightToLeft)
        {
            for (var slot = 0; slot < includedCount; slot++)
            {
                var i = _included[slot];
                bounds[i] = bounds[i] with
                {
                    X = result.X - bounds[i].X - bounds[i].Width
                };
            }
        }
        return result;
    }
    private protected override void ReleaseOwnedResources()
    {
        _runs = [];
        _included = [];
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiWrapWidget<>), typeof(UiHorizontalAxis))]
[UiComponent("Wrap", UiComponentExposure.Infrastructure)]
public partial class UiWrap;
[UiWidgetStyle(typeof(UiWrapWidget<>), typeof(UiVerticalAxis))]
[UiComponent("VerticalWrap", UiComponentExposure.Infrastructure)]
public partial class UiVerticalWrap;
public static partial class Dux
{
    public static UiWrap Wrap(UiWrapOptions options, params UiWidget[] children) => new(children, options);
    public static UiVerticalWrap VerticalWrap(UiWrapOptions options, params UiWidget[] children) => new(children, options);
}
