using System.Numerics;

namespace Duxel.Svg.Managed;

internal static class SvgTransform
{
    internal static Matrix3x2 Parse(string source)
    {
        var remaining = source.AsSpan();
        var result = Matrix3x2.Identity;
        Span<float> args = stackalloc float[6];
        while (!remaining.IsEmpty)
        {
            remaining = remaining.TrimStart();
            if (remaining.IsEmpty) break;
            if (remaining[0] == ',') remaining = remaining[1..].TrimStart();
            var open = remaining.IndexOf('(');
            var close = remaining.IndexOf(')');
            if (open <= 0 || close < open) throw new InvalidDataException("Invalid SVG transform list.");
            var name = remaining[..open].Trim();
            var numbers = new SvgNumberReader(remaining[(open + 1)..close]);
            var count = 0;
            while (!numbers.End)
            {
                if (count == args.Length) throw new InvalidDataException("Too many SVG transform parameters.");
                args[count++] = numbers.Number();
            }
            var transform = name switch
            {
                "matrix" when count == 6 => new(args[0], args[1], args[2], args[3], args[4], args[5]),
                "translate" when count is 1 or 2 => Matrix3x2.CreateTranslation(args[0], count == 2 ? args[1] : 0),
                "scale" when count is 1 or 2 => Matrix3x2.CreateScale(args[0], count == 2 ? args[1] : args[0]),
                "rotate" when count == 1 => Matrix3x2.CreateRotation(args[0] * MathF.PI / 180),
                "rotate" when count == 3 => Matrix3x2.CreateRotation(args[0] * MathF.PI / 180, new(args[1], args[2])),
                "skewX" when count == 1 => new Matrix3x2(1, 0, MathF.Tan(args[0] * MathF.PI / 180), 1, 0, 0),
                "skewY" when count == 1 => new Matrix3x2(1, MathF.Tan(args[0] * MathF.PI / 180), 0, 1, 0, 0),
                _ => throw new InvalidDataException("Unsupported SVG transform or parameter count: " + name.ToString())
            };
            result = transform * result;
            if (!float.IsFinite(result.M11) || !float.IsFinite(result.M12) || !float.IsFinite(result.M21) ||
                !float.IsFinite(result.M22) || !float.IsFinite(result.M31) || !float.IsFinite(result.M32))
                throw new InvalidDataException("SVG transform composition overflowed.");
            remaining = remaining[(close + 1)..];
        }
        return result;
    }
}
