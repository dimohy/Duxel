using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private static (int Start, int End, uint Count, string? Foreground) AttributeRun(nint text, int offset, bool defaults, bool legacy = false)
    {
        nint error = 0;
        int start, end;
        var value = legacy ? atspi_text_get_text_attributes(text, offset, &start, &end, &error)
            : atspi_text_get_attribute_run(text, offset, defaults ? 1 : 0, &start, &end, &error);
        try
        {
            Error(ref error);
            if (value == 0)
                throw new InvalidDataException("Missing native attribute table.");
            var color = g_hash_table_lookup(value, "fg-color");
            return (start, end, g_hash_table_size(value), color == 0 ? null : Marshal.PtrToStringUTF8(color));
        }
        finally { if (value != 0) g_hash_table_unref(value); }
    }
    private static string AttributeValue(nint text, int offset, string name)
    {
        nint error = 0;
        var value = atspi_text_get_text_attribute_value(text, offset, name, &error);
        try
        {
            Error(ref error);
            return Marshal.PtrToStringUTF8(value) ?? throw new InvalidDataException("Missing attribute value.");
        }
        finally { if (value != 0) g_free(value); }
    }
    private static void VerifyTextAttributes(nint edit, nint text, Action<bool, string> check)
    {
        nint error = 0;
        var defaults = atspi_text_get_default_attributes(text, &error);
        try
        {
            Error(ref error);
            if (defaults == 0)
                throw new InvalidDataException("Missing native default attributes.");
            var foreground = Marshal.PtrToStringUTF8(g_hash_table_lookup(defaults, "fg-color"));
            check(g_hash_table_size(defaults) == 2 && foreground == "7967,7967,7967"
                && Marshal.PtrToStringUTF8(g_hash_table_lookup(defaults, "size")) == "13.5",
                "Native default foreground and point size match actual editor paint and its nominal font request");
        }
        finally { if (defaults != 0) g_hash_table_unref(defaults); }
        check(AttributeRun(text, 2, false) == (0, 4, 3u, null),
            "Actual Korean font attributes use their scalar source interval and omit foreground defaults");
        check(AttributeRun(text, 2, true) == (0, 4, 5u, "7967,7967,7967"),
            "Including defaults merges actual font attributes with source foreground");
        var finalRun = AttributeRun(text, 6, false, legacy: true);
        check(finalRun == (4, 6, 3u, null),
            $"Legacy attributes select the actual final font run at document end; actual={finalRun}");
        check(AttributeValue(text, 0, "family-name") == "Malgun Gothic" && AttributeValue(text, 5, "family-name") == "DejaVu Sans",
            "Native font family queries follow actual Korean and supplementary-character font selection");
        check(AttributeValue(text, 0, "weight") == "400" && AttributeValue(text, 5, "style") == "normal",
            "Native weight and style use actual OpenType flags without a guessed font size");
        check(AttributeValue(text, 5, "fg-color") == "7967,7967,7967" && AttributeValue(text, 0, "unknown") == "",
            "Named foreground queries include defaults and unknown names return an empty value");
        check(AttributeValue(text, 0, "size") == "13.5", "Native size is the actual18 logical pixel font expressed as13.5 points");
        check(SetText(edit, "") && AttributeRun(text, 0, true) == (0, 0, 2u, "7967,7967,7967"),
            "Empty actual input retains its painter defaults with an empty attribute interval");
        check(SetText(edit, "붙여넣기 😀"), "Attribute queries restore the owned editor's original Unicode content");
    }
    [LibraryImport(Library)] private static partial nint atspi_text_get_default_attributes(nint text, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_text_attributes(nint text, int offset, int* start, int* end, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_attribute_run(nint text, int offset, int defaults, int* start, int* end, nint* error);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint atspi_text_get_text_attribute_value(nint text, int offset, string name, nint* error);
    [LibraryImport("libglib-2.0.so.0", StringMarshalling = StringMarshalling.Utf8)] private static partial nint g_hash_table_lookup(nint table, string key);
    [LibraryImport("libglib-2.0.so.0")] private static partial uint g_hash_table_size(nint table);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_hash_table_unref(nint table);
}
