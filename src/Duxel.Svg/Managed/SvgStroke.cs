using System.Numerics;

namespace Duxel.Svg.Managed;

internal static class SvgStroke
{
    internal static SvgGeometry Outline(SvgGeometry source, Matrix3x2 transform, float width, string cap, string join, float miterLimit,
        SvgDashPattern? pattern = null, float? pathLength = null, float tolerance = .5f)
    {
        Validate(width, cap, join, miterLimit);
        var outline = Build(source, transform, width, cap, join, miterLimit, pattern, pathLength, tolerance);
        if (SvgAnalyticStroke.Create(source, width, pattern) is { } ring)
            return outline.WithAnalyticStroke(ring);
        if (SvgRectangularStroke.Create(source, width, join, miterLimit, pattern) is { } rectangle)
            return outline.WithRectangularStroke(rectangle);
        return Analytic(source, width, cap, pattern) is { } shape ? outline.WithAnalytic(shape) : outline;
    }

    internal static SvgAnalyticRRect? Analytic(SvgGeometry source, float width, string cap, SvgDashPattern? pattern)
    {
        var segments = source.Segments;
        if (width > 0 && pattern is null && cap == "round" && segments.Length == 2 &&
            segments[0].Kind == SvgSegmentKind.Move && segments[1].Kind == SvgSegmentKind.Line && segments[0].End != segments[1].End)
        {
            var delta = segments[1].End - segments[0].End;
            var axis = SvgStrokeCurve.Unit(delta);
            var radius = width / 2;
            var frame = new Matrix3x2(axis.X, axis.Y, -axis.Y, axis.X, segments[0].End.X, segments[0].End.Y);
            return new(new(-radius, -radius, delta.Length() + width, width), new(radius), frame);
        }
        return null;
    }

    internal static void Validate(float width, string cap, string join, float miterLimit)
    {
        if (!float.IsFinite(width) || width < 0 || !float.IsFinite(miterLimit) || miterLimit < 1)
            throw new InvalidDataException("Invalid SVG stroke width or miter limit.");
        if (cap is not ("butt" or "square" or "round") || join is not ("miter" or "bevel" or "round"))
            throw new InvalidDataException("Invalid SVG stroke cap or join.");
    }

