namespace Duxel.Core;

/// <summary>Fluent field chrome with plain single-choice popup rows; no rich-content or checkmark slots.</summary>
public readonly struct UiFluentSelectPresentation : IUiDropdownPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiVector2 Measure(UiControlContext context, UiDropdownOptions options)
        => UiFluentDropdownPresentation.Measure(context, options);
    public static UiRect ExpandBounds(UiRect bounds, UiDropdownOptions options)
        => UiFluentDropdownPresentation.ExpandBounds(bounds, options);
    public static void Paint(UiControlContext context, UiRect bounds, string text, bool placeholder,
        UiDropdownOptions options, UiButtonInteraction interaction, bool open, string countText)
        => UiFluentDropdownPresentation.Paint(context, bounds, text, false, options, interaction, open, string.Empty);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => Measure(context, new());
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
        => Paint(context, bounds, label, false, new(), interaction, false, string.Empty);
    public static float MeasureItem(UiControlContext context, string label)
        => UiFluentMenuPresentation.MeasureItem(context, label);
    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) => MeasureItem(context, content.Label);
    public static void PaintSurface(UiControlContext context, UiRect bounds) => UiFluentMenuPresentation.PaintSurface(context, bounds);
    public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => PaintItem(context, new UiMenuItemContent(label, null, false), bounds, interaction, options);
    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds,
        UiButtonInteraction interaction, UiMenuOptions options)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled
            : interaction.Held || content.Checked == true ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentMenuPalette.Resolve(context, state);
        context.Frame(bounds, paint.Background, default, 0, 0);
        UiMenuContentLayout.Paint(context, content with { Checked = null, HasCheckmarks = false }, bounds,
            interaction, options, paint.Foreground, paint.Foreground, paint.Foreground, paint.Foreground, 8, 6);
        if (context.IsEnabled && interaction.FocusVisible && !context.Theme.ForcedColors.HasValue)
            context.FocusOutline(bounds, 0);
    }
    public static void PaintFocus(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => UiFluentDropdownPresentation.PaintFocus(context, bounds, interaction, options);
}
