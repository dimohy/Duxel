using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private void RaiseToggleChanged(UiAccessibilityToggleChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0) return;
        var provider = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The toggle change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            var current = change.Element.Node.ToggleState;
            if (change.PreviousState.HasValue != current.HasValue)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30041,
                    UiaVariant.Boolean(change.PreviousState.HasValue), UiaVariant.Boolean(current.HasValue)));
            if (change.PreviousState is { } previous && current is { } value && previous != value)
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30086,
                    UiaVariant.Integer((int)previous), UiaVariant.Integer((int)value)));
        }
        finally { Marshal.Release(pointer); }
    }
}

internal abstract partial class UiaControlProvider
{
    public int Toggle() => WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.Toggle));
    public int GetToggleState(out int value)
    {
        value = 0;
        if (!Read(out var entry)) return UiaNative.Unavailable;
        if (entry.Node.ToggleState is not { } state) return UiaNative.InvalidOperation;
        value = (int)state;
        return 0;
    }
}
