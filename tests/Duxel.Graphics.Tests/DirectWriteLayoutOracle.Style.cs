using System.Runtime.InteropServices;

internal static unsafe partial class DirectWriteLayoutOracle
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct StyleRange(uint Start, uint Length);

    internal static void SetFontWeight(nint layout, int start, int length, int weight) =>
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, StyleRange, int>)Method(layout, 32))
            (layout, weight, new(checked((uint)start), checked((uint)length))));
    internal static void SetFontFamily(nint layout, int start, int length, string family)
    {
        fixed (char* name = family)
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, StyleRange, int>)Method(layout, 31))
                (layout, name, new(checked((uint)start), checked((uint)length))));
    }
}
