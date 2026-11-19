namespace Duxel.Core;

public readonly struct UiFluentNavPresentation : IUiNavPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiNavRowLayout Measure(UiControlContext context, UiNavRowContent content, float width)
    {
        if (content.SectionHeader)
        {
            using var caption = context.BeginTypography<UiSemiboldTypeface, UiFluentType200>();
            var heading = context.MeasureTextBlock(context.WrapText(content.Label, MathF.Max(1, width - 20)));
            return new(new(width, heading.Size.Y + 16), heading, default, default);
        }
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var badge = content.Badge is { } status ? context.MeasureFluentBadge<UiNoBadgeIcon>(new(status.Text), status.Options) : default;
        var reserved = (content.Category ? 44 : 58) + (content.Icon.IsEmpty ? 0 : 28) + (badge.X > 0 ? badge.X + 8 : 0) + (content.HasSecondaryAction ? 36 : 0);
        var text = context.MeasureTextBlock(context.WrapText(content.Label, MathF.Max(1, width - reserved)));
        UiControlTextBlock description = default;
        if (content.Description.Length > 0)
        {
            using var caption = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
            description = context.MeasureTextBlock(context.WrapText(content.Description, MathF.Max(1, width - reserved)));
        }
        var textHeight = text.Size.Y + (description.Size.Y > 0 ? description.Size.Y + 2 : 0);
        return new(new(width, MathF.Max(36, MathF.Max(textHeight + 12, badge.Y + 8))), text, badge, description);
    }
    public static void Paint(UiControlContext context, UiNavRowContent content, UiNavRowLayout layout,
        UiRect bounds, bool current, bool expanded, UiButtonInteraction interaction, UiNavOptions options)
    {
        using var clip = context.Clip(bounds);
        if (content.SectionHeader)
        {
            using var caption = context.BeginTypography<UiSemiboldTypeface, UiFluentType200>();
            var color = context.Theme.ForcedColors is { } headerColors ? headerColors.CanvasText
                : context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(173, 173, 173) : new(97, 97, 97);
            context.TypographyTextBlock(layout.Text, new(bounds.X + 10, bounds.Y + 8,
                MathF.Max(0, bounds.Width - 20), layout.Text.Size.Y), color,
                options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left);
            return;
        }
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var foreground = context.IsEnabled ? dark ? new UiColor(242, 242, 242) : new(36, 36, 36) : context.Theme.TextDisabled;
        var accent = dark ? new UiColor(71, 158, 245) : new(15, 108, 189);
        var background = interaction.Held ? dark ? new UiColor(61, 61, 61) : new(209, 209, 209)
            : current || interaction.Hovered ? dark ? new UiColor(51, 51, 51) : new(235, 235, 235) : new(0, 0, 0, 0);
        if (context.Theme.ForcedColors is { } forced)
        {
            foreground = !context.IsEnabled ? forced.GrayText : current || interaction.Hovered ? forced.HighlightText : forced.CanvasText;
            background = current || interaction.Hovered ? forced.Highlight : forced.Canvas;
            accent = forced.HighlightText;
        }
        context.Frame(new(bounds.X + 2, bounds.Y + 1, MathF.Max(0, bounds.Width - 4), MathF.Max(0, bounds.Height - 2)),
            background, new(0, 0, 0, 0), 4, 0);
        if ((!content.Category || content.Disclosure) && context.IsHovered(bounds)) context.SetMouseCursor(context.IsEnabled ? UiMouseCursor.Hand : UiMouseCursor.NotAllowed);
        var rtl = options.RightToLeft;
        var leading = content.Category ? 12f : 46f;
        var trailing = content.Category ? 32f : 12f;
        var textLeft = bounds.X + (rtl ? trailing : leading);
        var textRight = bounds.X + bounds.Width - (rtl ? leading : trailing);
        if (content.HasSecondaryAction)
        {
            if (rtl) textLeft += 36; else textRight -= 36;
        }
        if (!content.Icon.IsEmpty)
        {
            content.Icon.Paint(context, new(rtl ? textRight - 20 : textLeft, bounds.Y + (bounds.Height - 20) / 2, 20, 20), foreground, interaction);
            if (rtl) textRight -= 28; else textLeft += 28;
        }
        if (content.Badge is { } status)
        {
            var badgeX = rtl ? textLeft : textRight - layout.BadgeSize.X;
            context.PaintFluentBadge(new(badgeX, bounds.Y + (bounds.Height - layout.BadgeSize.Y) / 2, layout.BadgeSize.X, layout.BadgeSize.Y),
                new(status.Text), status.Options);
            if (rtl) textLeft += layout.BadgeSize.X + 8; else textRight -= layout.BadgeSize.X + 8;
        }
        var align = rtl ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
        var textHeight = layout.Text.Size.Y + (layout.Description.Size.Y > 0 ? layout.Description.Size.Y + 2 : 0);
        var textY = bounds.Y + (bounds.Height - textHeight) / 2;
        context.TypographyTextBlock(layout.Text, new(textLeft, textY,
            MathF.Max(0, textRight - textLeft), layout.Text.Size.Y), foreground, align);
        if (layout.Description.Size.Y > 0)
        {
            using var caption = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
            context.TypographyTextBlock(layout.Description, new(textLeft, textY + layout.Text.Size.Y + 2,
                MathF.Max(0, textRight - textLeft), layout.Description.Size.Y), foreground, align);
        }
        if (content.Category && content.Disclosure)
        {
            var center = new UiVector2(rtl ? bounds.X + 16 : bounds.X + bounds.Width - 16, bounds.Y + bounds.Height / 2);
            context.PaintDisclosure(center, foreground, expanded, rtl ? 90 : -90);
        }
        if (current)
            context.Frame(new(rtl ? bounds.X + bounds.Width - 5 : bounds.X + 2, bounds.Y + 10, 3, MathF.Max(4, bounds.Height - 20)),
                accent, accent, 1.5f, 0);
        if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
    }
    public static UiRect SecondaryActionBounds(UiRect row, UiNavOptions options)
    {
        var width = MathF.Min(28, MathF.Max(0, row.Width - 8));
        var height = MathF.Min(28, row.Height);
        return new(options.RightToLeft ? row.X + 4 : row.X + row.Width - 4 - width,
            row.Y + (row.Height - height) / 2, width, height);
    }
    public static void PaintSecondaryAction(UiControlContext context, UiNavSecondaryAction action, UiRect bounds,
        UiButtonInteraction interaction, UiNavOptions options)
    {
        using var clip = context.Clip(bounds);
        var fill = interaction.Held ? context.Theme.ButtonActive : interaction.Hovered ? context.Theme.ButtonHovered : new UiColor(0, 0, 0, 0);
        var color = context.IsEnabled ? context.Theme.Text : context.Theme.TextDisabled;
        if (context.Theme.ForcedColors is { } forced)
        {
            fill = interaction.Hovered || interaction.FocusVisible ? forced.Highlight : forced.Canvas;
            color = !context.IsEnabled ? forced.GrayText : interaction.Hovered || interaction.FocusVisible ? forced.HighlightText : forced.CanvasText;
        }
        context.Frame(bounds, fill, new(0, 0, 0, 0), 4, 0);
        var size = MathF.Min(20, MathF.Min(bounds.Width, bounds.Height));
        action.Icon.Paint(context, new(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size), color, interaction);
        if (context.IsHovered(bounds)) context.SetMouseCursor(context.IsEnabled ? UiMouseCursor.Hand : UiMouseCursor.NotAllowed);
        if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
    }

}
