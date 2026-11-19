using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility
{
    private void RaiseRangeChanged(UiAccessibilityRangeChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0) return;
        var provider = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The range change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            void Raise(int property, UiaVariant previous, UiaVariant current) =>
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, property, previous, current));
            var current = change.Element.Node.Range;
            if (change.Previous.HasValue != current.HasValue)
                Raise(30033, UiaVariant.Boolean(change.Previous.HasValue), UiaVariant.Boolean(current.HasValue));
            if (change.Previous is not { } before || current is not { } after) return;
            if (before.Value != after.Value) Raise(30047, UiaVariant.Double(before.Value), UiaVariant.Double(after.Value));
            if (before.Minimum != after.Minimum) Raise(30049, UiaVariant.Double(before.Minimum), UiaVariant.Double(after.Minimum));
            if (before.Maximum != after.Maximum) Raise(30050, UiaVariant.Double(before.Maximum), UiaVariant.Double(after.Maximum));
            if (before.LargeChange != after.LargeChange) Raise(30051, UiaVariant.Double(before.LargeChange), UiaVariant.Double(after.LargeChange));
            if (before.SmallChange != after.SmallChange) Raise(30052, UiaVariant.Double(before.SmallChange), UiaVariant.Double(after.SmallChange));
            if (before.Vertical != after.Vertical) Raise(30023, UiaVariant.Integer(before.Vertical ? 2 : 1), UiaVariant.Integer(after.Vertical ? 2 : 1));
            if (change.WasReadOnly != change.Element.Node.ReadOnly)
                Raise(30048, UiaVariant.Boolean(change.WasReadOnly), UiaVariant.Boolean(change.Element.Node.ReadOnly));
        }
        finally { Marshal.Release(pointer); }
    }
}

internal abstract partial class UiaControlProvider
{
    public int SetRangeValue(double value) => WindowsAccessibility.Result(Host.Tree.RequestRangeValue(Id!, RuntimeId, value));
    public int GetRangeValue(out double value) => ReadRange(0, out value);
    public int GetRangeMaximum(out double value) => ReadRange(1, out value);
    public int GetRangeMinimum(out double value) => ReadRange(2, out value);
    public int GetRangeLargeChange(out double value) => ReadRange(3, out value);
    public int GetRangeSmallChange(out double value) => ReadRange(4, out value);
    public int GetRangeReadOnly(out int value)
    {
        value = 0;
        if (!Read(out var entry)) return UiaNative.Unavailable;
        if (entry.Node.Range is null) return UiaNative.InvalidOperation;
        value = entry.Node.ReadOnly ? 1 : 0;
        return 0;
    }
    private int ReadRange(int property, out double value)
    {
        value = 0;
        if (!Read(out var entry)) return UiaNative.Unavailable;
        if (entry.Node.Range is not { } range) return UiaNative.InvalidOperation;
        value = property switch
        {
            0 => range.Value, 1 => range.Maximum, 2 => range.Minimum,
            3 => range.LargeChange, 4 => range.SmallChange,
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
        return 0;
    }
}
