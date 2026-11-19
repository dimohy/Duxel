using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Stable 8x8 ordered dithering in device pixels, applied before RGBA8 paint storage.</summary>
internal sealed record SvgOrderedDither(ISvgPaint Source, Vector2 RasterOrigin = default) : ISvgPaint
{
    public bool RequiresSourcePrecision => true;
    public SvgGpuPaintData CompileGpu()
    {
        var value = Source.CompileGpu();
        var parameters = value.Parameters;
        parameters.W += 4;
        return value with
        {
            Parameters = parameters
        };
    }
    internal static float Offset(int x, int y)
    {
        var rank = 0;
        for (var bit = 0; bit < 3; bit++)
        {
            var quadrant = ((x >> bit) & 1) + (((y >> bit) & 1) * 2);
            rank += quadrant << (2 * (2 - bit));
        }
        var normalized = SvgCompositing.Channel((rank + .5f) / 64) / 255f;
        return (normalized - .5f) / 255;
    }

    public Vector4 Sample(Vector2 point)
    {
        var color = SvgCompositing.Premultiply(Source.Sample(point));
        var pixel = point - RasterOrigin;
        var offset = Offset((int)MathF.Floor(pixel.X), (int)MathF.Floor(pixel.Y));
        color.X = Math.Clamp(color.X + offset, 0, color.W);
        color.Y = Math.Clamp(color.Y + offset, 0, color.W);
        color.Z = Math.Clamp(color.Z + offset, 0, color.W);
        return SvgCompositing.Unpremultiply(color);
    }

    public ISvgPaint Bind(Matrix3x2 transform) => new SvgOrderedDither(Source.Bind(transform), RasterOrigin);
    public ISvgPaint WithRasterOrigin(Vector2 origin) => this with { RasterOrigin = origin };
}
