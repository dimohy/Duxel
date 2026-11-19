namespace Duxel.Core;

internal sealed record UiVisualStyledTableRow(UiStyledTextTableRow Source, float[] ColumnEdges, float[] CellX,
    UiVisualTextLines[] CellLayouts, float Height);

internal sealed partial class UiVisualTextLines
{
    private readonly Dictionary<int, UiVisualStyledTableRow> _tableSourceRows = [];
    internal Dictionary<int, UiVisualStyledTableRow> StyledTableRows { get; } = [];
    private void ClearStyledTables()
    {
        ClearStyledParagraphs();
        foreach (var row in _tableSourceRows.Values)
            foreach (var cell in row.CellLayouts)
                cell.Clear();
        _tableSourceRows.Clear();
        StyledTableRows.Clear();
        HasUnwrappedOverflow = false;
    }
    private void PrepareStyledTables(UiImmediateContext ui, float width)
    {
        ClearStyledTables();
        foreach (var table in _styledSource!.Tables)
        {
            if (_styledSource.ActiveBlock is { } active && active.Start < table.Source.End && active.End > table.Source.Start)
                continue;
            var padding = _lineHeight / 2;
            var widths = new float[table.Columns.Length];
            var measured = new float[table.Rows.Length][];
            var layouts = new UiVisualTextLines[table.Rows.Length][];
            var heights = new float[table.Rows.Length];
            for (var row = 0; row < table.Rows.Length; row++)
            {
                measured[row] = new float[widths.Length];
                layouts[row] = new UiVisualTextLines[widths.Length];
                heights[row] = _lineHeight;
                for (var column = 0; column < widths.Length; column++)
                {
                    var cell = table.Rows[row].Cells[column];
                    var source = new UiStyledTextSliceSource(_styledSource, cell);
                    var layout = new UiVisualTextLines();
                    layout.Prepare(ui, source.Source, float.PositiveInfinity, float.PositiveInfinity, _lineHeight, 0,
                        wrap: false, styled: source);
                    layouts[row][column] = layout;
                    measured[row][column] = layout.Width;
                    heights[row] = MathF.Max(heights[row], layout.Height);
                    widths[column] = MathF.Max(widths[column], measured[row][column] + padding * 2);
                }
            }
            var intrinsic = widths.Sum();
            var extra = widths.Length == 0 ? 0 : MathF.Max(0, width - intrinsic) / widths.Length;
            var edges = new float[widths.Length + 1];
            for (var column = 0; column < widths.Length; column++)
                edges[column + 1] = edges[column] + widths[column] + extra;
            HasUnwrappedOverflow |= edges[^1] - width > .5f;
            for (var row = 0; row < table.Rows.Length; row++)
            {
                var cellX = new float[widths.Length];
                for (var column = 0; column < widths.Length; column++)
                {
                    var free = edges[column + 1] - edges[column] - padding * 2 - measured[row][column];
                    var alignment = table.Columns[column] switch
                    {
                        UiStyledTableAlignment.Center => .5f,
                        UiStyledTableAlignment.Right => 1f,
                        _ => 0f
                    };
                    cellX[column] = edges[column] + padding + MathF.Max(0, free) * alignment;
                }
                _tableSourceRows.Add(table.Rows[row].Source.Start,
                    new(table.Rows[row], edges, cellX, layouts[row], heights[row] + _lineHeight * .6f));
            }
        }
    }

    private bool BuildStyledTableRow(string text, ref int begin)
    {
        if (!_tableSourceRows.TryGetValue(_boundaries[begin], out var table))
            return false;
        var end = Array.BinarySearch(_boundaries, table.Source.Source.End);
        if (end < 0)
            throw new InvalidOperationException("A table row must end at a source text-element boundary.");
        for (var i = begin; i <= end; i++)
        {
            var index = _boundaries[i];
            var x = 0f;
            for (var column = 0; column < table.Source.Cells.Length; column++)
            {
                var cell = table.Source.Cells[column];
                if (index < cell.Start)
                    break;
                var layout = table.CellLayouts[column];
                var local = Math.Min(index, cell.End) - cell.Start;
                x = table.CellX[column] + layout.X(local, layout.FindLine(local));
                if (index <= cell.End)
                    break;
            }
            _advances[i] = x;
        }
        var width = table.ColumnEdges[^1];
        StyledTableRows.Add(_lines.Count, table);
        _lines.Add(new(table.Source.Source.Start, table.Source.Source.End,
            text[table.Source.Source.Start..table.Source.Source.End], width, Height, table.Height));
        Width = MathF.Max(Width, width);
        Height += table.Height;
        begin = end + 1;
        if (end == _boundaries.Length - 1)
            begin = _boundaries.Length;
        return true;
    }

    private int HitStyledTableCell(float x, int line, UiVisualStyledTableRow row)
    {
        var column = 0;
        while (column + 1 < row.Source.Cells.Length && x >= row.ColumnEdges[column + 1])
            column++;
        var cell = row.Source.Cells[column];
        if (cell.Length == 0)
            return Snap(cell.Start);
        var layout = row.CellLayouts[column];
        var local = layout.Hit(x - row.CellX[column], 0, _lineHeight);
        return RememberTableCaret(row, column, local, line);
    }

    private int RememberTableCaret(UiVisualStyledTableRow row, int column, int local, int line)
    {
        var layout = row.CellLayouts[column];
        var result = row.Source.Cells[column].Start + local;
        _hitAffinityIndex = result;
        _hitAffinityLine = line;
        _hitAffinityX = row.CellX[column] + layout.X(local, layout.FindLine(local));
        return result;
    }
}

public sealed partial class UiImmediateContext
{
    private void PaintStyledTableRow(UiVisualTextLines visual, UiVisualTextLine line, UiVisualStyledTableRow row,
        UiVector2 origin, UiColor foreground, UiRect clip, UiStyledTextPaint paint = default)
    {
        var scheme = new UiControlContext(this).FluentColorScheme;
        var dark = scheme == UiFluentColorScheme.Dark;
        var palette = StyledPalette(foreground, dark);
        var border = palette.Border;
        var y = origin.Y + line.Y;
        var width = row.ColumnEdges[^1];
        if (row.Source.Header && paint.PaintBackground)
            _builder.AddRectFilled(new(origin.X, y, width, line.Height), palette.HeaderBackground, _whiteTexture, clip);
        if (paint.PaintContent)
        {
            _builder.AddRectFilled(new(origin.X, y, width, 1), border, _whiteTexture, clip);
            _builder.AddRectFilled(new(origin.X, y + line.Height - 1, width, 1), border, _whiteTexture, clip);
            foreach (var edge in row.ColumnEdges)
                _builder.AddRectFilled(new(origin.X + MathF.Min(edge, width - 1), y, 1, line.Height), border, _whiteTexture, clip);
        }
        for (var column = 0; column < row.Source.Cells.Length; column++)
        {
            var layout = row.CellLayouts[column];
            var cellClip = IntersectRect(clip, new(origin.X + row.ColumnEdges[column], y,
                row.ColumnEdges[column + 1] - row.ColumnEdges[column], line.Height));
            var position = new UiVector2(origin.X + row.CellX[column], y + (line.Height - layout.Height) / 2);
            for (var cellLine = 0; cellLine < layout.Count; cellLine++)
                PaintStyledLine(layout, cellLine, position, foreground, cellClip, paint);
        }
    }
}
