internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryDrawer(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, string output, nuint xid)
    {
        const int dialogRole = 16;
        const int buttonRole = 43;
        const int entryRole = 79;
        const int groupRole = 99;
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
        VerifyDrawerScrollSeparators(peer, first.Value, output, checks);
        using var editor = new NativeObject(atspi_accessible_get_editable_text_iface(memo.Value));
        using var text = new NativeObject(atspi_accessible_get_text_iface(memo.Value));
        using var clearMemo = required("메모 비우기", first.Value, buttonRole);
        if (string.IsNullOrEmpty(ReadText(text.Value, 0, -1)))
            throw new InvalidOperationException("The Drawer memo must contain text before the header command runs.");
        InvokeNamed(clearMemo, "click");
        Wait(() => !HasState(first.Value, defunctState) && ReadText(text.Value, 0, -1) == string.Empty,
            "header command clears the memo without closing the Drawer");
        checks.Add("The external client invokes the header button, which clears the memo and leaves the Drawer open.");
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

        using var direction = required("패널 오른쪽에서 왼쪽", window, null);
        InvokeNamed(direction, "toggle");
        InvokeNamed(opener, "click");
        using var mirrored = required("보조 작업", window, dialogRole);
        using var close = required("보조 패널 닫기", mirrored.Value, buttonRole);
        using var mirroredSave = required("패널 저장", mirrored.Value, buttonRole);
        Wait(() =>
        {
            var panel = Bounds(mirrored.Value);
            var center = panel.X + panel.Width / 2;
            return panel.X == 0 && panel.Width == 320
                && Bounds(close.Value).X < center && Bounds(mirroredSave.Value).X > center;
        }, "inherited RTL moves the End Drawer left and mirrors its close and save controls");
        InvokeNamed(close, "click");
        WaitForClose(mirrored);
        checks.Add("The Fluent provider's RTL direction moves the End Drawer to the left and mirrors its operable close and footer controls.");

        using var inlineToggle = required("인라인 패널 표시", window, null);
        Reveal(inlineToggle);
        using var inline = required("인라인 패널", window, groupRole);
        if (HasState(inline.Value, modalState))
            throw new InvalidOperationException("The inline Drawer must not make the page modal.");
        using var inlineMemo = required("인라인 메모", inline.Value, entryRole);
        using var inlineText = new NativeObject(atspi_accessible_get_text_iface(inlineMemo.Value));
        if (ReadText(inlineText.Value, 0, -1) != edited)
            throw new InvalidOperationException("The inline Drawer must display the retained memo.");
        checks.Add("The inline Drawer exposes a nonmodal group and reads the shared memo.");

        InvokeNamed(inlineToggle, "toggle");
        Wait(() => HasState(inline.Value, defunctState), "hidden inline Drawer retires");
        InvokeNamed(inlineToggle, "toggle");
        using var inlineReopened = required("인라인 패널", window, groupRole);
        using var inlineRestored = required("인라인 메모", inlineReopened.Value, entryRole);
        using var inlineRestoredText = new NativeObject(atspi_accessible_get_text_iface(inlineRestored.Value));
        if (ReadText(inlineRestoredText.Value, 0, -1) != edited)
            throw new InvalidOperationException("The inline Drawer must retain its memo after being hidden.");
        checks.Add("The page switch closes and reopens the inline Drawer and preserves its memo.");

        void WaitForClose(NativeObject drawer)
            => Wait(() => HasState(drawer.Value, defunctState) && HasState(opener.Value, focusedState),
                "Drawer retires and restores focus");
    }

    private static void VerifyDrawerScrollSeparators(NativePeer peer, nint drawer, string output, List<string> checks)
    {
        const int width = 1280, height = 900;
        const int panelWidth = 320;
        Wait(() =>
        {
            var bounds = Bounds(drawer);
            return bounds.X == width - panelWidth && bounds.Width == panelWidth;
        }, "the end-aligned Drawer reaches its settled extent");
        var panel = Bounds(drawer);
        using var body = Find(drawer, node => Name(node) == "Scroll view" && Bounds(node).Y > panel.Y + 35)
            ?? throw new InvalidOperationException("The Drawer body scroll viewport was not exposed.");
        var viewport = Bounds(body.Value);
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(output))!;
        if (viewport.X < 0 || viewport.Y <= 0 || viewport.Width < 32 || viewport.Height <= 0
            || viewport.X + viewport.Width > width || viewport.Y + viewport.Height >= height)
            throw new InvalidOperationException("The Drawer body viewport was outside the capture rectangle.");
        Wait(() => peer.CanCapture(width, height, out _), "the owned Drawer window capture rectangle");
        Capture("drawer-scroll-top", header : false, footer : true);
        checks.Add("The live Drawer shows only the footer separator at the top of its body.");
        Scroll(5);
        Capture("drawer-scroll-bottom", header : true, footer : false);
        checks.Add("X11 wheel input reaches the end of the Drawer body and changes the visible separator.");
        Scroll(4);
        Capture("drawer-scroll-return", header : false, footer : true);

        void Scroll(uint button)
        {
            int x = viewport.X + viewport.Width / 2, y = viewport.Y + viewport.Height / 2;
            peer.Pointer(6, x, y);
            for (var i = 0; i < 20; i++)
                peer.Pointer(4, x, y, button);
        }

        void Capture(string name, bool header, bool footer)
        {
            Wait(() =>
            {
                var matched = false;
                peer.Capture(Path.Combine(outputDirectory, name + ".ppm"), width, height, pixels =>
                    matched = Stroke(pixels, viewport.Y - 1) == header && Stroke(pixels, viewport.Y + viewport.Height) == footer);
                return matched;
            }, name + " separator pixels");
        }

        bool Stroke(byte[] pixels, int y)
        {
            for (var x = viewport.X + 4; x < viewport.X + viewport.Width - 20; x++)
            {
                var i = (y * width + x) * 3;
                var red = pixels[i];
                if (red is not (102 or 209) || pixels[i + 1] != red || pixels[i + 2] != red)
                    return false;
            }
            return true;
        }
    }

    static Rect Bounds(nint node)
    {
        using var component = new NativeObject(atspi_accessible_get_component_iface(node));
        nint error = 0;
        var pointer = atspi_component_get_extents(component.Value, 1, &error);
        try
        {
            Error(ref error);
            if (pointer == 0)
                throw new InvalidOperationException("AT-SPI did not provide the Drawer layout bounds.");
            return *(Rect*)pointer;
        }
        finally { if (pointer != 0) g_free(pointer); }
    }
}
