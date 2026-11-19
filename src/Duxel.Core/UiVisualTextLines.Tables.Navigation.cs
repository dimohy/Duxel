namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    private int MoveTableHorizontal(int index, int line, UiVisualStyledTableRow row, bool right)
    {
        var column = 0;
        while (column + 1 < row.Source.Cells.Length && index > row.Source.Cells[column].End)
            column++;
        var cell = row.Source.Cells[column];
        var layout = row.CellLayouts[column];
        var local = Math.Clamp(index - cell.Start, 0, cell.Length);
        var x = X(index, line) - row.CellX[column];
        var target = layout.MoveHorizontal(local, right, x);
        if (target != local || MathF.Abs(layout.X(target, layout.FindLine(target)) - x) > .0001f)
            return RememberTableCaret(row, column, target, line);
        var nextColumn = column + (right ? 1 : -1);
        if (nextColumn >= 0 && nextColumn < row.Source.Cells.Length)
        {
            layout = row.CellLayouts[nextColumn];
            target = layout.Hit(right ? -1 : layout.Width + 1, 0, _lineHeight);
            return RememberTableCaret(row, nextColumn, target, line);
        }
        var nextLine = line + (right ? 1 : -1);
        if (nextLine < 0 || nextLine >= Count)
            return index;
        ClearAffinity();
        target = HitLine(right ? -1 : Width + 1, nextLine);
        _affinityIndex = target == _lines[nextLine].End ? target : -1;
        return target;
    }

    internal bool TryTableObjectBounds(int source, int line, out UiRect bounds)
    {
        if (StyledTableRows.TryGetValue(line, out var row))
            for (var column = 0; column < row.Source.Cells.Length; column++)
            {
                var cell = row.Source.Cells[column];
                if (!cell.Contains(source) || row.CellLayouts[column] is not { NativeParagraph: not null } layout)
                    continue;
                bounds = layout.NativeObjectBounds(source - cell.Start);
                bounds = bounds with
                {
                    X = row.CellX[column] + bounds.X,
                    Y = _lines[line].Y + (row.Height - layout.Height) / 2 + bounds.Y
                };
                return true;
            }
        bounds = default;
        return false;
    }
}
