using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckDropdown(nint automation, nint windowRoot, Dictionary<string, nint> found,
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
                    throw new TimeoutException("Dropdown native state did not settle.");
                Thread.Sleep(10);
            }
        }
        Dictionary<string, nint> Snapshot(nint owner, bool includeLabels = false, bool preferButtons = false)
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
                if (name.Length > 0 && (includeLabels || Text(element, 30012) != "Duxel.Label"))
                {
                    if (ControlType(element) == 50011 || preferButtons && ControlType(element) == 50000)
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
        var single = found["Native dropdown"];
        var multiple = found["Native multiple"];
        check(ControlType(single) == 50003 && Text(single, 30012) == "Duxel.ComboBox"
            && Boolean(single, 30037) && Boolean(single, 30028) && Boolean(single, 30043),
            "external Dropdown exposes actual ComboBox Selection, ExpandCollapse and Value providers");
        check(Boolean(single, 30046) && !Boolean(single, 30060) && Boolean(multiple, 30060),
            "external Dropdown values are readonly and native multi-selection policies are accurate");
        var singleItems = Snapshot(single);
        check(!Boolean(single, 30103), "external Dropdown exposes standalone invalid state through DataValidForForm");
        var identity = RuntimeIdentity(singleItems["Beta"]);
        check(ControlType(singleItems["Beta"]) == 50007 && Boolean(singleItems["Beta"], 30022)
            && Boolean(singleItems["Beta"], 30036) && !Boolean(singleItems["Beta"], 30031),
            "external collapsed Dropdown retains logical selectable ListItems without Invoke");
        ReadPropertyChanges(automation, single, 1, owned, [30045], sink =>
        {
            Select(singleItems["Beta"], 3);
            check(sink.Wait() == new TooltipNativeProperty(30045, Text(single, 30011), "Beta"), "external Dropdown delivers the committed Value property change");
        });
        Wait(() => Text(single, 30045) == "Beta");
        check(Selected(single).SequenceEqual(new[] { "Beta" }) && Text(single, 30005) == "Native dropdown",
            "external collapsed ComboBox GetSelection delegates to its real ListBox without changing Name");
        Expand(single);
        Wait(() => Integer(single, 30070) == 1 && !Boolean(Snapshot(single)["Alpha"], 30022));
        var open = Snapshot(single);
        check(RuntimeIdentity(open["Beta"]) == identity, "external Dropdown logical option runtime identities survive expansion");
        Select(open["Alpha"], 3);
        Wait(() => Integer(single, 30070) == 0 && Text(single, 30045) == "Alpha");
        check(Selected(single).SequenceEqual(new[] { "Alpha" }), "external Dropdown single selection commits and dismisses");
        Wait(() => Boolean(single, 30103));
        check(Boolean(single, 30103), "external Dropdown clears its native validation state after the model changes");
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
        Wait(() => Boolean(found["Hide dropdowns"], 30010));
        var tag = Snapshot(windowRoot)["Remove New York"];
        var toolbar = Snapshot(windowRoot)["Native selected tags"];
        check(ControlType(toolbar) == 50021 && Text(toolbar, 30012) == "Duxel.Toolbar"
            && Text(toolbar, 30101) == "toolbar" && Boolean(toolbar, 30016) && Boolean(toolbar, 30017)
            && !Boolean(toolbar, 30009) && !Boolean(toolbar, 30031),
            "external TagGroup exposes native Toolbar semantics without a fake root action");
        check(Text(Snapshot(windowRoot, includeLabels: true)["정##세일"], 30005) == "정##세일",
            "external passive Tag preserves literal hash markers in its complete native name");
        check(ControlType(tag) == 50000 && Boolean(tag, 30031), "external selected Tag exposes a real dismiss Invoke provider");
        Invoke(tag);
        Wait(() => Selected(multiple).Length == 0 && TooltipElementUnavailable(tag));
        check(Selected(multiple).Length == 0, "external Tag dismissal updates the same Dropdown selection and retires its provider");
        Select(multiItems["New York"], 3);
        Wait(() => Selected(multiple).SequenceEqual(new[] { "New York" }));
        var interactiveRoot = Snapshot(windowRoot)["Native interactive tags"];
        var actions = Snapshot(interactiveRoot, preferButtons: true);
        var primary = actions["New York"];
        var secondary = actions["Dismiss New York"];
        check(ControlType(primary) == 50000 && ControlType(secondary) == 50000 && Boolean(primary, 30031) && Boolean(secondary, 30031),
            "external InteractionTag exposes independent primary and secondary Invoke providers");
        Invoke(primary);
        Wait(() => Snapshot(windowRoot, includeLabels: true).ContainsKey("Inspected : n"));
        check(Selected(multiple).SequenceEqual(new[] { "New York" }), "external InteractionTag primary invocation preserves the shared selection");
        Invoke(secondary);
        Wait(() => Selected(multiple).Length == 0 && TooltipElementUnavailable(primary) && TooltipElementUnavailable(secondary));
        check(Selected(multiple).Length == 0, "external InteractionTag secondary invocation dismisses and retires both child providers");
        Select(multiItems["New York"], 3);
        Wait(() => Selected(multiple).SequenceEqual(new[] { "New York" }));
        var emphasizedRoot = Snapshot(windowRoot)["Native emphasized tags"];
        var emphasizedActions = Snapshot(emphasizedRoot, preferButtons: true);
        var groupPrimary = emphasizedActions["New York"];
        var groupSecondary = emphasizedActions["Unlist New York"];
        var groupToggle = Pattern(groupPrimary, 10015);
        owned.Add(groupToggle);
        int GroupEmphasisState()
        {
            int state = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(groupToggle, 4))(groupToggle, &state));
            return state;
        }
        check(Boolean(groupPrimary, 30041) && !Boolean(groupPrimary, 30031) && Boolean(groupSecondary, 30031)
            && !Boolean(groupSecondary, 30041), "external group emphasis exposes independent Toggle and Invoke providers");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(groupToggle, 3))(groupToggle));
        Wait(() => GroupEmphasisState() == 1);
        check(Selected(multiple).SequenceEqual(new[] { "New York" }), "external group Toggle preserves visible Dropdown selection");
        Invoke(groupSecondary);
        Wait(() => Selected(multiple).Length == 0 && TooltipElementUnavailable(groupPrimary));
        Select(multiItems["New York"], 3);
        Wait(() => Snapshot(Snapshot(windowRoot)["Native emphasized tags"], preferButtons: true).ContainsKey("New York"));
        var restoredEmphasis = Snapshot(Snapshot(windowRoot)["Native emphasized tags"], preferButtons: true)["New York"];
        var restoredToggle = Pattern(restoredEmphasis, 10015);
        owned.Add(restoredToggle);
        int restoredState = -1;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(restoredToggle, 4))(restoredToggle, &restoredState));
        check(restoredState == 1, "external visibility restoration retains independent group emphasis");
        var controlled = Snapshot(windowRoot, preferButtons: true)["Native emphasis"];
        check(ControlType(controlled) == 50000 && Boolean(controlled, 30041) && !Boolean(controlled, 30031),
            "external controlled InteractionTag exposes the real Toggle button pattern");
        var toggle = Pattern(controlled, 10015);
        owned.Add(toggle);
        int EmphasisState()
        {
            int state = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(toggle, 4))(toggle, &state));
            return state;
        }
        check(EmphasisState() == 0, "external controlled tag starts unselected");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle));
        Wait(() => EmphasisState() == 1);
        check(EmphasisState() == 1, "external Toggle publishes the authoritative selected value");
        Invoke(found["Hide dropdowns"]);
        Wait(() => TooltipElementUnavailable(single));
        check(TooltipElementUnavailable(single) && TooltipElementUnavailable(multiItems["Beta"]),
            "external omitted Dropdown retires combo and logical option providers");
        Invoke(found["Finish"]);
    }
}
