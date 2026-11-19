namespace Duxel.Core;

internal readonly struct UiFluentTagPickerInputPresentation : IUiComboboxPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context, UiDropdownOptions options) => UiFluentComboboxPresentation.BeginText(context, options);
    public static UiVector2 Measure(UiControlContext context, UiDropdownOptions options)
    {
        using var text = BeginText(context, options);
        var size = UiFluentComboboxPresentation.Measure(context, options);
        return size with { Y = MathF.Max(text.LineHeight + 2, size.Y - 8) };
    }
    public static UiRect ExpandBounds(UiRect bounds, UiDropdownOptions options) => UiFluentComboboxPresentation.ExpandBounds(bounds, options);
    public static UiInputLayout EditingLayout(UiControlContext context, UiRect bounds, UiDropdownOptions options) => UiFluentComboboxPresentation.EditingLayout(context, bounds, options);
    public static void PaintEditor(UiControlContext context, UiInputLayout layout, UiInputEditResult edit, UiDropdownOptions options, bool open)
    {
        var palette = UiFluentInputPalette.Resolve(context, context.IsEnabled ? UiButtonVisualState.Rest : UiButtonVisualState.Disabled,
            options.Appearance, options.Invalid);
        context.PaintEditableText(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text) { Typography = true });
        UiFluentInputFrame.PaintPlaceholder(context, layout, edit, options.Placeholder, options.RightToLeft, palette.Placeholder);
        var arrow = ExpandBounds(layout.Field, options);
        context.PaintDisclosure(new(arrow.X + arrow.Width / 2, arrow.Y + arrow.Height / 2),
            UiFluentInputPalette.Adornment(context), open, 0, 180);
    }
    public static void Paint(UiControlContext context, UiRect bounds, string text, bool placeholder, UiDropdownOptions options,
        UiButtonInteraction interaction, bool open, string countText) => UiFluentComboboxPresentation.Paint(context, bounds, text, placeholder, options, interaction, open, countText);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiFluentComboboxPresentation.MeasureTrigger(context, label);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction) => UiFluentComboboxPresentation.PaintTrigger(context, label, bounds, interaction);
    public static float MeasureItem(UiControlContext context, string label) => UiFluentComboboxPresentation.MeasureItem(context, label);
    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) => UiFluentComboboxPresentation.MeasureItem(context, content);
    public static void PaintSurface(UiControlContext context, UiRect bounds) => UiFluentComboboxPresentation.PaintSurface(context, bounds);
    public static void PaintFocus(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options) => UiFluentComboboxPresentation.PaintFocus(context, bounds, interaction, options);
    public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options) => UiFluentComboboxPresentation.PaintItem(context, label, bounds, interaction, options);
    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options) => UiFluentComboboxPresentation.PaintItem(context, content, bounds, interaction, options);
}

public sealed partial class UiImmediateContext
{
    internal void PaintTagPickerFrame(UiRect bounds, UiDropdownOptions options, UiComboboxCompletion completion)
    {
        var context = new UiControlContext(this);
        var focused = IsControlEnabled && (completion.EditorId is { } editor && _state.FocusedId == editor || Array.IndexOf(completion.PreviousGroup.Ids, _state.FocusedId) >= 0);
        var state = !IsControlEnabled ? UiButtonVisualState.Disabled : focused ? UiButtonVisualState.Pressed
            : Contains(bounds, _mousePosition) ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentInputPalette.Resolve(context, state, options.Appearance, options.Invalid || _currentField is { Invalid: true });
        UiFluentInputFrame.Paint(context, bounds, palette, focused, options.Appearance);
    }
}
