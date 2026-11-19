using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckBreadcrumb(nint automation, nint windowRoot, Dictionary<string, nint> found,
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
                    throw new TimeoutException("Breadcrumb native state did not settle.");
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
                    result.TryAdd(name, element);
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
        var nav = found["Native breadcrumb"];
        check(ControlType(nav) == 50026 && Integer(nav, 30157) == 80003 && Text(nav, 30101) == "navigation",
            "external Breadcrumb exposes the native Navigation landmark identifier");
        nint trueCondition = 0, children = 0, list = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &trueCondition));
        owned.Add(trueCondition);
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(nav, 6))(nav, 2, trueCondition, &children));
        owned.Add(children);
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(children, 4))(children, 0, &list));
        owned.Add(list);
        check(ControlType(list) == 50008 && Text(list, 30012) == "Duxel.OrderedList", "external Breadcrumb contains its actual ordered native List");
        var current = found["Current"];
        check(ControlType(current) == 50007 && Integer(current, 30152) == 4 && Integer(current, 30153) == 4,
            "external current Breadcrumb level retains its ordered position and total");
        check(Text(current, 30102) == "current=page" && Text(current, 30026) == "Current location"
            && !Boolean(current, 30009) && !Boolean(current, 30031),
            "external current Breadcrumb item exposes current-page status without focus or Invoke");
        var currentIdentity = Text(current, 30011);
        ReadPropertyChanges(automation, current, 1, owned, [30026], sink =>
        {
            Invoke(found["Change current status"]);
            check(sink.Wait() == new TooltipNativeProperty(30026, currentIdentity, "Current project location")
                && Text(current, 30026) == "Current project location",
                "external Breadcrumb publishes the committed current-status property change");
        });
        var disabled = found["Disabled"];
        check(!Boolean(disabled, 30010) && Boolean(disabled, 30009), "external disabled Breadcrumb remains keyboard focusable");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(disabled, 3))(disabled));
        Wait(() => Boolean(disabled, 30008));
        check(Boolean(disabled, 30008), "external client can actually focus the disabled Breadcrumb level");
        var disabledInvoke = Pattern(disabled, 10000);
        try
        {
            check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(disabledInvoke, 3))(disabledInvoke) == unchecked((int)0x80040200),
            "external disabled Breadcrumb Invoke returns UIA element-not-enabled");
        }
        finally { Marshal.Release(disabledInvoke); }
        Invoke(found["Root"]);
        Invoke(found["Collapse path"]);
        Dictionary<string, nint>? collapsed = null;
        Wait(() => { collapsed = Snapshot(); return collapsed.ContainsKey("More levels"); });
        var overflow = collapsed!["More levels"];
        check(Boolean(overflow, 30028) && Integer(overflow, 30070) == 0,
            "external Breadcrumb overflow exposes its real collapsed ExpandCollapse state");
        var expansion = Pattern(overflow, 10005);
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expansion, 3))(expansion));
        }
        finally { Marshal.Release(expansion); }
        Wait(() => Integer(overflow, 30070) == 1);
        Dictionary<string, nint>? popup = null;
        Wait(() => { popup = Snapshot(); return popup.TryGetValue("Folder", out var item) && ControlType(item) == 50011; });
        check(ControlType(popup!["Folder"]) == 50011, "external Breadcrumb overflow publishes actual hidden-level MenuItems");
        Invoke(popup["Folder"]);
        Wait(() => Integer(overflow, 30070) == 0 && Boolean(found["Hide path"], 30010));
        check(Integer(overflow, 30070) == 0, "external hidden-level navigation dismisses the Breadcrumb menu");
        Invoke(found["Hide path"]);
        Wait(() => TooltipElementUnavailable(nav));
        check(TooltipElementUnavailable(nav) && TooltipElementUnavailable(current), "external omitted Breadcrumb retires landmark and current-item providers");
        Invoke(found["Finish"]);
    }
}
