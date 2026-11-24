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
        if (content.SubText is { } description)
            using (context.BeginTypography<UiRegularTypeface, UiFluentType100>())
                label = MathF.Max(label, context.MeasureTextBlock(description).Size.X);
        var trailing = 0f;
        if (content.SecondaryContent is { } secondary)
            using (context.BeginTypography<UiRegularTypeface, UiMenuSecondaryMetrics>()) trailing = context.MeasureText(secondary).X + 8;
        return label + 2 * textInset + (content.HasCheckmarks ? 20 : 0) + (content.HasIcons ? 24 : 0)
            + trailing + (content.HasSubmenu ? 24 : 0);
    }

    internal static float MeasureHeight(UiControlContext context, UiMenuItemContent content, float width,
        UiMenuOptions options, float textInset, float slotInset, bool wrapLabel)
    {
        if (!wrapLabel && content.SubText is null)
            return options.ItemHeight;
        var labelWidth = Arrange(context, content, new(0, 0, width, options.ItemHeight),
            options.RightToLeft, textInset, slotInset).Label.Width;
        var (label, description) = MeasureText(context, content, labelWidth, wrapLabel);
        return label.LineCount <= 1 && description.LineCount == 0
            ? options.ItemHeight : MathF.Max(options.ItemHeight, label.Size.Y + description.Size.Y + 12);
    }

    internal static UiMenuContentLayout Arrange(UiControlContext context, UiMenuItemContent content, UiRect bounds,
        bool rightToLeft, float textInset, float slotInset)
    {
        var checks = content.HasCheckmarks ? 20 : 0;
        var icons = content.HasIcons ? 24 : 0;
        var indicator = content.HasSubmenu ? 24 : 0;
        var available = MathF.Max(0, bounds.Width - 2 * textInset - checks - icons - indicator);
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
            Slot(bounds.Width - textInset - secondary - indicator, secondary, 20));
    }

    internal static void Paint(UiControlContext context, UiMenuItemContent content, UiRect bounds, UiButtonInteraction interaction,
        UiMenuOptions options, UiColor labelColor, UiColor iconColor, UiColor secondaryColor, UiColor secondaryHover,
        float textInset, float slotInset, bool wrapLabel = false, UiColor? subTextColor = null)
    {
        using var clip = context.Clip(bounds);
        var slots = wrapLabel || content.SubText is not null ? bounds with { Height = MathF.Min(bounds.Height, options.ItemHeight) } : bounds;
        var layout = Arrange(context, content, slots, options.RightToLeft, textInset, slotInset);
        if (content.Checked == true) UiFluentCheckboxCheck16.Paint(context, layout.Checkmark, labelColor, interaction);
        if (!content.Icon.IsEmpty) content.Icon.Paint(context, layout.Icon, iconColor, interaction);
        if (content.HasSubmenu)
        {
            var centerX = options.RightToLeft ? bounds.X + 16 : bounds.X + bounds.Width - 16;
            var indicator = new UiRect(centerX - 10, slots.Y + (slots.Height - 20) * .5f, 20, 20);
            if (options.RightToLeft)
                UiMenuSubmenuLeft.Paint(context, indicator, labelColor, interaction);
            else
                UiMenuSubmenuRight.Paint(context, indicator, labelColor, interaction);
        }
        using (context.Clip(layout.Label with { Height = bounds.Height }))
        {
            var description = MeasureSubText(context, content.SubText, layout.Label.Width);
            var align = options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
            float descriptionY;
            using (context.BeginTypography<UiRegularTypeface, UiFluentType300>())
            {
                var label = MeasureLabel(context, content.Label, layout.Label.Width, wrapLabel || content.SubText is not null);
                var y = bounds.Y + (bounds.Height - label.Size.Y - description.Size.Y) * .5f;
                descriptionY = y + label.Size.Y;
                context.TypographyTextBlock(label, layout.Label with { Y = y, Height = label.Size.Y }, labelColor, align);
            }
            if (description.LineCount > 0)
                using (context.BeginTypography<UiRegularTypeface, UiFluentType100>())
                    context.TypographyTextBlock(description, layout.Label with { Y = descriptionY, Height = description.Size.Y },
                        subTextColor ?? secondaryColor, align);
        }
        if (content.SecondaryContent is { } secondary)
        {
            var hoverBounds = layout.Secondary with { X = layout.Secondary.X - 2, Width = layout.Secondary.Width + 4 };
            var color = context.IsEnabled && context.IsHovered(hoverBounds) ? secondaryHover : secondaryColor;
            using var secondaryClip = context.Clip(layout.Secondary);
            using var typography = context.BeginTypography<UiRegularTypeface, UiMenuSecondaryMetrics>();
            var text = context.MeasureTextBlock(secondary);
            context.TypographyTextBlock(text, layout.Secondary with { Y = slots.Y + (slots.Height - text.Size.Y) * .5f, Height = text.Size.Y },
                color, options.RightToLeft ? UiItemHorizontalAlign.Left : UiItemHorizontalAlign.Right);
        }
    }

    private static (UiControlTextBlock Label, UiControlTextBlock Description) MeasureText(UiControlContext context,
        UiMenuItemContent content, float labelWidth, bool wrapLabel)
    {
        UiControlTextBlock label;
        using (context.BeginTypography<UiRegularTypeface, UiFluentType300>())
            label = MeasureLabel(context, content.Label, labelWidth, wrapLabel || content.SubText is not null);
        return (label, MeasureSubText(context, content.SubText, labelWidth));
    }

    private static UiControlTextBlock MeasureSubText(UiControlContext context, string? text, float width)
    {
        if (text is null)
            return default;
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType100>();
        return MeasureLabel(context, text, width, wrap : true);
    }

    internal static UiControlTextBlock MeasureLabel(UiControlContext context, string label, float width, bool wrap)
    {
        var block = context.MeasureTextBlock(label);
        return wrap && width > 0 && block.Size.X > width
            ? context.MeasureTextBlock(UiMenuLabelWrapping.Wrap(context, label, width))
            : block;
    }
}

[UiFluentIcon("chevron_left", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiMenuSubmenuLeft;

[UiFluentIcon("chevron_right", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiMenuSubmenuRight;
