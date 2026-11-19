namespace Duxel.Core;

public sealed record UiTypographyFonts(string Regular, string Semibold, string Bold,
    string? SecondaryRegular = null, string? SecondarySemibold = null, string? SecondaryBold = null)
{
    public string? Monospace
    {
        get; init;
    }
}
public readonly record struct UiTypographyFontPaths(string Primary, string? Secondary);
public interface IUiTypographyTypeface
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiTypographyFontPaths Select(UiTypographyFonts fonts);
}
public readonly struct UiRegularTypeface : IUiTypographyTypeface
{
    public static bool CanRetainMeasurement => true;
    public static UiTypographyFontPaths Select(UiTypographyFonts fonts) => new(fonts.Regular, fonts.SecondaryRegular);
}
public readonly struct UiSemiboldTypeface : IUiTypographyTypeface
{
    public static bool CanRetainMeasurement => true;
    public static UiTypographyFontPaths Select(UiTypographyFonts fonts) => new(fonts.Semibold, fonts.SecondarySemibold);
}
public readonly struct UiBoldTypeface : IUiTypographyTypeface
{
    public static bool CanRetainMeasurement => true;
    public static UiTypographyFontPaths Select(UiTypographyFonts fonts) => new(fonts.Bold, fonts.SecondaryBold);
}
public readonly struct UiMonospaceTypeface : IUiTypographyTypeface
{
    public static bool CanRetainMeasurement => true;
    public static UiTypographyFontPaths Select(UiTypographyFonts fonts) =>
        new(fonts.Monospace ?? throw new InvalidOperationException("Code typography requires a configured monospace font."), fonts.SecondaryRegular);
}
public interface IUiTypographyMetrics
{
    static virtual bool CanRetainMeasurement => false;
    static abstract float FontSize
    {
        get;
    }
    static abstract float LineHeight
    {
        get;
    }
}
public readonly struct UiFluentTypographyPresentation<TTypeface, TMetrics> : IUiTextPresentation
    where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => TTypeface.CanRetainMeasurement && TMetrics.CanRetainMeasurement;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => context.MeasureTypography<TTypeface, TMetrics>(text);
    public static void Render(UiControlContext context, string text, UiColor? color) =>
        context.Typography<TTypeface, TMetrics>(text, color);
}
