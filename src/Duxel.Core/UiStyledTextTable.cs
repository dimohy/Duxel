namespace Duxel.Core;

internal enum UiStyledTableAlignment
{
    Left, Center, Right
}
internal sealed record UiStyledTextTable(UiSourceTextRange Source, UiStyledTableAlignment[] Columns, UiStyledTextTableRow[] Rows);
internal sealed record UiStyledTextTableRow(UiSourceTextRange Source, UiSourceTextRange[] Cells, bool Header);
