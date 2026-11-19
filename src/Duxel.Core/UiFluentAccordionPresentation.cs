namespace Duxel.Core;

public readonly struct UiFluentAccordionPresentation : IUiAccordionPresentation
{
    public static float PanelMargin => 12;
    private static UiControlTypographyScope Typography(UiControlContext context, UiAccordionSize size) => size switch
    {
        UiAccordionSize.Small => context.BeginTypography<UiRegularTypeface, UiFluentType200>(),
        UiAccordionSize.Large => context.BeginTypography<UiRegularTypeface, UiFluentType400>(),
        UiAccordionSize.ExtraLarge => context.BeginTypography<UiRegularTypeface, UiFluentType500>(),
        _ => context.BeginTypography<UiRegularTypeface, UiFluentType300>()
    };
    public static UiAccordionHeaderLayout MeasureHeader(UiControlContext context, UiAccordionItem item,
        float width, UiAccordionOptions options)
    {
        using var typography = Typography(context, options.Size);
        var reserved = 22 + 20 + 8 + (item.Icon.IsEmpty ? 0 : 28);
        var text = context.MeasureTextBlock(context.WrapText(item.Label, MathF.Max(1, width - reserved)));
        return new(text, new(width, MathF.Max(options.Size == UiAccordionSize.Small ? 32 : 44, text.Size.Y)));
    }
    public static void PaintHeader(UiControlContext context, UiAccordionItem item, UiAccordionHeaderLayout layout,
        UiRect bounds, bool expanded, UiButtonInteraction interaction, UiAccordionOptions options)
    {
        using var clip = context.Clip(bounds);
        using var typography = Typography(context, options.Size);
        var enabled = context.IsEnabled && item.Enabled;
        if (context.IsHovered(bounds))
            context.SetMouseCursor(enabled ? UiMouseCursor.Hand : UiMouseCursor.NotAllowed);
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var color = enabled ? dark ? new UiColor(255, 255, 255) : new(36, 36, 36)
            : dark ? new UiColor(92, 92, 92) : new(189, 189, 189);
        var start = options.ChevronPosition == UiAccordionChevronPosition.Start;
        var x = bounds.X + (start ? 10 : 12);
        var center = new UiVector2(start ? x + 10 : bounds.X + bounds.Width - 18, bounds.Y + bounds.Height * .5f);
        context.PaintDisclosure(center, color, expanded, -90);
        if (start)
            x += 28;
        if (!item.Icon.IsEmpty)
        {
            item.Icon.Paint(context, new(x, bounds.Y + (bounds.Height - 20) * .5f, 20, 20), color, interaction);
            x += 28;
        }
        var textWidth = MathF.Max(0, bounds.X + bounds.Width - (start ? 12 : 38) - x);
        context.TypographyTextBlock(layout.Text,
            new(x, bounds.Y + (bounds.Height - layout.Text.Size.Y) * .5f, textWidth, layout.Text.Size.Y), color);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4);
    }
}
