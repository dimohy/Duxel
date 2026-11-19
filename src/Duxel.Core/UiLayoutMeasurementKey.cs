namespace Duxel.Core;

internal readonly record struct UiLayoutMeasurementKey(object Resources, UiControlTextLayoutKey Text,
    UiLayoutCultureKey Culture, UiDesignTokens Tokens, UiTypographyFonts? Fonts,
    float LineHeight, float BaseFontSize, bool DirectText, bool Fallback, bool RightToLeft);

// Equal culture names may still have different user-defined numeric formats.
internal readonly record struct UiLayoutCultureKey(System.Globalization.CultureInfo Culture)
{
    public bool Equals(UiLayoutCultureKey other) => ReferenceEquals(Culture, other.Culture);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Culture);
}

public readonly partial struct UiControlContext
{
    internal int LayoutFrame => _ui.GetFrameCount();
    internal UiLayoutMeasurementKey LayoutMeasurementKey => _ui.LayoutMeasurementKey;
    internal bool CanRetainLayoutMeasurement => _ui.CanRetainControlTextLayout;
}
public sealed partial class UiImmediateContext
{
    // This token identifies resource configuration without retaining the context, backend or atlas in a widget.
    private object _layoutMeasurementResources = new();
    internal UiLayoutMeasurementKey LayoutMeasurementKey => new(_layoutMeasurementResources, CurrentControlTextLayoutKey,
        new(System.Globalization.CultureInfo.CurrentCulture), _designTokens, _typographyFonts,
        _lineHeight, _directTextBaseFontSize, _directTextEnabled, _directTextFallbackEnabled, RightToLeft);
    private void InvalidateLayoutMeasurementResources() => _layoutMeasurementResources = new();
}
