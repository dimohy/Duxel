using System.Numerics;

namespace Duxel.Svg.Managed;

internal delegate void SvgCoverageRow(int row, ReadOnlySpan<float> coverage, ReadOnlySpan<byte> sampleMasks, ReadOnlySpan<Vector2> paintPoints);

/// <summary>Produces sample masks and independent analytic coverage before paint compositing.</summary>
internal static class SvgCoverageRasterizer
{
    internal static void Rasterize(SvgGeometry geometry, Matrix3x2 transform, bool evenOdd,
        int width, int height, SvgCoverageRow consume, Vector4? clip = null)
    {
        if (geometry.AnalyticStroke is { } ring)
        {
            ring.Rasterize(transform, width, height, consume, clip);
            return;
        }
        if (geometry.RectangularStroke?.Map(transform) is { } stroke)
        {
            stroke.Rasterize(width, height, consume, clip);
            return;
        }
        if (geometry.Analytic is { } analytic)
        {
            analytic.Rasterize(transform, width, height, consume, clip);
            return;
        }
        var smooth = geometry.SmoothPrimitive;
        var edges = geometry.Flatten(transform, smooth ? .05f : .25f, uniformCubics: !smooth);
        if (edges.Length == 0)
            return;
        if (IsRectangle(edges))
        {
            Rectangle(edges, width, height, consume, clip);
            return;
        }
        if (!smooth)
        {
            // Raster vertices share the browser GPU's eight fractional coordinate bits.
            static Vector2 Snap(Vector2 point) => new((float)(Math.Round((double)point.X * 256) / 256),
                (float)(Math.Round((double)point.Y * 256) / 256));
            for (var i = 0; i < edges.Length; i++)
                edges[i] = new(Snap(edges[i].Start), Snap(edges[i].End));
        }
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var edge in edges)
        {
            minY = MathF.Min(minY, MathF.Min(edge.Start.Y, edge.End.Y));
            maxY = MathF.Max(maxY, MathF.Max(edge.Start.Y, edge.End.Y));
        }
        var top = (int)Math.Clamp(MathF.Floor(minY), 0, height);
        var bottom = (int)Math.Clamp(MathF.Ceiling(maxY), 0, height);
        var coverage = new float[width];
        byte[] sampleMasks = smooth ? [] : new byte[width];
        List<(float X, int Winding)> crossings = [];
        // General vector paths follow the standard four-sample GPU pattern.
        // Primitive ellipses and rounded rectangles keep smooth area coverage.
        var samples = smooth ? 8 : 4;
        ReadOnlySpan<float> sampleX = [.375f, .875f, .125f, .625f];
        for (var row = top; row < bottom; row++)
        {
            Array.Clear(coverage);
            Array.Clear(sampleMasks);
            for (var sub = 0; sub < samples; sub++)
            {
                var y = row + (smooth ? (sub + .5f) / samples : .125f + sub * .25f);
                crossings.Clear();
                foreach (var edge in edges)
                {
                    var a = edge.Start;
                    var b = edge.End;
                    if (y < MathF.Min(a.Y, b.Y) || y >= MathF.Max(a.Y, b.Y))
                        continue;
                    var x = (float)(a.X + ((double)y - a.Y) / ((double)b.Y - a.Y) * ((double)b.X - a.X));
                    crossings.Add((x, b.Y > a.Y ? 1 : -1));
                }
                crossings.Sort(static (a, b) => a.X.CompareTo(b.X));
                var winding = 0;
                var previous = 0f;
                foreach (var crossing in crossings)
                {
                    if (evenOdd ? (winding & 1) != 0 : winding != 0)
                        SpanCoverage(previous, crossing.X, smooth ? 0 : sampleX[sub], (byte)(1 << sub));
                    winding += crossing.Winding;
                    previous = crossing.X;
                }
            }
            if (!smooth)
                for (var column = 0; column < width; column++)
                    coverage[column] = sampleMasks[column] == 0 ? 0 : clip is { } rectangle
                        ? SvgViewport.Coverage(rectangle, column, row) : 1;
            else if (clip is { } rectangle)
                for (var column = 0; column < width; column++)
                    coverage[column] *= SvgViewport.Coverage(rectangle, column, row);
            consume(row, coverage, sampleMasks, []);
        }

