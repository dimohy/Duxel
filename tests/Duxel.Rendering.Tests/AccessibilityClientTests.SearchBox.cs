using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckSearchBox(nint automation, nint window, nint root, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        static void Wait(Func<bool> condition)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("Search native state did not commit");
                Thread.Sleep(5);
            }
        }
        void Invoke(nint element)
        {
            var p = Pattern(element, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(p, 3))(p));
            }
            finally { Marshal.Release(p); }
        }
        int SetValue(nint element, string text)
        {
            var p = Pattern(element, 10002);
            var value = Marshal.StringToBSTR(text);
            try
            {
                return ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(p, 3))(p, value);
            }
            finally { Marshal.FreeBSTR(value); Marshal.Release(p); }
        }
        nint ClearButton()
        {
            nint condition = 0, elements = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
            owned.Add(condition);
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(root, 6))(root, 4, condition, &elements));
            owned.Add(elements);
            int count = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
            for (var i = 0; i < count; i++)
            {
                nint element = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                owned.Add(element);
                if (Text(element, 30005) == "Clear search")
                    return element;
            }
            return 0;
        }
        var search = found["Native search"];
        var identity = Text(search, 30011);
        check(ControlType(search) == 50004 && Text(search, 30012) == "Duxel.SearchBox" && Text(search, 30101) == "searchbox" && Boolean(search, 30043), "external SearchBox maps search semantics to native editable Value provider");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(search, 3))(search));
        nint clear = 0;
        Wait(() => (clear = ClearButton()) != 0);
        check(!Boolean(clear, 30009) && Boolean(clear, 30031), "external Search clear exposes Invoke without an extra keyboard stop");
        Invoke(clear);
        Wait(() => Text(search, 30045) == "");
        check(Boolean(search, 30008), "external Search clear restores real native editor focus");
        Ok(SetValue(search, "Held 한글"));
        Wait(() => Text(search, 30045) == "Held 한글");
        clear = ClearButton();
        var point = ClientCenter(window, clear);
        var position = (nint)((point.X & 0xffff) | ((point.Y & 0xffff) << 16));
        if (LabelPostMessage(window, 0x0200, 0, position) == 0 || LabelPostMessage(window, 0x0201, 1, position) == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned SearchBox pointer down failed");
        Thread.Sleep(120);
        check(Boolean(search, 30008) && Text(search, 30045) == "Held 한글" && !TooltipElementUnavailable(clear),
            "external held mouse clear retains native editor focus and its live action");
        if (LabelPostMessage(window, 0x0202, 0, position) == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned SearchBox pointer up failed");
        Wait(() => Text(search, 30045) == "");
        check(Boolean(search, 30008), "external real mouse press-hold-release clears the accepted native search value");
        if (LabelPostMessage(window, 0x0102, 0xAC00, 1) == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned SearchBox character post failed");
        Wait(() => Text(search, 30045) == "가");
        check(Text(search, 30045) == "가", "external Windows Korean character input resumes after held clear");
        ReadPropertyChanges(automation, search, 1, owned, [30045], sink =>
        {
            Ok(SetValue(search, "한글 search"));
            check(sink.Wait() == new TooltipNativeProperty(30045, identity, "한글 search"), "external Search emits the actual Korean Value property change");
        });
        Invoke(found["Reject search"]);
        Ok(SetValue(search, "rejected"));
        check(Text(search, 30045) == "한글 search", "external Search rejected SetValue preserves published accepted text");
        Invoke(found["Readonly search"]);
        Wait(() => Boolean(search, 30046));
        check(SetValue(search, "blocked") < 0 && Text(search, 30045) == "한글 search" && Text(search, 30011) == identity,
            "external Search readonly rejects native writes and preserves provider identity");
        Invoke(found["Finish"]);
    }
}
