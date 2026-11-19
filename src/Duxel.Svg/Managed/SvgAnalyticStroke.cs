using System.Buffers;
using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Exact local offsets for circular-corner primitive strokes; arbitrary curves keep the stroke program.</summary>
internal sealed record SvgAnalyticStroke(SvgAnalyticRRect Outer, SvgAnalyticRRect? Inner)
{
    internal static SvgAnalyticStroke? Create(SvgGeometry source, float width, SvgDashPattern? pattern)
    {
        if (width <= 0 || pattern is not null || source.Analytic is not { } shape ||
            shape.Radii.X <= 0 || shape.Radii.X != shape.Radii.Y)
            return null;
        var half = width / 2;
        var bounds = shape.Rectangle;
        var outer = new SvgAnalyticRRect(new(bounds.X - half, bounds.Y - half, bounds.Z + width, bounds.W + width),
            shape.Radii + new Vector2(half), shape.Frame);
        var inner = bounds.Z > width && bounds.W > width
            ? new SvgAnalyticRRect(new(bounds.X + half, bounds.Y + half, bounds.Z - width, bounds.W - width),
                new Vector2(MathF.Max(0, shape.Radii.X - half)), shape.Frame) : null;
        return new(outer, inner);
    }

    internal void Rasterize(Matrix3x2 transform, int width, int height, SvgCoverageRow consume, Vector4? clip)
    {
        if (Inner is null)
        {
            Outer.Rasterize(transform, width, height, consume, clip);
            return;
        }
        // This is used only by explicitly requested bitmap output. Interactive SVG stays on the GPU.
        var holes = ArrayPool<float>.Shared.Rent(checked(width * height));
        try
        {
            Array.Clear(holes, 0, width * height);
            Inner.Rasterize(transform, width, height, (row, coverage, _, _) =>
                coverage.CopyTo(holes.AsSpan(row * width, width)), clip);
            Outer.Rasterize(transform, width, height, (row, coverage, masks, points) =>
            {
                var output = holes.AsSpan(row * width, width);
                for (var x = 0; x < width; x++)
                    output[x] = Math.Clamp(coverage[x] - output[x], 0, 1);
                consume(row, output, masks, points);
            }, clip);
        }
        finally { ArrayPool<float>.Shared.Return(holes); }
    }
}
