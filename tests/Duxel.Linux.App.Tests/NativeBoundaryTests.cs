using Duxel.Core;
using Duxel.Platform.Linux;

internal static class NativeBoundaryTests
{
    internal static string Run(Action<bool, string> check)
    {
        var version = LinuxTextBoundaries.EnsureAvailable();
        check(version.Length > 0, "The native Unicode boundary dependency publishes its installed version");
        var content = new UiAccessibilityTextContent("정세일 한글. Hello 😀 world! 끝.");
        var boundaries = LinuxTextBoundaries.Retain(null, content, "ko");
        check(boundaries.GetRange(1, 1) == (0, 4) && boundaries.GetRange(6, 1) == (4, 8),
            "Korean word-start ranges include punctuation and following separators up to the next word");
        check(boundaries.GetRange(14, 1) == (8, 16) && content.GetText(8, 16) == "Hello 😀 ",
            "An emoji between words belongs to the preceding word-start interval without splitting its scalar");
        check(boundaries.GetRange(17, 1) == (16, 23) && boundaries.GetRange(25, 1) == (23, 25),
            "Word lookup preserves independent scalar offsets and returns the preceding word at document end");
        check(boundaries.GetRange(6, 2) == (0, 8) && boundaries.GetRange(14, 2) == (8, 23)
            && boundaries.GetRange(25, 2) == (23, 25), "Native sentence-start ranges retain complete intervening spaces and supplementary characters");
        check(boundaries.GetRange(14, 4) == (0, 25), "A single paragraph covers its entire scalar source text");
        check(ReferenceEquals(boundaries, LinuxTextBoundaries.Retain(boundaries, content, "ko")),
            "Repeated queries reuse the exact boundary table for unchanged content and language");
        var locale = LinuxTextBoundaries.Retain(boundaries, content, "en");
        check(!ReferenceEquals(locale, boundaries) && ReferenceEquals(locale.Content, content),
            "Changing the language rebuilds boundary data while retaining immutable source text");
        var replaced = LinuxTextBoundaries.Retain(boundaries, new("new source"), "ko");
        check(!ReferenceEquals(replaced, boundaries) && boundaries.GetRange(14, 1) == (8, 16),
            "Replacing source content does not mutate a previously captured boundary table");
        var combining = LinuxTextBoundaries.Retain(null, new("e\u0301 😀 end"), "en");
        check(combining.GetRange(1, 1) == (0, 5) && combining.GetRange(5, 1) == (5, 8),
            "Combining marks and emoji preserve word source boundaries in scalar rather than UTF16 units");
        var family = LinuxTextBoundaries.Retain(null, new("hi 👨‍👩‍👧‍👦 end"), "en");
        check(family.GetRange(7, 1) == (0, 11), "A joined family emoji remains inside one word-start interval instead of fragmenting the source");
        var rtl = LinuxTextBoundaries.Retain(null, new("שלום עולם. التالي."), "und");
        var rtlRange = rtl.GetRange(12, 1);
        check(rtl.Content.GetText(rtlRange.Start, rtlRange.End) == "التالي.",
            "Native script-aware word segmentation preserves the Arabic source after Hebrew words");
        var paragraphs = LinuxTextBoundaries.Retain(null, new("첫 😀\r\n둘째\u2029끝\n"), "ko");
        check(paragraphs.GetRange(2, 4) == (0, 5) && paragraphs.GetRange(5, 4) == (5, 8)
            && paragraphs.GetRange(8, 4) == (8, 10), "CRLF and Unicode paragraph separators retain their exact scalar source intervals");
        check(paragraphs.GetRange(10, 4) == (10, 10), "A trailing paragraph delimiter preserves the final empty paragraph");
        var blank = LinuxTextBoundaries.Retain(null, new("a\n\nb"), "und");
        check(blank.GetRange(2, 4) == (2, 3) && blank.GetRange(4, 4) == (3, 4),
            "Consecutive paragraph separators preserve the empty paragraph and terminal nonempty paragraph");
        var lineSeparator = LinuxTextBoundaries.Retain(null, new("a\u2028b"), "und");
        check(lineSeparator.GetRange(1, 4) == (0, 3), "A Unicode line separator does not invent a paragraph boundary");
        var empty = LinuxTextBoundaries.Retain(null, new(""), "und");
        check(new uint[] { 1, 2, 4 }.All(unit => empty.GetRange(0, unit) == (0, 0)),
            "Empty text has stable empty word, sentence and paragraph ranges");
        var punctuation = LinuxTextBoundaries.Retain(null, new("... 😀"), "und");
        check(punctuation.GetRange(3, 1) == (0, 5), "Text without lexical words still has a complete nonfragmented source interval");
        foreach (var invalid in new Action[] { () => boundaries.GetRange(-1, 1), () => boundaries.GetRange(26, 2), () => boundaries.GetRange(0, 5) })
        {
            var rejected = false;
            try
            {
                invalid();
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Invalid boundary offsets and granularities fail before querying source ranges");
        }
        return version;
    }
}
