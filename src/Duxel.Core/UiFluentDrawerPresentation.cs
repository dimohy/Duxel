namespace Duxel.Core;

public readonly struct UiFluentDrawerPresentation : IUiDrawerPresentation
{
    public static UiDialogMetrics Metrics => new(24, 12, 0, 1, 32);
    public static UiVector2 HeaderNavigationOutset => new(16, 8);
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => UiFluentDialogPresentation.BeginBodyText(context);
    public static UiVector2 MeasureTitle(UiControlContext context, string title) => UiFluentDialogPresentation.MeasureTitle(context, title);
    public static void PaintTitle(UiControlContext context, string title, UiColor? color) => UiFluentDialogPresentation.PaintTitle(context, title, color);
    public static UiBoxShadowOptions Shadow(UiControlContext context) => UiFluentShadow.Resolve(context.FluentColorScheme, UiShadowElevation.Shadow64);
    public static UiColor Background(UiControlContext context) => context.Theme.ForcedColors?.Canvas ?? UiFluentDialogPresentation.Background(context);
    public static UiColor Foreground(UiControlContext context) => context.Theme.ForcedColors?.CanvasText ?? UiFluentDialogPresentation.Foreground(context);
    public static UiColor Backdrop(UiControlContext context) => UiFluentDialogPresentation.Backdrop(context);
    public static UiColor? ScrollSeparator(UiControlContext context) => context.Theme.ForcedColors?.CanvasText
        ?? (context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(102, 102, 102) : new UiColor(209, 209, 209));
    public static void PaintClose(UiControlContext context, UiRect bounds, UiButtonInteraction interaction) => UiFluentDialogPresentation.PaintClose(context, bounds, interaction);
}
