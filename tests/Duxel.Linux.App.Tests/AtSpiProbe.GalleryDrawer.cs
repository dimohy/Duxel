internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryDrawer(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint xid)
    {
        const int dialogRole = 16;
        const int buttonRole = 43;
        const int entryRole = 79;
        const int focusedState = 12;
        const int modalState = 16;
        const int defunctState = 6;
        const nuint enter = 0xff0d;
        const nuint escape = 0xff1b;
        const string edited = "Linux 패널 편집값";

        using var peer = new NativePeer(xid);
        using var opener = required("보조 패널 열기", window, buttonRole);
        Reveal(opener);
        using var component = new NativeObject(atspi_accessible_get_component_iface(opener.Value));
        nint error = 0;
        var accepted = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (accepted == 0)
            throw new InvalidOperationException("AT-SPI could not focus the Drawer opener.");
        Wait(() => HasState(opener.Value, focusedState), "Drawer opener focus");

        peer.KeyBurst([enter]);
        using var first = required("보조 작업", window, dialogRole);
        if (!HasState(first.Value, modalState) || Description(first.Value) != "편집 내용을 유지하는 보조 작업 패널")
            throw new InvalidOperationException("AT-SPI did not report the modal Drawer description and state.");
        Wait(() =>
        {
            using var focused = Find(first.Value, child => HasState(child, focusedState));
            return focused is not null;
        }, "initial focus enters the modal Drawer");
        checks.Add("X11 Enter opens the modal Drawer with its accessible description and transfers focus inside.");

        using var memo = required("패널 메모", first.Value, entryRole);
        using var editor = new NativeObject(atspi_accessible_get_editable_text_iface(memo.Value));
        using var text = new NativeObject(atspi_accessible_get_text_iface(memo.Value));
        if (!SetText(editor.Value, edited))
            throw new InvalidOperationException("AT-SPI could not edit the Drawer memo.");
        Wait(() => ReadText(text.Value, 0, -1) == edited, "edited Drawer memo");
        checks.Add("The external client edits the open Drawer's memo.");

        peer.KeyBurst([escape]);
        WaitForClose(first);
        checks.Add("X11 Escape retires the modal Drawer and restores focus to the opener.");

        peer.KeyBurst([enter]);
        using var reopened = required("보조 작업", window, dialogRole);
        using var restored = required("패널 메모", reopened.Value, entryRole);
        using var restoredText = new NativeObject(atspi_accessible_get_text_iface(restored.Value));
        if (reopened.Value == first.Value || ReadText(restoredText.Value, 0, -1) != edited)
            throw new InvalidOperationException("Reopening must expose a live Drawer with the retained memo.");
        checks.Add("Reopening exposes a new Drawer object and retains the edited memo.");

        using var save = required("패널 저장", reopened.Value, buttonRole);
        InvokeNamed(save, "click");
        WaitForClose(reopened);
        using var saved = required("저장 횟수 : 1", window, null);
        checks.Add("The native Save action runs once, retires the Drawer and returns focus to the opener.");

        void WaitForClose(NativeObject drawer)
            => Wait(() => HasState(drawer.Value, defunctState) && HasState(opener.Value, focusedState),
                "Drawer retires and restores focus");
    }
}
