using System.Diagnostics;
using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryTooltip(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint? nativeWindow)
    {
        const int buttonRole = 43;
        const int tooltipRole = 64;
        const int defunctState = 6;
        const int focusableState = 11;
        const int hasTooltipState = 13;
        const int showingState = 25;
        const string caption = "코드에서 표시 여부를 직접 정하는 Tooltip입니다.";

        using var toggle = required("Tooltip 표시 전환", window, buttonRole);
        Reveal(toggle);
        using var target = required("제어 대상", window, buttonRole);
        using var label = required("세부 정보", window, buttonRole);
        using var watch = new EventWatch(tooltipLifecycle : true) { TooltipTarget = target.Value };

        if (Description(target.Value) != caption || !HasState(target.Value, hasTooltipState) || Count(target.Value) != 0)
            throw new InvalidOperationException("Hidden Tooltip must describe its target without a child object.");
        if (Description(label.Value).Length != 0 || !HasState(label.Value, hasTooltipState) || Count(label.Value) != 0)
            throw new InvalidOperationException("Tooltip in Label mode must provide the target name without duplicate description.");
        checks.Add("AT-SPI exposes hidden Tooltip descriptions and Label mode without a surface child.");

        using var first = Open(1);
        checks.Add("AT-SPI receives one child addition for the visible, nonfocusable ToolTip under its actual target.");
        Close(first, 2);
        checks.Add("AT-SPI receives one child removal and the old surface becomes defunct while target help remains.");

        using var second = Open(3);
        if (first.Value == second.Value)
            throw new InvalidOperationException("Reopening the Tooltip must publish a new native object.");
        Close(second, 4);
        checks.Add("Reopening gets a new AT-SPI object and both lifecycles deliver exactly one addition and removal.");

        if (nativeWindow is { } xid)
            VerifyAutomaticTooltip(window, required, checks, xid, watch);

        NativeObject Open(int eventCount)
        {
            InvokeNamed(toggle, "click");
            Wait(() => watch.TooltipChildren.Count >= eventCount, "Tooltip child addition");
            watch.Check();
            if (watch.TooltipChildren.Count != eventCount
                || watch.TooltipChildren[^1] != ("object:children-changed:add", 0) || ChildCount() != 1)
                throw new InvalidOperationException("The visible Tooltip must add exactly one child at position zero.");
            var surface = Child(target.Value, 0);
            try
            {
                if (Name(surface.Value) != caption || Role(surface.Value) != tooltipRole
                    || !HasState(surface.Value, showingState) || HasState(surface.Value, focusableState))
                    throw new InvalidOperationException("The Tooltip surface has invalid native name, role or state.");
                return surface;
            }
            catch
            {
                surface.Dispose();
                throw;
            }
        }

        void Close(NativeObject surface, int eventCount)
        {
            InvokeNamed(toggle, "click");
            Wait(() => watch.TooltipChildren.Count >= eventCount && HasState(surface.Value, defunctState),
                "Tooltip child removal and defunct state");
            watch.Check();
            if (watch.TooltipChildren.Count != eventCount
                || watch.TooltipChildren[^1] != ("object:children-changed:remove", 0) || ChildCount() != 0
                || Description(target.Value) != caption || !HasState(target.Value, hasTooltipState))
                throw new InvalidOperationException("Closing the Tooltip must remove one child and preserve the target help.");
        }

        int ChildCount()
        {
            atspi_accessible_clear_cache(target.Value);
            return Count(target.Value);
        }
    }

    private static void VerifyAutomaticTooltip(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, nuint xid, EventWatch watch)
    {
        const int buttonRole = 43;
        const int tooltipRole = 64;
        const int defunctState = 6;
        const int focusedState = 12;
        const nuint escape = 0xff1b;
        const nuint tab = 0xff09;
        const uint shiftMask = 1;

        using var target = required("설명 대상", window, buttonRole);
        Reveal(target);
        var description = Description(target.Value);
        watch.TooltipTarget = target.Value;
        watch.TooltipChildren.Clear();
        using var component = new NativeObject(atspi_accessible_get_component_iface(target.Value));
        nint error = 0;
        var focused = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (focused == 0)
            throw new InvalidOperationException("AT-SPI could not focus the Gallery Tooltip target.");

        using var peer = new NativePeer(xid);
        using var first = WaitForOpen(1);
        peer.KeyBurst([escape]);
        WaitForClose(first, 2);
        var suppressed = Stopwatch.StartNew();
        while (suppressed.ElapsedMilliseconds < 750)
        {
            Pump();
            Thread.Sleep(10);
        }
        watch.Check();
        if (!IsFocused() || watch.TooltipChildren.Count != 2 || ReadChildCount() != 0
            || Description(target.Value) != description)
            throw new InvalidOperationException("Escape must suppress the Tooltip while retaining focus and target help.");
        checks.Add("X11 Escape closes the automatic Tooltip without moving focus or immediately reopening it.");

        peer.KeyBurst([tab]);
        Wait(() => !IsFocused(), "Tab moves away from the automatic Tooltip target");
        peer.Key(true, tab, shiftMask);
        peer.Key(false, tab, shiftMask);
        using var second = WaitForOpen(3);
        if (first.Value == second.Value)
            throw new InvalidOperationException("Returning by keyboard must open a new ToolTip object.");
        peer.KeyBurst([escape]);
        WaitForClose(second, 4);
        checks.Add("X11 Tab and Shift+Tab renew the automatic Tooltip on keyboard return.");

        NativeObject WaitForOpen(int count)
        {
            Wait(() => watch.TooltipChildren.Count >= count && IsFocused(),
                "focused automatic Tooltip child");
            watch.Check();
            if (watch.TooltipChildren.Count != count || watch.TooltipChildren[^1] != ("object:children-changed:add", 0)
                || ReadChildCount() != 1)
                throw new InvalidOperationException("Keyboard focus must open exactly one ToolTip.");
            var surface = Child(target.Value, 0);
            if (Role(surface.Value) == tooltipRole && Name(surface.Value) == description)
                return surface;
            surface.Dispose();
            throw new InvalidOperationException("Keyboard-opened ToolTip must expose the target's help text.");
        }

        void WaitForClose(NativeObject surface, int count)
        {
            Wait(() => watch.TooltipChildren.Count >= count && HasState(surface.Value, defunctState),
                "X11 Escape retires the automatic Tooltip");
            watch.Check();
            if (watch.TooltipChildren.Count != count || watch.TooltipChildren[^1] != ("object:children-changed:remove", 0)
                || ReadChildCount() != 0)
                throw new InvalidOperationException("X11 Escape must remove exactly one ToolTip.");
        }

        int ReadChildCount()
        {
            atspi_accessible_clear_cache(target.Value);
            return Count(target.Value);
        }

        bool IsFocused()
        {
            atspi_accessible_clear_cache(target.Value);
            return HasState(target.Value, focusedState);
        }
    }

    private static bool HasState(nint element, int state)
    {
        using var states = new NativeObject(atspi_accessible_get_state_set(element));
        return atspi_state_set_contains(states.Value, state) != 0;
    }

    private static string Description(nint element)
    {
        nint error = 0;
        var description = atspi_accessible_get_description(element, &error);
        try
        {
            Error(ref error);
            return Marshal.PtrToStringUTF8(description) ?? throw new InvalidDataException("No accessible description.");
        }
        finally
        {
            if (description != 0)
                g_free(description);
        }
    }
}
