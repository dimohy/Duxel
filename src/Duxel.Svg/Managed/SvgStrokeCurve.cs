using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Authored stroke curves survive length measurement and dash slicing.</summary>
internal readonly record struct SvgStrokeCurve(Vector2 Start, Vector2 Control1, Vector2 Control2, Vector2 End, bool Cubic,
    Vector3 ConicWeights = default)
{
    internal bool IsConic => ConicWeights != default;
    internal bool Curved => Cubic || IsConic;
    internal static SvgStrokeCurve Conic(Vector2 start, Vector2 control, Vector2 end, Vector3 weights) =>
        new(start, control, end, end, false, weights);
    internal static SvgStrokeCurve Line(Vector2 start, Vector2 end) => new(start, start, end, end, false);
    internal bool IsPoint => Start == End && (!Curved || Start == Control1 && Start == Control2);
    internal Vector2 Position(float t) => IsConic ? SvgConicMath.Evaluate(Start, Control1, End, ConicWeights, t)
        : Cubic ? SvgCubicTessellation.Evaluate(Start, Control1, Control2, End, t) : Vector2.Lerp(Start, End, t);

    internal Vector2 Tangent(float t)
    {
        if (IsConic)
        {
            if (t == 0) return (Control1 != Start ? Control1 : End) - Start;
            if (t == 1) return End - (Control1 != End ? Control1 : Start);
            SvgConicMath.Derivative(Start, Control1, End, ConicWeights, out var quadratic, out var linear, out var constant);
            return (quadratic * t + linear) * t + constant;
        }
        if (!Cubic) return End - Start;
        if (t == 0) return (Control1 != Start ? Control1 : Control2 != Start ? Control2 : End) - Start;
        if (t == 1) return End - (Control2 != End ? Control2 : Control1 != End ? Control1 : Start);
        var ab = Vector2.Lerp(Start, Control1, t);
        var bc = Vector2.Lerp(Control1, Control2, t);
        var cd = Vector2.Lerp(Control2, End, t);
        return Vector2.Lerp(bc, cd, t) - Vector2.Lerp(ab, bc, t);
    }

    internal (SvgStrokeCurve Left, SvgStrokeCurve Right) Split(float t)
    {
        if (IsConic)
        {
            var a = new Vector3(Start * ConicWeights.X, ConicWeights.X);
            var b = new Vector3(Control1 * ConicWeights.Y, ConicWeights.Y);
            var c = new Vector3(End * ConicWeights.Z, ConicWeights.Z);
            var firstControl = Vector3.Lerp(a, b, t);
            var lastControl = Vector3.Lerp(b, c, t);
            var conicMiddle = Vector3.Lerp(firstControl, lastControl, t);
            var conicPoint = new Vector2(conicMiddle.X, conicMiddle.Y) / conicMiddle.Z;
            return (Conic(Start, new Vector2(firstControl.X, firstControl.Y) / firstControl.Z, conicPoint,
                    new(a.Z, firstControl.Z, conicMiddle.Z)),
                Conic(conicPoint, new Vector2(lastControl.X, lastControl.Y) / lastControl.Z, End,
                    new(conicMiddle.Z, lastControl.Z, c.Z)));
        }
        if (!Cubic)
        {
            var point = Position(t);
            return (Line(Start, point), Line(point, End));
        }
        var ab = Vector2.Lerp(Start, Control1, t);
        var bc = Vector2.Lerp(Control1, Control2, t);
        var cd = Vector2.Lerp(Control2, End, t);
        var abc = Vector2.Lerp(ab, bc, t);
        var bcd = Vector2.Lerp(bc, cd, t);
        var middle = Vector2.Lerp(abc, bcd, t);
        return (new(Start, ab, abc, middle, true), new(middle, bcd, cd, End, true));
    }

    internal SvgStrokeCurve Slice(float first, float last)
    {
        if (first == 0 && last == 1) return this;
        var suffix = first == 0 ? this : Split(first).Right;
        return last == 1 ? suffix : suffix.Split((last - first) / (1 - first)).Left;
    }

    internal static Vector2 Unit(Vector2 direction)
    {
        var scale = MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
        return scale == 0 ? Vector2.Zero : Vector2.Normalize(direction / scale);
    }
}

internal readonly record struct SvgStrokeLengthEdge(Vector2 Start, Vector2 End, int CurveIndex, float First, float Last);
