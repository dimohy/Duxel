using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using static Duxel.Platform.Linux.LinuxParagraphNative;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

/// <summary>Owns an isolated Pango font map containing only the requested immutable files.</summary>
internal sealed unsafe partial class LinuxParagraphFontSet : IDisposable
{
    internal readonly record struct Selection(int Start, int End, LinuxFontResource Font);
    private nint _map, _context, _primaryDescription, _secondaryDescription;
    private readonly Dictionary<(string Path, bool Oblique), nint> _descriptions = [];
    private readonly Dictionary<(string Path, bool Oblique), nint> _projectedDescriptions = [];

    internal LinuxParagraphFontSet(LinuxFontResource primary, LinuxFontResource? secondary)
        : this(secondary is null ? [primary] : [primary, secondary]) { }

    internal LinuxParagraphFontSet(ReadOnlySpan<LinuxFontResource> fonts)
    {
        if (pango_version_check(1, 48, 0) != 0)
            throw new NotSupportedException("Native paragraph layout requires Pango1.48 or later.");
        nint config = 0, options = 0;
        try
        {
            config = Require(FcConfigCreate(), "private Fontconfig config");
            foreach (var path in fonts.ToArray().Select(font => font.Path).Distinct(StringComparer.Ordinal))
                if (FcConfigAppFontAddFile(config, path) == 0)
                    throw new InvalidOperationException("Cannot register an explicit paragraph font in the private config.");
            if (FcConfigBuildFonts(config) == 0)
                throw new InvalidOperationException("Cannot register the explicit paragraph fonts in the private config.");
            _map = Require(pango_cairo_font_map_new_for_font_type(1), "FreeType Cairo font map");
            pango_fc_font_map_set_config(_map, config);
            pango_cairo_font_map_set_resolution(_map, 72);
            _context = Require(pango_font_map_create_context(_map), "paragraph context");
            pango_context_set_round_glyph_positions(_context, 0);
            pango_context_set_base_dir(_context, 0);
            fixed (byte* language = "und\0"u8)
                pango_context_set_language(_context, pango_language_from_string(language));
            options = cairo_font_options_create();
            cairo_font_options_set_antialias(options, 2);
            cairo_font_options_set_hint_style(options, 1);
            cairo_font_options_set_hint_metrics(options, 1);
            pango_cairo_context_set_font_options(_context, options);
            foreach (var font in fonts)
                if (!_descriptions.ContainsKey((font.Path, font.Oblique)))
                    _descriptions.Add((font.Path, font.Oblique), Description(font));
            _primaryDescription = FontDescription(fonts[0]);
            if (fonts.Length > 1)
                _secondaryDescription = FontDescription(fonts[1]);
        }
        catch { Dispose(); throw; }
        finally
        {
            if (options != 0)
                cairo_font_options_destroy(options);
            if (config != 0)
                FcConfigDestroy(config);
        }
    }

    private static nint Description(LinuxFontResource font)
    {
        var pattern = Require(FcFreeTypeQuery(font.Path, 0, 0, out _), "explicit font pattern");
        try
        {
            var description = Require(pango_fc_font_description_from_pattern(pattern, 0), "explicit font description");
            if (font.Oblique)
                pango_font_description_set_style(description, 1);
            return description;
        }
        finally { FcPatternDestroy(pattern); }
    }

    private nint FontDescription(LinuxFontResource font) => _descriptions[(font.Path, font.Oblique)];

    private nint ProjectedDescription(LinuxFontResource font)
    {
        if (font.ProjectedVariations.Length == 0)
            return FontDescription(font);
        var key = (font.Path, font.Oblique);
        if (_projectedDescriptions.TryGetValue(key, out var description))
            return description;
        description = Require(pango_font_description_copy(FontDescription(font)), "projected font description");
        pango_font_description_set_variations(description, font.ProjectedVariations);
        _projectedDescriptions.Add(key, description);
        return description;
    }

