namespace Duxel.Core;

internal static class UiDialogMotion
{
    internal const double DurationSeconds = .25;
    private static readonly UiCubicBezier Entry = new(0, 0, 0, 1);
    internal static readonly UiCubicBezier Exit = new(.8, 0, .78, 1);
    internal static readonly UiCubicBezier Backdrop = new(.33, 0, .67, 1);
    internal static UiOverlayMotionSample SampleEntry(double elapsed, UiOverlayMotionSample? from = null)
    {
        var progress = Math.Clamp(elapsed / DurationSeconds, 0, 1);
        var surface = (float)Entry.Evaluate(progress);
        var initial = from ?? new UiOverlayMotionSample(.85f, 0, 0);
        return new(initial.Scale + (1 - initial.Scale) * surface,
            initial.Opacity + (1 - initial.Opacity) * surface,
            initial.BackdropOpacity + (1 - initial.BackdropOpacity) * (float)Backdrop.Evaluate(progress));
    }
}
