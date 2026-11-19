using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

// Independent native drawing oracle: never reads the product's extracted glyphs or geometry.
internal sealed unsafe partial class PangoLineOracle : IDisposable
{
    internal readonly record struct Region(int Start, int End, float Left, float Width, bool Rtl);
    internal readonly record struct Hit(float X, int Position, float CaretX);
    internal readonly record struct Selection(int Start, int End, (float Left, float Width)[] Fragments);
    internal sealed record Snapshot(float Advance, float[] Carets, Region[] Regions, byte[] Pixels, Hit[] Hits, Selection[] Selections);
    private nint _map, _context, _description, _secondaryDescription, _primaryCharset, _secondaryCharset;
    private readonly List<nint> _additionalDescriptions = [];
    internal const int Width = 640, Height = 128;
    internal static nint CurrentFontConfig => FcConfigGetCurrent();

    internal PangoLineOracle(string primary, string secondary, string[]? additionalFonts = null)
    {
        var config = FcConfigCreate();
        nint pattern = 0, options = 0;
        try
        {
            if (config == 0 || FcConfigAppFontAddFile(config, primary) == 0 || FcConfigAppFontAddFile(config, secondary) == 0)
                throw new InvalidOperationException("Independent oracle font config failed.");
            foreach (var path in additionalFonts ?? [])
                if (FcConfigAppFontAddFile(config, path) == 0)
                    throw new InvalidOperationException("Independent additional font registration failed.");
            if (FcConfigBuildFonts(config) == 0)
                throw new InvalidOperationException("Independent oracle font build failed.");
            _map = pango_cairo_font_map_new_for_font_type(1);
            if (_map == 0)
                throw new InvalidOperationException("Independent oracle font map absent.");
            pango_fc_font_map_set_config(_map, config);
            pango_cairo_font_map_set_resolution(_map, 72);
            _context = pango_font_map_create_context(_map);
            if (_context == 0)
                throw new InvalidOperationException("Independent oracle context absent.");
            pango_context_set_round_glyph_positions(_context, 0);
            pango_context_set_base_dir(_context, 0);
            fixed (byte* language = "und\0"u8)
                pango_context_set_language(_context, pango_language_from_string(language));
            options = cairo_font_options_create();
            cairo_font_options_set_antialias(options, 2);
            cairo_font_options_set_hint_style(options, 1);
            cairo_font_options_set_hint_metrics(options, 1);
            pango_cairo_context_set_font_options(_context, options);
            pattern = FcFreeTypeQuery(primary, 0, 0, out _);
            if (pattern == 0)
                throw new InvalidOperationException("Oracle font query absent.");
            _description = pango_fc_font_description_from_pattern(pattern, 0);
            if (FcPatternGetCharSet(pattern, "charset", 0, out var charset) != 0)
                throw new InvalidOperationException("Independent primary coverage absent.");
            _primaryCharset = FcCharSetCopy(charset);
            FcPatternDestroy(pattern);
            pattern = FcFreeTypeQuery(secondary, 0, 0, out _);
            _secondaryDescription = pango_fc_font_description_from_pattern(pattern, 0);
            if (FcPatternGetCharSet(pattern, "charset", 0, out charset) != 0)
                throw new InvalidOperationException("Independent secondary coverage absent.");
            _secondaryCharset = FcCharSetCopy(charset);
            foreach (var path in additionalFonts ?? [])
            {
                FcPatternDestroy(pattern);
                pattern = FcFreeTypeQuery(path, 0, 0, out _);
                if (pattern == 0)
                    throw new InvalidOperationException("Independent additional font pattern is absent.");
                _additionalDescriptions.Add(pango_fc_font_description_from_pattern(pattern, 0));
            }
        }
        catch { Dispose(); throw; }
        finally
        {
            if (pattern != 0)
                FcPatternDestroy(pattern);
            if (options != 0)
                cairo_font_options_destroy(options);
            if (config != 0)
                FcConfigDestroy(config);
        }
    }

