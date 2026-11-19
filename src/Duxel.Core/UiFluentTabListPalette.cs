namespace Duxel.Core;

public readonly record struct UiTabPaint(UiColor Background, UiColor Border, UiColor Text,
    UiColor Icon, UiColor Indicator);

/// <summary>Fluent UI8830c10b tab tokens resolved against brandWeb in light and dark themes.</summary>
public static class UiFluentTabListPalette
{
    public static UiTabPaint Resolve(UiFluentColorScheme scheme, UiButtonVisualState state,
        UiTabListAppearance appearance, bool selected)
    {
        if (scheme is not (UiFluentColorScheme.Light or UiFluentColorScheme.Dark) || (uint)state > 3 || (uint)appearance > 3)
            throw new ArgumentOutOfRangeException(nameof(state));
        var dark = scheme == UiFluentColorScheme.Dark;
        var disabled = state == UiButtonVisualState.Disabled;
        var neutral = UiFluentButtonPalette.Resolve(scheme, state);
        var subtle = UiFluentButtonPalette.Subtle(scheme, state);
        var brand = UiFluentCheckboxPalette.Resolve(scheme, state, null).Border;
        if (appearance is UiTabListAppearance.Transparent or UiTabListAppearance.Subtle)
            return new(appearance == UiTabListAppearance.Subtle ? subtle.Background : default, default,
                selected ? neutral.Foreground : subtle.Foreground,
                selected && !disabled ? brand : subtle.Foreground,
                selected ? disabled ? neutral.Foreground : brand : !disabled && state != UiButtonVisualState.Rest ? neutral.Border : default);

        if (disabled)
        {
            var filled = selected || appearance == UiTabListAppearance.FilledCircular;
            var background = filled ? dark ? new UiColor(20, 20, 20) : new(240, 240, 240) : default;
            return new(background, selected ? neutral.Border : default, neutral.Foreground, neutral.Foreground, default);
        }
        if (appearance == UiTabListAppearance.FilledCircular)
        {
            var background = selected ? (dark, state) switch
            {
                (false, UiButtonVisualState.Rest) => new UiColor(15, 108, 189),
                (false, UiButtonVisualState.Hover) => new(17, 94, 163),
                (true, UiButtonVisualState.Rest) => new(17, 94, 163),
                (true, UiButtonVisualState.Hover) => new(15, 108, 189),
                _ => new(12, 59, 94)
            } : (dark, state) switch
            {
                (false, UiButtonVisualState.Rest) => new UiColor(245, 245, 245),
                (false, UiButtonVisualState.Hover) => new(235, 235, 235),
                (false, _) => new(214, 214, 214),
                (true, UiButtonVisualState.Rest) => new(20, 20, 20),
                (true, UiButtonVisualState.Hover) => new(41, 41, 41),
                _ => new(10, 10, 10)
            };
            var foreground = selected ? new UiColor(255, 255, 255) : subtle.Foreground;
            return new(background, default, foreground, foreground, default);
        }
        if (!selected)
            return new(subtle.Background, state == UiButtonVisualState.Rest ? default : neutral.Border,
                subtle.Foreground, subtle.Foreground, default);
        var fill = (dark, state) switch
        {
            (false, UiButtonVisualState.Rest) => new UiColor(235, 243, 252),
            (false, UiButtonVisualState.Hover) => new(207, 228, 250),
            (false, _) => new(150, 198, 250),
            (true, UiButtonVisualState.Rest) => new(8, 35, 56),
            (true, UiButtonVisualState.Hover) => new(12, 59, 94),
            _ => new(6, 23, 36)
        };
        var text = (dark, state) switch
        {
            (false, UiButtonVisualState.Rest) => new UiColor(17, 94, 163),
            (false, UiButtonVisualState.Hover) => new(15, 84, 140),
            (false, _) => new(10, 46, 74),
            (true, UiButtonVisualState.Rest) => new(98, 171, 245),
            (true, UiButtonVisualState.Hover) => new(150, 198, 250),
            _ => new(235, 243, 252)
        };
        return new(fill, brand, text, text, default);
    }
}
