using System.Buffers;

namespace Duxel.Core;

internal readonly record struct PlatformShapedGlyph(ushort Index, int X, int Y, float PhaseX, float PhaseY, float Advance);
internal sealed record PlatformShapedGlyphRun(int RasterSize, float Baseline, PlatformShapedGlyph[] Glyphs);
internal readonly record struct PlatformShapedGlyphMask(int X, int Y, int Width, int Height, byte[]? Pixels);

internal interface IPlatformShapedGlyphBackend
{
    PlatformShapedGlyphRun? ShapeGlyphs(string font, string? secondaryFont, string text, float fontSize);
    bool TryRasterizeGlyph(string font, int rasterSize, PlatformShapedGlyph glyph, out PlatformShapedGlyphMask mask);
}

public enum PlatformFontSimulation
{
    None, Oblique
}

/// <summary>Registers immutable font resources accepted by this backend's layout, raster, metrics and mesh methods.</summary>
public interface IPlatformFontVariantBackend
{
    /// <summary>Returns a stable backend-owned resource key; unsupported simulations fail explicitly.</summary>
    string GetFontVariantResource(string fontResource, PlatformFontSimulation simulation);
}

public readonly record struct PlatformFontMetrics(float Ascent, float Descent, float LineGap);
public interface IPlatformFontMetricsBackend
{
    bool TryGetFontMetrics(string fontPath, float fontSize, out PlatformFontMetrics metrics);
}

/// <summary>Exact raster bounds and advances without pixel storage. Advance is finite and nonnegative; zero is valid independently of ink bounds.</summary>
public readonly record struct PlatformTextLayoutResult(
    int Width, int Height, float Baseline, float FontAscent, float Advance)
{
    /// <summary>Horizontal pixel-canvas origin relative to the logical text origin.</summary>
    public float OriginX
    {
        get; init;
    }
}

/// <summary>Optional layout-only capability. Results must match TryRasterize for the same request.</summary>
public interface IPlatformTextLayoutBackend
{
    /// <summary>Changes when the same request can produce new metrics or pixels. Immutable backends use zero.</summary>
    ulong TextLayoutVersion
    {
        get;
    }
    bool TryLayout(in PlatformTextMeasureRequest request, out PlatformTextLayoutResult result);
}

public readonly record struct PlatformTextMeasureRequest(
    string FontPath,
    string? SecondaryFontPath,
    string Text,
    float FontSize
);

public readonly record struct PlatformTextMeasureResult(
    float Width,
    float Height,
    float Baseline
);

public readonly record struct PlatformTextRasterizeRequest(
    string FontPath,
    string? SecondaryFontPath,
    string Text,
    float FontSize
);

/// <summary>Raster coverage with a finite nonnegative logical Advance, including zero independently of ink bounds.</summary>
public readonly record struct PlatformTextRasterizeResult(
    int Width,
    int Height,
    ReadOnlyMemory<byte> RgbaPixels,
    float Baseline,
    float FontAscent,
    float Advance
)
{
    /// <summary>Optional exclusive pixel storage transferred to the caller. Dispose after use or transfer to a texture update.</summary>
    public IMemoryOwner<byte>? PixelOwner
    {
        get; init;
    }
    /// <summary>Horizontal pixel-canvas origin relative to the logical text origin.</summary>
    public float OriginX
    {
        get; init;
    }
}

public interface IPlatformTextBackend
{
    bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result);

    bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result);
}

public readonly record struct PlatformTextMeshRequest(
    string FontPath, string? SecondaryFontPath, string Text, float FontSize,
    float FlatteningTolerance = 0.001f);

// Triangle positions are relative to the top-left text origin, in font pixels.
public readonly record struct PlatformTextMeshResult(
    ReadOnlyMemory<UiVector2> TriangleVertices, float Width, float Height, float Baseline)
{
    public ReadOnlyMemory<PlatformTextGlyphMesh> Glyphs
    {
        get; init;
    }
}

// Glyph buffers are immutable and may be shared by several text layouts.
public readonly record struct PlatformTextGlyphMesh(
    ReadOnlyMemory<UiVector2> Vertices, ReadOnlyMemory<uint> Indices, UiVector2 Offset)
{
    /// <summary>Optional retained native curves, independent of the triangle flattening tolerance.</summary>
    public PlatformTextGlyphOutline Outline
    {
        get; init;
    }
}

public readonly record struct UiVectorCurve(UiVector2 Start, UiVector2 Control1, UiVector2 Control2, UiVector2 End, bool IsCubic = true)
{
    public static UiVectorCurve Line(UiVector2 start, UiVector2 end) => new(start, start, end, end, false);
}

/// <summary>Immutable-by-contract native contours; providers must close every contour.</summary>
public readonly record struct PlatformTextGlyphOutline(ReadOnlyMemory<UiVectorCurve> Curves, bool EvenOdd = false);

public interface IPlatformVectorTextBackend
{
    PlatformTextMeshResult CreateTextMesh(in PlatformTextMeshRequest request);
}
