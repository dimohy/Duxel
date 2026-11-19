using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckMarkdown(nint automation, nint window, nint root, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        using var dpiScope = new MarkdownDpiScope();
        static void Wait(Func<bool> condition, string description)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("Markdown native state did not commit: " + description);
                Thread.Sleep(5);
            }
        }
        static int Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            try
            {
                return ((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern);
            }
            finally { Marshal.Release(pattern); }
        }
        var first = found["Native first link"];
        var wrapped = found["Native wrapped link with enough text to occupy several visual lines"];
        var last = found["Native last link"];
        var counter = found["Native navigation count"];
        check(ControlType(first) == 50005 && Text(first, 30012) == "Duxel.Link" && Boolean(first, 30031),
            "external Markdown child exposes real Hyperlink control type and Invoke pattern");
        check(Boolean(first, 30043) && Boolean(first, 30046) && Text(first, 30045) == "https://example.test/first",
            "external Markdown URL is exposed through read-only Value");
        var value = Pattern(first, 10002);
        owned.Add(value);
        var replacement = Marshal.StringToBSTR("https://example.test/other");
        int result;
        try
        {
            result = ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, replacement);
        }
        finally { Marshal.FreeBSTR(replacement); }
        check(result < 0 && Text(first, 30045) == "https://example.test/first", "external Markdown rejects attempts to replace a link URL");
        ReadAutomationEvents(automation, root, owned, "Native first link", [20009], sink =>
        {
            Ok(Invoke(first));
            var invoked = sink.Wait(20009);
            check(invoked.ControlType == 50005 && invoked.Focusable && invoked.Id == Text(first, 30011),
                "external Markdown Invoked event caches the committed Hyperlink name, role and identity");
            Wait(() => Text(counter, 30045) == "Navigation count : 1", "first navigation");
            check(sink.TargetEvents == 1, "external Markdown publishes exactly one Invoked event per activation");
        });
        UiaVariant point = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(wrapped, 10))(wrapped, 30014, &point));
        try
        {
            if (point.Type != 0x2005)
                throw new InvalidOperationException("Markdown clickable point must be a double SAFEARRAY");
            Ok(SafeArrayGetLBound(point.Data.Pointer, 1, out var lower));
            Ok(SafeArrayGetUBound(point.Data.Pointer, 1, out var upper));
            check(lower == 0 && upper == 1, "external wrapped Markdown link exposes one exact two-coordinate clickable point");
            Ok(SafeArrayAccessData(point.Data.Pointer, out var pointer));
            int x, y;
            try
            {
                var origin = new NativePoint();
                if (ClientToScreen(window, ref origin) == 0)
                    throw new InvalidOperationException("Markdown client origin unavailable");
                var coordinates = (double*)pointer;
                x = (int)Math.Round(coordinates[0] - origin.X);
                y = (int)Math.Round(coordinates[1] - origin.Y);
            }
            finally { Ok(SafeArrayUnaccessData(point.Data.Pointer)); }
            var position = (nint)((x & 0xffff) | ((y & 0xffff) << 16));
            Console.WriteLine($"MARKDOWN_POST x={x} y={y} dpi={LabelGetDpiForWindow(window)}");
            if (LabelPostMessage(window, 0x0200, 0, position) == 0 || LabelPostMessage(window, 0x0201, 1, position) == 0)
                throw new InvalidOperationException("Markdown owned pointer down failed");
            Thread.Sleep(120);
            check(Text(counter, 30045) == "Navigation count : 1", "external held Markdown link does not navigate before release");
            if (LabelPostMessage(window, 0x0202, 0, position) == 0)
                throw new InvalidOperationException("Markdown owned pointer up failed");
        }
        finally { Ok(UiaNative.VariantClear(ref point)); }
        Wait(() => Text(counter, 30045) == "Navigation count : 2", "wrapped pointer navigation");
        check(Text(counter, 30045) == "Navigation count : 2", "external wrapped-link clickable point activates its actual displayed fragment once");
        check(Boolean(last, 30022) && Boolean(last, 30035), "external offscreen Markdown link exposes ScrollItem");
        var scroll = Pattern(last, 10017);
        owned.Add(scroll);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(scroll, 3))(scroll));
        Wait(() => !Boolean(last, 30022), "scroll item reveal");
        check(!Boolean(last, 30022), "external ScrollItem brings the real Markdown link into view");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(last, 3))(last));
        if (LabelPostMessage(window, 0x0100, 0x0D, 1) == 0 || LabelPostMessage(window, 0x0101, 0x0D, unchecked((nint)0xC0000001)) == 0)
            throw new InvalidOperationException("Markdown owned Enter post failed");
        Wait(() => Text(counter, 30045) == "Navigation count : 3", "keyboard navigation");
        check(Text(counter, 30045) == "Navigation count : 3", "external UIA focus and actual Windows Enter navigate the focused Markdown link");
        Ok(Invoke(found["Disable Markdown"]));
        Wait(() => !Boolean(last, 30010), "disabled link");
        check(Invoke(last) == UiaNative.Disabled, "external disabled Markdown link rejects Invoke");
        Ok(Invoke(found["Hide Markdown"]));
        Wait(() => TooltipElementUnavailable(last), "hidden link retirement");
        check(TooltipElementUnavailable(first) && TooltipElementUnavailable(last), "external omitted Markdown retires its retained link providers");
        Ok(Invoke(found["Finish"]));
    }

    private readonly struct MarkdownDpiScope : IDisposable
    {
        private readonly nint _previous;

        public MarkdownDpiScope()
        {
            _previous = MarkdownSetThreadDpiAwarenessContext(-4);
            if (_previous == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Markdown client DPI context could not be set.");
        }

        public void Dispose()
        {
            if (MarkdownSetThreadDpiAwarenessContext(_previous) == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Markdown client DPI context could not be restored.");
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SetThreadDpiAwarenessContext", SetLastError = true)]
    private static partial nint MarkdownSetThreadDpiAwarenessContext(nint context);
}
