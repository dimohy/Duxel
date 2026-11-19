using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe partial class TabListNativeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var host = new WindowsAccessibility(0);
        var tree = host.Tree;
        void Publish(bool selected)
        {
            tree.BeginFrame();
            tree.Add(new("tabs", "", UiAccessibilityRole.TabList, "Tabs", new(0, 0, 200, 40), true, false, false, Focusable : false, ValuePattern : false));
            tree.Add(new UiAccessibilityNode("first", "tabs", UiAccessibilityRole.Tab, "First", new(0, 0, 100, 40), true, false, false, ValuePattern : false) { Selected = selected });
            tree.Add(new UiAccessibilityNode("second", "tabs", UiAccessibilityRole.Tab, "Second", new(100, 0, 100, 40), true, false, false, ValuePattern : false) { Selected = false });
            tree.EndFrame();
        }
        Publish(true);
        var owner = host.Find("tabs")!;
        var first = host.Find("first")!;
        var second = host.Find("second")!;
        owner.GetPatternProvider(10001, out var selection);
        first.GetPatternProvider(10010, out var selectionItem);
        try
        {
            check(selection != 0 && selectionItem != 0, "Windows exposes Selection and SelectionItem COM patterns for actual tab roles");
            var table = *(nint**)selection;
            nint array = 0;
            var hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)table[3])(selection, &array);
            try
            {
                Marshal.ThrowExceptionForHR(UiaNative.SafeArrayAccessData(array, out var data));
                try
                {
                    var item = *(nint*)data;
                    var itemTable = *(nint**)item;
                    UiaVariant type = default;
                    var result = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)itemTable[5])(item, 30003, &type);
                    check(hr == 0 && result == 0 && type.Type == 3 && type.Data.Integer == 50019,
                        "Selection native ABI returns a SAFEARRAY containing the actual tab provider");
                }
                finally { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayUnaccessData(array)); }
            }
            finally { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayDestroy(array)); }
            first.GetIsSelected(out var isSelected);
            second.GetIsSelected(out var otherSelected);
            check(isSelected == 1 && otherSelected == 0, "native selection items distinguish selected and unselected state");
            first.GetSelectionContainer(out var container);
            try
            {
                var containerTable = *(nint**)container;
                UiaVariant type = default;
                var result = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)containerTable[5])(container, 30003, &type);
                check(result == 0 && type.Data.Integer == 50018, "SelectionContainer native ABI resolves the actual tab-list provider");
            }
            finally { Marshal.Release(container); }
            owner.GetCanSelectMultiple(out var multiple);
            owner.GetIsSelectionRequired(out var required);
            check(multiple == 0 && required == 0, "native TabList is single-selection and supports explicit empty selection");
            check(second.AddToSelection() == UiaNative.InvalidOperation, "native AddToSelection rejects simultaneous selected tabs");
            check(second.Select() == 0, "native Select queues without blocking the UIA provider thread");
            tree.BeginFrame();
            var select = tree.Take("second", UiAccessibilityAction.Select);
            check(select is not null, "native selection reaches the render-thread action queue");
            select!.Result = UiAccessibilityResult.Success;
            tree.AbortFrame();
            Publish(true);
            check(first.RemoveFromSelection() == 0, "native RemoveFromSelection queues explicit deselection");
            tree.BeginFrame();
            var remove = tree.Take("first", UiAccessibilityAction.Deselect);
            check(remove is not null, "native deselection reaches its actual tab action");
            remove!.Result = UiAccessibilityResult.Success;
            tree.AbortFrame();
            Publish(false);
            owner.GetSelection(out var empty);
            try
            {
                Marshal.ThrowExceptionForHR(SafeArrayGetUBound(empty, 1, out var upper));
                check(upper == -1, "empty native selection returns an empty SAFEARRAY without inventing a selected item");
            }
            finally { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayDestroy(empty)); }
            tree.BeginFrame(); tree.EndFrame();
            check(owner.GetSelection(out _) == UiaNative.Unavailable && first.GetIsSelected(out _) == UiaNative.Unavailable,
                "removed native tab providers reject stale selection reads");
        }
        finally
        {
            if (selection != 0) Marshal.Release(selection);
            if (selectionItem != 0) Marshal.Release(selectionItem);
        }
    }
    [LibraryImport("oleaut32.dll")]
    private static partial int SafeArrayGetUBound(nint array, uint dimension, out int upper);
}
