using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Resolved dash style shared by the scalar renderer and retained GPU dash programs.</summary>
internal sealed class SvgDashPattern(double[] lengths, double offset)
{
    internal ReadOnlySpan<double> Lengths => lengths;
    internal double Offset => offset;
    internal static SvgDashPattern? Parse(string array, string offset, float viewportWidth, float viewportHeight)
    {
        var diagonal = Math.Sqrt(((double)viewportWidth * viewportWidth + (double)viewportHeight * viewportHeight) / 2);
        double Length(ref SvgNumberReader reader)
        {
            var number = reader.Number();
            if (reader.Peek == '%')
            {
                reader.Take();
                return number * diagonal / 100;
            }
            if (reader.Peek == 'p')
            {
                reader.Take();
                if (reader.Take() != 'x')
                    throw new NotSupportedException("Unsupported SVG dash unit.");
            }
            else if (char.IsAsciiLetter(reader.Peek))
                throw new NotSupportedException("Unsupported SVG dash unit.");
            return number;
        }
        var offsetReader = new SvgNumberReader(offset.Trim());
        var phase = Length(ref offsetReader);
        if (!offsetReader.End)
            throw new InvalidDataException("SVG dash offset requires one length.");
        if (array.Trim() == "none")
            return null;
        var input = new SvgNumberReader(array);
        List<double> values = [];
        while (!input.End)
        {
            var value = Length(ref input);
            if (value < 0)
                throw new InvalidDataException("SVG dash lengths cannot be negative.");
            values.Add(value);
            if (values.Count > 65_536)
                throw new InvalidDataException("SVG dash array exceeds its entry limit.");
        }
        if (values.Count == 0)
            throw new InvalidDataException("SVG dash array requires lengths or none.");
        if (values.All(value => value == 0))
            return null;
        if (values.Count % 2 != 0)
            values.AddRange(values.ToArray());
        return new(values.ToArray(), phase);
    }

    internal SvgStrokePath[] Apply(SvgStrokePath[] paths, float? authorLength)
    {
        var scale = 1d;
        if (authorLength is { } calibrated)
        {
            if (!float.IsFinite(calibrated) || calibrated < 0)
                throw new InvalidDataException("SVG pathLength must be finite and nonnegative.");
            if (calibrated == 0)
                throw new NotSupportedException("Zero SVG pathLength calibration is not implemented.");
            scale = paths.Sum(path => path.Length) / calibrated;
        }
        if (scale == 0)
            scale = 1;
        var pattern = lengths.Select(length => length * scale).ToArray();
        var sum = pattern.Sum();
        var phase = (offset * scale % sum + sum) % sum;
        List<SvgStrokePath> output = [];
        foreach (var path in paths)
        {
            if (!path.HasDrawing)
                continue;
            var total = path.Length;
            if (total / sum * pattern.Length > 1_000_000)
                throw new InvalidDataException("SVG dash expansion exceeds its segment budget.");
            var cumulative = path.Distances;
            var index = 0;
            var skipped = phase;
            while (skipped > pattern[index])
            {
                skipped -= pattern[index];
                index = (index + 1) % pattern.Length;
            }
            var remaining = pattern[index] - skipped;
            var position = 0d;
            var first = output.Count;
            var firstAtOrigin = false;
            var lastAtEnd = false;
            var steps = 0;
            do
            {
                if (++steps > 1_000_000)
                    throw new InvalidDataException("SVG dash expansion exceeds its segment limit.");
                var end = position + Math.Min(remaining, total - position);
                if (end == position && remaining > 0 && total > position)
                    throw new InvalidDataException("SVG dash lengths are too small for this path's numeric range.");
                if ((index & 1) == 0)
                {
                    var dash = Slice(path, cumulative, position, end);
                    if (output.Count == 1_000_000)
                        throw new InvalidDataException("SVG dash expansion exceeds its path limit.");
                    output.Add(dash);
                    if (output.Count == first + 1)
                        firstAtOrigin = position == 0;
                    lastAtEnd = end == total;
                }
                position = end;
                index = (index + 1) % pattern.Length;
                remaining = pattern[index];
            } while (position < total);

            if (path.Closed && firstAtOrigin && lastAtEnd)
            {
                if (output.Count == first + 1)
                    output[first] = output[first] with
                    {
                        Closed = true
                    };
                else if (output[first].Edges.Length != 0 && output[^1].Edges.Length != 0)
                {
                    var start = output[first];
                    var finish = output[^1];
                    output[first] = SvgStrokePath.Create(finish.Curves.Concat(start.Curves).ToArray(), finish.Origin,
                        false, true, path.Tolerance, finish.DotDirection);
                    output.RemoveAt(output.Count - 1);
                }
            }
        }
        return output.ToArray();
    }
    private static SvgStrokePath Slice(SvgStrokePath path, float[] cumulative, double start, double end)
    {
        if (start == 0 && end == path.Length)
            return path with
            {
                Closed = false
            };
        var low = 0;
        var high = path.Edges.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (cumulative[middle + 1] < start)
                low = middle + 1;
            else
                high = middle;
        }
        var origin = path.Origin;
        var tangent = Vector2.UnitX;
        var firstCurve = -1;
        var lastCurve = -1;
        var firstParameter = 0f;
        var lastParameter = 1f;
        for (var index = low; index < path.Edges.Length; index++)
        {
            var edge = path.Edges[index];
            var distance = cumulative[index];
            var length = cumulative[index + 1] - distance;
            if (start <= distance + length && end >= distance)
            {
                float At(double value)
                {
                    var parameter = edge.First + (edge.Last - edge.First) * ((float)value - distance) / length;
                    return Math.Clamp(parameter, edge.First, edge.Last);
                }
                var a = At(start);
                var b = At(end);
                if (firstCurve < 0)
                {
                    firstCurve = edge.CurveIndex;
                    firstParameter = a;
                    origin = path.Curves[firstCurve].Position(a);
                    tangent = SvgStrokeCurve.Unit(path.Curves[firstCurve].Tangent(a));
                }
                lastCurve = edge.CurveIndex;
                lastParameter = b;
            }
            if (distance + length >= end)
                break;
        }
        List<SvgStrokeCurve> curves = [];
        if (start != end)
            for (var index = firstCurve; index <= lastCurve; index++)
                curves.Add(path.Curves[index].Slice(index == firstCurve ? firstParameter : 0, index == lastCurve ? lastParameter : 1));
        return SvgStrokePath.Create(curves.ToArray(), origin, false, true, path.Tolerance, tangent);
    }
}
