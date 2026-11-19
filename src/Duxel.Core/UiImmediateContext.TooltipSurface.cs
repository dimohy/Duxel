namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    /// <summary>Paints the combined rounded body/arrow and sequential Fluent drop shadows.</summary>
    internal void FluentTooltipSurface(UiPopupLayout layout, bool inverted = false) =>
        _ui.PaintFluentTooltipSurface(layout, inverted);
}

public sealed partial class UiImmediateContext
{
    private TooltipSurfaceTexture? _tooltipSurfaceTexture;
    private int _tooltipSurfacePaintFrame = -1;
    // Keep ordinary RGBA images below the renderer's font-mask threshold; the window icon owns 1_000_000_001.
    private nuint _nextTooltipSurfaceTextureId = 1_000_000_002;

    private sealed record TooltipSurfaceTexture(FluentPopupSurfaceKey Key, UiTextureId Id, int Width, int Height);
    internal readonly record struct FluentPopupSurfaceKey(float Width, float Height, float PhaseX, float PhaseY,
        float Scale, UiVector2 ArrowCenter, int ArrowDirection, bool Dark, bool Inverted, float ArrowHeight = 6, UiColor? Fill = null, float KeySigma = 8, float KeyOffset = 4);

    internal void PaintFluentTooltipSurface(UiPopupLayout layout, bool inverted)
    {
        EnsureNotMeasuring();
        var body = layout.Bounds;
        var scale = ContentScale;
        if (!float.IsFinite(body.X) || !float.IsFinite(body.Y) || !float.IsFinite(body.Width)
            || !float.IsFinite(body.Height) || body.Width <= 0 || body.Height <= 0
            || !float.IsFinite(scale) || scale <= 0
            || !float.IsFinite(body.X * scale) || !float.IsFinite(body.Y * scale))
        {
            throw new ArgumentOutOfRangeException(nameof(layout));
        }
        var pixelX = MathF.Floor(body.X * scale);
        var pixelY = MathF.Floor(body.Y * scale);
        var center = default(UiVector2);
        var direction = -1;
        if (layout.Arrow is { } arrow)
        {
            center = new((arrow.BaseStart.X + arrow.BaseEnd.X) / 2 - body.X,
                (arrow.BaseStart.Y + arrow.BaseEnd.Y) / 2 - body.Y);
            var dx = arrow.Tip.X - body.X - center.X;
            var dy = arrow.Tip.Y - body.Y - center.Y;
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y)
                || !float.IsFinite(dx) || !float.IsFinite(dy)
                || MathF.Abs(MathF.Abs(dx) + MathF.Abs(dy) - 6) > .001f || dx != 0 && dy != 0
                || MathF.Abs(MathF.Abs(arrow.BaseEnd.X - arrow.BaseStart.X)
                    + MathF.Abs(arrow.BaseEnd.Y - arrow.BaseStart.Y) - 8.484f) > .002f)
            {
                throw new ArgumentException("A Fluent arrow must face one axis with height 6.", nameof(layout));
            }
            direction = dy > 0 ? 0 : dy < 0 ? 1 : dx < 0 ? 2 : 3;
        }
        var dark = ControlFluentColorScheme switch
        {
            UiFluentColorScheme.Light => false,
            UiFluentColorScheme.Dark => true,
            _ => throw new NotSupportedException("Unknown Fluent color scheme.")
        };
        var key = new FluentPopupSurfaceKey(body.Width, body.Height, body.X * scale - pixelX,
            body.Y * scale - pixelY, scale, center, direction, dark, inverted);
        var marginPixels = Math.Ceiling(48d * scale);
        if (marginPixels > 2048)
        {
            throw new NotSupportedException("The tooltip scale exceeds the bounded raster size.");
        }
        var margin = (int)marginPixels;
        if (_tooltipSurfaceTexture is not { } surface || surface.Key != key)
        {
            if (_tooltipSurfacePaintFrame == _state.FrameCount)
            {
                throw new InvalidOperationException("A tooltip may paint only one Fluent surface shape per frame.");
            }
            if (_nextTooltipSurfaceTextureId >= 1_100_000_000)
            {
                throw new InvalidOperationException("The reserved tooltip color texture ID range is exhausted.");
            }
            var image = RasterizeFluentPopupSurface(key, margin);
            ReleaseTooltipSurface();
            surface = new(key, new(_nextTooltipSurfaceTextureId++), image.Width, image.Height);
            QueueTextureUpdate(new(UiTextureUpdateKind.Create, surface.Id, UiTextureFormat.Rgba8Unorm,
                image.Width, image.Height, image.RgbaPixels));
            _tooltipSurfaceTexture = surface;
        }
        _tooltipSurfacePaintFrame = _state.FrameCount;
        var origin = new UiVector2((pixelX - margin) / scale, (pixelY - margin) / scale);
        _builder.AddImage(surface.Id, origin,
            new(origin.X + surface.Width / scale, origin.Y + surface.Height / scale),
            default, new(1, 1), new(255, 255, 255), CurrentClipRect);
    }

    private void ReleaseTooltipSurface()
    {
        if (_tooltipSurfaceTexture is not { } surface)
        {
            return;
        }
        DestroyTooltipSurface(surface);
        _tooltipSurfaceTexture = null;
    }

    private void DestroyTooltipSurface(TooltipSurfaceTexture surface) =>
        QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, surface.Id, UiTextureFormat.Rgba8Unorm,
            surface.Width, surface.Height, ReadOnlyMemory<byte>.Empty));

    internal static UiImageData RasterizeFluentPopupSurface(FluentPopupSurfaceKey key, int margin)
    {
        var pixelWidth = Math.Ceiling(key.Width * (double)key.Scale + key.PhaseX) + 2d * margin;
        var pixelHeight = Math.Ceiling(key.Height * (double)key.Scale + key.PhaseY) + 2d * margin;
        if (pixelWidth > 4096 || pixelHeight > 4096 || pixelWidth * pixelHeight > 4_194_304)
        {
            throw new NotSupportedException("The tooltip surface exceeds the bounded raster size.");
        }
        var width = (int)pixelWidth;
        var height = (int)pixelHeight;
        var count = width * height;
        return RasterizeFluentPopupSurface(key, margin, width, height, new float[count * 4]);
    }

    private static UiImageData RasterizeFluentPopupSurface(FluentPopupSurfaceKey key, int margin, int width, int height,
        Span<float> scratch)
    {
        var count = width * height;
        var mask = scratch[..count];
        var horizontal = scratch.Slice(count, count);
        var blur = scratch.Slice(count * 2, count);
        var first = scratch.Slice(count * 3, count);
        // The zero-initialized mask includes the shadow padding skipped by the coverage loop.
        var radius = MathF.Min(4, MathF.Min(key.Width, key.Height) / 2);
        const int samples = 8;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Only the source's neighborhood needs coverage samples; shadow padding stays zero.
                if (x < margin - (key.ArrowHeight + 4) * key.Scale || x > margin + (key.Width + key.ArrowHeight + 4) * key.Scale
                    || y < margin - (key.ArrowHeight + 4) * key.Scale || y > margin + (key.Height + key.ArrowHeight + 4) * key.Scale)
                {
                    continue;
                }
                var coverage = 0;
                for (var sy = 0; sy < samples; sy++)
                {
                    for (var sx = 0; sx < samples; sx++)
                    {
                        var px = (x + (sx + .5f) / samples - margin - key.PhaseX) / key.Scale;
                        var py = (y + (sy + .5f) / samples - margin - key.PhaseY) / key.Scale;
                        if (TooltipBodyContains(px, py, key.Width, key.Height, radius)
                            || FluentPopupArrowContains(px, py, key))
                        {
                            coverage++;
                        }
                    }
                }
                mask[y * width + x] = coverage / (float)(samples * samples);
            }
        }
        BlurTooltipAlpha(mask, horizontal, blur, width, height, 2 * key.Scale);
        var ambientOpacity = key.Dark ? .24f : .12f;
        for (var index = 0; index < first.Length; index++)
        {
            first[index] = mask[index] + blur[index] * ambientOpacity * (1 - mask[index]);
        }
        // The second filter consumes the complete first result, including its shadow alpha.
        BlurTooltipAlpha(first, horizontal, blur, width, height, key.KeySigma * key.Scale);
        var keyOpacity = key.Dark ? .28f : .14f;
        var fill = key.Inverted ? key.Dark ? 61 : 51 : key.Dark ? 41 : 255;
        var pixels = new byte[mask.Length * 4];
        var offset = key.KeyOffset * key.Scale;
        for (var y = 0; y < height; y++)
        {
            var sourceY = y - offset;
            var top = (int)MathF.Floor(sourceY);
            var fraction = sourceY - top;
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var shadow = (top >= 0 ? blur[top * width + x] * (1 - fraction) : 0)
                    + (top + 1 >= 0 ? blur[(top + 1) * width + x] * fraction : 0);
                var alpha = first[index] + shadow * keyOpacity * (1 - first[index]);
                var color = alpha > 0 ? (byte)Math.Clamp((int)MathF.Round(fill * mask[index] / alpha), 0, 255) : (byte)0;
                if (key.Fill is { } fillColor)
                {
                    var source = mask[index] / (alpha > 0 ? alpha : 1);
                    pixels[index * 4] = (byte)Math.Clamp((int)MathF.Round((byte)fillColor.Rgba * source), 0, 255);
                    pixels[index * 4 + 1] = (byte)Math.Clamp((int)MathF.Round((byte)(fillColor.Rgba >> 8) * source), 0, 255);
                    pixels[index * 4 + 2] = (byte)Math.Clamp((int)MathF.Round((byte)(fillColor.Rgba >> 16) * source), 0, 255);
                }
                else
                {
                    pixels[index * 4] = pixels[index * 4 + 1] = pixels[index * 4 + 2] = color;
                }
                pixels[index * 4 + 3] = (byte)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255);
            }
        }
        return new(width, height, pixels);
    }

    private static bool TooltipBodyContains(float x, float y, float width, float height, float radius)
    {
        if (x < 0 || x > width || y < 0 || y > height)
        {
            return false;
        }
        var dx = x - Math.Clamp(x, radius, width - radius);
        var dy = y - Math.Clamp(y, radius, height - radius);
        return dx * dx + dy * dy <= radius * radius;
    }

    internal static bool FluentPopupArrowContains(float x, float y, FluentPopupSurfaceKey key)
    {
        if (key.ArrowDirection < 0)
        {
            return false;
        }
        var edge = key.ArrowHeight * 1.414f;
        const float diagonal = .7071067811865476f;
        var normal = key.ArrowDirection switch
        {
            0 => new UiVector2(0, 1),
            1 => new UiVector2(0, -1),
            2 => new UiVector2(-1, 0),
            _ => new UiVector2(1, 0)
        };
        var dx = x - key.ArrowCenter.X + normal.X;
        var dy = y - key.ArrowCenter.Y + normal.Y;
        var (cosine, sine) = key.ArrowDirection switch
        {
            0 => (diagonal, -diagonal),
            1 => (-diagonal, diagonal),
            2 => (diagonal, diagonal),
            _ => (-diagonal, -diagonal)
        };
        var u = cosine * dx + sine * dy + edge / 2;
        var v = -sine * dx + cosine * dy + edge / 2;
        var square = u >= 0 && u <= edge && v >= 0 && v <= edge
            && (u >= 2 || v <= edge - 2 || (u - 2) * (u - 2) + (v - edge + 2) * (v - edge + 2) <= 4);
        // The pseudo-element expands by its transparent border before the triangular clip.
        var pseudo = u >= -1 && u <= edge + 1 && v >= -1 && v <= edge + 1 && v >= u
            && (u >= 1 || v <= edge - 1 || (u - 1) * (u - 1) + (v - edge + 1) * (v - edge + 1) <= 4);
        return square || pseudo;
    }

    private static void BlurTooltipAlpha(ReadOnlySpan<float> input, Span<float> horizontal, Span<float> result,
        int width, int height, float sigma)
    {
        var radius = (int)MathF.Ceiling(4 * sigma);
        var weights = new float[2 * radius + 1];
        var total = 0f;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var weight = MathF.Exp(-offset * (float)offset / (2 * sigma * sigma));
            weights[offset + radius] = weight;
            total += weight;
        }
        for (var index = 0; index < weights.Length; index++)
        {
            weights[index] /= total;
        }
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = 0f;
                for (var offset = Math.Max(-radius, -x); offset <= Math.Min(radius, width - x - 1); offset++)
                {
                    value += input[y * width + x + offset] * weights[offset + radius];
                }
                horizontal[y * width + x] = value;
            }
        }
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = 0f;
                for (var offset = Math.Max(-radius, -y); offset <= Math.Min(radius, height - y - 1); offset++)
                {
                    value += horizontal[(y + offset) * width + x] * weights[offset + radius];
                }
                result[y * width + x] = value;
            }
        }
    }
}
