using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckAccordion(nint automation, nint root, nint condition, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        var group = found["Native Accordion"];
        var first = found["Native section one"];
        var second = found["Native section two"];
        var disabled = found["Native disabled section"];
        var firstPattern = Pattern(first, 10005); owned.Add(firstPattern);
        var secondPattern = Pattern(second, 10005); owned.Add(secondPattern);
        var disabledPattern = Pattern(disabled, 10005); owned.Add(disabledPattern);
        int State(nint pattern)
        {
            var value = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(pattern, 5))(pattern, &value));
            return value;
        }
        int Expand(nint pattern) => ((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern);
        int Collapse(nint pattern) => ((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 4))(pattern);
        void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        nint FindNew(nint parent, string name)
        {
            nint array = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(parent, 6))(parent, 4, condition, &array));
            owned.Add(array);
            var count = 0; Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(array, 3))(array, &count));
            for (var i = 0; i < count; i++)
            {
                nint element = 0; Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(array, 4))(array, i, &element));
                owned.Add(element);
                if (Text(element, 30005) == name) return element;
            }
            throw new InvalidOperationException("Native Accordion content missing : " + name);
        }
        check(Text(group, 30012) == "Duxel.Accordion" && State(firstPattern) == 0 && State(secondPattern) == 0,
            "external Accordion starts with a real group and two collapsed expansion patterns");
        var identity = Text(first, 30011);
        ReadPropertyChanges(automation, first, 1, owned, [30070], sink =>
        {
            Ok(Expand(firstPattern));
            check(sink.Wait() == new TooltipNativeProperty(30070, identity, 1) && State(firstPattern) == 1,
                "native Accordion Expand delivers the committed expanded property event");
        });
        var panels = ControlledPanels(first, owned);
        var count = 0; Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(panels, 3))(panels, &count));
        if (count != 1) throw new InvalidOperationException("Accordion header must control exactly one live panel.");
        nint panel = 0; Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(panels, 4))(panels, 0, &panel)); owned.Add(panel);
        var label = RelatedProperty(panel, 30018, new("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), owned);
        check(Text(panel, 30012) == "Duxel.AccordionPanel" && Text(label, 30011) == identity,
            "external Accordion resolves its actual controlled panel and reciprocal LabeledBy provider");
        var editor = FindNew(panel, "Accordion native editor");
        var valuePattern = Pattern(editor, 10002); owned.Add(valuePattern);
        var text = Marshal.StringToBSTR("Native saved text");
        try { Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(valuePattern, 3))(valuePattern, text)); }
        finally { Marshal.FreeBSTR(text); }
        check(Text(editor, 30045) == "Native saved text", "native expanded Accordion body accepts bound text editing");
        Invoke(found["Deny final collapse"]);
        check(Collapse(firstPattern) == UiaNative.InvalidOperation && State(firstPattern) == 1,
            "native noncollapsible last panel returns InvalidOperation instead of reporting a false collapse");
        Invoke(found["Allow final collapse"]);
        Ok(Collapse(firstPattern));
        check(State(firstPattern) == 0 && TooltipElementUnavailable(panel),
            "native collapse removes its panel and invalidates retained panel providers");
        Ok(Expand(firstPattern));
        editor = FindNew(group, "Accordion native editor");
        check(Text(editor, 30045) == "Native saved text", "native Accordion reopens the same retained model with saved text");
        Invoke(found["Allow multiple panels"]);
        Ok(Expand(secondPattern));
        check(State(firstPattern) == 1 && State(secondPattern) == 1, "native multiple mode keeps both Accordion panels expanded");
        check(Expand(disabledPattern) == UiaNative.Disabled && !Boolean(disabled, 30010),
            "native disabled Accordion header rejects Expand through the standard UIA result");
        Ok(Collapse(firstPattern));
        check(State(firstPattern) == 0 && State(secondPattern) == 1,
            "native multiple collapse changes only its requested Accordion item");
        Invoke(FindNew(group, "Accordion body action"));
        Invoke(found["Finish"]);
    }
}
