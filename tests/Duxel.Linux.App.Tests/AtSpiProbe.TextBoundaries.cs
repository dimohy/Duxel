using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private static void VerifyTextBoundaries(nint edit, nint text, Action<bool, string> check)
    {
        const string source = "정세일 한글. Hello 😀 world! 끝.";
        check(SetText(edit, source), "The real native editor accepts the Unicode boundary fixture text");
        check(TextUnit(text, 1, 1) == (0, 4, "정세일 ") && TextUnit(text, 6, 1) == (4, 8, "한글. "),
            "Native word granularity returns exact Korean words and their following punctuation");
        check(TextUnit(text, 14, 1) == (8, 16, "Hello 😀 "),
            "Native word granularity preserves the complete emoji and scalar interval");
        check(TextUnit(text, 14, 2) == (8, 23, "Hello 😀 world! "),
            "Native sentence granularity returns the complete sentence up to the next sentence start");
        check(TextUnit(text, 14, 4) == (0, 25, source), "Native paragraph granularity preserves a whole paragraph independently of painted line width");
        nint error = 0;
        var moved = atspi_text_set_caret_offset(text, 0, &error);
        Error(ref error);
        check(moved != 0 && Enumerable.Range(0, 16).All(_ => TextUnit(text, 14, 1) == (8, 16, "Hello 😀 ")),
            "Repeated boundary queries remain coherent after a caret-only native refresh");
        check(SetText(edit, "e\u0301 😀 end") && TextUnit(text, 1, 1) == (0, 5, "e\u0301 😀 "),
            "Native word queries preserve combining marks and supplementary scalar offsets after content replacement");
        check(SetText(edit, "") && Enumerable.Range(0, 5).All(unit => TextUnit(text, 0, (uint)unit) == (0, 0, "")),
            "All five native text granularities return valid empty ranges for an empty actual editor");
        check(SetText(edit, "붙여넣기 😀"), "The boundary probe restores the owned editor's original Unicode content");
    }
}
