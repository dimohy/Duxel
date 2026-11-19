using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckCombobox(nint automation, nint windowRoot, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        static int Integer(nint element, int property)
        {
            UiaVariant value = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, property, &value));
            try
            {
                if (value.Type != 3)
                    throw new InvalidOperationException("Expected UIA integer property: " + property);
                return value.Data.Integer;
            }
            finally { UiaNative.VariantClear(ref value); }
        }
        static void Wait(Func<bool> condition)
        {
            var limit = Environment.TickCount64 + 10000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("Combobox native state did not settle.");
                Thread.Sleep(10);
            }
        }
        Dictionary<string, nint> Snapshot(nint owner)
        {
            nint condition = 0, elements = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
            owned.Add(condition);
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(owner, 6))(owner, 4, condition, &elements));
            owned.Add(elements);
            var count = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
            var result = new Dictionary<string, nint>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                nint element = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                owned.Add(element);
                var name = Text(element, 30005);
                if (name.Length > 0 && Text(element, 30012) != "Duxel.Label")
                {
                    if (ControlType(element) == 50011)
                        result[name] = element;
                    else
                        result.TryAdd(name, element);
                }
            }
            return result;
        }
        void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
            }
            finally { Marshal.Release(pattern); }
        }
        void Expand(nint element)
        {
            var expansion = Pattern(element, 10005);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expansion, 3))(expansion));
            }
            finally { Marshal.Release(expansion); }
        }
        void Select(nint element, int operation)
        {
            var pattern = Pattern(element, 10010);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, operation))(pattern));
            }
            finally { Marshal.Release(pattern); }
        }
        string[] Selected(nint element)
        {
            var pattern = Pattern(element, 10001);
            nint array = 0;
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(pattern, 3))(pattern, &array));
                var count = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(array, 3))(array, &count));
                var result = new string[count];
                for (var i = 0; i < count; i++)
                {
                    nint item = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(array, 4))(array, i, &item));
                    try
                    {
                        result[i] = Text(item, 30005);
                    }
                    finally { Marshal.Release(item); }
                }
                return result;
            }
            finally { if (array != 0) Marshal.Release(array); Marshal.Release(pattern); }
        }
        void SetValue(nint element, string text)
        {
            var pattern = Pattern(element, 10002);
            var value = Marshal.StringToBSTR(text);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(pattern, 3))(pattern, value));
            }
            finally { Marshal.FreeBSTR(value); Marshal.Release(pattern); }
        }
        var single = found["Native combobox"];
        var multiple = found["Native multiple"];
        check(ControlType(single) == 50003 && Text(single, 30012) == "Duxel.ComboBox"
            && Boolean(single, 30037) && Boolean(single, 30028) && Boolean(single, 30043),
            "external Combobox exposes actual ComboBox Selection, ExpandCollapse and Value providers");
        check(!Boolean(single, 30046) && !Boolean(single, 30060) && Boolean(multiple, 30060),
            "external Combobox values are editable and native multi-selection policies are accurate");
        var singleItems = Snapshot(single);
        check(!Boolean(single, 30103) && !Boolean(singleItems["Native combobox"], 30103),
            "external Combobox standalone invalid state reaches the root and child editor DataValidForForm properties");
        var identity = RuntimeIdentity(singleItems["Beta"]);
        check(ControlType(singleItems["Beta"]) == 50007 && Boolean(singleItems["Beta"], 30022)
            && Boolean(singleItems["Beta"], 30036) && !Boolean(singleItems["Beta"], 30031),
            "external collapsed Combobox retains logical selectable ListItems without Invoke");
        ReadPropertyChanges(automation, single, 1, owned, [30045], sink =>
        {
            Select(singleItems["Beta"], 3);
            check(sink.Wait() == new TooltipNativeProperty(30045, Text(single, 30011), "Beta"), "external Combobox delivers the committed Value property change");
        });
        Wait(() => Text(single, 30045) == "Beta");
        check(Selected(single).SequenceEqual(new[] { "Beta" }) && Text(single, 30005) == "Native combobox",
            "external collapsed ComboBox GetSelection delegates to its real ListBox without changing Name");
        Expand(single);
        Wait(() => Integer(single, 30070) == 1 && !Boolean(Snapshot(single)["Alpha"], 30022));
        var open = Snapshot(single);
        check(RuntimeIdentity(open["Beta"]) == identity, "external Combobox logical option runtime identities survive expansion");
        Select(open["Alpha"], 3);
        Wait(() => Integer(single, 30070) == 0 && Text(single, 30045) == "Alpha");
        check(Selected(single).SequenceEqual(new[] { "Alpha" }), "external Combobox single selection commits and dismisses");
        Wait(() => Boolean(single, 30103) && Boolean(Snapshot(single)["Native combobox"], 30103));
        check(Boolean(single, 30103), "external Combobox clears its native validation state after the model changes");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(single, 3))(single));
        SetValue(single, "New ");
        Wait(() => Text(single, 30045) == "New " && Integer(single, 30070) == 1);
        var filtered = Snapshot(single);
        check(Boolean(filtered["Alpha"], 30022) && !Boolean(filtered["New York"], 30022)
            && Selected(single).SequenceEqual(new[] { "Alpha" }),
            "external editable Combobox preserves GetSelection for options hidden by an actual Value query");
        check(RuntimeIdentity(filtered["Beta"]) == identity,
            "external Combobox filtering preserves the original native option identity");
        Select(filtered["Alpha"], 3);
        Wait(() => Text(single, 30045) == "Alpha" && Integer(single, 30070) == 0);
        Expand(multiple);
        Wait(() => Integer(multiple, 30070) == 1);
        var multiItems = Snapshot(multiple);
        Select(multiItems["Alpha"], 3);
        Select(multiItems["Beta"], 4);
        Wait(() => Selected(multiple).Length == 2);
        check(Selected(multiple).SequenceEqual(new[] { "Alpha", "Beta" }) && Integer(multiple, 30070) == 1,
            "external AddToSelection preserves both values and the expanded multi-select list");
        Select(multiItems["Alpha"], 5);
        Wait(() => Selected(multiple).SequenceEqual(new[] { "Beta" }));
        check(Selected(multiple).SequenceEqual(new[] { "Beta" }), "external RemoveFromSelection updates the actual bound collection");
        Select(multiItems["New York"], 3);
        Wait(() => Selected(multiple).SequenceEqual(new[] { "New York" }));
        check(Selected(multiple).SequenceEqual(new[] { "New York" }), "external Select replaces the multi-select collection");
        var expansion = Pattern(multiple, 10005);
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expansion, 4))(expansion));
        }
        finally { Marshal.Release(expansion); }
        var gated = found["Native gated"];
        var gatePattern = Pattern(gated, 10005);
        try
        {
            var deniedOpen = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(gatePattern, 3))(gatePattern);
            check(deniedOpen == unchecked((int)0x80131509) && Integer(gated, 30070) == 0,
                "external controlled Combobox rejects expansion with the actual UIA InvalidOperation result");
            Wait(() => Boolean(found["Allow opening"], 30010));
            Invoke(found["Allow opening"]);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(gatePattern, 3))(gatePattern));
            Wait(() => Integer(gated, 30070) == 1);
            var deniedClose = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(gatePattern, 4))(gatePattern);
            check(deniedClose == unchecked((int)0x80131509) && Integer(gated, 30070) == 1,
                "external controlled Combobox rejects collapse without hiding the actual expanded popup");
            SetValue(gated, "Alpha");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(gatePattern, 4))(gatePattern));
            Wait(() => Integer(gated, 30070) == 0);
            check(Integer(gated, 30070) == 0, "external controlled Combobox accepts collapse after the model authorizes it");
        }
        finally { Marshal.Release(gatePattern); }
        Wait(() => Boolean(found["Hide dropdowns"], 30010));
        Invoke(found["Hide dropdowns"]);
        Wait(() => TooltipElementUnavailable(single));
        check(TooltipElementUnavailable(single) && TooltipElementUnavailable(multiItems["Beta"]),
            "external omitted Combobox retires combo and logical option providers");
        Invoke(found["Finish"]);
    }
}
