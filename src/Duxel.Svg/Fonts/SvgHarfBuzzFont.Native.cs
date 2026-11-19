using System.Runtime.InteropServices;

namespace Duxel.Svg;

internal sealed unsafe partial class SvgHarfBuzzFont
{
    private const string Library = "libHarfBuzzSharp";
    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphInfo { internal uint Codepoint, Mask, Cluster, Reserved1, Reserved2; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GlyphPosition { internal int XAdvance, YAdvance, XOffset, YOffset, Reserved; }
    [LibraryImport(Library)] private static partial nint hb_blob_create(byte* data, uint length, int mode, nint userData, nint destroy);
    [LibraryImport(Library)] private static partial void hb_blob_destroy(nint blob);
    [LibraryImport(Library)] private static partial byte* hb_blob_get_data(nint blob, out uint length);
    [LibraryImport(Library)] private static partial uint hb_face_count(nint blob);
    [LibraryImport(Library)] private static partial nint hb_face_create(nint blob, uint index);
    [LibraryImport(Library)] private static partial void hb_face_destroy(nint face);
    [LibraryImport(Library)] private static partial uint hb_face_get_upem(nint face);
    [LibraryImport(Library)] private static partial uint hb_face_get_glyph_count(nint face);
    [LibraryImport(Library)] private static partial nint hb_face_reference_table(nint face, uint tag);
    [LibraryImport(Library)] private static partial nint hb_font_create(nint face);
    [LibraryImport(Library)] private static partial void hb_font_destroy(nint font);
    [LibraryImport(Library)] private static partial void hb_ot_font_set_funcs(nint font);
    [LibraryImport(Library)] private static partial void hb_font_set_scale(nint font, int xScale, int yScale);
    [LibraryImport(Library)] private static partial int hb_font_get_nominal_glyph(nint font, uint unicode, out uint glyph);
    [LibraryImport(Library)] private static partial void hb_font_draw_glyph(nint font, uint glyph, nint functions, nint drawData);
    [LibraryImport(Library)] private static partial nint hb_draw_funcs_create();
    [LibraryImport(Library)] private static partial void hb_draw_funcs_destroy(nint functions);
    [LibraryImport(Library)] private static partial void hb_draw_funcs_make_immutable(nint functions);
    [LibraryImport(Library)] private static partial void hb_draw_funcs_set_move_to_func(nint functions, nint callback, nint data, nint destroy);
    [LibraryImport(Library)] private static partial void hb_draw_funcs_set_line_to_func(nint functions, nint callback, nint data, nint destroy);
    [LibraryImport(Library)] private static partial void hb_draw_funcs_set_cubic_to_func(nint functions, nint callback, nint data, nint destroy);
    [LibraryImport(Library)] private static partial void hb_draw_funcs_set_close_path_func(nint functions, nint callback, nint data, nint destroy);
    [LibraryImport(Library)] private static partial nint hb_buffer_create();
    [LibraryImport(Library)] private static partial void hb_buffer_destroy(nint buffer);
    [LibraryImport(Library)] private static partial void hb_buffer_add_utf16(nint buffer, char* text, int length, uint offset, int itemLength);
    [LibraryImport(Library)] private static partial void hb_buffer_guess_segment_properties(nint buffer);
    [LibraryImport(Library)] private static partial void hb_buffer_set_direction(nint buffer, int direction);
    [LibraryImport(Library)] private static partial void hb_shape(nint font, nint buffer, nint features, uint count);
    [LibraryImport(Library)] private static partial GlyphInfo* hb_buffer_get_glyph_infos(nint buffer, out uint length);
    [LibraryImport(Library)] private static partial GlyphPosition* hb_buffer_get_glyph_positions(nint buffer, out uint length);
}
