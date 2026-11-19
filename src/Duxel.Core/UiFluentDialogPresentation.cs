namespace Duxel.Core;

public readonly struct UiFluentDialogPresentation : IUiDialogPresentation
{
    private static readonly UiMenuIcon Dismiss = UiMenuIcon.Create<UiSearchBoxDismissIcon, UiSearchBoxDismissIcon>();
    public static UiDialogMetrics Metrics => new(24, 8, 8, 1, 32);
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>(true);
    public static UiVector2 MeasureTitle(UiControlContext context, string title) => context.MeasureTypography<UiSemiboldTypeface, UiFluentType500>(title, wrap: true);
    public static void PaintTitle(UiControlContext context, string title, UiColor? color) => context.TypographyWrapped<UiSemiboldTypeface, UiFluentType500>(title, color);
    public static UiBoxShadowOptions Shadow(UiControlContext context) => UiFluentShadow.Resolve(context.FluentColorScheme, UiShadowElevation.Shadow64);
    public static UiColor Background(UiControlContext context) => context.FluentColorScheme == UiFluentColorScheme.Dark ? new(41, 41, 41) : new(255, 255, 255);
    public static UiColor Foreground(UiControlContext context) => context.FluentColorScheme == UiFluentColorScheme.Dark ? new(255, 255, 255) : new(36, 36, 36);
    public static UiColor Backdrop(UiControlContext context) => new(0, 0, 0, 102);
    public static void PaintClose(UiControlContext context, UiRect bounds, UiButtonInteraction interaction)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentButtonPalette.Subtle(context.FluentColorScheme, state);
        var fill = palette.Background;
        var foreground = palette.Foreground;
        if (context.Theme.ForcedColors is { } forced)
        {
            var highlighted = interaction.Hovered || interaction.FocusVisible;
            fill = highlighted ? forced.Highlight : forced.ButtonFace;
            foreground = !context.IsEnabled ? forced.GrayText : highlighted ? forced.HighlightText : forced.ButtonText;
        }
        context.Frame(bounds, fill, palette.Border, 4, 0);
        Dismiss.Paint(context, new(bounds.X + 2, bounds.Y + 2, 20, 20), foreground, interaction);
        if (context.IsHovered(bounds)) context.SetMouseCursor(context.IsEnabled ? UiMouseCursor.Hand : UiMouseCursor.NotAllowed);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4);
    }
}
