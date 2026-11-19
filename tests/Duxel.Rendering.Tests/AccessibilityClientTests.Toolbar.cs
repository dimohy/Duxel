using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckToolbar(nint window, Dictionary<string, nint> found, Dictionary<string, nint> labels, Action<bool, string> check)
    {
        static int Integer(nint element, int property)
        {
            UiaVariant value = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, property, &value));
            try
            {
                if (value.Type != 3)
                    throw new InvalidOperationException("Expected native integer property");
                return value.Data.Integer;
            }
            finally { UiaNative.VariantClear(ref value); }
        }
        static void Wait(Func<bool> condition)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("Toolbar native transition did not commit");
                Thread.Sleep(5);
            }
        }
        void Invoke(string name)
        {
            var p = Pattern(found[name], 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(p, 3))(p));
            }
            finally { Marshal.Release(p); }
        }
        void Key(uint key)
        {
            if (LabelPostMessage(window, 0x100, key, 1) == 0 || LabelPostMessage(window, 0x101, key, unchecked((nint)0xc0000001)) == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned Toolbar key post failed");
        }
        var toolbar = found["Native toolbar"];
        var first = found["Native first"];
        var toggle = found["Native toggle"];
        var last = found["Native last"];
        var identity = Text(toolbar, 30011);
        check(ControlType(toolbar) == 50021 && Text(toolbar, 30012) == "Duxel.Toolbar" && Text(toolbar, 30101) == "toolbar" && Integer(toolbar, 30023) == 1,
            "external Toolbar exposes native Toolbar semantics and horizontal orientation");
        check(!Boolean(toolbar, 30009) && !Boolean(toolbar, 30031) && !Boolean(toolbar, 30043), "external Toolbar root has no invented action/value/focus patterns");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(first, 3))(first));
        Wait(() => Boolean(first, 30008));
        Key(0x27);
        Wait(() => Boolean(toggle, 30008));
        check(Boolean(toggle, 30008), "external Toolbar receives actual Windows Right key and moves to child toggle");
        Key(0x20);
        Wait(() => Integer(toggle, 30086) == 1);
        check(Integer(toggle, 30086) == 1, "external Toolbar Windows Space changes real bound Toggle state");
        Key(0x23);
        Wait(() => Boolean(last, 30008));
        Key(0x27);
        Wait(() => Boolean(first, 30008));
        check(Boolean(first, 30008), "external Toolbar End and circular Right traverse real children");
        Invoke("Native first");
        Wait(() => Text(labels["Toolbar count : 0"], 30005) == "Toolbar count : 1");
        check(Text(labels["Toolbar count : 0"], 30005) == "Toolbar count : 1", "external Toolbar child Invoke changes actual command model");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(first, 3))(first));
        Wait(() => Boolean(first, 30008));
        Invoke("Native first");
        Wait(() => !Boolean(first, 30010) && Boolean(toggle, 30008));
        check(Boolean(toggle, 30008), "external Toolbar disabled focused child repairs focus to actual enabled child");
        Invoke("Vertical toolbar");
        Wait(() => Integer(toolbar, 30023) == 2);
        check(Integer(toolbar, 30023) == 2 && Text(toolbar, 30011) == identity, "external Toolbar orientation changes retain provider identity");
        Invoke("Hide toolbar");
        Wait(() => TooltipElementUnavailable(toolbar));
        check(TooltipElementUnavailable(toolbar) && TooltipElementUnavailable(first), "external omitted Toolbar retires root and child providers");
        Invoke("Finish");
    }
}
