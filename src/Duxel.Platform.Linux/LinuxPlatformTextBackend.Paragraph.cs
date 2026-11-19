using System.Globalization;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxParagraphNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxPlatformTextBackend
{
    private readonly Dictionary<string, LinuxParagraphFontSet> _paragraphFonts = new(StringComparer.Ordinal);
    private readonly Queue<string> _paragraphOrder = [];
    private const int ParagraphFontCapacity = 16;

    private LinuxParagraphFontSet ParagraphFonts(LinuxFontResource primary, LinuxFontResource? secondary)
        => ParagraphFonts(secondary is null ? [primary] : [primary, secondary]);

    private LinuxParagraphFontSet ParagraphFonts(LinuxFontResource[] resources)
    {
        var distinct = resources.DistinctBy(font => (font.Path, font.Oblique)).ToArray();
        var key = string.Concat(distinct.Select(font => font.Path.Length + ":" + font.Path + (font.Oblique ? "1" : "0")));
        if (_paragraphFonts.TryGetValue(key, out var fonts))
            return fonts;
        fonts = new(distinct);
        if (_paragraphFonts.Count == ParagraphFontCapacity)
        {
            var previous = _paragraphOrder.Dequeue();
            _paragraphFonts.Remove(previous, out var retired);
            retired!.Dispose();
        }
        _paragraphFonts.Add(key, fonts);
        _paragraphOrder.Enqueue(key);
        return fonts;
    }

    private List<Run> ShapeParagraph(string text, LinuxFontResource primary, LinuxFontResource? secondary, double y,
        out float advance)
    {
        var layout = ParagraphFonts(primary, secondary).CreateLayout(text, primary.Size, primary, secondary, out var selections);
        nint iterator = 0;
        var runs = new List<Run>();
        try
        {
            pango_layout_get_size(layout, out var width, out _);
            advance = width / (float)Scale;
            var baseline = pango_layout_get_baseline(layout);
            iterator = pango_layout_get_iter(layout);
            if (iterator == 0)
                throw new InvalidOperationException("Cannot enumerate the native paragraph runs.");
            do
            {
                var run = pango_layout_iter_get_run_readonly(iterator);
                if (run == null)
                    continue;
                var font = LinuxParagraphFontSet.VerifyFont(run->Item, selections);
                pango_layout_iter_get_run_extents(iterator, null, out var logical);
                var x = logical.X / (double)Scale;
                var runY = y + (pango_layout_iter_get_baseline(iterator) - baseline - run->YOffset) / (double)Scale;
                var glyphs = new List<LinuxFontNative.Glyph>(run->Glyphs->Count);
                for (var index = 0; index < run->Glyphs->Count; index++)
                {
                    var glyph = run->Glyphs->Glyphs[index];
                    if (glyph.Glyph == 0 || (glyph.Glyph & 0x10000000) != 0)
                        throw new NotSupportedException("The explicit paragraph font produced a missing glyph.");
                    if (glyph.Glyph != 0x0FFFFFFF)
                        glyphs.Add(new(glyph.Glyph, x + glyph.XOffset / (double)Scale, runY + glyph.YOffset / (double)Scale));
                    x += glyph.Width / (double)Scale;
                }
                runs.Add(new(font, glyphs.ToArray()));
            }
            while (pango_layout_iter_next_run(iterator) != 0);
            return runs;
        }
        finally
        {
            if (iterator != 0)
                pango_layout_iter_free(iterator);
            g_object_unref(layout);
        }
    }

    public PlatformTextLineGeometry CreateLineGeometry(in PlatformTextMeasureRequest request, bool vectorMetrics)
    {
        ArgumentNullException.ThrowIfNull(request.Text);
        _ = StrictUtf16.GetByteCount(request.Text);
        if (request.Text.Contains('\r') || request.Text.Contains('\n'))
            throw new ArgumentException("Line geometry requires one complete visual row.");
        lock (_sync)
        {
            var primary = Font(request.FontPath, request.FontSize);
            var secondary = request.SecondaryFontPath is { } path ? Font(path, request.FontSize, primary) : null;
            var normalized = new StringBuilder();
            var bytes = new int[request.Text.Length + 1];
            var sourceIndex = 0;
            var byteIndex = 0;
            foreach (var rune in request.Text.EnumerateRunes())
            {
                for (var index = 0; index < rune.Utf16SequenceLength; index++)
                    bytes[sourceIndex + index] = byteIndex;
                normalized.Append(rune.Value == '\t' ? "    " : rune.ToString());
                byteIndex += rune.Value == '\t' ? 4 : rune.Utf8SequenceLength;
                sourceIndex += rune.Utf16SequenceLength;
            }
            bytes[^1] = byteIndex;
            var normalizedUtf8 = Encoding.UTF8.GetBytes(normalized.ToString());
            var layout = ParagraphFonts(primary, secondary).CreateLayout(normalized.ToString(), request.FontSize, primary, secondary, out var selections);
            try
            {
                var iterator = pango_layout_get_iter(layout);
                if (iterator == 0)
                    throw new InvalidOperationException("Cannot validate native geometry fonts.");
                try
                {
                    do
                    {
                        var run = pango_layout_iter_get_run_readonly(iterator);
                        if (run != null)
                            LinuxParagraphFontSet.VerifyFont(run->Item, selections);
                    }
                    while (pango_layout_iter_next_run(iterator) != 0);
                }
                finally { pango_layout_iter_free(iterator); }
                var carets = new float[bytes.Length];
                for (var index = 0; index < bytes.Length; index++)
                {
                    pango_layout_index_to_pos(layout, bytes[index], out var position);
                    carets[index] = position.X / (float)Scale;
                }
                var boundaries = StringInfo.ParseCombiningCharacters(request.Text);
                var characters = new List<PlatformTextCharacterGeometry>();
                pango_layout_get_size(layout, out var width, out var nativeHeight);
                int HitSource(int x)
                {
                    pango_layout_xy_to_index(layout, x, nativeHeight / 2, out var nativeByte, out var trailingCharacters);
                    for (var count = 0; count < trailingCharacters; count++)
                    {
                        if (Rune.DecodeFromUtf8(normalizedUtf8.AsSpan(nativeByte), out _, out var consumed) != System.Buffers.OperationStatus.Done)
                            throw new InvalidOperationException("Pango returned an invalid UTF8 hit range.");
                        nativeByte += consumed;
                    }
                    var low = 0;
                    var high = bytes.Length - 1;
                    while (low < high)
                    {
                        var middle = low + (high - low) / 2;
                        if (bytes[middle] < nativeByte)
                            low = middle + 1;
                        else
                            high = middle;
                    }
                    return low;
                }
                for (var index = 0; index < boundaries.Length; index++)
                {
                    var start = boundaries[index];
                    var end = index + 1 < boundaries.Length ? boundaries[index + 1] : request.Text.Length;
                    pango_layout_index_to_pos(layout, bytes[start], out var position);
                    var trailing = position.X + position.Width;
                    if (request.Text[start] == '\t')
                        trailing = (int)MathF.Round(carets[end] * Scale);
                    var left = Math.Min(position.X, trailing);
                    var right = Math.Max(position.X, trailing);
                    float? hitBoundary = null;
                    if (right - left > 1 && request.Text[start] != '\t')
                    {
                        // Native grapheme hit transitions need not coincide with their visual midpoint.
                        // Snapshot the transition once while the layout is alive; later hits stay managed.
                        var low = left;
                        var high = right;
                        var leftPosition = position.Width < 0 ? end : start;
                        var rightPosition = position.Width < 0 ? start : end;
                        while (low < high)
                        {
                            var middle = low + (high - low) / 2;
                            var hit = HitSource(Math.Clamp(middle, left + 1, right - 1));
                            if (hit == leftPosition)
                                low = middle + 1;
                            else if (hit == rightPosition)
                                high = middle;
                            else
                                throw new NotSupportedException("Pango pointer boundaries differ from the original source grapheme.");
                        }
                        hitBoundary = low / (float)Scale;
                    }
                    characters.Add(new(start, end - start, left / (float)Scale,
                        (right - left) / (float)Scale, position.Width < 0 ? 1u : 0u)
                    {
                        HitBoundaryX = hitBoundary
                    });
                }
                return new(width / (float)Scale, carets, characters.ToArray());
            }
            finally { g_object_unref(layout); }
        }
    }
}
