namespace Duxel.Core;

/// <summary>A labeled progress indicator using Fluent Android's linear track motion.</summary>
public sealed class UiFluentAndroidLinearProgressIndicator(UiProgressIndicatorOptions? options = null,
    UiWidgetIdentity identity = UiWidgetIdentity.Instance)
    : UiProgressIndicatorWidget<UiFluentAndroidLinearProgressBehavior, UiFluentAndroidLinearProgressPresentation>(options, identity);

/// <summary>Owns the determinate transition and the start of the waiting cycle.</summary>
public struct UiFluentAndroidLinearProgressBehavior : IUiProgressBehavior<UiFluentAndroidLinearProgressBehavior>
{
    private static readonly UiTransitionOptions Transition = new(DurationSeconds : 1) { Curve = new(0, 0, .2, 1) };
    private UiFloatTransition _value;
    private double? _waitingSince;

    public static void OnHidden(ref UiFluentAndroidLinearProgressBehavior state) => state = default;
    public static void Release(ref UiFluentAndroidLinearProgressBehavior state) => state = default;

    public static UiProgressValue Evaluate(ref UiFluentAndroidLinearProgressBehavior state, UiProgressValue target,
        double timeSeconds, out bool animating)
    {
        if (target.Fraction is not { } fraction)
        {
            if (state._waitingSince is null)
            {
                state._value = default;
                state._waitingSince = timeSeconds;
            }
            animating = false;
            return target with { IndeterminateElapsedSeconds = timeSeconds - state._waitingSince.Value };
        }

        state._waitingSince = null;
        var frame = state._value.Update(fraction, timeSeconds, Transition with { ReducedMotion = target.ReducedMotion });
        animating = frame.Running;
        return target with { Value = frame.Value * target.Maximum };
    }
}

/// <summary>Paints the square track and a gradient whose leading and trailing edges move independently.</summary>
public readonly struct UiFluentAndroidLinearProgressPresentation : IUiProgressPresentation
{
    private const double CycleSeconds = 1.375;
    private const double EdgeDurationSeconds = .875;
    private const double TailDelaySeconds = .437;
    private static readonly UiCubicBezier EdgeEase = new(.4, 0, .2, 1);

    public static bool CanRetainMeasurement => true;
    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 size) => Measure(context, size);
    public static UiVector2 Measure(UiControlContext context, UiVector2 size) => UiFluentMobileProgressTrack.Measure(context, size);
    public static UiVector2 Arrange(UiControlContext context, UiVector2 naturalSize, UiVector2 allocatedSize) => allocatedSize;

    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds)
    {
        var (track, fill) = UiFluentMobileProgressTrack.Colors(context, value.Color);
        if (value.Fraction is { } fraction)
        {
            UiFluentMobileProgressTrack.PaintDeterminate(context, bounds, track, fill, fraction, value.RightToLeft);
            return;
        }

        if (!value.ReducedMotion)
            context.RequestAnimation(bounds);
        var (tail, head) = WaitingEdges(value.ReducedMotion ? CycleSeconds * .5 : value.IndeterminateElapsedSeconds);
        if (tail == head)
            context.Frame(bounds, track, track, 0, 0);
        else
            context.ProgressGradient(UiFluentMobileProgressTrack.Segment(bounds, tail, head, value.RightToLeft), bounds, track, fill);
    }

    internal static (float Tail, float Head) WaitingEdges(double elapsedSeconds)
    {
        var time = elapsedSeconds % CycleSeconds;
        return ((float)EdgeEase.Evaluate((time - TailDelaySeconds) / EdgeDurationSeconds),
            (float)EdgeEase.Evaluate(time / EdgeDurationSeconds));
    }
}
