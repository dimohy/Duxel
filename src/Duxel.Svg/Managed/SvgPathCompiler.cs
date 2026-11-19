using System.Numerics;

namespace Duxel.Svg.Managed;

internal static class SvgPathCompiler
{
    internal static SvgGeometry Parse(string source, bool smoothPrimitive = false)
    {
        var input = new SvgNumberReader(source);
        List<SvgSegment> segments = [];
        var point = Vector2.Zero;
        var first = Vector2.Zero;
        var control = Vector2.Zero;
        var command = '\0';
        var previous = '\0';
        while (!input.End)
        {
            if (char.IsAsciiLetter(input.Peek))
                command = input.Take();
            if (input.End && command is not ('Z' or 'z'))
                throw new InvalidDataException("An SVG path command requires its parameters.");
            if (segments.Count == 0 && command is not ('M' or 'm'))
                throw new InvalidDataException("SVG paths must start with moveto.");
            var relative = char.IsAsciiLetterLower(command);
            var kind = char.ToUpperInvariant(command);
            var start = point;
            Vector2 end;
            switch (kind)
            {
                case 'M':
                    end = Pair(ref input, start, relative);
                    segments.Add(new(SvgSegmentKind.Move, end));
                    first = point = end;
                    command = relative ? 'l' : 'L';
                    break;
                case 'L':
                    point = Pair(ref input, start, relative);
                    segments.Add(new(SvgSegmentKind.Line, point));
                    break;
                case 'H':
                    point = new(input.Number() + (relative ? start.X : 0), start.Y);
                    segments.Add(new(SvgSegmentKind.Line, point));
                    break;
                case 'V':
                    point = new(start.X, input.Number() + (relative ? start.Y : 0));
                    segments.Add(new(SvgSegmentKind.Line, point));
                    break;
                case 'C':
                case 'S':
                    var c1 = kind == 'C' ? Pair(ref input, start, relative)
                        : previous is 'C' or 'S' ? 2 * start - control : start;
                    var c2 = Pair(ref input, start, relative);
                    point = Pair(ref input, start, relative);
                    segments.Add(new(SvgSegmentKind.Cubic, point, c1, c2));
                    control = c2;
                    break;
                case 'Q':
                case 'T':
                    var quadratic = kind == 'Q' ? Pair(ref input, start, relative)
                        : previous is 'Q' or 'T' ? 2 * start - control : start;
                    point = Pair(ref input, start, relative);
                    segments.Add(new(SvgSegmentKind.Cubic, point, start + (quadratic - start) * (2f / 3), point + (quadratic - point) * (2f / 3)));
                    control = quadratic;
                    break;
                case 'A':
                    var rx = input.Number();
                    var ry = input.Number();
                    var angle = input.Number();
                    var large = input.Flag();
                    var sweep = input.Flag();
                    point = Pair(ref input, start, relative);
                    Arc(segments, start, point, rx, ry, angle, large, sweep);
                    break;
                case 'Z':
                    segments.Add(new(SvgSegmentKind.Close, first));
                    point = first;
                    command = '\0';
                    break;
                default:
                    throw new InvalidDataException("Unsupported or incomplete SVG path command: " + command);
            }
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                throw new InvalidDataException("SVG path coordinates overflowed.");
            previous = kind;
            if (segments.Count > 1_000_000)
                throw new InvalidDataException("SVG path exceeds the geometry limit.");
        }
        return new(segments.ToArray(), smoothPrimitive);
    }
    private static Vector2 Pair(ref SvgNumberReader input, Vector2 start, bool relative) =>
        new Vector2(input.Number(), input.Number()) + (relative ? start : Vector2.Zero);

    private static void Arc(List<SvgSegment> output, Vector2 start, Vector2 end, float radiusX, float radiusY,
        float degrees, bool large, bool sweep)
    {
        if (start == end)
            return;
        var rx = Math.Abs((double)radiusX);
        var ry = Math.Abs((double)radiusY);
        if (rx == 0 || ry == 0)
        {
            output.Add(new(SvgSegmentKind.Line, end));
            return;
        }
        var phi = degrees % 360 * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var dx = ((double)start.X - end.X) / 2;
        var dy = ((double)start.Y - end.Y) / 2;
        var x = cos * dx + sin * dy;
        var y = -sin * dx + cos * dy;
        var ratio = x * x / (rx * rx) + y * y / (ry * ry);
        if (ratio > 1)
        {
            var scale = Math.Sqrt(ratio);
            rx *= scale;
            ry *= scale;
        }
        var numerator = rx * rx * ry * ry - rx * rx * y * y - ry * ry * x * x;
        var denominator = rx * rx * y * y + ry * ry * x * x;
        var factor = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, numerator / denominator));
        var cx = factor * rx * y / ry;
        var cy = -factor * ry * x / rx;
        var centerX = cos * cx - sin * cy + ((double)start.X + end.X) / 2;
        var centerY = sin * cx + cos * cy + ((double)start.Y + end.Y) / 2;
        var theta = Math.Atan2((y - cy) / ry, (x - cx) / rx);
        var delta = Math.Atan2((-y - cy) / ry, (-x - cx) / rx) - theta;
        if (!sweep && delta > 0)
            delta -= 2 * Math.PI;
        if (sweep && delta < 0)
            delta += 2 * Math.PI;
        var count = (int)Math.Ceiling(Math.Abs(delta) / (2 * Math.PI / 3));
        var step = delta / count;
        Vector2 Position(double t) => new((float)(centerX + cos * rx * Math.Cos(t) - sin * ry * Math.Sin(t)),
            (float)(centerY + sin * rx * Math.Cos(t) + cos * ry * Math.Sin(t)));
        var weight = (float)Math.Cos(step / 2);
        for (var i = 0; i < count; i++)
        {
            var next = theta + step;
            var middle = theta + step / 2;
            var control = new Vector2((float)(centerX + (cos * rx * Math.Cos(middle) - sin * ry * Math.Sin(middle)) / weight),
                (float)(centerY + (sin * rx * Math.Cos(middle) + cos * ry * Math.Sin(middle)) / weight));
            output.Add(new(SvgSegmentKind.Conic, i == count - 1 ? end : Position(next), control, Weight: weight));
            theta = next;
        }
    }
}