    internal nint CreateStyledLayout(string text, ReadOnlySpan<Selection> selections, LinuxFontResource emptyFont,
        float width, bool rightToLeft, ReadOnlySpan<Shape> shapes = default)
    {
        ObjectDisposedException.ThrowIf(_context == 0, this);
        if (pango_version_check(1, 50, 0) != 0)
            throw new NotSupportedException("Styled paragraph snapshots require Pango1.50 or later.");
        pango_context_set_base_dir(_context, rightToLeft ? 1 : 0);
        var layout = Require(pango_layout_new(_context), "styled paragraph layout");
        nint attributes = 0;
        try
        {
            var initial = FontDescription(emptyFont);
            pango_font_description_set_absolute_size(initial, emptyFont.Size * Scale);
            pango_layout_set_font_description(layout, initial);
            pango_layout_set_auto_dir(layout, 0);
            pango_layout_set_single_paragraph_mode(layout, 0);
            pango_layout_set_width(layout, float.IsPositiveInfinity(width) ? -1 : checked((int)Math.Round(width * Scale)));
            pango_layout_set_wrap(layout, 2); // WORD_CHAR: words first, graphemes for an overlong word.
            attributes = Require(pango_attr_list_new(), "styled paragraph attributes");
            foreach (var selection in selections)
            {
                var description = FontDescription(selection.Font);
                pango_font_description_set_absolute_size(description, selection.Font.Size * Scale);
                var attribute = pango_attr_font_desc_new(description);
                if (attribute == null)
                    throw new OutOfMemoryException("Cannot allocate a styled paragraph font range.");
                attribute->Start = (uint)selection.Start;
                attribute->End = (uint)selection.End;
                pango_attr_list_insert(attributes, attribute);
            }
            foreach (var shape in shapes)
            {
                var ink = default(Rectangle);
                var logical = shape.Logical;
                var attribute = pango_attr_shape_new(&ink, &logical);
                if (attribute == null)
                    throw new OutOfMemoryException("Cannot allocate a native inline object shape.");
                attribute->Start = (uint)shape.Start;
                attribute->End = (uint)shape.End;
                pango_attr_list_insert(attributes, attribute);
            }
            pango_layout_set_attributes(layout, attributes);
            var bytes = Encoding.UTF8.GetBytes(text + '\0');
            fixed (byte* pointer = bytes)
                pango_layout_set_text(layout, pointer, bytes.Length - 1);
            if (pango_layout_get_unknown_glyphs_count(layout) != 0)
                throw new NotSupportedException("The explicit styled paragraph fonts produced unknown glyphs.");
            var result = layout;
            layout = 0;
            return result;
        }
        finally
        {
            if (attributes != 0)
                pango_attr_list_unref(attributes);
            if (layout != 0)
                g_object_unref(layout);
        }
    }

    internal void RestoreBaseDirection() => pango_context_set_base_dir(_context, 0);

