using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckAvatarGroup(nint automation, nint windowRoot, Dictionary<string, nint> found,
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
                    throw new TimeoutException("AvatarGroup native state did not settle.");
                Thread.Sleep(10);
            }
        }
        Dictionary<string, nint> Snapshot()
        {
            nint condition = 0, elements = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
            owned.Add(condition);
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(windowRoot, 6))(windowRoot, 4, condition, &elements));
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
        var group = found["Native avatar group"];
        check(ControlType(group) == 50026 && Text(group, 30012) == "Duxel.AvatarGroup",
            "external AvatarGroup exposes its named native Group provider");
        check(!Boolean(group, 30009) && !Boolean(group, 30031), "external AvatarGroup passive root has no keyboard focus or Invoke");
        check(Enumerable.Range(2, 4).All(i => found.TryGetValue("Person " + i, out var item)
            && ControlType(item) == 50006 && !Boolean(item, 30009) && !Boolean(item, 30031)),
            "external AvatarGroup inline members remain individually named passive Images");
        var overflow = found["More people (2)"];
        check(ControlType(overflow) == 50000 && Boolean(overflow, 30009) && Boolean(overflow, 30028)
            && Integer(overflow, 30070) == 0, "external AvatarGroup overflow exposes real Button and ExpandCollapse patterns");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(overflow, 3))(overflow));
        Wait(() => Boolean(overflow, 30008));
        check(Boolean(overflow, 30008), "external AvatarGroup trigger accepts native keyboard focus");
        Expand(overflow);
        Dictionary<string, nint>? popup = null;
        Wait(() => { popup = Snapshot(); return popup.TryGetValue("Person 1", out var item) && ControlType(item) == 50011; });
        check(Integer(overflow, 30070) == 1 && ControlType(popup!["Person 0"]) == 50011,
            "external AvatarGroup publishes all hidden members in its expanded native Menu");
        Invoke(popup!["Person 1"]);
        Wait(() => Integer(overflow, 30070) == 0);
        check(Integer(overflow, 30070) == 0, "external AvatarGroup hidden member selection dismisses the native Menu");
        Invoke(found["Show pie"]);
        Dictionary<string, nint>? pie = null;
        Wait(() => { pie = Snapshot(); return pie.ContainsKey("More people (6)"); });
        var pieTrigger = pie!["More people (6)"];
        Expand(pieTrigger);
        Wait(() => { popup = Snapshot(); return Enumerable.Range(0, 6).All(i => popup.TryGetValue("Person " + i, out var item) && ControlType(item) == 50011); });
        check(Enumerable.Range(0, 6).All(i => ControlType(popup!["Person " + i]) == 50011),
            "external AvatarGroup pie lists the entire group through real MenuItem providers");
        Invoke(popup!["Person 4"]);
        Wait(() => Integer(pieTrigger, 30070) == 0);
        Invoke(found["Hide group"]);
        Wait(() => TooltipElementUnavailable(group));
        check(TooltipElementUnavailable(group) && TooltipElementUnavailable(pieTrigger),
            "external AvatarGroup omission retires held group and trigger providers");
        Invoke(found["Finish"]);
    }
}
