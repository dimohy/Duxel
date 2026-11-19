namespace Duxel.Core;

public abstract partial class UiWidget
{
    private sealed class PresentationState
    {
        internal UiEdgeInsets Margin;
        internal UiWidgetTransform Transform = UiWidgetTransform.Identity;
    }

    public UiWidgetTransform Transform
    {
        get => (_identity as IdentityCache)?.Presentation?.Transform ?? UiWidgetTransform.Identity;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (Transform == value)
                return;
            ThrowIfBuilding();
            var cache = _identity as IdentityCache ?? new(_identity as string);
            _identity = cache;
            (cache.Presentation ??= new()).Transform = value;
            RequestRebuild();
        }
    }

    internal virtual bool AllowsVisualOverflow => (_identity as IdentityCache)?.Presentation?.Transform is
    { Mode: UiWidgetTransformMode.Visual, IsIdentity: false };

    /// <summary>Nonnegative space outside this widget's content, included in measurement and flow.</summary>
    public UiEdgeInsets Margin
    {
        get => (_identity as IdentityCache)?.Presentation?.Margin ?? default;
        set
        {
            ThrowIfDisposed();
            static void Validate(float edge)
            {
                if (!float.IsFinite(edge) || edge < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
            Validate(value.Left);
            Validate(value.Top);
            Validate(value.Right);
            Validate(value.Bottom);
            Validate(value.Left + value.Right);
            Validate(value.Top + value.Bottom);
            if (Margin == value)
                return;
            ThrowIfBuilding();
            var cache = _identity as IdentityCache ?? new(_identity as string);
            _identity = cache;
            (cache.Presentation ??= new()).Margin = value;
            RequestRebuild();
        }
    }

    private UiVector2 MeasureWithMargin(UiControlContext context)
    {
        var margin = Margin;
        var available = context.AvailableSize;
        var inner = new UiVector2(MathF.Max(0, available.X - margin.Left - margin.Right),
            MathF.Max(0, available.Y - margin.Top - margin.Bottom));
        var transform = Transform;
        var transformed = !transform.IsIdentity;
        if (transformed)
            inner = transform.ContentMaximum(inner);
        var content = context.MeasureWidgetContent(this, inner,
            context.HasBoxConstraints ? transformed ? UiBoxConstraints.Loose(inner) : context.LayoutConstraints.Deflate(margin) : null, transformed);
        return MarginExtent(transform.IsIdentity ? content : transform.Arrange(content, default).Extent, margin);
    }

    internal static UiVector2 MarginExtent(UiVector2 content, UiEdgeInsets margin)
    {
        var size = new UiVector2(content.X + margin.Left + margin.Right, content.Y + margin.Top + margin.Bottom);
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y))
            throw new InvalidOperationException("Widget margin extent exceeds the numeric range.");
        return size;
    }

    internal UiVector2 MeasureContentCore(UiControlContext context) => MeasureContent(context);
    internal void RenderUnpresentedContent(UiImmediateContext ui) => RenderContent(ui);
}

public readonly partial struct UiControlContext
{
    internal UiVector2 MeasureWidgetContent(UiWidget widget, UiVector2 available, UiBoxConstraints? constraints, bool transformed = false) =>
        _ui.MeasureWidgetContent(widget, available, constraints, transformed);
}

public sealed partial class UiImmediateContext
{
    private UiVector2 GetWidgetMeasureAvailable() => (_inputSlotBounds is not null && _inputSlotBoundsTier == _popupTierDepth)
        || HasCurrentChild || _hasWindowRect ? GetContentRegionAvail() : new(float.PositiveInfinity, float.PositiveInfinity);

    internal void RenderWidgetMargin(UiWidget widget, UiEdgeInsets margin)
    {
        var available = GetWidgetMeasureAvailable();
        var inner = new UiVector2(MathF.Max(0, available.X - margin.Left - margin.Right),
            MathF.Max(0, available.Y - margin.Top - margin.Bottom));
        var content = MeasureWidgetContent(widget, inner, null);
        var desired = UiWidget.MarginExtent(content, margin);
        var size = ConstrainToLayout(desired);
        var origin = AdvanceCursor(size);
        FinishLayoutBox(origin, size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var bounds = new UiRect(origin.X + margin.Left, origin.Y + margin.Top,
            size.X == desired.X ? content.X : MathF.Max(0, size.X - margin.Left - margin.Right),
            size.Y == desired.Y ? content.Y : MathF.Max(0, size.Y - margin.Top - margin.Bottom));
        RenderAllocatedChild(_builder, widget, bounds, root, constrainLayout: true, renderContentOnly: true);
    }
}
