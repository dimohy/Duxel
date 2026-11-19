namespace Duxel.Core;

internal readonly record struct UiOptionPaint(UiColor Background, UiColor Foreground,
    UiColor Secondary, UiColor SecondaryHover, UiColor CheckBackground, UiColor CheckBorder, UiColor CheckIndicator);

/// <summary>Option roles remain separate from MenuItem's explicit Highlight hover treatment.</summary>
internal static class UiFluentOptionPalette
{
    internal static UiOptionPaint Resolve(UiControlContext context, UiButtonInteraction interaction, bool selected)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var row = UiFluentButtonPalette.Resolve(context.FluentColorScheme, state);
        var mark = UiFluentCheckboxPalette.Resolve(context.FluentColorScheme, state, selected);
        if (context.Theme.ForcedColors is { } colors)
        {
            var foreground = state == UiButtonVisualState.Disabled ? colors.GrayText : colors.CanvasText;
            return new(state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed ? colors.Canvas : default,
                foreground, foreground, foreground, selected ? colors.Canvas : default, foreground, foreground);
        }
        return new(interaction.Hovered || interaction.Held ? row.Background : default,
            row.Foreground,
            UiFluentMenuPalette.Secondary(context.FluentColorScheme, false, context.IsEnabled),
            UiFluentMenuPalette.Secondary(context.FluentColorScheme, true, context.IsEnabled),
            mark.Background, mark.Border, mark.Indicator);
    }
}
