namespace Duxel.Core;

internal sealed record UiVisualStyledParagraphRow(int SourceStart, int DisplayStart, UiVisualTextLines Layout, int Line);

internal sealed partial class UiVisualTextLines
{
    private readonly List<UiVisualTextLines> _paragraphSlices = [];
    internal Dictionary<int, UiVisualStyledParagraphRow> StyledParagraphRows { get; } = [];

    private void ClearStyledParagraphs()
    {
        foreach (var layout in _paragraphSlices)
            layout.Clear();
        _paragraphSlices.Clear();
        StyledParagraphRows.Clear();
    }

    private bool BuildStyledParagraphRows(UiImmediateContext ui, string text, float width, bool wrap, ref int begin)
    {
        if (_tableSourceRows.Count == 0)
            return false;
        var start = _boundaries[begin];
        var end = text.Length;
        foreach (var next in _tableSourceRows.Keys)
            if (next > start && next < end)
                end = next;
        var source = new UiStyledTextSliceSource(_styledSource!, new(start, end - start));
        var layout = new UiVisualTextLines();
        layout.Prepare(ui, source.Source, width, float.PositiveInfinity, _lineHeight, 0, wrap, styled: source);
        _paragraphSlices.Add(layout);
        var display = _styledSource!.Projection.SourceToDisplay[start];
        var count = layout.Count;
        // A slice ending at the following table's start must not insert a duplicate empty row there.
        if (end < text.Length && count > 0 && layout[count - 1].Start == end - start && layout[count - 1].End == end - start)
            count--;
        for (var line = 0; line < count; line++)
        {
            var row = layout[line];
            StyledParagraphRows.Add(_lines.Count, new(start, display, layout, line));
            _lines.Add(row with
            {
                Start = start + row.Start,
                End = start + row.End,
                Y = Height
            });
            Height += row.Height;
            Width = MathF.Max(Width, row.Width);
        }
        var last = Array.BinarySearch(_boundaries, end);
        if (last < 0)
            throw new InvalidOperationException("A composed paragraph must end at an original text-element boundary.");
        if (layout.NativeParagraph is null)
            for (var boundary = begin; boundary <= last; boundary++)
            {
                var local = _boundaries[boundary] - start;
                _advances[boundary] = layout.X(local, layout.FindLine(local));
            }
        begin = end == text.Length ? _boundaries.Length : last;
        return true;
    }

    internal bool TryComposedObjectBounds(int source, int line, out UiRect bounds)
    {
        if (StyledParagraphRows.TryGetValue(line, out var row) && row.Layout.NativeParagraph is not null)
        {
            bounds = row.Layout.NativeObjectBounds(source - row.SourceStart);
            bounds = bounds with
            {
                Y = _lines[line].Y - row.Layout.Y(row.Line) + bounds.Y
            };
            return true;
        }
        return TryTableObjectBounds(source, line, out bounds);
    }
}
