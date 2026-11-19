namespace Duxel.Core;

public interface IUiSkeletonPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 Measure(UiControlContext context, UiSkeletonStyle style, float? width);
    static abstract void Paint(UiControlContext context, UiSkeletonStyle style, UiSkeletonFrame frame, UiRect bounds);
}

/// <summary>Owns loading content and supplies inherited placeholder properties.</summary>
public class UiSkeletonWidget : UiContainerBox<UiBoxContainerLayout, UiPlainContainerPresentation>
{
    private UiSkeletonOptions _options;
    private readonly UiLayoutEnvironment<UiSkeletonStyle> _environment = new();

    public UiSkeletonWidget(UiWidget child, UiSkeletonOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(child, default, identity)
    {
        _options = options.Validate();
    }

    public new UiSkeletonOptions Options
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

    protected override UiContainerOptions LayoutOptions => new(Width: _options.Width);

    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        using var scope = context.SkeletonScope(_options, _environment);
        return base.MeasureContent(context);
    }

    protected override void RenderControl(UiImmediateContext ui)
    {
        using var scope = ui.BeginSkeletonScope(_options, _environment);
        ui.RenderSkeleton(PrimaryInputId("Skeleton"), AccessibleName ?? string.Empty, Child,
            GetLayout(new(ui), render: true));
    }
}

/// <summary>A noninteractive placeholder painted by a statically selected presentation.</summary>
public class UiSkeletonItem<TPresentation> : UiControlWidget where TPresentation : IUiSkeletonPresentation
{
    private UiSkeletonOptions _options;
    public UiSkeletonItem(UiSkeletonOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity) => _options = options.Validate();

    public UiSkeletonOptions Options
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

    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        context.ConstrainToLayout(TPresentation.Measure(context, _options.Resolve(context.SkeletonStyle), _options.Width));
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSkeletonItem<TPresentation>(_options);
}

[UiWidgetStyle(typeof(UiSkeletonWidget))]
[UiComponent("Skeleton", UiComponentExposure.Fluent)]
public partial class UiFluentSkeleton;

[UiWidgetStyle(typeof(UiSkeletonItem<>), typeof(UiFluentSkeletonPresentation))]
[UiComponent("SkeletonItem", UiComponentExposure.Fluent)]
public partial class UiFluentSkeletonItem;
