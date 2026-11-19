using System.Xml.Linq;

namespace Duxel.Svg.Managed;

/// <summary>Resolves local paint templates once, before retained paints are created.</summary>
internal sealed class SvgPaintTemplates(IReadOnlyDictionary<string, XElement> definitions)
{
    private readonly Dictionary<XElement, XElement> _resolved = [];

    internal XElement Resolve(XElement element, int depth = 0)
    {
        if (_resolved.TryGetValue(element, out var cached)) return cached;
        if (depth > 64) throw new InvalidDataException("SVG paint template cycle or depth exceeds 64.");
        var result = new XElement(element);
        var href = element.Attribute("href") ?? element.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
        if (href is not null)
        {
            if (!href.Value.StartsWith('#') || !definitions.TryGetValue(href.Value[1..], out var template))
                throw new InvalidDataException("SVG paint templates require an existing local reference.");
            var pattern = element.Name.LocalName == "pattern";
            if (pattern ? template.Name.LocalName != "pattern" : template.Name.LocalName is not ("linearGradient" or "radialGradient"))
                throw new InvalidDataException("Incompatible SVG paint template.");
            var inherited = Resolve(template, depth + 1);
            string[] attributes = pattern
                ? ["x", "y", "width", "height", "patternUnits", "patternContentUnits", "patternTransform", "viewBox", "preserveAspectRatio"]
                : ["gradientUnits", "gradientTransform", "spreadMethod"];
            if (!pattern && element.Name.LocalName == template.Name.LocalName)
                attributes = element.Name.LocalName == "linearGradient"
                    ? [.. attributes, "x1", "y1", "x2", "y2"]
                    : [.. attributes, "cx", "cy", "r", "fx", "fy", "fr"];
            foreach (var name in attributes)
                if (result.Attribute(name) is null && inherited.Attribute(name) is { } value)
                    result.Add(new XAttribute(value));
            bool Relevant(XElement child) => child.Name.LocalName is not ("title" or "desc" or "metadata");
            if (!element.Elements().Any(Relevant))
                result.Add(inherited.Elements().Where(Relevant).Select(child => new XElement(child)));
            result.Attribute("href")?.Remove();
            result.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))?.Remove();
        }
        _resolved.Add(element, result);
        return result;
    }
}