    private static SvgGeometry Build(SvgGeometry source, Matrix3x2 transform, float width, string cap, string join, float miterLimit,
        SvgDashPattern? pattern, float? pathLength, float tolerance)
    {
        List<SvgSegment> output = [];
        if (width == 0)
            return new([]);
        var paths = SvgStrokePath.Extract(source, tolerance);
        if (pattern is not null)
            paths = pattern.Apply(paths, pathLength);
        var half = width / 2;
        foreach (var path in paths)
        {
            if (!path.HasDrawing)
                continue;
            var curves = path.Curves.Where(curve => !curve.IsPoint).ToArray();
            if (curves.Length == 0)
            {
                if (cap == "round")
                    Circle(path.Origin);
                else if (cap == "square")
                {
                    var tangent = (path.DotDirection == default ? Vector2.UnitX : path.DotDirection) * half;
                    var normal = new Vector2(-tangent.Y, tangent.X);
                    Polygon([path.Origin - tangent + normal, path.Origin + tangent + normal,
                        path.Origin + tangent - normal, path.Origin - tangent - normal]);
                }
                continue;
            }
            const int first = 0;
            var last = curves.Length - 1;
            var closed = path.Closed;
            if (!closed && cap == "square")
            {
                SquareCap(curves[first].Start, -SvgStrokeCurve.Unit(curves[first].Tangent(0)));
                SquareCap(curves[last].End, SvgStrokeCurve.Unit(curves[last].Tangent(1)));
            }
            for (var index = first; index <= last; index++)
            {
                var curve = curves[index];
                var direction = SvgStrokeCurve.Unit(curve.Tangent(1));
                var normal = new Vector2(-direction.Y, direction.X) * half;
                var patches = SvgStrokeTessellation.Sections(curve, transform, half);
                for (var patchIndex = 0; patchIndex < patches.Length; patchIndex++)
                {
                    var sections = patches[patchIndex];
                    if (patchIndex > 0 && Vector2.Dot(patches[patchIndex - 1][^1].Tangent, sections[0].Tangent) < -.999f)
                        Circle(sections[0].Point);
                    for (var section = 1; section < sections.Length; section++)
                    {
                        var a = sections[section - 1];
                        var b = sections[section];
                        var start = a.Point;
                        var end = b.Point;
                        var startNormal = new Vector2(-a.Tangent.Y, a.Tangent.X) * half;
                        var endNormal = new Vector2(-b.Tangent.Y, b.Tangent.X) * half;
                        // Keep both strip triangles consistently wound, including folded inner offsets.
                        Polygon([start + startNormal, end + endNormal, end - endNormal]);
                        Polygon([start + startNormal, end - endNormal, start - startNormal]);
                    }
                }
                if (index < last || closed)
                {
                    var next = curves[index == last ? first : index + 1];
                    var nextDirection = SvgStrokeCurve.Unit(next.Tangent(0));
                    var cross = direction.X * nextDirection.Y - direction.Y * nextDirection.X;
                    if (join == "round")
                        RoundJoin(curve.End, direction, nextDirection);
                    else if (MathF.Abs(cross) > .00001f)
                    {
                        var sign = cross > 0 ? -1 : 1;
                        var a = normal * sign;
                        var b = new Vector2(-nextDirection.Y, nextDirection.X) * (half * sign);
                        var bisector = a + b;
                        var projection = Vector2.Dot(bisector, a);
                        var intersection = bisector * (half * half / projection);
                        if (join == "miter" && intersection.Length() <= half * miterLimit)
                            Polygon([curve.End + a, curve.End + intersection, curve.End + b, curve.End]);
                        else
                            Polygon([curve.End + a, curve.End, curve.End + b]);
                    }
                }
            }
            if (!closed && cap == "round")
            {
                Circle(curves[first].Start);
                Circle(curves[last].End);
            }
        }
        return new(output.ToArray());

        void SquareCap(Vector2 center, Vector2 direction)
        {
            var normal = new Vector2(-direction.Y, direction.X) * half;
            var end = center + direction * half;
            Polygon([center + normal, end + normal, end - normal, center - normal]);
        }

        void RoundJoin(Vector2 center, Vector2 incoming, Vector2 outgoing)
        {
            var cross = incoming.X * outgoing.Y - incoming.Y * outgoing.X;
            var dot = Math.Clamp(Vector2.Dot(incoming, outgoing), -1, 1);
            if (cross == 0)
            {
                if (dot < 0)
                    Circle(center);
                return;
            }
            var sign = cross > 0 ? -1 : 1;
            var a = new Vector2(-incoming.Y, incoming.X) * sign;
            var b = new Vector2(-outgoing.Y, outgoing.X) * sign;
            var rotation = Math.CopySign(Math.Acos(dot), cross);
            var normals = SvgStrokeTessellation.ArcNormals(a, b, rotation, transform, half);
            var points = new Vector2[normals.Length + 1];
            points[0] = center;
            for (var index = 0; index < normals.Length; index++)
                points[index + 1] = center + normals[index] * half;
            Polygon(points);
        }

        void Polygon(Vector2[] points)
        {
            if (output.Count > 1_000_000 - points.Length - 1)
                throw new InvalidDataException("SVG stroke outline exceeds its segment limit.");
            var area = 0d;
            var origin = points[0];
            for (var i = 1; i < points.Length - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                // Translation must not change the winding of overlapping stroke pieces.
                area += ((double)a.X - origin.X) * ((double)b.Y - origin.Y) -
                    ((double)a.Y - origin.Y) * ((double)b.X - origin.X);
            }
            if (area < 0)
                Array.Reverse(points);
            output.Add(new(SvgSegmentKind.Move, points[0]));
            for (var i = 1; i < points.Length; i++)
                output.Add(new(SvgSegmentKind.Line, points[i]));
            output.Add(new(SvgSegmentKind.Close, points[0]));
        }
        void Circle(Vector2 center)
        {
            var sections = SvgStrokeTessellation.PointSections(center, transform, half);
            for (var index = 1; index < sections.Length; index++)
            {
                var a = sections[index - 1].Tangent;
                var b = sections[index].Tangent;
                var firstNormal = new Vector2(-a.Y, a.X) * half;
                var lastNormal = new Vector2(-b.Y, b.X) * half;
                Polygon([center + firstNormal, center + lastNormal, center - lastNormal]);
                Polygon([center + firstNormal, center - lastNormal, center - firstNormal]);
            }
        }
    }
}
