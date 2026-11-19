using System.Runtime.InteropServices;
using System.Text;

internal sealed unsafe partial class PangoLineOracle
{
    internal readonly record struct StyledSpan(string Text, int Face, float Size);
    internal readonly record struct StyledCaret(float X, float Y);
    internal readonly record struct StyledLine(int Start, int End, float FormattedWidth);
    internal sealed record StyledSnapshot(float Width, float Height, StyledCaret[] Carets, StyledLine[] Lines, byte[] Pixels)
    {
        internal List<RunAlpha> Trace { get; } = [];
    }

    internal readonly record struct RunAlpha(byte[] Destination, byte[] Mask);
    internal enum RunSurface { Direct, Cropped, FullSize, FullSizeMarked, CroppedMarked }
    internal CairoGlyph[] LastNativeGlyphs { get; private set; } = [];

    internal StyledSnapshot CaptureStyled(StyledSpan[] spans, float width, bool rtl, bool nativeGlyphPaint = false, bool translatedGlyphPaint = false,
        RunSurface runSurface = RunSurface.Direct, bool omitInkless = false, int cropPadding = 1, int[]? traceOffsets = null)
    {
        var source = string.Concat(spans.Select(span => span.Text));
        var text = source.Replace("\t", "    ", StringComparison.Ordinal);
        var utf8 = Encoding.UTF8.GetBytes(text + '\0');
        var map = new int[source.Length + 1];
        var offset = 0;
        for (var index = 0; index < source.Length;)
        {
            Rune.DecodeFromUtf16(source.AsSpan(index), out var rune, out var count);
            for (var unit = 0; unit < count; unit++)
                map[index + unit] = offset;
            offset += rune.Value == '\t' ? 4 : rune.Utf8SequenceLength;
            index += count;
        }
        map[^1] = offset;
        pango_context_set_base_dir(_context, rtl ? 1 : 0);
        var layout = pango_layout_new(_context);
        nint attributes = 0, surface = 0, cairo = 0;
        try
        {
            attributes = pango_attr_list_new();
            var start = 0;
            foreach (var span in spans)
            {
                var font = span.Face == 0 ? _description : span.Face == 1 ? _secondaryDescription : _additionalDescriptions[span.Face - 2];
                pango_font_description_set_absolute_size(font, span.Size * 1024);
                if (start == 0)
                    pango_layout_set_font_description(layout, font);
                var attribute = pango_attr_font_desc_new(font);
                if (attribute == null)
                    throw new OutOfMemoryException();
                attribute->Start = (uint)map[start];
                attribute->End = (uint)map[start + span.Text.Length];
                pango_attr_list_insert(attributes, attribute);
                start += span.Text.Length;
            }
            pango_layout_set_attributes(layout, attributes);
            pango_layout_set_auto_dir(layout, 0);
            pango_layout_set_single_paragraph_mode(layout, 0);
            pango_layout_set_width(layout, float.IsPositiveInfinity(width) ? -1 : checked((int)Math.Round(width * 1024)));
            pango_layout_set_wrap(layout, 2);
            fixed (byte* pointer = utf8)
                pango_layout_set_text(layout, pointer, utf8.Length - 1);
            if (pango_layout_get_unknown_glyphs_count(layout) != 0)
                throw new InvalidOperationException("Independent styled oracle contains unknown glyphs.");
            var carets = new StyledCaret[map.Length];
            for (var index = 0; index < map.Length; index++)
            {
                pango_layout_index_to_pos(layout, map[index], out var position);
                carets[index] = new(position.X / 1024f, position.Y / 1024f);
            }
            int SourcePosition(int atByte)
            {
                var index = Array.FindIndex(map, value => value >= atByte);
                return index < 0 ? source.Length : index;
            }
            var lines = new List<StyledLine>();
            for (var index = 0; index < pango_layout_get_line_count(layout); index++)
            {
                var line = pango_layout_get_line_readonly(layout, index);
                var from = pango_layout_line_get_start_index(line);
                var sourceStart = SourcePosition(from);
                var sourceEnd = SourcePosition(from + pango_layout_line_get_length(line));
                var visible = sourceEnd;
                while (visible > sourceStart && char.IsWhiteSpace(source[visible - 1]))
                    visible--;
                int* ranges = null;
                var formatted = 0f;
                try
                {
                    if (visible > sourceStart)
                    {
                        pango_layout_line_get_x_ranges(line, map[sourceStart], map[visible], &ranges, out var count);
                        var coordinates = new ReadOnlySpan<int>(ranges, count * 2).ToArray();
                        if (count > 0)
                            formatted = (coordinates.Max() - coordinates.Min()) / 1024f;
                    }
                }
                finally { if (ranges != null) g_free(ranges); }
                lines.Add(new(sourceStart, sourceEnd, formatted));
            }
            pango_layout_get_size(layout, out var nativeWidth, out var height);
            surface = cairo_image_surface_create(2, Width, Height);
            cairo = cairo_create(surface);
            cairo_set_source_rgba(cairo, 1, 1, 1, 1);
            cairo_move_to(cairo, 20, 20);
            var snapshot = new StyledSnapshot(nativeWidth / 1024f, height / 1024f, carets, lines.ToArray(), new byte[Width * Height]);
            if (nativeGlyphPaint)
            {
                if (translatedGlyphPaint)
                    cairo_translate(cairo, 20, 20);
                DrawOriginalNativeGlyphs(cairo, surface, layout, translatedGlyphPaint ? 0 : 20, runSurface, omitInkless, cropPadding,
                    traceOffsets, snapshot.Trace);
            }
            else
                pango_cairo_show_layout(cairo, layout);
            cairo_surface_flush(surface);
            if (cairo_status(cairo) != 0 || cairo_surface_status(surface) != 0)
                throw new InvalidOperationException("Independent styled paragraph drawing failed.");
            var data = cairo_image_surface_get_data(surface);
            var stride = cairo_image_surface_get_stride(surface);
            for (var row = 0; row < Height; row++)
                new ReadOnlySpan<byte>(data + row * stride, Width).CopyTo(snapshot.Pixels.AsSpan(row * Width, Width));
            return snapshot;
        }
        finally
        {
            if (cairo != 0)
                cairo_destroy(cairo);
            if (surface != 0)
                cairo_surface_destroy(surface);
            if (attributes != 0)
                pango_attr_list_unref(attributes);
            if (layout != 0)
                g_object_unref(layout);
            pango_context_set_base_dir(_context, 0);
        }
    }

