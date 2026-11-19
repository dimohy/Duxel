using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Duxel.Interop.Windows;

// Shared source for explicit-font Windows text and SVG. The caller owns both COM objects.
internal static unsafe class DirectWriteTextShaper
{
    internal readonly record struct Glyph(ushort Index, uint Cluster, float Advance, float OffsetX, float OffsetY);
    [StructLayout(LayoutKind.Sequential)]
    private struct ScriptAnalysis
    {
        internal ushort Script; internal int Shapes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphOffset
    {
        internal float Advance, Ascender;
    }
    private readonly record struct Run(uint Start, uint Length, ScriptAnalysis Script, byte Level);
    private sealed class Analysis(char* text, uint length, bool rtl)
    {
        internal readonly char* Text = text;
        internal readonly uint Length = length;
        internal readonly bool RightToLeft = rtl;
        internal readonly List<Run> Scripts = [];
        internal readonly byte[] Levels = new byte[length];
        internal ExceptionDispatchInfo? Failure;
    }

    internal static Glyph[] Shape(nint factory, nint analyzer, nint face, string text, float size, bool rightToLeft = false)
    {
        if (text.Length == 0)
            return [];
        var input = DirectWriteHangulInput.Prepare(text);
        text = input.Text;
        if (text.Length > 16_384)
        {
            var longGlyphs = DirectWriteLongTextShaper.Shape(factory, face, text, size, rightToLeft);
            if (input.OriginalPositions is { } original)
                for (var index = 0; index < longGlyphs.Length; index++)
                    longGlyphs[index] = longGlyphs[index] with
                    {
                        Cluster = original[longGlyphs[index].Cluster]
                    };
            return longGlyphs;
        }
        List<Glyph> result = [];
        fixed (char* pointer = text)
        {
            var state = new Analysis(pointer, checked((uint)text.Length), rightToLeft);
            var source = CreateCallback(state, false);
            Callback* sink = null;
            try
            {
                sink = CreateCallback(state, true);
                Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, nint, int>)Method(analyzer, 3))
                    (analyzer, (nint)source, 0, state.Length, (nint)sink), state);
                Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, nint, int>)Method(analyzer, 4))
                    (analyzer, (nint)source, 0, state.Length, (nint)sink), state);
                var runs = new List<Run>();
                uint end = 0;
                foreach (var script in state.Scripts.OrderBy(run => run.Start))
                {
                    if (script.Start != end || script.Length == 0 || script.Length > state.Length - end)
                        throw new InvalidDataException("DirectWrite script analysis must cover the complete shaping input.");
                    end += script.Length;
                    for (var start = script.Start; start < end;)
                    {
                        var next = start + 1;
                        while (next < end && state.Levels[next] == state.Levels[start])
                            next++;
                        runs.Add(new(start, next - start, script.Script, state.Levels[start]));
                        start = next;
                    }
                }
                if (end != state.Length)
                    throw new InvalidDataException("DirectWrite script analysis omitted source characters.");
                // Unicode L2: reverse contiguous runs at descending embedding levels.
                var maximum = runs.Max(run => run.Level);
                for (var level = (int)maximum; level >= 1; level--)
                {
                    for (var start = 0; start < runs.Count;)
                    {
                        if (runs[start].Level < level)
                        {
                            start++;
                            continue;
                        }
                        var next = start + 1;
                        while (next < runs.Count && runs[next].Level >= level)
                            next++;
                        runs.Reverse(start, next - start);
                        start = next;
                    }
                }
                foreach (var run in runs)
                    ShapeRun(analyzer, face, pointer + run.Start, run, size, result);
            }
            finally
            {
                if (sink != null)
                    ReleaseOwned(sink);
                ReleaseOwned(source);
            }
        }
        var glyphs = result.ToArray();
        if (input.OriginalPositions is { } positions)
            for (var index = 0; index < glyphs.Length; index++)
                glyphs[index] = glyphs[index] with
                {
                    Cluster = positions[glyphs[index].Cluster]
                };
        return glyphs;
    }

    private static void ShapeRun(nint analyzer, nint face, char* text, Run run, float size, List<Glyph> result)
    {
        // DirectWrite cluster indices are UINT16. Never split a shaping cluster to fit a buffer.
        var capacity = (int)Math.Min(65_535L, (long)run.Length * 3 / 2 + 16);
        var clusters = new ushort[run.Length];
        var textProperties = new ushort[run.Length];
        var script = run.Script;
        var rtl = (run.Level & 1) != 0;
        while (true)
        {
            var glyphs = new ushort[capacity];
            var properties = new ushort[capacity];
            uint count = 0;
            int status;
            fixed (ushort* cluster = clusters, textProps = textProperties, indices = glyphs, glyphProps = properties)
                status = ((delegate* unmanaged[Stdcall]<nint, char*, uint, nint, int, int, ScriptAnalysis*, char*, nint, nint, uint*, uint, uint,
                    ushort*, ushort*, ushort*, ushort*, uint*, int>)Method(analyzer, 7))
                    (analyzer, text, run.Length, face, 0, rtl ? 1 : 0, &script, null, 0, 0, null, 0, (uint)capacity,
                    cluster, textProps, indices, glyphProps, &count);
            if (status == unchecked((int)0x8007007A) && capacity < 65_535)
            {
                capacity = Math.Min(65_535, checked(capacity * 2));
                continue;
            }
            Marshal.ThrowExceptionForHR(status);
            if (count == 0)
                throw new InvalidDataException("DirectWrite returned no glyphs for a nonempty script run.");
            var advances = new float[count];
            var offsets = new GlyphOffset[count];
            fixed (ushort* cluster = clusters, textProps = textProperties, indices = glyphs, glyphProps = properties)
            fixed (float* glyphAdvances = advances)
            fixed (GlyphOffset* glyphOffsets = offsets)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, ushort*, ushort*, uint, ushort*, ushort*, uint, nint,
                    float, int, int, ScriptAnalysis*, char*, nint, uint*, uint, float*, GlyphOffset*, int>)Method(analyzer, 8))
                    (analyzer, text, cluster, textProps, run.Length, indices, glyphProps, count, face, size, 0, rtl ? 1 : 0,
                    &script, null, 0, null, 0, glyphAdvances, glyphOffsets));
            var source = new uint[count];
            Array.Fill(source, uint.MaxValue);
            for (uint index = 0; index < run.Length; index++)
            {
                if (clusters[index] >= count)
                    throw new InvalidDataException("DirectWrite cluster index exceeds the glyph buffer.");
                source[clusters[index]] = Math.Min(source[clusters[index]], run.Start + index);
            }
            var current = run.Start;
            for (var index = 0; index < count; index++)
            {
                if (source[index] != uint.MaxValue)
                    current = source[index];
                else
                    source[index] = current;
            }
            for (var visual = 0; visual < count; visual++)
            {
                var index = rtl ? (int)count - 1 - visual : visual;
                result.Add(new(glyphs[index], source[index], advances[index],
                    rtl ? -offsets[index].Advance : offsets[index].Advance, -offsets[index].Ascender));
            }
            return;
        }
    }

    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];
    private static void Check(int status, Analysis state)
    {
        state.Failure?.Throw();
        Marshal.ThrowExceptionForHR(status);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Callback
    {
        internal nint* Table; internal nint State; internal int References; internal int IsSink;
    }
    private static readonly nint* SourceTable = CreateTable(false);
    private static readonly nint* SinkTable = CreateTable(true);
    private static readonly char* Locale = CreateLocale();
    private static char* CreateLocale()
    {
        var value = (char*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DirectWriteTextShaper), 8);
        value[0] = 'u';
        value[1] = 'n';
        value[2] = 'd';
        value[3] = '\0';
        return value;
    }
    private static nint* CreateTable(bool sink)
    {
        var table = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DirectWriteTextShaper), (sink ? 7 : 8) * sizeof(nint));
        table[0] = (nint)(delegate* unmanaged[Stdcall]<Callback*, Guid*, void**, int>)&Query;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint>)&AddRef;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint>)&Release;
        if (sink)
        {
            table[3] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, uint, ScriptAnalysis*, int>)&SetScript;
            table[4] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, uint, void*, int>)&SetBreaks;
            table[5] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, uint, byte, byte, int>)&SetBidi;
            table[6] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, uint, nint, int>)&SetSubstitution;
        }
        else
        {
            table[3] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, char**, uint*, int>)&TextAt;
            table[4] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, char**, uint*, int>)&TextBefore;
            table[5] = (nint)(delegate* unmanaged[Stdcall]<Callback*, int>)&ReadingDirection;
            table[6] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, uint*, char**, int>)&GetLocale;
            table[7] = (nint)(delegate* unmanaged[Stdcall]<Callback*, uint, uint*, nint*, int>)&GetSubstitution;
        }
        return table;
    }
    private static Callback* CreateCallback(Analysis state, bool sink)
    {
        var handle = GCHandle.Alloc(state);
        try
        {
            var callback = (Callback*)NativeMemory.AllocZeroed((nuint)sizeof(Callback));
            callback->Table = sink ? SinkTable : SourceTable;
            callback->State = GCHandle.ToIntPtr(handle);
            callback->References = 1;
            callback->IsSink = sink ? 1 : 0;
            return callback;
        }
        catch { handle.Free(); throw; }
    }
    private static Analysis State(Callback* self) => (Analysis)GCHandle.FromIntPtr(self->State).Target!;
    private static uint ReleaseOwned(Callback* self)
    {
        var remaining = Interlocked.Decrement(ref self->References);
        if (remaining == 0)
        {
            GCHandle.FromIntPtr(self->State).Free();
            NativeMemory.Free(self);
        }
        return (uint)remaining;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Query(Callback* self, Guid* iid, void** result)
    {
        *result = null;
        var supported = self->IsSink != 0 ? new Guid("5810cd44-0ca0-4701-b3fa-bec5182ae4f6") : new Guid("688e1a58-5094-47c8-adc8-fbcea60ae92b");
        if (*iid != new Guid("00000000-0000-0000-C000-000000000046") && *iid != supported)
            return unchecked((int)0x80004002);
        Interlocked.Increment(ref self->References);
        *result = self;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(Callback* self) => (uint)Interlocked.Increment(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(Callback* self) => ReleaseOwned(self);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int TextAt(Callback* self, uint position, char** text, uint* length)
    {
        var state = State(self);
        *text = position < state.Length ? state.Text + position : null;
        *length = position < state.Length ? state.Length - position : 0;
        return position <= state.Length ? 0 : unchecked((int)0x80070057);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int TextBefore(Callback* self, uint position, char** text, uint* length)
    {
        var state = State(self);
        *text = position > 0 && position <= state.Length ? state.Text : null;
        *length = position <= state.Length ? position : 0;
        return position <= state.Length ? 0 : unchecked((int)0x80070057);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ReadingDirection(Callback* self) => State(self).RightToLeft ? 1 : 0;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetLocale(Callback* self, uint position, uint* length, char** locale)
    {
        var state = State(self);
        *length = position <= state.Length ? state.Length - position : 0;
        *locale = Locale;
        return position <= state.Length ? 0 : unchecked((int)0x80070057);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetSubstitution(Callback* self, uint position, uint* length, nint* substitution)
    {
        var state = State(self);
        *length = position <= state.Length ? state.Length - position : 0;
        *substitution = 0;
        return position <= state.Length ? 0 : unchecked((int)0x80070057);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetScript(Callback* self, uint position, uint length, ScriptAnalysis* script)
    {
        var state = State(self);
        try
        {
            state.Scripts.Add(new(position, length, *script, 0));
            return 0;
        }
        catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetBidi(Callback* self, uint position, uint length, byte explicitLevel, byte resolvedLevel)
    {
        var state = State(self);
        try
        {
            state.Levels.AsSpan(checked((int)position), checked((int)length)).Fill(resolvedLevel);
            return 0;
        }
        catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetBreaks(Callback* self, uint position, uint length, void* breaks) => 0;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetSubstitution(Callback* self, uint position, uint length, nint substitution) => 0;
}
