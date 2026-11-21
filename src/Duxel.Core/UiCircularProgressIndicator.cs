namespace Duxel.Core;

/// <summary>A progress control with a statically selected behavior and circular presentation.</summary>
public class UiCircularProgressIndicator<TBehavior, TPresentation> : UiProgress<TBehavior, TPresentation>
    where TBehavior : struct, IUiProgressBehavior<TBehavior>
    where TPresentation : IUiProgressPresentation
{
    public UiCircularProgressIndicator(float? value = null, float maximum = 1,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(value, maximum, identity)
        => Color = UiProgressColor.Neutral;
}

/// <summary>The Fluent Android determinate ring transition.</summary>
public struct UiFluentCircularProgressBehavior : IUiProgressBehavior<UiFluentCircularProgressBehavior>
{
    private static readonly UiTransitionOptions Transition = new(.75)
    {
        Curve = new(0, 0, .2, 1)
    };
    private UiFloatTransition _transition;

    public static UiProgressValue Evaluate(ref UiFluentCircularProgressBehavior state, UiProgressValue target,
        double timeSeconds, out bool animating)
    {
        if (target.Fraction is not { } fraction)
        {
            state = default;
            animating = false;
            return target;
        }

        var frame = state._transition.Update(fraction, timeSeconds, Transition with { ReducedMotion = target.ReducedMotion });
        animating = frame.Running;
        return target with { Value = frame.Value * target.Maximum };
    }

    public static void Release(ref UiFluentCircularProgressBehavior state) => state = default;
}

public readonly struct UiFluentCircularProgressPresentation : IUiProgressPresentation
{
    public static bool CanRetainMeasurement => true;

    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 requestedSize) =>
        Measure(context, requestedSize);

    public static UiVector2 Measure(UiControlContext context, UiVector2 requestedSize)
    {
        var diameter = requestedSize.X > 0
            ? requestedSize.Y > 0 ? MathF.Min(requestedSize.X, requestedSize.Y) : requestedSize.X
            : requestedSize.Y > 0 ? requestedSize.Y : 12;
        return new(diameter, diameter);
    }

    public static UiVector2 Arrange(UiControlContext context, UiVector2 naturalSize, UiVector2 allocatedSize) =>
        allocatedSize;

    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds)
    {
        var diameter = MathF.Min(bounds.Width, bounds.Height);
        var stroke = diameter switch { < 24 => 1, < 32 => 2, < 40 => 3, _ => 4 };
        var radius = diameter * .5f;
        if (radius <= stroke + 1 || value.Fraction == 0)
            return;

        var color = context.Theme.ForcedColors is not null ? context.Theme.Text
            : UiFluentProgressPresentation.ResolveColor(context, value.Color);
        var direction = value.RightToLeft ? -1 : 1;
        var fraction = value.Fraction;
        var start = 0f;
        if (fraction is null && !value.ReducedMotion)
        {
            context.RequestAnimation(bounds);
            start = (float)(context.TimeSeconds % 1) * MathF.Tau * direction;
        }

        var sweep = (fraction ?? .75f) * MathF.Tau * direction;
        var center = new UiVector2(bounds.X + bounds.Width * .5f, bounds.Y + bounds.Height * .5f);
        context.AnnularSector(center, radius, stroke, start, sweep, color, color);
        if (fraction == 1)
            return;

        var capDistance = radius - (stroke + 1) * .5f;
        void PaintCap(float angle) => context.Circle(
            new(center.X + MathF.Sin(angle) * capDistance, center.Y - MathF.Cos(angle) * capDistance), stroke * .5f, color);
        PaintCap(start);
        PaintCap(start + sweep);
    }
}

[UiWidgetStyle(typeof(UiCircularProgressIndicator<,>), typeof(UiFluentCircularProgressBehavior),
    typeof(UiFluentCircularProgressPresentation))]
[UiComponent("CircularProgressIndicator", UiComponentExposure.Fluent)]
public partial class UiFluentCircularProgressIndicator;