        void SpanCoverage(float left, float right, float sampleOffset, byte mask)
        {
            if (!smooth)
            {
                var firstSample = (int)Math.Clamp(MathF.Ceiling(left - sampleOffset), 0, width);
                var lastSample = (int)Math.Clamp(MathF.Ceiling(right - sampleOffset), 0, width);
                for (var column = firstSample; column < lastSample; column++)
                {
                    sampleMasks[column] |= mask;
                }
                return;
            }
            left = Math.Clamp(left, 0, width);
            right = Math.Clamp(right, 0, width);
            var first = (int)MathF.Floor(left);
            var last = (int)MathF.Ceiling(right);
            for (var column = first; column < last; column++)
                coverage[column] += (MathF.Min(column + 1, right) - MathF.Max(column, left)) / samples;
        }
    }

    private static bool IsRectangle(SvgEdge[] edges)
    {
        if (edges.Length != 4)
            return false;
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = edges[i];
            if (edge.End != edges[(i + 1) % edges.Length].Start ||
                edge.Start.X != edge.End.X && edge.Start.Y != edge.End.Y)
                return false;
            var next = edges[(i + 1) % edges.Length];
            if ((edge.Start.X == edge.End.X) == (next.Start.X == next.End.X))
                return false;
        }
        return true;
    }

    private static void Rectangle(SvgEdge[] edges, int width, int height, SvgCoverageRow consume, Vector4? clip)
    {
        var min = edges[0].Start;
        var max = min;
        foreach (var edge in edges.AsSpan(1))
        {
            min = Vector2.Min(min, edge.Start);
            max = Vector2.Max(max, edge.Start);
        }
        if (clip is { } rectangle)
        {
            min = Vector2.Max(min, new(rectangle.X, rectangle.Y));
            max = Vector2.Min(max, new(rectangle.X + rectangle.Z, rectangle.Y + rectangle.W));
        }
        if (max.X <= min.X || max.Y <= min.Y)
            return;
        var top = (int)Math.Clamp(MathF.Floor(min.Y), 0, height);
        var bottom = (int)Math.Clamp(MathF.Ceiling(max.Y), 0, height);
        var first = (int)Math.Clamp(MathF.Floor(min.X), 0, width);
        var last = (int)Math.Clamp(MathF.Ceiling(max.X), 0, width);
        var coverage = new float[width];
        for (var y = top; y < bottom; y++)
        {
            if (first < last)
            {
                coverage[first] = RectangleCoverage(min, max, first, y);
                if (last - first > 1)
                    coverage[last - 1] = RectangleCoverage(min, max, last - 1, y);
                if (last - first > 2)
                {
                    // The interior columns span a whole pixel horizontally.
                    var rowCoverage = Math.Clamp(MathF.Min(y + 1, max.Y) - MathF.Max(y, min.Y), 0, 1);
                    coverage.AsSpan(first + 1, last - first - 2).Fill(rowCoverage);
                }
            }
            consume(y, coverage, [], []);
        }
    }

    internal static float RectangleCoverage(Vector2 minimum, Vector2 maximum, int x, int y)
    {
        var covered = Vector2.Min(new(x + 1, y + 1), maximum) - Vector2.Max(new(x, y), minimum);
        var coverage = Math.Clamp(MathF.Min(covered.X, covered.Y), 0, 1);
        var center = new Vector2(x + .5f, y + .5f);
        var outside = Vector2.Max(minimum - center, center - maximum);
        return outside.X > 0 && outside.Y > 0
            ? MathF.Min(coverage, MathF.Max(0, .5f - outside.Length())) : coverage;
    }
}
