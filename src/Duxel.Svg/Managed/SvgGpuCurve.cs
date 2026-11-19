using System.Numerics;
using Duxel.Core;

namespace Duxel.Svg.Managed;

/// <summary>Retains cubic controls and monotone intervals. Subdivision depends on the GPU camera, not XML compilation.</summary>
internal readonly record struct SvgGpuCurve(Vector2 Start, Vector2 Control1, Vector2 Control2, Vector2 End, Vector4 Intervals)
{
    internal Vector2 Minimum => Vector2.Min(Vector2.Min(Start, End), Vector2.Min(Control1, Control2));
    internal Vector2 Maximum => Vector2.Max(Vector2.Max(Start, End), Vector2.Max(Control1, Control2));

    internal static SvgGpuCurve[] Compile(SvgGeometry geometry, Matrix3x2 transform)
    {
        List<SvgGpuCurve> result = [];
        var point = Vector2.Zero;
        var first = point;
        var contour = false;
        void Line(Vector2 end)
        {
            if (point != end)
                result.Add(new(point, point, end, end, default));
            point = end;
        }
        void Close()
        {
            if (contour)
                Line(first);
            contour = false;
        }
        foreach (var segment in geometry.Segments)
        {
            var end = Vector2.Transform(segment.End, transform);
            switch (segment.Kind)
            {
                case SvgSegmentKind.Move:
                    Close();
                    point = first = end;
                    contour = true;
                    break;
                case SvgSegmentKind.Close:
                    Close();
                    break;
                case SvgSegmentKind.Line:
                    contour = true;
                    Line(end);
                    break;
                case SvgSegmentKind.Cubic:
                    contour = true;
                    var b = Vector2.Transform(segment.Control1, transform);
                    var c = Vector2.Transform(segment.Control2, transform);
                    var extrema = UiCubicBezierMath.YExtrema(point.Y, b.Y, c.Y, end.Y);
                    var roots = new Vector4(1, extrema.First, extrema.Second, extrema.Count);
                    result.Add(new(point, b, c, end, roots));
                    point = end;
                    break;
                case SvgSegmentKind.Conic:
                    contour = true;
                    var control = Vector2.Transform(segment.Control1, transform);
                    var conicExtrema = SvgConicMath.Extrema(point.Y, control.Y, end.Y, new(1, segment.Weight, 1));
                    result.Add(new(point, control, end, end,
                        new(-segment.Weight, conicExtrema.First, conicExtrema.Second, conicExtrema.Count)));
                    point = end;
                    break;
            }
        }
        Close();
        return result.ToArray();
    }
}
