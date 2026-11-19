using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Duxel.Interop.Windows;

internal static unsafe partial class DirectWriteLongTextShaper
{
    private abstract class State : IDisposable
    {
        internal ExceptionDispatchInfo? Failure;
        public virtual void Dispose()
        {
        }
    }
    private sealed class FileState : State
    {
        internal nint File;
        internal readonly nint Key;
        internal int Position = -1;
        internal FileState(nint file, nint key)
        {
            AddReference(file);
            File = file;
            Key = key;
        }
        public override void Dispose() => Release(ref File);
    }
    internal readonly record struct Placed(ushort Index, uint Cluster, float Advance, float X, float Y);
    private readonly record struct VisualRun(float Left, float Y, Placed[] Glyphs, int FontSpan, RasterRun Raster, int PaintStyle);
    private sealed class PaintState : State;
    private readonly record struct FontBinding(int Start, int Length, FontIdentity Identity, float Size);
    private sealed class CaptureState(FontIdentity identity, uint length) : State
    {
        internal readonly FontIdentity Identity = identity;
        internal readonly uint Length = length;
        internal readonly List<VisualRun> Runs = [];
        internal FontBinding[]? Fonts;
        internal Dictionary<nint, int>? PaintStyles;
        internal Dictionary<nint, InlineSpan>? InlineObjects;
        internal readonly List<PlacedInline> ObjectPlacements = [];
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeOffset
    {
        internal float Advance, Ascender;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRun
    {
        internal nint Face;
        internal float Size;
        internal uint Count;
        internal ushort* Indices;
        internal float* Advances;
        internal NativeOffset* Offsets;
        internal int Sideways;
        internal uint BidiLevel;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct RunDescription
    {
        internal char* Locale;
        internal char* Text;
        internal uint Length;
        internal ushort* Clusters;
        internal uint Position;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Callback
    {
        internal void** Table; internal nint State; internal int References, Kind;
    }
    private static readonly void** LoaderTable = CreateTable(0);
    private static readonly void** EnumeratorTable = CreateTable(1);
    private static readonly void** RendererTable = CreateTable(2);
    private static readonly void** PaintTable = CreateTable(3);
    private static readonly void** InlineTable = CreateInlineTable();
    private static T GetState<T>(Callback* self) where T : State => (T)GCHandle.FromIntPtr(self->State).Target!;
    private static Callback* CreateCallback(int kind, State state)
    {
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(state);
            var callback = (Callback*)NativeMemory.AllocZeroed((nuint)sizeof(Callback));
            callback->Table = kind == 0 ? LoaderTable : kind == 1 ? EnumeratorTable : kind == 2 ? RendererTable : kind == 3 ? PaintTable : InlineTable;
            callback->State = GCHandle.ToIntPtr(handle);
            callback->References = 1;
            callback->Kind = kind;
            return callback;
        }
        catch
        {
            if (handle.IsAllocated)
                handle.Free();
            state.Dispose();
            throw;
        }
    }
    private static uint ReleaseOwned(Callback* self)
    {
        var count = Interlocked.Decrement(ref self->References);
        if (count == 0)
        {
            var handle = GCHandle.FromIntPtr(self->State);
            ((State)handle.Target!).Dispose();
            handle.Free();
            NativeMemory.Free(self);
        }
        return (uint)count;
    }
    private static void** CreateTable(int kind)
    {
        var table = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(DirectWriteLongTextShaper), (kind == 0 ? 4 : kind == 1 ? 5 : kind == 2 ? 10 : 3) * sizeof(nint));
        table[0] = (delegate* unmanaged[Stdcall]<Callback*, Guid*, void**, int>)&Query;
        table[1] = (delegate* unmanaged[Stdcall]<Callback*, uint>)&AddRef;
        table[2] = (delegate* unmanaged[Stdcall]<Callback*, uint>)&ReleaseRef;
        if (kind == 0)
            table[3] = (delegate* unmanaged[Stdcall]<Callback*, nint, void*, uint, Callback**, int>)&CreateEnumerator;
        else if (kind == 1)
        {
            table[3] = (delegate* unmanaged[Stdcall]<Callback*, int*, int>)&MoveNext;
            table[4] = (delegate* unmanaged[Stdcall]<Callback*, nint*, int>)&CurrentFile;
        }
        else if (kind == 2)
        {
            table[3] = (delegate* unmanaged[Stdcall]<Callback*, nint, int*, int>)&Snapping;
            table[4] = (delegate* unmanaged[Stdcall]<Callback*, nint, float*, int>)&Transform;
            table[5] = (delegate* unmanaged[Stdcall]<Callback*, nint, float*, int>)&PixelsPerDip;
            table[6] = (delegate* unmanaged[Stdcall]<Callback*, nint, float, float, int, NativeRun*, RunDescription*, nint, int>)&DrawGlyphRun;
            table[7] = (delegate* unmanaged[Stdcall]<Callback*, nint, float, float, nint, nint, int>)&Decoration;
            table[8] = (delegate* unmanaged[Stdcall]<Callback*, nint, float, float, nint, nint, int>)&Decoration;
            table[9] = (delegate* unmanaged[Stdcall]<Callback*, nint, float, float, nint, int, int, nint, int>)&InlineObject;
        }
        return table;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Query(Callback* self, Guid* iid, void** result)
    {
        *result = null;
        var supported = self->Kind switch
        {
            0 => new Guid("cca920e4-52f0-492b-bfa8-29c72ee0a468"),
            1 => new Guid("72755049-5ff7-435d-8348-4be97cfa6c7c"),
            4 => new Guid("8339fde3-106f-47ab-8373-1c6295eb10b3"),
            _ => new Guid("ef8a8135-5cc6-45fe-8825-c5a0724eb819")
        };
        if (*iid != new Guid("00000000-0000-0000-C000-000000000046") && (self->Kind == 3 || *iid != supported)
            && !(self->Kind == 2 && *iid == new Guid("eaf3a2da-ecf4-4d24-b644-b34f6842024b")))
            return unchecked((int)0x80004002);
        Interlocked.Increment(ref self->References);
        *result = self;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(Callback* self) => (uint)Interlocked.Increment(ref self->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint ReleaseRef(Callback* self) => ReleaseOwned(self);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CreateEnumerator(Callback* self, nint factory, void* key, uint keySize, Callback** enumerator)
    {
        *enumerator = null;
        var state = GetState<FileState>(self);
        if (key == null || keySize != sizeof(nint) || *(nint*)key != state.Key)
            return unchecked((int)0x80070057);
        try
        {
            *enumerator = CreateCallback(1, new FileState(state.File, state.Key));
            return 0;
        }
        catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int MoveNext(Callback* self, int* hasCurrent)
    {
        var state = GetState<FileState>(self);
        *hasCurrent = state.Position == -1 ? 1 : 0;
        state.Position = *hasCurrent != 0 ? 0 : 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CurrentFile(Callback* self, nint* file)
    {
        *file = 0;
        var state = GetState<FileState>(self);
        if (state.Position != 0)
            return unchecked((int)0x80004005);
        AddReference(state.File);
        *file = state.File;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Snapping(Callback* self, nint context, int* disabled)
    {
        *disabled = 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int PixelsPerDip(Callback* self, nint context, float* value)
    {
        *value = 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Transform(Callback* self, nint context, float* matrix)
    {
        new Span<float>(matrix, 6).Clear();
        matrix[0] = matrix[3] = 1;
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int DrawGlyphRun(Callback* self, nint context, float x, float y, int mode, NativeRun* run, RunDescription* description, nint effect)
    {
        var state = GetState<CaptureState>(self);
        try
        {
            var paintStyle = 0;
            if (effect != 0 && (state.PaintStyles is null || !state.PaintStyles.TryGetValue(effect, out paintStyle)))
                throw new InvalidDataException("TextLayout returned an undeclared paint-style marker.");
            if (description == null || description->Position > state.Length || description->Length > state.Length - description->Position)
                throw new InvalidDataException("TextLayout omitted or exceeded original shaping-input positions.");
            var fontSpan = 0;
            if (state.Fonts is { } fonts)
            {
                while (fontSpan < fonts.Length && description->Position >= fonts[fontSpan].Start + fonts[fontSpan].Length)
                    fontSpan++;
                if (fontSpan == fonts.Length)
                    throw new InvalidDataException("TextLayout glyph run has no declared font interval.");
                var selected = fonts[fontSpan];
                selected.Identity.Verify(run->Face);
                var end = description->Position + description->Length;
                for (var index = fontSpan; index < fonts.Length && fonts[index].Start < end; index++)
                    if (!ReferenceEquals(fonts[index].Identity, selected.Identity) || fonts[index].Size != run->Size)
                        throw new InvalidDataException("TextLayout glyph run crosses incompatible explicit font intervals.");
            }
            else
                state.Identity.Verify(run->Face);
            // No-visual control runs have source text but no glyph/cluster output to read.
            if (run->Count == 0)
                return 0;
            var sources = new uint[run->Count];
            Array.Fill(sources, uint.MaxValue);
            for (uint index = 0; index < description->Length; index++)
            {
                var glyph = description->Clusters[index];
                if (glyph >= run->Count)
                    throw new InvalidDataException($"TextLayout cluster {glyph} exceeds glyph count {run->Count}; source position {description->Position}, length {description->Length}, character {index}.");
                sources[glyph] = Math.Min(sources[glyph], description->Position + index);
            }
            var current = description->Position;
            for (var index = 0; index < sources.Length; index++)
            {
                if (sources[index] == uint.MaxValue)
                    sources[index] = current;
                else
                    current = sources[index];
            }
            var glyphs = new Placed[run->Count];
            var nativeGlyphs = new DirectWriteTextShaper.Glyph[run->Count];
            var nativeOriginX = x;
            var rtl = (run->BidiLevel & 1) != 0;
            var width = 0f;
            foreach (var advance in new ReadOnlySpan<float>(run->Advances, checked((int)run->Count)))
                width += advance;
            var left = rtl ? x - width : x;
            for (var index = 0; index < glyphs.Length; index++)
            {
                var advance = run->Advances[index];
                var offset = run->Offsets == null ? default : run->Offsets[index];
                nativeGlyphs[index] = new(run->Indices[index], sources[index], advance, offset.Advance, -offset.Ascender);
                if (rtl)
                    x -= advance;
                glyphs[rtl ? glyphs.Length - 1 - index : index] = new(run->Indices[index], sources[index], advance,
                    x + (rtl ? -offset.Advance : offset.Advance), y - offset.Ascender);
                if (!rtl)
                    x += advance;
            }
            state.Runs.Add(new(left, y, glyphs, fontSpan, new(nativeGlyphs, run->BidiLevel, nativeOriginX, y), paintStyle));
            return 0;
        }
        catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Decoration(Callback* self, nint context, float x, float y, nint decoration, nint effect) => unchecked((int)0x80004001);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InlineObject(Callback* self, nint context, float x, float y, nint inline, int sideways, int rtl, nint effect)
    {
        var state = GetState<CaptureState>(self);
        try
        {
            if (sideways != 0 || state.InlineObjects is null || !state.InlineObjects.TryGetValue(inline, out var item))
                throw new NotSupportedException("The paragraph renderer received an undeclared or sideways inline object.");
            state.ObjectPlacements.Add(new(item.Start, x, y, item.Width, item.Height, rtl != 0));
            return 0;
        }
        catch (Exception error) { state.Failure = ExceptionDispatchInfo.Capture(error); return error.HResult; }
    }
}
