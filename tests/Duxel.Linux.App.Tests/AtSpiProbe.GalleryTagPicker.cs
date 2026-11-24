internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryTagPicker(nint window, Func<string, nint, int?, NativeObject> required,
        Action<string, int> waitForRemoval, List<string> checks, nuint xid)
    {
        const int comboRole = 11, optionRole = 32, buttonRole = 43, entryRole = 79;
        const int expandedState = 10, focusedState = 12, showingState = 25;
        using var peer = new NativePeer(xid);
        using var picker = required("받는 사람", window, comboRole);
        using var editor = required("받는 사람", picker.Value, entryRole);
        using var action = required("선택 지우기", picker.Value, buttonRole);
        using var arrow = required("Show options", picker.Value, buttonRole);
        using var original = required("제거 디모이", window, buttonRole);
        Reveal(action);
        var field = ComponentBox(picker.Value);
        var input = ComponentBox(editor.Value);
        var button = ComponentBox(action.Value);
        var expand = ComponentBox(arrow.Value);
        if (!HasState(action.Value, showingState) || button.Width <= 0 || button.Height <= 0
            || input.X + input.Width > button.X || button.X + button.Width > expand.X
            || button.Y < field.Y || button.Y + button.Height > field.Y + field.Height)
            throw new InvalidOperationException("The TagPicker action is outside its allocated lane.");
        checks.Add("AT-SPI exposes the action inside the picker, between the input and the chevron.");

        Click(action);
        waitForRemoval("제거 디모이", buttonRole);
        if (HasState(picker.Value, expandedState))
            throw new InvalidOperationException("Clicking the secondary action opened suggestions.");
        checks.Add("X11 pointer input clears the tag and leaves suggestions closed.");

        Click(arrow);
        using var choice = required("김민수", window, optionRole);
        Wait(() => HasState(picker.Value, expandedState) && HasState(choice.Value, showingState), "the open TagPicker suggestions");
        Click(choice);
        using var selected = required("제거 김민수", window, buttonRole);
        Wait(() => !HasState(picker.Value, expandedState), "the popup closed after pointer selection");
        checks.Add("The chevron and suggestion remain clickable after the action clears the selection.");

        using var component = new NativeObject(atspi_accessible_get_component_iface(editor.Value));
        nint error = 0;
        var focused = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (focused == 0)
            throw new InvalidOperationException("The TagPicker editor refused AT-SPI focus.");
        Wait(() => HasState(editor.Value, focusedState), "the focused TagPicker editor");
        peer.KeyBurst([0xff09]);
        Wait(() => HasState(action.Value, focusedState), "the focused secondary action");
        peer.KeyBurst([0xff0d]);
        waitForRemoval("제거 김민수", buttonRole);
        checks.Add("X11 Tab enters the action from the editor, and Enter clears the selected tag.");

        void Click(NativeObject element)
        {
            var bounds = ComponentBox(element.Value);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("The TagPicker click target has no visible bounds.");
            var x = bounds.X + bounds.Width / 2;
            var y = bounds.Y + bounds.Height / 2;
            peer.Pointer(6, x, y);
            peer.Pointer(4, x, y, 1);
            peer.Pointer(5, x, y, 1, 256);
        }
    }
}
