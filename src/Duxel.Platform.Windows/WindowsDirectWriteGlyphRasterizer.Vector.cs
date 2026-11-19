using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsDirectWriteGlyphRasterizer
{
    private readonly record struct VectorGlyphKey(string FontPath, ushort GlyphIndex, float Size, float Tolerance);
    private readonly record struct VectorGlyph(UiVector2[] Points, uint[] Indices, float Ascent, PlatformTextGlyphOutline Outline);
    private static readonly Dictionary<VectorGlyphKey, VectorGlyph> VectorGlyphs = [];
    private static readonly Queue<VectorGlyphKey> VectorGlyphOrder = [];
    private const int VectorGlyphCapacity = 2048;

    internal static unsafe PlatformTextMeshResult CreateTextMesh(in PlatformTextMeshRequest request)
    {
        if (!float.IsFinite(request.FontSize) || request.FontSize <= 0
            || !float.IsFinite(request.FlatteningTolerance) || request.FlatteningTolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        lock (Sync)
        {
            var glyphs = new List<PlatformTextGlyphMesh>();
            var y = 0f;
            var width = 0f;
            var baseline = 0f;
            nint factory = 0;
            try
            {
                var lines = request.Text.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\t", "    ", StringComparison.Ordinal).Split('\n');
                foreach (var line in lines)
                {
                    if (line.Length == 0)
                    {
                        y += request.FontSize;
                        continue;
                    }
                    var paragraph = ShapeContextualParagraph(request.FontPath, request.SecondaryFontPath, line, request.FontSize);
                    foreach (var run in paragraph.Runs)
                    {
                        if (!TryGetFontFace(run.FontPath, out var face))
                            throw new InvalidOperationException("Cannot load the selected vector font face.");
                        foreach (var positioned in run.Glyphs)
                        {
                            var key = new VectorGlyphKey(run.FontPath, positioned.Index, request.FontSize, request.FlatteningTolerance);
                            if (!VectorGlyphs.TryGetValue(key, out var glyph))
                            {
                                if (factory == 0)
                                {
                                    var factoryId = new Guid("06152247-6F50-465A-9245-118BFD3B6007");
                                    Marshal.ThrowExceptionForHR(D2D1CreateFactory(0, &factoryId, null, &factory));
                                }
                                glyph = CreateVectorGlyph(factory, face, positioned.Index, request.FontSize, request.FlatteningTolerance);
                                if (VectorGlyphs.Count == VectorGlyphCapacity)
                                    VectorGlyphs.Remove(VectorGlyphOrder.Dequeue());
                                VectorGlyphs.Add(key, glyph);
                                VectorGlyphOrder.Enqueue(key);
                            }
                            baseline = MathF.Max(baseline, paragraph.Baseline);
                            glyphs.Add(new(glyph.Points, glyph.Indices, new(positioned.X, y + positioned.Y))
                            {
                                Outline = glyph.Outline
                            });
                        }
                    }
                    width = MathF.Max(width, paragraph.Advance);
                    y += request.FontSize;
                }
                return new(ReadOnlyMemory<UiVector2>.Empty, width, y, baseline)
                {
                    Glyphs = glyphs.ToArray()
                };
            }
            finally { if (factory != 0) Release(factory); }
        }
    }

    private static unsafe VectorGlyph CreateVectorGlyph(nint factory, nint face, ushort glyph, float size, float tolerance)
    {
        nint geometry = 0, geometrySink = 0;
        var state = new VectorTriangleState();
        var sink = VectorTriangleSink.Create(state);
        try
        {
            // Slots follow d2d1.h: factory CreatePathGeometry=10; path Open=17;
            // geometry Tessellate=10; simplified geometry sink Close=9.
            var factoryMethods = *(void***)factory;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)factoryMethods[10])(factory, &geometry));
            var geometryMethods = *(void***)geometry;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)geometryMethods[17])(geometry, &geometrySink));
            var faceMethods = *(void***)face;
            // IDWriteFontFace::GetGlyphRunOutline is slot 14 (dwrite.h).
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, ushort*, float*, void*, uint, int, int, nint, int>)faceMethods[14])
                (face, size, &glyph, null, null, 1, 0, 0, geometrySink));
            var sinkMethods = *(void***)geometrySink;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int>)sinkMethods[9])(geometrySink));
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, void*, float, VectorTriangleSink*, int>)geometryMethods[10])
                (geometry, null, tolerance, sink));
            state.Failure?.Throw();
            GetMethod<GetFontMetricsDelegate>(face, 8)(face, out var metrics);
            return new(state.Points.ToArray(), state.Indices.ToArray(), metrics.Ascent * size / metrics.DesignUnitsPerEm,
                CaptureGlyphOutline(face, glyph, size));
        }
        finally
        {
            VectorTriangleSink.ReleaseOwned(sink);
            if (geometrySink != 0)
                Release(geometrySink);
            if (geometry != 0)
                Release(geometry);
        }
    }

    private sealed class VectorTriangleState
    {
        public readonly List<UiVector2> Points = [];
        public readonly List<uint> Indices = [];
        public readonly Dictionary<UiVector2, uint> VertexIndices = [];
        public ExceptionDispatchInfo? Failure;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct VectorTriangleSink
    {
        public void** Methods;
        public nint StateHandle;
        public int References;
        private static readonly void** VTable = CreateVTable();

        private static void** CreateVTable()
        {
            var methods = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(VectorTriangleSink), 5 * sizeof(nint));
            methods[0] = (delegate* unmanaged[Stdcall]<VectorTriangleSink*, Guid*, void**, int>)&QueryInterface;
            methods[1] = (delegate* unmanaged[Stdcall]<VectorTriangleSink*, uint>)&AddRef;
            methods[2] = (delegate* unmanaged[Stdcall]<VectorTriangleSink*, uint>)&ReleaseRef;
            methods[3] = (delegate* unmanaged[Stdcall]<VectorTriangleSink*, float*, uint, void>)&AddTriangles;
            methods[4] = (delegate* unmanaged[Stdcall]<VectorTriangleSink*, int>)&Close;
            return methods;
        }

        public static VectorTriangleSink* Create(VectorTriangleState state)
        {
            var sink = (VectorTriangleSink*)NativeMemory.AllocZeroed((nuint)sizeof(VectorTriangleSink));
            sink->Methods = VTable;
            sink->StateHandle = GCHandle.ToIntPtr(GCHandle.Alloc(state));
            sink->References = 1;
            return sink;
        }

        public static uint ReleaseOwned(VectorTriangleSink* sink)
        {
            var references = Interlocked.Decrement(ref sink->References);
            if (references == 0)
            {
                GCHandle.FromIntPtr(sink->StateHandle).Free();
                NativeMemory.Free(sink);
            }
            return (uint)references;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int QueryInterface(VectorTriangleSink* self, Guid* iid, void** result)
        {
            *result = null;
            if (*iid != new Guid("00000000-0000-0000-C000-000000000046")
                && *iid != new Guid("2CD906C1-12E2-11DC-9FED-001143A055F9"))
                return unchecked((int)0x80004002);
            Interlocked.Increment(ref self->References);
            *result = self;
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint AddRef(VectorTriangleSink* self) => (uint)Interlocked.Increment(ref self->References);
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint ReleaseRef(VectorTriangleSink* self) => ReleaseOwned(self);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static void AddTriangles(VectorTriangleSink* self, float* triangles, uint count)
        {
            var state = (VectorTriangleState)GCHandle.FromIntPtr(self->StateHandle).Target!;
            if (state.Failure is not null)
                return;
            try
            {
                for (var point = 0u; point < count * 3; point++)
                {
                    var position = new UiVector2(triangles[point * 2], triangles[point * 2 + 1]);
                    if (!state.VertexIndices.TryGetValue(position, out var index))
                    {
                        index = (uint)state.Points.Count;
                        state.VertexIndices.Add(position, index);
                        state.Points.Add(position);
                    }
                    state.Indices.Add(index);
                }
            }
            catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Close(VectorTriangleSink* self)
            => ((VectorTriangleState)GCHandle.FromIntPtr(self->StateHandle).Target!).Failure is null ? 0 : unchecked((int)0x80004005);
    }

    [LibraryImport("d2d1.dll")]
    private static unsafe partial int D2D1CreateFactory(int factoryType, Guid* iid, void* options, nint* factory);
}
