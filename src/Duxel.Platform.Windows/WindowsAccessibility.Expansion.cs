using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private void RaiseExpansionChanged(UiAccessibilityExpansionChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0) return;
        var provider = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The expansion change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            var current = change.Element.Node.Expansion;
            if (change.Previous.HasValue != current.HasValue)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30028,
                    UiaVariant.Boolean(change.Previous.HasValue), UiaVariant.Boolean(current.HasValue)));
            var previousState = change.Previous ?? UiAccessibilityExpansionState.LeafNode;
            var currentState = current ?? UiAccessibilityExpansionState.LeafNode;
            if (previousState != currentState)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30070,
                    UiaVariant.Integer((int)previousState), UiaVariant.Integer((int)currentState)));
        }
        finally { Marshal.Release(pointer); }
    }
}

internal abstract partial class UiaControlProvider
{
    public int Expand() => WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.Expand));
    public int Collapse() => WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.Collapse));
    public int GetExpandCollapseState(out int value)
    {
        value = (int)UiAccessibilityExpansionState.LeafNode;
        if (!Read(out var entry)) return UiaNative.Unavailable;
        if (entry.Node.Expansion is not { } state) return UiaNative.InvalidOperation;
        value = (int)state;
        return 0;
    }
}
