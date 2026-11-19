namespace Duxel.Core;

[UiFluentIcon("chevron_up", UiFluentIconStyle.Regular, 16)]
internal readonly partial struct UiSpinChevronUp;
[UiFluentIcon("chevron_down", UiFluentIconStyle.Regular, 16)]
internal readonly partial struct UiSpinChevronDown;

public readonly struct UiFluentSpinButtonPresentation : IUiSpinButtonPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context, UiSpinButtonOptions options)
        => options.Size == UiSpinButtonSize.Small
            ? context.BeginTypography<UiRegularTypeface, UiFluentType200>()
            : context.BeginTypography<UiRegularTypeface, UiFluentType300>();

    public static UiVector2 Measure(UiControlContext context, UiSpinButtonOptions options)
    {
        using var typography = BeginText(context, options);
        return new(options.Width, MathF.Max(options.Size == UiSpinButtonSize.Small ? 24 : 32, typography.LineHeight + 2));
    }

    public static UiSpinButtonLayout Arrange(UiControlContext context, UiRect bounds, UiSpinButtonOptions options)
    {
        var stepWidth = MathF.Min(24, bounds.Width);
        var stepX = options.RightToLeft ? bounds.X : bounds.X + bounds.Width - stepWidth;
        var half = bounds.Height / 2;
        var increment = new UiRect(stepX, bounds.Y, stepWidth, half);
        var decrement = new UiRect(stepX, bounds.Y + half, stepWidth, half);
        var padding = options.Size == UiSpinButtonSize.Small ? 8 : 10;
        var left = options.RightToLeft ? stepX + stepWidth + 4 : bounds.X;
        var right = options.RightToLeft ? bounds.X + bounds.Width : stepX - 4;
        left = Math.Clamp(left, bounds.X, bounds.X + bounds.Width);
        right = Math.Clamp(right, left, bounds.X + bounds.Width);
        var contentX = options.RightToLeft ? left : MathF.Min(right, left + padding);
        var contentRight = options.RightToLeft ? MathF.Max(contentX, right - padding) : right;
        var lineHeight = context.TypographyLineHeight;
        var input = new UiInputLayout(new(bounds.Width, bounds.Height), bounds,
            new(contentX, bounds.Y + (bounds.Height - lineHeight) / 2, contentRight - contentX, lineHeight), default, lineHeight)
        {
            Editing = new(left, bounds.Y, right - left, bounds.Height),
            Options = new(options.ReadOnly ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None, Invalid: options.Invalid)
        };
        return new(input, increment, decrement);
    }

    public static void Paint(UiControlContext context, UiSpinButtonLayout layout, UiInputEditResult edit,
        UiSpinButtonOptions options, UiButtonInteraction increment, UiButtonInteraction decrement, bool canIncrement, bool canDecrement)
    {
        var focused = (edit.Active || edit.FocusWithin) && context.IsEnabled;
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : focused ? UiButtonVisualState.Pressed
            : edit.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentInputPalette.Resolve(context, state, options.Appearance, options.Invalid);
        var radius = options.Appearance == UiInputAppearance.Underline ? 0 : 4;
        context.Frame(layout.Input.Field, paint.Background, default, radius, 0);
        using (context.RoundedClip(layout.Input.Field, radius))
        {
            PaintStepper(context, layout.Increment, increment, canIncrement, options, true);
            PaintStepper(context, layout.Decrement, decrement, canDecrement, options, false);
        }
        UiFluentInputFrame.Paint(context, layout.Input.Field, paint with
        {
            Background = default
        }, focused, options.Appearance);
        context.PaintEditableText(layout.Input, edit, new(paint.Text, context.Theme.InputSelectionBg, paint.Text)
        {
            Typography = true
        });
    }

    private static void PaintStepper(UiControlContext context, UiRect bounds, UiButtonInteraction interaction,
        bool enabled, UiSpinButtonOptions options, bool increment)
    {
        enabled &= context.IsEnabled;
        var state = !enabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentSpinButtonPalette.Resolve(context, state, options.Appearance);
        if (context.IsHovered(bounds))
            context.SetMouseCursor(enabled ? UiMouseCursor.Hand : UiMouseCursor.NotAllowed);
        if (enabled && (interaction.Hovered || interaction.Held))
            context.Frame(bounds, paint.Background, default, 0, 0);
        var x = bounds.X + (bounds.Width - 16) / 2;
        if (options.Size == UiSpinButtonSize.Small)
            x += options.RightToLeft ? 1 : -1;
        var icon = new UiRect(x, bounds.Y + (bounds.Height - 16) / 2 + (increment ? 1.5f : -1.5f), 16, 16);
        if (increment)
            UiSpinChevronUp.Paint(context, icon, paint.Foreground, interaction);
        else
            UiSpinChevronDown.Paint(context, icon, paint.Foreground, interaction);
    }
}
