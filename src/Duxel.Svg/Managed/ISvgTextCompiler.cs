using System.Xml.Linq;

namespace Duxel.Svg.Managed;

internal readonly record struct SvgTextStyle(string? Family, float Size, int Weight, string Style, string Direction, string Anchor);
internal readonly record struct SvgTextShape(SvgGeometry Geometry, XElement Properties);
internal interface ISvgTextCompiler
{
    IReadOnlyList<SvgTextShape> Compile(XElement text, SvgTextStyle inherited);
}
