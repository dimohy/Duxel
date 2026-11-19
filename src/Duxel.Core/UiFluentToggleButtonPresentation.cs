namespace Duxel.Core;

/// <summary>A button-shaped checked presentation sharing the compiled toggle value/input kernel.</summary>
public readonly struct UiFluentToggleButtonPresentation : IUiTogglePresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Button;
    public static UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)=>layout.CenterContent(allocatedSize);
    public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel) =>
        new(UiFluentButtonPresentation.Measure(context, textSize, default), default, default);

    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
        UiToggleLayout layout, UiButtonInteraction interaction, bool? value)
    {
        var theme = context.Theme;
        var selected = context.SampleStateMotion(value == true);
        var fill = !context.IsEnabled ? theme.CheckboxBg
            : interaction.Held ? theme.ButtonActive
            : UiDisclosureMotion.Blend(interaction.Hovered ? theme.ButtonHovered : theme.Button, theme.ButtonActive, selected);
        var border = !context.IsEnabled ? theme.TextDisabled
            : interaction.Held ? theme.ButtonBorderActive
            : interaction.Hovered ? theme.ButtonBorderHovered : theme.ButtonBorder;
        context.Frame(bounds, fill, border, 4, 1);
        if(interaction.FocusVisible)context.FocusOutline(bounds,4);
        context.Text(text, new(bounds.X + (bounds.Width - textSize.X) / 2,
            bounds.Y + (bounds.Height - textSize.Y) / 2), context.IsEnabled ? theme.ButtonText : theme.TextDisabled);
    }
}
