using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal abstract partial class UiaElementProvider
{
    private UiaVariant RelatedElement(string? id) => UiaNative.Reference(id is null ? null : Host.Find(id));

    private UiaVariant ControlledElement(string? id) => UiaNative.ReferenceArray(id is null ? null : Host.Find(id));
}

internal static partial class UiaNative
{
    internal static UiaVariant Reference(UiaElementProvider? provider) => new()
    {
        Type = 13,
        Data = new()
        {
            Pointer = provider is null ? 0 : Pointer<IUiaSimple>(provider)
        }
    };
    internal static unsafe UiaVariant ReferenceArray(UiaElementProvider? provider)
    {
        var array = SafeArrayCreateVector(13, 0, provider is null ? 0u : 1u);
        if (array == 0)
            throw new OutOfMemoryException();
        try
        {
            if (provider is not null)
            {
                Marshal.ThrowExceptionForHR(SafeArrayAccessData(array, out var data));
                try
                {
                    *(nint*)data = Pointer<IUiaSimple>(provider);
                }
                finally { Marshal.ThrowExceptionForHR(SafeArrayUnaccessData(array)); }
            }
            return new()
            {
                Type = 0x2000 | 13,
                Data = new()
                {
                    Pointer = array
                }
            };
        }
        catch { Marshal.ThrowExceptionForHR(SafeArrayDestroy(array)); throw; }
    }
}

internal sealed partial class WindowsAccessibility
{
    private void RaiseRelationshipChanged(UiAccessibilityRelationshipChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
            return;
        var source = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The relationship event source must already be published.");
        var previous = change.Previous is { } oldTarget ? CaptureRelationshipTarget(oldTarget) : null;
        var current = change.Current is { } newTarget ? Find(newTarget.Element.Node.Id)
            ?? throw new InvalidOperationException("The new relationship target must already be published.") : null;
        var pointer = UiaNative.Pointer<IUiaSimple>(source);
        UiaVariant oldValue = default, newValue = default;
        try
        {
            var property = change.Property switch
            {
                UiAccessibilityRelationshipProperty.LabeledBy => 30018,
                UiAccessibilityRelationshipProperty.ControllerFor => 30104,
                UiAccessibilityRelationshipProperty.ContainingGrid => 30068,
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            };
            oldValue = property is 30018 or 30068 ? UiaNative.Reference(previous) : UiaNative.ReferenceArray(previous);
            newValue = property is 30018 or 30068 ? UiaNative.Reference(current) : UiaNative.ReferenceArray(current);
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, property, oldValue, newValue));
        }
        finally
        {
            var newResult = UiaNative.VariantClear(ref newValue);
            var oldResult = UiaNative.VariantClear(ref oldValue);
            Marshal.Release(pointer);
            Marshal.ThrowExceptionForHR(newResult);
            Marshal.ThrowExceptionForHR(oldResult);
        }
    }

    private UiaElementProvider CaptureRelationshipTarget(UiAccessibilityRelationshipTarget target)
    {
        var root = new UiaEventRootProvider(this);
        UiaElementProvider provider = root;
        var ancestry = target.Ancestry;
        for (var i = ancestry.Length - 1; i >= 0; i--)
            provider = new UiaEventProvider(this, ancestry[i], provider, root);
        return provider;
    }
}
