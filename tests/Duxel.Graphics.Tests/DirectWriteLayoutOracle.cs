using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

// Independent system TextLayout pipeline; never calls the shared analyzer under test.
internal static unsafe partial class DirectWriteLayoutOracle
{
    internal readonly record struct PositionedGlyph(ushort Index, float X, float Y, float Advance);
    internal readonly record struct RunRaster(int Left, int Top, int Width, int Height, float Baseline, byte[] Pixels);
    private sealed class Capture
    {
        internal readonly List<PositionedGlyph> Glyphs = [];
        internal ExceptionDispatchInfo? Failure;
        internal List<(float X, float Y, bool RightToLeft)>? Objects;
        internal byte[]? Coverage;
        internal nint AnalysisFactory;
        internal bool AbsoluteParagraphPositions;
        internal List<RunRaster>? Rasters;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Offset
    {
        internal float X, Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphRun
    {
        internal nint Face;
        internal float Size;
        internal uint Count;
        internal ushort* Indices;
        internal float* Advances;
        internal Offset* Offsets;
        internal int Sideways;
        internal uint BidiLevel;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Renderer
    {
        internal void** Table; internal nint Capture; internal int References;
    }
    private static readonly void** Table = CreateTable();
    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];

    internal static PositionedGlyph[] Draw(string family, string text, bool rtl, Action<nint>? inspectLayout = null, float size = 32,
        Action<nint>? configureLayout = null, byte[]? coverage = null, bool absoluteParagraphPositions = false,
        List<RunRaster>? rasters = null, List<(float X, float Y, bool RightToLeft)>? objects = null)
    {
        nint factory = 0, format = 0, layout = 0;
        Renderer* renderer = null;
        var capture = new Capture();
        capture.Objects = objects;
        capture.Coverage = coverage;
        capture.AbsoluteParagraphPositions = absoluteParagraphPositions;
        capture.Rasters = rasters;
        var handle = GCHandle.Alloc(capture);
        try
        {
            var iid = new Guid("B859EE5A-D838-4B5B-A2E8-1ADC7D93DB48");
            Marshal.ThrowExceptionForHR(DWriteCreateFactory(1, &iid, &factory));
            fixed (char* name = family)
            fixed (char* locale = "und")
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, nint, int, int, int, float, char*, nint*, int>)Method(factory, 15))
                    (factory, name, 0, 400, 0, 5, size, locale, &format));
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(format, 6))(format, rtl ? 1 : 0));
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(format, 3))(format, rtl ? 1 : 0));
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(format, 5))(format, 1));
            fixed (char* source = text)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, uint, nint, float, float, nint*, int>)Method(factory, 18))
                    (factory, source, (uint)text.Length, format, 4096, 512, &layout));
            configureLayout?.Invoke(layout);
            if (coverage is not null || rasters is not null)
            {
                var analysisId = new Guid("0439FC60-CA44-4994-8DEE-3A9AF7B732EC");
                nint analysisFactory = 0;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Method(factory, 0))(factory, &analysisId, &analysisFactory));
                capture.AnalysisFactory = analysisFactory;
            }
            renderer = (Renderer*)NativeMemory.AllocZeroed((nuint)sizeof(Renderer));
            renderer->Table = Table;
            renderer->Capture = GCHandle.ToIntPtr(handle);
            renderer->References = 1;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, Renderer*, float, float, int>)Method(layout, 58))
                (layout, 0, renderer, 0, 0));
            capture.Failure?.Throw();
            inspectLayout?.Invoke(layout);
            return capture.Glyphs.ToArray();
        }
        finally
        {
            if (capture.AnalysisFactory != 0)
                Marshal.Release(capture.AnalysisFactory);
            if (layout != 0)
                Marshal.Release(layout);
            if (format != 0)
                Marshal.Release(format);
            if (factory != 0)
                Marshal.Release(factory);
            if (renderer != null)
            {
                if (renderer->References != 1)
                    throw new InvalidOperationException("TextLayout retained the test renderer after Draw and disposal.");
                NativeMemory.Free(renderer);
            }
            handle.Free();
        }
    }

    private static void** CreateTable()
    {
        var table = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DirectWriteLayoutOracle), 10 * sizeof(nint));
        table[0] = (delegate* unmanaged[Stdcall]<Renderer*, Guid*, void**, int>)&Query;
        table[1] = (delegate* unmanaged[Stdcall]<Renderer*, uint>)&AddRef;
        table[2] = (delegate* unmanaged[Stdcall]<Renderer*, uint>)&Release;
        table[3] = (delegate* unmanaged[Stdcall]<Renderer*, nint, int*, int>)&Snapping;
        table[4] = (delegate* unmanaged[Stdcall]<Renderer*, nint, float*, int>)&Transform;
        table[5] = (delegate* unmanaged[Stdcall]<Renderer*, nint, float*, int>)&PixelsPerDip;
        table[6] = (delegate* unmanaged[Stdcall]<Renderer*, nint, float, float, int, GlyphRun*, nint, nint, int>)&DrawGlyphRun;
        table[7] = (delegate* unmanaged[Stdcall]<Renderer*, nint, float, float, nint, nint, int>)&Decoration;
        table[8] = (delegate* unmanaged[Stdcall]<Renderer*, nint, float, float, nint, nint, int>)&Decoration;
        table[9] = (delegate* unmanaged[Stdcall]<Renderer*, nint, float, float, nint, int, int, nint, int>)&InlineObject;
        return table;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Query(Renderer* self, Guid* iid, void** result)
    {
        *result = null;
        if (*iid != new Guid("00000000-0000-0000-C000-000000000046")
            && *iid != new Guid("ef8a8135-5cc6-45fe-8825-c5a0724eb819")
            && *iid != new Guid("eaf3a2da-ecf4-4d24-b644-b34f6842024b"))
            return unchecked((int)0x80004002);
        Interlocked.Increment(ref self->References);
        *result = self;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(Renderer* self) => (uint)Interlocked.Increment(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(Renderer* self) => (uint)Interlocked.Decrement(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Snapping(Renderer* self, nint context, int* disabled)
    {
        *disabled = 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int PixelsPerDip(Renderer* self, nint context, float* value)
    {
        *value = 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Transform(Renderer* self, nint context, float* matrix)
    {
        new Span<float>(matrix, 6).Clear();
        matrix[0] = matrix[3] = 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int DrawGlyphRun(Renderer* self, nint context, float x, float y, int measuringMode, GlyphRun* run, nint description, nint effect)
    {
        var capture = (Capture)GCHandle.FromIntPtr(self->Capture).Target!;
        try
        {
            if (capture.Coverage is not null || capture.Rasters is not null)
                CaptureCoverage(capture, x, capture.AbsoluteParagraphPositions ? y : 0, run);
            var rtl = (run->BidiLevel & 1) != 0;
            for (var index = 0; index < run->Count; index++)
            {
                var advance = run->Advances[index];
                var offset = run->Offsets == null ? default : run->Offsets[index];
                if (rtl)
                    x -= advance;
                capture.Glyphs.Add(new(run->Indices[index], x + (rtl ? -offset.X : offset.X), (capture.AbsoluteParagraphPositions ? y : 0) - offset.Y, advance));
                if (!rtl)
                    x += advance;
            }
            return 0;
        }
        catch (Exception error) { capture.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Decoration(Renderer* self, nint context, float x, float y, nint decoration, nint effect) => unchecked((int)0x80004001);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InlineObject(Renderer* self, nint context, float x, float y, nint inline, int sideways, int rtl, nint effect)
    {
        var capture = (Capture)GCHandle.FromIntPtr(self->Capture).Target!;
        if (capture.Objects is null || sideways != 0)
            return unchecked((int)0x80004001);
        try
        {
            capture.Objects.Add((x, y, rtl != 0));
            return 0;
        }
        catch (Exception error) { capture.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
    [LibraryImport("dwrite.dll")]
    private static partial int DWriteCreateFactory(int factoryType, Guid* iid, nint* factory);
}
