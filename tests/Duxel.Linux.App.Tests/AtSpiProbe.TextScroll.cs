using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private static bool ScrollText(nint text, int start, int end, uint placement)
    {
        nint error = 0;
        var result = atspi_text_scroll_substring_to(text, start, end, placement, &error);
        Error(ref error);
        return result != 0;
    }

    private static bool ScrollTextPoint(nint text, int start, int end, uint coordinates, int x, int y)
    {
        nint error = 0;
        var result = atspi_text_scroll_substring_to_point(text, start, end, coordinates, x, y, &error);
        Error(ref error);
        return result != 0;
    }

    private static Rect ComponentBox(nint accessible, uint coordinates = 1)
    {
        using var component = new NativeObject(atspi_accessible_get_component_iface(accessible));
        nint error = 0;
        var value = atspi_component_get_extents(component.Value, coordinates, &error);
        try
        {
            Error(ref error);
            return value != 0 ? *(Rect*)value : throw new InvalidDataException("Missing native component box.");
        }
        finally { if (value != 0) g_free(value); }
    }

    private static void VerifyTextScroll(nint accessible, nint text, nint edit, Action<bool, string> check)
    {
        var source = string.Join('\n', Enumerable.Repeat(new string('W', 70), 20));
        check(SetText(edit, source), "Native source-range scroll fixture installs a two-axis unwrapped document");
        var caret = TextCaret(text);
        var selections = TextSelectionCount(text);
        var component = ComponentBox(accessible);
        // Independent fixture: small textarea has9/9 horizontal and5/7 vertical content insets.
        var viewport = new Rect { X = component.X + 9, Y = component.Y + 5, Width = component.Width - 18, Height = component.Height - 12 };
        const int target = 5 * 71 + 40;
        check(ScrollText(text, target, target + 1, 0), "Native ScrollSubstringTo reaches the real source-range scroll action");
        var aligned = CharacterBox(text, target, 1);
        check(Math.Abs(aligned.X - viewport.X) <= 1 && Math.Abs(aligned.Y - viewport.Y) <= 1,
            "Native top-left range scrolling aligns actual glyph geometry inside the editor viewport");
        using (var states = new NativeObject(atspi_accessible_get_state_set(accessible)))
            check(atspi_state_set_contains(states.Value, 12) == 0 && TextCaret(text) == caret && TextSelectionCount(text) == selections,
                "Native source-range scrolling does not steal keyboard focus or move source selection");
        const int distant = 15 * 71 + 60;
        check(ScrollText(text, distant, distant + 1, 6), "Native anywhere placement reveals an offscreen source character");
        var visible = CharacterBox(text, distant, 1);
        check(visible.X >= viewport.X - 1 && visible.Y >= viewport.Y - 1
            && visible.X + visible.Width <= viewport.X + viewport.Width + 1 && visible.Y + visible.Height <= viewport.Y + viewport.Height + 1
            && TextAtPoint(text, visible.X + visible.Width / 2, visible.Y + visible.Height / 2, 1) == distant,
            "The scrolled native source cell is visible and maps back to its original scalar offset");
        check(ScrollText(text, distant, distant + 1, 6) && CharacterBox(text, distant, 1).X == visible.X
            && CharacterBox(text, distant, 1).Y == visible.Y,
            "Repeating native nearest range scrolling preserves an already visible source position");
        foreach (var coordinates in new uint[] { 1, 0, 2 })
        {
            var windowBox = CharacterBox(text, target, 1);
            var requestedBox = CharacterBox(text, target, coordinates);
            var x = viewport.X + 30;
            var y = viewport.Y + 40;
            check(ScrollTextPoint(text, target, target + 1, coordinates,
                x + requestedBox.X - windowBox.X, y + requestedBox.Y - windowBox.Y),
                "Native range-to-point accepts the declared coordinate system, " + coordinates);
            var actual = CharacterBox(text, target, 1);
            check(Math.Abs(actual.X - x) <= 1 && Math.Abs(actual.Y - y) <= 1,
                "Native screen/window/parent point conversion reaches the actual requested source position, " + coordinates);
        }
        var before = CharacterBox(text, target, 1);
        check(!ScrollText(text, -1, 1, 6) && !ScrollText(text, 2, 1, 6) && !ScrollText(text, 0, source.Length + 1, 6)
            && !ScrollText(text, 0, 1, 7), "Invalid native source ranges and scroll placement fail before scrolling");
        check(!ScrollTextPoint(text, target, target + 1, 1, viewport.X - 5, viewport.Y)
            && CharacterBox(text, target, 1).X == before.X && CharacterBox(text, target, 1).Y == before.Y,
            "A native target outside the visible local viewport is refused without moving source geometry");
        check(ScrollText(text, source.Length, source.Length, 6) && ReadText(text, 0, -1) == source
            && TextCaret(text) == caret && TextSelectionCount(text) == selections,
            "Native end-insertion range scroll preserves the exact committed source and caret");
    }

    [LibraryImport(Library)] private static partial int atspi_text_scroll_substring_to(nint text, int start, int end, uint placement, nint* error);
    [LibraryImport(Library)] private static partial int atspi_text_scroll_substring_to_point(nint text, int start, int end, uint coordinates, int x, int y, nint* error);
}
