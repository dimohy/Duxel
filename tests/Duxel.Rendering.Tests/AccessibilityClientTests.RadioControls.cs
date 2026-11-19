using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckRadioControls(nint automation, nint root, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var group = found["Native radio choice"];
        var alpha = found["Radio alpha"];
        var beta = found["Radio beta"];
        var groupPattern = Pattern(group, 10001);
        var alphaPattern = Pattern(alpha, 10010);
        var betaPattern = Pattern(beta, 10010);
        owned.Add(groupPattern); owned.Add(alphaPattern); owned.Add(betaPattern);
        int IsSelected(nint pattern)
        {
            var selected = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(pattern, 6))(pattern, &selected));
            return selected;
        }
        int Select(nint pattern) => ((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern);
        int SelectedCount()
        {
            nint array = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(groupPattern, 3))(groupPattern, &array));
            try
            {
                var count = -1;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(array, 3))(array, &count));
                return count;
            }
            finally { Marshal.Release(array); }
        }
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        check(Text(group, 30012) == "Duxel.RadioGroup" && Text(beta, 30012) == "Duxel.RadioButton" &&
            Boolean(group, 30037) && Boolean(beta, 30036) && !Boolean(beta, 30041) && !Boolean(beta, 30031),
            "external actual RadioGroup exposes Selection and RadioButton SelectionItem without Toggle or Invoke");
        var multiple = -1;
        var required = -1;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(groupPattern, 4))(groupPattern, &multiple));
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(groupPattern, 5))(groupPattern, &required));
        check(multiple == 0 && required == 1 && SelectedCount() == 0 && IsSelected(alphaPattern) == 0,
            "initial null radio binding remains empty while exclusive required-selection policy is exposed");
        nint container = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(betaPattern, 7))(betaPattern, &container));
        try { check(Text(container, 30011) == Text(group, 30011), "external radio SelectionContainer resolves its real semantic owner"); }
        finally { Marshal.Release(container); }
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
            Ok(Select(betaPattern));
            var selected = sink.Wait(20012);
            check(selected.Name == "Radio beta" && selected.Role == 50013 && selected.Selected == true &&
                IsSelected(betaPattern) == 1 && SelectedCount() == 1, "external Select delivers a committed native RadioButton50013 selected event and exact group selection");
            check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(betaPattern, 5))(betaPattern) == UiaNative.InvalidOperation &&
                ((delegate* unmanaged[Stdcall]<nint, int>)Slot(alphaPattern, 4))(alphaPattern) == UiaNative.InvalidOperation,
                "external radio forbids selected-item removal and adding a second exclusive selection");
            ReadPropertyChanges(automation, root, 4, owned, [30079], properties =>
            {
                Ok(Select(alphaPattern));
                var changed = new[] { properties.Wait(), properties.Wait() }.ToHashSet();
                check(changed.SetEquals(new[]
                {
                    new TooltipNativeProperty(30079, Text(alpha, 30011), true),
                    new TooltipNativeProperty(30079, Text(beta, 30011), false)
                }) && IsSelected(alphaPattern) == 1 && IsSelected(betaPattern) == 0,
                    "external exclusive radio selection publishes both actual changed identities and final values");
            });
        }
        finally
        {
            if (registered) Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint, int>)Slot(automation, 33))(automation, 20012, root, handler));
            Marshal.Release(handler);
            GC.KeepAlive(sink);
        }
        var disabledPattern = Pattern(found["Radio disabled"], 10010);
        owned.Add(disabledPattern);
        check(Select(disabledPattern) == UiaNative.Disabled, "external disabled radio option rejects selection");
        Invoke("Disable radio choice");
        WaitForSlider(() => !Boolean(beta, 30010));
        check(Select(betaPattern) == UiaNative.Disabled && IsSelected(alphaPattern) == 1, "external disabled RadioGroup keeps readable selection while rejecting mutation");
        Invoke("Remove radio choice");
        var getter = 0;
        WaitForSlider(() =>
        {
            var retained = -1;
            getter = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(betaPattern, 6))(betaPattern, &retained);
            return getter == UiaNative.Unavailable;
        });
        check(getter == UiaNative.Unavailable && Select(betaPattern) == UiaNative.Unavailable,
            "retained external RadioButton getter and Select return exact unavailable after real group removal");
        Invoke("Finish");
    }
}