    private void DrawOriginalNativeGlyphs(nint cairo, nint surface, nint layout, double origin, RunSurface runSurface,
        bool omitInkless, int cropPadding, int[]? traceOffsets, List<RunAlpha> trace)
    {
        var captured = new List<CairoGlyph>();
        var iterator = pango_layout_get_iter(layout);
        try
        {
            do
            {
                var item = pango_layout_iter_get_run_readonly(iterator);
                if (item == null)
                    continue;
                pango_layout_iter_get_run_extents(iterator, null, out var logical);
                var x = logical.X / 1024d;
                var y = (pango_layout_iter_get_baseline(iterator) - item->YOffset) / 1024d;
                var glyphs = new List<CairoGlyph>();
                for (var index = 0; index < item->Glyphs->Count; index++)
                {
                    var glyph = item->Glyphs->Glyphs[index];
                    if (glyph.Index != 0x0FFFFFFF)
                    {
                        glyphs.Add(new(glyph.Index, origin + x + glyph.X / 1024d, origin + y + glyph.Y / 1024d));
                        captured.Add(new(glyph.Index, x + glyph.X / 1024d, y + glyph.Y / 1024d));
                    }
                    x += glyph.Width / 1024d;
                }
                var font = pango_cairo_font_get_scaled_font(item->Item->Analysis.Font);
                cairo_set_scaled_font(cairo, font);
                var values = omitInkless ? glyphs.Where(glyph => HasInk(font, glyph.Index)).ToArray() : glyphs.ToArray();
                byte[] mask = [];
                if (runSurface is RunSurface.Cropped or RunSurface.CroppedMarked)
                    DrawCroppedOriginalRun(cairo, font, values, cropPadding, markDestination : runSurface == RunSurface.CroppedMarked);
                else if (runSurface is RunSurface.FullSize or RunSurface.FullSizeMarked)
                    mask = CompositeOriginalRun(cairo, font, values, 0, 0, Width, Height, traceOffsets,
                        markDestination : runSurface == RunSurface.FullSizeMarked);
                else
                    fixed (CairoGlyph* pointer = values)
                        cairo_show_glyphs(cairo, pointer, values.Length);
                if (traceOffsets is not null)
                    trace.Add(new(ReadAlpha(surface, traceOffsets), mask));
            }
            while (pango_layout_iter_next_run(iterator) != 0);
        }
        finally { pango_layout_iter_free(iterator); }
        LastNativeGlyphs = captured.ToArray();
    }

