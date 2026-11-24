internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGallerySelect(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint xid)
    {
        const int comboRole = 11, optionRole = 32, listBoxRole = 98;
        const int enabledState = 8, expandedState = 10, focusedState = 12, showingState = 25;
        using var peer = new NativePeer(xid);
        using var field = required("Region", window, comboRole);
        using var list = required("Region", field.Value, listBoxRole);
        using var japan = required("Japan", list.Value, optionRole);
        using var germany = required("Germany", list.Value, optionRole);
        using var component = new NativeObject(atspi_accessible_get_component_iface(field.Value));
        nint error = 0;
        var focused = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (focused == 0)
            throw new InvalidOperationException("The Select field refused AT-SPI focus.");
        Wait(() => HasState(field.Value, focusedState), "the focused Select field");
        using (required("Selected value: kr", window, null)) { }
        checks.Add("The actual Select initializes the first enabled option and accepts accessible focus.");

        Key(0xff54, "us");
        Key(0xff57, "jp");
        Key(0xff50, "kr");
        checks.Add("X11 Down, End and Home change the selection in the closed field and skip the disabled last option.");

        peer.KeyBurst([0x20]);
        Wait(() => HasState(field.Value, expandedState) && HasState(germany.Value, showingState), "opened Select suggestions");
        if (HasState(germany.Value, enabledState))
            throw new InvalidOperationException("The disabled Select option was exposed as enabled.");
        peer.KeyBurst([0xff1b]);
        Wait(() => !HasState(field.Value, expandedState) && HasState(field.Value, focusedState), "Escape closed Select and restored focus");
        checks.Add("Space opens the list and exposes the disabled option. Escape closes it and returns focus to the field.");

        peer.KeyBurst([0x20]);
        Wait(() => HasState(field.Value, expandedState) && HasState(japan.Value, showingState), "the visible Japan option");
        var bounds = ComponentBox(japan.Value);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException("The visible Select option has no pointer target.");
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;
        peer.Pointer(6, x, y);
        peer.Pointer(4, x, y, 1);
        peer.Pointer(5, x, y, 1, 256);
        using (required("Selected value: jp", window, null)) { }
        Wait(() => !HasState(field.Value, expandedState) && HasState(field.Value, focusedState), "pointer selection closed Select and restored focus");
        checks.Add("An X11 pointer click commits an option, closes the list and returns focus to the field.");

        Key('u', "us");
        checks.Add("X11 typeahead commits United States while the list remains closed.");

        void Key(nuint key, string value)
        {
            peer.KeyBurst([key]);
            using var accepted = required("Selected value: " + value, window, null);
            if (HasState(field.Value, expandedState) || !HasState(field.Value, focusedState))
                throw new InvalidOperationException("Closed Select navigation changed the popup or focus state.");
        }
    }
}
