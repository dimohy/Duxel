namespace Duxel.Core;

public readonly record struct GlyphRasterizationResult(
    int Width,
    int Height,
    byte[] Alpha,
    bool HasPlacementMetrics = false,
    float OffsetX = 0f,
    float OffsetY = 0f,
    float Advance = 0f,
    byte[]? Rgb = null
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
                new GlyphBitmap(result.Width, result.Height, result.Alpha, result.Rgb),
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
