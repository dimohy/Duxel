using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeTextRange
    {
        internal int Start, End;
        internal nint Content;
    }
    private static Rect CharacterBox(nint text, int offset, uint coordinates)
    {
        nint error = 0;
        var value = atspi_text_get_character_extents(text, offset, coordinates, &error);
        try
        {
            Error(ref error);
            return value != 0 ? *(Rect*)value : throw new InvalidDataException("Missing character box.");
        }
        finally { if (value != 0) g_free(value); }
    }
    private static Rect RangeBox(nint text, int start, int end, uint coordinates)
    {
        nint error = 0;
        var value = atspi_text_get_range_extents(text, start, end, coordinates, &error);
        try
        {
            Error(ref error);
            return value != 0 ? *(Rect*)value : throw new InvalidDataException("Missing range box.");
        }
        finally { if (value != 0) g_free(value); }
    }
    private static int TextAtPoint(nint text, int x, int y, uint coordinates)
    {
        nint error = 0;
        var value = atspi_text_get_offset_at_point(text, x, y, coordinates, &error);
        Error(ref error);
        return value;
    }
    private static (int Start, int End, string Text) TextUnit(nint text, int offset, uint granularity)
    {
        nint error = 0;
        var value = atspi_text_get_string_at_offset(text, offset, granularity, &error);
        try
        {
            Error(ref error);
            if (value == 0)
                throw new InvalidDataException("Missing native text unit.");
            var unit = *(NativeTextRange*)value;
            return (unit.Start, unit.End, Marshal.PtrToStringUTF8(unit.Content) ?? throw new InvalidDataException("Missing native text unit content."));
        }
        finally { if (value != 0) g_boxed_free(atspi_text_range_get_type(), value); }
    }
    private static void VerifyTextGeometry(nint input, nint text, nuint nativeWindow, Action<bool, string> check)
    {
        check(TextUnit(text, 5, 0) == (5, 6, "😀") && TextUnit(text, 6, 0) == (6, 6, ""),
            "Native character granularity preserves a supplementary scalar and the end insertion position");
        check(TextUnit(text, 2, 3) == (0, 6, "붙여넣기 😀"), "Native line granularity uses the actual single painted source line");
        var first = CharacterBox(text, 0, 1);
        var last = CharacterBox(text, 5, 1);
        check(first.X > 20 && first.Y >= 125 && first.Width > 0 && first.Height > 0 && last.X > first.X,
            "Native character boxes follow the actual input content after its Korean label");
        var all = RangeBox(text, 0, 6, 1);
        check(all.X == first.X && all.Y == first.Y && all.Width == last.X + last.Width - first.X && all.Height == first.Height,
            "Native range geometry spans the complete painted text including the emoji");
        var insertion = RangeBox(text, 6, 6, 1);
        check(insertion.Width == 0 && Math.Abs(insertion.X - last.X - last.Width) <= 1,
            "An empty native range returns the downstream insertion rectangle");
        var screen = CharacterBox(text, 0, 0);
        var parent = CharacterBox(text, 0, 2);
        using var component = new NativeObject(atspi_accessible_get_component_iface(input));
        nint error = 0;
        var screenField = atspi_component_get_extents(component.Value, 0, &error);
        try
        {
            Error(ref error);
            var clientField = atspi_component_get_extents(component.Value, 1, &error);
            try
            {
                Error(ref error);
                if (screenField == 0 || clientField == 0)
                    throw new InvalidDataException("Missing coordinate reference fields.");
                var a = *(Rect*)screenField;
                var b = *(Rect*)clientField;
                check(screen.X - first.X == a.X - b.X && screen.Y - first.Y == a.Y - b.Y
                    && parent.X == first.X && parent.Y == first.Y, "Screen, window and actual parent text coordinates use the native window origin");
            }
            finally { if (clientField != 0) g_free(clientField); }
        }
        finally { if (screenField != 0) g_free(screenField); }
        check(TextAtPoint(text, first.X + first.Width / 2, first.Y + first.Height / 2, 1) == 0
            && TextAtPoint(text, screen.X + screen.Width / 2, screen.Y + screen.Height / 2, 0) == 0
            && TextAtPoint(text, 0, 0, 1) == -1, "Native text hit testing respects coordinate spaces and rejects exterior points");
        using var peer = new NativePeer(nativeWindow);
        var x = first.X + Math.Max(1, first.Width / 4);
        var y = first.Y + first.Height / 2;
        peer.Pointer(6, x, y);
        peer.Pointer(4, x, y, 1);
        Thread.Sleep(60);
        peer.Pointer(5, x, y, 1);
        Wait(() => TextCaret(text) == 0, "a real X11 click inside the reported first character box");
        check(true, "A real pointer click at the native character box reaches the matching editor caret");
        // X11 pointer and D-Bus requests use separate connections. Observe a subsequent
        // keyboard edit before restoring the caret so pending pointer release cannot race it.
        peer.Key(true, 0xff53);
        peer.Key(false, 0xff53);
        Wait(() => TextCaret(text) == 1, "the ordered Right key after the pointer release");
        var restored = atspi_text_set_caret_offset(text, 6, &error);
        Error(ref error);
        check(restored != 0 && TextCaret(text) == 6, "The geometry probe restores the actual editor caret before selection checks");
    }
    [LibraryImport(Library)] private static partial nint atspi_text_get_character_extents(nint text, int offset, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_range_extents(nint text, int start, int end, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_get_offset_at_point(nint text, int x, int y, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_text_get_string_at_offset(nint text, int offset, uint granularity, nint* error);
    [LibraryImport(Library)] private static partial nuint atspi_text_range_get_type();
}
