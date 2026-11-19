namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    internal UiStyledTextStyle CaretStyle(int sourceIndex)
    {
        var projection = _styledSource!.Projection;
        var index = Math.Clamp(sourceIndex, 0, projection.SourceToDisplay.Length - 1);
        var display = projection.SourceToDisplay[index];
        var lineStart = projection.SourceToDisplay[_lines[FindLine(index)].Start];
        // At a line end the next run may be an unstyled newline, not the caret's text.
        return projection.StyleAt(display > lineStart ? display - 1 : display);
    }

    internal int LineAtY(float y)
    {
        var low = 0;
        var high = Count - 1;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (y >= _lines[mid].Y + _lines[mid].Height)
                low = mid + 1;
            else
                high = mid;
        }
        return low;
    }
    internal int MovePage(int index, int direction, float pageHeight)
    {
        var from = FindLine(index);
        var target = LineAtY(Y(from) + direction * pageHeight);
        return MoveVertical(index, target - from);
    }

    private void BuildStyled(UiImmediateContext ui, string text, float width, bool wrap)
    {
        if (TryBuildNativeStyledParagraph(ui, text, width, wrap))
            return;
        var projection = _styledSource!.Projection;
        _lines.Clear();
        Width = 0;
        Height = 0;
        EffectiveWidth = width;
        PrepareStyledTables(ui, width);
        var begin = 0;
        while (true)
        {
            if (BuildStyledTableRow(text, ref begin))
            {
                if (begin >= _boundaries.Length)
                    break;
                continue;
            }
            if (BuildStyledParagraphRows(ui, text, width, wrap, ref begin))
            {
                if (begin >= _boundaries.Length)
                    break;
                continue;
            }
            var end = begin;
            var whitespace = -1;
            var measured = 0f;
            var lineHeight = _lineHeight;
            while (end < _boundaries.Length - 1)
            {
                var displayStart = projection.SourceToDisplay[_boundaries[end]];
                var displayEnd = projection.SourceToDisplay[_boundaries[end + 1]];
                var displayed = projection.Text.AsSpan(displayStart, displayEnd - displayStart);
                if (displayed.Contains('\n') || displayed.Contains('\r'))
                    break;
                var candidate = ui.MeasureStyledText(projection, projection.SourceToDisplay[_boundaries[begin]], displayEnd, out var height, width);
                if (wrap && candidate > width && end > begin)
                    break;
                measured = candidate;
                lineHeight = MathF.Max(lineHeight, height);
                _advances[end + 1] = candidate;
                end++;
                if (!displayed.IsEmpty && char.IsWhiteSpace(displayed[^1]))
                    whitespace = end;
                if (wrap && candidate > width)
                    break;
            }
            if (wrap && end < _boundaries.Length - 1 && measured > 0 && whitespace > begin && whitespace < end)
            {
                var from = projection.SourceToDisplay[_boundaries[end]];
                var to = projection.SourceToDisplay[_boundaries[end + 1]];
                if (!projection.Text.AsSpan(from, to - from).Contains('\n'))
                {
                    end = whitespace;
                    measured = _advances[end];
                }
            }
            var atomicEnd = SnapUp(AtomicBoundary(_boundaries[end], forward: true));
            if (atomicEnd != _boundaries[end])
            {
                var next = Array.BinarySearch(_boundaries, atomicEnd);
                for (var boundary = end + 1; boundary <= next; boundary++)
                    _advances[boundary] = measured;
                end = next;
            }
            var start = _boundaries[begin];
            var stop = _boundaries[end];
            _lines.Add(new(start, stop, text[start..stop], measured, Height, lineHeight));
            Height += lineHeight;
            Width = MathF.Max(Width, measured);
            if (end == _boundaries.Length - 1)
                break;
            var nextStart = projection.SourceToDisplay[_boundaries[end]];
            var nextEnd = projection.SourceToDisplay[_boundaries[end + 1]];
            var newline = projection.Text.AsSpan(nextStart, nextEnd - nextStart);
            begin = newline.Contains('\n') || newline.Contains('\r') ? end + 1 : end;
        }
    }
}

public sealed partial class UiImmediateContext
{
    private UiControlTypographyScope BeginStyledTypography<TTypeface>(UiStyledTextStyle style) where TTypeface : IUiTypographyTypeface
        => (style.Flags & UiStyledTextFlags.Caption) != 0
            ? BeginControlTypography<TTypeface, UiFluentType200>(true)
            : style.Heading switch
        {
            1 => BeginControlTypography<TTypeface, UiFluentType700>(true),
            2 => BeginControlTypography<TTypeface, UiFluentType600>(true),
            3 => BeginControlTypography<TTypeface, UiFluentType500>(true),
            4 => BeginControlTypography<TTypeface, UiFluentType400>(true),
            _ => BeginControlTypography<TTypeface, UiFluentType300>(true)
        };

