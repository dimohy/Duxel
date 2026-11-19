namespace Duxel.Core;

public readonly struct UiFluentPopoverPresentation : IUiPopoverPresentation
{
    public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => UiFluentButtonPresentation.BeginText(context, true);
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiFluentButtonPresentation.Measure(context, context.MeasureText(label), default);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
        => UiFluentButtonPresentation.Paint(context, label, context.MeasureText(label), bounds, interaction);
    public static float Padding(UiPopoverOptions options) => (options.Size == UiPopoverSize.Small ? 12 : options.Size == UiPopoverSize.Large ? 20 : 16) + 1;
    public static UiColor Foreground(UiControlContext context, UiPopoverOptions options)
        => options.Appearance == UiPopoverAppearance.Neutral ? context.FluentColorScheme == UiFluentColorScheme.Dark ? new(255, 255, 255) : new(36, 36, 36) : new(255, 255, 255);
    public static UiRect PaintBounds(UiControlContext context, UiPopupLayout layout, UiPopoverOptions options)
    {
        var body = layout.Bounds;
        return new(body.X - 88, body.Y - 88, body.Width + 176, body.Height + 176);
    }
    public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
    {
        var fill = options.Appearance == UiPopoverAppearance.Brand ? UiFluentButtonPalette.Primary(context.FluentColorScheme, UiButtonVisualState.Rest).Background
            : options.Appearance == UiPopoverAppearance.Inverted ? new(51, 51, 51)
            : context.FluentColorScheme == UiFluentColorScheme.Dark ? new(41, 41, 41) : new UiColor(255, 255, 255);
        context.FluentPopupSurface(surface, layout, fill);
    }
}
