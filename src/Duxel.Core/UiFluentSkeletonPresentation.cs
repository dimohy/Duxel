namespace Duxel.Core;

public readonly struct UiFluentSkeletonPresentation : IUiSkeletonPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiVector2 Measure(UiControlContext context, UiSkeletonStyle style, float? width)
    {
        // An unbounded block has zero intrinsic inline content, then stretches
        // when its layout parent supplies a finite allocation.
        var natural = style.Shape == UiSkeletonShape.Rectangle
            ? float.IsFinite(context.AvailableWidth) ? context.AvailableWidth : 0
            : style.Size;
        return new(width ?? natural, style.Size);
    }

    public static void Paint(UiControlContext context, UiSkeletonStyle style, UiSkeletonFrame frame, UiRect bounds)
    {
        var dark = context.FluentColorScheme switch
        {
            UiFluentColorScheme.Light => false,
            UiFluentColorScheme.Dark => true,
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
        var first = dark ? new UiColor(87, 87, 87) : new UiColor(230, 230, 230);
        var second = dark ? new UiColor(51, 51, 51) : new UiColor(250, 250, 250);
        if (style.Appearance == UiSkeletonAppearance.Translucent)
            first = dark ? new UiColor(255, 255, 255, 26) : new UiColor(0, 0, 0, 26);
        var radius = style.Shape switch
        {
            UiSkeletonShape.Circle => MathF.Min(bounds.Width, bounds.Height) * .5f,
            UiSkeletonShape.Rectangle => 4,
            _ => 0
        };
        if (style.Animation == UiSkeletonAnimation.Pulse)
        {
            var alpha = (uint)Math.Clamp((int)MathF.Round((first.Rgba >> 24) * frame.Opacity), 0, 255);
            context.Frame(bounds, new(first.Rgba & 0x00ffffffu | alpha << 24), default, radius, 0);
            return;
        }
        context.Frame(bounds, first, default, radius, 0);
        var band = bounds with
        {
            X = bounds.X + frame.WaveOffset * bounds.Width
        };
        // The pseudo-element is one item wide; only its intersection exists.
        var left = MathF.Max(bounds.X, band.X);
        var right = MathF.Min(bounds.X + bounds.Width, band.X + band.Width);
        if (right <= left)
            return;
        using var clip = context.Clip(new(left, bounds.Y, right - left, bounds.Height));
        if (style.Appearance == UiSkeletonAppearance.Translucent)
            context.ProgressGradient(band, bounds, new(first.Rgba & 0x00ffffffu), first, radius);
        else
            context.ProgressGradient(band, bounds, first, second, radius);
    }
}
