using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Duxel.Platform.Windows;

[GeneratedComInterface, Guid("d847d3a5-cab0-4a98-8c32-ecb45c59ad24")]
internal partial interface IUiaExpandCollapse
{
    [PreserveSig] int Expand();
    [PreserveSig] int Collapse();
    [PreserveSig] int GetExpandCollapseState(out int value);
}

// ABI pinned in artifacts/uia-foundation-2026-11-06/source-manifest.json.
// Selection interfaces are additionally pinned in artifacts/fluent-tablist-2026-11-11/reference-manifest.json.
[GeneratedComInterface, Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
internal partial interface IUiaSimple
{
    [PreserveSig] int GetProviderOptions(out int value);
    [PreserveSig] int GetPatternProvider(int pattern, out nint value);
    [PreserveSig] int GetPropertyValue(int property, out UiaVariant value);
    [PreserveSig] int GetHostProvider(out nint value);
}
[GeneratedComInterface, Guid("f7063da8-8359-439c-9297-bbc5299a7d87")]
internal partial interface IUiaFragment
{
    [PreserveSig] int Navigate(int direction, out nint value);
    [PreserveSig] int GetRuntimeId(out nint value);
    [PreserveSig] int GetBounds(out UiaBounds value);
    [PreserveSig] int GetEmbeddedRoots(out nint value);
    [PreserveSig] int SetFocus();
    [PreserveSig] int GetFragmentRoot(out nint value);
}
[GeneratedComInterface, Guid("620ce2a5-ab8f-40a9-86cb-de3c75599b58")]
internal partial interface IUiaFragmentRoot
{
    [PreserveSig] int ElementFromPoint(double x, double y, out nint value);
    [PreserveSig] int GetFocus(out nint value);
}
[GeneratedComInterface, Guid("54fcb24b-e18e-47a2-b4d3-eccbe77599a2")]
internal partial interface IUiaInvoke
{
    [PreserveSig] int Invoke();
}
[GeneratedComInterface, Guid("56d00bd0-c4f4-433c-a836-1a52a57e0892")]
internal partial interface IUiaToggle
{
    [PreserveSig] int Toggle();
    [PreserveSig] int GetToggleState(out int value);
}
[GeneratedComInterface, Guid("36dc7aef-33e6-4691-afe1-2be7274b3d33")]
internal partial interface IUiaRangeValue
{
    [PreserveSig] int SetRangeValue(double value);
    [PreserveSig] int GetRangeValue(out double value);
    [PreserveSig] int GetRangeReadOnly(out int value);
    [PreserveSig] int GetRangeMaximum(out double value);
    [PreserveSig] int GetRangeMinimum(out double value);
    [PreserveSig] int GetRangeLargeChange(out double value);
    [PreserveSig] int GetRangeSmallChange(out double value);
}
[GeneratedComInterface, Guid("b17d6187-0907-464b-a168-0ef17a1572b1")]
internal partial interface IUiaGrid
{
    [PreserveSig] int GetItem(int row, int column, out nint value);
    [PreserveSig] int GetRowCount(out int value);
    [PreserveSig] int GetColumnCount(out int value);
}
[GeneratedComInterface, Guid("d02541f1-fb81-4d64-ae32-f520f8a6dbd1")]
internal partial interface IUiaGridItem
{
    [PreserveSig] int GetRow(out int value);
    [PreserveSig] int GetColumn(out int value);
    [PreserveSig] int GetRowSpan(out int value);
    [PreserveSig] int GetColumnSpan(out int value);
    [PreserveSig] int GetContainingGrid(out nint value);
}
[GeneratedComInterface, Guid("c7935180-6fb3-4201-b174-7df73adbf64a")]
internal partial interface IUiaValue
{
    [PreserveSig] int SetValue(nint value);
    [PreserveSig] int GetValue(out nint value);
    [PreserveSig] int GetReadOnly(out int value);
}
[GeneratedComInterface, Guid("b38b8077-1fc3-42a5-8cae-d40c2215055a")]
internal partial interface IUiaScroll
{
    [PreserveSig] int Scroll(int horizontalAmount, int verticalAmount);
    [PreserveSig] int SetScrollPercent(double horizontalPercent, double verticalPercent);
    [PreserveSig] int GetHorizontalScrollPercent(out double value);
    [PreserveSig] int GetVerticalScrollPercent(out double value);
    [PreserveSig] int GetHorizontalViewSize(out double value);
    [PreserveSig] int GetVerticalViewSize(out double value);
    [PreserveSig] int GetHorizontallyScrollable(out int value);
    [PreserveSig] int GetVerticallyScrollable(out int value);
}
[GeneratedComInterface, Guid("2360c714-4bf1-4b26-ba65-9b21316127eb")]
internal partial interface IUiaScrollItem
{
    [PreserveSig] int ScrollIntoView();
}
[GeneratedComInterface, Guid("e747770b-39ce-4382-ab30-d8fb3f336f24")]
internal partial interface IUiaItemContainer
{
    [PreserveSig] int FindItemByProperty(nint startAfter, int propertyId, UiaVariant value, out nint found);
}
[GeneratedComInterface, Guid("cb98b665-2d35-4fac-ad35-f3c60d0c0b8b")]
internal partial interface IUiaVirtualizedItem
{
    [PreserveSig] int Realize();
}
[GeneratedComInterface, Guid("fb8b03af-3bdf-48d4-bd36-1a65793be168")]
internal partial interface IUiaSelection
{
    [PreserveSig] int GetSelection(out nint value);
    [PreserveSig] int GetCanSelectMultiple(out int value);
    [PreserveSig] int GetIsSelectionRequired(out int value);
}
[GeneratedComInterface, Guid("2acad808-b2d4-452d-a407-91ff1ad167b2")]
internal partial interface IUiaSelectionItem
{
    [PreserveSig] int Select();
    [PreserveSig] int AddToSelection();
    [PreserveSig] int RemoveFromSelection();
    [PreserveSig] int GetIsSelected(out int value);
    [PreserveSig] int GetSelectionContainer(out nint value);
}
[StructLayout(LayoutKind.Sequential)]
internal struct UiaBounds
{
    internal double X, Y, Width, Height;
}
[StructLayout(LayoutKind.Sequential)]
internal struct UiaRecord
{
    internal nint Record, Info;
}
[StructLayout(LayoutKind.Explicit)]
internal struct UiaVariantData
{
    [FieldOffset(0)] internal int Integer;
    [FieldOffset(0)] internal double Real;
    [FieldOffset(0)] internal short Boolean;
    [FieldOffset(0)] internal nint Pointer;
    [FieldOffset(0)] internal UiaRecord Record;
}
[StructLayout(LayoutKind.Sequential)]
internal struct UiaVariant
{
    internal ushort Type, Reserved1, Reserved2, Reserved3;
    internal UiaVariantData Data;
    internal static UiaVariant Integer(int value) => new() { Type = 3, Data = new() { Integer = value } };
    internal static UiaVariant Double(double value) => new() { Type = 5, Data = new() { Real = value } };
    internal static UiaVariant Boolean(bool value) => new() { Type = 11, Data = new() { Boolean = value ? (short)-1 : (short)0 } };
    internal static UiaVariant String(string value) => new() { Type = 8, Data = new() { Pointer = UiaNative.Bstr(value) } };
}
internal static partial class UiaNative
{
    internal const int Unavailable = unchecked((int)0x80040201), Disabled = unchecked((int)0x80040200), InvalidOperation = unchecked((int)0x80131509), InvalidArgument = unchecked((int)0x80070057);
    internal static unsafe nint Pointer<T>(T value) where T : class => (nint)ComInterfaceMarshaller<T>.ConvertToUnmanaged(value);
    internal static nint Bstr(string value)
    {
        var result = SysAllocStringLen(value, (uint)value.Length);
        return result != 0 ? result : throw new OutOfMemoryException();
    }
    internal static unsafe nint RuntimeId(long id)
    {
        var array = SafeArrayCreateVector(3, 0, 3);
        if (array == 0)
            throw new OutOfMemoryException();
        try
        {
            Marshal.ThrowExceptionForHR(SafeArrayAccessData(array, out var pointer));
            var data = (int*)pointer;
            data[0] = 3;
            data[1] = (int)(id >> 32);
            data[2] = unchecked((int)id);
            Marshal.ThrowExceptionForHR(SafeArrayUnaccessData(array));
            return array;
        }
        catch { SafeArrayDestroy(array); throw; }
    }

    internal static unsafe UiaVariant ClickablePoint(UiaBounds bounds)
    {
        var array = SafeArrayCreateVector(5, 0, 2);
        if (array == 0)
            throw new OutOfMemoryException();
        try
        {
            Marshal.ThrowExceptionForHR(SafeArrayAccessData(array, out var pointer));
            var data = (double*)pointer;
            data[0] = bounds.X;
            data[1] = bounds.Y;
            Marshal.ThrowExceptionForHR(SafeArrayUnaccessData(array));
            return new() { Type = 0x2005, Data = new() { Pointer = array } };
        }
        catch
        {
            _ = SafeArrayDestroy(array);
            throw;
        }
    }

    internal static unsafe UiaVariant Rectangle(UiaBounds bounds)
    {
        const ushort doubleType = 5;
        const ushort arrayFlag = 0x2000;
        var array = SafeArrayCreateVector(doubleType, 0, 4);
        if (array == 0)
            throw new OutOfMemoryException();

        try
        {
            Marshal.ThrowExceptionForHR(SafeArrayAccessData(array, out var pointer));
            *(UiaBounds*)pointer = bounds;
            Marshal.ThrowExceptionForHR(SafeArrayUnaccessData(array));
            return new()
            {
                Type = arrayFlag | doubleType,
                Data = new()
                {
                    Pointer = array
                }
            };
        }
        catch
        {
            _ = SafeArrayDestroy(array);
            throw;
        }
    }
    [LibraryImport("UIAutomationCore.dll")] internal static partial nint UiaReturnRawElementProvider(nint hwnd, nuint wParam, nint lParam, nint provider);
    [LibraryImport("UIAutomationCore.dll")] internal static partial int UiaHostProviderFromHwnd(nint hwnd, out nint provider);
    [LibraryImport("UIAutomationCore.dll")] internal static partial int UiaDisconnectProvider(nint provider);
    [LibraryImport("UIAutomationCore.dll")]
    internal static partial int UiaRaiseAutomationEvent(nint provider, int eventId);
    [LibraryImport("UIAutomationCore.dll")]
    internal static partial int UiaRaiseAutomationPropertyChangedEvent(nint provider, int propertyId,
        UiaVariant oldValue, UiaVariant newValue);
    [LibraryImport("UIAutomationCore.dll")]
    internal static partial int UiaRaiseStructureChangedEvent(nint provider, int changeType, nint runtimeId, int runtimeIdLength);
    [LibraryImport("UIAutomationCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UiaClientsAreListening();
    [LibraryImport("oleaut32.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial nint SysAllocStringLen(string value, uint length);
    [LibraryImport("oleaut32.dll")] internal static partial nint SafeArrayCreateVector(ushort type, int lowerBound, uint count);
    [LibraryImport("oleaut32.dll")] internal static partial int SafeArrayAccessData(nint array, out nint data);
    [LibraryImport("oleaut32.dll")] internal static partial int SafeArrayUnaccessData(nint array);
    [LibraryImport("oleaut32.dll")] internal static partial int SafeArrayDestroy(nint array);
    [LibraryImport("oleaut32.dll")] internal static partial int VariantClear(ref UiaVariant value);
    [LibraryImport("user32.dll")] internal static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static partial bool GetClientRect(nint hwnd, out NativeRect rect);
    [LibraryImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static partial bool ClientToScreen(nint hwnd, ref NativePoint point);
    [LibraryImport("user32.dll")] internal static partial uint GetDpiForWindow(nint hwnd);
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        internal int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        internal int X, Y;
    }
}
