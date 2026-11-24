using System.Runtime.InteropServices;

namespace Duxel.Interop.Windows;

// TextLayout owns native run partitioning. A private collection keeps the selected face explicit.
internal static unsafe partial class DirectWriteLongTextShaper
{
    internal static DirectWriteTextShaper.Glyph[] Shape(nint factory, nint face, string text, float size, bool rtl, Action<nint>? inspectLayout = null)
    {
        var paragraph = ShapeParagraph(factory, text, [new(0, text.Length, face, size)], rtl, inspectLayout);
        var result = new List<DirectWriteTextShaper.Glyph>();
        var paragraphPen = 0f;
        foreach (var run in paragraph.Runs)
        {
            foreach (var glyph in run.Glyphs)
            {
                result.Add(new(glyph.Index, glyph.Cluster, glyph.Advance, glyph.X - paragraphPen, glyph.Y - paragraph.Baseline));
                paragraphPen += glyph.Advance;
            }
        }
        return result.ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HitMetrics
    {
        internal uint Position, Length;
        internal float Left, Top, Width, Height;
        internal uint BidiLevel;
        internal int IsText, IsTrimmed;
    }
    internal readonly record struct CharacterGeometry(int Start, int Length, float Left, float Width, uint Level);
    internal readonly record struct LineGeometry(float[] Carets, CharacterGeometry[] Characters, float Advance);

    internal static LineGeometry Geometry(nint factory, nint face, string source, float size)
        => Geometry(source, (text, inspect) => Shape(factory, face, text, size, false, inspect).Sum(glyph => glyph.Advance));

    internal static LineGeometry Geometry(string source, Func<string, Action<nint>, float> inspectLayout)
    {
        var prepared = DirectWriteHangulInput.Prepare(source);
        int PreparedPosition(int original)
        {
            if (original == source.Length)
                return prepared.Text.Length;
            if (prepared.OriginalPositions is not { } positions)
                return original;
            var at = Array.BinarySearch(positions, 0, prepared.Text.Length, (uint)original);
            return at >= 0 ? at : Math.Max(0, ~at - 1);
        }
        var carets = new float[source.Length + 1];
        var characters = new List<CharacterGeometry>();
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(source);
        var advance = inspectLayout(prepared.Text, layout =>
        {
            for (var index = 0; index <= source.Length; index++)
            {
                float x = 0, y = 0;
                HitMetrics metrics = default;
                Check(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                    (layout, (uint)PreparedPosition(index), 0, &x, &y, &metrics), "HitTestTextPosition");
                carets[index] = x;
            }
            for (var index = 0; index < boundaries.Length; index++)
            {
                var start = boundaries[index];
                var end = index + 1 < boundaries.Length ? boundaries[index + 1] : source.Length;
                var position = (uint)PreparedPosition(start);
                var length = (uint)PreparedPosition(end) - position;
                uint count = 0;
                var status = ((delegate* unmanaged[Stdcall]<nint, uint, uint, float, float, HitMetrics*, uint, uint*, int>)Method(layout, 66))
                    (layout, position, length, 0, 0, null, 0, &count);
                if (status != unchecked((int)0x8007007A))
                    Check(status, "HitTestTextRange count");
                var regions = new HitMetrics[count];
                fixed (HitMetrics* output = regions)
                    Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, float, float, HitMetrics*, uint, uint*, int>)Method(layout, 66))
                        (layout, position, length, 0, 0, output, (uint)regions.Length, &count), "HitTestTextRange");
                foreach (var region in regions)
                    characters.Add(new(start, end - start, region.Left, region.Width, region.BidiLevel));
            }
        });
        return new(carets, characters.ToArray(), advance);
    }

    private static nint GetFile(nint face)
    {
        uint count = 0;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint*, nint*, int>)Method(face, 4))(face, &count, null));
        if (count != 1)
            throw new NotSupportedException("The selected explicit font face must have exactly one source file.");
        nint file = 0;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint*, nint*, int>)Method(face, 4))(face, &count, &file));
        return file;
    }

    internal sealed class FontIdentity : IDisposable
    {
        private nint _loader;
        private nint _variableFace;
        internal required byte[] Key;
        internal uint Index;
        internal int Simulations;
        internal static FontIdentity Read(nint face)
        {
            nint file = 0, loader = 0, identity = 0;
            try
            {
                file = GetFile(face);
                void* key = null;
                uint length = 0;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, void**, uint*, int>)Method(file, 3))(file, &key, &length));
                var bytes = new ReadOnlySpan<byte>(key, checked((int)length)).ToArray();
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(file, 4))(file, &loader));
                var iid = new Guid("00000000-0000-0000-C000-000000000046");
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Method(loader, 0))(loader, &iid, &identity));
                var result = new FontIdentity
                {
                    Key = bytes,
                    _loader = identity,
                    Index = ((delegate* unmanaged[Stdcall]<nint, uint>)Method(face, 5))(face),
                    Simulations = ((delegate* unmanaged[Stdcall]<nint, int>)Method(face, 6))(face)
                };
                identity = 0;
                return result;
            }
            finally { Release(ref identity); Release(ref loader); Release(ref file); }
        }
        internal void Verify(nint face)
        {
            using var actual = Read(face);
            if (_loader != actual._loader || Index != actual.Index || Simulations != actual.Simulations || !Key.AsSpan().SequenceEqual(actual.Key))
                throw new InvalidOperationException("TextLayout substituted a font outside the explicitly selected face.");
            if (_variableFace != 0 && ((delegate* unmanaged[Stdcall]<nint, nint, int>)Method(_variableFace, 57))(_variableFace, face) == 0)
                throw new InvalidOperationException("TextLayout substituted a different variation of the explicitly selected face.");
        }
        internal void BindVariableFace(nint face) => _variableFace = face;
        public void Dispose()
        {
            Release(ref _variableFace);
            Release(ref _loader);
        }
    }
    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];
    private static uint AddReference(nint instance) => ((delegate* unmanaged[Stdcall]<nint, uint>)Method(instance, 1))(instance);
    private static void Release(ref nint instance)
    {
        var pointer = instance;
        instance = 0;
        if (pointer != 0)
            ((delegate* unmanaged[Stdcall]<nint, uint>)Method(pointer, 2))(pointer);
    }
    private static void Check(int status, string operation)
    {
        if (status < 0)
            throw new COMException("Explicit long-text " + operation + " failed.", status);
    }
}
