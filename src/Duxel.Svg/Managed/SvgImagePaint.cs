using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed class SvgImagePaint(SvgDecodedImage image, Vector4 viewport, bool stretch) : ISvgPaint
{
    public SvgGpuPaintData CompileGpu() => new(4, viewport, new(image.Width, image.Height, 0, stretch ? 1 : 0),
        Matrix3x2.Identity, [], Image: image);
    public Vector4 Sample(Vector2 point)
    {
        var width = viewport.Z;
        var height = viewport.W;
        if (!stretch)
        {
            var scale = MathF.Min(width / image.Width, height / image.Height);
            width = image.Width * scale;
            height = image.Height * scale;
        }
        var x = (point.X - viewport.X - (viewport.Z - width) / 2) / width * image.Width;
        var y = (point.Y - viewport.Y - (viewport.W - height) / 2) / height * image.Height;
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) return Vector4.Zero;
        return SvgRasterSampling.Bilinear(image.Rgba, image.Width, image.Height, x - .5f, y - .5f, repeat: false);
    }
}
