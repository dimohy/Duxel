namespace Duxel.Core;

public readonly struct UiFluentDropdownPresentation : IUiDropdownPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiVector2 Measure(UiControlContext context, UiDropdownOptions options)
    {
        using var typography = Typography(context, options);
        var height = options.Size == UiDropdownSize.Small ? 24 : options.Size == UiDropdownSize.Large ? 40 : 32;
        return new(options.Width, MathF.Max(height, context.TypographyLineHeight + 2));
    }
    private static float IconSize(UiDropdownOptions options) => options.Size == UiDropdownSize.Small ? 16 : options.Size == UiDropdownSize.Large ? 24 : 20;
    internal static float StartPadding(UiDropdownOptions options) => options.Size == UiDropdownSize.Small ? 8 : options.Size == UiDropdownSize.Large ? 18 : 12;
    private static float EndPadding(UiDropdownOptions options) => options.Size == UiDropdownSize.Small ? 6 : options.Size == UiDropdownSize.Large ? 12 : 10;
    public static UiRect ExpandBounds(UiRect bounds, UiDropdownOptions options)
    {
        var edge = IconSize(options);
        return new(options.RightToLeft ? bounds.X + EndPadding(options) : bounds.X + bounds.Width - EndPadding(options) - edge,
            bounds.Y + (bounds.Height - edge) * .5f, edge, edge);
    }
    internal static UiControlTypographyScope Typography(UiControlContext context, UiDropdownOptions options) => options.Size switch
    {
        UiDropdownSize.Small => UiFluentSmallInputSize.BeginText(context),
        UiDropdownSize.Large => UiFluentLargeInputSize.BeginText(context),
        _ => UiFluentMediumInputSize.BeginText(context)
    };
    public static void Paint(UiControlContext context, UiRect bounds, string text, bool placeholder,
        UiDropdownOptions options, UiButtonInteraction interaction, bool open, string countText)
    {
        var focused = context.IsEnabled && (interaction.Focused || open);
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : focused ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentInputPalette.Resolve(context, state, options.Appearance, options.Invalid, interaction.Held);
        UiFluentInputFrame.Paint(context, bounds, paint, focused, options.Appearance);
        var end = EndPadding(options) + IconSize(options) + (options.Size == UiDropdownSize.Large ? 6 : 2);
        var rect = new UiRect(bounds.X + (options.RightToLeft ? end : StartPadding(options)), bounds.Y,
            MathF.Max(0, bounds.Width - StartPadding(options) - end), bounds.Height);
        using var typography = Typography(context, options);
        if (countText.Length > 0 && context.MeasureText(text).X > rect.Width)
            text = countText;
        using (context.Clip(rect))
        {
            var block = context.MeasureTextBlock(text);
            context.TypographyTextBlock(block, rect with
            {
                Y = bounds.Y + (bounds.Height - block.Size.Y) * .5f,
                Height = block.Size.Y
            },
                placeholder ? paint.Placeholder : paint.Text, options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left);
        }
        var arrow = UiFluentInputPalette.Adornment(context);
        var arrowBounds = ExpandBounds(bounds, options);
        context.PaintDisclosure(new(arrowBounds.X + arrowBounds.Width / 2, arrowBounds.Y + arrowBounds.Height / 2),
            arrow, open, 0, 180);
    }
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => Measure(context, new());
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction)
        => Paint(context, bounds, label, false, new(), interaction, false, string.Empty);
    public static float MeasureItem(UiControlContext context, string label) => UiFluentMenuPresentation.MeasureItem(context, label);
    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) => UiFluentMenuPresentation.MeasureItem(context, content);
    public static void PaintSurface(UiControlContext context, UiRect bounds) => UiFluentMenuPresentation.PaintSurface(context, bounds);
    public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => PaintItem(context, new UiMenuItemContent(label, null, false), bounds, interaction, options);
    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds,
        UiButtonInteraction interaction, UiMenuOptions options)
    {
        var selected = content.Checked == true;
        var paint = UiFluentOptionPalette.Resolve(context, interaction, selected);
        context.Frame(bounds, paint.Background, default, 4, 0);
        UiMenuContentLayout.Paint(context, content with
        {
            Checked = null
        }, bounds, interaction, options,
            paint.Foreground, paint.Foreground, paint.Secondary, paint.SecondaryHover, 8, 6);
        if (context.IsEnabled && interaction.FocusVisible && !context.Theme.ForcedColors.HasValue)
            context.FocusOutline(bounds, 4);
        var slot = UiMenuContentLayout.Arrange(context, content, bounds, options.RightToLeft, 8, 6).Checkmark;
        if (content.MultipleSelection)
            context.Frame(slot, selected ? paint.CheckBackground : default, paint.CheckBorder, 2, 1);
        var glyph = content.MultipleSelection ? new UiRect(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4) : slot;
        if (selected)
            UiFluentCheckboxCheck16.Paint(context, glyph, content.MultipleSelection ? paint.CheckIndicator : paint.Foreground, interaction);
    }
    public static void PaintFocus(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
    {
        if (context.IsEnabled && interaction.FocusVisible && context.Theme.ForcedColors is { } colors)
            context.Frame(new(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4), default, colors.CanvasText, 4, 2);
    }
}
