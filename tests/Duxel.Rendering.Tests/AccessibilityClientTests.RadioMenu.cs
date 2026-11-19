using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckRadioMenu(nint automation, nint root, nint trigger, List<nint> owned,
        Action<bool, string> check, Func<string, int, nint> find, Action<nint> invoke)
    {
        invoke(trigger);
        var sort = find("Sort", 50026);
        var view = find("View", 50026);
        var one = find("One", 50011);
        var two = find("Two", 50011);
        var twoPattern = Pattern(two, 10010);
        owned.Add(twoPattern);
        var groupPattern = Pattern(sort, 10001);
        owned.Add(groupPattern);
        string Selected(nint group)
        {
            var pattern = Pattern(group, 10001);
            try
            {
                nint elements = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(pattern, 3))(pattern, &elements));
                try
                {
                    var count = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
                    if (count != 1) throw new InvalidOperationException("Radio group must report one selected item.");
                    nint element = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, 0, &element));
                    try { return Text(element, 30005); }
                    finally { Marshal.Release(element); }
                }
                finally { Marshal.Release(elements); }
            }
            finally { Marshal.Release(pattern); }
        }
        var multiple = -1;
        var required = -1;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(groupPattern, 4))(groupPattern, &multiple));
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(groupPattern, 5))(groupPattern, &required));
        check(multiple == 0 && required == 1 && Selected(sort) == "One" && Selected(view) == "Compact"
            && Boolean(one, 30036) && !Boolean(one, 30041),
            "external named radio groups expose independent single-selection patterns and no Toggle");
        nint container = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(twoPattern, 7))(twoPattern, &container));
        try { check(Text(container, 30005) == "Sort", "external radio SelectionContainer resolves its actual named group"); }
        finally { Marshal.Release(container); }
        check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(twoPattern, 4))(twoPattern) == UiaNative.InvalidOperation,
            "external AddToSelection rejects a second exclusive radio selection");
        var disabled = Pattern(find("Disabled", 50011), 10010);
        owned.Add(disabled);
        check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(disabled, 3))(disabled) == UiaNative.Disabled,
            "external disabled radio Select is rejected");
        var previousIdentity = RuntimeIdentity(two);
        nint cache = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 20))(automation, &cache));
        owned.Add(cache);
        foreach (var property in new[] { 30003, 30005, 30079 })
            Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, property));
        var sink = new TabSelectionSink();
        var handler = (nint)ComInterfaceMarshaller<ITooltipEventHandler>.ConvertToUnmanaged(sink);
        var registered = false;
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, int, nint, nint, int>)Slot(automation, 32))(
                automation, 20012, root, 4, cache, handler));
            registered = true;
            ReadPropertyChanges(automation, root, 4, owned, [30079], properties =>
            {
                var oneId = Text(one, 30011);
                var twoId = Text(two, 30011);
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(twoPattern, 3))(twoPattern));
                var selected = sink.Wait(20012);
                var changed = new[] { properties.Wait(), properties.Wait() }.ToHashSet();
                Console.WriteLine("RADIO_SELECTED : " + selected);
                foreach (var property in changed) Console.WriteLine("RADIO_PROPERTY : " + property);
                Console.WriteLine($"RADIO_EXPECTED_IDS : One = {oneId} / Two = {twoId}");
                check(selected.Name == "Two" && selected.Role == 50011 && selected.Selected == true
                    && changed.SetEquals(new[] { new TooltipNativeProperty(30079, oneId, false), new TooltipNativeProperty(30079, twoId, true) }),
                    "external radio selection delivers one cached final selected event and both original-source property changes");
            });
        }
        finally
        {
            if (registered) Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint, int>)Slot(automation, 33))(automation, 20012, root, handler));
            Marshal.Release(handler);
            GC.KeepAlive(sink);
        }
        var deadline = Environment.TickCount64 + 3000;
        while (!TooltipElementUnavailable(two) && Environment.TickCount64 < deadline) Thread.Sleep(5);
        check(TooltipElementUnavailable(two), "external radio selection dismisses and retires its menu item");
        var expander = Pattern(trigger, 10005);
        owned.Add(expander);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 3))(expander));
        var newTwo = find("Two", 50011);
        var renewed = Pattern(newTwo, 10010);
        owned.Add(renewed);
        check(RuntimeIdentity(newTwo) != previousIdentity && Boolean(newTwo, 30079)
            && Selected(find("Sort", 50026)) == "Two" && Selected(find("View", 50026)) == "Compact",
            "external radio reopen preserves both bindings with fresh item identities");
        check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(renewed, 5))(renewed) == UiaNative.InvalidOperation,
            "external RemoveFromSelection cannot clear the selected radio");
        invoke(find("Finish", 50011));
    }
}
