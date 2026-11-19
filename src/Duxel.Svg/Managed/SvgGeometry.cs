using System.Numerics;

namespace Duxel.Svg.Managed;

internal enum SvgSegmentKind
{
    Move, Line, Cubic, Close, Conic
}
internal readonly record struct SvgSegment(SvgSegmentKind Kind, Vector2 End, Vector2 Control1 = default, Vector2 Control2 = default, float Weight = 1);
internal readonly record struct SvgEdge(Vector2 Start, Vector2 End);

/// <summary>Retains normalized geometry; source commands are consumed only during compilation.</summary>
internal sealed class SvgGeometry(SvgSegment[] segments, bool smoothPrimitive = false, SvgAnalyticRRect? analytic = null,
    SvgRectangularStroke? rectangularStroke = null, SvgAnalyticStroke? analyticStroke = null)
{
    internal ReadOnlySpan<SvgSegment> Segments => segments;
    internal bool SmoothPrimitive { get; } = smoothPrimitive;
    internal SvgAnalyticRRect? Analytic { get; } = analytic;
    internal SvgRectangularStroke? RectangularStroke { get; } = rectangularStroke;
    internal SvgAnalyticStroke? AnalyticStroke { get; } = analyticStroke;
    internal SvgGeometry WithAnalytic(SvgAnalyticRRect? shape) => new(segments, SmoothPrimitive, shape, RectangularStroke, AnalyticStroke);
    internal SvgGeometry WithRectangularStroke(SvgRectangularStroke shape) => new(segments, SmoothPrimitive, Analytic, shape, AnalyticStroke);
    internal SvgGeometry WithAnalyticStroke(SvgAnalyticStroke shape) => new(segments, SmoothPrimitive, null, RectangularStroke, shape);
    internal Vector4 Bounds { get; } = Measure(segments);

    private static Vector4 Measure(SvgSegment[] source)
    {
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        var point = Vector2.Zero;
        void Include(Vector2 value)
        {
            minimum = Vector2.Min(minimum, value);
            maximum = Vector2.Max(maximum, value);
        }
        foreach (var segment in source)
        {
            if (!Finite(segment.End) || segment.Kind == SvgSegmentKind.Cubic && (!Finite(segment.Control1) || !Finite(segment.Control2)))
                throw new InvalidDataException("SVG geometry coordinates must remain finite after normalization.");
            if (segment.Kind == SvgSegmentKind.Conic && (!Finite(segment.Control1) || !float.IsFinite(segment.Weight) || segment.Weight <= 0))
                throw new InvalidDataException("SVG conics require finite controls and positive weights.");
            if (segment.Kind != SvgSegmentKind.Move)
                Include(point);
            Include(segment.End);
            if (segment.Kind == SvgSegmentKind.Conic)
            {
                var weights = new Vector3(1, segment.Weight, 1);
                var start = point;
                foreach (var vertical in new[] { false, true })
                {
                    var roots = vertical ? SvgConicMath.Extrema(start.Y, segment.Control1.Y, segment.End.Y, weights)
                        : SvgConicMath.Extrema(start.X, segment.Control1.X, segment.End.X, weights);
                    if (roots.Count > 0) Include(SvgConicMath.Evaluate(start, segment.Control1, segment.End, weights, roots.First));
                    if (roots.Count > 1) Include(SvgConicMath.Evaluate(start, segment.Control1, segment.End, weights, roots.Second));
                }
            }
            if (segment.Kind == SvgSegmentKind.Cubic)
            {
                var start = point;
                void Extremes(double a, double b, double c, double d)
                {
                    var quadratic = -a + 3 * b - 3 * c + d;
                    var linear = 2 * (a - 2 * b + c);
                    var constant = b - a;
                    void Root(double t)
                    {
                        if (t <= 0 || t >= 1)
                            return;
                        var u = (float)t;
                        var v = 1 - u;
                        Include(v * v * v * start + 3 * v * v * u * segment.Control1 + 3 * v * u * u * segment.Control2 + u * u * u * segment.End);
                    }
                    if (Math.Abs(quadratic) < 1e-20)
                    {
                        if (linear != 0)
                            Root(-constant / linear);
                    }
                    else
                    {
                        var discriminant = linear * linear - 4 * quadratic * constant;
                        if (discriminant < 0)
                            return;
                        var square = Math.Sqrt(discriminant);
                        Root((-linear + square) / (2 * quadratic));
                        Root((-linear - square) / (2 * quadratic));
                    }
                }
                Extremes(start.X, segment.Control1.X, segment.Control2.X, segment.End.X);
                Extremes(start.Y, segment.Control1.Y, segment.Control2.Y, segment.End.Y);
            }
            point = segment.End;
        }
        return source.Length == 0 ? Vector4.Zero : new(minimum.X, minimum.Y, maximum.X - minimum.X, maximum.Y - minimum.Y);
    }

    internal SvgEdge[] Flatten(Matrix3x2 transform, float tolerance = .05f, bool closeOpenContours = true, bool uniformCubics = false)
    {
        if (!float.IsFinite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (!float.IsFinite(transform.M11) || !float.IsFinite(transform.M12) || !float.IsFinite(transform.M21) ||
            !float.IsFinite(transform.M22) || !float.IsFinite(transform.M31) || !float.IsFinite(transform.M32))
            throw new ArgumentOutOfRangeException(nameof(transform));
        List<SvgEdge> edges = [];
        var point = Vector2.Zero;
        var first = Vector2.Zero;
        var contour = false;
        void Edge(Vector2 a, Vector2 b)
        {
            if (a == b)
                return;
            if (edges.Count == 1_000_000)
                throw new InvalidDataException("SVG flattened geometry exceeds the edge limit.");
            edges.Add(new(a, b));
        }
        void Close()
        {
            if (contour)
                Edge(point, first);
            contour = false;
        }
        foreach (var segment in segments)
        {
            var end = Vector2.Transform(segment.End, transform);
            if (!Finite(end))
                throw new InvalidDataException("Transformed SVG coordinates overflowed.");
            if (segment.Kind == SvgSegmentKind.Move)
            {
                if (closeOpenContours)
                    Close();
                point = first = end;
                contour = true;
            }
            else if (segment.Kind == SvgSegmentKind.Close)
            {
                Close();
                point = first;
            }
            else
            {
                contour = true;
                if (segment.Kind == SvgSegmentKind.Conic)
                {
                    var control = Vector2.Transform(segment.Control1, transform);
                    var count = SvgConicMath.SegmentCount(point, control, end, segment.Weight, tolerance);
                    if (count > 1_000_000 - edges.Count)
                        throw new InvalidDataException("SVG conic tessellation exceeds the remaining edge limit.");
                    var previous = point;
                    for (var index = 1; index <= count; index++)
                    {
                        var value = index == count ? end : SvgConicMath.Evaluate(point, control, end,
                            new(1, segment.Weight, 1), index / (float)count);
                        Edge(previous, value);
                        previous = value;
                    }
                }
                else if (segment.Kind == SvgSegmentKind.Cubic)
                    Cubic(point, Vector2.Transform(segment.Control1, transform), Vector2.Transform(segment.Control2, transform), end, 0);
                else
                    Edge(point, end);
                point = end;
            }
        }
        if (closeOpenContours)
            Close();
        return edges.ToArray();

        void Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int depth)
        {
            if (!Finite(b) || !Finite(c))
                throw new InvalidDataException("Transformed SVG control points overflowed.");
            if (uniformCubics)
            {
                var count = SvgCubicTessellation.SegmentCount(a, b, c, d, tolerance);
                if (count > 1_000_000 - edges.Count)
                    throw new InvalidDataException("SVG cubic tessellation exceeds the remaining edge limit.");
                var previous = a;
                for (var index = 1; index <= count; index++)
                {
                    var t = index / (float)count;
                    var point = index == count ? d : SvgCubicTessellation.Evaluate(a, b, c, d, t);
                    Edge(previous, point);
                    previous = point;
                }
                return;
            }
            var chord = d - a;
            var length = chord.LengthSquared();
            var flat = length == 0
                ? MathF.Max(Vector2.DistanceSquared(a, b), Vector2.DistanceSquared(a, c)) <= tolerance * tolerance
                : MathF.Max(Cross(b - a, chord) * Cross(b - a, chord), Cross(c - a, chord) * Cross(c - a, chord)) <= tolerance * tolerance * length
                    && Vector2.Dot(b - a, chord) >= 0 && Vector2.Dot(b - d, chord) <= 0
                    && Vector2.Dot(c - a, chord) >= 0 && Vector2.Dot(c - d, chord) <= 0;
            if (flat)
            {
                Edge(a, d);
                return;
            }
            if (depth == 20)
                throw new InvalidDataException("SVG curve exceeds the subdivision limit at the requested tolerance.");
            var ab = (a + b) / 2;
            var bc = (b + c) / 2;
            var cd = (c + d) / 2;
            var abc = (ab + bc) / 2;
            var bcd = (bc + cd) / 2;
            var middle = (abc + bcd) / 2;
            Cubic(a, ab, abc, middle, depth + 1);
            Cubic(middle, bcd, cd, d, depth + 1);
        }
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
