namespace Duxel.Core;

internal readonly struct UiMenuSecondaryMetrics : IUiTypographyMetrics
{
    public static float FontSize => 12;
    public static float LineHeight => 20;
}

internal readonly record struct UiMenuContentLayout(UiRect Checkmark, UiRect Icon, UiRect Label, UiRect Secondary)
{
    internal static float Measure(UiControlContext context, UiMenuItemContent content, float textInset)
    {
        float label;
        using (context.BeginTypography<UiRegularTypeface, UiFluentType300>()) label = context.MeasureText(content.Label).X;
        var trailing = 0f;
        if (content.SecondaryContent is { } secondary)
            using (context.BeginTypography<UiRegularTypeface, UiMenuSecondaryMetrics>()) trailing = context.MeasureText(secondary).X + 8;
        return label + 2 * textInset + (content.HasCheckmarks ? 20 : 0) + (content.HasIcons ? 24 : 0) + trailing;
    }

    internal static UiMenuContentLayout Arrange(UiControlContext context, UiMenuItemContent content, UiRect bounds,
        bool rightToLeft, float textInset, float slotInset)
    {
        var checks = content.HasCheckmarks ? 20 : 0;
        var icons = content.HasIcons ? 24 : 0;
        var available = MathF.Max(0, bounds.Width - 2 * textInset - checks - icons);
        var secondary = 0f;
        if (content.SecondaryContent is { } text)
            using (context.BeginTypography<UiRegularTypeface, UiMenuSecondaryMetrics>())
                secondary = MathF.Min(context.MeasureText(text).X, MathF.Max(0, available - 8));
        var trailing = content.SecondaryContent is null ? 0 : secondary + 8;
        UiRect Slot(float start, float width, float height) => new(
            rightToLeft ? bounds.X + bounds.Width - start - width : bounds.X + start,
            bounds.Y + (bounds.Height - height) * .5f, width, height);
        return new(Slot(slotInset, 16, 16), Slot(slotInset + checks, 20, 20),
            Slot(textInset + checks + icons, MathF.Max(0, available - trailing), bounds.Height),
            Slot(bounds.Width - textInset - secondary, secondary, 20));
    }

    internal static void Paint(UiControlContext context, UiMenuItemContent content, UiRect bounds, UiButtonInteraction interaction,
        UiMenuOptions options, UiColor labelColor, UiColor iconColor, UiColor secondaryColor, UiColor secondaryHover,
        float textInset, float slotInset)
    {
        using var clip = context.Clip(bounds);
        var layout = Arrange(context, content, bounds, options.RightToLeft, textInset, slotInset);
        if (content.Checked == true) UiFluentCheckboxCheck16.Paint(context, layout.Checkmark, labelColor, interaction);
        if (!content.Icon.IsEmpty) content.Icon.Paint(context, layout.Icon, iconColor, interaction);
        using (context.Clip(layout.Label))
        using (context.BeginTypography<UiRegularTypeface, UiFluentType300>())
        {
            var label = context.MeasureTextBlock(content.Label);
            context.TypographyTextBlock(label, layout.Label with { Y = bounds.Y + (bounds.Height - label.Size.Y) * .5f, Height = label.Size.Y },
                labelColor, options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left);
        }
        if (content.SecondaryContent is { } secondary)
        {
            var hoverBounds = layout.Secondary with { X = layout.Secondary.X - 2, Width = layout.Secondary.Width + 4 };
            var color = context.IsEnabled && context.IsHovered(hoverBounds) ? secondaryHover : secondaryColor;
            using var secondaryClip = context.Clip(layout.Secondary);
            using var typography = context.BeginTypography<UiRegularTypeface, UiMenuSecondaryMetrics>();
            var text = context.MeasureTextBlock(secondary);
            context.TypographyTextBlock(text, layout.Secondary with { Y = bounds.Y + (bounds.Height - text.Size.Y) * .5f, Height = text.Size.Y },
                color, options.RightToLeft ? UiItemHorizontalAlign.Left : UiItemHorizontalAlign.Right);
        }
    }
}
