using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Positive-weight rational quadratics shared by retained fills, bounds and stroke slicing.</summary>
internal static class SvgConicMath
{
    internal static Vector2 Evaluate(Vector2 a, Vector2 b, Vector2 c, Vector3 weights, float t)
    {
        var first = new Vector3(a * weights.X, weights.X);
        var middle = new Vector3(b * weights.Y, weights.Y);
        var last = new Vector3(c * weights.Z, weights.Z);
        var value = Vector3.Lerp(Vector3.Lerp(first, middle, t), Vector3.Lerp(middle, last, t), t);
        return new(value.X / value.Z, value.Y / value.Z);
    }

    internal static void Derivative(Vector2 a, Vector2 b, Vector2 c, Vector3 weights,
        out Vector2 quadratic, out Vector2 linear, out Vector2 constant)
    {
        var n0 = a * weights.X;
        var n1 = 2 * (b * weights.Y - n0);
        var n2 = n0 - 2 * b * weights.Y + c * weights.Z;
        var d1 = 2 * (weights.Y - weights.X);
        var d2 = weights.X - 2 * weights.Y + weights.Z;
        constant = n1 * weights.X - n0 * d1;
        linear = 2 * (n2 * weights.X - n0 * d2);
        quadratic = n2 * d1 - n1 * d2;
    }

    internal static (float First, float Second, int Count) Extrema(float a, float b, float c, Vector3 weights)
    {
        var n0 = (double)a * weights.X;
        var n1 = 2 * ((double)b * weights.Y - n0);
        var n2 = n0 - 2 * (double)b * weights.Y + (double)c * weights.Z;
        var d1 = 2d * (weights.Y - weights.X);
        var d2 = (double)weights.X - 2 * weights.Y + weights.Z;
        var qa = n2 * d1 - n1 * d2;
        var qb = 2 * (n2 * weights.X - n0 * d2);
        var qc = n1 * weights.X - n0 * d1;
        var first = 0f;
        var second = 0f;
        var count = 0;
        void Include(double t)
        {
            if (t > 0 && t < 1)
            {
                if (count == 0) first = (float)t;
                else if (first != (float)t) second = (float)t;
                else return;
                count++;
            }
        }
        if (qa == 0)
        {
            if (qb != 0) Include(-qc / qb);
        }
        else
        {
            var discriminant = qb * qb - 4 * qa * qc;
            if (discriminant >= 0)
            {
                var q = -.5 * (qb + Math.CopySign(Math.Sqrt(discriminant), qb));
                if (q == 0) Include(-qb / (2 * qa));
                else { Include(q / qa); Include(qc / q); }
            }
        }
        if (count == 2 && first > second) (first, second) = (second, first);
        return (count == 0 ? 0 : first, count < 2 ? 0 : second, count);
    }

    internal static int SegmentCount(Vector2 a, Vector2 b, Vector2 c, float weight, float tolerance)
    {
        var center = (Vector2.Min(Vector2.Min(a, b), c) + Vector2.Max(Vector2.Max(a, b), c)) * .5f;
        a -= center;
        b -= center;
        c -= center;
        var magnitude = MathF.Sqrt(MathF.Max(MathF.Max(a.LengthSquared(), b.LengthSquared()), c.LengthSquared()));
        var difference = a - 2 * weight * b + c;
        var numerator = difference.Length() / tolerance + MathF.Max(0, magnitude / tolerance - 1) * MathF.Abs(2 - 2 * weight);
        var required = Math.Sqrt(numerator / (4 * MathF.Min(weight, 1)));
        if (!double.IsFinite(required) || required > 524_288)
            throw new InvalidDataException("SVG conic tessellation exceeds the edge limit.");
        var count = 1;
        while (count < required)
            count *= 2;
        return count;
    }

    internal static float SecondDerivativeBound(Vector2 a, Vector2 b, Vector2 c, Vector3 weights)
    {
        var center = (Vector2.Min(Vector2.Min(a, b), c) + Vector2.Max(Vector2.Max(a, b), c)) * .5f;
        a -= center;
        b -= center;
        c -= center;
        var minimum = MathF.Min(weights.X, MathF.Min(weights.Y, weights.Z));
        var maximum = MathF.Max(weights.X, MathF.Max(weights.Y, weights.Z));
        var magnitude = MathF.Max(a.Length(), MathF.Max(b.Length(), c.Length())) * maximum;
        var n0 = a * weights.X;
        var n1 = b * weights.Y;
        var n2 = c * weights.Z;
        var first = 2 * MathF.Max((n1 - n0).Length(), (n2 - n1).Length());
        var second = 2 * (n0 - 2 * n1 + n2).Length();
        var d1 = 2 * MathF.Max(MathF.Abs(weights.Y - weights.X), MathF.Abs(weights.Z - weights.Y));
        var d2 = 2 * MathF.Abs(weights.X - 2 * weights.Y + weights.Z);
        return second / minimum + (magnitude * d2 + 2 * first * d1) / (minimum * minimum)
            + 2 * magnitude * d1 * d1 / (minimum * minimum * minimum);
    }
}
