using System.Numerics;
using System.Xml.Linq;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private sealed record GaussianFilter(Vector2 Deviation, SvgGeometry Region, bool LinearRgb);

    private static GaussianFilter CompileFilter(XElement element, Vector4 bounds, Vector2 viewport)
    {
        var primitives = element.Elements().Where(child => child.Name.LocalName is not ("title" or "desc" or "metadata")).ToArray();
        if (primitives is not [var blur] || blur.Name.LocalName != "feGaussianBlur")
            throw new NotSupportedException("SVG filters currently require a single feGaussianBlur primitive.");
        if ((string?)blur.Attribute("in") is not (null or "SourceGraphic") ||
            (string?)blur.Attribute("edgeMode") is not (null or "none") ||
            blur.Attributes().Any(attribute => attribute.Name.LocalName is "x" or "y" or "width" or "height"))
            throw new NotSupportedException("Unsupported SVG Gaussian input, edge mode or primitive subregion.");
        var values = Numbers((string?)blur.Attribute("stdDeviation") ?? "0");
        if (values.Length is < 1 or > 2 || values.Any(value => value < 0))
            throw new InvalidDataException("SVG Gaussian deviation requires one or two nonnegative numbers.");
        var deviation = new Vector2(values[0], values.Length == 1 ? values[0] : values[1]);
        var primitiveUnits = (string?)element.Attribute("primitiveUnits") ?? "userSpaceOnUse";
        if (primitiveUnits == "objectBoundingBox") deviation *= new Vector2(bounds.Z, bounds.W);
        else if (primitiveUnits != "userSpaceOnUse") throw new InvalidDataException("Invalid SVG primitive units.");
        var units = (string?)element.Attribute("filterUnits") ?? "objectBoundingBox";
        if (units is not ("objectBoundingBox" or "userSpaceOnUse")) throw new InvalidDataException("Invalid SVG filter units.");
        var box = units == "objectBoundingBox";
        float Coordinate(string name, float defaultValue, float dimension)
        {
            var text = (string?)element.Attribute(name);
            if (text is null) return defaultValue * (box ? 1 : dimension);
            if (text.TrimEnd().EndsWith('%'))
            {
                var reader = new SvgNumberReader(text.AsSpan().Trim()[..^1]);
                var value = reader.Number();
                if (!reader.End) throw new InvalidDataException("Invalid SVG filter percentage.");
                return value / 100 * (box ? 1 : dimension);
            }
            return Length(element, name);
        }
        var x = Coordinate("x", -.1f, viewport.X);
        var y = Coordinate("y", -.1f, viewport.Y);
        var width = Coordinate("width", 1.2f, viewport.X);
        var height = Coordinate("height", 1.2f, viewport.Y);
        if (box) { x = bounds.X + x * bounds.Z; y = bounds.Y + y * bounds.W; width *= bounds.Z; height *= bounds.W; }
        if (width < 0 || height < 0) throw new InvalidDataException("SVG filter region cannot be negative.");
        var region = new SvgGeometry([new(SvgSegmentKind.Move, new(x, y)), new(SvgSegmentKind.Line, new(x + width, y)),
            new(SvgSegmentKind.Line, new(x + width, y + height)), new(SvgSegmentKind.Line, new(x, y + height)), new(SvgSegmentKind.Close, new(x, y))]);
        var colorSpace = (string?)blur.Attribute("color-interpolation-filters") ?? (string?)element.Attribute("color-interpolation-filters") ?? "linearRGB";
        if (colorSpace is not ("linearRGB" or "sRGB")) throw new NotSupportedException("Unsupported SVG filter color space.");
        return new(deviation, region, colorSpace == "linearRGB");
    }

    private static Vector4 TransformBounds(Vector4 bounds, Matrix3x2 transform)
    {
        var a = Vector2.Transform(new(bounds.X, bounds.Y), transform);
        var b = Vector2.Transform(new(bounds.X + bounds.Z, bounds.Y), transform);
        var c = Vector2.Transform(new(bounds.X, bounds.Y + bounds.W), transform);
        var d = Vector2.Transform(new(bounds.X + bounds.Z, bounds.Y + bounds.W), transform);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }
}
