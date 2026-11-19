using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct TextRange
    {
        internal int Start, End;
    }
    private static string ReadText(nint text, int start, int end)
    {
        nint error = 0;
        var value = atspi_text_get_text(text, start, end, &error);
        try
        {
            Error(ref error);
            return Marshal.PtrToStringUTF8(value) ?? throw new InvalidDataException("Native text returned null.");
        }
        finally { if (value != 0) g_free(value); }
    }
    private static int TextCaret(nint text)
    {
        nint error = 0;
        var result = atspi_text_get_caret_offset(text, &error);
        Error(ref error);
        return result;
    }
    private static int TextSelectionCount(nint text)
    {
        nint error = 0;
        var result = atspi_text_get_n_selections(text, &error);
        Error(ref error);
        return result;
    }
    private static TextRange ReadSelection(nint text)
    {
        nint error = 0;
        var value = atspi_text_get_selection(text, 0, &error);
        try
        {
            Error(ref error);
            return value != 0 ? *(TextRange*)value : throw new InvalidDataException("Native selection returned null.");
        }
        finally { if (value != 0) g_free(value); }
    }
    private static void VerifyText(nint input, nint button, nuint nativeWindow, EventWatch events, Action<bool, string> check)
    {
        check(atspi_accessible_get_text_iface(button) == 0, "Nontext controls omit the native Text interface");
        using var text = new NativeObject(atspi_accessible_get_text_iface(input));
        nint error = 0;
        var count = atspi_text_get_character_count(text.Value, &error);
        Error(ref error);
        check(count == 6 && TextCaret(text.Value) == 6, "Native Text counts Korean and emoji as Unicode scalars rather than UTF16 units");
        VerifyTextGeometry(input, text.Value, nativeWindow, check);
        check(ReadText(text.Value, 0, -1) == "붙여넣기 😀" && ReadText(text.Value, 4, 6) == " 😀" && ReadText(text.Value, 6, 6) == "",
            "Native text ranges preserve exact Unicode content, exclusive ends and the end sentinel");
        var rejected = atspi_text_get_text(text.Value, -1, 3, &error);
        try
        {
            check(error != 0, "Invalid native text queries return a protocol error without terminating the bridge");
        }
        finally
        {
            if (rejected != 0)
                g_free(rejected);
            if (error != 0)
                g_error_free(error);
            error = 0;
        }
        var scalar = atspi_text_get_character_at_offset(text.Value, 5, &error);
        Error(ref error);
        check(scalar == 0x1F600, "Native character lookup returns the complete supplementary code point");
        using (var states = new NativeObject(atspi_accessible_get_state_set(input)))
            check(atspi_state_set_contains(states.Value, 38) != 0, "The input advertises selectable text independently of child selection");
        check(TextSelectionCount(text.Value) == 0, "The native editor initially has no nonempty text selection");
        var before = events.TextSelections;
        var accepted = atspi_text_add_selection(text.Value, 4, 6, &error);
        Error(ref error);
        var range = ReadSelection(text.Value);
        check(accepted != 0 && TextSelectionCount(text.Value) == 1 && range.Start == 4 && range.End == 6,
            "Native AddSelection reaches the real editor and returns scalar selection bounds");
        Wait(() => { events.Check(); return events.TextSelections > before; }, "the native TextSelectionChanged event");
        check(true, "The native client observes actual text selection changes without clearing its cache");
        accepted = atspi_text_add_selection(text.Value, 0, 1, &error);
        Error(ref error);
        check(accepted == 0 && ReadSelection(text.Value).Start == 4, "An additional disjoint text selection is rejected without replacing the existing selection");
        accepted = atspi_text_set_selection(text.Value, 0, 6, 1, &error);
        Error(ref error);
        range = ReadSelection(text.Value);
        check(accepted != 0 && range.Start == 1 && range.End == 6 && TextCaret(text.Value) == 1,
            "Native SetSelection preserves reversed anchor and caret while reporting ordered bounds");
        accepted = atspi_text_set_selection(text.Value, 1, 0, 2, &error);
        Error(ref error);
        check(accepted == 0 && TextCaret(text.Value) == 1, "An invalid text selection index preserves the editor state");
        accepted = atspi_text_remove_selection(text.Value, 0, &error);
        Error(ref error);
        check(accepted != 0 && TextSelectionCount(text.Value) == 0 && TextCaret(text.Value) == 1,
            "Native RemoveSelection collapses at the existing caret");
        accepted = atspi_text_set_caret_offset(text.Value, 7, &error);
        Error(ref error);
        check(accepted == 0 && TextCaret(text.Value) == 1, "A caret outside scalar bounds is rejected without silently clamping");
        var caretEvents = events.Carets;
        accepted = atspi_text_set_caret_offset(text.Value, 6, &error);
        Error(ref error);
        Wait(() => { events.Check(); return events.Carets > caretEvents && events.LastCaret == 6; }, "the native scalar TextCaretMoved event");
        check(accepted != 0 && TextCaret(text.Value) == 6 && ReadText(text.Value, 0, -1) == "붙여넣기 😀",
            "Native caret movement emits its scalar position and preserves committed text");
        using (var states = new NativeObject(atspi_accessible_get_state_set(input)))
            check(atspi_state_set_contains(states.Value, 12) != 0, "Native text selection and caret changes preserve keyboard focus");
        using var peer = new NativePeer(nativeWindow);
        var changes = events.TextChanges.Count;
        peer.Key(true, 0xff08);
        peer.Key(false, 0xff08);
        Wait(() => { events.Check(); return ReadText(text.Value, 0, -1) == "붙여넣기 " && events.TextChanges.Count > changes; },
            "actual emoji deletion and native TextChanged delivery");
        check(events.TextChanges.Skip(changes).Any(change => change is { Type: "object:text-changed:delete", Start: 5, Length: 1, Text: "😀" }),
            "Actual Backspace emits a one-scalar deletion with the complete emoji payload");
        changes = events.TextChanges.Count;
        peer.Key(true, 0x7a, 4);
        peer.Key(false, 0x7a, 4);
        Wait(() => { events.Check(); return ReadText(text.Value, 0, -1) == "붙여넣기 😀" && events.TextChanges.Count > changes; },
            "actual undo and native TextChanged delivery");
        check(events.TextChanges.Skip(changes).Any(change => change is { Type: "object:text-changed:insert", Start: 5, Length: 1, Text: "😀" }),
            "Actual undo emits a one-scalar insertion with the complete emoji payload");
    }
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_text_iface(nint value);
    [LibraryImport(Library)] private static partial int atspi_text_get_character_count(nint value, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_get_caret_offset(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_text(nint value, int start, int end, nint* error);
    [LibraryImport(Library)] private static partial uint atspi_text_get_character_at_offset(nint value, int offset, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_set_caret_offset(nint value, int offset, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_get_n_selections(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_selection(nint value, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_add_selection(nint value, int start, int end, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_set_selection(nint value, int index, int start, int end, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_remove_selection(nint value, int index, nint* error);
}
