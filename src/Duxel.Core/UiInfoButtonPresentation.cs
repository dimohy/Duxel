namespace Duxel.Core;

[UiFluentIcon("info", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiInfo12Regular;
[UiFluentIcon("info", UiFluentIconStyle.Filled, 12)] internal readonly partial struct UiInfo12Filled;
[UiFluentIcon("info", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiInfo16Regular;
[UiFluentIcon("info", UiFluentIconStyle.Filled, 16)] internal readonly partial struct UiInfo16Filled;
[UiFluentIcon("info", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiInfo20Regular;
[UiFluentIcon("info", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiInfo20Filled;

internal interface IUiInfoSize
{
    static abstract int Edge
    {
        get;
    }
    static abstract int ButtonEdge
    {
        get;
    }
    static abstract UiControlTypographyScope BeginBody(UiControlContext context);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction);
}
internal readonly struct UiInfoSmall : IUiInfoSize
{
    public static int Edge => 12;
    public static int ButtonEdge => 20;
    public static UiControlTypographyScope BeginBody(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType200>();
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
    {
        if (interaction.Expanded || interaction.Hovered)
            UiInfo12Filled.Paint(context, bounds, color, interaction);
        else
            UiInfo12Regular.Paint(context, bounds, color, interaction);
    }
}
internal readonly struct UiInfoMedium : IUiInfoSize
{
    public static int Edge => 16;
    public static int ButtonEdge => 24;
    public static UiControlTypographyScope BeginBody(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType200>();
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
    {
        if (interaction.Expanded || interaction.Hovered)
            UiInfo16Filled.Paint(context, bounds, color, interaction);
        else
            UiInfo16Regular.Paint(context, bounds, color, interaction);
    }
}
internal readonly struct UiInfoLarge : IUiInfoSize
{
    public static int Edge => 20;
    public static int ButtonEdge => 24;
    public static UiControlTypographyScope BeginBody(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
    {
        if (interaction.Expanded || interaction.Hovered)
            UiInfo20Filled.Paint(context, bounds, color, interaction);
        else
            UiInfo20Regular.Paint(context, bounds, color, interaction);
    }
}
internal readonly struct UiInfoButtonPresentation<TSize> : IUiPopoverPresentation where TSize : IUiInfoSize
{
    public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => default;
    public static UiControlTypographyScope BeginBodyText(UiControlContext context) => TSize.BeginBody(context);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => new(TSize.ButtonEdge, TSize.ButtonEdge);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
    {
        if (context.IsEnabled && interaction.Hovered)
            context.SetMouseCursor(UiMouseCursor.Hand);
        var color = UiFluentTransparentButtonAppearance.Paint(context, bounds,
            interaction with
            {
                Hovered = interaction.Hovered || interaction.Expanded
            });
        using var clip = context.Clip(bounds);
        TSize.Paint(context, new(bounds.X + (bounds.Width - TSize.Edge) / 2,
            bounds.Y + (bounds.Height - TSize.Edge) / 2, TSize.Edge, TSize.Edge), color, interaction);
    }
    public static float Padding(UiPopoverOptions options) => UiFluentPopoverPresentation.Padding(options);
    public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => UiFluentPopoverPresentation.Foreground(context, options);
    public static UiRect PaintBounds(UiControlContext context, UiPopupLayout layout, UiPopoverOptions options) => UiFluentPopoverPresentation.PaintBounds(context, layout, options);
    public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
        => UiFluentPopoverPresentation.PaintSurface(context, surface, layout, options);
}
