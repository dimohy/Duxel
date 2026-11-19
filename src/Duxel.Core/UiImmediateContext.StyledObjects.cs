namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private static bool IsStyledTask(ReadOnlySpan<char> text, UiStyledTextStyle style) =>
        (style.Flags & UiStyledTextFlags.Task) != 0 && text is ['\uFFFC'];

    private float MeasureStyledRun(ReadOnlySpan<char> text, UiStyledTextStyle style) =>
        IsStyledTask(text, style) ? 16 * _textSettings.Scale : MeasureTextWidthSpan(text);

    private void PaintStyledTask(UiRect bounds, UiStyledTextStyle style, UiRect clip)
    {
        var context = new UiControlContext(this);
        using var clipped = context.Clip(clip);
        var isChecked = (style.Flags & UiStyledTextFlags.Checked) != 0;
        var palette = UiFluentCheckboxPalette.Resolve(context, UiButtonVisualState.Rest, isChecked);
        UiFluentSquareCheckboxShape.Paint(context, bounds, palette.Background, palette.Border);
        if (isChecked)
        {
            var glyph = bounds.Width * .75f;
            var inset = (bounds.Width - glyph) / 2;
            UiFluentCheckboxCheck12.Paint(context, new(bounds.X + inset, bounds.Y + inset, glyph, glyph), palette.Indicator, default);
        }
    }

    private void PaintStyledRule(UiVisualTextLines visual, UiVisualTextLine row, UiVector2 origin, UiRect clip, bool dark)
    {
        var source = visual.StyledSource!;
        foreach (var run in source.Runs)
        {
            if ((run.Style.Flags & UiStyledTextFlags.Rule) == 0 || run.Source.End <= row.Start || run.Source.Start >= row.End
                || source.ActiveBlock is { } active && active.Contains(run.Source.Start))
                continue;
            var projection = source.Projection;
            var prefixWidth = MeasureStyledText(projection, projection.SourceToDisplay[row.Start],
                projection.SourceToDisplay[Math.Max(row.Start, run.Source.Start)], out _, visual.EffectiveWidth);
            var left = origin.X + prefixWidth;
            var right = MathF.Min(origin.X + visual.EffectiveWidth, clip.X + clip.Width);
            var palette = StyledPalette(_theme.Text, dark);
            _builder.AddRectFilled(new(left, origin.Y + row.Y + row.Height / 2, MathF.Max(0, right - left), 1),
                palette.Border, _whiteTexture, clip);
            return;
        }
    }
}
