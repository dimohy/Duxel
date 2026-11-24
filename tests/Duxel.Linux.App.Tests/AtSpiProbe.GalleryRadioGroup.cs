internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryRadioGroup(nint window,
        Func<string, nint, int?, NativeObject> required, List<string> checks, nuint xid, string? captureOutput)
    {
        const int groupRole = 99;
        const int radioRole = 44;
        const int focusedState = 12;
        const int requiredState = 33;
        const nuint left = 0xff51;
        const nuint up = 0xff52;
        const nuint right = 0xff53;
        const nuint down = 0xff54;

        using var group = required("처리 방식", window, groupRole);
        using var speed = required("속도 우선", group.Value, radioRole);
        using var balanced = required("균형", group.Value, radioRole);
        using var quality = required("품질 우선", group.Value, radioRole);
        using var selection = new NativeObject(atspi_accessible_get_selection_iface(group.Value));
        using var groupStates = new NativeObject(atspi_accessible_get_state_set(group.Value));
        if (atspi_state_set_contains(groupStates.Value, requiredState) != 0)
            throw new InvalidOperationException("An optional RadioGroup must not expose the AT-SPI required state.");
        checks.Add("The optional processing mode RadioGroup omits the AT-SPI required state.");
        WaitForSelection(selection, "균형");
        checks.Add("AT-SPI exposes three radio choices and one default selection.");

        Focus(balanced);
        using var peer = new NativePeer(xid);
        peer.KeyBurst([down]);
        WaitForSelection(selection, "품질 우선");
        Wait(() => IsFocused(quality), "focus follows the new RadioGroup choice");
        checks.Add("X11 Down moves focus and selection to the next radio option.");

        peer.KeyBurst([up]);
        WaitForSelection(selection, "균형");
        Wait(() => IsFocused(balanced), "focus returns to the default RadioGroup choice");
        checks.Add("X11 Up restores focus and selection on the previous radio option.");

        InvokeNamed(speed, "select");
        WaitForSelection(selection, "속도 우선");
        checks.Add("Selecting another option through AT-SPI replaces the previous selection.");
        if (SelectionAction(selection.Value, 0, SelectionOperation.Clear))
            throw new InvalidOperationException("An optional RadioGroup must still refuse to clear its chosen option.");
        WaitForSelection(selection, "속도 우선");
        checks.Add("The optional RadioGroup still refuses to clear its current selection.");
        VerifyHorizontal("크기", "작게", "보통", "크게");
        VerifyHorizontal("정렬", "왼쪽", "중앙", "오른쪽");
        if (captureOutput is { } output)
            VerifyAllocation(output);
        VerifyRequiredField();

        void VerifyRequiredField()
        {
            const string hint = "알림을 받을 주기를 선택하세요.";
            using var field = required("알림 주기", window, groupRole);
            using var weekly = required("주간", field.Value, radioRole);
            using var fieldSelection = new NativeObject(atspi_accessible_get_selection_iface(field.Value));
            using var fieldStates = new NativeObject(atspi_accessible_get_state_set(field.Value));
            if (atspi_state_set_contains(fieldStates.Value, requiredState) == 0 || SelectionCount(fieldSelection.Value) != 0)
                throw new InvalidOperationException("The required RadioGroup must initially have no selected option.");
            checks.Add("The required notification RadioGroup is exposed with no initial selection.");

            if (Description(field.Value) != hint || Description(weekly.Value) != hint)
                throw new InvalidOperationException("Field help was not exposed on the group and its radio option.");
            checks.Add("The AT-SPI group and radio option both expose the Field help.");

            Focus(weekly);
            Wait(() => IsFocused(weekly), "AT-SPI focus on the weekly notification option");
            if (SelectionCount(fieldSelection.Value) != 0)
                throw new InvalidOperationException("Focusing a required radio option must not choose it.");
            checks.Add("AT-SPI can focus the required option without selecting it.");

            InvokeNamed(weekly, "select");
            WaitForSelection(fieldSelection, "주간");
            checks.Add("The AT-SPI select action commits the focused option in the Field.");
        }

        void VerifyHorizontal(string name, string firstLabel, string middleLabel, string lastLabel)
        {
            using var example = required(name, window, groupRole);
            using var first = required(firstLabel, example.Value, radioRole);
            using var middle = required(middleLabel, example.Value, radioRole);
            using var last = required(lastLabel, example.Value, radioRole);
            using var exampleSelection = new NativeObject(atspi_accessible_get_selection_iface(example.Value));
            WaitForSelection(exampleSelection, firstLabel);
            Focus(first);
            Wait(() => IsFocused(first) && IsShowing(middle) && IsShowing(last), $"visible {name} radio row");
            var firstBox = ComponentBox(first.Value);
            var middleBox = ComponentBox(middle.Value);
            var lastBox = ComponentBox(last.Value);
            if (!(firstBox.X < middleBox.X && middleBox.X < lastBox.X && firstBox.Y == middleBox.Y && middleBox.Y == lastBox.Y))
                throw new InvalidOperationException($"The {name} radio options are not arranged in one horizontal row.");
            checks.Add($"The {name} RadioGroup has three horizontal AT-SPI bounds and one default selection.");

            peer.KeyBurst([right]);
            WaitForSelection(exampleSelection, middleLabel);
            Wait(() => IsFocused(middle), $"Right arrow focus in the {name} RadioGroup");
            checks.Add($"X11 Right selects and focuses the middle {name} option.");

            peer.KeyBurst([left]);
            WaitForSelection(exampleSelection, firstLabel);
            Wait(() => IsFocused(first), $"Left arrow focus in the {name} RadioGroup");
            checks.Add($"X11 Left restores the first {name} selection and focus.");
        }

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

        static void WaitForSelection(NativeObject selection, string label)
        {
            Wait(() =>
            {
                using var selected = SelectedChild(selection.Value, 0);
                return SelectionCount(selection.Value) == 1 && selected is not null && Name(selected.Value) == label;
            }, label + " RadioGroup selection");
        }

        static void Focus(NativeObject option)
        {
            Reveal(option);
            using var component = new NativeObject(atspi_accessible_get_component_iface(option.Value));
            nint error = 0;
            var focused = atspi_component_grab_focus(component.Value, &error);
            Error(ref error);
            if (focused == 0)
                throw new InvalidOperationException("Could not focus a RadioGroup option.");
        }

        static bool IsFocused(NativeObject option)
        {
            atspi_accessible_clear_cache(option.Value);
            using var states = new NativeObject(atspi_accessible_get_state_set(option.Value));
            return atspi_state_set_contains(states.Value, focusedState) != 0;
        }
    }
}