    private static bool HasInk(nint font, nuint index)
    {
        var glyph = new CairoGlyph(index, 0, 0);
        cairo_scaled_font_glyph_extents(font, &glyph, 1, out var bounds);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static void DrawCroppedOriginalRun(nint target, nint font, CairoGlyph[] glyphs, int padding, bool markDestination)
    {
        double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (var placed in glyphs)
        {
            var glyph = new CairoGlyph(placed.Index, 0, 0);
            cairo_scaled_font_glyph_extents(font, &glyph, 1, out var bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                continue;
            left = Math.Min(left, placed.X + bounds.X);
            top = Math.Min(top, placed.Y + bounds.Y);
            right = Math.Max(right, placed.X + bounds.X + bounds.Width);
            bottom = Math.Max(bottom, placed.Y + bounds.Y + bounds.Height);
        }
        if (!double.IsFinite(left))
            return;
        left = Math.Floor(left) - padding;
        top = Math.Floor(top) - padding;
        CompositeOriginalRun(target, font, glyphs, left, top,
            (int)(Math.Ceiling(right) + padding - left), (int)(Math.Ceiling(bottom) + padding - top), markDestination : markDestination);
    }

    private static byte[] CompositeOriginalRun(nint target, nint font, CairoGlyph[] glyphs,
        double left, double top, int width, int height, int[]? traceOffsets = null, bool markDestination = false)
    {
        var surface = cairo_image_surface_create(2, width, height);
        var context = cairo_create(surface);
        try
        {
            cairo_translate(context, -left, -top);
            cairo_set_source_rgba(context, 1, 1, 1, 1);
            if (markDestination)
            {
                // A pixel outside the paragraph distinguishes a cleared destination from an empty sample.
                cairo_rectangle(context, width - 1, height - 1, 1, 1);
                cairo_fill(context);
            }
            cairo_set_scaled_font(context, font);
            fixed (CairoGlyph* pointer = glyphs)
                cairo_show_glyphs(context, pointer, glyphs.Length);
            if (markDestination)
            {
                cairo_set_operator(context, 0); // CAIRO_OPERATOR_CLEAR
                cairo_rectangle(context, width - 1, height - 1, 1, 1);
                cairo_fill(context);
            }
            cairo_surface_flush(surface);
            if (cairo_status(context) != 0 || cairo_surface_status(surface) != 0)
                throw new InvalidOperationException("Independent glyph run drawing failed.");
            cairo_mask_surface(target, surface, left, top);
            return traceOffsets is null ? [] : ReadAlpha(surface, traceOffsets);
        }
        finally { cairo_destroy(context); cairo_surface_destroy(surface); }
    }

    private static byte[] ReadAlpha(nint surface, int[] offsets)
    {
        cairo_surface_flush(surface);
        var data = cairo_image_surface_get_data(surface);
        var stride = cairo_image_surface_get_stride(surface);
        var samples = new byte[offsets.Length];
        for (var index = 0; index < offsets.Length; index++)
        {
            var offset = offsets[index];
            samples[index] = data[offset / Width * stride + offset % Width];
        }
        return samples;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct CairoBounds
    {
        internal double X, Y, Width, Height, AdvanceX, AdvanceY;
    }
    [LibraryImport(C)] private static partial void cairo_scaled_font_glyph_extents(nint font, CairoGlyph* glyphs, int count, out CairoBounds bounds);
    [LibraryImport(C)] private static partial void cairo_mask_surface(nint target, nint surface, double x, double y);
    [LibraryImport(C)] private static partial void cairo_rectangle(nint cairo, double x, double y, double width, double height);
    [LibraryImport(C)] private static partial void cairo_fill(nint cairo);
    [LibraryImport(C)] private static partial void cairo_set_operator(nint cairo, int operation);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeAnalysis
    {
        internal nint Shape, LanguageEngine, Font; internal byte Level, Gravity, Flags, Script; internal nint Language, Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeItem
    {
        internal int Offset, Length, Count; internal NativeAnalysis Analysis;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGlyph
    {
        internal uint Index; internal int Width, X, Y; internal uint Attributes;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGlyphString
    {
        internal int Count; internal NativeGlyph* Glyphs; internal int* Clusters;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGlyphItem
    {
        internal NativeItem* Item; internal NativeGlyphString* Glyphs; internal int YOffset, StartX, EndX;
    }
    [StructLayout(LayoutKind.Sequential)] internal readonly record struct CairoGlyph(nuint Index, double X, double Y);
    [LibraryImport(P)] private static partial nint pango_layout_get_iter(nint layout);
    [LibraryImport(P)] private static partial NativeGlyphItem* pango_layout_iter_get_run_readonly(nint iterator);
    [LibraryImport(P)] private static partial void pango_layout_iter_get_run_extents(nint iterator, Rectangle* ink, out Rectangle logical);
    [LibraryImport(P)] private static partial int pango_layout_iter_get_baseline(nint iterator);
    [LibraryImport(P)] private static partial int pango_layout_iter_next_run(nint iterator);
    [LibraryImport(P)] private static partial void pango_layout_iter_free(nint iterator);
    [LibraryImport(PC)] private static partial nint pango_cairo_font_get_scaled_font(nint font);
    [LibraryImport(C)] private static partial void cairo_set_scaled_font(nint cairo, nint font);
    [LibraryImport(C)] private static partial void cairo_show_glyphs(nint cairo, CairoGlyph* glyphs, int count);
    [LibraryImport(P)] private static partial void pango_layout_set_width(nint layout, int width);
    [LibraryImport(P)] private static partial void pango_layout_set_wrap(nint layout, int wrap);
    [LibraryImport(P)] private static partial int pango_layout_get_line_count(nint layout);
    [LibraryImport(P)] private static partial int pango_layout_line_get_start_index(nint line);
    [LibraryImport(P)] private static partial int pango_layout_line_get_length(nint line);
}
