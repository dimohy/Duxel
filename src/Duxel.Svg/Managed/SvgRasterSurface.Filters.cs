using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgRasterSurface
{
    internal void GaussianBlur(Vector2 horizontal, Vector2 vertical, bool linearRgb)
    {
        // Spatial filtering consumes the resolved input rather than four independent sample planes.
        _samplePixels?.Clear();
        if (linearRgb)
            ConvertColor(toLinear: true);
        Convolve(horizontal);
        Convolve(vertical);
        if (linearRgb)
            ConvertColor(toLinear: false);
        for (var i = 0; i < _pixels.Length; i++)
            _pixels[i] = SvgCompositing.Quantize(_pixels[i]);
    }

    private void Convolve(Vector2 deviation)
    {
        var sigma = deviation.Length();
        if (sigma == 0)
            return;
        var radius = MathF.Ceiling(3 * sigma);
        var axisAligned = deviation.X == 0 || deviation.Y == 0;
        if (!float.IsFinite(radius) || radius > 2048 || !axisAligned && (2 * radius + 1) * _pixels.Length > 250_000_000d)
            throw new InvalidDataException("SVG blur exceeds the retained raster work limit.");
        var direction = deviation / sigma;
        var count = (int)radius;
        var weights = new float[count * 2 + 1];
        double sum = 0;
        for (var i = -count; i <= count; i++)
        {
            weights[i + count] = (float)Math.Exp(-.5 * i * i / ((double)sigma * sigma));
            sum += weights[i + count];
        }
        for (var i = 0; i < weights.Length; i++)
            weights[i] /= (float)sum;
        var destination = new Vector4[_pixels.Length];
        if (axisAligned)
        {
            ConvolveAxis(destination, weights, count, direction);
            destination.CopyTo(_pixels, 0);
            return;
        }
        Vector4 Pixel(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height ? _pixels[y * Width + x] : Vector4.Zero;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var value = Vector4.Zero;
                for (var offset = -count; offset <= count; offset++)
                {
                    var px = x + direction.X * offset;
                    var py = y + direction.Y * offset;
                    var left = (int)MathF.Floor(px);
                    var top = (int)MathF.Floor(py);
                    var sample = Vector4.Lerp(Vector4.Lerp(Pixel(left, top), Pixel(left + 1, top), px - left),
                        Vector4.Lerp(Pixel(left, top + 1), Pixel(left + 1, top + 1), px - left), py - top);
                    value += sample * weights[offset + count];
                }
                destination[y * Width + x] = value;
            }
        destination.CopyTo(_pixels, 0);
    }

    private void ConvolveAxis(Vector4[] destination, float[] weights, int radius, Vector2 direction)
    {
        // Each channel keeps the original coefficient accumulation order. Axis-aligned
        // samples are already on the pixel grid and need no bilinear interpolation.
        var horizontal = direction.Y == 0;
        var sign = horizontal ? Math.Sign(direction.X) : Math.Sign(direction.Y);
        var source = System.Runtime.InteropServices.MemoryMarshal.Cast<Vector4, float>(_pixels.AsSpan());
        var target = System.Runtime.InteropServices.MemoryMarshal.Cast<Vector4, float>(destination.AsSpan());
        for (var offset = -radius; offset <= radius; offset++)
        {
            var delta = offset * sign;
            var firstY = horizontal ? 0 : Math.Max(0, -delta);
            var lastY = horizontal ? Height : Math.Min(Height, Height - delta);
            var firstX = horizontal ? Math.Max(0, -delta) : 0;
            var lastX = horizontal ? Math.Min(Width, Width - delta) : Width;
            if (lastX <= firstX)
                continue;
            var weight = weights[offset + radius];
            var factor = new Vector<float>(weight);
            for (var y = firstY; y < lastY; y++)
            {
                var start = (y * Width + firstX) * 4;
                var sourceStart = horizontal ? start + delta * 4 : start + delta * Width * 4;
                var length = (lastX - firstX) * 4;
                var input = source.Slice(sourceStart, length);
                var output = target.Slice(start, length);
                var channel = 0;
                for (; channel <= length - Vector<float>.Count; channel += Vector<float>.Count)
                {
                    var value = new Vector<float>(output.Slice(channel));
                    (value + new Vector<float>(input.Slice(channel)) * factor).CopyTo(output.Slice(channel));
                }
                for (; channel < length; channel++)
                    output[channel] += input[channel] * weight;
            }
        }
    }

    private void ConvertColor(bool toLinear)
    {
        float Channel(float value) => toLinear
            ? value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f)
            : value <= .0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1 / 2.4f) - .055f;
        for (var i = 0; i < _pixels.Length; i++)
        {
            var color = _pixels[i];
            if (color.W != 0)
                _pixels[i] = new(Channel(color.X / color.W) * color.W,
                Channel(color.Y / color.W) * color.W, Channel(color.Z / color.W) * color.W, color.W);
        }
    }

    internal void CompositeAt(SvgRasterSurface source, float opacity, int left, int top, Vector4? geometricClip = null)
    {
        var firstX = Math.Max(0, left);
        var lastX = Math.Min(Width, left + source.Width);
        var firstY = Math.Max(0, top);
        var lastY = Math.Min(Height, top + source.Height);
        for (var y = firstY; y < lastY; y++)
            for (var x = firstX; x < lastX; x++)
            {
                var index = y * Width + x;
                var coverage = geometricClip is { } clip
                    ? SvgCoverageRasterizer.RectangleCoverage(new(clip.X, clip.Y), new(clip.X + clip.Z, clip.Y + clip.W), x, y) : 1;
                CompositePixel(source, (y - top) * source.Width + x - left, index, opacity, coverage);
            }
    }
}
