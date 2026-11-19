using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRangeArray
    {
        internal nint Data;
        internal uint Length;
    }

    private static (int Start, int End, string Text)[] BoundedText(nint text, Rect box, uint coordinates = 1, uint xClip = 0, uint yClip = 0)
    {
        nint error = 0;
        var value = atspi_text_get_bounded_ranges(text, box.X, box.Y, box.Width, box.Height, coordinates, xClip, yClip, &error);
        try
        {
            Error(ref error);
            if (value == 0 || g_array_get_element_size(value) != sizeof(NativeTextRange))
                throw new InvalidDataException("Missing or incompatible native text range array.");
            var array = *(NativeRangeArray*)value;
            var result = new (int, int, string)[array.Length];
            for (var index = 0; index < result.Length; index++)
            {
                var range = ((NativeTextRange*)array.Data)[index];
                result[index] = (range.Start, range.End,
                    Marshal.PtrToStringUTF8(range.Content) ?? throw new InvalidDataException("Missing bounded range source content."));
            }
            return result;
        }
        finally
        {
            if (value != 0)
            {
                var array = *(NativeRangeArray*)value;
                if (g_array_get_element_size(value) == sizeof(NativeTextRange))
                    for (var index = 0; index < array.Length; index++)
                        g_free(((NativeTextRange*)array.Data)[index].Content);
                g_array_unref(value);
            }
        }
    }

    private static void VerifyBoundedText(nint text, Action<bool, string> check)
    {
        var caret = TextCaret(text);
        var selections = TextSelectionCount(text);
        var box = CharacterBox(text, 2, 1);
        foreach (var coordinates in new uint[] { 1, 0, 2 })
        {
            var requested = CharacterBox(text, 2, coordinates);
            requested.X += 2;
            requested.Y += 2;
            requested.Width -= 4;
            requested.Height -= 4;
            check(BoundedText(text, requested, coordinates).SequenceEqual([(2, 3, "정")]),
                "Independent native bounded ranges preserve source text and coordinate conversion, " + coordinates);
        }
        var partial = box;
        partial.X += Math.Max(1, box.Width / 4);
        partial.Width = Math.Max(1, box.Width / 2);
        foreach (var clip in new uint[] { 0, 1, 2, 3 })
            check(BoundedText(text, partial, xClip: clip).Length == (clip == 0 ? 1 : 0),
                "Native horizontal clipping obeys all four glyph edge policies, " + clip);
        partial = box;
        partial.Y += Math.Max(1, box.Height / 4);
        partial.Height = Math.Max(1, box.Height / 2);
        foreach (var clip in new uint[] { 0, 1, 2, 3 })
            check(BoundedText(text, partial, yClip: clip).Length == (clip == 0 ? 1 : 0),
                "Native vertical clipping obeys all four glyph edge policies, " + clip);
        foreach (var clip in new uint[] { 0, 1, 2, 3 })
        {
            var minimum = box;
            minimum.X += box.Width / 2;
            minimum.Width = box.Width / 2;
            var maximum = box;
            maximum.Width = box.Width / 2;
            check(BoundedText(text, minimum, xClip: clip).Length == ((clip & 1) == 0 ? 1 : 0)
                && BoundedText(text, maximum, xClip: clip).Length == ((clip & 2) == 0 ? 1 : 0),
                "Native minimum and maximum clipping independently retain the permitted horizontal edge, " + clip);
        }
        var all = BoundedText(text, new() { X = box.X - 100, Y = box.Y - 100, Width = 1000, Height = 1000 });
        check(all.Length >= 4 && all[0] == (2, 4, "정😀") && all.All(range => !range.Text.Contains('*') && !range.Text.Contains('\n')),
            "Native bounded source runs split hidden Markdown delimiters and visual lines without rewriting scalar offsets");
        check(BoundedText(text, new() { Width = 0, Height = 20 }).Length == 0
            && BoundedText(text, new() { X = -100, Y = -100, Width = 10, Height = 10 }).Length == 0,
            "Native empty and disjoint bounded queries return an empty protocol array");
        foreach (var invalid in new Action[]
        {
            () => BoundedText(text, new() { Width = -1, Height = 1 }),
            () => BoundedText(text, new() { Width = 1, Height = -1 }),
            () => BoundedText(text, box, coordinates: 3),
            () => BoundedText(text, box, xClip: 4),
            () => BoundedText(text, box, yClip: 4)
        })
        {
            var refused = false;
            try { invalid(); }
            catch (InvalidOperationException error) when (error.Message.StartsWith("Independent AT-SPI call failed:", StringComparison.Ordinal)) { refused = true; }
            check(refused, "Invalid native bounded query input is rejected through an explicit protocol error");
        }
        check(TextCaret(text) == caret && TextSelectionCount(text) == selections,
            "Native bounded queries preserve the source caret and selection");
    }

    [LibraryImport(Library)] private static partial nint atspi_text_get_bounded_ranges(nint text, int x, int y, int width, int height, uint coordinates, uint xClip, uint yClip, nint* error);
    [LibraryImport("libglib-2.0.so.0")] private static partial uint g_array_get_element_size(nint array);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_array_unref(nint array);
}
