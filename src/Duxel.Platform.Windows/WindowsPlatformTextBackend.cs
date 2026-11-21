using System.Text;
using System.Buffers;
using Duxel.Core;

namespace Duxel.Platform.Windows;

/// <summary>Font textures encode coverage in RGB. Grayscale samples replicate alpha into every coverage channel.</summary>
public sealed partial class WindowsPlatformTextBackend : IPlatformTextBackend, IPlatformVectorTextBackend, IPlatformFontMetricsBackend, IPlatformTextLayoutBackend, IPlatformFontVariantBackend, IPlatformTextGeometryBackend, IPlatformTextParagraphBackend
{
    public string GetFontVariantResource(string fontResource, PlatformFontSimulation simulation) =>
        WindowsDirectWriteGlyphRasterizer.GetFontVariantResource(fontResource, simulation);
    public bool TryGetFontMetrics(string fontPath, float fontSize, out PlatformFontMetrics metrics) =>
        WindowsDirectWriteGlyphRasterizer.TryGetFontMetrics(fontPath, fontSize, out metrics);
    public static readonly WindowsPlatformTextBackend Instance = new();

    // Below10 physical pixels, grid fitting can close subpixel counters such as the DND bar.
    // Rasterize at twice the resolution and use the existing downsampler; normal text is unchanged.
    private static int ResolveRasterFontSize(float fontSize) => Math.Clamp((int)MathF.Round(fontSize), 1, 512);

    private static int OversampleForSize(int size) => size < 10 ? 2 : 1;
    public ulong TextLayoutVersion => 0;

    public PlatformTextMeshResult CreateTextMesh(in PlatformTextMeshRequest request)
        => WindowsDirectWriteGlyphRasterizer.CreateTextMesh(request);

