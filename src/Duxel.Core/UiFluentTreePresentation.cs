namespace Duxel.Core;

public readonly struct UiFluentTreePresentation : IUiTreePresentation
{
    public static bool CanRetainMeasurement => true;
    public static double ExpansionDurationSeconds => UiDisclosureMotion.DurationSeconds;
    public static UiTreeLabel CreateLabel(string text, Func<UiTreeOptions> options) => new UiFluentTreeLabel(text, options);
    public static UiTreeMetrics Measure(UiControlContext context, UiTreeOptions options)
    {
        options.Validate();
        return new(options.Size == UiTreeSize.Small ? 24 : 32, options.Size == UiTreeSize.Small ? 12 : 24);
    }

    public static UiMenuPaint Colors(UiControlContext context, UiButtonVisualState state, UiTreeAppearance appearance)
    {
        if ((uint)appearance > 2 || (uint)state > 3) throw new ArgumentOutOfRangeException(nameof(appearance));
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        if (context.Theme.ForcedColors is { } forced)
            return state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed
                ? new(forced.Highlight, forced.HighlightText)
                : new(default, state == UiButtonVisualState.Disabled ? forced.GrayText : forced.CanvasText);
        var background = state is UiButtonVisualState.Rest or UiButtonVisualState.Disabled || appearance == UiTreeAppearance.Transparent
            ? default : appearance == UiTreeAppearance.SubtleAlpha
                ? dark ? new UiColor(36, 36, 36, state == UiButtonVisualState.Hover ? (byte)204 : (byte)128)
                    : new UiColor(255, 255, 255, state == UiButtonVisualState.Hover ? (byte)179 : (byte)128)
                : dark ? state == UiButtonVisualState.Hover ? new UiColor(56, 56, 56) : new(46, 46, 46)
                    : state == UiButtonVisualState.Hover ? new UiColor(245, 245, 245) : new(224, 224, 224);
        var foreground = state == UiButtonVisualState.Disabled ? dark ? new UiColor(92, 92, 92) : new(189, 189, 189)
            : state == UiButtonVisualState.Rest ? dark ? new UiColor(214, 214, 214) : new(66, 66, 66)
            : dark ? new UiColor(255, 255, 255) : new(36, 36, 36);
        return new(background, foreground);
    }

    public static void PaintRow(UiControlContext context, UiRect bounds, UiRect chevron,
        UiButtonInteraction interaction, bool branch, bool expanded, bool selected, UiTreeOptions options)
    {
        PaintAnimatedRow(context, bounds, chevron, interaction, branch, expanded, selected, options, expanded ? 1f : 0f);
    }

    public static bool PaintAnimatedRow(UiControlContext context, UiRect bounds, UiRect chevron,
        UiButtonInteraction interaction, bool branch, bool expanded, bool selected, UiTreeOptions options, float expansion)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = Colors(context, state, options.Appearance);
        context.Frame(bounds, paint.Background, default, 4, 0);
        if (branch && chevron.Width > 0)
        {
            var angle = (options.RightToLeft ? 90 : -90) * (1 - expansion);
            var center = new UiVector2(chevron.X + chevron.Width / 2, chevron.Y + chevron.Height / 2);
            UiDisclosureMotion.Paint(context, center, paint.Foreground, 1.5f, angle);
        }
        return true;
    }

    public static void PaintSelector(UiControlContext context, UiRect bounds, UiButtonInteraction interaction,
        bool? value, bool multiple, UiTreeOptions options)
    {
        if (multiple)
        {
            var layout = UiFluentCheckboxPresentation.Measure(context, default, false);
            layout = UiFluentCheckboxPresentation.Arrange(context, layout, new(bounds.Width, bounds.Height));
            layout = layout with { Indicator = layout.Indicator with { X = (bounds.Width - layout.Indicator.Width) / 2 } };
            UiFluentCheckboxPresentation.Paint(context, string.Empty, default, bounds, layout, interaction with { FocusVisible = false }, value);
        }
        else
        {
            var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
                : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
            var radio = UiFluentCheckboxPalette.Resolve(context, state, value == true ? null : false);
            var indicator = new UiRect(bounds.X + (bounds.Width - 16) / 2, bounds.Y + (bounds.Height - 16) / 2, 16, 16);
            context.Frame(indicator, default, radio.Border, 8, 1);
            if (value == true) context.Circle(new(indicator.X + 8, indicator.Y + 8), 5, radio.Indicator);
        }
    }
    public static void PaintFocus(UiControlContext context, UiRect bounds, UiTreeOptions options) => context.FocusOutline(bounds, 4);
}
