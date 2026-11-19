using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckCard(nint automation, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var card = found["Native Card"];
        var floatingAction = found.ContainsKey("Floating native selection");
        var button = found["Card child action"];
        var input = found["Card child input"];
        var identity = Text(card, 30011);
        var inputIdentity = Text(input, 30011);
        static double Coordinate(nint element, int index)
        {
            UiaVariant rectangle = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30001, &rectangle));
            try
            {
                if (rectangle.Type != (0x2000 | 5)) throw new InvalidOperationException("Card bounds must be a double SAFEARRAY.");
                Ok(SafeArrayAccessData(rectangle.Data.Pointer, out var data));
                try { return ((double*)data)[index]; }
                finally { Ok(SafeArrayUnaccessData(rectangle.Data.Pointer)); }
            }
            finally { UiaNative.VariantClear(ref rectangle); }
        }
        static double Width(nint element) => Coordinate(element, 2);
        var physicalScale = Width(card) / 260;
        // UIA core truncates the observed physical X and width independently; the host checks exact logical edges.
        var mediumEdgeError = Coordinate(card, 0) + Width(card) - 12 * physicalScale - Coordinate(button, 0) - Width(button);
        check(mediumEdgeError is >= 0 and < 2,
            $"external Card Footer action reaches the actual medium body padding edge : cardX={Coordinate(card, 0)},cardWidth={Width(card)},buttonX={Coordinate(button, 0)},buttonWidth={Width(button)},scale={physicalScale}");
        check(ControlType(card) == 50026 && Text(card, 30012) == "Duxel.Card", "external passive Card is a real native UIA Group");
        check(!Boolean(card, 30009) && !Boolean(card, 30031) && !Boolean(card, 30043),
            "external passive Card root has no keyboard stop Invoke or editable Value pattern");
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        ReadPropertyChanges(automation, card, 1, owned, [30005], sink =>
        {
            Invoke("Rename card");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Card"),
                "external Card emits its committed renamed group event");
        });
        Invoke("Enable card focus");
        WaitUntil(() => Boolean(card, 30009));
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(card, 3))(card));
        WaitUntil(() => Boolean(card, 30008));
        check(Boolean(card, 30009) && Boolean(card, 30008) && !Boolean(card, 30031),
            "external Card SetFocus targets its real group without adding an Invoke pattern");
        Invoke("Disable card root");
        WaitUntil(() => !Boolean(card, 30010) && !Boolean(card, 30009));
        check(Boolean(button, 30010) && Boolean(input, 30010) && Boolean(input, 30009),
            "external disabled Card root preserves enabled focusable owned children");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(input, 3))(input));
        WaitUntil(() => Boolean(input, 30008));
        check(Boolean(input, 30008) && !Boolean(card, 30008),
            "external SetFocus enters an enabled input inside the disabled Card root");
        check(Boolean(found["Header info"], 30010) && Boolean(found["Header info"], 30031),
            "external disabled Card root preserves its real enabled Header action provider");
        Invoke("Header info");
        Invoke("Card child action");
        check(Boolean(button, 30031), "external Card child retains its native Invoke pattern");
        var value = Pattern(input, 10002);
        owned.Add(value);
        var text = Marshal.StringToBSTR("Edited by native client");
        try { Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, text)); }
        finally { Marshal.FreeBSTR(text); }
        check(Text(input, 30045) == "Edited by native client", "external Card child Value pattern edits the actual binding");
        Invoke("Enable card root");
        WaitUntil(() => Boolean(card, 30010) && Boolean(card, 30009));
        check(Text(card, 30011) == identity && Text(input, 30011) == inputIdentity,
            "external Card focus and disabled transitions preserve native provider identities");
        Invoke("Enable card action");
        WaitUntil(() => Boolean(card, 30031));
        check(ControlType(card) == 50026 && Text(card, 30012) == "Duxel.Card",
            "external actionable Card preserves Group semantics while exposing Invoke");
        var cardAction = Pattern(card, 10000);
        try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(cardAction, 3))(cardAction)); }
        finally { Marshal.Release(cardAction); }
        Invoke("Enable card selection");
        WaitUntil(() => !Boolean(card, 30009));
        nint condition = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
        owned.Add(condition);
        nint elements = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(card, 6))(card, 2, condition, &elements));
        nint checkbox = 0;
        try
        {
            int count = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
            for (var index = 0; index < count; index++)
            {
                nint item = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, index, &item));
                if (ControlType(item) == 50002) { checkbox = item; owned.Add(item); break; }
                Marshal.Release(item);
            }
        }
        finally { Marshal.Release(elements); }
        if (checkbox == 0) throw new InvalidOperationException("Selectable Card native child checkbox is missing.");
        var checkboxId = Text(checkbox, 30011);
        check(Text(checkbox, 30005) == (floatingAction ? "Floating native selection" : "Renamed Native Card") && Boolean(checkbox, 30009) && Boolean(checkbox, 30041)
            && !Boolean(card, 30041), "external selectable Card owns a named native Checkbox Toggle provider while its Group stays nonfocusable");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(checkbox, 3))(checkbox));
        WaitUntil(() => Boolean(checkbox, 30008));
        check(!Boolean(card, 30008) && Text(card, 30011) == identity && Text(input, 30011) == inputIdentity,
            "external Card selection focus targets its checkbox without replacing existing group or content providers");
        if (floatingAction)
            check(Text(checkbox, 30005) == "Floating native selection" && Boolean(checkbox, 30009),
                "external selectable Card uses its actual focusable floating checkbox without a hidden proxy");
        var toggle = Pattern(checkbox, 10015);
        owned.Add(toggle);
        int ToggleState()
        {
            int state = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(toggle, 4))(toggle, &state));
            return state;
        }
        ReadPropertyChanges(automation, checkbox, 1, owned, [30086], properties =>
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle));
            check(properties.Wait() == new TooltipNativeProperty(30086, checkboxId, 1) && ToggleState() == 1,
                "external Card Toggle publishes the committed selected integer state on its actual checkbox");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle));
            check(properties.Wait() == new TooltipNativeProperty(30086, checkboxId, 0) && ToggleState() == 0,
                "external Card Toggle clears its real bound selection with a committed off event");
        });
        Invoke("Disable card root");
        if (floatingAction)
        {
            WaitUntil(() => !Boolean(card, 30010));
            check(ToggleState() == 0 && Boolean(checkbox, 30010) && Boolean(input, 30010),
                "external disabled Card leaves caller-owned floating checkbox and content enabled");
        }
        else
        {
            WaitUntil(() => !Boolean(checkbox, 30010));
            check(ToggleState() == 0 && ((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle) == UiaNative.Disabled
                && Boolean(input, 30010), "external disabled Card checkbox keeps readable selection and rejects Toggle while its child input stays enabled");
        }
        Invoke("Enable card root");
        WaitUntil(() => Boolean(checkbox, 30010));
        var initialWidth = Width(card);
        Invoke("Restyle card");
        var deadline = Environment.TickCount64 + 5000;
        while (Math.Abs(Width(card) - initialWidth * 240 / 260) > .1)
        { if (Environment.TickCount64 >= deadline) throw new TimeoutException("Card resized bounds did not commit."); Thread.Sleep(10); }
        check(Text(card, 30011) == identity && Text(input, 30011) == inputIdentity && Text(input, 30045) == "Edited by native client",
            "external Card restyling preserves group and child provider identities and edited state");
        var smallEdgeError = Coordinate(card, 0) + Width(card) - 8 * physicalScale - Coordinate(button, 0) - Width(button);
        check(smallEdgeError is >= 0 and < 2,
            "external Card Footer action follows the actual small body padding after restyling");
        Invoke("Hide card");
        deadline = Environment.TickCount64 + 5000;
        while (!TooltipElementUnavailable(card))
        { if (Environment.TickCount64 >= deadline) throw new TimeoutException("Card provider was not removed."); Thread.Sleep(10); }
        check(TooltipElementUnavailable(card) && TooltipElementUnavailable(input), "external omitted Card invalidates both group and owned child providers");
        check(TooltipElementUnavailable(checkbox) && ((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle) == UiaNative.Unavailable,
            "external omitted Card invalidates its actual selection checkbox and retained Toggle action");
        Invoke("Finish");

        static void WaitUntil(Func<bool> condition)
        {
            var deadline = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= deadline) throw new TimeoutException("Card native focus state did not commit.");
                Thread.Sleep(10);
            }
        }
    }
}
