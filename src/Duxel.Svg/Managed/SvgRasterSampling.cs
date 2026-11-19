using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Shared texel-center and premultiplied interpolation rules for embedded images and repeating paint tiles.</summary>
internal static class SvgRasterSampling
{
    internal static Vector4 Bilinear(byte[] rgba, int width, int height, float x, float y, bool repeat)
    {
        var left = (int)MathF.Floor(x);
        var top = (int)MathF.Floor(y);
        int Coordinate(int value, int extent) => repeat ? (value % extent + extent) % extent : Math.Clamp(value, 0, extent - 1);
        Vector4 Pixel(int column, int row)
        {
            var offset = (Coordinate(row, height) * width + Coordinate(column, width)) * 4;
            return SvgCompositing.Premultiply(new Vector4(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]) / 255);
        }
        var color = Vector4.Lerp(Vector4.Lerp(Pixel(left, top), Pixel(left + 1, top), x - left),
            Vector4.Lerp(Pixel(left, top + 1), Pixel(left + 1, top + 1), x - left), y - top);
        return SvgCompositing.Unpremultiply(color);
    }
}
