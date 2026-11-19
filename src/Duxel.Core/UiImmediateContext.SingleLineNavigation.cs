namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private int MoveSingleLineHorizontal(UiTextEditBuffer buffer, string text, int caretIndex,
        UiTextSelection selection, bool ctrl, bool shift, bool right)
    {
        var layout = buffer.SingleLine;
        layout.Prepare(this, text);
        if (!ctrl && layout.Geometry is not null)
            return !shift && selection.HasSelection ? layout.Collapse(selection, right) : layout.Move(caretIndex, right);
        layout.ClearAffinity();
        return !ctrl && !shift && selection.HasSelection ? right ? Math.Max(selection.Start, selection.End) : Math.Min(selection.Start, selection.End)
            : ctrl ? buffer.SnapTextElement(right ? FindWordBoundaryRight(text, caretIndex) : FindWordBoundaryLeft(text, caretIndex), forward: right)
            : right ? buffer.NextTextElement(caretIndex) : buffer.PreviousTextElement(caretIndex);
    }
}
