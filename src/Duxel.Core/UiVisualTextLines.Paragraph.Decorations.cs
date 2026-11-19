namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private void PaintNativeParagraphDecorations(UiVisualTextLines visual, int line, UiVector2 origin,
        UiColor foreground, UiRect clip, bool dark, bool background, UiStyledTextPaint paint = default)
    {
        var row = visual.NativeParagraph!.Lines.Span[line];
        var scale = visual.NativeParagraphScale;
        foreach (var run in visual.StyledSource!.Projection.Runs)
        {
            var left = Math.Max(row.Start, run.Start);
            var right = Math.Min(row.Start + row.Length, run.Start + run.Length);
            if (left >= right || (run.Style.Flags & (UiStyledTextFlags.Code | UiStyledTextFlags.Strike | UiStyledTextFlags.Link)) == 0)
                continue;
            using var typography = BeginStyledTypography(run.Style);
            var y = origin.Y + visual[line].Y + (visual[line].Height - typography.LineHeight) / 2;
            var color = paint.ForegroundOverride ?? StyledTextColor(run.Style, foreground, dark);
            // Native selection geometry preserves disjoint visual intervals in bidirectional text.
            foreach (var fragment in row.Geometry.SelectionFragments(left - row.Start, right - row.Start))
            {
                var x = origin.X + fragment.Left * scale;
                var width = fragment.Width * scale;
                if (background && paint.PaintBackground && (run.Style.Flags & UiStyledTextFlags.Code) != 0)
                    _builder.AddRectFilled(new(x, y, width, typography.LineHeight),
                        StyledPalette(foreground, dark).CodeBackground, _whiteTexture, clip);
                if (!background && (run.Style.Flags & (UiStyledTextFlags.Strike | UiStyledTextFlags.Link)) != 0)
                {
                    var offset = (run.Style.Flags & UiStyledTextFlags.Strike) != 0 ? .55f : .9f;
                    _builder.AddRectFilled(new(x, y + typography.LineHeight * offset, width, 1), color, _whiteTexture, clip);
                }
            }
        }
    }
}
