namespace Duxel.Core;

/// <summary>Resolved Fluent8830c10b Tag tokens using the pinned web brand ramp.</summary>
public static class UiFluentTagPalette
{
    public static UiBadgePaint Resolve(UiFluentColorScheme scheme, UiTagAppearance appearance, UiButtonVisualState state,
        bool interactive = false, bool secondary = false, bool selected = false)
    {
        if ((uint)scheme > 1 || (uint)appearance > 2 || (uint)state > 3)
            throw new ArgumentOutOfRangeException(nameof(state));
        var dark = scheme == UiFluentColorScheme.Dark;
        var disabled = state == UiButtonVisualState.Disabled;
        var pressed = state == UiButtonVisualState.Pressed;
        UiColor border = appearance == UiTagAppearance.Outline
            ? disabled ? dark ? new(66, 66, 66) : new(224, 224, 224) : dark ? new(102, 102, 102) : new(209, 209, 209)
            : default;
        if (disabled)
            return new(appearance == UiTagAppearance.Outline ? default : dark ? new(20, 20, 20) : new(240, 240, 240),
                dark ? new(92, 92, 92) : new(189, 189, 189), border);
        if (selected)
        {
            var background = interactive && state != UiButtonVisualState.Rest
                ? pressed ? new UiColor(12, 59, 94) : dark ? new UiColor(15, 108, 189) : new UiColor(17, 94, 163)
                : dark ? new UiColor(17, 94, 163) : new UiColor(15, 108, 189);
            return new(background, new(255, 255, 255), dark ? new(71, 158, 245) : new(15, 108, 189));
        }
        UiColor fill = appearance switch
        {
            UiTagAppearance.Outline => default,
            UiTagAppearance.Brand => dark ? new(8, 35, 56) : new(235, 243, 252),
            _ => dark ? new(20, 20, 20) : new(245, 245, 245)
        };
        UiColor foreground = appearance == UiTagAppearance.Brand
            ? dark ? new(98, 171, 245) : new(17, 94, 163)
            : dark ? new(214, 214, 214) : new(66, 66, 66);
        if (interactive && state != UiButtonVisualState.Rest)
        {
            fill = appearance switch
            {
                UiTagAppearance.Outline => dark ? pressed ? new(46, 46, 46) : new(56, 56, 56) : pressed ? new(224, 224, 224) : new(245, 245, 245),
                UiTagAppearance.Brand => dark ? pressed ? new(6, 23, 36) : new(12, 59, 94) : pressed ? new(150, 198, 250) : new(207, 228, 250),
                _ => dark ? pressed ? new(10, 10, 10) : new(41, 41, 41) : pressed ? new(214, 214, 214) : new(235, 235, 235)
            };
            foreground = appearance == UiTagAppearance.Brand ? Dismiss(scheme, state)
                : secondary ? dark ? pressed ? new(40, 134, 222) : new(71, 158, 245) : pressed ? new(17, 94, 163) : new(15, 108, 189)
                : dark ? new(255, 255, 255) : new(36, 36, 36);
        }
        return new(fill, foreground, border);
    }
    internal static UiColor Dismiss(UiFluentColorScheme scheme, UiButtonVisualState state)
        => scheme == UiFluentColorScheme.Dark
            ? state == UiButtonVisualState.Pressed ? new(40, 134, 222) : new(98, 171, 245)
            : state == UiButtonVisualState.Pressed ? new(15, 84, 140) : new(17, 94, 163);
    internal static UiColor Divider(UiFluentColorScheme scheme, UiTagAppearance appearance, bool enabled, bool selected = false)
        => !enabled
            ? scheme == UiFluentColorScheme.Dark ? new(66, 66, 66) : new(224, 224, 224)
            : selected ? new(255, 255, 255) : appearance == UiTagAppearance.Brand
                ? scheme == UiFluentColorScheme.Dark ? new(14, 71, 117) : new(180, 214, 250)
                : scheme == UiFluentColorScheme.Dark ? new(102, 102, 102) : new(209, 209, 209);
}
