namespace Duxel.Core;

internal readonly record struct UiSpinButtonPaint(UiColor Background, UiColor Foreground);

internal static class UiFluentSpinButtonPalette
{
    internal static UiSpinButtonPaint Resolve(UiControlContext context, UiButtonVisualState state, UiInputAppearance appearance)
    {
        var disabled = state == UiButtonVisualState.Disabled;
        var interactive = state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed;
        if (context.Theme.ForcedColors is { } forced)
            return new(interactive ? forced.ButtonFace : default, disabled ? forced.GrayText : forced.ButtonText);

        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var foreground = disabled ? dark ? new UiColor(92, 92, 92) : new(189, 189, 189)
            : interactive ? dark ? new(214, 214, 214) : new(66, 66, 66)
            : dark ? new(173, 173, 173) : new(97, 97, 97);
        if (!interactive)
            return new(default, foreground);
        var pressed = state == UiButtonVisualState.Pressed;
        var background = appearance switch
        {
            UiInputAppearance.FilledDarker => dark
                ? pressed ? new UiColor(10, 10, 10) : new(41, 41, 41)
                : pressed ? new UiColor(214, 214, 214) : new(235, 235, 235),
            UiInputAppearance.FilledLighter => dark
                ? pressed ? new UiColor(31, 31, 31) : new(61, 61, 61)
                : pressed ? new UiColor(224, 224, 224) : new(245, 245, 245),
            _ => UiFluentButtonPalette.Subtle(context.FluentColorScheme, state).Background
        };
        return new(background, foreground);
    }
}
