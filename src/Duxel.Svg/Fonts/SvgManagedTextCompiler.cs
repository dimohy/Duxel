using System.Numerics;
using System.Xml.Linq;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal sealed class SvgManagedTextCompiler(SvgFontCatalog fonts) : ISvgTextCompiler
{
    public IReadOnlyList<SvgTextShape> Compile(XElement text, SvgTextStyle inherited)
    {
        var definitions = text.Document!.Descendants().Where(e => e.Attribute("id") is not null)
            .ToDictionary(e => e.Attribute("id")!.Value, StringComparer.Ordinal);
        List<SvgTextShape> shapes = [];
        var position = Vector2.Zero;
        var chunkStart = 0;
        var chunkPosition = 0f;
        var chunkStyle = inherited;
        var started = false;
        var whitespace = false;
        var referenceStack = new HashSet<string>(StringComparer.Ordinal);
        void FinishChunk()
        {
            var advance = position.X - chunkPosition;
            var shift = chunkStyle.Anchor switch
            {
                "middle" => -advance / 2,
                "end" => chunkStyle.Direction == "rtl" ? 0 : -advance,
                "start" => chunkStyle.Direction == "rtl" ? -advance : 0,
                _ => throw new InvalidDataException("Invalid SVG text-anchor.")
            };
            if (shift != 0)
                for (var i = chunkStart; i < shapes.Count; i++)
                    shapes[i] = shapes[i] with
                    {
                        Geometry = Transform(shapes[i].Geometry, Matrix3x2.CreateTranslation(shift, 0))
                    };
            chunkStart = shapes.Count;
            chunkPosition = position.X;
        }
        XElement Properties(XElement element, XElement? parent)
        {
            var result = parent is null ? new XElement("path") : new XElement(parent);
            foreach (var attribute in element.Attributes())
                if (attribute.Name.NamespaceName.Length == 0 && attribute.Name.LocalName is
                    "fill" or "stroke" or "stroke-width" or "stroke-linecap" or "stroke-linejoin" or "stroke-miterlimit" or
                    "fill-opacity" or "stroke-opacity" or "fill-rule" or "color" or "opacity" or "stroke-dasharray" or "stroke-dashoffset" or "visibility")
                    if (parent is not null || attribute.Name.LocalName != "opacity")
                    {
                        // Glyph properties are flattened; inherited visibility keeps the parent span's value.
                        if (attribute.Name.LocalName == "visibility" && attribute.Value.Trim() is "inherit" or "unset")
                            continue;
                        result.SetAttributeValue(attribute.Name, attribute.Value);
                    }
            return result;
        }
        string Characters(XElement reference)
        {
            var id = reference.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
            if (id is null || !id.StartsWith('#') || !definitions.TryGetValue(id[1..], out var target))
                throw new InvalidDataException("SVG text reference requires an existing local target.");
            if (!referenceStack.Add(id))
                throw new InvalidDataException("Cyclic SVG text reference.");
            try
            {
                var builder = new System.Text.StringBuilder();
                void Gather(XElement current)
                {
                    foreach (var node in current.Nodes())
                    {
                        if (node is XText literal)
                            builder.Append(literal.Value);
                        else if (node is XElement child)
                        {
                            if (child.Name.LocalName == "tref")
                                builder.Append(Characters(child));
                            else
                                Gather(child);
                        }
                    }
                }
                Gather(target);
                return builder.ToString();
            }
            finally { referenceStack.Remove(id); }
        }
        void Literal(string value, SvgTextStyle style, XElement properties, bool preserve, SvgEdge[]? path = null, float pathOffset = 0)
        {
            if (!preserve)
            {
                var normalized = new System.Text.StringBuilder(value.Length);
                foreach (var c in value)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        whitespace = started;
                        continue;
                    }
                    if (whitespace)
                    {
                        normalized.Append(' ');
                        whitespace = false;
                    }
                    normalized.Append(c);
                    started = true;
                }
                value = normalized.ToString();
            }
            if (value.Length == 0 || style.Size == 0)
                return;
            var runs = fonts.Runs(value, style.Family, style.Weight, style: style.Style);
            foreach (var run in runs)
            {
                var scale = style.Size / run.Font.UnitsPerEm;
                foreach (var glyph in run.Font.Shape(run.Text, style.Direction == "rtl"))
                {
                    var origin = position + glyph.Offset * scale;
                    var transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(origin);
                    if (path is not null)
                    {
                        var distance = pathOffset + position.X + glyph.Advance.X * scale / 2;
                        var found = false;
                        foreach (var edge in path)
                        {
                            var length = Vector2.Distance(edge.Start, edge.End);
                            if (distance >= 0 && distance <= length)
                            {
                                var direction = edge.End - edge.Start;
                                var point = Vector2.Lerp(edge.Start, edge.End, distance / length);
                                transform = Matrix3x2.CreateScale(scale)
                                    * Matrix3x2.CreateTranslation((glyph.Offset.X - glyph.Advance.X / 2) * scale, position.Y + glyph.Offset.Y * scale)
                                    * Matrix3x2.CreateRotation(MathF.Atan2(direction.Y, direction.X)) * Matrix3x2.CreateTranslation(point);
                                found = true;
                                break;
                            }
                            distance -= length;
                        }
                        if (!found)
                        {
                            position += glyph.Advance * scale;
                            continue;
                        }
                    }
                    shapes.Add(new(Transform(glyph.Outline, transform), new(properties)));
                    position += glyph.Advance * scale;
                }
            }
        }
        void Visit(XElement element, SvgTextStyle parent, XElement? parentPaint, bool preserve, SvgEdge[]? path = null, float offset = 0)
        {
            var style = parent with
            {
                Family = (string?)element.Attribute("font-family") ?? parent.Family,
                Size = Number(element, "font-size", parent.Size),
                Weight = element.Attribute("font-weight") is { } weight ? weight.Value switch
                {
                    "normal" => 400,
                    "bold" => 700,
                    _ => checked((int)Number(element, "font-weight", parent.Weight))
                } : parent.Weight,
                Style = (string?)element.Attribute("font-style") ?? parent.Style,
                Direction = (string?)element.Attribute("direction") ?? parent.Direction,
                Anchor = (string?)element.Attribute("text-anchor") ?? parent.Anchor
            };
            if (style.Size < 0 || style.Direction is not ("ltr" or "rtl"))
                throw new InvalidDataException("Invalid SVG text size or direction.");
            if (element.Attribute("x") is not null || element.Attribute("y") is not null)
            {
                FinishChunk();
                position = new(Number(element, "x", position.X), Number(element, "y", position.Y));
                chunkPosition = position.X;
                chunkStyle = style;
            }
            position += new Vector2(Number(element, "dx", 0), Number(element, "dy", 0));
            preserve = (string?)element.Attribute(XNamespace.Xml + "space") switch
            {
                "preserve" => true,
                "default" => false,
                _ => preserve
            };
            var properties = Properties(element, parentPaint);
            foreach (var node in element.Nodes())
            {
                if (node is XText literal)
                    Literal(literal.Value, style, properties, preserve, path, offset);
                else if (node is XElement child)
                {
                    if (child.Name.LocalName == "tref")
                    {
                        var instance = new XElement(child.Name, child.Attributes(), new XText(Characters(child)));
                        Visit(instance, style, properties, preserve, path, offset);
                    }
                    else if (child.Name.LocalName == "textPath")
                    {
                        var href = child.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
                        if (href is null || !href.StartsWith('#') || !definitions.TryGetValue(href[1..], out var target) || target.Name.LocalName != "path")
                            throw new InvalidDataException("SVG textPath requires a local path.");
                        var geometry = SvgPathCompiler.Parse((string?)target.Attribute("d") ?? "");
                        var edges = geometry.Flatten(SvgTransform.Parse((string?)target.Attribute("transform") ?? ""), .05f, closeOpenContours: false);
                        position = Vector2.Zero;
                        Visit(child, style, properties, preserve, edges, Number(child, "startOffset", 0));
                    }
                    else if (child.Name.LocalName == "tspan")
                        Visit(child, style, properties, preserve, path, offset);
                    else if (child.Name.LocalName is not ("title" or "desc"))
                        throw new NotSupportedException("Unsupported SVG text child: " + child.Name);
                }
            }
        }
        Visit(text, inherited, null, false);
        FinishChunk();
        return shapes;
    }
    private static float Number(XElement element, string property, float defaultValue)
    {
        if (element.Attribute(property) is not { } attribute)
            return defaultValue;
        var value = attribute.Value.AsSpan().Trim();
        if (value.EndsWith("px", StringComparison.Ordinal))
            value = value[..^2];
        var reader = new SvgNumberReader(value);
        var result = reader.Number();
        if (!reader.End)
            throw new NotSupportedException("SVG text coordinate lists or units are pending migration: " + attribute.Value);
        return result;
    }
    private static SvgGeometry Transform(SvgGeometry source, Matrix3x2 transform)
    {
        var segments = source.Segments.ToArray();
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            segments[i] = segment with
            {
                End = Vector2.Transform(segment.End, transform),
                Control1 = Vector2.Transform(segment.Control1, transform),
                Control2 = Vector2.Transform(segment.Control2, transform)
            };
        }
        return new(segments);
    }
}
