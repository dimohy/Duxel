using System.Runtime.InteropServices;
using Duxel.Platform.Linux;

internal static unsafe partial class AtSpiProbe
{
    private static bool SetText(nint edit, string text)
    {
        nint error = 0;
        var result = atspi_editable_text_set_text_contents(edit, text, &error);
        Error(ref error);
        return result != 0;
    }
    private static bool InsertText(nint edit, int position, string text, int bytes)
    {
        nint error = 0;
        var result = atspi_editable_text_insert_text(edit, position, text, bytes, &error);
        Error(ref error);
        return result != 0;
    }
    private static void VerifyEditableText(nint input, nint button, nuint nativeWindow, string privateSelection, Action<bool, string> check)
    {
        check(atspi_accessible_get_editable_text_iface(button) == 0, "A nontext button omits the native EditableText interface");
        using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(input));
        using var text = new NativeObject(atspi_accessible_get_text_iface(input));
        using var clipboard = new LinuxX11Clipboard(new()
        {
            Selection = privateSelection
        });
        using var peer = new NativePeer(nativeWindow);
        const string original = "붙여넣기 😀";
        void Shortcut(nuint symbol)
        {
            peer.Key(true, symbol, 4);
            peer.Key(false, symbol, 4);
        }
        check(SetText(edit.Value, "정😀z") && ReadText(text.Value, 0, -1) == "정😀z" && TextCaret(text.Value) == 3,
            "Native SetTextContents changes the real shared editor and publishes a scalar end caret");
        Shortcut(0x7a);
        Wait(() => ReadText(text.Value, 0, -1) == original, "actual undo of native SetTextContents");
        check(true, "Actual Ctrl+Z reverses one native replacement through the existing history");
        Shortcut(0x79);
        Wait(() => ReadText(text.Value, 0, -1) == "정😀z", "actual redo of native SetTextContents");
        check(true, "Actual Ctrl+Y reapplies the native replacement");
        check(InsertText(edit.Value, 1, "한😀ignored", 7) && ReadText(text.Value, 0, -1) == "정한😀😀z" && TextCaret(text.Value) == 3,
            "Native InsertText uses scalar positions and an exact UTF8 byte prefix without splitting the emoji");
        var unchanged = ReadText(text.Value, 0, -1);
        check(!InsertText(edit.Value, 0, "한😀ignored", 4) && ReadText(text.Value, 0, -1) == unchanged,
            "A byte length splitting a UTF8 scalar is rejected without replacement or truncation");
        check(!InsertText(edit.Value, -1, "x", 1) && !InsertText(edit.Value, 0, "x", -1) && ReadText(text.Value, 0, -1) == unchanged,
            "Invalid native insert positions and byte counts preserve committed text");
        nint error = 0;
        var deleted = atspi_editable_text_delete_text(edit.Value, 1, 3, &error);
        Error(ref error);
        check(deleted != 0 && ReadText(text.Value, 0, -1) == "정😀z", "Native DeleteText removes the requested scalar range through the actual edit buffer");
        var caret = TextCaret(text.Value);
        var copied = atspi_editable_text_copy_text(edit.Value, 1, 2, &error);
        Error(ref error);
        check(copied != 0 && clipboard.GetText() == "😀" && TextCaret(text.Value) == caret,
            "Native CopyText uses the caller-supplied private clipboard service and preserves the editor caret");
        var cut = atspi_editable_text_cut_text(edit.Value, 1, 2, &error);
        Error(ref error);
        check(cut != 0 && ReadText(text.Value, 0, -1) == "정z" && clipboard.GetText() == "😀",
            "Native CutText copies and removes the complete supplementary scalar");
        clipboard.SetText("한😀");
        var pasted = atspi_editable_text_paste_text(edit.Value, 1, &error);
        Error(ref error);
        check(pasted != 0 && ReadText(text.Value, 0, -1) == "정한😀z",
            "Native PasteText inserts private clipboard Unicode at the explicit scalar position");
        clipboard.SetText("bad\nline");
        pasted = atspi_editable_text_paste_text(edit.Value, 0, &error);
        Error(ref error);
        check(pasted == 0 && ReadText(text.Value, 0, -1) == "정한😀z",
            "Native single-line paste rejects forbidden controls without sanitizing or partial writes");
        check(!SetText(edit.Value, "bad\ttext") && ReadText(text.Value, 0, -1) == "정한😀z",
            "Native SetTextContents rejects forbidden controls before mutation");
        check(SetText(edit.Value, new string('x', 256)) && !InsertText(edit.Value, 0, "y", 1)
            && ReadText(text.Value, 0, -1) == new string('x', 256), "Native maximum-length insertion fails atomically rather than truncating");
        check(!SetText(edit.Value, new string('x', 257)) && ReadText(text.Value, 0, -1).Length == 256,
            "Native overlong replacement leaves the exact-limit text intact");
        check(SetText(edit.Value, original) && ReadText(text.Value, 0, -1) == original,
            "The native edit probe restores the owned editor's original committed Unicode text");
        VerifyTextBoundaries(edit.Value, text.Value, check);
        VerifyLegacyText(edit.Value, text.Value, check);
        VerifyTextAttributes(edit.Value, text.Value, check);
        using var states = new NativeObject(atspi_accessible_get_state_set(input));
        check(atspi_state_set_contains(states.Value, 12) != 0, "Native range and clipboard edits preserve actual keyboard focus");
    }
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_editable_text_iface(nint input);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial int atspi_editable_text_set_text_contents(nint edit, string text, nint* error);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial int atspi_editable_text_insert_text(nint edit, int position, string text, int length, nint* error);
    [LibraryImport(Library)] private static partial int atspi_editable_text_copy_text(nint edit, int start, int end, nint* error);
    [LibraryImport(Library)] private static partial int atspi_editable_text_cut_text(nint edit, int start, int end, nint* error);
    [LibraryImport(Library)] private static partial int atspi_editable_text_delete_text(nint edit, int start, int end, nint* error);
    [LibraryImport(Library)] private static partial int atspi_editable_text_paste_text(nint edit, int position, nint* error);
}
