namespace Duxel.Core;

internal readonly record struct UiToastContentLayout(float Start, float Width, float Top)
{
    // Two twelve-pixel insets, a twenty-pixel icon, an eight-pixel gap and a twenty-four-pixel close control.
    internal const float MinimumWidth = 76;
    internal const float CompactThreshold = 160;
    internal float VerticalPadding => Top + 12;

    internal static UiToastContentLayout Resolve(float width, bool rightToLeft, bool canDismiss)
    {
        if (width < CompactThreshold)
            return new(12, MathF.Max(0, width - 24), 40);
        return new(rightToLeft ? 12 + (canDismiss ? 28 : 0) : 40,
            MathF.Max(0, width - 52 - (canDismiss ? 28 : 0)), 12);
    }

    internal static float MinimumHeight(float width, float textScale) =>
        (width < CompactThreshold ? 52 : 24) + MathF.Max(20, UiFluentType300.LineHeight * textScale);
}
