using System.ComponentModel;
using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckLabels(nint window, nint automation, nint root, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        static void WaitUntil(Func<bool> predicate,
            [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(predicate))] string? description = null)
        {
            var deadline = Environment.TickCount64 + 5000;
            while (!predicate())
            {
                if (Environment.TickCount64 >= deadline) throw new TimeoutException("Label UIA condition : " + description);
                Thread.Sleep(10);
            }
        }
        var input = found["Native label target"];
        var identity = Text(input, 30011);
        var pointer = found["Pointer presses : 0"];
        var labelElement = RelatedProperty(input, 30018, new("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), owned);
        var labelIdentity = Text(labelElement, 30011);
        check(Text(labelElement, 30012) == "Duxel.Label" && Text(labelElement, 30005) == "Native label target"
            && !Boolean(labelElement, 30009) && !Boolean(labelElement, 30017),
            "external LabeledBy resolves the actual nonfocusable Label text provider without duplicate content view");
        check(Boolean(input, 30025) && Boolean(input, 30009) && Boolean(input, 30043),
            "external standalone Label supplies required focusable Edit metadata and Value support");
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        void Anchor()
        {
            var element = found["Label focus anchor"];
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(element, 3))(element));
            WaitUntil(() => Boolean(element, 30008));
        }
        void ClickLabel()
        {
            var before = Text(pointer, 30005);
            var dpi = LabelGetDpiForWindow(window);
            if (dpi == 0) throw new InvalidOperationException("Owned Label window DPI is unavailable.");
            var coordinate = checked((int)Math.Round(30 * dpi / 96d));
            var position = (nint)((coordinate & 0xffff) | (coordinate << 16));
            Post(0x0200, 0);
            Post(0x0201, 1);
            WaitUntil(() => Text(pointer, 30005) != before);
            Post(0x0202, 0);
            void Post(uint message, nuint buttons)
            {
                if (LabelPostMessage(window, message, buttons, position) == 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "Owned Label pointer post failed.");
            }
        }
        Anchor();
        ClickLabel();
        WaitUntil(() => Boolean(input, 30008));
        check(Boolean(input, 30008), "native Label pointer press moves actual keyboard focus to the associated Edit");
        ReadPropertyChanges(automation, input, 1, owned, [30005], sink =>
        {
            Invoke("Rename label");
            var change = sink.Wait();
            check(change == new TooltipNativeProperty(30005, identity, "Renamed native label"),
                "external Label rename emits the committed native Name property event");
            check(Text(input, 30005) == "Renamed native label" && !Boolean(input, 30025) && Text(input, 30011) == identity,
                "Label rename and required change preserve native automation identity");
        });
        var renamedLabel = RelatedProperty(input, 30018, new("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), owned);
        check(Text(renamedLabel, 30011) == labelIdentity && Text(renamedLabel, 30005) == "Renamed native label",
            "external Label rename keeps the same live LabeledBy provider identity");
        var value = Pattern(input, 10002);
        owned.Add(value);
        var text = Marshal.StringToBSTR("Native edited value");
        try { Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, text)); }
        finally { Marshal.FreeBSTR(text); }
        WaitUntil(() => Text(input, 30045) == "Native edited value");
        check(Text(input, 30045) == "Native edited value", "renamed native input retains its working Value pattern");
        Invoke("Disable label");
        Anchor();
        ClickLabel();
        check(!Boolean(input, 30008) && Boolean(input, 30010), "disabled Label cannot focus its still-enabled native target");
        ReadRelationshipChanges(automation, root, owned, [30018], sink =>
        {
            Invoke("Detach label");
            var change = WaitRelationship(sink, owned);
            check(change.Property == 30018 && change.Source == identity && change.TargetId is null,
                "external Label detach emits a native LabeledBy removal event for the committed Edit");
        });
        WaitUntil(() => Text(input, 30005) == "Original label source");
        check(Text(input, 30011) == identity && !Boolean(input, 30025), "Label detach restores native input name and preserves identity");
        Anchor();
        ClickLabel();
        check(!Boolean(input, 30008), "detached Label pointer press cannot focus the old native target");
        Invoke("Disable label target");
        WaitUntil(() => !Boolean(input, 30010));
        check(!Boolean(input, 30009), "disabled Label target is not natively keyboard focusable");
        Anchor();
        ClickLabel();
        Invoke("Enable label target");
        WaitUntil(() => Boolean(input, 30010));
        check(!Boolean(input, 30008), "re-enabling a native target does not revive a consumed Label focus request");
        Invoke("Finish");
    }

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    private static partial int LabelPostMessage(nint window, uint message, nuint wParam, nint lParam);
    [LibraryImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static partial uint LabelGetDpiForWindow(nint window);
}