    internal Snapshot Capture(string source, float size, bool translateOrigin = false)
    {
        var normalized = new StringBuilder();
        var map = new int[source.Length + 1];
        var character = 0;
        var bytes = 0;
        foreach (var rune in source.EnumerateRunes())
        {
            for (var unit = 0; unit < rune.Utf16SequenceLength; unit++)
                map[character + unit] = bytes;
            normalized.Append(rune.Value == '\t' ? "    " : rune.ToString());
            bytes += rune.Value == '\t' ? 4 : rune.Utf8SequenceLength;
            character += rune.Utf16SequenceLength;
        }
        map[^1] = bytes;
        var utf8 = Encoding.UTF8.GetBytes(normalized + "\0");
        var layout = pango_layout_new(_context);
        nint surface = 0, cairo = 0, attributes = 0;
        try
        {
            pango_font_description_set_absolute_size(_description, size * 1024);
            pango_font_description_set_absolute_size(_secondaryDescription, size * 1024);
            pango_layout_set_font_description(layout, _description);
            attributes = pango_attr_list_new();
            var offset = 0;
            var description = _description;
            var fontRunStart = 0;
            var current = _description;
            void AddFontRange(int start, int end, nint font)
            {
                var attribute = pango_attr_font_desc_new(font);
                if (attribute == null)
                    throw new OutOfMemoryException();
                attribute->Start = (uint)start;
                attribute->End = (uint)end;
                pango_attr_list_insert(attributes, attribute);
            }
            // The input contract chooses primary coverage first. Fontconfig charsets
            // independently establish the reference; the product queries its FT face.
            foreach (var rune in normalized.ToString().EnumerateRunes())
            {
                if (Rune.GetUnicodeCategory(rune) != UnicodeCategory.Format)
                    description = FcCharSetHasChar(_primaryCharset, (uint)rune.Value) != 0 ? _description
                        : FcCharSetHasChar(_secondaryCharset, (uint)rune.Value) != 0 ? _secondaryDescription
                        : throw new NotSupportedException("Oracle explicit-font coverage absent.");
                if (description != current)
                {
                    if (offset > fontRunStart)
                        AddFontRange(fontRunStart, offset, current);
                    fontRunStart = offset;
                    current = description;
                }
                offset += rune.Utf8SequenceLength;
            }
            if (offset > fontRunStart)
                AddFontRange(fontRunStart, offset, current);
            pango_layout_set_attributes(layout, attributes);
            pango_layout_set_auto_dir(layout, 0);
            pango_layout_set_single_paragraph_mode(layout, 1);
            fixed (byte* pointer = utf8)
                pango_layout_set_text(layout, pointer, bytes);
            if (pango_layout_get_unknown_glyphs_count(layout) != 0)
                throw new InvalidOperationException("Independent oracle contains unknown glyphs.");
            pango_layout_get_size(layout, out var width, out var logicalHeight);
            var carets = new float[map.Length];
            for (var index = 0; index < map.Length; index++)
            {
                pango_layout_index_to_pos(layout, map[index], out var rectangle);
                carets[index] = rectangle.X / 1024f;
            }
            var starts = StringInfo.ParseCombiningCharacters(source);
            var regions = new List<Region>();
            for (var index = 0; index < starts.Length; index++)
            {
                var from = starts[index];
                var end = index + 1 < starts.Length ? starts[index + 1] : source.Length;
                pango_layout_index_to_pos(layout, map[from], out var rectangle);
                var trailing = source[from] == '\t' ? carets[end] : (rectangle.X + rectangle.Width) / 1024f;
                regions.Add(new(from, end, Math.Min(carets[from], trailing), Math.Abs(trailing - carets[from]), rectangle.Width < 0));
            }
            var hits = new List<Hit>();
            for (var probeX = .25f; probeX < width / 1024f; probeX += 1.25f)
            {
                pango_layout_xy_to_index(layout, (int)(probeX * 1024), logicalHeight / 2, out var atByte, out var trailingCharacters);
                pango_layout_index_to_pos(layout, atByte, out var hitRectangle);
                var caret = (hitRectangle.X + (trailingCharacters == 0 ? 0 : hitRectangle.Width)) / 1024f;
                var byteEnd = atByte;
                for (var count = 0; count < trailingCharacters; count++)
                {
                    if (Rune.DecodeFromUtf8(utf8.AsSpan(byteEnd, bytes - byteEnd), out _, out var consumed) != System.Buffers.OperationStatus.Done)
                        throw new InvalidOperationException("Native hit returned an invalid UTF8 character range.");
                    byteEnd += consumed;
                }
                var original = Array.BinarySearch(map, byteEnd);
                original = original >= 0 ? original : ~original - 1;
                foreach (var region in regions)
                    if (source[region.Start] == '\t' && probeX >= region.Left && probeX < region.Left + region.Width)
                    {
                        var leading = probeX < region.Left + region.Width / 2;
                        original = leading ? region.Start : region.End;
                        caret = leading ? region.Left : region.Left + region.Width;
                    }
                hits.Add(new(probeX, original, caret));
            }
            var selections = new List<Selection>();
            var boundaries = starts.Append(source.Length).ToArray();
            var nativeLine = pango_layout_get_line_readonly(layout, 0);
            foreach (var from in boundaries)
                foreach (var end in boundaries)
                {
                    if (end <= from)
                        continue;
                    int* ranges = null;
                    pango_layout_line_get_x_ranges(nativeLine, map[from], map[end], &ranges, out var rangeCount);
                    try
                    {
                        var fragments = new (float Left, float Width)[rangeCount];
                        for (var index = 0; index < rangeCount; index++)
                            fragments[index] = (ranges[index * 2] / 1024f, (ranges[index * 2 + 1] - ranges[index * 2]) / 1024f);
                        selections.Add(new(from, end, fragments));
                    }
                    finally { if (ranges != null) g_free(ranges); }
                }
            surface = cairo_image_surface_create(2, Width, Height);
            cairo = cairo_create(surface);
            cairo_set_source_rgba(cairo, 1, 1, 1, 1);
            var baseline = pango_layout_get_baseline(layout) / 1024d;
            // Both bitmap contracts use an integer baseline; avoid comparing different raster phases.
            if (translateOrigin)
            {
                cairo_translate(cairo, 20, 20 + Math.Floor(baseline));
                cairo_move_to(cairo, 0, -baseline);
            }
            else
                cairo_move_to(cairo, 20, 20 - (baseline - Math.Floor(baseline)));
            pango_cairo_show_layout(cairo, layout);
            cairo_surface_flush(surface);
            var pixels = new byte[Width * Height];
            var data = cairo_image_surface_get_data(surface);
            var stride = cairo_image_surface_get_stride(surface);
            for (var y = 0; y < Height; y++)
                new ReadOnlySpan<byte>(data + y * stride, Width).CopyTo(pixels.AsSpan(y * Width));
            if (cairo_status(cairo) != 0 || cairo_surface_status(surface) != 0)
                throw new InvalidOperationException("Independent native layout drawing failed.");
            LastBaseline = (float)Math.Floor(baseline);
            return new(width / 1024f, carets, regions.ToArray(), pixels, hits.ToArray(), selections.ToArray());
        }
        finally
        {
            if (cairo != 0)
                cairo_destroy(cairo);
            if (surface != 0)
                cairo_surface_destroy(surface);
            if (attributes != 0)
                pango_attr_list_unref(attributes);
            g_object_unref(layout);
        }
    }
    internal float LastBaseline
    {
        get; private set;
    }
    public void Dispose()
    {
        foreach (var description in _additionalDescriptions)
            pango_font_description_free(description);
        _additionalDescriptions.Clear();
        if (_description != 0)
            pango_font_description_free(_description);
        if (_secondaryDescription != 0)
            pango_font_description_free(_secondaryDescription);
        if (_primaryCharset != 0)
            FcCharSetDestroy(_primaryCharset);
        if (_secondaryCharset != 0)
            FcCharSetDestroy(_secondaryCharset);
        if (_context != 0)
            g_object_unref(_context);
        if (_map != 0)
            g_object_unref(_map);
        _description = _secondaryDescription = _primaryCharset = _secondaryCharset = _context = _map = 0;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        internal int X, Y, Width, Height;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Attribute
    {
        internal nint Type; internal uint Start, End;
    }
    private const string P = "libpango-1.0.so.0", PC = "libpangocairo-1.0.so.0", FC = "libfontconfig.so.1", PF = "libpangoft2-1.0.so.0", C = "libcairo.so.2";
    [LibraryImport(FC)] private static partial nint FcConfigCreate();
    [LibraryImport(FC)] private static partial nint FcConfigGetCurrent();
    [LibraryImport(FC)] private static partial void FcConfigDestroy(nint config);
    [LibraryImport(FC, StringMarshalling = StringMarshalling.Utf8)] private static partial int FcConfigAppFontAddFile(nint config, string path);
    [LibraryImport(FC)] private static partial int FcConfigBuildFonts(nint config);
    [LibraryImport(FC, StringMarshalling = StringMarshalling.Utf8)] private static partial nint FcFreeTypeQuery(string path, uint index, nint blanks, out int count);
    [LibraryImport(FC)] private static partial void FcPatternDestroy(nint pattern);
    [LibraryImport(FC, StringMarshalling = StringMarshalling.Utf8)] private static partial int FcPatternGetCharSet(nint pattern, string name, int index, out nint charset);
    [LibraryImport(FC)] private static partial nint FcCharSetCopy(nint charset);
    [LibraryImport(FC)] private static partial void FcCharSetDestroy(nint charset);
    [LibraryImport(FC)] private static partial int FcCharSetHasChar(nint charset, uint codepoint);
    [LibraryImport(PF)] private static partial nint pango_fc_font_description_from_pattern(nint pattern, int size);
    [LibraryImport(PF)] private static partial void pango_fc_font_map_set_config(nint map, nint config);
    [LibraryImport(PC)] private static partial nint pango_cairo_font_map_new_for_font_type(int type);
    [LibraryImport(PC)] private static partial void pango_cairo_font_map_set_resolution(nint map, double dpi);
    [LibraryImport(PC)] private static partial void pango_cairo_context_set_font_options(nint context, nint options);
    [LibraryImport(PC)] private static partial void pango_cairo_show_layout(nint cairo, nint layout);
    [LibraryImport(P)] private static partial nint pango_font_map_create_context(nint map);
    [LibraryImport(P)] private static partial void pango_context_set_round_glyph_positions(nint context, int value);
    [LibraryImport(P)] private static partial void pango_context_set_base_dir(nint context, int value);
    [LibraryImport(P)] private static partial nint pango_language_from_string(byte* language);
    [LibraryImport(P)] private static partial void pango_context_set_language(nint context, nint language);
    [LibraryImport(P)] private static partial nint pango_attr_list_new();
    [LibraryImport(P)] private static partial Attribute* pango_attr_font_desc_new(nint description);
    [LibraryImport(P)] private static partial void pango_attr_list_insert(nint attributes, Attribute* attribute);
    [LibraryImport(P)] private static partial void pango_attr_list_unref(nint attributes);
    [LibraryImport(P)] private static partial void pango_layout_set_attributes(nint layout, nint attributes);
    [LibraryImport(P)] private static partial nint pango_layout_new(nint context);
    [LibraryImport(P)] private static partial void pango_layout_set_auto_dir(nint layout, int value);
    [LibraryImport(P)] private static partial void pango_layout_set_single_paragraph_mode(nint layout, int value);
    [LibraryImport(P)] private static partial void pango_layout_set_font_description(nint layout, nint description);
    [LibraryImport(P)] private static partial void pango_layout_set_text(nint layout, byte* text, int length);
    [LibraryImport(P)] private static partial int pango_layout_get_unknown_glyphs_count(nint layout);
    [LibraryImport(P)] private static partial void pango_layout_get_size(nint layout, out int width, out int height);
    [LibraryImport(P)] private static partial int pango_layout_get_baseline(nint layout);
    [LibraryImport(P)] private static partial void pango_layout_index_to_pos(nint layout, int index, out Rectangle rectangle);
    [LibraryImport(P)] private static partial int pango_layout_xy_to_index(nint layout, int x, int y, out int index, out int trailing);
    [LibraryImport(P)] private static partial nint pango_layout_get_line_readonly(nint layout, int line);
    [LibraryImport(P)] private static partial void pango_layout_line_get_x_ranges(nint line, int start, int end, int** ranges, out int count);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_free(void* memory);
    [LibraryImport(P)] private static partial void pango_font_description_set_absolute_size(nint description, double size);
    [LibraryImport(P)] private static partial nint pango_font_description_get_family(nint description);
    [LibraryImport(P, StringMarshalling = StringMarshalling.Utf8)] private static partial void pango_font_description_set_family(nint description, string family);
    [LibraryImport(P)] private static partial void pango_font_description_free(nint description);
    [LibraryImport("libgobject-2.0.so.0")] private static partial void g_object_unref(nint value);
    [LibraryImport(C)] private static partial nint cairo_font_options_create();
    [LibraryImport(C)] private static partial void cairo_font_options_set_antialias(nint options, int value);
    [LibraryImport(C)] private static partial void cairo_font_options_set_hint_style(nint options, int value);
    [LibraryImport(C)] private static partial void cairo_font_options_set_hint_metrics(nint options, int value);
    [LibraryImport(C)] private static partial void cairo_font_options_destroy(nint options);
    [LibraryImport(C)] private static partial nint cairo_image_surface_create(int format, int width, int height);
    [LibraryImport(C)] private static partial nint cairo_create(nint surface);
    [LibraryImport(C)] private static partial void cairo_set_source_rgba(nint context, double r, double g, double b, double a);
    [LibraryImport(C)] private static partial void cairo_move_to(nint context, double x, double y);
    [LibraryImport(C)] private static partial void cairo_translate(nint context, double x, double y);
    [LibraryImport(C)] private static partial void cairo_surface_flush(nint surface);
    [LibraryImport(C)] private static partial byte* cairo_image_surface_get_data(nint surface);
    [LibraryImport(C)] private static partial int cairo_image_surface_get_stride(nint surface);
    [LibraryImport(C)] private static partial int cairo_status(nint context);
    [LibraryImport(C)] private static partial int cairo_surface_status(nint surface);
    [LibraryImport(C)] private static partial void cairo_destroy(nint context);
    [LibraryImport(C)] private static partial void cairo_surface_destroy(nint surface);
}
