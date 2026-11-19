namespace Duxel.Core;

public readonly partial struct UiFluentTagPresentation
{
    public static bool SupportsInteraction => true;
    public static float SecondaryWidth(UiTagOptions options) => options.Size == UiTagSize.Medium ? 32 : 24;
    public static UiVector2 MeasureInteraction(UiControlContext context, UiTagContent content, UiTagOptions options, bool dismissible)
    {
        var size = Measure(context, content, options, false);
        return new(size.X + (dismissible ? SecondaryWidth(options) : 0), size.Y);
    }
    public static void PaintInteraction(UiControlContext context, UiTagContent content, UiRect bounds, UiRect primary, UiRect secondary,
        UiTagOptions options, UiButtonInteraction primaryInteraction, UiButtonInteraction secondaryInteraction, bool dismissible)
    {
        PaintFrame(context, bounds, options, default);
        var radius = options.Shape == UiTagShape.Circular ? bounds.Height / 2 : 4;
        var visual = !context.IsEnabled ? UiButtonVisualState.Disabled : primaryInteraction.Held ? UiButtonVisualState.Pressed
            : primaryInteraction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentTagPalette.Resolve(context.FluentColorScheme, options.Appearance, visual, interactive: true, selected: options.Selected);
        using (context.RoundedClip(bounds, radius))
        {
            if (context.IsEnabled && (primaryInteraction.Hovered || primaryInteraction.Held))
                context.Frame(primary, palette.Background, default, 0, 0);
            PaintContent(context, content, primary, options, primaryInteraction, false, palette.Foreground);
            if (dismissible)
            {
                var state = !context.IsEnabled ? UiButtonVisualState.Disabled : secondaryInteraction.Held ? UiButtonVisualState.Pressed
                    : secondaryInteraction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
                var secondaryPalette = UiFluentTagPalette.Resolve(context.FluentColorScheme, options.Appearance, state, interactive: true, secondary: true, selected: options.Selected);
                if (context.IsEnabled && (secondaryInteraction.Hovered || secondaryInteraction.Held))
                    context.Frame(secondary, secondaryPalette.Background, default, 0, 0);
                var edge = IconSize(options);
                Dismiss.Paint(context, new(secondary.X + (secondary.Width - edge) / 2, secondary.Y + (secondary.Height - edge) / 2, edge, edge),
                    secondaryPalette.Foreground, secondaryInteraction);
                var x = options.RightToLeft ? secondary.X + secondary.Width : secondary.X;
                context.Line(new(x, bounds.Y), new(x, bounds.Y + bounds.Height),
                    UiFluentTagPalette.Divider(context.FluentColorScheme, options.Appearance, context.IsEnabled, options.Selected), 1);
                if (context.IsEnabled && secondaryInteraction.FocusVisible)
                    context.FocusOutline(secondary, radius);
            }
            if (context.IsEnabled && primaryInteraction.FocusVisible)
                context.FocusOutline(primary, radius);
        }
        if (options.Selected || options.Appearance == UiTagAppearance.Outline)
            context.Frame(bounds, default, UiFluentTagPalette.Resolve(context.FluentColorScheme, options.Appearance,
                context.IsEnabled ? UiButtonVisualState.Rest : UiButtonVisualState.Disabled, selected: options.Selected).Border, radius, 1);
    }
}
