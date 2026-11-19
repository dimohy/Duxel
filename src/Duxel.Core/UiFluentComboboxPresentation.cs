namespace Duxel.Core;

public readonly struct UiFluentComboboxPresentation : IUiComboboxPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context, UiDropdownOptions options)
        => UiFluentDropdownPresentation.Typography(context, options);
    public static UiVector2 Measure(UiControlContext context, UiDropdownOptions options) => UiFluentDropdownPresentation.Measure(context, options);
    public static UiRect ExpandBounds(UiRect bounds, UiDropdownOptions options) => UiFluentDropdownPresentation.ExpandBounds(bounds, options);
    public static UiInputLayout EditingLayout(UiControlContext context, UiRect bounds, UiDropdownOptions options)
    {
        var arrow = ExpandBounds(bounds, options);
        var padding = UiFluentDropdownPresentation.StartPadding(options);
        var left = options.RightToLeft ? arrow.X + arrow.Width + 2 : bounds.X + padding;
        var right = options.RightToLeft ? bounds.X + bounds.Width - padding : arrow.X - 2;
        var editing = new UiRect(left, bounds.Y, MathF.Max(0, right - left), bounds.Height);
        return new(new(bounds.Width, bounds.Height), bounds,
            new(left, bounds.Y + (bounds.Height - context.TypographyLineHeight) / 2, editing.Width, context.TypographyLineHeight),
            default, context.TypographyLineHeight)
        {
            Editing = editing,
            Options = new(Placeholder: options.Placeholder, Invalid: options.Invalid)
        };
    }
    public static void PaintEditor(UiControlContext context, UiInputLayout layout, UiInputEditResult edit, UiDropdownOptions options, bool open)
    {
        Paint(context, layout.Field, string.Empty, false, options,
            new(false, edit.Hovered, edit.Held)
            {
                Focused = edit.Active
            }, open, string.Empty);
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : edit.Active || open ? UiButtonVisualState.Pressed
            : edit.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var palette = UiFluentInputPalette.Resolve(context, state, options.Appearance, options.Invalid, edit.Held);
        using var clip = context.Clip(layout.EditingBounds);
        context.PaintEditableText(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text)
        {
            Typography = true
        });
        UiFluentInputFrame.PaintPlaceholder(context, layout, edit, options.Placeholder, options.RightToLeft, palette.Placeholder);
    }
    public static void Paint(UiControlContext context, UiRect bounds, string text, bool placeholder, UiDropdownOptions options,
        UiButtonInteraction interaction, bool open, string countText)
        => UiFluentDropdownPresentation.Paint(context, bounds, text, placeholder, options, interaction, open, countText);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiFluentDropdownPresentation.MeasureTrigger(context, label);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
        => UiFluentDropdownPresentation.PaintTrigger(context, label, bounds, interaction);
    public static float MeasureItem(UiControlContext context, string label) => UiFluentDropdownPresentation.MeasureItem(context, label);
    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) => UiFluentDropdownPresentation.MeasureItem(context, content);
    public static void PaintSurface(UiControlContext context, UiRect bounds) => UiFluentDropdownPresentation.PaintSurface(context, bounds);
    public static void PaintFocus(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => UiFluentDropdownPresentation.PaintFocus(context, bounds, interaction, options);
    public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => UiFluentDropdownPresentation.PaintItem(context, label, bounds, interaction, options);
    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => UiFluentDropdownPresentation.PaintItem(context, content, bounds, interaction, options);
}
