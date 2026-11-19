using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Interop.Windows;

internal static unsafe partial class DirectWriteLongTextShaper
{
    private sealed class InlineState(InlineSpan metrics) : State
    {
        internal readonly InlineSpan Metrics = metrics;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct InlineMetrics
    {
        internal float Width, Height, Baseline;
        internal int SupportsSideways;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct InlineOverhang
    {
        internal float Left, Top, Right, Bottom;
    }
    private static void** CreateInlineTable()
    {
        var table = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DirectWriteLongTextShaper), 7 * sizeof(nint));
        table[0] = (delegate* unmanaged[Stdcall]<Callback*, Guid*, void**, int>)&Query;
        table[1] = (delegate* unmanaged[Stdcall]<Callback*, uint>)&AddRef;
        table[2] = (delegate* unmanaged[Stdcall]<Callback*, uint>)&ReleaseRef;
        table[3] = (delegate* unmanaged[Stdcall]<Callback*, nint, nint, float, float, int, int, nint, int>)&InlineDraw;
        table[4] = (delegate* unmanaged[Stdcall]<Callback*, InlineMetrics*, int>)&InlineGetMetrics;
        table[5] = (delegate* unmanaged[Stdcall]<Callback*, InlineOverhang*, int>)&InlineGetOverhang;
        table[6] = (delegate* unmanaged[Stdcall]<Callback*, int*, int*, int>)&InlineGetBreakConditions;
        return table;
    }
    // IDWriteTextLayout invokes the renderer's DrawInlineObject, not this optional object drawing method.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InlineDraw(Callback* self, nint context, nint renderer, float x, float y, int sideways, int rtl, nint effect)
        => unchecked((int)0x80004001);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InlineGetMetrics(Callback* self, InlineMetrics* result)
    {
        var metrics = GetState<InlineState>(self).Metrics;
        *result = new()
        {
            Width = metrics.Width,
            Height = metrics.Height,
            Baseline = metrics.Baseline
        };
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InlineGetOverhang(Callback* self, InlineOverhang* result)
    {
        *result = default;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InlineGetBreakConditions(Callback* self, int* before, int* after)
    {
        *before = *after = 0;
        return 0;
    }
}
