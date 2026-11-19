namespace Duxel.Core;

internal static class UiSegmentedControlMotion
{
    internal static UiTransitionOptions Options(bool reducedMotion) => new(.2, ReducedMotion : reducedMotion)
    {
        Curve = new(0, 0, .58, 1)
    };
}

/// <summary>Fluent Apple's body2 font size with the shared twenty-unit line box.</summary>
public readonly struct UiSegmentedControlTypography : IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize => 15;
    public static float LineHeight => 20;
}
