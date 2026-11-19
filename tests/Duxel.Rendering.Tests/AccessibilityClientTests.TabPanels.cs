using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static nint CheckControlledPanel(nint tab, List<nint> owned, Action<bool, string> check, bool focusable = true)
    {
        var elements = ControlledPanels(tab, owned);
        int count = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
        if (count != 1) throw new InvalidOperationException("Selected tab must control exactly one live panel.");
        nint panel = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, 0, &panel));
        owned.Add(panel);
        UiaVariant role = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(panel, 10))(panel, 30003, &role));
        try
        {
            check(role.Type == 3 && role.Data.Integer == 50033 && Text(panel, 30012) == "Duxel.TabPanel"
                && Text(panel, 30101) == "tabpanel",
                "external ControllerFor returns the actual Pane control and tabpanel role through IUIAutomationElementArray");
        }
        finally { Ok(UiaNative.VariantClear(ref role)); }
        var label = RelatedProperty(panel, 30018, new("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), owned);
        check(Text(label, 30011) == Text(tab, 30011),
            "external panel LabeledBy resolves back to its actual controlling tab identity");
        check(Boolean(panel, 30009) == focusable && Boolean(panel, 30016) && Boolean(panel, 30017),
            "external panel exposes its declared keyboard focusability and actual control/content views");
        return panel;
    }

    private static bool NoControlledPanel(nint tab, List<nint> owned)
    {
        var elements = ControlledPanels(tab, owned);
        int count = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
        return count == 0;
    }
    private static nint ControlledPanels(nint tab, List<nint> owned) =>
        RelatedProperty(tab, 30104, new("14314595-b4bc-4055-95f2-58f2e42c9855"), owned);

    private static nint RelatedProperty(nint element, int property, Guid iid, List<nint> owned)
    {
        UiaVariant value = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, property, &value));
        try
        {
            if (value.Type != 13 || value.Data.Pointer == 0)
                throw new InvalidOperationException("Required native relationship property is unavailable : " + property);
            Ok(Marshal.QueryInterface(value.Data.Pointer, in iid, out var related));
            owned.Add(related);
            return related;
        }
        finally { Ok(UiaNative.VariantClear(ref value)); }
    }
}
