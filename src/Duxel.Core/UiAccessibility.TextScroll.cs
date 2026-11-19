namespace Duxel.Core;

public enum UiAccessibilityTextScrollPlacement
{
    TopLeft, BottomRight, TopEdge, BottomEdge, LeftEdge, RightEdge, Nearest
}

/// <summary>Guarded committed UTF-16 source range and a bounded local viewport alignment.</summary>
public sealed class UiAccessibilityTextScroll
{
    public UiAccessibilityTextContent Content
    {
        get;
    }
    public int StartUtf16
    {
        get;
    }
    public int EndUtf16
    {
        get;
    }
    public UiAccessibilityTextScrollPlacement Placement
    {
        get;
    }
    public UiVector2? Point => Alignment.Point;
    internal UiAccessibilityScrollAlignment Alignment
    {
        get;
    }

    public UiAccessibilityTextScroll(UiAccessibilityTextContent content, int startUtf16, int endUtf16,
        UiAccessibilityTextScrollPlacement placement = UiAccessibilityTextScrollPlacement.Nearest)
    {
        ArgumentNullException.ThrowIfNull(content);
        content.GetCharacterOffset(startUtf16);
        content.GetCharacterOffset(endUtf16);
        if (endUtf16 < startUtf16)
            throw new ArgumentOutOfRangeException(nameof(endUtf16));
        if ((uint)placement > (uint)UiAccessibilityTextScrollPlacement.Nearest)
            throw new ArgumentOutOfRangeException(nameof(placement));
        (Content, StartUtf16, EndUtf16, Placement) = (content, startUtf16, endUtf16, placement);
        Alignment = new((UiAccessibilityScrollPlacement)placement);
    }

    public UiAccessibilityTextScroll(UiAccessibilityTextContent content, int startUtf16, int endUtf16, UiVector2 point)
        : this(content, startUtf16, endUtf16)
    {
        Alignment = new(point);
    }

    internal UiVector2 Align(UiRect box, UiRect viewport, UiVector2 offset, UiVector2 maximum,
        bool useHorizontalPoint = true, bool useVerticalPoint = true)
    {
        return new((float)AlignAxis(box.X, box.Width, viewport.X, viewport.Width, offset.X, maximum.X, true, useHorizontalPoint),
            (float)AlignAxis(box.Y, box.Height, viewport.Y, viewport.Height, offset.Y, maximum.Y, false, useVerticalPoint));
    }

    internal double AlignAxis(double start, double size, double visibleStart, double visibleSize,
        double offset, double maximum, bool horizontal, bool usePoint = true)
    {
        return Alignment.AlignAxis(start, size, visibleStart, visibleSize, offset, maximum, horizontal, usePoint);
    }
}

public sealed partial class UiAccessibilityTree
{
    public UiAccessibilityResult RequestTextScroll(string id, long runtimeId, UiAccessibilityTextScroll scroll, bool wait = true)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        return QueueAction(id, runtimeId, UiAccessibilityAction.ScrollText, null, null, wait, null, out _, textScroll: scroll);
    }
}
