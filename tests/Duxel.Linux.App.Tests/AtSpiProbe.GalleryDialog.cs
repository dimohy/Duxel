internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryDialog(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint xid)
    {
        const int alertRole = 2, dialogRole = 16, buttonRole = 43, entryRole = 79;
        const int focusedState = 12, modalState = 16, defunctState = 6;
        const nuint enter = 0xff0d, escape = 0xff1b, tab = 0xff09;

        using var peer = new NativePeer(xid);
        using var alertOpener = required("경고 열기", window, buttonRole);
        Reveal(alertOpener);
        InvokeNamed(alertOpener, "click");
        using var alert = required("사용자 정보", window, alertRole);
        using var alertEntry = required("이름", alert.Value, entryRole);
        Wait(() => HasState(alert.Value, modalState) && HasState(alertEntry.Value, focusedState),
            "the Gallery Alert Dialog takes focus and exposes its modal alert role");
        checks.Add("The Gallery Alert Dialog exposes its modal alert role and focuses its name field.");

        using var save = required("저장", alert.Value, buttonRole);
        using var cancel = required("취소", alert.Value, buttonRole);
        peer.Pointer(6, 4, 4);
        peer.Pointer(4, 4, 4, 1);
        peer.Pointer(5, 4, 4, 1, 256);
        peer.KeyBurst([escape, tab]);
        Wait(() => !HasState(alert.Value, defunctState) && HasState(save.Value, focusedState),
            "the live Alert survives the backdrop click and Escape before Tab reaches its footer");
        checks.Add("An X11 backdrop click and Escape leave the Alert open, and Tab reaches its Save button.");

        peer.KeyBurst([tab]);
        Wait(() => HasState(cancel.Value, focusedState), "Tab reaches the Alert Cancel button");
        peer.KeyBurst([enter]);
        WaitForClose(alert, alertOpener, "Enter on Cancel retires the Alert and restores focus to its opener");
        checks.Add("X11 Enter on the Alert Cancel button dismisses it and restores focus to its opener.");

        using var modalOpener = required("모달 열기", window, buttonRole);
        InvokeNamed(modalOpener, "click");
        using var modal = required("사용자 정보", window, dialogRole);
        using var modalEntry = required("이름", modal.Value, entryRole);
        Wait(() => HasState(modalEntry.Value, focusedState), "the regular Modal receives focus");
        peer.KeyBurst([escape]);
        WaitForClose(modal, modalOpener, "Escape retires the regular Modal and restores focus to its opener");
        checks.Add("X11 Escape still dismisses the next regular Modal and restores focus to its opener.");

        void WaitForClose(NativeObject surface, NativeObject opener, string phase)
            => Wait(() => HasState(surface.Value, defunctState) && HasState(opener.Value, focusedState), phase);
    }
}
