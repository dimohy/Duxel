namespace Duxel.Core;

// Hard newlines are independent shaping boundaries in C# source. Preserve their
// immutable native snapshots across source edits and viewport widths.
internal sealed class UiCodeParagraphLayouts
{
    private sealed class Line(int start, string text, int newline, PlatformTextParagraphStyle[] styles, int[] styleMap)
    {
        internal readonly int Start = start, Newline = newline;
        internal readonly string Text = text;
        internal readonly PlatformTextParagraphStyle[] Styles = styles;
        internal readonly int[] StyleMap = styleMap;
        internal PlatformTextParagraphLayout? Natural, Wrapped;
        internal PlatformTextParagraphLayout Layout = null!;
        internal float WrappedWidth;
        internal PlatformTextParagraphLayout? PositionedLayout;
        internal float PositionedTop;
        internal PlatformTextParagraphRun[] PositionedRuns = [];

        internal bool TryReuseLayouts(Line previous)
        {
            if (Text != previous.Text || !Styles.AsSpan().SequenceEqual(previous.Styles))
                return false;
            Natural = previous.Natural;
            Wrapped = previous.Wrapped;
            WrappedWidth = previous.WrappedWidth;
            return true;
        }
    }
    internal int NativeBuilds { get; private set; }
    private string? _text;
    private PlatformTextParagraphStyle[] _styles = [];
    private UiLayoutMeasurementKey _key;
    private bool _vector;
    private List<Line> _lines = [];

    private void PrepareLines(IPlatformTextParagraphBackend backend, string text,
        PlatformTextParagraphStyle[] styles, float width, bool vector, UiLayoutMeasurementKey key,
        UiCodeLineLayoutCache? shared = null)
    {
        if (_text != text || _key != key || _vector != vector || !_styles.AsSpan().SequenceEqual(styles))
        {
            var previous = _key == key && _vector == vector ? _lines : null;
            _text = text;
            _styles = styles;
            _key = key;
            _vector = vector;
            _lines = [];
            var firstStyle = 0;
            for (var start = 0; start <= text.Length;)
            {
                var end = text.IndexOf('\n', start);
                var newline = end < 0 ? 0 : 1;
                if (end < 0) end = text.Length;
                var local = new List<PlatformTextParagraphStyle>();
                var map = new List<int>();
                // Display styles cover ordered source intervals. Revisit only
                // styles that can overlap this line, including spanning styles.
                while (firstStyle < styles.Length && styles[firstStyle].Start + styles[firstStyle].Length <= start)
                    firstStyle++;
                for (var i = firstStyle; i < styles.Length && styles[i].Start < end; i++)
                {
                    var from = Math.Max(start, styles[i].Start);
                    var to = Math.Min(end, styles[i].Start + styles[i].Length);
                    if (to <= from) continue;
                    local.Add(styles[i] with { Start = from - start, Length = to - from });
                    map.Add(i);
                }
                if (local.Count == 0)
                {
                    local.Add(styles[0] with { Start = 0, Length = 0 });
                    map.Add(0);
                }
                _lines.Add(new(start, text[start..end], newline, local.ToArray(), map.ToArray()));
                if (newline == 0) break;
                start = end + 1;
            }
            if (previous is not null)
                ReuseUnchangedLines(previous);
        }
        foreach (var line in _lines)
        {
            if (line.Natural is null)
            {
                line.Natural = CreateLineLayout(line, float.PositiveInfinity);
            }
            var natural = line.Natural;
            var layout = natural;
            if (float.IsFinite(width) && natural.Width > width)
            {
                if (line.Wrapped is null || line.WrappedWidth != width)
                {
                    line.Wrapped = CreateLineLayout(line, width);
                    line.WrappedWidth = width;
                }
                layout = line.Wrapped;
            }
            line.Layout = layout;
        }

        PlatformTextParagraphLayout CreateLineLayout(Line line, float layoutWidth)
        {
            if (shared is null)
            {
                NativeBuilds++;
                return backend.CreateParagraph(new(line.Text, line.Styles, layoutWidth), vector);
            }
            var result = shared.Create(backend, line.Text, line.Styles, layoutWidth, vector, key, out var built);
            if (built)
                NativeBuilds++;
            return result;
        }
    }

    internal float MeasureHeight(IPlatformTextParagraphBackend backend, string text,
        PlatformTextParagraphStyle[] styles, float width, bool vector, UiLayoutMeasurementKey key,
        UiCodeLineLayoutCache? shared, float lineHeight, float[] styleHeights)
    {
        PrepareLines(backend, text, styles, width, vector, key, shared);
        var height = 0f;
        var firstStyle = 0;
        // Preserve the row order and the logical typography heights of the full
        // layout without building document-wide carets or positioned glyph runs.
        foreach (var line in _lines)
            foreach (var row in line.Layout.Lines.Span)
            {
                var rowHeight = lineHeight;
                var start = line.Start + row.Start;
                var end = start + row.Length;
                while (firstStyle < styles.Length && styles[firstStyle].Start + styles[firstStyle].Length <= start)
                    firstStyle++;
                for (var style = firstStyle; style < styles.Length && styles[style].Start < end; style++)
                    rowHeight = MathF.Max(rowHeight, styleHeights[style]);
                height += rowHeight;
            }
        return height;
    }

