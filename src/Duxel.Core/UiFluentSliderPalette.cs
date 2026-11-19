namespace Duxel.Core;

public readonly record struct UiSliderPaint(UiColor Rail, UiColor Fill, UiColor ThumbBorder, UiColor Surface);

/// <summary>Fluent Slider tokens resolved against the pinned web light/dark brand.</summary>
public static class UiFluentSliderPalette
{
    public static UiSliderPaint Resolve(UiFluentColorScheme scheme, UiButtonVisualState state)
    {
        // Checkbox uses the same compound-brand tokens; disabled foreground is shared as well.
        var compound = UiFluentCheckboxPalette.Resolve(scheme, state, null);
        var dark = scheme == UiFluentColorScheme.Dark;
        var disabled = state == UiButtonVisualState.Disabled;
        return new(
            disabled ? dark ? new(20, 20, 20) : new(240, 240, 240) : dark ? new(173, 173, 173) : new(97, 97, 97),
            disabled ? compound.Indicator : compound.Border,
            disabled ? compound.Indicator : dark ? new UiColor(102, 102, 102) : new(209, 209, 209),
            dark ? new(41, 41, 41) : new(255, 255, 255));
    }
}
