namespace Duxel.Core;

public enum UiAccessibilityScrollPlacement
{
    TopLeft, BottomRight, TopEdge, BottomEdge, LeftEdge, RightEdge, Nearest
}

/// <summary>Viewport alignment for a component or a committed text range, in UI client coordinates.</summary>
public sealed class UiAccessibilityScrollAlignment
{
    public UiAccessibilityScrollPlacement Placement
    {
        get;
    }
    public UiVector2? Point
    {
        get;
    }

    public UiAccessibilityScrollAlignment(UiAccessibilityScrollPlacement placement = UiAccessibilityScrollPlacement.Nearest)
    {
        if ((uint)placement > (uint)UiAccessibilityScrollPlacement.Nearest)
            throw new ArgumentOutOfRangeException(nameof(placement));
        Placement = placement;
    }

    public UiAccessibilityScrollAlignment(UiVector2 point) : this()
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        Point = point;
    }

    internal double AlignAxis(double start, double size, double visibleStart, double visibleSize,
        double offset, double maximum, bool horizontal, bool usePoint = true, bool reverseOffset = false)
    {
        // Inner point placement leaves room for the outermost viewport to reach the requested client point.
        var minimum = !usePoint && Point.HasValue || (horizontal
            ? Placement is UiAccessibilityScrollPlacement.TopLeft or UiAccessibilityScrollPlacement.LeftEdge
            : Placement is UiAccessibilityScrollPlacement.TopLeft or UiAccessibilityScrollPlacement.TopEdge);
        var maximumEdge = horizontal ? Placement is UiAccessibilityScrollPlacement.BottomRight or UiAccessibilityScrollPlacement.RightEdge
            : Placement is UiAccessibilityScrollPlacement.BottomRight or UiAccessibilityScrollPlacement.BottomEdge;
        var leading = start - visibleStart;
        var trailing = leading + size - visibleSize;
        var delta = usePoint && Point is { } point ? start - (horizontal ? point.X : point.Y)
            : minimum ? leading
            : maximumEdge ? trailing
            : Math.Clamp(0, Math.Min(leading, trailing), Math.Max(leading, trailing));
        return Math.Clamp(offset + (reverseOffset ? -delta : delta), 0, maximum);
    }
}

public sealed partial class UiAccessibilityTree
{
    public UiAccessibilityResult RequestScrollIntoView(string id, long runtimeId,
        UiAccessibilityScrollAlignment alignment, bool wait = true)
    {
        ArgumentNullException.ThrowIfNull(alignment);
        return QueueAction(id, runtimeId, UiAccessibilityAction.ScrollIntoView, null, null, wait, null, out _, scrollAlignment: alignment);
    }
}
