using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Resolved RGBA8 pixels with sparse premultiplied sample colors and straight-alpha export.</summary>
internal sealed partial class SvgRasterSurface
{
    private readonly Vector4[] _pixels;
    private readonly Vector4? _clip;
    internal Vector2 RasterOrigin
    {
        get;
    }
    internal int Width
    {
        get;
    }
    internal int Height
    {
        get;
    }
    internal SvgRasterSurface(int width, int height, Vector4 background = default, Vector4? clip = null, Vector2 rasterOrigin = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if ((long)width * height > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        _clip = clip;
        RasterOrigin = rasterOrigin;
        _pixels = new Vector4[checked(width * height)];
        if (background != default)
        {
            if (!float.IsFinite(background.X) || !float.IsFinite(background.Y) || !float.IsFinite(background.Z) || !float.IsFinite(background.W) ||
                Vector4.Min(background, Vector4.Zero) != Vector4.Zero || Vector4.Max(background, Vector4.One) != Vector4.One)
                throw new ArgumentOutOfRangeException(nameof(background));
            Array.Fill(_pixels, SvgCompositing.Premultiply(background));
        }
    }

    internal void Fill(SvgGeometry geometry, Matrix3x2 transform, Vector4 color, bool evenOdd = false)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) || !float.IsFinite(color.W) ||
            Vector4.Min(color, Vector4.Zero) != Vector4.Zero || Vector4.Max(color, Vector4.One) != Vector4.One)
            throw new ArgumentOutOfRangeException(nameof(color));
        FillPaint(geometry, transform, new SvgSolidPaint(color), evenOdd);
    }

    internal void FillPaint(SvgGeometry geometry, Matrix3x2 transform, ISvgPaint paint, bool evenOdd = false)
    {
        paint = paint.WithRasterOrigin(RasterOrigin);
        var requiresSourcePrecision = paint.RequiresSourcePrecision;
        SvgCoverageRasterizer.Rasterize(geometry, transform, evenOdd, Width, Height, (row, coverage, masks, paintPoints) =>
        {
            for (var column = 0; column < Width; column++)
            {
                if (coverage[column] == 0)
                    continue;
                var color = paint.Sample(paintPoints.IsEmpty ? new(column + .5f, row + .5f) : paintPoints[column]);
                var alpha = Math.Clamp(coverage[column], 0, 1) * color.W;
                if (alpha == 0)
                    continue;
                var index = row * Width + column;
                color.W = alpha;
                var source = SvgCompositing.Premultiply(color);
                if (requiresSourcePrecision)
                    source = SvgShaderColor.Quantize(source);
                Blend(index, source, masks.IsEmpty ? (byte)15 : masks[column]);
            }
        }, _clip);
    }

    internal byte[] CopyRgba()
    {
        var bytes = new byte[checked(_pixels.Length * 4)];
        for (var i = 0; i < _pixels.Length; i++)
        {
            var pixel = _pixels[i];
            if (pixel.W == 0)
                continue;
            var alpha = SvgCompositing.Channel(pixel.W);
            if (alpha == 0)
                continue;
            byte Straight(float value) => Duxel.Core.UiRgba8.Unpremultiply(SvgCompositing.Channel(value), alpha);
            bytes[i * 4] = Straight(pixel.X);
            bytes[i * 4 + 1] = Straight(pixel.Y);
            bytes[i * 4 + 2] = Straight(pixel.Z);
            bytes[i * 4 + 3] = alpha;
        }
        return bytes;
    }
    internal void Composite(SvgRasterSurface source, float opacity)
    {
        if (source.Width != Width || source.Height != Height)
            throw new ArgumentException("SVG layer sizes must agree.");
        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity));
        for (var i = 0; i < _pixels.Length; i++)
        {
            CompositePixel(source, i, i, opacity);
        }
    }
    internal void Mask(SvgRasterSurface source, bool luminance, bool preserveSamples = false)
    {
        if (source.Width != Width || source.Height != Height)
            throw new ArgumentException("SVG mask sizes must agree.");
        // Mask images consume resolved colors; geometric clips retain sample identity.
        if (!preserveSamples)
            _samplePixels?.Clear();
        for (var i = 0; i < _pixels.Length; i++)
        {
            if (preserveSamples && ((_samplePixels is not null && _samplePixels.ContainsKey(i)) ||
                (source._samplePixels is not null && source._samplePixels.ContainsKey(i))))
            {
                StoreSamples(i, Samples(i).Mask(source.Samples(i), luminance));
                continue;
            }
            var mask = source._pixels[i];
            _pixels[i] = SvgCompositing.Quantize(_pixels[i] * (luminance ? .2126f * mask.X + .7152f * mask.Y + .0722f * mask.Z : mask.W));
        }
    }
}
