using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace Duxel.Core;

public sealed partial class UiMarkdownDocument
{
    private sealed partial class Compiler
    {
        internal List<UiStyledTextTable> Tables { get; } = [];

        private void CompileTable(Table table, UiSourceTextRange range)
        {
            var source = SourceText;
            var lines = SourceLineStarts;
            var header = table[0] is TableRow { IsHeader: true };
            var columns = table.ColumnDefinitions.Select(column => column.Alignment switch
            {
                TableColumnAlign.Center => UiStyledTableAlignment.Center,
                TableColumnAlign.Right => UiStyledTableAlignment.Right,
                _ => UiStyledTableAlignment.Left
            }).ToArray();
            var rows = new UiStyledTextTableRow[table.Count];
            var logicalStart = range.Start;
            for (var rowIndex = 0; rowIndex < table.Count; rowIndex++)
            {
                var row = (TableRow)table[rowIndex];
                var physicalLine = table.Line + rowIndex + (header && rowIndex > 0 ? 1 : 0);
                var start = lines[physicalLine];
                var end = SourceLineEnd(source, start);
                var cells = new UiSourceTextRange[columns.Length];
                var cursor = start;
                for (var column = 0; column < cells.Length; column++)
                {
                    var cell = (TableCell)row[column];
                    if (cell.Count > 0 && cell[0] is ParagraphBlock { Inline.FirstChild: { } first, Inline.LastChild: { } last })
                    {
                        var left = Math.Clamp(first.Span.Start, cursor, end);
                        var right = Math.Clamp(last.Span.End + 1, left, end);
                        while (left < right && char.IsWhiteSpace(source[left]))
                            left++;
                        while (right > left && char.IsWhiteSpace(source[right - 1]))
                            right--;
                        cells[column] = new(left, right - left);
                        cursor = right;
                    }
                    else
                    {
                        // Only the empty gap remains here; inline content and escaped pipes belong to AST cells.
                        var separator = source.IndexOf('|', cursor, end - cursor);
                        cursor = separator < 0 ? end : separator + 1;
                        cells[column] = new(cursor, 0);
                    }
                }
                cursor = start;
                foreach (var cell in cells)
                {
                    Hide(cursor, cell.Start);
                    cursor = cell.End;
                }
                Hide(cursor, end);
                rows[rowIndex] = new(new(logicalStart, end - logicalStart), cells, row.IsHeader);
                logicalStart = SourceNextLineStart(source, end);
            }
            if (header)
            {
                var separatorStart = lines[table.Line + 1];
                var separatorEnd = table.Line + 2 < lines.Length ? lines[table.Line + 2] : source.Length;
                Hide(separatorStart, separatorEnd);
                // A header-only table still owns its hidden separator; it is not an extra visual row.
                if (rows.Length == 1)
                    rows[0] = rows[0] with
                    {
                        Source = range
                    };
            }
            Tables.Add(new(range, columns, rows));
        }
    }
}
