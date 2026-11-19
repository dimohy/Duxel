using System.Globalization;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxParagraphNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxPlatformTextBackend
{
    private readonly record struct NativeParagraphLine(nint Handle, int Start, int Length, Rectangle Bounds, int Baseline);

    public PlatformTextParagraphLayout CreateParagraph(PlatformTextParagraphRequest request, bool vectorMetrics)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // A native layout borrows all selected resources until its managed snapshot is complete.
            // Cache eviction resumes after the entire request, including requests with more than32 faces/sizes.
            _fontLeaseDepth++;
            nint layout = 0;
            LinuxParagraphFontSet? fonts = null;
            try
            {
                var normalized = new StringBuilder();
                var sourceBytes = new int[request.Text.Length + 1];
                var bytePosition = 0;
                for (var source = 0; source < request.Text.Length;)
                {
                    Rune.DecodeFromUtf16(request.Text.AsSpan(source), out var rune, out var count);
                    for (var unit = 0; unit < count; unit++)
                        sourceBytes[source + unit] = bytePosition;
                    normalized.Append(rune.Value == '\t' ? "    " : rune.ToString());
                    bytePosition += rune.Value == '\t' ? 4 : rune.Utf8SequenceLength;
                    source += count;
                }
                sourceBytes[^1] = bytePosition;
                var text = normalized.ToString();
                var objectPositions = request.Objects.ToArray().Select(item => item.Start).ToHashSet();
                var selections = new List<LinuxParagraphFontSet.Selection>();
                var styleFonts = new List<LinuxFontResource>();
                void AddSelection(int start, int end, LinuxFontResource font)
                {
                    if (selections.Count > 0 && selections[^1].End == start && ReferenceEquals(selections[^1].Font, font))
                        selections[^1] = selections[^1] with
                        {
                            End = end
                        };
                    else
                        selections.Add(new(start, end, font));
                }
                foreach (var style in request.Styles.Span)
                {
                    var primary = Font(style.FontPath, style.FontSize);
                    var secondary = style.SecondaryFontPath is { } path ? Font(path, style.FontSize) : null;
                    styleFonts.Add(primary);
                    if (secondary is not null)
                        styleFonts.Add(secondary);
                    var start = sourceBytes[style.Start];
                    var end = sourceBytes[style.Start + style.Length];
                    var selectedStart = start;
                    LinuxFontResource? selected = null;
                    var characterSource = style.Start;
                    foreach (var rune in request.Text.AsSpan(style.Start, style.Length).EnumerateRunes())
                    {
                        var font = objectPositions.Contains(characterSource) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format || rune.Value is '\r' or '\n' or '\t'
                            ? selected ?? primary : SelectFont(primary, secondary, rune);
                        if (selected is not null && !ReferenceEquals(selected, font))
                        {
                            AddSelection(selectedStart, start, selected);
                            selectedStart = start;
                        }
                        selected = font;
                        start += rune.Value == '\t' ? 4 : rune.Utf8SequenceLength;
                        characterSource += rune.Utf16SequenceLength;
                    }
                    if (selected is not null)
                        AddSelection(selectedStart, end, selected);
                }
                fonts = ParagraphFonts(styleFonts.ToArray());
                layout = fonts.CreateStyledLayout(text, selections.ToArray(), styleFonts[0], request.Width,
                    request.Direction == PlatformTextParagraphDirection.RightToLeft,
                    request.Objects.ToArray().Select(item => LinuxParagraphFontSet.ObjectShape(sourceBytes[item.Start],
                        sourceBytes[item.Start + 1], item.Width, item.Height, item.Baseline)).ToArray());
                return CaptureStyledParagraph(layout, request, sourceBytes, Encoding.UTF8.GetBytes(text), selections.ToArray());
            }
            finally
            {
                if (layout != 0)
                    g_object_unref(layout);
                fonts?.RestoreBaseDirection();
                _fontLeaseDepth--;
                while (_fontLeaseDepth == 0 && _fonts.Count > FontCapacity)
                {
                    var oldest = _order.Dequeue();
                    _fonts.Remove(oldest, out var retired);
                    retired!.Dispose();
                }
            }
        }
    }

    private static int OriginalPosition(int[] sourceBytes, int nativeByte)
    {
        var low = 0;
        var high = sourceBytes.Length - 1;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (sourceBytes[middle] < nativeByte)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private static PlatformTextParagraphLayout CaptureStyledParagraph(nint layout, PlatformTextParagraphRequest request,
        int[] sourceBytes, byte[] utf8, LinuxParagraphFontSet.Selection[] selections)
    {
        var nativeLines = new List<NativeParagraphLine>();
        var iterator = pango_layout_get_iter(layout);
        if (iterator == 0)
            throw new InvalidOperationException("Cannot enumerate styled paragraph lines.");
        try
        {
            do
            {
                var line = pango_layout_iter_get_line_readonly(iterator);
                pango_layout_iter_get_line_extents(iterator, null, out var bounds);
                nativeLines.Add(new(line, pango_layout_line_get_start_index(line), pango_layout_line_get_length(line),
                    bounds, pango_layout_iter_get_baseline(iterator)));
            }
            while (pango_layout_iter_next_line(iterator) != 0);
        }
        finally { pango_layout_iter_free(iterator); }

        var carets = new PlatformTextParagraphCaret[sourceBytes.Length];
        for (var index = 0; index < carets.Length; index++)
        {
            pango_layout_index_to_pos(layout, sourceBytes[index], out var position);
            carets[index] = new(position.X / (float)Scale, position.Y / (float)Scale);
        }
        var lines = new List<PlatformTextParagraphLine>();
        for (var index = 0; index < nativeLines.Count; index++)
        {
            var native = nativeLines[index];
            var start = OriginalPosition(sourceBytes, native.Start);
            var stop = OriginalPosition(sourceBytes, native.Start + native.Length);
            var next = index + 1 < nativeLines.Count ? OriginalPosition(sourceBytes, nativeLines[index + 1].Start) : request.Text.Length;
            var geometry = CaptureStyledLineGeometry(layout, native, request.Text, sourceBytes, utf8, start, stop);
            var visibleEnd = stop;
            while (visibleEnd > start && char.IsWhiteSpace(request.Text[visibleEnd - 1]))
                visibleEnd--;
            int* ranges = null;
            var formattedWidth = 0f;
            try
            {
                if (visibleEnd > start)
                {
                    pango_layout_line_get_x_ranges(native.Handle, sourceBytes[start], sourceBytes[visibleEnd], &ranges, out var count);
                    var left = int.MaxValue;
                    var right = int.MinValue;
                    for (var range = 0; range < count; range++)
                    {
                        left = Math.Min(left, ranges[range * 2]);
                        right = Math.Max(right, ranges[range * 2 + 1]);
                    }
                    if (count > 0)
                        formattedWidth = (right - left) / (float)Scale;
                }
            }
            finally { if (ranges != null) g_free(ranges); }
            lines.Add(new(start, stop - start, next - stop, native.Bounds.Y / (float)Scale,
                native.Bounds.Height / (float)Scale, (native.Baseline - native.Bounds.Y) / (float)Scale, geometry)
            {
                WidthWithoutTrailingWhitespace = formattedWidth
            });
        }
        var objects = new List<PlatformTextParagraphObjectPlacement>();
        var runs = CaptureStyledRuns(layout, request, sourceBytes, selections, objects);
        if (objects.Count != request.Objects.Length)
            throw new InvalidOperationException("Pango omitted declared inline object allocations.");
        pango_layout_get_size(layout, out var width, out var height);
        return new(runs, lines.ToArray(), carets, width / (float)Scale, height / (float)Scale,
            objects.OrderBy(item => item.Start).ToArray());
    }

    private static PlatformTextLineGeometry CaptureStyledLineGeometry(nint layout, NativeParagraphLine native,
        string text, int[] sourceBytes, byte[] utf8, int start, int stop)
    {
        var carets = new float[stop - start + 1];
        for (var index = 0; index < carets.Length; index++)
        {
            pango_layout_line_index_to_x(native.Handle, sourceBytes[start + index], 0, out var x);
            carets[index] = x / (float)Scale + native.Bounds.X / (float)Scale;
        }
        int HitSource(int x)
        {
            pango_layout_xy_to_index(layout, x, native.Bounds.Y + native.Bounds.Height / 2, out var position, out var trailing);
            for (var count = 0; count < trailing; count++)
            {
                if (Rune.DecodeFromUtf8(utf8.AsSpan(position), out _, out var consumed) != System.Buffers.OperationStatus.Done)
                    throw new InvalidOperationException("Pango returned an invalid styled paragraph hit range.");
                position += consumed;
            }
            return OriginalPosition(sourceBytes, position);
        }
        var boundaries = StringInfo.ParseCombiningCharacters(text.Substring(start, stop - start));
        var characters = new List<PlatformTextCharacterGeometry>();
        for (var index = 0; index < boundaries.Length; index++)
        {
            var from = start + boundaries[index];
            var to = index + 1 < boundaries.Length ? start + boundaries[index + 1] : stop;
            pango_layout_index_to_pos(layout, sourceBytes[from], out var position);
            var trailing = position.X + position.Width;
            if (text[from] == '\t')
                trailing = (int)MathF.Round(carets[to - start] * Scale);
            var left = Math.Min(position.X, trailing);
            var right = Math.Max(position.X, trailing);
            float? transition = null;
            if (right - left > 1 && text[from] != '\t')
            {
                var low = left;
                var high = right;
                var leftSource = position.Width < 0 ? to : from;
                var rightSource = position.Width < 0 ? from : to;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    var hit = HitSource(Math.Clamp(middle, left + 1, right - 1));
                    if (hit == leftSource)
                        low = middle + 1;
                    else if (hit == rightSource)
                        high = middle;
                    else
                        throw new NotSupportedException("Pango styled hits differ from original grapheme boundaries.");
                }
                transition = low / (float)Scale;
            }
            characters.Add(new(from - start, to - from, left / (float)Scale, (right - left) / (float)Scale,
                position.Width < 0 ? 1u : 0u)
            {
                HitBoundaryX = transition
            });
        }
        return new(native.Bounds.Width / (float)Scale, carets, characters.ToArray());
    }

    private static PlatformTextParagraphRun[] CaptureStyledRuns(nint layout, PlatformTextParagraphRequest request,
        int[] sourceBytes, LinuxParagraphFontSet.Selection[] selections, List<PlatformTextParagraphObjectPlacement> objects)
    {
        var objectMetrics = request.Objects.ToArray().ToDictionary(item => item.Start);
        var output = new List<PlatformTextParagraphRun>();
        var iterator = pango_layout_get_iter(layout);
        if (iterator == 0)
            throw new InvalidOperationException("Cannot enumerate styled paragraph glyphs.");
        try
        {
            do
            {
                var run = pango_layout_iter_get_run_readonly(iterator);
                if (run == null)
                    continue;
                var font = LinuxParagraphFontSet.VerifyFont(run->Item, selections);
                pango_layout_iter_get_run_extents(iterator, null, out var bounds);
                var x = bounds.X / (float)Scale;
                var baseline = (pango_layout_iter_get_baseline(iterator) - run->YOffset) / (float)Scale;
                var glyphs = new List<PlatformTextParagraphGlyph>();
                var currentStyle = -1;
                void Commit()
                {
                    if (glyphs.Count == 0)
                        return;
                    var resource = font.Oblique ? ObliquePrefix + Uri.EscapeDataString(font.Path) : font.Path;
                    output.Add(new(currentStyle, resource, font.Size, run->Item->Analysis.Level, new(bounds.X / (float)Scale, baseline), glyphs.ToArray()));
                    glyphs.Clear();
                }
                for (var index = 0; index < run->Glyphs->Count; index++)
                {
                    var glyph = run->Glyphs->Glyphs[index];
                    var source = LinuxParagraphSourceMap.GlyphPosition(sourceBytes, run->Item->Offset + run->Glyphs->Clusters[index]);
                    if (objectMetrics.TryGetValue(source, out var atomic))
                    {
                        Commit();
                        var shape = LinuxParagraphFontSet.ObjectShape(0, 0, atomic.Width, atomic.Height, atomic.Baseline).Logical;
                        objects.Add(new(source, new(x + glyph.XOffset / (float)Scale,
                            baseline + (glyph.YOffset + shape.Y) / (float)Scale,
                            shape.Width / (float)Scale, shape.Height / (float)Scale), (run->Item->Analysis.Level & 1) != 0));
                        x += glyph.Width / (float)Scale;
                        continue;
                    }
                    var style = 0;
                    while (style + 1 < request.Styles.Length && source >= request.Styles.Span[style].Start + request.Styles.Span[style].Length)
                        style++;
                    if (style != currentStyle)
                    {
                        Commit();
                        currentStyle = style;
                    }
                    if (glyph.Glyph == 0 || (glyph.Glyph & 0x10000000) != 0)
                        throw new NotSupportedException("The explicit styled paragraph font produced a missing glyph.");
                    if (glyph.Glyph != 0x0FFFFFFF)
                        glyphs.Add(new(glyph.Glyph, source, glyph.Width / (float)Scale, glyph.XOffset / (float)Scale,
                            -glyph.YOffset / (float)Scale, x + glyph.XOffset / (float)Scale, baseline + glyph.YOffset / (float)Scale));
                    x += glyph.Width / (float)Scale;
                }
                Commit();
            }
            while (pango_layout_iter_next_run(iterator) != 0);
        }
        finally { pango_layout_iter_free(iterator); }
        return output.ToArray();
    }
}