    internal UiControlTypographyScope BeginStyledTypography(UiStyledTextStyle style)
    {
        var scope = style.CodeScript ? BeginStyledTypography<UiRegularTypeface>(style)
            : style.CodeToken != UiCodeToken.None || (style.Flags & UiStyledTextFlags.Code) != 0 ? BeginStyledTypography<UiMonospaceTypeface>(style)
            : style.Heading > 0 || (style.Flags & UiStyledTextFlags.Bold) != 0
                ? BeginStyledTypography<UiBoldTypeface>(style)
                : (style.Flags & UiStyledTextFlags.Semibold) != 0
                    ? BeginStyledTypography<UiSemiboldTypeface>(style) : BeginStyledTypography<UiRegularTypeface>(style);
        try
        {
            if ((style.Flags & UiStyledTextFlags.Italic) != 0)
            {
                if (_platformTextBackend is not IPlatformFontVariantBackend variants)
                    throw new NotSupportedException("Styled emphasis requires a font-variant backend.");
                _directTextPrimaryFontPath = variants.GetFontVariantResource(_directTextPrimaryFontPath!, PlatformFontSimulation.Oblique);
                if (_directTextSecondaryFontPath is { } secondary)
                    _directTextSecondaryFontPath = variants.GetFontVariantResource(secondary, PlatformFontSimulation.Oblique);
            }
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    internal float MeasureStyledText(UiStyledTextProjection projection, int start, int end, out float height,
        float imageWidthLimit = float.PositiveInfinity)
    {
        var width = 0f;
        height = 0;
        foreach (var run in projection.Runs)
        {
            var left = Math.Max(start, run.Start);
            var right = Math.Min(end, run.Start + run.Length);
            if (left >= right)
                continue;
            using var scope = BeginStyledTypography(run.Style);
            if (run.Style.ObjectIndex > 0)
            {
                var size = projection.Objects[run.Style.ObjectIndex - 1].Size;
                width += size.X;
                height = MathF.Max(height, size.Y);
                continue;
            }
            if (IsStyledImage(projection.Text.AsSpan(left, right - left), run.Style))
            {
                var size = StyledImageSize(projection.Images[run.Style.ImageIndex - 1], imageWidthLimit);
                width += size.X;
                height = MathF.Max(height, size.Y);
                continue;
            }
            width += MeasureStyledRun(projection.Text.AsSpan(left, right - left), run.Style);
            height = MathF.Max(height, scope.LineHeight);
        }
        return width;
    }

    private void PaintStyledLine(UiVisualTextLines visual, int line, UiVector2 origin, UiColor foreground, UiRect clip,
        UiStyledTextPaint paint = default)
    {
        if (visual.StyledParagraphRows.TryGetValue(line, out var paragraph))
        {
            PaintStyledLine(paragraph.Layout, paragraph.Line,
                origin with
                {
                    Y = origin.Y + visual.Y(line) - paragraph.Layout.Y(paragraph.Line)
                }, foreground, clip, paint);
            return;
        }
        if (visual.NativeParagraph is not null)
        {
            PaintNativeStyledParagraphLine(visual, line, origin, foreground, clip, paint);
            return;
        }
        var projection = visual.StyledSource!.Projection;
        var row = visual[line];
        if (visual.StyledTableRows.TryGetValue(line, out var tableRow))
        {
            PaintStyledTableRow(visual, row, tableRow, origin, foreground, clip, paint);
            return;
        }
        var start = projection.SourceToDisplay[row.Start];
        var end = projection.SourceToDisplay[row.End];
        var dark = new UiControlContext(this).FluentColorScheme == UiFluentColorScheme.Dark;
        if (paint.PaintContent)
            PaintStyledRule(visual, row, origin, clip, dark);
        PaintStyledRange(projection, start, end, new(origin.X, origin.Y + row.Y), row.Height, foreground, clip, dark, visual.EffectiveWidth, paint);
    }

    private void PaintStyledRange(UiStyledTextProjection projection, int start, int end, UiVector2 origin, float height,
        UiColor foreground, UiRect clip, bool dark, float imageWidthLimit = float.PositiveInfinity,
        UiStyledTextPaint paint = default)
    {
        var x = origin.X;
        foreach (var run in projection.Runs)
        {
            var left = Math.Max(start, run.Start);
            var right = Math.Min(end, run.Start + run.Length);
            if (left >= right)
                continue;
            using var scope = BeginStyledTypography(run.Style);
            if (run.Style.ObjectIndex > 0)
            {
                // The owned widget paints through its normal render lifetime.
                x += projection.Objects[run.Style.ObjectIndex - 1].Size.X;
                continue;
            }
            var text = projection.Text.Substring(left, right - left);
            if (IsStyledImage(text, run.Style))
            {
                var image = projection.Images[run.Style.ImageIndex - 1];
                var bounds = StyledImageBounds(image, imageWidthLimit, x, origin.Y, height);
                if (paint.PaintContent)
                    PaintStyledImage(image, bounds, clip, dark);
                x += bounds.Width;
                continue;
            }
            var width = MeasureStyledRun(text, run.Style);
            var y = origin.Y + (height - scope.LineHeight) / 2;
            var color = paint.ForegroundOverride ?? StyledTextColor(run.Style, foreground, dark);
            if (paint.PaintBackground && (run.Style.Flags & UiStyledTextFlags.Code) != 0)
                _builder.AddRectFilled(new(x, y, width, scope.LineHeight), StyledPalette(foreground, dark).CodeBackground, _whiteTexture, clip);
            if (!paint.PaintContent)
            {
                x += width;
                continue;
            }
            if (IsStyledTask(text, run.Style))
                PaintStyledTask(new(x, y + (scope.LineHeight - width) / 2, width, width), run.Style, clip);
            else
                PaintInputRun(text, new(x, y), color, clip, typography: true);
            if ((run.Style.Flags & (UiStyledTextFlags.Strike | UiStyledTextFlags.Link)) != 0)
            {
                var offset = (run.Style.Flags & UiStyledTextFlags.Strike) != 0 ? .55f : .9f;
                _builder.AddRectFilled(new(x, y + scope.LineHeight * offset, width, 1), color, _whiteTexture, clip);
            }
            x += width;
        }
    }
}
