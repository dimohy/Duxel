namespace Duxel.Core;

internal static class UiFluentInputFrame
{
    internal static void PaintPlaceholder(UiControlContext context, UiInputLayout layout, UiInputEditResult edit,
        string? placeholder, bool rightToLeft, UiColor color)
    {
        if (edit.Text.Length != 0 || !string.IsNullOrEmpty(edit.CompositionText) || placeholder is null)
            return;
        var text = context.MeasureTextBlock(placeholder);
        var bounds = layout.Content;
        if (rightToLeft)
            bounds = bounds with { X = bounds.X + MathF.Max(0, bounds.Width - text.Size.X) };
        using var clip = context.Clip(layout.EditingBounds);
        context.TypographyTextBlock(text, bounds, color);
    }

    internal static void Paint(UiControlContext context, UiRect bounds, UiFluentInputPaint paint, bool focused, UiInputAppearance appearance)
    {
        switch (appearance)
        {
            case UiInputAppearance.Outline:
                UiFluentOutlineInput.Paint(context, bounds, paint, focused);
                break;
            case UiInputAppearance.Underline:
                UiFluentUnderlineInput.Paint(context, bounds, paint, focused);
                break;
            case UiInputAppearance.FilledDarker:
                UiFluentFilledDarkerInput.Paint(context, bounds, paint, focused);
                break;
            case UiInputAppearance.FilledLighter:
                UiFluentFilledLighterInput.Paint(context, bounds, paint, focused);
                break;
        }
    }
}
