internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryTreeActions(nuint xid, nint window, nint grid, NativeObject open,
        Func<string, nint, int?, NativeObject> required, List<string> checks, string output)
    {
        const int rowRole = 90;
        const int focused = 12;
        const nuint f2 = 0xffbf;
        const nuint enter = 0xff0d;
        const nuint escape = 0xff1b;

        using var report = required("보고서", grid, rowRole);
        Reveal(report);
        using var component = new NativeObject(atspi_accessible_get_component_iface(report.Value));
        nint error = 0;
        var accepted = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (accepted == 0)
            throw new InvalidOperationException("The published TreeGrid row did not accept focus.");
        Wait(() => HasFocus(report) && !HasFocus(open), "TreeGrid row focus before entering its action");

        using var peer = new NativePeer(xid);
        peer.KeyBurst([f2]);
        Wait(() => HasFocus(open) && !HasFocus(report), "F2 moving focus to the report button");
        using var beforeEnter = required("보고서를 연 횟수 : 1", window, null);
        checks.Add("X11 F2 moves focus to the report button without invoking it.");
        CaptureGalleryPair(peer, output, "TreeGridActionFocused", checks);

        peer.KeyBurst([enter]);
        using var afterEnter = required("보고서를 연 횟수 : 2", window, null);
        Wait(() => HasFocus(open), "focus staying on the report button after Enter");
        checks.Add("X11 Enter invokes the focused report button exactly once.");

        peer.KeyBurst([escape]);
        Wait(() => HasFocus(report) && !HasFocus(open), "Escape restoring focus to the report row");
        using var afterEscape = required("보고서를 연 횟수 : 2", window, null);
        checks.Add("X11 Escape returns focus to the report row without invoking its button.");
        CaptureGalleryPair(peer, output, "TreeGridRowFocused", checks);

        static bool HasFocus(NativeObject target)
        {
            atspi_accessible_clear_cache(target.Value);
            using var states = new NativeObject(atspi_accessible_get_state_set(target.Value));
            return atspi_state_set_contains(states.Value, focused) != 0;
        }
    }
}
