internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryCombobox(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint xid)
    {
        const int comboRole = 11, optionRole = 32, buttonRole = 43, entryRole = 79;
        const int expandedState = 10, focusedState = 12, showingState = 25;
        using var peer = new NativePeer(xid);
        using var field = required("Search cities", window, comboRole);
        using var editor = required("Search cities", field.Value, entryRole);
        using var arrow = required("Open city list", field.Value, buttonRole);
        using var text = new NativeObject(atspi_accessible_get_text_iface(editor.Value));
        using var component = new NativeObject(atspi_accessible_get_component_iface(editor.Value));
        nint error = 0;
        var focused = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (focused == 0)
            throw new InvalidOperationException("The Combobox editor refused AT-SPI focus.");
        Wait(() => HasState(editor.Value, focusedState), "the focused Combobox editor");
        checks.Add("AT-SPI exposes an editable child and focuses it through the native bridge.");

        peer.KeyBurst(['l', 'o']);
        using var london = required("London", window, optionRole);
        Wait(() => ReadText(text.Value, 0, -1) == "lo" && HasState(field.Value, expandedState)
            && HasState(london.Value, showingState), "the filtered London suggestion");
        checks.Add("X11 typing updates the editor and opens the filtered list.");

        peer.KeyBurst([0xff54, 0xff0d]);
        Wait(() => ReadText(text.Value, 0, -1) == "London" && !HasState(field.Value, expandedState)
            && HasState(editor.Value, focusedState), "keyboard selection and editor focus");
        checks.Add("Down and Enter commit London, close the popup and restore editor focus.");

        Click(arrow);
        using var busan = required("Busan", window, optionRole);
        Wait(() => HasState(field.Value, expandedState) && HasState(busan.Value, showingState), "the arrow reopened the complete list");
        Click(busan);
        Wait(() => ReadText(text.Value, 0, -1) == "Busan" && !HasState(field.Value, expandedState)
            && HasState(editor.Value, focusedState), "pointer selection and editor focus");
        checks.Add("The arrow reopens the full list. Clicking Busan commits it and returns focus to the editor.");

        Click(editor);
        peer.KeyBurst([0xff57, 'x']);
        Wait(() => ReadText(text.Value, 0, -1) == "Busanx" && HasState(field.Value, expandedState), "an unmatched freeform draft");
        peer.KeyBurst([0xff1b]);
        Wait(() => ReadText(text.Value, 0, -1) == "Busanx" && !HasState(field.Value, expandedState)
            && HasState(editor.Value, focusedState), "Escape preserved the freeform draft");
        checks.Add("Escape dismisses the popup and preserves the freeform draft and focus.");

        void Click(NativeObject element)
        {
            var bounds = ComponentBox(element.Value);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("The Combobox click target has no visible bounds.");
            var x = bounds.X + bounds.Width / 2;
            var y = bounds.Y + bounds.Height / 2;
            peer.Pointer(6, x, y);
            peer.Pointer(4, x, y, 1);
            peer.Pointer(5, x, y, 1, 256);
        }
    }
}
