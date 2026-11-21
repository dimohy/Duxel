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
        internal float WrappedWidth;

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

    internal PlatformTextParagraphLayout Prepare(IPlatformTextParagraphBackend backend, string text,
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
        var outputLines = new List<PlatformTextParagraphLine>();
        var outputRuns = new List<PlatformTextParagraphRun>();
        var carets = new PlatformTextParagraphCaret[text.Length + 1];
        var height = 0f;
        var maximum = 0f;
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
            maximum = MathF.Max(maximum, layout.Width);
            var rows = layout.Lines.Span;
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
            var rowIndex = 0;
            for (var i = 0; i < layout.Carets.Length; i++)
            {
                while (rowIndex + 1 < rows.Length && i >= rows[rowIndex + 1].Start)
                    rowIndex++;
                var caret = layout.Carets.Span[i];
                carets[line.Start + i] = caret with { Y = caret.Y + shifts[rowIndex] };
            }
            foreach (var run in layout.Runs.Span)
            {
                if (run.Glyphs.IsEmpty) continue;
                var first = run.Glyphs.Span[0].SourcePosition;
                var row = 0;
                while (row + 1 < rows.Length && first >= rows[row + 1].Start) row++;
                var shift = shifts[row];
                var glyphs = run.Glyphs.ToArray();
                for (var i = 0; i < glyphs.Length; i++)
                    glyphs[i] = glyphs[i] with { SourcePosition = line.Start + glyphs[i].SourcePosition, Y = glyphs[i].Y + shift };
                outputRuns.Add(new(line.StyleMap[run.StyleIndex], run.FontPath, run.FontSize, run.BidiLevel,
                    run.Origin with { Y = run.Origin.Y + shift }, glyphs, run.RasterContext));
            }
        }
        return new(outputRuns.ToArray(), outputLines.ToArray(), carets, maximum, height);

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
