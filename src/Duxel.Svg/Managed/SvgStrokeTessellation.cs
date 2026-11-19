using System.Numerics;

namespace Duxel.Svg.Managed;

internal readonly record struct SvgStrokeSection(Vector2 Point, Vector2 Tangent);

/// <summary>Combines positional and angular subdivisions before offsetting analytic curve tangents.</summary>
internal static class SvgStrokeTessellation
{
    internal static double DeviceScale(Matrix3x2 transform)
    {
        var a = (double)transform.M11 * transform.M11 + (double)transform.M12 * transform.M12;
        var b = (double)transform.M21 * transform.M21 + (double)transform.M22 * transform.M22;
        var c = (double)transform.M11 * transform.M21 + (double)transform.M12 * transform.M22;
        var scale = Math.Sqrt((a + b + Math.Sqrt((a - b) * (a - b) + 4 * c * c)) / 2);
        if (!double.IsFinite(scale) || scale <= 0)
            throw new InvalidDataException("SVG stroke requires a finite nonsingular device mapping.");
        return scale;
    }

    internal static SvgStrokeSection[][] Sections(SvgStrokeCurve curve, Matrix3x2 transform, float radius)
    {
        if (!curve.Curved)
        {
            var tangent = SvgStrokeCurve.Unit(curve.End - curve.Start);
            return [[new(curve.Start, tangent), new(curve.End, tangent)]];
        }
        return Patches(curve).Select(patch => Patch(patch, transform, radius)).ToArray();
    }

    internal static SvgStrokeCurve[] Patches(SvgStrokeCurve curve)
    {
        if (!curve.Curved)
            return [curve];
        List<SvgStrokeCurve> result = [];
        var remaining = curve;
        var previous = 0f;
        foreach (var cut in Chops(curve))
        {
            var split = remaining.Split((cut - previous) / (1 - previous));
            result.Add(split.Left);
            remaining = split.Right;
            previous = cut;
        }
        result.Add(remaining);
        return result.ToArray();
    }

    internal static SvgStrokeSection[] PointSections(Vector2 center, Matrix3x2 transform, float radius)
    {
        var count = RadialCount(Math.PI, radius, transform);
        var result = new SvgStrokeSection[count + 1];
        for (var index = 0; index <= count; index++)
        {
            var theta = Math.PI * index / count;
            var tangent = index == 0 ? Vector2.UnitX : index == count ? -Vector2.UnitX
                : new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
            result[index] = new(center, tangent);
        }
        return result;
    }

    internal static Vector2[] ArcNormals(Vector2 start, Vector2 end, double rotation, Matrix3x2 transform, float radius)
    {
        var count = RadialCount(rotation, radius, transform);
        var normals = new Vector2[count + 1];
        var angle = Math.Atan2(start.Y, start.X);
        normals[0] = start;
        normals[^1] = end;
        for (var index = 1; index < count; index++)
        {
            var theta = angle + rotation * index / count;
            normals[index] = new((float)Math.Cos(theta), (float)Math.Sin(theta));
        }
        return normals;
    }

