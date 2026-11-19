namespace Duxel.Core;

/// <summary>Maps committed UTF-16 boundaries around an inserted, uncommitted preedit span.</summary>
internal readonly record struct UiCompositionTextMap(int Start, int Length)
{
    internal int Display(int source, bool end = false)
        => source > Start || source == Start && !end ? source + Length : source;

    internal int Source(int display)
        => display <= Start ? display : display <= Start + Length ? Start : display - Length;

    internal UiTextSelection Display(UiTextSelection selection)
    {
        if (!selection.HasSelection)
        {
            var caret = Display(selection.End);
            return new(caret, caret);
        }
        var first = Display(Math.Min(selection.Start, selection.End));
        var last = Display(Math.Max(selection.Start, selection.End), end: true);
        return selection.Start < selection.End ? new(first, last) : new(last, first);
    }
}
