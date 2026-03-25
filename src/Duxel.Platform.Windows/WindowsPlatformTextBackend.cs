using System.Text;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed class WindowsPlatformTextBackend : IPlatformTextBackend
{
    public static readonly WindowsPlatformTextBackend Instance = new();

    private const int Oversample = 1;

    public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result)
    {
        result = default;
        if (!TryRasterizeInternal(request.FontPath, request.SecondaryFontPath, request.Text, request.FontSize, out var rasterized))
        {
            return false;
        }

        result = new PlatformTextMeasureResult(rasterized.Width, rasterized.Height, rasterized.Baseline);
        return true;
    }

    public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result)
    {
        result = default;
        if (!TryRasterizeInternal(request.FontPath, request.SecondaryFontPath, request.Text, request.FontSize, out var rasterized))
        {
            return false;
        }

        result = new PlatformTextRasterizeResult(
            rasterized.Width,
            rasterized.Height,
            rasterized.Pixels,
            rasterized.Baseline,
            rasterized.FontAscent,
            rasterized.Advance);
        return true;
    }

    private static bool TryRasterizeInternal(string fontPath, string? secondaryFontPath, string text, float fontSize, out RasterizedLine line)
    {
        line = default;
        if (string.IsNullOrWhiteSpace(fontPath) || string.IsNullOrEmpty(text) || fontSize <= 0f)
        {
            return false;
        }

        text = text.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\t", "    ", StringComparison.Ordinal);

        if (text.IndexOf('\n') >= 0)
        {
            var lines = text.Split('\n');
            var lineRasters = new List<RasterizedLine>(lines.Length);
            var maxWidth = 0;
            var lineAdvance = Math.Max(1, (int)MathF.Ceiling(fontSize));

            for (var i = 0; i < lines.Length; i++)
            {
                var segment = lines[i];
                if (segment.Length == 0)
                {
                    lineRasters.Add(new RasterizedLine(0, 0, ReadOnlyMemory<byte>.Empty, lineAdvance, 0f, 0f));
                    continue;
                }

                if (!TryRasterizeInternal(fontPath, secondaryFontPath, segment, fontSize, out var segmentRaster))
                {
                    return false;
                }

                lineRasters.Add(segmentRaster);
                if (segmentRaster.Width > maxWidth)
                {
                    maxWidth = segmentRaster.Width;
                }
            }

            var multilineWidth = Math.Max(1, maxWidth);
            var multilineHeight = Math.Max(1, lineAdvance * lines.Length);
            var multilinePixels = new byte[multilineWidth * multilineHeight * 4];

            for (var lineIndex = 0; lineIndex < lineRasters.Count; lineIndex++)
            {
                var segmentRaster = lineRasters[lineIndex];
                if (segmentRaster.Width <= 0 || segmentRaster.Height <= 0)
                {
                    continue;
                }

                var yBase = lineIndex * lineAdvance;
                var segmentPixels = segmentRaster.Pixels.Span;
                for (var y = 0; y < segmentRaster.Height; y++)
                {
                    var destY = yBase + y;
                    if ((uint)destY >= (uint)multilineHeight)
                    {
                        break;
                    }

                    for (var x = 0; x < segmentRaster.Width; x++)
                    {
                        if ((uint)x >= (uint)multilineWidth)
                        {
                            break;
                        }

                        var srcIndex = (y * segmentRaster.Width + x) * 4;
                        var alpha = segmentPixels[srcIndex + 3];
                        if (alpha == 0)
                        {
                            continue;
                        }

                        var dstIndex = (destY * multilineWidth + x) * 4;
                        multilinePixels[dstIndex + 0] = segmentPixels[srcIndex + 0];
                        multilinePixels[dstIndex + 1] = segmentPixels[srcIndex + 1];
                        multilinePixels[dstIndex + 2] = segmentPixels[srcIndex + 2];
                        multilinePixels[dstIndex + 3] = alpha;
                    }
                }
            }

            line = new RasterizedLine(multilineWidth, multilineHeight, multilinePixels, lineAdvance, lineRasters.Count > 0 ? lineRasters[0].FontAscent : 0f, multilineWidth);
            return true;
        }

        var size = Math.Clamp((int)MathF.Round(fontSize), 1, 512);
        var runs = BuildFontRuns(fontPath, secondaryFontPath, text);
        if (runs.Count == 0)
        {
            return false;
        }

        if (runs.Count == 1)
        {
            return TryRasterizeSingleRun(runs[0].FontPath, runs[0].Text, size, out line);
        }

        return TryRasterizeMultiRun(runs, size, out line);
    }

    private static bool TryRasterizeSingleRun(string fontPath, string text, int size, out RasterizedLine line)
    {
        line = default;
        if (!WindowsDirectWriteGlyphRasterizer.Instance.TryRasterizeTextRun(
                fontPath, text, size, Oversample, sourceScale: 1f, renderScale: 1f, out var result))
        {
            return false;
        }

        if (result.Width <= 0 || result.Height <= 0)
        {
            // Whitespace or invisible glyph — return advance with empty bitmap
            line = new RasterizedLine(0, 0, ReadOnlyMemory<byte>.Empty, 0f, result.Baseline, result.Advance);
            return true;
        }

        var ox = Math.Max(0, result.OffsetX);
        var totalWidth = ox + result.Width;
        var pixels = new byte[totalWidth * result.Height * 4];
        var alpha = result.Alpha;
        var rgb = result.Rgb;
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                var srcIdx = y * result.Width + x;
                if (alpha[srcIdx] == 0)
                {
                    continue;
                }

                var dstIdx = (y * totalWidth + ox + x) * 4;
                if (rgb is not null)
                {
                    var rgbIdx = srcIdx * 3;
                    pixels[dstIdx] = rgb[rgbIdx];
                    pixels[dstIdx + 1] = rgb[rgbIdx + 1];
                    pixels[dstIdx + 2] = rgb[rgbIdx + 2];
                    pixels[dstIdx + 3] = alpha[srcIdx];
                }
                else
                {
                    pixels[dstIdx] = 255;
                    pixels[dstIdx + 1] = 255;
                    pixels[dstIdx + 2] = 255;
                    pixels[dstIdx + 3] = alpha[srcIdx];
                }
            }
        }

        var baseline = MathF.Max(0f, -result.OffsetY);
        line = new RasterizedLine(totalWidth, result.Height, pixels, baseline, result.Baseline, result.Advance);
        return true;
    }

    private static bool TryRasterizeMultiRun(List<FontRun> runs, int size, out RasterizedLine line)
    {
        line = default;
        var runResults = new List<(WindowsDirectWriteGlyphRasterizer.TextRunRasterizationResult Result, float PenX)>(runs.Count);
        var penX = 0f;

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!WindowsDirectWriteGlyphRasterizer.Instance.TryRasterizeTextRun(
                    run.FontPath, run.Text, size, Oversample, sourceScale: 1f, renderScale: 1f, out var result))
            {
                return false;
            }

            if (result.Width > 0 && result.Height > 0)
            {
                runResults.Add((result, penX));
            }

            penX += result.Advance;
        }

        if (runResults.Count == 0)
        {
            return false;
        }

        var minX = 0f;
        var maxX = penX;
        var minY = 0f;
        var maxY = 0f;

        for (var i = 0; i < runResults.Count; i++)
        {
            var (r, px) = runResults[i];
            var left = px + r.OffsetX;
            var right = left + r.Width;
            var top = (float)r.OffsetY;
            var bottom = top + r.Height;

            if (left < minX)
            {
                minX = left;
            }

            if (right > maxX)
            {
                maxX = right;
            }

            if (top < minY)
            {
                minY = top;
            }

            if (bottom > maxY)
            {
                maxY = bottom;
            }
        }

        var totalWidth = Math.Max(1, (int)MathF.Ceiling(maxX - minX));
        var totalHeight = Math.Max(1, (int)MathF.Ceiling(maxY - minY));
        var baseline = MathF.Max(0f, -minY);
        var fontAscent = runResults.Count > 0 ? runResults[0].Result.Baseline : baseline;
        var pixels = new byte[totalWidth * totalHeight * 4];

        for (var i = 0; i < runResults.Count; i++)
        {
            var (r, px) = runResults[i];
            var baseX = (int)MathF.Floor(px + r.OffsetX - minX);
            var baseY = (int)MathF.Floor(r.OffsetY - minY);
            var rgb = r.Rgb;

            for (var y = 0; y < r.Height; y++)
            {
                for (var x = 0; x < r.Width; x++)
                {
                    var srcIdx = y * r.Width + x;
                    var a = r.Alpha[srcIdx];
                    if (a == 0)
                    {
                        continue;
                    }

                    var dstX = baseX + x;
                    var dstY = baseY + y;
                    if ((uint)dstX >= (uint)totalWidth || (uint)dstY >= (uint)totalHeight)
                    {
                        continue;
                    }

                    var idx = (dstY * totalWidth + dstX) * 4;
                    if (rgb is not null)
                    {
                        var rgbIdx = srcIdx * 3;
                        pixels[idx] = rgb[rgbIdx];
                        pixels[idx + 1] = rgb[rgbIdx + 1];
                        pixels[idx + 2] = rgb[rgbIdx + 2];
                        pixels[idx + 3] = a;
                    }
                    else
                    {
                        pixels[idx] = 255;
                        pixels[idx + 1] = 255;
                        pixels[idx + 2] = 255;
                        pixels[idx + 3] = a;
                    }
                }
            }
        }

        line = new RasterizedLine(totalWidth, totalHeight, pixels, baseline, fontAscent, penX);
        return true;
    }

    private static List<FontRun> BuildFontRuns(string primaryFontPath, string? secondaryFontPath, string text)
    {
        var runs = new List<FontRun>();
        var builder = new StringBuilder(text.Length);
        string? currentFontPath = null;

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value < 0x20 && !Rune.IsWhiteSpace(rune))
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune))
            {
                if (currentFontPath is null)
                {
                    currentFontPath = primaryFontPath;
                }

                builder.Append(rune.ToString());
                continue;
            }

            if (!TryResolveFontPathForRune(primaryFontPath, secondaryFontPath, rune.Value, out var selectedFontPath))
            {
                const int replacementCodepoint = 0x25A1;
                if (!TryResolveFontPathForRune(primaryFontPath, secondaryFontPath, replacementCodepoint, out selectedFontPath))
                {
                    continue;
                }

                if (!string.Equals(currentFontPath, selectedFontPath, StringComparison.Ordinal))
                {
                    if (builder.Length > 0 && !string.IsNullOrEmpty(currentFontPath))
                    {
                        runs.Add(new FontRun(currentFontPath, builder.ToString()));
                        builder.Clear();
                    }

                    currentFontPath = selectedFontPath;
                }

                builder.Append('□');
                continue;
            }

            if (!string.Equals(currentFontPath, selectedFontPath, StringComparison.Ordinal))
            {
                if (builder.Length > 0 && !string.IsNullOrEmpty(currentFontPath))
                {
                    runs.Add(new FontRun(currentFontPath, builder.ToString()));
                    builder.Clear();
                }

                currentFontPath = selectedFontPath;
            }

            builder.Append(rune.ToString());
        }

        if (builder.Length > 0 && !string.IsNullOrEmpty(currentFontPath))
        {
            runs.Add(new FontRun(currentFontPath, builder.ToString()));
        }

        return runs;
    }

    private static bool TryResolveFontPathForRune(string primaryFontPath, string? secondaryFontPath, int codepoint, out string selectedFontPath)
    {
        if (WindowsDirectWriteGlyphRasterizer.Instance.HasGlyph(primaryFontPath, codepoint))
        {
            selectedFontPath = primaryFontPath;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(secondaryFontPath)
            && WindowsDirectWriteGlyphRasterizer.Instance.HasGlyph(secondaryFontPath, codepoint))
        {
            selectedFontPath = secondaryFontPath;
            return true;
        }

        selectedFontPath = string.Empty;
        return false;
    }

    private readonly record struct FontRun(string FontPath, string Text);
    private readonly record struct RasterizedLine(int Width, int Height, ReadOnlyMemory<byte> Pixels, float Baseline, float FontAscent, float Advance);
}
