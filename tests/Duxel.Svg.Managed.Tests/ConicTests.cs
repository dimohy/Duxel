using System.Numerics;
using Duxel.Svg.Managed;

internal static class ConicTests
{
    internal static void Run(Action<bool, string> check)
    {
        var weight = MathF.Sqrt(.5f);
        var quarter = SvgStrokeCurve.Conic(new(1, 0), new(1, 1), new(0, 1), new(1, weight, 1));
        check(Vector2.Distance(quarter.Position(.5f), new(weight, weight)) < 1e-6f,
            "Conic / rational quarter circle retains its exact circular midpoint");
        var split = quarter.Split(.37f);
        check(Enumerable.Range(0, 9).All(index =>
        {
            var t = index / 8f;
            return Vector2.Distance(split.Left.Position(t), quarter.Position(t * .37f)) < 2e-6f
                && Vector2.Distance(split.Right.Position(t), quarter.Position(.37f + t * .63f)) < 2e-6f;
        }), "Conic / homogeneous splitting preserves original parameters and unequal endpoint weights");
        var slice = quarter.Slice(.2f, .8f);
        check(Vector2.Distance(slice.Position(.5f), quarter.Position(.5f)) < 2e-6f
            && Vector2.Dot(SvgStrokeCurve.Unit(slice.Tangent(.5f)), new(-weight, weight)) > .99999f,
            "Conic / dash slicing preserves position and analytic tangent direction");
        var rotation = Matrix3x2.CreateRotation(MathF.PI / 4);
        var geometry = new SvgGeometry([
            new(SvgSegmentKind.Move, Vector2.Transform(quarter.Start, rotation)),
            new(SvgSegmentKind.Conic, Vector2.Transform(quarter.End, rotation),
                Vector2.Transform(quarter.Control1, rotation), Weight: weight)
        ]);
        check(MathF.Abs(geometry.Bounds.Y + geometry.Bounds.W - 1) < 2e-6f,
            "Conic / rational extrema bound a rotated arc instead of its control hull");
        var path = SvgPathCompiler.Parse("M58 32A26 26 0 1 1 6 32A26 26 0 1 1 58 32Z");
        check(path.Segments.ToArray().Count(segment => segment.Kind == SvgSegmentKind.Conic) == 4
            && path.Segments.ToArray().All(segment => segment.Kind != SvgSegmentKind.Cubic),
            "Conic / authored elliptical arcs remain rational through normalization");
        var curves = SvgStrokePath.Extract(path)[0].Curves;
        check(curves.All(curve => curve.IsConic) && curves.All(curve => float.IsFinite(curve.Position(.4f).X)),
            "Conic / stroke length and dash preparation retain every authored arc");
        var gpu = SvgGpuCurve.Compile(path, Matrix3x2.Identity);
        check(gpu.All(curve => curve.Intervals.X < 0) && gpu.Length == 4,
            "Conic / retained GPU records preserve weights without CPU flattening");
        var invalidRejected = false;
        try
        {
            _ = new SvgGeometry([new(SvgSegmentKind.Move, Vector2.Zero),
                new(SvgSegmentKind.Conic, Vector2.One, Vector2.UnitX, Weight: -1)]);
        }
        catch (InvalidDataException) { invalidRejected = true; }
        check(invalidRejected, "Conic / nonpositive weights reject before bounds or device work");
    }
}
