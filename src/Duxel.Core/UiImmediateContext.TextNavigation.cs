namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private int MoveMultilineHorizontal(UiTextEditBuffer buffer, string text, int caretIndex,
        UiTextSelection selection, UiVisualTextLines? visual, bool ctrl, bool shift, bool right)
    {
        if (ctrl)
        {
            visual?.ClearAffinity();
            var word = right ? FindWordBoundaryRight(text, caretIndex) : FindWordBoundaryLeft(text, caretIndex);
            return buffer.SnapTextElement(word, forward: right);
        }
        if (!shift && selection.HasSelection)
            return visual?.CollapseHorizontal(selection.Start, selection.End, right)
                ?? (right ? Math.Max(selection.Start, selection.End) : Math.Min(selection.Start, selection.End));
        return visual?.MoveHorizontal(caretIndex, right)
            ?? (right ? buffer.NextTextElement(caretIndex) : buffer.PreviousTextElement(caretIndex));
    }
}
