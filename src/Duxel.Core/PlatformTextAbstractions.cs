namespace Duxel.Core;

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

public readonly record struct PlatformTextRasterizeResult(
    int Width,
    int Height,
    ReadOnlyMemory<byte> RgbaPixels,
    float Baseline
);

public interface IPlatformTextBackend
{
    bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result);

    bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result);
}