    internal nint CreateLayout(string text, float size, LinuxFontResource primary, LinuxFontResource? secondary,
        out Selection[] selections, bool rightToLeft = false, bool projected = false)
    {
        selections = [];
        ObjectDisposedException.ThrowIf(_context == 0, this);
        pango_context_set_base_dir(_context, rightToLeft ? 1 : 0);
        var bytes = Encoding.UTF8.GetBytes(text + '\0');
        var layout = Require(pango_layout_new(_context), "paragraph layout");
        nint attributes = 0;
        try
        {
            var primaryDescription = projected ? ProjectedDescription(primary) : _primaryDescription;
            var secondaryDescription = projected && secondary is not null ? ProjectedDescription(secondary) : _secondaryDescription;
            pango_font_description_set_absolute_size(primaryDescription, size * Scale);
            if (secondaryDescription != 0)
                pango_font_description_set_absolute_size(secondaryDescription, size * Scale);
            pango_layout_set_font_description(layout, primaryDescription);
            pango_layout_set_auto_dir(layout, 0);
            pango_layout_set_single_paragraph_mode(layout, 1);
            attributes = Require(pango_attr_list_new(), "paragraph attributes");
            var byteOffset = 0;
            var runStart = 0;
            var selectedRanges = new List<Selection>();
            LinuxFontResource? selected = null;
            void AddRange(LinuxFontResource font, int start, int end)
            {
                selectedRanges.Add(new(start, end, font));
                var attribute = pango_attr_font_desc_new(ReferenceEquals(font, primary) ? primaryDescription : secondaryDescription);
                if (attribute == null)
                    throw new OutOfMemoryException("Cannot allocate the explicit paragraph font range.");
                attribute->Start = (uint)start;
                attribute->End = (uint)end;
                pango_attr_list_insert(attributes, attribute);
            }
            foreach (var rune in text.EnumerateRunes())
            {
                var font = Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format ? selected ?? primary
                    : LinuxPlatformTextBackend.SelectFont(primary, secondary, rune, selected);
                if (selected is not null && !ReferenceEquals(selected, font))
                {
                    AddRange(selected, runStart, byteOffset);
                    runStart = byteOffset;
                }
                selected = font;
                byteOffset += rune.Utf8SequenceLength;
            }
            if (selected is not null)
                AddRange(selected, runStart, byteOffset);
            pango_layout_set_attributes(layout, attributes);
            fixed (byte* pointer = bytes)
                pango_layout_set_text(layout, pointer, bytes.Length - 1);
            if (pango_layout_get_unknown_glyphs_count(layout) != 0)
                throw new NotSupportedException("The explicit paragraph fonts produced unknown glyphs.");
            var result = layout;
            selections = selectedRanges.ToArray();
            layout = 0;
            return result;
        }
        finally
        {
            if (attributes != 0)
                pango_attr_list_unref(attributes);
            if (layout != 0)
                g_object_unref(layout);
        }
    }

    internal static LinuxFontResource VerifyFont(Item* item, ReadOnlySpan<Selection> selections, bool projected = false)
    {
        var low = 0;
        var high = selections.Length - 1;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (selections[middle].End <= item->Offset)
                low = middle + 1;
            else
                high = middle;
        }
        if (selections.IsEmpty || item->Offset < selections[low].Start || item->Length > selections[low].End - item->Offset)
            throw new InvalidOperationException("Native shaping crossed an explicit font-selection range.");
        var expected = selections[low].Font;
        VerifyFontResource(item->Analysis.Font, expected, projected);
        return expected;
    }

    private static void VerifyFontResource(nint nativeFont, LinuxFontResource expected, bool projected = false)
    {
        var pattern = pango_fc_font_get_pattern(nativeFont);
        if (pattern == 0 || FcPatternGetString(pattern, "file", 0, out var path) != 0
            || FcPatternGetInteger(pattern, "index", 0, out var index) != 0 || (index & 0xffff) != 0)
            throw new InvalidOperationException("Paragraph shaping did not identify an explicit index0 font file.");
        var actual = Marshal.PtrToStringUTF8(path) ?? throw new InvalidOperationException("Native font path is absent.");
        if (actual != expected.Path)
            throw new InvalidOperationException("Paragraph shaping substituted the selected font file: " + actual + " / expected: " + expected.Path);
        if (projected && expected.ProjectedVariations.Length != 0)
        {
            // A named-instance pattern may resolve to the default coordinates after applying the description.
            var shaper = Require(pango_font_get_hb_font(nativeFont), "projected font shaper");
            var coordinates = hb_font_get_var_coords_normalized(shaper, out var length);
            for (var axis = 0u; axis < length; axis++)
                if (coordinates[axis] != 0)
                    throw new InvalidOperationException("Projected shaping substituted a different font variation.");
        }
        else if (index != 0)
            throw new InvalidOperationException("Paragraph shaping substituted a named font instance.");
    }

    private static nint Require(nint value, string operation) => value != 0 ? value
        : throw new InvalidOperationException("Cannot create " + operation + ".");

    public void Dispose()
    {
        foreach (var description in _projectedDescriptions.Values)
            pango_font_description_free(description);
        _projectedDescriptions.Clear();
        foreach (var description in _descriptions.Values)
            pango_font_description_free(description);
        _descriptions.Clear();
        if (_context != 0)
            g_object_unref(_context);
        if (_map != 0)
            g_object_unref(_map);
        _secondaryDescription = _primaryDescription = _context = _map = 0;
    }
}
