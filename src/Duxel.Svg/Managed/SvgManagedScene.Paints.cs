using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private static ISvgPaint Gradient(XElement element, Vector4 bounds, Vector2 viewport)
    {
        var radial = element.Name.LocalName == "radialGradient";
        if (!radial && element.Name.LocalName != "linearGradient") throw new NotSupportedException("SVG paint definition is pending migration.");
        var userSpace = (string?)element.Attribute("gradientUnits") == "userSpaceOnUse";
        var mapping = SvgTransform.Parse((string?)element.Attribute("gradientTransform") ?? "");
        if (!userSpace) mapping *= Matrix3x2.CreateScale(bounds.Z, bounds.W) * Matrix3x2.CreateTranslation(bounds.X, bounds.Y);
        if (!Matrix3x2.Invert(mapping, out var inverse)) return new SvgSolidPaint(Vector4.Zero);
        var dimensions = userSpace ? viewport : Vector2.One;
        var diagonal = dimensions.Length() / MathF.Sqrt(2);
        float Coordinate(string name, float value, float reference)
        {
            var text = (string?)element.Attribute(name);
            if (text is null) return value;
            if (text.TrimEnd().EndsWith('%'))
            {
                var reader = new SvgNumberReader(text.AsSpan().Trim()[..^1]);
                var number = reader.Number();
                if (!reader.End) throw new InvalidDataException("Invalid SVG gradient percentage.");
                return number / 100 * reference;
            }
            return Length(element, name);
        }
        var center = new Vector2(Coordinate("cx", dimensions.X / 2, dimensions.X), Coordinate("cy", dimensions.Y / 2, dimensions.Y));
        var start = radial ? new Vector2(Coordinate("fx", center.X, dimensions.X), Coordinate("fy", center.Y, dimensions.Y))
            : new Vector2(Coordinate("x1", 0, dimensions.X), Coordinate("y1", 0, dimensions.Y));
        var end = radial ? center : new Vector2(Coordinate("x2", dimensions.X, dimensions.X), Coordinate("y2", 0, dimensions.Y));
        var radius = Coordinate("r", diagonal / 2, diagonal);
        if (radial && radius < 0) throw new InvalidDataException("SVG gradient radius cannot be negative.");
        if (radial && radius > 0 && Vector2.Distance(start, end) >= radius)
            start = end + Vector2.Normalize(start - end) * (radius * .99999f);
        List<SvgGradientStop> stops = [];
        var offset = 0f;
        foreach (var stop in element.Elements())
        {
            if (stop.Name.LocalName is "title" or "desc" or "metadata") continue;
            if (stop.Name.LocalName != "stop") throw new InvalidDataException("SVG gradients contain only stops.");
            offset = Math.Max(offset, Opacity(stop, "offset", 0));
            var color = SvgColor.Parse((string?)stop.Attribute("stop-color") ?? "black");
            color.W *= Opacity(stop, "stop-opacity", 1);
            stops.Add(new(offset, color));
        }
        var spread = (string?)element.Attribute("spreadMethod") ?? "pad";
        if (spread is not ("pad" or "reflect" or "repeat")) throw new InvalidDataException("Invalid SVG spread method.");
        if (radial && radius == 0) return new SvgSolidPaint(stops.Count == 0 ? Vector4.Zero : stops[^1].Color);
        return new SvgGradientPaint(radial, start, end, radius, stops.ToArray(), inverse, spread);
    }

}