    public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result)
    {
        result = default;
        if (!TryLayout(request, out var layout))
            return false;
        result = new PlatformTextMeasureResult(layout.Width, layout.Height, layout.Baseline);
        return true;
    }

    public bool TryLayout(in PlatformTextMeasureRequest request, out PlatformTextLayoutResult result)
    {
        result = default;
        if (!TryRasterizeInternal(request.FontPath, request.SecondaryFontPath, request.Text, request.FontSize, out var line, rasterizePixels: false))
            return false;
        result = new(line.Width, line.Height, line.Baseline, line.FontAscent, line.Advance)
        {
            OriginX = line.OriginX
        };
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
            rasterized.Advance)
        {
            PixelOwner = rasterized.PixelOwner,
            OriginX = rasterized.OriginX
        };
        return true;
    }

    private static bool TryRasterizeInternal(string fontPath, string? secondaryFontPath, string text, float fontSize, out RasterizedLine line, bool rasterizePixels = true)
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

                if (!TryRasterizeInternal(fontPath, secondaryFontPath, segment, fontSize, out var segmentRaster, rasterizePixels))
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
            if (!rasterizePixels)
            {
                line = new(multilineWidth, multilineHeight, ReadOnlyMemory<byte>.Empty, lineAdvance,
                    lineRasters.Count > 0 ? lineRasters[0].FontAscent : 0f, multilineWidth);
                return true;
            }
            var pixelOwner = new PooledRasterPixels(checked(multilineWidth * multilineHeight * 4));
            var transferred = false;
            try
            {
                var multilinePixels = pixelOwner.Memory.Span;

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

                line = new RasterizedLine(multilineWidth, multilineHeight, pixelOwner.Memory, lineAdvance, lineRasters.Count > 0 ? lineRasters[0].FontAscent : 0f, multilineWidth) { PixelOwner = pixelOwner };
                transferred = true;
                return true;
            }
            finally
            {
                if (!transferred)
                    pixelOwner.Dispose();
            }
        }

        var size = ResolveRasterFontSize(fontSize);
        var runs = BuildFontRuns(fontPath, secondaryFontPath, text);
        if (runs.Count == 0)
        {
            return false;
        }

        return TryRasterizeContextualLine(fontPath, secondaryFontPath, runs, size, out line, rasterizePixels);
    }

    private static bool TryRasterizeContextualLine(string primary, string? secondary, List<FontRun> runs, int size, out RasterizedLine line, bool rasterizePixels)
    {
        line = default;
        var runResults = new List<(WindowsDirectWriteGlyphRasterizer.TextRunRasterizationResult Result, float PenX)>(runs.Count);
        var text = string.Concat(runs.Select(run => run.Text));
        var oversample = OversampleForSize(size);
        var paragraph = WindowsDirectWriteGlyphRasterizer.ShapeContextualParagraph(primary, secondary, text, size * oversample);
        var penX = paragraph.Advance / oversample;

        foreach (var run in paragraph.Runs)
        {
            if (!WindowsDirectWriteGlyphRasterizer.Instance.TryRasterizeTextRun(
                    run.FontPath, text, size, oversample, sourceScale: 1f, renderScale: 1f, out var result, rasterizePixels,
                    run.Raster with
                    {
                        OriginY = run.Raster.OriginY + paragraph.Baseline
                    }))
            {
                return false;
            }

            if (result.Width > 0 && result.Height > 0)
            {
                runResults.Add((result, 0));
            }
        }

        if (runResults.Count == 0)
        {
            if (!WindowsDirectWriteGlyphRasterizer.TryGetFontMetrics(runs[0].FontPath, size, out var metrics))
                return false;
            line = new(0, 0, ReadOnlyMemory<byte>.Empty, 0, metrics.Ascent, penX);
            return true;
        }

        var minX = 0f;
        var maxX = penX;
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;

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
        var baseline = paragraph.Baseline / oversample - minY;
        var fontAscent = runResults.Count > 0 ? runResults[0].Result.Baseline : baseline;
        if (!rasterizePixels)
        {
            line = new(totalWidth, totalHeight, ReadOnlyMemory<byte>.Empty, baseline, fontAscent, penX)
            {
                OriginX = minX
            };
            return true;
        }
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
                        pixels[idx] = a;
                        pixels[idx + 1] = a;
                        pixels[idx + 2] = a;
                        pixels[idx + 3] = a;
                    }
                }
            }
        }

        line = new RasterizedLine(totalWidth, totalHeight, pixels, baseline, fontAscent, penX) { OriginX = minX };
        return true;
    }

    internal static List<FontRun> BuildFontRuns(string primaryFontPath, string? secondaryFontPath, string text)
    {
        var runs = new List<FontRun>();
        var builder = new StringBuilder(text.Length);
        Span<char> utf16 = stackalloc char[2];
        string? currentFontPath = null;

        foreach (var rune in text.EnumerateRunes())
        {
            // Unicode17 PropList Variation_Selector values remain in the preceding font span for native shaping.
            if (rune.Value < 0x20 || Rune.IsWhiteSpace(rune) || Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Format
                || rune.Value is >= 0x180B and <= 0x180D or 0x180F or >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF)
            {
                if (currentFontPath is null)
                {
                    currentFontPath = primaryFontPath;
                }

                builder.Append(utf16[..rune.EncodeToUtf16(utf16)]);
                continue;
            }

            if (!TryResolveFontPathForRune(primaryFontPath, secondaryFontPath, rune.Value, out var selectedFontPath))
                throw new NotSupportedException($"No selected font contains U+{rune.Value:X4}.");

            if (!string.Equals(currentFontPath, selectedFontPath, StringComparison.Ordinal))
            {
                if (builder.Length > 0 && !string.IsNullOrEmpty(currentFontPath))
                {
                    runs.Add(new FontRun(currentFontPath, builder.ToString()));
                    builder.Clear();
                }

                currentFontPath = selectedFontPath;
            }

            builder.Append(utf16[..rune.EncodeToUtf16(utf16)]);
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

    internal readonly record struct FontRun(string FontPath, string Text);
    private readonly record struct RasterizedLine(int Width, int Height, ReadOnlyMemory<byte> Pixels, float Baseline, float FontAscent, float Advance)
    {
        internal float OriginX
        {
            get; init;
        }
        internal IMemoryOwner<byte>? PixelOwner
        {
            get; init;
        }
    }

    private sealed class PooledRasterPixels : IMemoryOwner<byte>
    {
        private byte[]? _buffer;
        private readonly int _length;
        internal PooledRasterPixels(int length)
        {
            _length = length;
            _buffer = ArrayPool<byte>.Shared.Rent(length);
            _buffer.AsSpan(0, length).Clear();
        }
        public Memory<byte> Memory
        {
            get
            {
                ObjectDisposedException.ThrowIf(_buffer is null, this);
                return _buffer.AsMemory(0, _length);
            }
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _buffer, null) is { } buffer)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
