using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsDirectWriteGlyphRasterizer
{
    // IDWriteGeometrySink aliases ID2D1SimplifiedGeometrySink. Slots and layouts:
    // https://learn.microsoft.com/windows/win32/api/d2d1/nn-d2d1-id2d1simplifiedgeometrysink
    private static unsafe PlatformTextGlyphOutline CaptureGlyphOutline(nint face, ushort glyph, float size)
    {
        var state = new GlyphOutlineState();
        var sink = GlyphOutlineSink.Create(state);
        try
        {
            var methods = *(void***)face;
            var result = ((delegate* unmanaged[Stdcall]<nint, float, ushort*, float*, void*, uint, int, int, GlyphOutlineSink*, int>)methods[14])
                (face, size, &glyph, null, null, 1, 0, 0, sink);
            state.Failure?.Throw();
            Marshal.ThrowExceptionForHR(result);
            var close = ((delegate* unmanaged[Stdcall]<GlyphOutlineSink*, int>)sink->Methods[9])(sink);
            state.Failure?.Throw();
            Marshal.ThrowExceptionForHR(close);
            return new(state.Curves.ToArray(), state.EvenOdd);
        }
        finally { GlyphOutlineSink.ReleaseOwned(sink); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct OutlinePoint(float x, float y)
    {
        internal readonly float X = x;
        internal readonly float Y = y;
        internal UiVector2 Value => new(X, Y);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct OutlineBezier
    {
        internal OutlinePoint Control1, Control2, End;
    }
    private sealed class GlyphOutlineState
    {
        internal readonly List<UiVectorCurve> Curves = [];
        // Direct2D's documented default is D2D1_FILL_MODE_ALTERNATE.
        internal bool EvenOdd = true;
        internal bool Open, Begun, Closed;
        internal UiVector2 First, Current;
        internal ExceptionDispatchInfo? Failure;
        internal void Line(UiVector2 end)
        {
            if (!Open)
                throw new InvalidOperationException("A glyph segment requires an open contour.");
            if (Current != end)
                Curves.Add(UiVectorCurve.Line(Current, end));
            Current = end;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct GlyphOutlineSink
    {
        internal void** Methods;
        internal nint StateHandle;
        internal int References;
        private static readonly void** VTable = CreateVTable();
        private static void** CreateVTable()
        {
            var methods = (void**)NativeMemory.Alloc((nuint)(10 * sizeof(void*)));
            methods[0] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, Guid*, void**, int>)&QueryInterface;
            methods[1] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, uint>)&AddRef;
            methods[2] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, uint>)&ReleaseRef;
            methods[3] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, int, void>)&SetFillMode;
            methods[4] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, int, void>)&SetSegmentFlags;
            methods[5] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, OutlinePoint, int, void>)&BeginFigure;
            methods[6] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, OutlinePoint*, uint, void>)&AddLines;
            methods[7] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, OutlineBezier*, uint, void>)&AddBeziers;
            methods[8] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, int, void>)&EndFigure;
            methods[9] = (delegate* unmanaged[Stdcall]<GlyphOutlineSink*, int>)&Close;
            return methods;
        }
        internal static GlyphOutlineSink* Create(GlyphOutlineState state)
        {
            var sink = (GlyphOutlineSink*)NativeMemory.AllocZeroed((nuint)sizeof(GlyphOutlineSink));
            try
            {
                sink->StateHandle = GCHandle.ToIntPtr(GCHandle.Alloc(state));
            }
            catch { NativeMemory.Free(sink); throw; }
            sink->Methods = VTable;
            sink->References = 1;
            return sink;
        }
        internal static uint ReleaseOwned(GlyphOutlineSink* sink)
        {
            var count = Interlocked.Decrement(ref sink->References);
            if (count == 0)
            {
                GCHandle.FromIntPtr(sink->StateHandle).Free();
                NativeMemory.Free(sink);
            }
            return (uint)count;
        }
        private static GlyphOutlineState State(GlyphOutlineSink* self) => (GlyphOutlineState)GCHandle.FromIntPtr(self->StateHandle).Target!;
        private static void Dispatch(GlyphOutlineSink* self, int operation, OutlinePoint point = default, OutlineBezier curve = default, int value = 0)
        {
            var state = State(self);
            if (state.Failure is not null)
                return;
            try
            {
                if (state.Closed)
                    throw new InvalidOperationException("The glyph outline sink is closed.");
                switch (operation)
                {
                    case 0:
                        if (state.Begun || (uint)value > 1)
                            throw new InvalidOperationException("Invalid glyph fill mode.");
                        state.EvenOdd = value == 0;
                        break;
                    case 1:
                        if (state.Open || value != 0)
                            throw new NotSupportedException("Glyph fill contours must be filled and nonnested.");
                        state.First = state.Current = point.Value;
                        state.Open = state.Begun = true;
                        break;
                    case 2:
                        state.Line(point.Value);
                        break;
                    case 3:
                        if (!state.Open)
                            throw new InvalidOperationException("A glyph curve requires an open contour.");
                        state.Curves.Add(new(state.Current, curve.Control1.Value, curve.Control2.Value, curve.End.Value));
                        state.Current = curve.End.Value;
                        break;
                    case 4:
                        if (!state.Open || value != 1)
                            throw new NotSupportedException("Glyph fill contours must close.");
                        state.Line(state.First);
                        state.Open = false;
                        break;
                }
            }
            catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); }
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int QueryInterface(GlyphOutlineSink* self, Guid* iid, void** result)
        {
            if (result == null)
                return unchecked((int)0x80004003);
            *result = null;
            if (iid == null || *iid != new Guid("00000000-0000-0000-C000-000000000046")
                && *iid != new Guid("2CD9069E-12E2-11DC-9FED-001143A055F9"))
                return unchecked((int)0x80004002);
            Interlocked.Increment(ref self->References);
            *result = self;
            return 0;
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint AddRef(GlyphOutlineSink* self) => (uint)Interlocked.Increment(ref self->References);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint ReleaseRef(GlyphOutlineSink* self) => ReleaseOwned(self);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void SetFillMode(GlyphOutlineSink* self, int value) => Dispatch(self, 0, value: value);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void SetSegmentFlags(GlyphOutlineSink* self, int value)
        {
        } // Stroke flags do not affect glyph fills.
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void BeginFigure(GlyphOutlineSink* self, OutlinePoint point, int value) => Dispatch(self, 1, point, value: value);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void AddLines(GlyphOutlineSink* self, OutlinePoint* points, uint count)
        {
            for (var i = 0u; i < count; i++)
                Dispatch(self, 2, points[i]);
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void AddBeziers(GlyphOutlineSink* self, OutlineBezier* curves, uint count)
        {
            for (var i = 0u; i < count; i++)
                Dispatch(self, 3, curve: curves[i]);
        }
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void EndFigure(GlyphOutlineSink* self, int value) => Dispatch(self, 4, value: value);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Close(GlyphOutlineSink* self)
        {
            var state = State(self);
            if (state.Open && state.Failure is null)
                state.Failure = ExceptionDispatchInfo.Capture(new InvalidOperationException("A glyph contour was not closed."));
            state.Closed = true;
            return state.Failure is null ? 0 : unchecked((int)0x80004005);
        }
    }
}
