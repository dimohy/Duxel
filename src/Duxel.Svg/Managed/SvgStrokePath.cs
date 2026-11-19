using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Separates user-space length measurement from device-space stroke tessellation.</summary>
internal sealed record SvgStrokePath(SvgStrokeCurve[] Curves, SvgStrokeLengthEdge[] Edges, float[] Distances, Vector2 Origin,
    bool Closed, bool HasDrawing, float Tolerance, Vector2 DotDirection = default)
{
    internal double Length => Distances[^1];
    internal static double Distance(Vector2 a, Vector2 b)
    {
        var x = (double)b.X - a.X;
        var y = (double)b.Y - a.Y;
        return Math.Sqrt(x * x + y * y);
    }
    internal static SvgStrokePath Create(SvgStrokeCurve[] curves, Vector2 origin, bool closed, bool hasDrawing,
        float tolerance, Vector2 dotDirection = default)
    {
        List<SvgStrokeLengthEdge> edges = [];
        for (var index = 0; index < curves.Length; index++)
            Flatten(curves[index], index, 0, 1, 0);
        var distances = new float[edges.Count + 1];
        for (var index = 0; index < edges.Count; index++)
        {
            distances[index + 1] = distances[index] + (float)Distance(edges[index].Start, edges[index].End);
            if (!float.IsFinite(distances[index + 1]) || distances[index + 1] <= distances[index])
                throw new InvalidDataException("SVG stroke length exceeds the measurable numeric range.");
        }
        return new(curves, edges.ToArray(), distances, origin, closed, hasDrawing, tolerance, dotDirection);

        void Flatten(SvgStrokeCurve curve, int index, float first, float last, int depth)
        {
            // Length interpolation needs parametric flatness, including collinear reversals.
            var firstError = Vector2.Abs(curve.Control1 - Vector2.Lerp(curve.Start, curve.End, 1f / 3));
            var secondError = Vector2.Abs(curve.Control2 - Vector2.Lerp(curve.Start, curve.End, 2f / 3));
            var error = Vector2.Max(firstError, secondError);
            if (curve.IsConic)
                error = new(SvgConicMath.SecondDerivativeBound(curve.Start, curve.Control1, curve.End, curve.ConicWeights) / 8);
            var flat = !curve.Curved || MathF.Max(error.X, error.Y) <= tolerance;
            if (flat)
            {
                if (curve.Start != curve.End)
                {
                    if (edges.Count == 1_000_000) throw new InvalidDataException("SVG stroke exceeds the length edge limit.");
                    edges.Add(new(curve.Start, curve.End, index, first, last));
                }
                return;
            }
            if (depth == 20) throw new InvalidDataException("SVG stroke length exceeds the subdivision limit.");
            var split = curve.Split(.5f);
            var middle = (first + last) / 2;
            Flatten(split.Left, index, first, middle, depth + 1);
            Flatten(split.Right, index, middle, last, depth + 1);
        }
    }

    internal static SvgStrokePath[] Extract(SvgGeometry source, float tolerance = .5f)
    {
        List<SvgStrokePath> paths = [];
        List<SvgStrokeCurve> curves = [];
        var point = Vector2.Zero;
        var origin = Vector2.Zero;
        var contour = false;
        var totalEdges = 0;
        void Finish(bool closed)
        {
            if (!contour) return;
            var path = Create(curves.ToArray(), origin, closed, curves.Count != 0 || closed, tolerance);
            totalEdges = checked(totalEdges + path.Edges.Length);
            if (totalEdges > 1_000_000) throw new InvalidDataException("SVG stroke exceeds the length edge limit.");
            paths.Add(path);
            curves.Clear();
            contour = false;
        }
        foreach (var segment in source.Segments)
        {
            if (segment.Kind == SvgSegmentKind.Move)
            {
                Finish(false);
                origin = point = segment.End;
                contour = true;
            }
            else if (segment.Kind == SvgSegmentKind.Close)
            {
                if (point != origin) curves.Add(SvgStrokeCurve.Line(point, origin));
                point = origin;
                Finish(true);
            }
            else
            {
                if (!contour) { origin = point; contour = true; }
                curves.Add(segment.Kind == SvgSegmentKind.Conic
                    ? SvgStrokeCurve.Conic(point, segment.Control1, segment.End, new(1, segment.Weight, 1))
                    : segment.Kind == SvgSegmentKind.Cubic
                    ? new(point, segment.Control1, segment.Control2, segment.End, true)
                    : SvgStrokeCurve.Line(point, segment.End));
                point = segment.End;
            }
        }
        Finish(false);
        return paths.ToArray();
    }
}
