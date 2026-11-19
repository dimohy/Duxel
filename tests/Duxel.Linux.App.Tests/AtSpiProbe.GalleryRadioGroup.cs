internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryRadioGroup(nint window,
        Func<string, nint, int?, NativeObject> required, List<string> checks, nuint xid, string? captureOutput)
    {
        const int groupRole = 99;
        const int radioRole = 44;
        const int focusedState = 12;
        const nuint up = 0xff52;
        const nuint down = 0xff54;

        using var group = required("처리 방식", window, groupRole);
        using var speed = required("속도 우선", group.Value, radioRole);
        using var balanced = required("균형", group.Value, radioRole);
        using var quality = required("품질 우선", group.Value, radioRole);
        using var selection = new NativeObject(atspi_accessible_get_selection_iface(group.Value));
        WaitForSelection("균형");
        checks.Add("AT-SPI exposes three radio choices and one default selection.");

        Reveal(balanced);
        using var component = new NativeObject(atspi_accessible_get_component_iface(balanced.Value));
        nint error = 0;
        var focused = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (focused == 0)
            throw new InvalidOperationException("Could not focus the selected RadioGroup option.");

        using var peer = new NativePeer(xid);
        peer.KeyBurst([down]);
        WaitForSelection("품질 우선");
        Wait(() => IsFocused(quality), "focus follows the new RadioGroup choice");
        checks.Add("X11 Down moves focus and selection to the next radio option.");

        peer.KeyBurst([up]);
        WaitForSelection("균형");
        Wait(() => IsFocused(balanced), "focus returns to the default RadioGroup choice");
        checks.Add("X11 Up restores focus and selection on the previous radio option.");

        InvokeNamed(speed, "select");
        WaitForSelection("속도 우선");
        checks.Add("Selecting another option through AT-SPI replaces the previous selection.");
        if (captureOutput is { } output)
            VerifyAllocation(output);

        void VerifyAllocation(string output)
        {
            const string qualityLabel = "시간이 더 걸려도 결과의 품질을 높입니다.";
            using var toggle = required("부모 폭 전환", window, 43);
            using var toggleComponent = new NativeObject(atspi_accessible_get_component_iface(toggle.Value));
            if (!ScrollComponentTo(toggleComponent.Value, 2))
                throw new InvalidOperationException("Could not align the RadioGroup allocation sample at the top of the viewport.");
            using var example = required("처리 우선순위", window, groupRole);
            using var qualityOption = required(qualityLabel, example.Value, radioRole);
            using var exampleSelection = new NativeObject(atspi_accessible_get_selection_iface(example.Value));
            using var initialWidth = required("부모 최대 폭 180px", window, null);
            Wait(() => IsShowing(qualityOption) && IsShowing(toggle), "both the width button and the wrapped radio option");
            InvokeNamed(qualityOption, "select");
            Wait(() => SelectionPreserved(), "selection in the narrow RadioGroup");
            var narrow = ComponentBox(qualityOption.Value);
            CaptureGalleryPair(peer, output, "RadioGroupNarrow", checks);

            InvokeNamed(toggle, "click");
            using var widerWidth = required("부모 최대 폭 360px", window, null);
            Wait(() =>
            {
                var wide = ComponentBox(qualityOption.Value);
                return IsShowing(qualityOption) && wide.Width > narrow.Width && wide.Height < narrow.Height && SelectionPreserved();
            }, "the wider radio option fitting on one line with the same selection");
            checks.Add("The width button changes option wrapping and preserves the selected radio option.");
            CaptureGalleryPair(peer, output, "RadioGroupWide", checks);

            bool SelectionPreserved()
            {
                using var selected = SelectedChild(exampleSelection.Value, 0);
                return SelectionCount(exampleSelection.Value) == 1 && selected is not null && Name(selected.Value) == qualityLabel;
            }
        }

        void WaitForSelection(string label)
        {
            Wait(() =>
            {
                using var selected = SelectedChild(selection.Value, 0);
                return SelectionCount(selection.Value) == 1 && selected is not null && Name(selected.Value) == label;
            }, label + " RadioGroup selection");
        }

        static bool IsFocused(NativeObject option)
        {
            atspi_accessible_clear_cache(option.Value);
            using var states = new NativeObject(atspi_accessible_get_state_set(option.Value));
            return atspi_state_set_contains(states.Value, focusedState) != 0;
        }
    }
}
