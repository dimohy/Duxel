namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    /// <summary>
    /// Places a borrowed widget in sequential content using the available width and its natural height.
    /// The enclosing window or child owns clipping and scrolling; the caller owns the widget lifetime.
    /// </summary>
    public void RenderNaturalHeight(UiWidget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        EnsureNotMeasuring();
        var width = GetContentRegionAvail().X;
        if (!float.IsFinite(width) || width < 0)
            throw new InvalidOperationException("Natural-height widget flow requires a finite nonnegative available width.");
        var size = widget.MeasureConstrained(this, new(0, width, 0, float.PositiveInfinity));
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
            throw new InvalidOperationException("Natural-height widget flow requires a finite nonnegative measured size.");
        var clip = ResolveItemClipRect();
        var origin = AllocateLayoutBox(size);
        try
        {
            RenderAllocatedChild(_builder, widget, new(origin.X, origin.Y, size.X, size.Y), clip,
                constrainLayout : true, clipToAllocation : false);
        }
        finally { FinishLayoutBox(origin, size); }
    }
}
