using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe class TreeNativeSelectionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var host = new WindowsAccessibility(0);
        var tree = host.Tree;
        void Publish(bool selected)
        {
            tree.BeginFrame();
            tree.Add(new UiAccessibilityNode("root", "", UiAccessibilityRole.Tree, "Tree", new(0, 0, 200, 120), true, false, false)
            { ListSelection = true, SelectionRequired = true });
            tree.Add(new UiAccessibilityNode("branch", "root", UiAccessibilityRole.TreeItem, "Branch", new(0, 0, 200, 30), true, false, false)
            { Selected = false, SelectionContainerId = "root" });
            tree.Add(new UiAccessibilityNode("leaf", "branch", UiAccessibilityRole.TreeItem, "Nested leaf", new(20, 30, 180, 30), true, false, false)
            { Selected = selected, SelectionContainerId = "root" });
            tree.EndFrame();
        }
        Publish(true);
        var root = host.Find("root")!;
        var branch = host.Find("branch")!;
        var leaf = host.Find("leaf")!;
        root.GetPatternProvider(10001, out var selection);
        leaf.GetPatternProvider(10010, out var itemPattern);
        try
        {
            check(selection != 0 && itemPattern != 0, "Windows Tree exposes native Selection and nested SelectionItem patterns");
            var table = *(nint**)selection;
            nint array = 0;
            var hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)table[3])(selection, &array);
            try
            {
                Marshal.ThrowExceptionForHR(UiaNative.SafeArrayAccessData(array, out var data));
                try
                {
                    var item = *(nint*)data;
                    check(hr == 0 && ControlType(item) == 50024, "Tree native selection SAFEARRAY contains its selected grandchild provider");
                    UiaVariant name = default;
                    var itemTable = *(nint**)item;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)itemTable[5])(item, 30005, &name));
                    try { check(Marshal.PtrToStringBSTR(name.Data.Pointer) == "Nested leaf", "Tree native selection identifies the selected descendant rather than its branch"); }
                    finally { Marshal.FreeBSTR(name.Data.Pointer); }
                }
                finally { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayUnaccessData(array)); }
            }
            finally { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayDestroy(array)); }
            Marshal.ThrowExceptionForHR(leaf.GetSelectionContainer(out var container));
            try { check(ControlType(container) == 50023, "nested native SelectionContainer resolves the actual Tree root"); }
            finally { Marshal.Release(container); }
            check(branch.AddToSelection() == UiaNative.InvalidOperation,
                "native Tree AddToSelection rejects a selected descendant outside the direct sibling level");
            check(leaf.RemoveFromSelection() == UiaNative.InvalidOperation,
                "native Tree required-selection policy protects its only selected descendant");
            tree.BeginFrame(); tree.EndFrame();
            check(root.GetSelection(out _) == UiaNative.Unavailable && leaf.GetSelectionContainer(out _) == UiaNative.Unavailable,
                "retired native Tree providers reject selection queries after removal");
        }
        finally
        {
            if (selection != 0) Marshal.Release(selection);
            if (itemPattern != 0) Marshal.Release(itemPattern);
        }
    }

    private static int ControlType(nint provider)
    {
        UiaVariant value = default;
        var table = *(nint**)provider;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)table[5])(provider, 30003, &value));
        return value.Data.Integer;
    }
}
