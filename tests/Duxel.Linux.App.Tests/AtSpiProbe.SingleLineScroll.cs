internal static unsafe partial class AtSpiProbe
{
    private static void VerifySingleLineScroll(nint window, nuint nativeWindow, Action<bool, string> check)
    {
        using var input = Find(window, value => Role(value) == 79 && Name(value) == "Single-line editor")
            ?? throw new InvalidOperationException("The native single-line scroll fixture is missing.");
        using var text = new NativeObject(atspi_accessible_get_text_iface(input.Value));
        using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(input.Value));
        using var component = new NativeObject(atspi_accessible_get_component_iface(input.Value));
        var source = "정😀" + new string('W', 80);
        check(SetText(edit.Value, source), "Native single-line source-range fixture installs exact Korean/emoji text with horizontal overflow");
        var field = ComponentBox(input.Value);
        var left = field.X + 9;
        var right = field.X + field.Width - 9;
        const int target = 30;
        var caret = TextCaret(text.Value);
        var selections = TextSelectionCount(text.Value);
        foreach (var placement in new uint[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            var accepted = ScrollText(text.Value, 0, 1, 0) && ScrollText(text.Value, target, target + 1, placement);
            var cell = CharacterBox(text.Value, target, 1);
            var alignLeft = placement is 0 or 4;
            check(accepted && Math.Abs((alignLeft ? cell.X : cell.X + cell.Width) - (alignLeft ? left : right)) <= 1
                && TextCaret(text.Value) == caret && TextSelectionCount(text.Value) == selections,
                "Native single-line placement reaches actual glyph geometry and preserves source selection, " + placement);
        }
        using (var states = new NativeObject(atspi_accessible_get_state_set(input.Value)))
            check(atspi_state_set_contains(states.Value, 12) == 0,
                "Native single-line range movement does not steal keyboard focus");
        foreach (var coordinates in new uint[] { 1, 0, 2 })
        {
            var local = CharacterBox(text.Value, target, 1);
            var requested = CharacterBox(text.Value, target, coordinates);
            check(ScrollTextPoint(text.Value, target, target + 1, coordinates,
                left + 40 + requested.X - local.X, local.Y + 1 + requested.Y - local.Y)
                && Math.Abs(CharacterBox(text.Value, target, 1).X - left - 40) <= 1,
                "Native single-line range-to-point uses the declared screen/window/parent coordinates, " + coordinates);
        }
        nint error = 0;
        var focused = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        using var peer = new NativePeer(nativeWindow);
        check(focused != 0 && peer.HasNativeFocus(), "Native single-line focus reaches the actual X11 window before active range scrolling");
        caret = TextCaret(text.Value);
        check(ScrollText(text.Value, target, target + 1, 4), "Native active single-line input accepts a range independent of its caret");
        var positioned = CharacterBox(text.Value, target, 1);
        using var other = Find(window, value => Role(value) == 79 && Name(value) == "Unwrapped editor")
            ?? throw new InvalidOperationException("The native repaint trigger input is missing.");
        using var otherText = new NativeObject(atspi_accessible_get_text_iface(other.Value));
        check(ScrollText(otherText.Value, 0, 1, 6) && CharacterBox(text.Value, target, 1).X == positioned.X
            && TextCaret(text.Value) == caret,
            "A completed native action on another input guarantees a published frame while preserving the focused source viewport");
        // Observe the real focused input without assuming a repaint count from elapsed time alone.
        var observation = System.Diagnostics.Stopwatch.StartNew();
        while (observation.ElapsedMilliseconds < 650)
        {
            if (CharacterBox(text.Value, target, 1).X != positioned.X || TextCaret(text.Value) != caret)
                throw new InvalidOperationException("An unchanged native active caret reverted the requested range viewport.");
            while (g_main_context_iteration(0, 0) != 0) { }
            Thread.Sleep(10);
        }
        check(true, "Native focused650ms observation retains the requested source viewport without moving the caret");
        var acceptedCaret = atspi_text_set_caret_offset(text.Value, caret, &error);
        Error(ref error);
        var end = RangeBox(text.Value, source.Length - 1, source.Length - 1, 1);
        check(acceptedCaret != 0 && end.X <= right + 1,
            "A native explicit unchanged-caret request releases retained scrolling and reveals the caret");
        check(ScrollText(text.Value, target, target + 1, 4), "Native input accepts another independent source viewport before a keyboard move");
        peer.Key(true, 0xff50);
        try { Wait(() => TextCaret(text.Value) == 0, "the owned single-line Home navigation"); }
        finally { peer.Key(false, 0xff50); }
        check(CharacterBox(text.Value, 0, 1).X >= left - 1 && ReadText(text.Value, 0, -1) == source,
            "Real X11 Home navigation resumes caret visibility without changing the source");
        check(ScrollText(text.Value, source.Length - 1, source.Length - 1, 6)
            && TextCaret(text.Value) == 0 && TextSelectionCount(text.Value) == selections,
            "Native single-line end-insertion range scrolling preserves the unrelated caret and selection");
        check(SetText(edit.Value, "정😀") && ScrollText(text.Value, 1, 2, 6)
            && ReadText(text.Value, 0, -1) == "정😀" && CharacterBox(text.Value, 0, 1).X >= left - 1,
            "Native shortened single-line source clamps old scroll offsets and treats visible ranges as no-ops");
    }
}
