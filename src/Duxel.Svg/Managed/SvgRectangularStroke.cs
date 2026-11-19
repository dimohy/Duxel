using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Analytic coverage for a mitered rectangular stroke, independently of MSAA samples.</summary>
internal sealed record SvgRectangularStroke(Vector4 Outer, Vector4? Inner)
{
    internal static SvgRectangularStroke? Create(SvgGeometry source, float width, string join,
        float miterLimit, SvgDashPattern? pattern)
    {
        if (width <= 0 || pattern is not null || join != "miter" || miterLimit < MathF.Sqrt(2) ||
            !SvgRectangleGeometry.TryBounds(source, Matrix3x2.Identity, out var rectangle))
            return null;
        var radius = width / 2;
        var outer = new Vector4(rectangle.X - radius, rectangle.Y - radius, rectangle.Z + width, rectangle.W + width);
        Vector4? inner = rectangle.Z > width && rectangle.W > width
            ? new(rectangle.X + radius, rectangle.Y + radius, rectangle.Z - width, rectangle.W - width) : null;
        return new(outer, inner);
    }

    internal SvgRectangularStroke? Map(Matrix3x2 transform)
    {
        if (transform.GetDeterminant() == 0 ||
            !(transform.M12 == 0 && transform.M21 == 0 || transform.M11 == 0 && transform.M22 == 0))
            return null;
        Vector4 Rectangle(Vector4 bounds)
        {
            var a = Vector2.Transform(new(bounds.X, bounds.Y), transform);
            var b = Vector2.Transform(new(bounds.X + bounds.Z, bounds.Y + bounds.W), transform);
            var minimum = Vector2.Min(a, b);
            var size = Vector2.Abs(b - a);
            return new(minimum.X, minimum.Y, size.X, size.Y);
        }
        return new(Rectangle(Outer), Inner is { } inner ? Rectangle(inner) : null);
    }

    internal float Coverage(int x, int y)
    {
        static float Rectangle(Vector4 r, int x, int y) => SvgCoverageRasterizer.RectangleCoverage(
            new(r.X, r.Y), new(r.X + r.Z, r.Y + r.W), x, y);
        return Math.Clamp(Rectangle(Outer, x, y) - (Inner is { } inner ? Rectangle(inner, x, y) : 0), 0, 1);
    }

    internal void Rasterize(int width, int height, SvgCoverageRow consume, Vector4? clip)
    {
        var first = (int)Math.Clamp(MathF.Floor(Outer.X), 0, width);
        var last = (int)Math.Clamp(MathF.Ceiling(Outer.X + Outer.Z), 0, width);
        var top = (int)Math.Clamp(MathF.Floor(Outer.Y), 0, height);
        var bottom = (int)Math.Clamp(MathF.Ceiling(Outer.Y + Outer.W), 0, height);
        var coverage = new float[width];
        for (var y = top; y < bottom; y++)
        {
            for (var x = first; x < last; x++)
                coverage[x] = Coverage(x, y) * (clip is { } rectangle ? SvgViewport.Coverage(rectangle, x, y) : 1);
            consume(y, coverage, [], []);
        }
    }
}
