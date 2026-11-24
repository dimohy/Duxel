internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryDropdown(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint xid)
    {
        const int comboRole = 11, optionRole = 32, listRole = 98;
        const int enabledState = 8, expandedState = 10, focusedState = 12, showingState = 25;
        using var peer = new NativePeer(xid);
        using var single = required("Work location", window, comboRole);
        using var locations = required("Work location", single.Value, listRole);
        using var locationSelection = new NativeObject(atspi_accessible_get_selection_iface(locations.Value));
        using var disabled = required("Disabled location", locations.Value, optionRole);
        Reveal(single);
        Click(single);
        Wait(() => HasState(single.Value, expandedState) && HasState(disabled.Value, showingState), "the open location list");
        if (HasState(disabled.Value, enabledState))
            throw new InvalidOperationException("The disabled location was exposed as enabled.");
        peer.KeyBurst([0xff57, 0xff0d]);
        Wait(() => !HasState(single.Value, expandedState) && HasState(single.Value, focusedState)
            && Selected(locationSelection, "Japan"), "keyboard selection of the last enabled location");
        checks.Add("End and Enter skip the disabled location, select Japan and return focus to the closed field.");

        Click(single);
        using var korea = required("South Korea", locations.Value, optionRole);
        Wait(() => HasState(single.Value, expandedState) && HasState(korea.Value, showingState), "the visible South Korea option");
        Click(korea);
        Wait(() => !HasState(single.Value, expandedState) && HasState(single.Value, focusedState)
            && Selected(locationSelection, "South Korea"), "pointer replacement of the single selection");
        checks.Add("Pointer selection replaces the previous location and closes the single-selection list.");

        using var multiple = required("Features to try", window, comboRole);
        using var features = required("Features to try", multiple.Value, listRole);
        using var featureSelection = new NativeObject(atspi_accessible_get_selection_iface(features.Value));
        using var image = required("Image", features.Value, optionRole);
        using var svg = required("SVG icon", features.Value, optionRole);
        Reveal(multiple);
        Click(multiple);
        Wait(() => HasState(multiple.Value, expandedState) && HasState(image.Value, showingState), "the opened features list");
        Click(image);
        Wait(() => HasState(multiple.Value, expandedState) && Selected(featureSelection, "Image"), "the first feature without dismissal");
        Click(svg);
        Wait(() => HasState(multiple.Value, expandedState) && Selected(featureSelection, "Image", "SVG icon"), "both selected features");
        checks.Add("Clicking two checkable features adds both to AT-SPI Selection and keeps the list open.");

        Click(image);
        Wait(() => HasState(multiple.Value, expandedState) && Selected(featureSelection, "SVG icon"), "removal of one selected feature");
        peer.KeyBurst([0xff1b]);
        Wait(() => !HasState(multiple.Value, expandedState) && HasState(multiple.Value, focusedState)
            && Selected(featureSelection, "SVG icon"), "Escape with one retained selection and restored focus");
        checks.Add("Clicking a selected option toggles it off. Escape preserves the other selection and restores field focus.");

        static bool Selected(NativeObject selection, params string[] expected)
        {
            if (SelectionCount(selection.Value) != expected.Length)
                return false;
            for (var i = 0; i < expected.Length; i++)
            {
                using var child = SelectedChild(selection.Value, i);
                if (child is null || Name(child.Value) != expected[i])
                    return false;
            }
            return true;
        }

        void Click(NativeObject element)
        {
            var bounds = ComponentBox(element.Value);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("The Dropdown click target has no visible bounds.");
            var x = bounds.X + bounds.Width / 2;
            var y = bounds.Y + bounds.Height / 2;
            peer.Pointer(6, x, y);
            peer.Pointer(4, x, y, 1);
            peer.Pointer(5, x, y, 1, 256);
        }
    }
}
