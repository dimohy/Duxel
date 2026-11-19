namespace Duxel.Core;

/// <summary>A CSS cubic-bezier timing curve with monotonic time coordinates and optional value overshoot.</summary>
public readonly record struct UiCubicBezier(double X1, double Y1, double X2, double Y2)
{
    public UiCubicBezier Validate()
    {
        if (!double.IsFinite(X1) || X1 < 0 || X1 > 1 || !double.IsFinite(X2) || X2 < 0 || X2 > 1
            || !double.IsFinite(Y1) || !double.IsFinite(Y2))
            throw new ArgumentOutOfRangeException(nameof(UiCubicBezier), "Timing controls require finite coordinates and time controls in [0,1].");
        return this;
    }

    public double Evaluate(double progress)
    {
        Validate();
        if (!double.IsFinite(progress))
            throw new ArgumentOutOfRangeException(nameof(progress));
        if (progress <= 0)
            return 0;
        if (progress >= 1)
            return 1;
        // The Fluent decelerate curve has x=t^3, including its vertical initial tangent.
        if (X1 == 0 && X2 == 0)
        {
            var t = Math.Cbrt(progress);
            return Y1 == 0 && Y2 == 1 ? t * t * (3 - 2 * t) : Sample(Y1, Y2, t);
        }
        var lower = 0d;
        var upper = 1d;
        for (var i = 0; i < 32; i++)
        {
            var t = (lower + upper) * .5;
            if (Sample(X1, X2, t) < progress)
                lower = t;
            else
                upper = t;
        }
        return Sample(Y1, Y2, (lower + upper) * .5);
    }

    private static double Sample(double first, double second, double t)
    {
        var inverse = 1 - t;
        return (3 * inverse * inverse * t) * first + (3 * inverse * t * t) * second + t * t * t;
    }
}
