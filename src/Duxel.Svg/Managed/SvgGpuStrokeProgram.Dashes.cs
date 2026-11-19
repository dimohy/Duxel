using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed partial record SvgGpuStrokeProgram
{
    private static SvgGpuStrokeProgram CompileDashes(SvgGeometry source, Matrix3x2 transform, float width,
        string cap, string join, float miterLimit, SvgDashPattern pattern, float? pathLength)
    {
        var paths = SvgStrokePath.Extract(source).Where(path => path.HasDrawing).ToArray();
        var radius = width / 2;
        var scale = (float)SvgStrokeTessellation.DeviceScale(transform);
        var primitiveEnd = 3 + paths.Length * 5;
        List<Vector4> data = new(primitiveEnd);
        for (var i = 0; i < primitiveEnd; i++)
            data.Add(default);
        var metadata = data.Count;
        data.Add(new(pattern.Lengths.Length, cap == "round" ? 1 : cap == "square" ? 2 : 0,
            join == "round" ? 1 : join == "bevel" ? 2 : 0, miterLimit));
        data.Add(new((float)pattern.Offset, pathLength ?? 0, paths.Length, 0));
        double period = 0;
        foreach (var length in pattern.Lengths)
        {
            period += length;
            data.Add(new((float)length, 0, 0, 0));
        }
        var directory = data.Count;
        for (var i = 0; i < paths.Length; i++)
            data.Add(default);
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        var difference = 0f;
        var workFactor = 1;
        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index];
            data[directory + index] = new(data.Count, 0, 0, 0);
            data.Add(new(path.Curves.Length, path.Closed ? 1 : 0, path.Origin.X, path.Origin.Y));
            var first = new Vector2(float.PositiveInfinity);
            var last = new Vector2(float.NegativeInfinity);
            double upperLength = 0;
            foreach (var curve in path.Curves)
            {
                data.Add(new(curve.Start.X, curve.Start.Y, curve.Control1.X, curve.Control1.Y));
                data.Add(new(curve.Control2.X, curve.Control2.Y, curve.End.X, curve.End.Y));
                data.Add(new(curve.IsConic ? 2 : curve.Cubic ? 1 : 0,
                    curve.ConicWeights.X, curve.ConicWeights.Y, curve.ConicWeights.Z));
                foreach (var point in new[] { curve.Start, curve.Control1, curve.Control2, curve.End })
                {
                    var mapped = Vector2.Transform(point, transform);
                    first = Vector2.Min(first, mapped);
                    last = Vector2.Max(last, mapped);
                }
                upperLength += SvgStrokePath.Distance(curve.Start, curve.Control1)
                    + SvgStrokePath.Distance(curve.Control1, curve.Control2) + SvgStrokePath.Distance(curve.Control2, curve.End);
                difference = MathF.Max(difference, MathF.Max(
                    Vector2.TransformNormal(curve.Start - 2 * curve.Control1 + curve.Control2, transform).Length(),
                    Vector2.TransformNormal(curve.Control1 - 2 * curve.Control2 + curve.End, transform).Length()));
                if (curve.IsConic)
                    difference = MathF.Max(difference, SvgConicMath.SecondDerivativeBound(
                        Vector2.TransformNormal(curve.Start, transform), Vector2.TransformNormal(curve.Control1, transform),
                        Vector2.TransformNormal(curve.End, transform), curve.ConicWeights) / 6);
            }
            if (path.Curves.Length == 0)
                first = last = Vector2.Transform(path.Origin, transform);
            var expansion = MathF.Max(cap == "square" ? MathF.Sqrt(2) : 1, join == "miter" ? miterLimit : 1);
            var padding = new Vector2(radius * scale * expansion);
            first -= padding;
            last += padding;
            minimum = Vector2.Min(minimum, first);
            maximum = Vector2.Max(maximum, last);
            var primitive = 3 + index * 5;
            data[primitive] = new(metadata, index, 0, 0);
            data[primitive + 2] = new(6, 0, 0, 0);
            data[primitive + 3] = new(first.X, first.Y, last.X, last.Y);
            // Control polygon length bounds every camera's chord length, including pathLength calibration.
            var dashes = (Math.Ceiling((pathLength ?? upperLength) / period) + 2) * (pattern.Lengths.Length / 2d);
            var factor = dashes * (Math.Max(1, path.Curves.Length) * 5d + 4);
            if (!double.IsFinite(factor) || factor > 1_000_000)
                throw new InvalidDataException("GPU SVG dash expansion exceeds its work budget.");
            workFactor = Math.Max(workFactor, (int)Math.Ceiling(factor));
        }
        data[0] = new(transform.M11, transform.M12, transform.M21, transform.M22);
        data[1] = new(transform.M31, transform.M32, radius, paths.Length);
        data[2] = new(difference, radius * scale, 1, workFactor);
        return new(data.ToArray(), paths.Length == 0 ? default : new(minimum.X, minimum.Y, maximum.X, maximum.Y));
    }
}
