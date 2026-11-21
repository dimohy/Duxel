using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

// Pango/Fontconfig public LP64 ABI. Every font map uses an explicit private config.
internal static unsafe partial class LinuxParagraphNative
{
    internal const int Scale = 1024;
    private const string Pango = "libpango-1.0.so.0", PangoCairo = "libpangocairo-1.0.so.0",
        PangoFc = "libpangoft2-1.0.so.0", Fontconfig = "libfontconfig.so.1", GObject = "libgobject-2.0.so.0";

    [LibraryImport(Pango)]
    internal static partial void pango_layout_get_pixel_extents(nint layout, out Rectangle ink, out Rectangle logical);

    [LibraryImport(PangoCairo)]
    internal static partial void pango_cairo_show_layout(nint context, nint layout);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rectangle
    {
        internal int X, Y, Width, Height;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Analysis
    {
        internal nint ShapeEngine, LanguageEngine, Font;
        internal byte Level, Gravity, Flags, Script;
        internal nint Language, Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Item
    {
        internal int Offset, Length, Characters; internal Analysis Analysis;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct GlyphInfo
    {
        internal uint Glyph; internal int Width, XOffset, YOffset; internal uint Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct GlyphString
    {
        internal int Count; internal GlyphInfo* Glyphs; internal int* Clusters;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct GlyphItem
    {
        internal Item* Item; internal GlyphString* Glyphs; internal int YOffset, StartXOffset, EndXOffset;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Attribute
    {
        internal nint Type; internal uint Start, End;
    }

    [LibraryImport(Pango)] internal static partial nint pango_version_check(int major, int minor, int micro);
    [LibraryImport(PangoCairo)] internal static partial nint pango_cairo_font_map_new_for_font_type(int type);
    [LibraryImport(PangoCairo)] internal static partial void pango_cairo_font_map_set_resolution(nint map, double dpi);
    [LibraryImport(PangoFc)] internal static partial void pango_fc_font_map_set_config(nint map, nint config);
    [LibraryImport(PangoFc)] internal static partial nint pango_fc_font_get_pattern(nint font);
    [LibraryImport(PangoFc)] internal static partial nint pango_fc_font_description_from_pattern(nint pattern, int includeSize);
    [LibraryImport(Pango)] internal static partial nint pango_font_map_create_context(nint map);
    [LibraryImport(Pango)] internal static partial void pango_context_set_round_glyph_positions(nint context, int round);
    [LibraryImport(Pango)] internal static partial void pango_context_set_base_dir(nint context, int direction);
    [LibraryImport(PangoCairo)] internal static partial void pango_cairo_context_set_font_options(nint context, nint options);
    [LibraryImport(Pango)] internal static partial nint pango_language_from_string(byte* language);
    [LibraryImport(Pango)] internal static partial void pango_context_set_language(nint context, nint language);
    [LibraryImport(Pango)] internal static partial nint pango_layout_new(nint context);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_text(nint layout, byte* text, int length);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_auto_dir(nint layout, int auto);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_single_paragraph_mode(nint layout, int single);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_width(nint layout, int width);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_wrap(nint layout, int wrap);
    [LibraryImport(Pango)] internal static partial nint pango_layout_iter_get_line_readonly(nint iterator);
    [LibraryImport(Pango)] internal static partial void pango_layout_iter_get_line_extents(nint iterator, Rectangle* ink, out Rectangle logical);
    [LibraryImport(Pango)] internal static partial int pango_layout_iter_next_line(nint iterator);
    [LibraryImport(Pango)] internal static partial int pango_layout_line_get_start_index(nint line);
    [LibraryImport(Pango)] internal static partial int pango_layout_line_get_length(nint line);
    [LibraryImport(Pango)] internal static partial void pango_layout_line_index_to_x(nint line, int index, int trailing, out int x);
    [LibraryImport(Pango)] internal static partial void pango_layout_line_get_x_ranges(nint line, int start, int end, int** ranges, out int count);
    [LibraryImport("libglib-2.0.so.0")] internal static partial void g_free(void* memory);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_font_description(nint layout, nint description);
    [LibraryImport(Pango)] internal static partial void pango_font_description_set_absolute_size(nint description, double size);
    [LibraryImport(Pango)] internal static partial void pango_font_description_set_style(nint description, int style);
    [LibraryImport(Pango)] internal static partial void pango_font_description_free(nint description);
    [LibraryImport(Pango, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint pango_font_description_from_string(string description);
    [LibraryImport(Pango)] internal static partial int pango_font_description_get_size(nint description);
    [LibraryImport(Pango)] internal static partial int pango_font_description_get_size_is_absolute(nint description);
    [LibraryImport(Pango)] internal static partial nint pango_attr_list_new();
    [LibraryImport(Pango)] internal static partial void pango_attr_list_insert(nint attributes, Attribute* attribute);
    [LibraryImport(Pango)] internal static partial Attribute* pango_attr_font_desc_new(nint description);
    [LibraryImport(Pango)] internal static partial void pango_attr_list_unref(nint attributes);
    [LibraryImport(Pango)] internal static partial void pango_layout_set_attributes(nint layout, nint attributes);
    [LibraryImport(Pango)] internal static partial int pango_layout_get_unknown_glyphs_count(nint layout);
    [LibraryImport(Pango)] internal static partial void pango_layout_get_size(nint layout, out int width, out int height);
    [LibraryImport(Pango)] internal static partial int pango_layout_get_baseline(nint layout);
    [LibraryImport(Pango)] internal static partial nint pango_layout_get_iter(nint layout);
    [LibraryImport(Pango)] internal static partial GlyphItem* pango_layout_iter_get_run_readonly(nint iterator);
    [LibraryImport(Pango)] internal static partial void pango_layout_iter_get_run_extents(nint iterator, Rectangle* ink, out Rectangle logical);
    [LibraryImport(Pango)] internal static partial int pango_layout_iter_get_baseline(nint iterator);
    [LibraryImport(Pango)] internal static partial int pango_layout_iter_next_run(nint iterator);
    [LibraryImport(Pango)] internal static partial void pango_layout_iter_free(nint iterator);
    [LibraryImport(Pango)] internal static partial void pango_layout_index_to_pos(nint layout, int index, out Rectangle rectangle);
    [LibraryImport(Pango)] internal static partial int pango_layout_xy_to_index(nint layout, int x, int y, out int index, out int trailing);
    [LibraryImport(GObject)] internal static partial void g_object_unref(nint instance);
    [LibraryImport(Fontconfig)] internal static partial nint FcConfigCreate();
    [LibraryImport(Fontconfig)] internal static partial void FcConfigDestroy(nint config);
    [LibraryImport(Fontconfig, StringMarshalling = StringMarshalling.Utf8)] internal static partial int FcConfigAppFontAddFile(nint config, string path);
    [LibraryImport(Fontconfig)] internal static partial int FcConfigBuildFonts(nint config);
    [LibraryImport(Fontconfig, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint FcFreeTypeQuery(string path, uint index, nint blanks, out int count);
    [LibraryImport(Fontconfig)] internal static partial void FcPatternDestroy(nint pattern);
    [LibraryImport(Fontconfig, StringMarshalling = StringMarshalling.Utf8)] internal static partial int FcPatternGetString(nint pattern, string name, int index, out nint value);
    [LibraryImport(Fontconfig, StringMarshalling = StringMarshalling.Utf8)] internal static partial int FcPatternGetInteger(nint pattern, string name, int index, out int value);
}
