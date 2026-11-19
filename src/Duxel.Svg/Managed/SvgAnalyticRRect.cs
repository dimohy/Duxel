using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Retains one analytic ellipse, rounded rectangle or capsule in a canonical local frame.</summary>
internal sealed record SvgAnalyticRRect(Vector4 Rectangle, Vector2 Radii, Matrix3x2 Frame)
{
    private readonly record struct Vertex(Vector2 Position, Vector2 Local, float Outset);
    private readonly record struct Triangle(Vertex A, Vertex B, Vertex C);

    internal void Rasterize(Matrix3x2 transform, int width, int height, SvgCoverageRow consume, Vector4? clip)
    {
        var mapping = Frame * transform;
        if (!Matrix3x2.Invert(mapping, out var inverse)) throw new InvalidDataException("SVG analytic shape requires an invertible mapping.");
        var left = Rectangle.X;
        var top = Rectangle.Y;
        var right = left + Rectangle.Z;
        var bottom = top + Rectangle.W;
        var rx = Radii.X;
        var ry = Radii.Y;
        const float corner = .4142135623730951f;
        var dx = (1 - corner) * rx;
        var dy = (1 - corner) * ry;
        Vector2[] local = [new(left, top + dy), new(left + dx, top), new(right - dx, top), new(right, top + dy),
            new(right, bottom - dy), new(right - dx, bottom), new(left + dx, bottom), new(left, bottom - dy)];
        var points = local.Select(point => Vector2.Transform(point, mapping)).ToArray();
        var normals = new Vector2[8];
        var winding = mapping.GetDeterminant() > 0 ? 1 : -1;
        for (var index = 0; index < 8; index++)
        {
            var edge = points[(index + 1) % 8] - points[index];
            normals[index] = SvgStrokeCurve.Unit(new Vector2(edge.Y, -edge.X)) * winding;
        }
        List<Triangle> triangles = new(32);
        var center = new Vector2((left + right) / 2, (top + bottom) / 2);
        var centerVertex = Make(center, Vector2.Transform(center, mapping), 0);
        for (var index = 0; index < 8; index++)
        {
            var next = (index + 1) % 8;
            var a = Make(local[index], points[index], 0);
            var b = Make(local[next], points[next], 0);
            var outerA = Make(local[index], points[index] + normals[index], 1);
            var outerB = Make(local[next], points[next] + normals[index], 1);
            var previous = Make(local[index], points[index] + normals[(index + 7) % 8], 1);
            if (local[index].X != local[next].X && local[index].Y != local[next].Y)
            {
                triangles.Add(new(a, outerA, b));
                triangles.Add(new(outerA, b, outerB));
            }
            else
            {
                triangles.Add(new(a, b, outerB));
                triangles.Add(new(a, outerB, outerA));
            }
            triangles.Add(new(a, previous, outerA));
            triangles.Add(new(centerVertex, a, b));
        }
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        foreach (var triangle in triangles)
        {
            min = Vector2.Min(min, Vector2.Min(triangle.A.Position, Vector2.Min(triangle.B.Position, triangle.C.Position)));
            max = Vector2.Max(max, Vector2.Max(triangle.A.Position, Vector2.Max(triangle.B.Position, triangle.C.Position)));
        }
        var firstRow = (int)Math.Clamp(MathF.Floor(min.Y), 0, height);
        var lastRow = (int)Math.Clamp(MathF.Ceiling(max.Y), 0, height);
        var coverage = new float[width];
        var paintPoints = new Vector2[width];
        var visited = new bool[width];
        var xGradient = new Vector2(inverse.M11, inverse.M21);
        var yGradient = new Vector2(inverse.M12, inverse.M22);
        var planeScale = new Vector2(1 / xGradient.Length(), 1 / yGradient.Length());
        var coverageScale = MathF.Min(1, MathF.Min(Rectangle.Z * planeScale.X, Rectangle.W * planeScale.Y));
        var coverageBias = 1 - .5f * coverageScale;
        for (var row = firstRow; row < lastRow; row++)
        {
            Array.Clear(coverage);
            Array.Clear(visited);
            foreach (var triangle in triangles)
            {
                var a = triangle.A.Position;
                var b = triangle.B.Position;
                var c = triangle.C.Position;
                var y = row + .5;
                if (y < MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) || y > MathF.Max(a.Y, MathF.Max(b.Y, c.Y))) continue;
                var determinant = ((double)b.Y - c.Y) * (a.X - c.X) + ((double)c.X - b.X) * (a.Y - c.Y);
                if (determinant == 0) continue;
                var firstColumn = (int)Math.Clamp(MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))), 0, width);
                var lastColumn = (int)Math.Clamp(MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), 0, width);
                for (var column = firstColumn; column < lastColumn; column++)
                {
                    if (visited[column]) continue;
                    var x = column + .5;
                    var w0 = (((double)b.Y - c.Y) * (x - c.X) + ((double)c.X - b.X) * (y - c.Y)) / determinant;
                    var w1 = (((double)c.Y - a.Y) * (x - c.X) + ((double)a.X - c.X) * (y - c.Y)) / determinant;
                    var w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    visited[column] = true;
                    var point = new Vector2((float)(triangle.A.Local.X * w0 + triangle.B.Local.X * w1 + triangle.C.Local.X * w2),
                        (float)(triangle.A.Local.Y * w0 + triangle.B.Local.Y * w1 + triangle.C.Local.Y * w2));
                    var outset = (float)(triangle.A.Outset * w0 + triangle.B.Outset * w1 + triangle.C.Outset * w2);
                    var distance = MathF.Min(MathF.Min(point.X - left, right - point.X) * planeScale.X,
                        MathF.Min(point.Y - top, bottom - point.Y) * planeScale.Y);
                    var uv = new Vector2(point.X < left + rx ? point.X - left - rx : point.X > right - rx ? point.X - right + rx : 0,
                        point.Y < top + ry ? point.Y - top - ry : point.Y > bottom - ry ? point.Y - bottom + ry : 0);
                    if (uv.X != 0 && uv.Y != 0)
                    {
                        var normalized = uv / (Radii * Radii);
                        var gradient = normalized.X * xGradient + normalized.Y * yGradient;
                        var curveDistance = .5f * (1 - Vector2.Dot(uv, normalized)) / gradient.Length();
                        distance = MathF.Min(distance, curveDistance);
                    }
                    var value = Math.Clamp(coverageScale * (distance - outset + coverageBias), 0, 1);
                    if (!float.IsFinite(value)) throw new InvalidDataException("SVG analytic coverage exceeds its numeric range.");
                    coverage[column] = value * (clip is { } rectangle ? SvgViewport.Coverage(rectangle, column, row) : 1);
                    paintPoints[column] = Vector2.Transform(point, mapping);
                }
            }
            consume(row, coverage, [], paintPoints);
        }

        static Vertex Make(Vector2 localPoint, Vector2 devicePoint, float outset)
        {
            var snapped = new Vector2((float)(Math.Round((double)devicePoint.X * 256) / 256),
                (float)(Math.Round((double)devicePoint.Y * 256) / 256));
            return new(snapped, localPoint, outset);
        }
    }
}
