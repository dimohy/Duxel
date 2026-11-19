using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private void RaiseSelectionChanged(UiAccessibilitySelectionChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
            return;
        // A menu selection dismisses its surface before asynchronous UIA caches may finish reading it.
        UiaElementProvider provider = change.Element.Node.Role is UiAccessibilityRole.MenuItem or UiAccessibilityRole.MenuGroup or UiAccessibilityRole.Option
            ? CaptureEventSource(change.Element, closed : false)
            : Find(change.Element.Node.Id)
                ?? throw new InvalidOperationException("The selection event source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            if (change.PreviousMultiple is { } multiple)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30060,
                    UiaVariant.Boolean(multiple), UiaVariant.Boolean(change.Element.Node.CanSelectMultiple)));
            else if (change.PreviousRequired is { } required)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30061,
                    UiaVariant.Boolean(required), UiaVariant.Boolean(change.Element.Node.SelectionRequired)));
            else if (change.Previous.HasValue != change.Element.Node.Selected.HasValue)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30036,
                    UiaVariant.Boolean(change.Previous.HasValue), UiaVariant.Boolean(change.Element.Node.Selected.HasValue)));
            if (change.Previous is { } previous && change.Element.Node.Selected is { } current && previous != current)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30079,
                    UiaVariant.Boolean(previous), UiaVariant.Boolean(current)));
            if (change.Event is { } kind)
            {
                var eventId = kind switch
                {
                    UiAccessibilitySelectionEventKind.Selected => 20012,
                    UiAccessibilitySelectionEventKind.Added => 20010,
                    UiAccessibilitySelectionEventKind.Removed => 20011,
                    UiAccessibilitySelectionEventKind.Invalidated => 20013,
                    _ => throw new ArgumentOutOfRangeException(nameof(change))
                };
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationEvent(pointer, eventId));
            }
        }
        finally { Marshal.Release(pointer); }
    }
}

internal abstract partial class UiaControlProvider
{
    public int Select() => WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.Select, wait : false));
    public int RemoveFromSelection() => WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.Deselect, wait : false));
    public int AddToSelection()
    {
        if (!Read(out _)) return UiaNative.Unavailable;
        var result = Host.Tree.ResolveSelectionContainer(Id!, RuntimeId, out var parent);
        if (result != UiAccessibilityResult.Success) return WindowsAccessibility.Result(result);
        if (parent.Node.CanSelectMultiple)
            return WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.AddToSelection, wait : false));
        result = Host.Tree.CaptureSelection(parent.Node.Id, parent.RuntimeId, out var selection);
        if (result != UiAccessibilityResult.Success) return WindowsAccessibility.Result(result);
        if (selection!.SelectedItems.Any(item => item.RuntimeId != RuntimeId)) return UiaNative.InvalidOperation;
        return Select();
    }
    public int GetIsSelected(out int value)
    {
        value = 0;
        if (!Read(out var entry)) return UiaNative.Unavailable;
        if (!entry.Node.SupportsSelectionItem) return UiaNative.InvalidOperation;
        value = entry.Node.Selected == true ? 1 : 0;
        return 0;
    }
    public int GetSelectionContainer(out nint value)
    {
        value = 0;
        if (!Read(out _)) return UiaNative.Unavailable;
        var result = Host.Tree.ResolveSelectionContainer(Id!, RuntimeId, out var owner);
        if (result != UiAccessibilityResult.Success) return WindowsAccessibility.Result(result);
        if (Host.Find(owner.Node.Id) is not { } parent || parent.RuntimeId != owner.RuntimeId) return UiaNative.Unavailable;
        value = UiaNative.Pointer<IUiaSimple>(parent);
        return 0;
    }
    public int GetCanSelectMultiple(out int value) => ReadSelectionPolicy(out value, required : false);
    public int GetIsSelectionRequired(out int value) => ReadSelectionPolicy(out value, required : true);
    private int ReadSelectionPolicy(out int value, bool required)
    {
        value = 0;
        if (!Read(out var entry)) return UiaNative.Unavailable;
        if (!entry.Node.SupportsSelection) return UiaNative.InvalidOperation;
        value = (required ? entry.Node.SelectionRequired : entry.Node.CanSelectMultiple) ? 1 : 0;
        return 0;
    }
    public unsafe int GetSelection(out nint value)
    {
        value = 0;
        if (!Read(out _)) return UiaNative.Unavailable;
        var result = Host.Tree.CaptureSelection(Id!, RuntimeId, out var selection);
        if (result != UiAccessibilityResult.Success) return WindowsAccessibility.Result(result);
        var selected = new List<UiaNodeProvider>();
        foreach (var child in selection!.SelectedItems)
        {
            if (Host.Find(child.Node.Id) is not { } provider || provider.RuntimeId != child.RuntimeId) return UiaNative.Unavailable;
            selected.Add(provider);
        }
        var array = UiaNative.SafeArrayCreateVector(13, 0, (uint)selected.Count);
        if (array == 0) throw new OutOfMemoryException();
        try
        {
            Marshal.ThrowExceptionForHR(UiaNative.SafeArrayAccessData(array, out var data));
            try
            {
                for (var i = 0; i < selected.Count; i++) ((nint*)data)[i] = UiaNative.Pointer<IUiaSimple>(selected[i]);
            }
            finally { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayUnaccessData(array)); }
            value = array;
            return 0;
        }
        catch { Marshal.ThrowExceptionForHR(UiaNative.SafeArrayDestroy(array)); throw; }
    }
}
