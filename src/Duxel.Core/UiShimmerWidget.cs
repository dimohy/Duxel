namespace Duxel.Core;

public sealed record UiShimmerOptions(bool ReducedMotion = false)
{
    public UiTransitionOptions Transition { get; init; } = new(.2, UiAnimationEasing.Linear);
    internal void Validate() => Transition.Validate();
}

/// <summary>Owns a loading placeholder and real content with a finite, reversible crossfade.</summary>
public class UiShimmerWidget : UiArrangedChildrenWidget
{
    private bool _isDataLoaded;
    private UiShimmerOptions _options;
    private UiFloatTransition _transition;
    private UiTransitionFrame _frame;
    private readonly UiPresencePaint _placeholderPaint = new();
    private readonly UiPresencePaint _contentPaint = new();
    private readonly UiLayoutEnvironment<UiSkeletonStyle> _environment = new();
    public UiWidget Content => Children[1];
    public UiSkeletonWidget Placeholder => (UiSkeletonWidget)Children[0];
    public bool IsDataLoaded
    {
        get => _isDataLoaded;
        set
        {
            ThrowIfDisposed();
            if (_isDataLoaded == value) return;
            ThrowIfBuilding();
            _isDataLoaded = value;
            RequestRebuild();
        }
    }
    public UiShimmerOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiShimmerWidget(UiWidget content, UiSkeletonWidget? placeholder = null, bool isDataLoaded = false,
        UiShimmerOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(Validate(content, placeholder, options), identity)
    {
        _options = options ?? new();
        _isDataLoaded = isDataLoaded;
        _transition = new(isDataLoaded ? 1 : 0);
    }
    private static UiWidget[] Validate(UiWidget content, UiSkeletonWidget? placeholder, UiShimmerOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        options?.Validate();
        return [placeholder ?? new UiSkeletonWidget(new UiSkeletonItem<UiFluentSkeletonPresentation>()) { AccessibleName = "Loading" }, content];
    }
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainPreparedArrangement => !_frame.Running;
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        _frame = _transition.Update(_isDataLoaded ? 1 : 0, context.TimeSeconds,
            _options.Transition with { ReducedMotion = _options.ReducedMotion || _options.Transition.ReducedMotion });
        UiVector2 placeholder;
        using (context.SkeletonScope(new(ReducedMotion : _options.ReducedMotion || _options.Transition.ReducedMotion), _environment))
            placeholder = context.MeasureConstrained(Placeholder, context.LayoutConstraints);
        var content = context.MeasureConstrained(Content, context.LayoutConstraints);
        var size = context.LayoutConstraints.Constrain(new(MathF.Max(placeholder.X, content.X), MathF.Max(placeholder.Y, content.Y)));
        bounds[0] = bounds[1] = new(0, 0, size.X, size.Y);
        return size;
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        using (ui.BeginSkeletonScope(new(ReducedMotion : _options.ReducedMotion || _options.Transition.ReducedMotion), _environment))
            ui.RenderPresenceChild(Placeholder, _placeholderPaint, root, root, !_isDataLoaded, 1 - _frame.Value);
        ui.RenderPresenceChild(Content, _contentPaint, root, root, _isDataLoaded, _frame.Value);
        if (_frame.NextFrameTimeSeconds is { } next)
            ui.RequestShimmerFrame(root, next);
    }
    private protected override void ReleaseOwnedResources()
    {
        _placeholderPaint.Dispose();
        _contentPaint.Dispose();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiShimmerWidget))]
[UiComponent("Shimmer", UiComponentExposure.Fluent)]
public partial class UiFluentShimmer;

public sealed partial class UiImmediateContext
{
    internal void RequestShimmerFrame(UiRect bounds, double deadline)
    {
        if (IntersectRect(bounds, CurrentClipRect) is { Width: > 0, Height: > 0 })
            RequestFrameAt(deadline);
    }
}
