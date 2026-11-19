namespace Duxel.Core;

[UiFluentIcon("dismiss")]
internal readonly partial struct UiTagDismissIcon;

public readonly partial struct UiFluentTagPresentation : IUiTagPresentation
{
    private static readonly UiMenuIcon Dismiss = UiMenuIcon.Create<UiTagDismissIcon, UiTagDismissIcon>();
    public static bool CanRetainMeasurement => true;
    private static float IconSize(UiTagOptions options) => options.Size == UiTagSize.Medium ? 20 : options.Size == UiTagSize.Small ? 16 : 12;
    private static float MediaSpacing(UiTagOptions options) => options.Size == UiTagSize.Medium ? 8 : 6;
    private static float LeadingWidth(UiTagContent content, UiTagOptions options) => !content.Media.IsEmpty
        ? 1 + UiTagMedia.PixelSize(options) + MediaSpacing(options)
        : Padding(options) + (content.Icon.IsEmpty ? 0 : IconSize(options) + 4);
    private static float Padding(UiTagOptions options) => options.Size == UiTagSize.Medium ? 7 : 5;
    private static UiControlTypographyScope BeginText(UiControlContext context, UiTagOptions options, bool secondary = false)
        => options.Size == UiTagSize.Medium && !secondary ? UiFluentMediumInputSize.BeginText(context) : UiFluentSmallInputSize.BeginText(context);
    public static UiVector2 Measure(UiControlContext context, UiTagContent content, UiTagOptions options, bool dismissible)
    {
        using var text = BeginText(context, options, content.SecondaryText is not null);
        var primary = context.MeasureTextBlock(content.Text).Size;
        var secondary = default(UiVector2);
        if (content.SecondaryText is not null)
        {
            using var caption = context.BeginTypography<UiRegularTypeface, UiFluentType100>();
            secondary = context.MeasureTextBlock(content.SecondaryText).Size;
        }
        var height = options.Size == UiTagSize.Medium ? 32 : options.Size == UiTagSize.Small ? 24 : 20;
        return new(MathF.Max(primary.X, secondary.X) + LeadingWidth(content, options) + Padding(options) + (dismissible ? IconSize(options) + 4 : 0),
            MathF.Max(height, primary.Y + secondary.Y));
    }
    public static void Paint(UiControlContext context, UiTagContent content, UiRect bounds,
        UiTagOptions options, UiButtonInteraction interaction, bool dismissible)
    {
        var foreground = PaintFrame(context, bounds, options, interaction);
        PaintContent(context, content, bounds, options, interaction, dismissible, foreground);
        if (context.IsEnabled && interaction.FocusVisible)
            context.FocusOutline(bounds, options.Shape == UiTagShape.Circular ? bounds.Height / 2 : 4);
    }
    internal static UiColor PaintFrame(UiControlContext context, UiRect bounds, UiTagOptions options, UiButtonInteraction interaction)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentTagPalette.Resolve(context.FluentColorScheme, options.Appearance, state, selected: options.Selected);
        var radius = options.Shape == UiTagShape.Circular ? bounds.Height / 2 : 4;
        context.Frame(bounds, paint.Background, paint.Border, radius, 1);
        return paint.Foreground;
    }
    internal static void PaintContent(UiControlContext context, UiTagContent content, UiRect bounds, UiTagOptions options,
        UiButtonInteraction interaction, bool dismissible, UiColor foreground)
    {
        using var typography = BeginText(context, options, content.SecondaryText is not null);
        var edge = IconSize(options);
        var start = bounds.X + Padding(options);
        var end = bounds.X + bounds.Width - Padding(options);
        if (!content.Media.IsEmpty)
        {
            var mediaSize = UiTagMedia.PixelSize(options);
            var x = options.RightToLeft ? bounds.X + bounds.Width - 1 - mediaSize : bounds.X + 1;
            content.Media.Paint(context, new(x, bounds.Y + (bounds.Height - mediaSize) / 2, mediaSize, mediaSize), options);
            if (options.RightToLeft)
                end = x - MediaSpacing(options);
            else
                start = x + mediaSize + MediaSpacing(options);
        }
        else if (!content.Icon.IsEmpty)
        {
            content.Icon.Paint(context, new(options.RightToLeft ? end - edge : start, bounds.Y + (bounds.Height - edge) / 2, edge, edge), foreground, interaction);
            if (options.RightToLeft)
                end -= edge + 4;
            else
                start += edge + 4;
        }
        if (dismissible)
        {
            var dismissColor = !options.Selected && context.IsEnabled && (interaction.Hovered || interaction.Held)
                ? UiFluentTagPalette.Dismiss(context.FluentColorScheme, interaction.Held ? UiButtonVisualState.Pressed : UiButtonVisualState.Hover) : foreground;
            Dismiss.Paint(context, new(options.RightToLeft ? start : end - edge, bounds.Y + (bounds.Height - edge) / 2, edge, edge), dismissColor, interaction);
            if (options.RightToLeft)
                start += edge + 4;
            else
                end -= edge + 4;
        }
        var primary = context.MeasureTextBlock(content.Text);
        var secondary = default(UiControlTextBlock);
        if (content.SecondaryText is not null)
        {
            using var caption = context.BeginTypography<UiRegularTypeface, UiFluentType100>();
            secondary = context.MeasureTextBlock(content.SecondaryText);
        }
        var y = bounds.Y + (bounds.Height - primary.Size.Y - secondary.Size.Y) / 2;
        var align = options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
        context.TypographyTextBlock(primary, new(start, y, MathF.Max(0, end - start), primary.Size.Y), foreground, align);
        if (content.SecondaryText is not null)
        {
            using var caption = context.BeginTypography<UiRegularTypeface, UiFluentType100>();
            context.TypographyTextBlock(secondary, new(start, y + primary.Size.Y, MathF.Max(0, end - start), secondary.Size.Y), foreground, align);
        }
    }
}
