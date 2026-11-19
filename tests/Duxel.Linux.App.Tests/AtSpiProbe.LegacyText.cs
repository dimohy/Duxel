using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private static (int Start, int End, string Text) LegacyText(nint text, int offset, uint boundary, int relative)
    {
        nint error = 0;
        var value = relative switch
        {
            -1 => atspi_text_get_text_before_offset(text, offset, boundary, &error),
            0 => atspi_text_get_text_at_offset(text, offset, boundary, &error),
            1 => atspi_text_get_text_after_offset(text, offset, boundary, &error),
            _ => throw new ArgumentOutOfRangeException(nameof(relative))
        };
        try
        {
            Error(ref error);
            if (value == 0)
                throw new InvalidDataException("Missing legacy native text range.");
            var range = *(NativeTextRange*)value;
            return (range.Start, range.End, Marshal.PtrToStringUTF8(range.Content)
                ?? throw new InvalidDataException("Missing legacy native text content."));
        }
        finally { if (value != 0) g_boxed_free(atspi_text_range_get_type(), value); }
    }
    private static void VerifyLegacyText(nint edit, nint text, Action<bool, string> check)
    {
        const string source = "one two. Last!";
        check(SetText(edit, source), "The actual editor accepts the independent legacy boundary source");
        (uint Type, int Start, int End, int BeforeStart, int BeforeEnd, int AfterStart, int AfterEnd)[] cases =
        [
            (0,5,6,4,5,6,7), (1,4,9,0,4,9,14), (2,3,7,0,3,7,13),
            (3,0,9,0,0,9,14), (4,0,8,0,0,8,14), (5,0,14,0,0,14,14), (6,0,14,0,0,14,14)
        ];
        foreach (var c in cases)
        {
            check(LegacyText(text, 5, c.Type, 0) == (c.Start, c.End, source[c.Start..c.End]),
                $"Native legacy at-offset boundary {c.Type} preserves exact source text and offsets");
            check(LegacyText(text, 5, c.Type, -1) == (c.BeforeStart, c.BeforeEnd, source[c.BeforeStart..c.BeforeEnd]),
                $"Native legacy before-offset boundary {c.Type} returns the preceding interval");
            check(LegacyText(text, 5, c.Type, 1) == (c.AfterStart, c.AfterEnd, source[c.AfterStart..c.AfterEnd]),
                $"Native legacy after-offset boundary {c.Type} returns the following interval");
        }
        check(LegacyText(text, 14, 5, 0) == (0, 14, source) && LegacyText(text, 14, 6, 0) == (14, 14, "")
            && LegacyText(text, 14, 6, -1) == (0, 14, source),
            "Actual single-line start and end boundary queries preserve distinct document-end behavior");
        check(SetText(edit, "정😀e\u0301 end") && LegacyText(text, 2, 0, -1) == (1, 2, "😀")
            && LegacyText(text, 2, 0, 0) == (2, 3, "e") && LegacyText(text, 2, 0, 1) == (3, 4, "\u0301"),
            "Native legacy character queries preserve supplementary and combining scalar offsets");
        check(SetText(edit, "") && Enumerable.Range(0, 7).All(t => Enumerable.Range(-1, 3).All(r => LegacyText(text, 0, (uint)t, r) == (0, 0, ""))),
            "All seven native legacy boundary types return empty before, at and after intervals for empty text");
        check(SetText(edit, "붙여넣기 😀"), "The legacy boundary probe restores the owned editor's Unicode content");
    }
    [LibraryImport(Library)] private static partial nint atspi_text_get_text_before_offset(nint text, int offset, uint boundary, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_text_at_offset(nint text, int offset, uint boundary, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_text_after_offset(nint text, int offset, uint boundary, nint* error);
}
