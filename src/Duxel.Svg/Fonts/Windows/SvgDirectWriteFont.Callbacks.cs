using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal sealed unsafe partial class SvgDirectWriteFont
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal float X, Y;
        internal readonly Vector2 Vector => new(X, Y);
    }
    private sealed class OutlineState
    {
        internal List<SvgSegment> Segments { get; } = [];
        internal Vector2 First;
        internal ExceptionDispatchInfo? Failure;
        internal void Add(SvgSegment value)
        {
            if (Failure is not null)
                return;
            try
            {
                if (Segments.Count == 1_000_000)
                    throw new InvalidDataException("SVG glyph exceeds the outline segment limit.");
                Segments.Add(value);
            }
            catch (Exception error) { Failure = ExceptionDispatchInfo.Capture(error); }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct CallbackObject
    {
        internal nint* Table; internal nint State; internal int References;
    }
    private static readonly nint* OutlineTable = CallbackTable();
    private static nint* CallbackTable()
    {
        var table = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(SvgDirectWriteFont), 10 * sizeof(nint));
        table[0] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, Guid*, void**, int>)&Query;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, uint>)&AddReference;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, uint>)&ReleaseReference;
        table[3] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, int, void>)&SetFillMode;
        table[4] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, int, void>)&SetSegmentFlags;
        table[5] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, NativePoint, int, void>)&BeginFigure;
        table[6] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, NativePoint*, uint, void>)&AddLines;
        table[7] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, NativePoint*, uint, void>)&AddBeziers;
        table[8] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, int, void>)&EndFigure;
        table[9] = (nint)(delegate* unmanaged[Stdcall]<CallbackObject*, int>)&CloseOutline;
        return table;
    }
    private static CallbackObject* Callback(object state)
    {
        var handle = GCHandle.Alloc(state);
        try
        {
            var value = (CallbackObject*)NativeMemory.AllocZeroed((nuint)sizeof(CallbackObject));
            value->Table = OutlineTable;
            value->State = GCHandle.ToIntPtr(handle);
            value->References = 1;
            return value;
        }
        catch { handle.Free(); throw; }
    }
    private static T State<T>(CallbackObject* self) where T : class => (T)GCHandle.FromIntPtr(self->State).Target!;
    private static uint ReleaseCallback(CallbackObject* self)
    {
        var references = Interlocked.Decrement(ref self->References);
        if (references == 0)
        {
            GCHandle.FromIntPtr(self->State).Free();
            NativeMemory.Free(self);
        }
        return (uint)references;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Query(CallbackObject* self, Guid* iid, void** result)
    {
        *result = null;
        var supported = new Guid("2cd9069e-12e2-11dc-9fed-001143a055f9");
        if (*iid != new Guid("00000000-0000-0000-C000-000000000046") && *iid != supported)
            return unchecked((int)0x80004002);
        Interlocked.Increment(ref self->References);
        *result = self;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static uint AddReference(CallbackObject* self) => (uint)Interlocked.Increment(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static uint ReleaseReference(CallbackObject* self) => ReleaseCallback(self);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void SetFillMode(CallbackObject* self, int fillMode)
    {
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void SetSegmentFlags(CallbackObject* self, int flags)
    {
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void BeginFigure(CallbackObject* self, NativePoint start, int begin)
    {
        var state = State<OutlineState>(self);
        state.First = start.Vector;
        state.Add(new(SvgSegmentKind.Move, start.Vector));
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void AddLines(CallbackObject* self, NativePoint* points, uint count)
    {
        var state = State<OutlineState>(self);
        for (var i = 0u; i < count; i++)
            state.Add(new(SvgSegmentKind.Line, points[i].Vector));
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void AddBeziers(CallbackObject* self, NativePoint* points, uint count)
    {
        var state = State<OutlineState>(self);
        for (var i = 0u; i < count; i++)
            state.Add(new(SvgSegmentKind.Cubic, points[i * 3 + 2].Vector, points[i * 3].Vector, points[i * 3 + 1].Vector));
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void EndFigure(CallbackObject* self, int end)
    {
        var state = State<OutlineState>(self);
        if (end == 1)
            state.Add(new(SvgSegmentKind.Close, state.First));
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CloseOutline(CallbackObject* self) => State<OutlineState>(self).Failure?.SourceException.HResult ?? 0;
}
