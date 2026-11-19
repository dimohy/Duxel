namespace Duxel.Core;

/// <summary>A retained SVG scene. The resource owner outlives consumers; drawing never reparses XML.</summary>
public interface IUiSvgDocument
{
    UiVector2 IntrinsicSize { get; }
    long Revision { get; }
    event Action? Changed;
    UiSvgRaster Rasterize(int pixelWidth, int pixelHeight);
}

/// <summary>Retained device-independent commands for SVG widgets. Bitmap export remains an explicit separate operation.</summary>
public interface IUiGpuSvgDocument : IUiSvgDocument
{
    UiGpuVectorScene GpuScene { get; }
}

/// <summary>Immutable-by-contract straight-alpha RGBA pixels. The producer must not mutate the supplied memory.</summary>
public sealed class UiSvgRaster
{
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Pixels { get; }
    public UiSvgRaster(int width, int height, ReadOnlyMemory<byte> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != checked(width * height * 4)) throw new ArgumentException("RGBA size does not match dimensions.", nameof(pixels));
        (Width, Height, Pixels) = (width, height, pixels);
    }
    public UiImageData CopyImageData() => new(Width, Height, Pixels.ToArray());
    internal UiTextureUpdate CreateTextureUpdate(UiTextureId texture, int width, int height)
    {
        if (Width != width || Height != height)
            throw new InvalidOperationException("Raster document returned dimensions different from the requested viewport.");
        return new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm, width, height, Pixels) { IsColorImage = true };
    }
}
