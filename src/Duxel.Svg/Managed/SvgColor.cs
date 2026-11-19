using System.Globalization;
using System.Numerics;

namespace Duxel.Svg.Managed;

internal static class SvgColor
{
    internal static Vector4 Parse(string source)
    {
        var text = source.Trim();
        if (text.StartsWith('#'))
        {
            var hex = text.AsSpan(1);
            Span<byte> rgba = stackalloc byte[4];
            rgba[3] = 255;
            var length = hex.Length;
            if (length is not (3 or 4 or 6 or 8))
                throw new InvalidDataException("Invalid SVG hexadecimal color.");
            for (var index = 0; index < (length is 3 or 6 ? 3 : 4); index++)
            {
                var token = length < 5 ? hex.Slice(index, 1) : hex.Slice(index * 2, 2);
                if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var channel))
                    throw new InvalidDataException("Invalid SVG hexadecimal channel.");
                rgba[index] = (byte)(length < 5 ? channel * 17 : channel);
            }
            return new Vector4(rgba[0], rgba[1], rgba[2], rgba[3]) / 255;
        }
        if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) || text.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            if (!text.EndsWith(')'))
                throw new InvalidDataException("Incomplete SVG rgb color.");
            var alpha = text.StartsWith("rgba", StringComparison.OrdinalIgnoreCase);
            var tokens = text[(alpha ? 5 : 4)..^1].Split(',', StringSplitOptions.TrimEntries);
            if (tokens.Length != (alpha ? 4 : 3))
                throw new InvalidDataException("Invalid SVG rgb color components.");
            Span<float> values = stackalloc float[4];
            values[3] = 1;
            for (var i = 0; i < tokens.Length; i++)
            {
                var percentage = tokens[i].EndsWith('%');
                var number = new SvgNumberReader(percentage ? tokens[i].AsSpan(0, tokens[i].Length - 1) : tokens[i]);
                var value = number.Number();
                if (!number.End)
                    throw new InvalidDataException("Invalid SVG color number.");
                var normalized = Math.Clamp(value / (percentage ? 100 : i == 3 ? 1 : 255), 0, 1);
                values[i] = i == 3 ? normalized : MathF.Floor(normalized * 255 + .5f) / 255;
            }
            return new(values[0], values[1], values[2], values[3]);
        }
        return text.ToLowerInvariant() switch
        {
            "transparent" => Vector4.Zero,
            "black" => new(0, 0, 0, 1),
            "white" => Vector4.One,
            "red" => new(1, 0, 0, 1),
            "lime" => new(0, 1, 0, 1),
            "green" => new(0, 128f / 255, 0, 1),
            "blue" => new(0, 0, 1, 1),
            "yellow" => new(1, 1, 0, 1),
            "cyan" or "aqua" => new(0, 1, 1, 1),
            "magenta" or "fuchsia" => new(1, 0, 1, 1),
            "gray" or "grey" => new(128f / 255, 128f / 255, 128f / 255, 1),
            "orange" => new(1, 165f / 255, 0, 1),
            "purple" => new(128f / 255, 0, 128f / 255, 1),
            _ => throw new NotSupportedException("SVG color is not implemented: " + source)
        };
    }
}
