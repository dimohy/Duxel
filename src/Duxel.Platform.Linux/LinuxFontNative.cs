using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

// Linux LP64 ABI. Native face internals remain opaque.
internal static unsafe partial class LinuxFontNative
{
    private const string FreeType = "libfreetype.so.6", Cairo = "libcairo.so.2", HarfBuzz = "libharfbuzz.so.0";

    [StructLayout(LayoutKind.Sequential)]
    internal struct Matrix(double scale)
    {
        internal double Xx = scale, Yx, Xy, Yy = scale, X0, Y0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Glyph(nuint index, double x = 0, double y = 0)
    {
        internal nuint Index = index;
        internal double X = x, Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FontExtents
    {
        internal double Ascent, Descent, Height, MaxXAdvance, MaxYAdvance;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextExtents
    {
        internal double XBearing, YBearing, Width, Height, XAdvance, YAdvance;
    }

    [LibraryImport(FreeType)] internal static partial int FT_Init_FreeType(out nint library);
    [LibraryImport(FreeType, StringMarshalling = StringMarshalling.Utf8)] internal static partial int FT_New_Face(nint library, string path, nint index, out nint face);
    [LibraryImport(FreeType)] internal static partial int FT_Done_Face(nint face);
    [LibraryImport(FreeType)] internal static partial int FT_Done_FreeType(nint library);
    [LibraryImport(FreeType)] internal static partial int FT_Set_Char_Size(nint face, nint width, nint height, uint dpiX, uint dpiY);
    [LibraryImport(FreeType)] internal static partial uint FT_Get_Char_Index(nint face, nuint codepoint);

    [LibraryImport(Cairo)] internal static partial nint cairo_ft_font_face_create_for_ft_face(nint face, int flags);
    [LibraryImport(Cairo)] internal static partial int cairo_font_face_status(nint face);
    [LibraryImport(Cairo)] internal static partial int cairo_font_face_set_user_data(nint face, void* key, void* data, delegate* unmanaged[Cdecl]<void*, void> destroy);
    [LibraryImport(Cairo)] internal static partial void cairo_font_face_destroy(nint face);
    [LibraryImport(Cairo)] internal static partial nint cairo_font_options_create();
    [LibraryImport(Cairo)] internal static partial void cairo_font_options_set_antialias(nint options, int value);
    [LibraryImport(Cairo)] internal static partial void cairo_font_options_set_hint_style(nint options, int value);
    [LibraryImport(Cairo)] internal static partial void cairo_font_options_set_hint_metrics(nint options, int value);
    [LibraryImport(Cairo)] internal static partial void cairo_font_options_destroy(nint options);
    [LibraryImport(Cairo)] internal static partial nint cairo_scaled_font_create(nint face, in Matrix font, in Matrix ctm, nint options);
    [LibraryImport(Cairo)] internal static partial int cairo_scaled_font_status(nint font);
    [LibraryImport(Cairo)] internal static partial void cairo_scaled_font_extents(nint font, out FontExtents extents);
    [LibraryImport(Cairo)] internal static partial void cairo_scaled_font_glyph_extents(nint font, Glyph* glyphs, int count, out TextExtents extents);
    [LibraryImport(Cairo)] internal static partial void cairo_scaled_font_destroy(nint font);
    [LibraryImport(Cairo)] internal static partial nint cairo_image_surface_create(int format, int width, int height);
    [LibraryImport(Cairo)] internal static partial int cairo_surface_status(nint surface);
    [LibraryImport(Cairo)] internal static partial void cairo_surface_destroy(nint surface);
    [LibraryImport(Cairo)] internal static partial void cairo_surface_flush(nint surface);
    [LibraryImport(Cairo)] internal static partial byte* cairo_image_surface_get_data(nint surface);
    [LibraryImport(Cairo)] internal static partial int cairo_image_surface_get_stride(nint surface);
    [LibraryImport(Cairo)] internal static partial nint cairo_create(nint surface);
    [LibraryImport(Cairo)] internal static partial int cairo_status(nint context);
    [LibraryImport(Cairo)] internal static partial void cairo_destroy(nint context);
    [LibraryImport(Cairo)] internal static partial void cairo_set_source_rgba(nint context, double r, double g, double b, double a);
    [LibraryImport(Cairo)] internal static partial void cairo_set_scaled_font(nint context, nint font);
    [LibraryImport(Cairo)] internal static partial void cairo_translate(nint context, double x, double y);
    [LibraryImport(Cairo)] internal static partial void cairo_show_glyphs(nint context, Glyph* glyphs, int count);

    [LibraryImport(HarfBuzz)] internal static partial nint hb_ft_face_create_referenced(nint face);
    [LibraryImport(HarfBuzz)] internal static partial nint hb_font_create(nint face);
    [LibraryImport(HarfBuzz)] internal static partial uint hb_face_get_glyph_count(nint face);
    [LibraryImport(HarfBuzz)] internal static partial nint hb_font_get_empty();
    [LibraryImport(HarfBuzz)] internal static partial void hb_face_destroy(nint face);
    [LibraryImport(HarfBuzz)] internal static partial void hb_ot_font_set_funcs(nint font);
    [LibraryImport(HarfBuzz)] internal static partial void hb_font_set_scale(nint font, int x, int y);
    [LibraryImport(HarfBuzz)] internal static partial int hb_version_atleast(uint major, uint minor, uint micro);
    [LibraryImport(HarfBuzz)] internal static partial void hb_font_set_synthetic_slant(nint font, float slant);
    [LibraryImport(HarfBuzz)] internal static partial nint hb_draw_funcs_create();
    [LibraryImport(HarfBuzz)] internal static partial nint hb_draw_funcs_get_empty();
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_destroy(nint funcs);
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_make_immutable(nint funcs);
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_set_move_to_func(nint funcs, delegate* unmanaged[Cdecl]<nint, nint, void*, float, float, void*, void> callback, void* data, void* destroy);
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_set_line_to_func(nint funcs, delegate* unmanaged[Cdecl]<nint, nint, void*, float, float, void*, void> callback, void* data, void* destroy);
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_set_quadratic_to_func(nint funcs, delegate* unmanaged[Cdecl]<nint, nint, void*, float, float, float, float, void*, void> callback, void* data, void* destroy);
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_set_cubic_to_func(nint funcs, delegate* unmanaged[Cdecl]<nint, nint, void*, float, float, float, float, float, float, void*, void> callback, void* data, void* destroy);
    [LibraryImport(HarfBuzz)] internal static partial void hb_draw_funcs_set_close_path_func(nint funcs, delegate* unmanaged[Cdecl]<nint, nint, void*, void*, void> callback, void* data, void* destroy);
    [LibraryImport(HarfBuzz)] internal static partial void hb_font_draw_glyph(nint font, uint glyph, nint funcs, nint data);
    [LibraryImport(HarfBuzz)] internal static partial void hb_font_destroy(nint font);
}
