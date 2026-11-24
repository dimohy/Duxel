namespace Duxel.Core;

/// <summary>A button-shaped checked presentation sharing the compiled toggle value/input kernel.</summary>
public readonly struct UiFluentToggleButtonPresentation : IUiTogglePresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Button;
    public static UiToggleLayout Arrange(UiControlContext context, UiToggleLayout layout, UiVector2 allocatedSize)
        => layout.CenterContent(allocatedSize);
    public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel) =>
        new(UiFluentButtonPresentation.Measure(context, textSize, default), default, default);

    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
        UiToggleLayout layout, UiButtonInteraction interaction, bool? value)
    {
        var theme = context.Theme;
        var isChecked = value == true;
        var selected = context.SampleStateMotion(isChecked);
        UiColor foreground;
        if (theme.ForcedColors is { } colors)
        {
            var (fill, border, label) = ResolveForcedColors(colors, context.IsEnabled, interaction, isChecked);
            context.Frame(bounds, fill, border, 4, 1);
            if (context.IsEnabled && interaction.FocusVisible)
                context.FocusOutline(bounds, 4, isChecked ? colors.Highlight : colors.ButtonText);
            foreground = label;
        }
        else
        {
            var fill = !context.IsEnabled ? theme.CheckboxBg
                : interaction.Held ? theme.ButtonActive
                : UiDisclosureMotion.Blend(interaction.Hovered ? theme.ButtonHovered : theme.Button, theme.ButtonActive, selected);
            var border = !context.IsEnabled ? theme.TextDisabled
                : interaction.Held ? theme.ButtonBorderActive
                : interaction.Hovered ? theme.ButtonBorderHovered : theme.ButtonBorder;
            context.Frame(bounds, fill, border, 4, 1);
            if (interaction.FocusVisible)
                context.FocusOutline(bounds, 4);
            foreground = context.IsEnabled ? theme.ButtonText : theme.TextDisabled;
        }
        context.Text(text, new(bounds.X + (bounds.Width - textSize.X) / 2,
            bounds.Y + (bounds.Height - textSize.Y) / 2), foreground);
    }

    private static (UiColor Fill, UiColor Border, UiColor Label) ResolveForcedColors(UiForcedColorPalette colors,
        bool enabled, UiButtonInteraction interaction, bool isChecked)
    {
        if (!enabled)
            return (colors.ButtonFace, colors.GrayText, colors.GrayText);
        var interactive = interaction.Hovered || (interaction.Held && interaction.FocusVisible);
        if (isChecked)
            return (interactive ? colors.HighlightText : colors.Highlight,
                interaction.Focused || interaction.FocusVisible ? colors.HighlightText : colors.Highlight,
                interactive ? colors.Highlight : colors.HighlightText);
        return interactive
            ? (colors.HighlightText, colors.Highlight, colors.Highlight)
            : (colors.ButtonFace, colors.ButtonText, colors.ButtonText);
    }
}
