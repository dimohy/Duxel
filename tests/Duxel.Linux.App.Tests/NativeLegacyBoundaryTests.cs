using Duxel.Core;
using Duxel.Platform.Linux;

internal static class NativeLegacyBoundaryTests
{
    internal static void Run(Action<bool, string> check)
    {
        var table = LinuxTextBoundaries.Retain(null, new("one two. Last!"), "en");
        (uint Type, int Offset, int Relative, int Start, int End)[] cases =
        [
            (1,5,-1,0,4), (1,5,0,4,9), (1,5,1,9,14),
            (2,5,-1,0,3), (2,5,0,3,7), (2,5,1,7,13),
            (3,5,-1,0,0), (3,5,0,0,9), (3,5,1,9,14),
            (4,5,-1,0,0), (4,5,0,0,8), (4,5,1,8,14),
            (1,14,0,9,14), (2,14,0,13,14), (4,14,0,14,14),
            (4,14,-1,8,14), (1,0,-1,0,0), (1,14,1,14,14)
        ];
        foreach (var c in cases)
            check(table.GetLegacyRange(c.Offset, c.Type, c.Relative) == (c.Start, c.End),
                $"Legacy native boundary {c.Type} at {c.Offset} relative {c.Relative} matches independent source offsets");
        foreach (var c in new[] { ("one two", 2u, 7, 7, 3), ("one two ", 2u, 8, 7, 3), ("One. ", 4u, 5, 4, 0) })
        {
            var trailing = LinuxTextBoundaries.Retain(null, new(c.Item1), "en");
            check(trailing.GetLegacyRange(c.Item3, c.Item2, 0) == (c.Item4, c.Item3)
                && trailing.GetLegacyRange(c.Item3, c.Item2, -1) == (c.Item5, c.Item4),
                "Actual end flags distinguish a terminal lexical boundary from a trailing separator interval");
        }
        var content = new UiAccessibilityTextContent("정😀e\u0301x");
        check(LinuxTextBoundarySequence.Character(content.CharacterCount, 2, -1) == (1, 2)
            && content.GetText(1, 2) == "😀" && LinuxTextBoundarySequence.Character(5, 2, 1) == (3, 4),
            "Legacy character intervals use individual Unicode scalars including combining marks");
        var empty = LinuxTextBoundaries.Retain(null, new(""), "und");
        check(Enumerable.Range(1, 4).All(t => Enumerable.Range(-1, 3).All(r => empty.GetLegacyRange(0, (uint)t, r) == (0, 0))),
            "Empty source has empty before, at and after word and sentence intervals");
        foreach (var invalid in new Action[] { () => table.GetLegacyRange(-1, 1, 0), () => table.GetLegacyRange(15, 1, 0),
            () => table.GetLegacyRange(0, 0, 0), () => table.GetLegacyRange(0, 1, 2) })
        {
            var rejected = false;
            try
            {
                invalid();
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Legacy queries reject invalid offsets, boundary kinds and directions");
        }
    }
}
