using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe partial class StyledParagraphTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct OracleObject
    {
        internal void** Table;
        internal int References;
        internal float Width, Height, Baseline;
    }
    private static readonly void** OracleObjectTable = CreateOracleObjectTable();
    private static void** CreateOracleObjectTable()
    {
        var table = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(StyledParagraphTests), 7 * sizeof(nint));
        table[0] = (delegate* unmanaged[Stdcall]<OracleObject*, Guid*, void**, int>)&ObjectQuery;
        table[1] = (delegate* unmanaged[Stdcall]<OracleObject*, uint>)&ObjectAddRef;
        table[2] = (delegate* unmanaged[Stdcall]<OracleObject*, uint>)&ObjectRelease;
        table[3] = (delegate* unmanaged[Stdcall]<OracleObject*, nint, nint, float, float, int, int, nint, int>)&ObjectDraw;
        table[4] = (delegate* unmanaged[Stdcall]<OracleObject*, float*, int>)&ObjectMetrics;
        table[5] = (delegate* unmanaged[Stdcall]<OracleObject*, float*, int>)&ObjectOverhang;
        table[6] = (delegate* unmanaged[Stdcall]<OracleObject*, int*, int*, int>)&ObjectBreaks;
        return table;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ObjectQuery(OracleObject* self, Guid* iid, void** result)
    {
        *result = null;
        if (*iid != new Guid("8339fde3-106f-47ab-8373-1c6295eb10b3") && *iid != new Guid("00000000-0000-0000-c000-000000000046"))
            return unchecked((int)0x80004002);
        Interlocked.Increment(ref self->References);
        *result = self;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint ObjectAddRef(OracleObject* self) => (uint)Interlocked.Increment(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint ObjectRelease(OracleObject* self) => (uint)Interlocked.Decrement(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ObjectDraw(OracleObject* self, nint context, nint renderer, float x, float y, int sideways, int rtl, nint effect)
        => unchecked((int)0x80004001);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ObjectMetrics(OracleObject* self, float* output)
    {
        output[0] = self->Width;
        output[1] = self->Height;
        output[2] = self->Baseline;
        *(int*)(output + 3) = 0;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ObjectOverhang(OracleObject* self, float* output)
    {
        for (var index = 0; index < 4; index++)
            output[index] = 0;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ObjectBreaks(OracleObject* self, int* before, int* after)
    {
        *before = *after = 0;
        return 0;
    }

    private static void RunObjects(Action<bool, string> check)
    {
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        var cases = new (string Text, PlatformTextParagraphObject[] Objects)[]
        {
            ("abc \uFFFC אב \uFFFC 123", [new(4, 26.5f, 31, 23), new(9, 11.25f, 8, 3)]),
            ("\uFFFC", [new(0, 0, 31, 0)]),
            ("\uFFFC\uFFFC", [new(0, 26.5f, 31, 31), new(1, 11.25f, 8, 3)]),
            ("a\uFFFCb\uFFFCc", [new(1, 5, 0, 5.25f), new(3, 5, 4, -2.5f)])
        };
        foreach (var (text, objects) in cases)
            foreach (var rtl in new[] { false, true })
                foreach (var width in new[] { 640f, 65f })
                {
                    var carets = new PlatformTextParagraphCaret[text.Length + 1];
                    uint expectedLines = 0;
                    var placements = new List<(float X, float Y, bool RightToLeft)>();
                    var allocations = new nint[objects.Length];
                    try
                    {
                        for (var index = 0; index < objects.Length; index++)
                        {
                            var item = objects[index];
                            var native = (OracleObject*)NativeMemory.AllocZeroed((nuint)sizeof(OracleObject));
                            *native = new()
                            {
                                Table = OracleObjectTable,
                                References = 1,
                                Width = item.Width,
                                Height = item.Height,
                                Baseline = item.Baseline
                            };
                            allocations[index] = (nint)native;
                        }
                        var expected = DirectWriteLayoutOracle.Draw("Segoe UI", text, rtl, layout =>
                        {
                            uint count = 0;
                            var status = ((delegate* unmanaged[Stdcall]<nint, void*, uint, uint*, int>)Method(layout, 59))(layout, null, 0, &count);
                            if (status != unchecked((int)0x8007007A))
                                Marshal.ThrowExceptionForHR(status);
                            expectedLines = count;
                            for (var index = 0; index < carets.Length; index++)
                            {
                                float x = 0, y = 0;
                                HitMetrics hit = default;
                                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                                    (layout, (uint)index, 0, &x, &y, &hit));
                                carets[index] = new(x, y);
                            }
                        }, 14, layout =>
                        {
                            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, width));
                            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
                            for (var index = 0; index < objects.Length; index++)
                                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, TextRange, int>)Method(layout, 39))
                                    (layout, allocations[index], new((uint)objects[index].Start, 1)));
                        }, absoluteParagraphPositions: true, objects: placements);
                        var request = new PlatformTextParagraphRequest(text, [new(0, text.Length, font, null, 14)], objects, width,
                            rtl ? PlatformTextParagraphDirection.RightToLeft : PlatformTextParagraphDirection.LeftToRight);
                        var actual = ((IPlatformTextParagraphBackend)WindowsPlatformTextBackend.Instance).CreateParagraph(request, false);
                        var label = $"{width} / RTL = {rtl} / source length = {text.Length}";
                        check(actual.Carets.Span.SequenceEqual(carets), "Atomic paragraph carets equal independent native layout : " + label);
                        check(actual.Objects.Length == objects.Length && actual.Objects.ToArray().All(item => placements.Any(native =>
                            native.X == item.Bounds.X && native.Y == item.Bounds.Y && native.RightToLeft == item.RightToLeft))
                            && actual.Objects.ToArray().Zip(objects).All(pair => pair.First.Start == pair.Second.Start
                                && pair.First.Bounds.Width == pair.Second.Width && pair.First.Bounds.Height == pair.Second.Height),
                            "Atomic paragraph allocations preserve native top-left and source positions : " + label);
                        var glyphs = actual.Runs.ToArray().SelectMany(run => run.Glyphs.ToArray()).OrderBy(item => item.Y).ThenBy(item => item.X).ThenBy(item => item.Index).ToArray();
                        var oracle = expected.OrderBy(item => item.Y).ThenBy(item => item.X).ThenBy(item => item.Index).ToArray();
                        check(glyphs.Length == oracle.Length && glyphs.Zip(oracle).All(pair => pair.First.Index == pair.Second.Index
                            && MathF.Abs(pair.First.X - pair.Second.X) < .002f && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f)
                            && glyphs.All(item => objects.All(atomic => item.SourcePosition != atomic.Start)),
                            "Atomic paragraph suppresses object glyphs while preserving surrounding native text : " + label);
                        check(actual.Lines.ToArray().Sum(item => item.Length + item.NewlineLength) == text.Length
                        && actual.Lines.Length == expectedLines,
                            "Atomic paragraph wraps measured objects and preserves complete source : " + label);
                    }
                    finally
                    {
                        foreach (var allocation in allocations)
                        {
                            var native = (OracleObject*)allocation;
                            if (native == null)
                                continue;
                            var references = native->References;
                            NativeMemory.Free(native);
                            if (references != 1)
                                throw new InvalidOperationException("Independent layout retained an inline object.");
                        }
                    }
                }
    }
}
