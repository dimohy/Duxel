using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;
using Duxel.Interop.Windows;

namespace Duxel.Platform.Windows;

internal sealed unsafe partial class WindowsDirectWriteGlyphRasterizer
{
    [ThreadStatic] private static ParagraphBatch? _paragraphBatch;

    // The synchronous caller owns the batch. DirectWrite layouts and renderer callbacks
    // are released for each paragraph before the font collections are released.
    internal sealed class ParagraphBatch : IDisposable
    {
        private readonly ParagraphBatch? _previous = _paragraphBatch;
        internal readonly Dictionary<nint, DirectWriteLongTextShaper.ExplicitFontScope> Fonts = [];

        internal ParagraphBatch() => _paragraphBatch = this;

        public void Dispose()
        {
            if (!ReferenceEquals(_paragraphBatch, this))
                throw new InvalidOperationException("DirectWrite paragraph batches must be disposed on the owning thread in order.");
            _paragraphBatch = _previous;
            lock (Sync)
                foreach (var font in Fonts.Values)
                    font.Dispose();
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ParagraphLineMetrics
    {
        internal uint Length, TrailingWhitespaceLength, NewlineLength;
        internal float Height, Baseline;
        internal int IsTrimmed;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ParagraphHitMetrics
    {
        internal uint Position, Length;
        internal float Left, Top, Width, Height;
        internal uint BidiLevel;
        internal int IsText, IsTrimmed;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ParagraphMetrics
    {
        internal float Left, Top, Width, WidthIncludingTrailingWhitespace, Height, LayoutWidth, LayoutHeight;
        internal uint MaxBidiDepth, LineCount;
    }
    private static void* ParagraphMethod(nint instance, int slot) => (*(void***)instance)[slot];

    internal static PlatformTextParagraphLayout CreateStyledParagraph(PlatformTextParagraphRequest request, bool vectorMetrics)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (Sync)
        {
            var text = new StringBuilder();
            var original = new List<int>(request.Text.Length + 1);
            var spans = new List<DirectWriteLongTextShaper.FontSpan>();
            var resources = new List<string>();
            var styles = request.Styles.ToArray();
            var objectPositions = request.Objects.IsEmpty ? null : request.Objects.ToArray().Select(item => item.Start).ToHashSet();
            for (var styleIndex = 0; styleIndex < styles.Length; styleIndex++)
            {
                var style = styles[styleIndex];
                var styleText = request.Text.Substring(style.Start, style.Length);
                List<int>? expandedSource = null;
                if (styleText.Contains('\t'))
                {
                    var expanded = new StringBuilder(style.Length);
                    expandedSource = new(style.Length);
                    for (var index = 0; index < styleText.Length; index++)
                    {
                        var c = styleText[index];
                        var count = c == '\t' ? 4 : 1;
                        expanded.Append(c == '\t' ? ' ' : c, count);
                        for (var repeat = 0; repeat < count; repeat++)
                            expandedSource.Add(style.Start + index);
                    }
                    styleText = expanded.ToString();
                }
                var prepared = DirectWriteHangulInput.Prepare(styleText);
                for (var index = 0; index < prepared.Text.Length; index++)
                {
                    var expandedPosition = prepared.OriginalPositions is { } positions ? (int)positions[index] : index;
                    original.Add(expandedSource is null ? style.Start + expandedPosition : expandedSource[expandedPosition]);
                }
                var start = text.Length;
                text.Append(prepared.Text);
                var size = vectorMetrics ? style.FontSize : Math.Clamp(MathF.Round(style.FontSize), 1, 512);
                char[]? fontSelection = null;
                if (objectPositions is not null)
                    for (var index = 0; index < prepared.Text.Length; index++)
                        if (objectPositions.Contains(original[start + index]))
                        {
                            fontSelection ??= prepared.Text.ToCharArray();
                            fontSelection[index] = ' ';
                        }
                // An externally painted object needs a declared face, not font coverage for its marker.
                foreach (var selected in WindowsPlatformTextBackend.BuildFontRuns(style.FontPath, style.SecondaryFontPath,
                    fontSelection is null ? prepared.Text : new string(fontSelection)))
                {
                    if (!TryGetFontFace(selected.FontPath, out var face))
                        throw new InvalidOperationException("Cannot load an explicitly selected paragraph font.");
                    spans.Add(new(start, selected.Text.Length, face, size, styleIndex));
                    resources.Add(selected.FontPath);
                    start += selected.Text.Length;
                }
            }
            if (spans.Count == 0)
            {
                var style = styles[0];
                if (!TryGetFontFace(style.FontPath, out var face))
                    throw new InvalidOperationException("Cannot load the empty paragraph's explicit font.");
                spans.Add(new(0, 0, face, vectorMetrics ? style.FontSize : Math.Clamp(MathF.Round(style.FontSize), 1, 512)));
                resources.Add(style.FontPath);
            }
            original.Add(request.Text.Length);
            var sourceMap = original.ToArray();
            int PreparedPosition(int source)
            {
                var low = 0;
                var high = sourceMap.Length - 1;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (sourceMap[middle] < source)
                        low = middle + 1;
                    else
                        high = middle;
                }
                return sourceMap[low] == source ? low : Math.Max(0, low - 1);
            }
            var caretPositions = new PlatformTextParagraphCaret[request.Text.Length + 1];
            var caretHits = new ParagraphHitMetrics[caretPositions.Length];
            var lines = new List<PlatformTextParagraphLine>();
            ParagraphMetrics metrics = default;
            var paragraph = DirectWriteLongTextShaper.ShapeParagraph(_factory, text.ToString(), spans.ToArray(),
                request.Direction == PlatformTextParagraphDirection.RightToLeft, layout =>
                {
                    for (var index = 0; index < caretPositions.Length; index++)
                    {
                        float x = 0, y = 0;
                        ParagraphHitMetrics hit = default;
                        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, ParagraphHitMetrics*, int>)ParagraphMethod(layout, 65))
                            (layout, (uint)PreparedPosition(index), 0, &x, &y, &hit));
                        caretPositions[index] = new(x, y);
                        caretHits[index] = hit;
                    }
                    uint count = 0;
                    var status = ((delegate* unmanaged[Stdcall]<nint, ParagraphLineMetrics*, uint, uint*, int>)ParagraphMethod(layout, 59))
                        (layout, null, 0, &count);
                    if (status != unchecked((int)0x8007007A))
                        Marshal.ThrowExceptionForHR(status);
                    var nativeLines = new ParagraphLineMetrics[count];
                    fixed (ParagraphLineMetrics* data = nativeLines)
                        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, ParagraphLineMetrics*, uint, uint*, int>)ParagraphMethod(layout, 59))
                            (layout, data, count, &count));
                    ParagraphHitMetrics[] Regions(int start, int length)
                    {
                        if (length == 0)
                            return [];
                        uint regionCount = 0;
                        var result = ((delegate* unmanaged[Stdcall]<nint, uint, uint, float, float, ParagraphHitMetrics*, uint, uint*, int>)ParagraphMethod(layout, 66))
                            (layout, (uint)start, (uint)length, 0, 0, null, 0, &regionCount);
                        if (result != unchecked((int)0x8007007A))
                            Marshal.ThrowExceptionForHR(result);
                        var regions = new ParagraphHitMetrics[regionCount];
                        fixed (ParagraphHitMetrics* data = regions)
                            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, uint, float, float, ParagraphHitMetrics*, uint, uint*, int>)ParagraphMethod(layout, 66))
                                (layout, (uint)start, (uint)length, 0, 0, data, regionCount, &regionCount));
                        return regions;
                    }
                    var preparedStart = 0;
                    var top = 0f;
                    foreach (var line in nativeLines)
                    {
                        var preparedEnd = checked(preparedStart + (int)line.Length);
                        var preparedStop = checked(preparedEnd - (int)line.NewlineLength);
                        var from = sourceMap[preparedStart];
                        var stop = sourceMap[preparedStop];
                        var end = sourceMap[preparedEnd];
                        var carets = new float[stop - from + 1];
                        for (var index = 0; index < carets.Length; index++)
                            carets[index] = caretPositions[from + index].X;
                        if (preparedStop > preparedStart)
                        {
                            float x = 0, y = 0;
                            ParagraphHitMetrics hit = default;
                            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, ParagraphHitMetrics*, int>)ParagraphMethod(layout, 65))
                                (layout, (uint)(preparedStop - 1), 1, &x, &y, &hit));
                            carets[^1] = x;
                        }
                        var characters = new List<PlatformTextCharacterGeometry>();
                        var boundaries = StringInfo.ParseCombiningCharacters(request.Text.Substring(from, stop - from));
                        for (var index = 0; index < boundaries.Length; index++)
                        {
                            var start = from + boundaries[index];
                            var finish = index + 1 < boundaries.Length ? from + boundaries[index + 1] : stop;
                            var nativeStart = PreparedPosition(start);
                            var nativeLength = PreparedPosition(finish) - nativeStart;
                            var hit = caretHits[start];
                            // A complete native cluster has the exact same rectangle in
                            // HitTestTextPosition and HitTestTextRange. Partial ligatures,
                            // expanded tabs and other split clusters still need range hits.
                            if (hit.IsText != 0 && hit.Position == (uint)nativeStart && hit.Length == (uint)nativeLength)
                                characters.Add(new(start - from, finish - start, hit.Left, hit.Width, hit.BidiLevel));
                            else
                                foreach (var region in Regions(nativeStart, nativeLength))
                                    characters.Add(new(start - from, finish - start, region.Left, region.Width, region.BidiLevel));
                        }
                        var extents = Regions(preparedStart, preparedStop - preparedStart);
                        var advance = extents.Length == 0 ? 0 : extents.Max(region => region.Left + region.Width) - extents.Min(region => region.Left);
                        var visibleStop = checked(preparedEnd - (int)line.TrailingWhitespaceLength);
                        var visibleWidth = advance;
                        if (visibleStop != preparedStop)
                        {
                            var visible = Regions(preparedStart, visibleStop - preparedStart);
                            visibleWidth = visible.Length == 0 ? 0 : visible.Max(region => region.Left + region.Width) - visible.Min(region => region.Left);
                        }
                        lines.Add(new(from, stop - from, end - stop, top, line.Height, line.Baseline, new(advance, carets, characters.ToArray()))
                        {
                            WidthWithoutTrailingWhitespace = visibleWidth
                        });
                        top += line.Height;
                        preparedStart = preparedEnd;
                    }
                    ParagraphMetrics measured = default;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, ParagraphMetrics*, int>)ParagraphMethod(layout, 60))(layout, &measured));
                    metrics = measured;
                }, float.IsFinite(request.Width) ? request.Width : null,
                request.Objects.ToArray().Select(item => new DirectWriteLongTextShaper.InlineSpan(
                    PreparedPosition(item.Start), item.Width, item.Height, item.Baseline)).ToArray(), _paragraphBatch?.Fonts);
            var runs = new List<PlatformTextParagraphRun>();
            foreach (var run in paragraph.Runs)
            {
                var raw = run.Raster;
                var glyphs = new PlatformTextParagraphGlyph[raw.Glyphs.Length];
                for (var index = 0; index < glyphs.Length; index++)
                {
                    var native = raw.Glyphs[index];
                    var placed = run.Glyphs[(raw.BidiLevel & 1) != 0 ? glyphs.Length - 1 - index : index];
                    glyphs[index] = new(native.Index, sourceMap[native.Cluster], native.Advance, native.OffsetX, -native.OffsetY, placed.X, placed.Y);
                }
                runs.Add(new(run.PaintStyle, resources[run.FontSpan], spans[run.FontSpan].Size, raw.BidiLevel,
                    new(raw.OriginX, raw.OriginY + paragraph.Baseline), glyphs));
            }
            var objects = paragraph.Objects.Select(item => new PlatformTextParagraphObjectPlacement(sourceMap[item.Start],
                new(item.X, item.Y, item.Width, item.Height), item.RightToLeft)).ToArray();
            return new(runs.ToArray(), lines.ToArray(), caretPositions, metrics.WidthIncludingTrailingWhitespace, metrics.Height, objects);
        }
    }
}
