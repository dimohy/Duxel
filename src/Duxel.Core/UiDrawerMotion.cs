namespace Duxel.Core;

internal static class UiDrawerMotion
{
    internal const double DurationSeconds = .25;
    internal static readonly UiCubicBezier Entry = new(0, 0, 0, 1);
    internal static UiOverlayMotionSample Sample(double elapsed, UiOverlayMotionSample from)
    {
        var progress = Math.Clamp(elapsed / DurationSeconds, 0, 1);
        var surface = (float)Entry.Evaluate(progress);
        return new(1, from.Opacity + (1 - from.Opacity) * surface,
            from.BackdropOpacity + (1 - from.BackdropOpacity) * (float)UiDialogMotion.Backdrop.Evaluate(progress),
            new(from.Translation.X * (1 - surface), from.Translation.Y * (1 - surface)));
    }
}
public sealed partial class UiImmediateContext
{
    internal void PrepareDrawerOpening(UiDrawerInputState input)
    {
        input.EntryFrom = new(1, 1, 0, input.ClosedOffset);
        if (_overlayExits is null) return;
        foreach (var exit in _overlayExits)
            if (ReferenceEquals(exit.Owner, input)) input.EntryFrom = exit.Sample(_state.TimeSeconds);
    }
    internal void PrepareDrawerClosing(UiDrawerInputState input)
    {
        if (!input.MotionEnabled || input.Inline || input.SurfacePaint is not { } paint) return;
        input.SurfacePaint = null;
        if (input.LastMotion.Opacity <= 0 || paint.For(this).Content is null) { paint.Dispose(); return; }
        CancelOverlayExit(input);
        (_overlayExits ??= []).Add(new(input, paint, input.NaturalBounds, input.BackdropColor, input.Tier,
            input.LastMotion, UiDrawerMotion.DurationSeconds, 1, UiDialogMotion.Exit, UiDialogMotion.Backdrop, input.ClosedOffset));
    }
}
