using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private static SvgGeometry Geometry(XElement element)
    {
        var kind = element.Name.LocalName;
        if (kind == "path")
            return SvgPathCompiler.Parse((string?)element.Attribute("d") ?? "");
        var x = Length(element, kind is "circle" or "ellipse" ? "cx" : kind == "line" ? "x1" : "x");
        var y = Length(element, kind is "circle" or "ellipse" ? "cy" : kind == "line" ? "y1" : "y");
        string F(float number) => number.ToString("R", CultureInfo.InvariantCulture);
        if (kind == "line")
            return SvgPathCompiler.Parse($"M{F(x)} {F(y)}L{F(Length(element, "x2"))} {F(Length(element, "y2"))}");
        if (kind is "polygon" or "polyline")
        {
            var points = Numbers((string?)element.Attribute("points") ?? "");
            if (points.Length % 2 != 0)
                throw new InvalidDataException("SVG points require coordinate pairs.");
            List<SvgSegment> segments = [];
            for (var index = 0; index < points.Length; index += 2)
                segments.Add(new(index == 0 ? SvgSegmentKind.Move : SvgSegmentKind.Line, new(points[index], points[index + 1])));
            if (kind == "polygon" && segments.Count != 0)
                segments.Add(new(SvgSegmentKind.Close, segments[0].End));
            return new(segments.ToArray());
        }
        if (kind is "circle" or "ellipse")
        {
            var rx = Length(element, kind == "circle" ? "r" : "rx");
            var ry = kind == "circle" ? rx : Length(element, "ry");
            if (rx < 0 || ry < 0)
                throw new InvalidDataException("SVG radii cannot be negative.");
            if (rx == 0 || ry == 0)
                return new([]);
            return SvgPathCompiler.Parse($"M{F(x + rx)} {F(y)}A{F(rx)} {F(ry)} 0 1 1 {F(x - rx)} {F(y)}A{F(rx)} {F(ry)} 0 1 1 {F(x + rx)} {F(y)}Z", smoothPrimitive: true)
                .WithAnalytic(new(new(x - rx, y - ry, 2 * rx, 2 * ry), new(rx, ry), Matrix3x2.Identity));
        }
        if (kind == "rect")
        {
            var width = Length(element, "width");
            var height = Length(element, "height");
            if (width < 0 || height < 0)
                throw new InvalidDataException("SVG rectangle sizes cannot be negative.");
            if (width == 0 || height == 0)
                return new([]);
            var rx = Length(element, "rx", Length(element, "ry"));
            var ry = Length(element, "ry", rx);
            if (rx < 0 || ry < 0)
                throw new InvalidDataException("SVG corner radii cannot be negative.");
            rx = MathF.Min(rx, width / 2);
            ry = MathF.Min(ry, height / 2);
            if (rx == 0 || ry == 0)
                return SvgPathCompiler.Parse($"M{F(x)} {F(y)}h{F(width)}v{F(height)}h{F(-width)}Z");
            return SvgPathCompiler.Parse($"M{F(x + rx)} {F(y)}H{F(x + width - rx)}A{F(rx)} {F(ry)} 0 0 1 {F(x + width)} {F(y + ry)}V{F(y + height - ry)}A{F(rx)} {F(ry)} 0 0 1 {F(x + width - rx)} {F(y + height)}H{F(x + rx)}A{F(rx)} {F(ry)} 0 0 1 {F(x)} {F(y + height - ry)}V{F(y + ry)}A{F(rx)} {F(ry)} 0 0 1 {F(x + rx)} {F(y)}Z", smoothPrimitive: true)
                .WithAnalytic(new(new(x, y, width, height), new(rx, ry), Matrix3x2.Identity));
        }
        throw new NotSupportedException("Managed SVG element is pending migration: " + kind);
    }
}
