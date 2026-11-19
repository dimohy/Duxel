using System.Numerics;

namespace Duxel.Svg.Managed;

internal static class SvgRectangleGeometry
{
    internal static bool TryBounds(SvgGeometry geometry, Matrix3x2 frame, out Vector4 bounds)
    {
        bounds = default;
        var segments = geometry.Segments;
        if (segments.Length != 5 || segments[0].Kind != SvgSegmentKind.Move ||
            segments[1].Kind != SvgSegmentKind.Line || segments[2].Kind != SvgSegmentKind.Line ||
            segments[3].Kind != SvgSegmentKind.Line || segments[4].Kind != SvgSegmentKind.Close)
            return false;
        Span<Vector2> points = stackalloc Vector2[4];
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = Vector2.Transform(segments[index].End, frame);
            minimum = Vector2.Min(minimum, points[index]);
            maximum = Vector2.Max(maximum, points[index]);
        }
        for (var index = 0; index < points.Length; index++)
        {
            var first = points[index];
            var next = points[(index + 1) % points.Length];
            if (first == next || first.X != next.X && first.Y != next.Y)
                return false;
        }
        if ((points[0].X == points[1].X) == (points[1].X == points[2].X))
            return false;
        bounds = new(minimum.X, minimum.Y, maximum.X - minimum.X, maximum.Y - minimum.Y);
        return true;
    }
}
