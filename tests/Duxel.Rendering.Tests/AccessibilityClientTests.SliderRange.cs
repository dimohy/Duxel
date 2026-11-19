using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckSliderRange(nint automation, nint root, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        if (found.ContainsKey("Legacy continuous"))
        {
            CheckLegacySliderRange(automation, found, owned, check);
            return;
        }
        check(found.ContainsKey("Native range") && found.ContainsKey("Disabled range"), "external UIA discovers actual Slider range controls");
        var element = found["Native range"];
        var pattern = Pattern(element, 10003);
        var disabledPattern = Pattern(found["Disabled range"], 10003);
        owned.Add(pattern); owned.Add(disabledPattern);
        double Read(int slot)
        {
            double value = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(pattern, slot))(pattern, &value));
            return value;
        }
        int Set(nint target, double value) => ((delegate* unmanaged[Stdcall]<nint, double, int>)Slot(target, 3))(target, value);
        void Invoke(nint target)
        {
            var invoke = Pattern(target, 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(invoke, 3))(invoke)); }
            finally { Marshal.Release(invoke); }
        }
        int readOnly = -1;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(pattern, 5))(pattern, &readOnly));
        check(Boolean(element, 30033) && Text(element, 30012) == "Duxel.Slider" && Read(4) == 35 && readOnly == 0 &&
            Read(6) == 110 && Read(7) == 10 && Read(8) == 10 && Read(9) == 5,
            "external native RangeValue client reads correct value, bounds, changes and editability through every ABI getter");
        var source = Text(element, 30011);
        ReadPropertyChanges(automation, element, 1, owned, [30047], properties =>
        {
            Ok(Set(pattern, 63));
            check(Read(4) == 65 && properties.Wait() == new TooltipNativeProperty(30047, source, 65d),
                "external numeric SetValue snaps on the UI thread and reports its committed double-valued property event");
            Ok(Set(pattern, 65));
            Ok(Set(pattern, 75));
            check(Read(4) == 75 && properties.Wait() == new TooltipNativeProperty(30047, source, 75d),
                "external identical SetValue creates no duplicate event before the next real numeric change");
        });
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, 9d, 111d })
            check(Set(pattern, invalid) == UiaNative.InvalidArgument, "external numeric SetValue rejects nonfinite/out-of-range value : " + invalid);
        check(Set(disabledPattern, 30) == UiaNative.Disabled, "external disabled Slider returns UIA_E_ELEMENTNOTENABLED");
        ReadPropertyChanges(automation, element, 1, owned, [30049, 30050, 30051, 30052, 30023], properties =>
        {
            Invoke(found["Reconfigure range"]);
            var events = new Dictionary<int, object>();
            for (var index = 0; index < 5; index++)
            {
                var change = properties.Wait();
                if (change.SourceId != source) throw new InvalidOperationException("Range metadata event has the wrong source identity.");
                events.Add(change.PropertyId, change.Value);
            }
            check(Equals(events[30049], 0d) && Equals(events[30050], 200d) && Equals(events[30051], 20d) &&
                Equals(events[30052], 10d) && Equals(events[30023], 2) && Read(6) == 200 && Read(7) == 0 && Read(8) == 20 && Read(9) == 10,
                "external range reconfiguration reports new bounds, step, page change and vertical orientation on the same provider");
        });
        Ok(Set(pattern, 73));
        check(Read(4) == 70, "external reversed/vertical Slider mutates logical values using the current step");
        Invoke(found["Disable range"]);
        WaitForSlider(() => !Boolean(element, 30010));
        check(Set(pattern, 20) == UiaNative.Disabled && Read(4) == 70,
            "external live disabled state blocks mutation while numeric getters remain readable");
        Invoke(found["Remove range"]);
        WaitForSlider(() => Set(pattern, 20) == UiaNative.Unavailable);
        var retiredResult = int.MinValue;
        WaitForSlider(() =>
        {
            double retired = -1;
            var observed = ((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(pattern, 4))(pattern, &retired);
            if (retiredResult != observed) Console.WriteLine($"RetiredRangeGetter : HRESULT = 0x{observed:X8}, value = {retired}");
            retiredResult = observed;
            return observed == UiaNative.Unavailable;
        });
        check(retiredResult == UiaNative.Unavailable, "external retained RangeValue getter rejects the removed Slider");
        Invoke(found["Finish"]);
    }

    private static void CheckLegacySliderRange(nint automation, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var element = found["Legacy continuous"];
        var pattern = Pattern(element, 10003);
        var integerPattern = Pattern(found["Legacy integer"], 10003);
        var verticalPattern = Pattern(found["Legacy vertical"], 10003);
        owned.Add(pattern); owned.Add(integerPattern); owned.Add(verticalPattern);
        double Read(nint target, int slot = 4)
        {
            double value = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(target, slot))(target, &value));
            return value;
        }
        int Set(nint target, double value) => ((delegate* unmanaged[Stdcall]<nint, double, int>)Slot(target, 3))(target, value);
        void Invoke(string name)
        {
            var invoke = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(invoke, 3))(invoke)); }
            finally { Marshal.Release(invoke); }
        }
        UiaVariant orientation = default;
        var verticalElement = found["Legacy vertical"];
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(verticalElement, 10))(verticalElement, 30023, &orientation));
        var verticalOrientation = orientation.Type == 3 && orientation.Data.Integer == 2;
        Ok(UiaNative.VariantClear(ref orientation));
        check(Read(pattern) == 10 && Read(pattern, 6) == 100 && Read(pattern, 7) == 0 && Read(pattern, 9) == 5 &&
            Read(integerPattern, 9) == 1 && verticalOrientation,
            "external legacy RangeValue exposes actual bounds, keyboard change amounts and vertical orientation");
        var source = Text(element, 30011);
        ReadPropertyChanges(automation, element, 1, owned, [30047], properties =>
        {
            Ok(Set(pattern, 12.25));
            check(Read(pattern) == 12.25 && properties.Wait() == new TooltipNativeProperty(30047, source, 12.25d),
                "external legacy continuous mutation bypasses generated step snapping and emits the committed numeric event");
            Ok(Set(pattern, 12.25));
        });
        Ok(Set(integerPattern, 12.5)); Ok(Set(verticalPattern, 16.75));
        check(Read(integerPattern) == 12 && Read(verticalPattern) == 16.75,
            "external legacy integer and vertical controls apply their caller conversion rules");
        check(Set(pattern, double.NaN) == UiaNative.InvalidArgument && Set(pattern, 101) == UiaNative.InvalidArgument,
            "external legacy RangeValue rejects nonfinite and out-of-range changes");
        Invoke("Disable legacy");
        WaitForSlider(() => !Boolean(element, 30010));
        check(Set(pattern, 20) == UiaNative.Disabled && Read(pattern) == 12.25,
            "external legacy disabled state rejects mutations while preserving readable numeric state");
        Invoke("Remove legacy");
        var getterResult = 0;
        WaitForSlider(() =>
        {
            double value = -1;
            getterResult = ((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(pattern, 4))(pattern, &value);
            return getterResult == UiaNative.Unavailable;
        });
        check(getterResult == UiaNative.Unavailable && Set(pattern, 20) == UiaNative.Unavailable,
            "external held legacy getter and setter reject the removed provider with the strict unavailable HRESULT");
        Invoke("Finish");
    }

    private static void WaitForSlider(Func<bool> condition)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (condition()) return;
            Thread.Sleep(10);
        }
        throw new TimeoutException("The actual Slider state did not reach the requested condition.");
    }
}
