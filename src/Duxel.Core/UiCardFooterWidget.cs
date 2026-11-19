// Card Footer layout follows Fluent UI (MIT, Microsoft Corporation); see Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

/// <summary>Owns footer content and an optional action aligned to the far end, with a 12-pixel minimum gap.</summary>
public class UiCardFooterWidget : UiMultiChildWidget
{
    private UiVector2[] _sizes;
    private readonly bool _hasAction;
    private UiRetainedLayout<UiVector2>? _retained;

    public UiCardFooterWidget(UiWidget[] children, UiWidget? action = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(Combine(children, action), identity)
    {
        _hasAction = action is not null;
        _sizes = new UiVector2[Children.Length];
    }

    public UiWidget? Action { get { ThrowIfDisposed(); return _hasAction ? Children[^1] : null; } }
    public ReadOnlySpan<UiWidget> Content { get { ThrowIfDisposed(); return _hasAction ? Children[..^1] : Children; } }
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();

    private static UiWidget[] Combine(UiWidget[] children, UiWidget? action)
    {
        ArgumentNullException.ThrowIfNull(children);
        return action is null ? children : [.. children, action];
    }

    private UiVector2 MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } previous && previous.TryGet(context, render, out var size, allowed))
            return size;
        var cache = _retained ??= new();
        cache.Clear();
        var attempt = cache.Begin(context);
        var constraints = context.LayoutConstraints.Validate();
        double width = 12d * Math.Max(0, Children.Length - 1);
        var height = constraints.MinHeight;
        for (var i = 0; i < Children.Length; i++)
        {
            var child = context.MeasureConstrained(Children[i], new(0, float.PositiveInfinity, 0, constraints.MaxHeight));
            _sizes[i] = child;
            width += child.X;
            height = MathF.Max(height, child.Y);
        }
        if (width > constraints.MaxWidth || width > float.MaxValue)
            throw new InvalidOperationException("Card Footer children and gaps exceed the available width.");
        var result = constraints.Constrain(new(float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : (float)width, height));
        for (var i = 0; i < Children.Length; i++)
            _sizes[i] = context.MeasureConstrained(Children[i], UiBoxConstraints.Tight(new(_sizes[i].X, result.Y)));
        cache.Commit(context, attempt, result, result, allowed && ChildrenRetainMeasurement);
        return result;
    }

    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context);

    protected override void RenderControl(UiImmediateContext ui)
    {
        var size = MeasureLayout(new(ui), render: true);
        var origin = ui.AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var x = origin.X;
        for (var i = 0; i < Children.Length; i++)
        {
            var childSize = _sizes[i];
            if (_hasAction && i == Children.Length - 1) x = origin.X + size.X - childSize.X;
            ui.RenderLayoutChild(Children[i], new(x, origin.Y, childSize.X, childSize.Y), root);
            x += childSize.X + 12;
        }
        ui.FinishLayoutBox(origin, size);
    }

    private protected override void ReleaseOwnedResources()
    {
        _sizes = [];
        _retained = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiCardFooterWidget))]
[UiComponent("CardFooter", UiComponentExposure.Fluent)]
public partial class UiFluentCardFooter;
