namespace Duxel.Core;

internal static class UiScrollReveal
{
    internal static double Axis(float start, float extent, float viewportStart, float viewportExtent, double offset, double maximum,
        bool reverseOffset = false)
    {
        var leading = start - viewportStart;
        var trailing = leading + (extent - viewportExtent);
        var delta = Math.Clamp(0, MathF.Min(leading, trailing), MathF.Max(leading, trailing));
        return MathF.Abs(delta) <= 0.0001f ? offset : Math.Clamp(offset + (reverseOffset ? -delta : delta), 0, maximum);
    }
}

public sealed partial class UiImmediateContext
{
    private int _scrollRevealVersion;
    private int _scrollRevealTier;
    private int _scrollRevealDepth;
    private UiRect _scrollRevealBounds;

    private void RequestScrollReveal(UiRect bounds)
    {
        _scrollRevealVersion++;
        _scrollRevealTier = _popupTierDepth;
        _scrollRevealBounds = bounds;
    }

    private UiVector2 ApplyControlScrollReveal(int previousVersion, UiRect viewport, UiRect enclosingClip,
        UiVector2 offset, UiVector2 maximum)
    {
        if (_scrollRevealVersion == previousVersion || _scrollRevealTier != _popupTierDepth)
            return offset;
        var visible = _scrollRevealDepth == 0 ? IntersectRect(viewport, enclosingClip) : viewport;
        if (visible is not { Width: > 0, Height: > 0 } || _scrollRevealBounds is not { Width: > 0, Height: > 0 })
            return offset;
        var target = _scrollRevealBounds;
        var next = new UiVector2(
            (float)UiScrollReveal.Axis(target.X, target.Width, visible.X, visible.Width, offset.X, maximum.X),
            (float)UiScrollReveal.Axis(target.Y, target.Height, visible.Y, visible.Height, offset.Y, maximum.Y));
        // Propagate the visible slice in the geometry the next frame will use.
        target = target with
        {
            X = target.X + offset.X - next.X,
            Y = target.Y + offset.Y - next.Y
        };
        _scrollRevealBounds = IntersectRect(target, viewport);
        return next;
    }
}
