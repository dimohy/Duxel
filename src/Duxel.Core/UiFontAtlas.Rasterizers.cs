namespace Duxel.Core;

public readonly record struct GlyphRasterizationResult(
    int Width,
    int Height,
    byte[] Alpha,
    bool HasPlacementMetrics = false,
    float OffsetX = 0f,
    float OffsetY = 0f,
    float Advance = 0f
);

internal readonly record struct RasterizedGlyph(
    GlyphBitmap Bitmap,
    bool HasPlacementMetrics,
    float OffsetX,
    float OffsetY,
    float Advance
);

internal readonly record struct UiDirectGlyphRasterization(
    int GlyphIndex,
    int Width,
    int Height,
    byte[] Alpha,
    float Advance,
    float OffsetX,
    float OffsetY
);

public interface IPlatformGlyphBitmapRasterizer
{
    string CacheKeyTag { get; }

    bool TryRasterize(
        string fontPath,
        int codepoint,
        int glyphIndex,
        int fontSize,
        int oversample,
        float sourceScale,
        float renderScale,
        out GlyphRasterizationResult result
    );
}

internal readonly record struct GlyphRasterizationInput(
    string FontPath,
    int Codepoint,
    int GlyphIndex,
    int FontSize,
    int Oversample,
    float SourceScale,
    TtfGlyph Glyph,
    float RenderScale
);

internal interface IGlyphBitmapRasterizer
{
    string CacheKeyTag { get; }

    bool TryRasterize(in GlyphRasterizationInput input, out RasterizedGlyph glyph);
}

internal sealed class TtfGlyphBitmapRasterizer : IGlyphBitmapRasterizer
{
    public static readonly TtfGlyphBitmapRasterizer Instance = new();

    public string CacheKeyTag => "ttf";

    public bool TryRasterize(in GlyphRasterizationInput input, out RasterizedGlyph glyph)
    {
        var bitmap = input.Glyph.Rasterize(input.RenderScale);
        if (input.Oversample > 1)
        {
            bitmap = Downsample(bitmap, input.Oversample);
        }

        glyph = new RasterizedGlyph(bitmap, false, 0f, 0f, 0f);

        return true;
    }

    private static GlyphBitmap Downsample(GlyphBitmap source, int oversample)
    {
        var targetWidth = Math.Max(1, (source.Width + oversample - 1) / oversample);
        var targetHeight = Math.Max(1, (source.Height + oversample - 1) / oversample);
        var output = new byte[targetWidth * targetHeight];

        for (var y = 0; y < targetHeight; y++)
        {
            for (var x = 0; x < targetWidth; x++)
            {
                var sum = 0;
                var count = 0;
                var startX = x * oversample;
                var startY = y * oversample;

                for (var oy = 0; oy < oversample; oy++)
                {
                    var sy = startY + oy;
                    if (sy >= source.Height)
                    {
                        break;
                    }

                    for (var ox = 0; ox < oversample; ox++)
                    {
                        var sx = startX + ox;
                        if (sx >= source.Width)
                        {
                            break;
                        }

                        sum += source.Alpha[(sy * source.Width) + sx];
                        count++;
                    }
                }

                output[(y * targetWidth) + x] = count == 0 ? (byte)0 : (byte)(sum / count);
            }
        }

        return new GlyphBitmap(targetWidth, targetHeight, output);
    }
}

internal sealed class CompositeGlyphBitmapRasterizer : IGlyphBitmapRasterizer
{
    private readonly IReadOnlyList<IGlyphBitmapRasterizer> _chain;

    public CompositeGlyphBitmapRasterizer(params IGlyphBitmapRasterizer[] chain)
    {
        _chain = chain ?? throw new ArgumentNullException(nameof(chain));
        if (_chain.Count == 0)
        {
            throw new ArgumentException("At least one rasterizer is required.", nameof(chain));
        }
    }

    public string CacheKeyTag
    {
        get
        {
            var tags = new string[_chain.Count];
            for (var i = 0; i < _chain.Count; i++)
            {
                tags[i] = _chain[i].CacheKeyTag;
            }

            return string.Join("+", tags);
        }
    }

    public bool TryRasterize(in GlyphRasterizationInput input, out RasterizedGlyph glyph)
    {
        for (var i = 0; i < _chain.Count; i++)
        {
            if (_chain[i].TryRasterize(input, out glyph))
            {
                return true;
            }
        }

        glyph = default;
        return false;
    }
}

internal sealed class PlatformGlyphBitmapRasterizerAdapter : IGlyphBitmapRasterizer
{
    private readonly IPlatformGlyphBitmapRasterizer _platformRasterizer;

    public PlatformGlyphBitmapRasterizerAdapter(IPlatformGlyphBitmapRasterizer platformRasterizer)
    {
        _platformRasterizer = platformRasterizer ?? throw new ArgumentNullException(nameof(platformRasterizer));
    }

    public string CacheKeyTag => _platformRasterizer.CacheKeyTag;

    public bool TryRasterize(in GlyphRasterizationInput input, out RasterizedGlyph glyph)
    {
        if (_platformRasterizer.TryRasterize(
                input.FontPath,
                input.Codepoint,
            input.GlyphIndex,
                input.FontSize,
                input.Oversample,
                input.SourceScale,
                input.RenderScale,
                out var result))
        {
            glyph = new RasterizedGlyph(
                new GlyphBitmap(result.Width, result.Height, result.Alpha),
                result.HasPlacementMetrics,
                result.OffsetX,
                result.OffsetY,
                result.Advance
            );
            return true;
        }

        glyph = default;
        return false;
    }
}
