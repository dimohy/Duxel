using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Local curves, joins and caps. Only camera-dependent sections are generated on the device.</summary>
internal sealed partial record SvgGpuStrokeProgram(Vector4[] Data, Vector4 Bounds, SvgRectangularStroke? Rectangle = null)
{
    internal bool IsRectangle
    {
        get
        {
            if (Rectangle is not null)
                return false;
            if (Data[1].W != 1 || Data[5].X != 5)
                return false;
            var m = Data[0];
            var p = Data[1];
            var transform = new Matrix3x2(m.X, m.Y, m.Z, m.W, p.X, p.Y);
            var first = Data[3];
            var second = Data[4];
            Vector2[] vertices = [new(first.X, first.Y), new(first.Z, first.W), new(second.X, second.Y), new(second.Z, second.W)];
            for (var i = 0; i < vertices.Length; i++)
                vertices[i] = Vector2.Transform(vertices[i], transform);
            for (var i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % 4];
                var c = vertices[(i + 2) % 4];
                if (a == b || a.X != b.X && a.Y != b.Y || (a.X == b.X) == (b.X == c.X))
                    return false;
            }
            return true;
        }
    }

    internal static SvgGpuStrokeProgram Compile(SvgGeometry source, Matrix3x2 transform, float width,
        string cap, string join, float miterLimit, SvgDashPattern? pattern, float? pathLength)
    {
        if (width == 0)
            return new([default, default, default], default);
        if (SvgRectangularStroke.Create(source, width, join, miterLimit, pattern)?.Map(transform) is { } rectangle)
        {
            var outer = rectangle.Outer;
            return new([], new(outer.X, outer.Y, outer.X + outer.Z, outer.Y + outer.W), rectangle);
        }
        if (pattern is not null)
            return CompileDashes(source, transform, width, cap, join, miterLimit, pattern, pathLength);
        var radius = width / 2;
        var deviceScale = (float)SvgStrokeTessellation.DeviceScale(transform);
        List<Vector4> data = [new(transform.M11, transform.M12, transform.M21, transform.M22),
            new(transform.M31, transform.M32, radius, 0), default];
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        var maximumDifference = 0f;
        var paths = SvgStrokePath.Extract(source, .5f / deviceScale);
        void Piece(int kind, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float rotation = 0, Vector4 tangents = default)
        {
            var localMin = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            var localMax = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            if (kind is 2 or 3)
                localMin = localMax = a;
            var first = new Vector2(float.PositiveInfinity);
            var last = new Vector2(float.NegativeInfinity);
            foreach (var point in new[] { localMin, new(localMax.X, localMin.Y), localMax, new(localMin.X, localMax.Y) })
            {
                var mapped = Vector2.Transform(point, transform);
                first = Vector2.Min(first, mapped);
                last = Vector2.Max(last, mapped);
            }
            var padding = kind <= 3 || kind == 7 ? radius * deviceScale : 0;
            first -= new Vector2(padding);
            last += new Vector2(padding);
            minimum = Vector2.Min(minimum, first);
            maximum = Vector2.Max(maximum, last);
            if (kind == 0)
                maximumDifference = MathF.Max(maximumDifference, MathF.Max(
                    Vector2.TransformNormal(a - 2 * b + c, transform).Length(),
                    Vector2.TransformNormal(b - 2 * c + d, transform).Length()));
            if (kind == 7)
                maximumDifference = MathF.Max(maximumDifference, SvgConicMath.SecondDerivativeBound(
                    Vector2.TransformNormal(a, transform), Vector2.TransformNormal(b, transform),
                    Vector2.TransformNormal(d, transform), new(tangents.X, tangents.Y, tangents.Z)) / 6);
            data.Add(new(a.X, a.Y, b.X, b.Y));
            data.Add(new(c.X, c.Y, d.X, d.Y));
            data.Add(new(kind, rotation, 0, 0));
            data.Add(new(first.X, first.Y, last.X, last.Y));
            data.Add(tangents);
        }
        void Polygon(params Vector2[] points)
        {
            double area = 0;
            for (var i = 1; i < points.Length - 1; i++)
                area += ((double)points[i].X - points[0].X) * ((double)points[i + 1].Y - points[0].Y)
                    - ((double)points[i].Y - points[0].Y) * ((double)points[i + 1].X - points[0].X);
            if (area < 0)
                Array.Reverse(points);
            Piece(points.Length == 3 ? 4 : 5, points[0], points[1], points[2], points[^1]);
        }
        void Circle(Vector2 center) => Piece(2, center, default, default, default);
        void Square(Vector2 center, Vector2 direction)
        {
            var normal = new Vector2(-direction.Y, direction.X) * radius;
            var end = center + direction * radius;
            Polygon(center + normal, end + normal, end - normal, center - normal);
        }
        foreach (var path in paths)
        {
            if (!path.HasDrawing || radius == 0)
                continue;
            var curves = path.Curves.Where(curve => !curve.IsPoint).ToArray();
            if (curves.Length == 0)
            {
                if (cap == "round")
                    Circle(path.Origin);
                else if (cap == "square")
                {
                    var tangent = (path.DotDirection == default ? Vector2.UnitX : path.DotDirection) * radius;
                    var normal = new Vector2(-tangent.Y, tangent.X);
                    Polygon(path.Origin - tangent + normal, path.Origin + tangent + normal,
                        path.Origin + tangent - normal, path.Origin - tangent - normal);
                }
                continue;
            }
            if (!path.Closed && cap == "square")
            {
                Square(curves[0].Start, -SvgStrokeCurve.Unit(curves[0].Tangent(0)));
                Square(curves[^1].End, SvgStrokeCurve.Unit(curves[^1].Tangent(1)));
            }
            for (var index = 0; index < curves.Length; index++)
            {
                var curve = curves[index];
                var patches = SvgStrokeTessellation.Patches(curve);
                Vector2 previous = default;
                for (var patchIndex = 0; patchIndex < patches.Length; patchIndex++)
                {
                    var patch = patches[patchIndex];
                    var start = SvgStrokeCurve.Unit(patch.Tangent(0));
                    var end = SvgStrokeCurve.Unit(patch.Tangent(1));
                    if (patchIndex > 0 && Vector2.Dot(previous, start) < -.999f)
                        Circle(patch.Start);
                    previous = end;
                    var rotation = Math.Atan2((double)start.X * end.Y - (double)start.Y * end.X, Vector2.Dot(start, end));
                    var first = patch.Control2 - patch.Start;
                    var second = patch.End - patch.Control1;
                    var turn = (double)first.X * second.Y - (double)first.Y * second.X;
                    if (turn > 0 && rotation < 0)
                        rotation += 2 * Math.PI;
                    if (turn < 0 && rotation > 0)
                        rotation -= 2 * Math.PI;
                    Piece(patch.IsConic ? 7 : patch.Cubic ? 0 : 1, patch.Start, patch.Control1, patch.Control2, patch.End,
                        (float)rotation, patch.IsConic ? new(patch.ConicWeights, 0) : new(start.X, start.Y, end.X, end.Y));
                }
                var direction = SvgStrokeCurve.Unit(curve.Tangent(1));
                if (index < curves.Length - 1 || path.Closed)
                {
                    var next = curves[(index + 1) % curves.Length];
                    var nextDirection = SvgStrokeCurve.Unit(next.Tangent(0));
                    var cross = direction.X * nextDirection.Y - direction.Y * nextDirection.X;
                    var dot = Math.Clamp(Vector2.Dot(direction, nextDirection), -1, 1);
                    if (join == "round")
                    {
                        if (cross == 0)
                        {
                            if (dot < 0)
                                Circle(curve.End);
                        }
                        else
                        {
                            var sign = cross > 0 ? -1 : 1;
                            var a = new Vector2(-direction.Y, direction.X) * sign;
                            var b = new Vector2(-nextDirection.Y, nextDirection.X) * sign;
                            Piece(3, curve.End, a, b, default, (float)Math.CopySign(Math.Acos(dot), cross));
                        }
                    }
                    else if (MathF.Abs(cross) > .00001f)
                    {
                        var sign = cross > 0 ? -1 : 1;
                        var a = new Vector2(-direction.Y, direction.X) * (radius * sign);
                        var b = new Vector2(-nextDirection.Y, nextDirection.X) * (radius * sign);
                        var bisector = a + b;
                        var intersection = bisector * (radius * radius / Vector2.Dot(bisector, a));
                        if (join == "miter" && intersection.Length() <= radius * miterLimit)
                            Polygon(curve.End + a, curve.End + intersection, curve.End + b, curve.End);
                        else
                            Polygon(curve.End + a, curve.End, curve.End + b);
                    }
                }
            }
            if (!path.Closed && cap == "round")
            {
                Circle(curves[0].Start);
                Circle(curves[^1].End);
            }
        }
        var count = (data.Count - 3) / 5;
        data[1] = new(transform.M31, transform.M32, radius, count);
        data[2] = new(maximumDifference, radius * deviceScale, 0, 0);
        return new(data.ToArray(), count == 0 ? default : new(minimum.X, minimum.Y, maximum.X, maximum.Y));
    }
}
