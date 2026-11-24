namespace Duxel.Core;

internal static class UiFluentMobileProgressTrack
{
    internal static UiVector2 Measure(UiControlContext context, UiVector2 size) =>
        new(size.X > 0 ? size.X : context.AvailableWidth, size.Y > 0 ? size.Y : 2);

    internal static (UiColor Track, UiColor Fill) Colors(UiControlContext context, UiProgressColor color = UiProgressColor.Brand)
    {
        var forced = context.Theme.ForcedColors;
        var track = forced?.CanvasText ?? (context.FluentColorScheme == UiFluentColorScheme.Dark
            ? new(0x4d, 0x4d, 0x4d) : new(0xd1, 0xd1, 0xd1));
        return (track, forced?.Highlight ?? UiFluentProgressPresentation.ResolveColor(context, color));
    }

    internal static void PaintDeterminate(UiControlContext context, UiRect bounds, UiColor track, UiColor fill,
        float fraction, bool rightToLeft)
    {
        context.Frame(bounds, track, track, 0, 0);
        if (fraction > 0)
            context.Frame(Segment(bounds, 0, fraction, rightToLeft), fill, fill, 0, 0);
    }

    internal static UiRect Segment(UiRect bounds, float start, float end, bool rightToLeft) => bounds with
    {
        X = bounds.X + bounds.Width * (rightToLeft ? 1 - end : start),
        Width = bounds.Width * (end - start)
    };
}
