namespace Duxel.Core;

public readonly struct UiFluentAvatarGroupPresentation : IUiAvatarGroupPresentation
{
    public static bool CanRetainMeasurement => true;
    public static float Spacing(UiAvatarGroupOptions options)
    {
        var size = new UiAvatarOptions(options.Size).PixelSize;
        return options.Layout switch
        {
            UiAvatarGroupLayout.Spread => size < 20 ? 8 : size < 32 ? 10 : size < 64 ? 16 : 24,
            UiAvatarGroupLayout.Stack => -(size < 24 ? 2 : size < 48 ? 4 : size < 96 ? 8 : 20),
            _ => 0
        };
    }
    public static float Divider(UiAvatarGroupOptions options)
    {
        var size = new UiAvatarOptions(options.Size).PixelSize;
        return size < 56 ? 2 : size < 72 ? 3 : 4;
    }
    public static void PaintSeparator(UiControlContext context, UiRect bounds, UiAvatarGroupOptions options)
    {
        var width = Divider(options);
        context.Frame(new(bounds.X - width, bounds.Y - width, bounds.Width + 2 * width, bounds.Height + 2 * width),
            context.FluentColorScheme == UiFluentColorScheme.Dark ? new(31, 31, 31) : new(250, 250, 250),
            default, bounds.Width, 0);
    }
    public static void PaintAvatar(UiControlContext context, UiRect bounds, UiAvatarContent content,
        UiAvatarOptions avatarOptions, bool squareEdges)
    {
        if (squareEdges)
            UiFluentAvatarPresentation.PaintGroupSlice(context, bounds, content, avatarOptions);
        else
            UiFluentAvatarPresentation.Paint(context, bounds, content, avatarOptions);
    }
    public static void PaintOverflow(UiControlContext context, UiRect bounds, string count,
        UiButtonInteraction interaction, UiAvatarGroupOptions options)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentButtonPalette.Resolve(context.FluentColorScheme, state);
        if (options.Layout != UiAvatarGroupLayout.Pie)
        {
            var size = new UiAvatarOptions(options.Size).PixelSize;
            context.Frame(bounds, paint.Background, paint.Border, bounds.Width * .5f, size < 36 ? 1 : size < 56 ? 2 : size < 72 ? 3 : 4);
            using var typography = UiFluentAvatarPresentation.Typography(context, new UiAvatarOptions(options.Size).PixelSize);
            context.TypographyButtonText(count, bounds, context.MeasureText(count),
                paint.Foreground);
        }
        if (interaction.FocusVisible)
            context.Frame(bounds, default, paint.Focus, bounds.Width * .5f, 2);
    }
}

internal readonly struct UiAvatarGroupMenuPresentation : IUiMenuPresentation
{
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiThemeMenuPresentation.MeasureTrigger(context, label);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction) =>
        UiThemeMenuPresentation.PaintTrigger(context, label, bounds, interaction);
    public static float MeasureItem(UiControlContext context, string label)
    {
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        return context.MeasureText(label).X + 16;
    }
    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) => MeasureItem(context, content.Label) + content.Icon.SizeHint + 8;
    public static void PaintSurface(UiControlContext context, UiRect bounds) => UiFluentMenuPresentation.PaintSurface(context, bounds);
    public static void PaintItem(UiControlContext context, string label, UiRect bounds,
        UiButtonInteraction interaction, UiMenuOptions options) => PaintItem(context, new UiMenuItemContent(label, null, false), bounds, interaction, options);
    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds,
        UiButtonInteraction interaction, UiMenuOptions options)
    {
        var theme = context.Theme;
        context.Frame(bounds, interaction.Held ? theme.MenuItemBgActive : interaction.Hovered || interaction.FocusVisible
            ? theme.MenuItemBgHovered : default, default, 2, 0);
        var edge = content.Icon.SizeHint;
        var icon = new UiRect(options.RightToLeft ? bounds.X + bounds.Width - edge - 4 : bounds.X + 4,
            bounds.Y + (bounds.Height - edge) * .5f, edge, edge);
        content.Icon.Paint(context, icon, theme.Text, interaction);
        var text = new UiRect(options.RightToLeft ? bounds.X + 4 : bounds.X + edge + 12, bounds.Y,
            MathF.Max(0, bounds.Width - edge - 16), bounds.Height);
        using var clip = context.Clip(text);
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var block = context.MeasureTextBlock(content.Label);
        context.TypographyTextBlock(block, text with
        {
            Y = bounds.Y + (bounds.Height - block.Size.Y) * .5f,
            Height = block.Size.Y
        }, theme.Text,
            options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left);
    }
}