    internal PlatformTextParagraphLayout Prepare(IPlatformTextParagraphBackend backend, string text,
        PlatformTextParagraphStyle[] styles, float width, bool vector, UiLayoutMeasurementKey key,
        UiCodeLineLayoutCache? shared = null)
    {
        PrepareLines(backend, text, styles, width, vector, key, shared);
        var outputLines = new List<PlatformTextParagraphLine>();
        var outputRuns = new List<PlatformTextParagraphRun>();
        var carets = new PlatformTextParagraphCaret[text.Length + 1];
        var height = 0f;
        var maximum = 0f;
        foreach (var line in _lines)
        {
            var layout = line.Layout;
            maximum = MathF.Max(maximum, layout.Width);
            var rows = layout.Lines.Span;
            var top = height;
            var shifts = new float[rows.Length];
            for (var row = 0; row < rows.Length; row++)
            {
                shifts[row] = height - rows[row].Top;
                outputLines.Add(rows[row] with
                {
                    Start = line.Start + rows[row].Start,
                    Top = height,
                    NewlineLength = rows[row].NewlineLength + (row == rows.Length - 1 ? line.Newline : 0)
                });
                height += rows[row].Height;
            }
            PositionLineCarets(layout.Carets.Span, rows, shifts, carets.AsSpan(line.Start, layout.Carets.Length));
            // Width changes often leave the earlier hard lines and their origins intact.
            // Reuse their positioned runs, including the raster cache identities.
            if (!ReferenceEquals(line.PositionedLayout, layout) || line.PositionedTop != top)
            {
                line.PositionedRuns = PositionLineRuns(line, layout, rows, shifts);
                line.PositionedLayout = layout;
                line.PositionedTop = top;
            }
            outputRuns.AddRange(line.PositionedRuns);
        }
        return new(outputRuns.ToArray(), outputLines.ToArray(), carets, maximum, height);
    }

    // Keep the per-character loops at line scope. Accumulating all backedges
    // in the outer method can trigger synchronous JIT compilation on first scroll.
    private static void PositionLineCarets(ReadOnlySpan<PlatformTextParagraphCaret> local,
        ReadOnlySpan<PlatformTextParagraphLine> rows, ReadOnlySpan<float> shifts, Span<PlatformTextParagraphCaret> document)
    {
        var row = 0;
        for (var i = 0; i < local.Length; i++)
        {
            while (row + 1 < rows.Length && i >= rows[row + 1].Start)
                row++;
            var caret = local[i];
            document[i] = caret with { Y = caret.Y + shifts[row] };
        }
    }

    private static PlatformTextParagraphRun[] PositionLineRuns(Line line, PlatformTextParagraphLayout layout,
        ReadOnlySpan<PlatformTextParagraphLine> rows, ReadOnlySpan<float> shifts)
    {
        var positioned = new List<PlatformTextParagraphRun>(layout.Runs.Length);
        foreach (var run in layout.Runs.Span)
        {
            if (run.Glyphs.IsEmpty) continue;
            var first = run.Glyphs.Span[0].SourcePosition;
            var row = 0;
            while (row + 1 < rows.Length && first >= rows[row + 1].Start)
                row++;
            var shift = shifts[row];
            var glyphs = run.Glyphs.ToArray();
            for (var i = 0; i < glyphs.Length; i++)
                glyphs[i] = glyphs[i] with { SourcePosition = line.Start + glyphs[i].SourcePosition, Y = glyphs[i].Y + shift };
            positioned.Add(new(line.StyleMap[run.StyleIndex], run.FontPath, run.FontSize, run.BidiLevel,
                run.Origin with { Y = run.Origin.Y + shift }, glyphs, run.RasterContext));
        }
        return positioned.ToArray();
    }

    private void ReuseUnchangedLines(List<Line> previous)
    {
        var prefix = 0;
        var limit = Math.Min(previous.Count, _lines.Count);
        while (prefix < limit && _lines[prefix].TryReuseLayouts(previous[prefix]))
            prefix++;

        // The suffix may have moved. Keep its new source offsets and paint style map
        // while reusing only the native snapshots, which use line-local coordinates.
        var oldIndex = previous.Count - 1;
        var newIndex = _lines.Count - 1;
        while (oldIndex >= prefix && newIndex >= prefix && _lines[newIndex].TryReuseLayouts(previous[oldIndex]))
        {
            oldIndex--;
            newIndex--;
        }
    }
}