    private static SvgStrokeSection[] Patch(SvgStrokeCurve curve, Matrix3x2 transform, float radius)
    {
        var first = Vector2.TransformNormal(curve.Start - 2 * curve.Control1 + curve.Control2, transform);
        var second = Vector2.TransformNormal(curve.Control1 - 2 * curve.Control2 + curve.End, transform);
        var positional = Math.Ceiling(Math.Sqrt(3 * Math.Max(first.Length(), second.Length())));
        if (curve.IsConic)
            positional = Math.Ceiling(Math.Sqrt(SvgConicMath.SecondDerivativeBound(
                Vector2.TransformNormal(curve.Start, transform), Vector2.TransformNormal(curve.Control1, transform),
                Vector2.TransformNormal(curve.End, transform), curve.ConicWeights) / 2));
        Coefficients(curve, out var a, out var b, out var c);
        var start = SvgStrokeCurve.Unit(curve.Tangent(0));
        var end = SvgStrokeCurve.Unit(curve.Tangent(1));
        var rotation = Math.Atan2(Cross(start, end), Vector2.Dot(start, end));
        var turn = Cross(curve.Control2 - curve.Start, curve.End - curve.Control1);
        if (turn > 0 && rotation < 0)
            rotation += 2 * Math.PI;
        if (turn < 0 && rotation > 0)
            rotation -= 2 * Math.PI;
        var angular = RadialCount(rotation, radius, transform);
        if (!double.IsFinite(positional) || positional + angular > 1_000_000)
            throw new InvalidDataException("SVG stroke tessellation exceeds its section limit.");
        var count = Math.Max(1, (int)positional);
        var radialCount = angular;
        List<(float T, Vector2 Tangent)> samples = new(count + radialCount + 1);
        for (var index = 0; index <= count; index++)
        {
            var t = index / (float)count;
            samples.Add((t, SvgStrokeCurve.Unit(curve.Tangent(t))));
        }
        var startAngle = Math.Atan2(start.Y, start.X);
        for (var index = 1; index < radialCount; index++)
        {
            var theta = startAngle + rotation * index / radialCount;
            var tangent = new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
            var normal = new Vector2(-tangent.Y, tangent.X);
            var roots = Roots(Vector2.Dot(normal, a), 2d * Vector2.Dot(normal, b), Vector2.Dot(normal, c));
            foreach (var root in roots)
            {
                if (root <= 0 || root >= 1)
                    continue;
                var t = (float)root;
                if (Vector2.Dot(SvgStrokeCurve.Unit(curve.Tangent(t)), tangent) <= 0)
                    continue;
                samples.Add((t, tangent));
                break;
            }
        }
        samples.Sort(static (left, right) => left.T.CompareTo(right.T));
        return samples.Select(sample => new SvgStrokeSection(curve.Position(sample.T), sample.Tangent)).ToArray();
    }

    private static float[] Chops(SvgStrokeCurve curve)
    {
        Coefficients(curve, out var a, out var b, out var c);
        var qa = Cross(a, b);
        var qb = Cross(a, c);
        var qc = Cross(b, c);
        List<double> roots = [];
        if (qa == 0 && qb == 0 && qc == 0)
        {
            // Collinear cubics can reverse at cusps without a nonzero inflection polynomial.
            var x = MathF.Max(MathF.Abs(a.X), MathF.Max(MathF.Abs(b.X), MathF.Abs(c.X)));
            var y = MathF.Max(MathF.Abs(a.Y), MathF.Max(MathF.Abs(b.Y), MathF.Abs(c.Y)));
            roots.AddRange(x >= y ? Roots(a.X, 2d * b.X, c.X) : Roots(a.Y, 2d * b.Y, c.Y));
        }
        else if (qb * qb - 4 * qa * qc < 0)
        {
            // A convex curve can still turn through more than a half revolution.
            if (qb != 0)
                roots.Add(-2 * qc / qb);
        }
        else
            roots.AddRange(Roots(qa, qb, qc));
        const float endpointGuard = 1f / 2048;
        return roots.Where(value => value >= endpointGuard && value < 1 - endpointGuard)
            .Select(value => (float)value).Distinct().Order().ToArray();
    }

    private static void Coefficients(SvgStrokeCurve curve, out Vector2 a, out Vector2 b, out Vector2 c)
    {
        if (curve.IsConic)
        {
            SvgConicMath.Derivative(curve.Start, curve.Control1, curve.End, curve.ConicWeights, out a, out b, out c);
            b *= .5f;
            return;
        }
        c = curve.Control1 - curve.Start;
        var middle = curve.Control2 - curve.Control1;
        b = middle - c;
        a = curve.End - curve.Start - 3 * middle;
    }

    private static int RadialCount(double rotation, float radius, Matrix3x2 transform)
    {
        // The asin form retains precision when the device-space radius is large.
        var angle = 2 * Math.Asin(Math.Sqrt(Math.Min(.25 / (radius * DeviceScale(transform)), 2) / 2));
        var count = Math.Max(1, Math.Ceiling(Math.Abs(rotation) * .5 / angle));
        if (!double.IsFinite(count) || count > 1_000_000)
            throw new InvalidDataException("SVG stroke rotation exceeds its section limit.");
        return (int)count;
    }

    private static double Cross(Vector2 a, Vector2 b) => (double)a.X * b.Y - (double)a.Y * b.X;

    private static double[] Roots(double a, double b, double c)
    {
        if (a == 0)
            return b == 0 ? [] : [-c / b];
        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
            return [];
        var q = -.5 * (b + Math.CopySign(Math.Sqrt(discriminant), b));
        if (q == 0)
            return [-b / (2 * a)];
        return [q / a, c / q];
    }
}
