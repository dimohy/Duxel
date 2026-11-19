using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

/// <summary>Explicit font snapshot and OpenType shaping; outlines are consumed by the managed SVG renderer.</summary>
internal sealed unsafe partial class SvgHarfBuzzFont : ISvgNativeFont
{
    private nint _face, _font, _draw;
    private readonly Dictionary<uint, SvgGeometry> _outlines = [];
    public int UnitsPerEm { get; }
    public int Weight { get; }
    public int Width { get; }
    public bool Italic { get; }
    public bool Oblique { get; }
    public bool IsDisposed => _font == 0 && _face == 0 && _draw == 0;

    internal SvgHarfBuzzFont(byte[] bytes, int collectionIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(collectionIndex);
        nint blob;
        fixed (byte* source = bytes) blob = hb_blob_create(source, checked((uint)bytes.Length), 0, 0, 0);
        try
        {
            if ((uint)collectionIndex >= hb_face_count(blob)) throw new InvalidDataException("Invalid SVG font collection index or font bytes.");
            _face = hb_face_create(blob, (uint)collectionIndex);
            UnitsPerEm = checked((int)hb_face_get_upem(_face));
            if (hb_face_get_glyph_count(_face) == 0 || UnitsPerEm <= 0) throw new InvalidDataException("SVG font has no usable glyphs or units.");
            var table = hb_face_reference_table(_face, 0x4f532f32); // OS/2
            try
            {
                var pointer = hb_blob_get_data(table, out var size);
                if (size < 64) throw new InvalidDataException("SVG fonts require explicit OS/2 style metadata.");
                var metadata = new ReadOnlySpan<byte>(pointer, checked((int)size));
                Weight = BinaryPrimitives.ReadUInt16BigEndian(metadata[4..]);
                Width = BinaryPrimitives.ReadUInt16BigEndian(metadata[6..]);
                var selection = BinaryPrimitives.ReadUInt16BigEndian(metadata[62..]);
                Italic = (selection & 1) != 0;
                Oblique = (selection & 512) != 0;
            }
            finally { hb_blob_destroy(table); }
            _font = hb_font_create(_face);
            hb_ot_font_set_funcs(_font);
            hb_font_set_scale(_font, UnitsPerEm, UnitsPerEm);
            _draw = hb_draw_funcs_create();
            hb_draw_funcs_set_move_to_func(_draw, (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, float, float, nint, void>)&Move, 0, 0);
            hb_draw_funcs_set_line_to_func(_draw, (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, float, float, nint, void>)&Line, 0, 0);
            hb_draw_funcs_set_cubic_to_func(_draw, (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, float, float, float, float, float, float, nint, void>)&Cubic, 0, 0);
            hb_draw_funcs_set_close_path_func(_draw, (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, void>)&Close, 0, 0);
            hb_draw_funcs_make_immutable(_draw);
        }
        catch { Dispose(); throw; }
        finally { hb_blob_destroy(blob); }
    }
    public bool Contains(uint codepoint)
    {
        ObjectDisposedException.ThrowIf(_font == 0, this);
        return hb_font_get_nominal_glyph(_font, codepoint, out _) != 0;
    }
    public SvgShapedGlyph[] Shape(string text, bool rightToLeft)
    {
        ObjectDisposedException.ThrowIf(_font == 0, this);
        var buffer = hb_buffer_create();
        try
        {
            fixed (char* source = text) hb_buffer_add_utf16(buffer, source, text.Length, 0, text.Length);
            hb_buffer_set_direction(buffer, rightToLeft ? 5 : 4);
            hb_buffer_guess_segment_properties(buffer);
            hb_shape(_font, buffer, 0, 0);
            var info = hb_buffer_get_glyph_infos(buffer, out var count);
            var position = hb_buffer_get_glyph_positions(buffer, out var positions);
            if (count != positions) throw new InvalidDataException("OpenType shaping returned inconsistent glyph positions.");
            var result = new SvgShapedGlyph[checked((int)count)];
            for (var index = 0; index < result.Length; index++)
            {
                var glyph = info[index];
                var placement = position[index];
                result[index] = new(glyph.Codepoint, glyph.Cluster, new(placement.XAdvance, -placement.YAdvance),
                    new(placement.XOffset, -placement.YOffset), Outline(glyph.Codepoint));
            }
            return result;
        }
        finally { hb_buffer_destroy(buffer); }
    }
    private SvgGeometry Outline(uint glyph)
    {
        if (_outlines.TryGetValue(glyph, out var cached)) return cached;
        var capture = new Capture();
        var handle = GCHandle.Alloc(capture);
        try { hb_font_draw_glyph(_font, glyph, _draw, GCHandle.ToIntPtr(handle)); }
        finally { handle.Free(); }
        capture.Failure?.Throw();
        var geometry = new SvgGeometry(capture.Segments.ToArray());
        _outlines.Add(glyph, geometry);
        return geometry;
    }
    public void Dispose()
    {
        if (_draw != 0) { hb_draw_funcs_destroy(_draw); _draw = 0; }
        if (_font != 0) { hb_font_destroy(_font); _font = 0; }
        if (_face != 0) { hb_face_destroy(_face); _face = 0; }
        _outlines.Clear();
    }
    private sealed class Capture
    {
        internal List<SvgSegment> Segments { get; } = [];
        internal Vector2 First;
        internal ExceptionDispatchInfo? Failure;
        internal void Add(SvgSegment segment)
        {
            if (Failure is not null) return;
            try
            {
                if (Segments.Count == 1_000_000) throw new InvalidDataException("SVG glyph exceeds the outline segment limit.");
                Segments.Add(segment);
            }
            catch (Exception error) { Failure = ExceptionDispatchInfo.Capture(error); }
        }
    }
    private static Capture Target(nint data) => (Capture)GCHandle.FromIntPtr(data).Target!;
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Move(nint functions, nint data, nint state, float x, float y, nint user)
    {
        var capture = Target(data);
        capture.First = new(x, -y);
        capture.Add(new(SvgSegmentKind.Move, capture.First));
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Line(nint functions, nint data, nint state, float x, float y, nint user) =>
        Target(data).Add(new(SvgSegmentKind.Line, new(x, -y)));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Cubic(nint functions, nint data, nint state, float a, float b, float c, float d, float x, float y, nint user) =>
        Target(data).Add(new(SvgSegmentKind.Cubic, new(x, -y), new(a, -b), new(c, -d)));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Close(nint functions, nint data, nint state, nint user)
    {
        var capture = Target(data);
        capture.Add(new(SvgSegmentKind.Close, capture.First));
    }
}
