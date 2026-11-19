namespace Duxel.Core;

/// <summary>Owns reusable child bounds for measured multi-child layouts without parent-data wrappers.</summary>
public abstract class UiArrangedChildrenWidget : UiMultiChildWidget
{
    private UiRect[] _bounds;
    private UiRetainedLayout<UiVector2>? _retained;
    private uint _childSnapshotRevision;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement()
        => _retained?.Invalidate();
    protected UiArrangedChildrenWidget(UiWidget[] children, UiWidgetIdentity identity = UiWidgetIdentity.Instance,
        string[]? childKeys = null) : base(children, identity, childKeys)
        => _bounds = new UiRect[children.Length];
    protected virtual void PrepareChildren(UiControlContext context)
    {
    }
    protected abstract UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds);
    protected virtual bool ShouldRenderChild(int index) => true;
    protected virtual bool CanRetainPreparedArrangement => true;
    private UiVector2 PrepareLayout(UiControlContext context, bool render = false)
    {
        PrepareChildren(context);
        if (_childSnapshotRevision != ChildSnapshotRevision)
        {
            _childSnapshotRevision = ChildSnapshotRevision;
            if (_bounds.Length != Children.Length)
                _bounds = new UiRect[Children.Length];
            // Prepared bounds belong to child identities, including when only their order changes.
            _retained?.Clear();
        }
        var retentionAllowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } cached && cached.TryGet(context, render, out var previous, retentionAllowed))
            return previous;
        _retained?.Clear();
        var retain = retentionAllowed && ChildrenRetainMeasurement;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var size = ArrangeChildren(context, _bounds);
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
            throw new InvalidOperationException("Child arrangement returned invalid layout extents.");
        if (context.LayoutConstraints.Constrain(size) != size)
            throw new InvalidOperationException("Child arrangement must satisfy its parent box constraints.");
        for (var i = 0; i < _bounds.Length; i++)
        {
            var rect = _bounds[i];
            if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height)
                || rect.Width < 0 || rect.Height < 0)
                throw new InvalidOperationException("Child arrangement returned invalid child bounds.");
        }
        cache.Commit(context, attempt, size, size, retain && CanRetainPreparedArrangement);
        return size;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => PrepareLayout(context);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var size = PrepareLayout(new(ui), render: true);
        var origin = ui.AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        RenderArrangedChildren(ui, _bounds, root);
        ui.FinishLayoutBox(origin, size);
    }
    protected virtual void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        for (var i = 0; i < Children.Length; i++)
        {
            if (!ShouldRenderChild(i))
                continue;
            var bounds = layout[i] with
            {
                X = root.X + layout[i].X,
                Y = root.Y + layout[i].Y
            };
            ui.RenderParentDataChild(Children[i], null, bounds, root);
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        _bounds = [];
        _retained = null;
        base.ReleaseOwnedResources();
    }
}

internal static class UiLayoutDistribution
{
    internal static (float Start, float Gap) Distribute(UiMainAxisAlignment alignment, float free, int count, float gap)
        => alignment switch
        {
            UiMainAxisAlignment.Center => (free * .5f, gap),
            UiMainAxisAlignment.End => (free, gap),
            UiMainAxisAlignment.SpaceBetween when count > 1 => (0, gap + free / (count - 1)),
            UiMainAxisAlignment.SpaceAround when count > 0 => (free / count * .5f, gap + free / count),
            UiMainAxisAlignment.SpaceEvenly => (free / (count + 1), gap + free / (count + 1)),
            _ => (0, gap)
        };
    internal static float Finite(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > float.MaxValue)
            throw new InvalidOperationException("Layout exceeds finite coordinate capacity.");
        return (float)value;
    }
}
