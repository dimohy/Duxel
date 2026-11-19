using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Duxel.Svg;

internal static class SvgSource
{
    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "title", "desc", "metadata", "symbol", "use", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "style", "linearGradient", "radialGradient", "stop", "pattern", "clipPath", "mask", "filter", "marker", "image", "view", "switch", "a",
        "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feConvolveMatrix", "feDiffuseLighting", "feDisplacementMap",
        "feDistantLight", "feDropShadow", "feFlood", "feFuncA", "feFuncB", "feFuncG", "feFuncR", "feGaussianBlur", "feImage", "feMerge",
        "feMergeNode", "feMorphology", "feOffset", "fePointLight", "feSpecularLighting", "feSpotLight", "feTile", "feTurbulence",
        "text", "tspan", "textPath", "tref"
    };

    internal static XDocument Parse(byte[] bytes, long limit, bool textEnabled = false)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = limit
        });
        // Parse XML bytes once. The renderer subsequently walks this DOM reader, never reparses source text.
        var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        var root = document.Root;
        if (root is null || root.Name.LocalName != "svg" || root.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg"))
            throw new InvalidDataException("An SVG root is required.");
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.DescendantsAndSelf())
            if (element.Attribute("id") is { } id && !identifiers.Add(id.Value))
                throw new InvalidDataException("Duplicate SVG id : " + id.Value);
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.AncestorsAndSelf().Any(parent => parent.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg")) ||
                element.Ancestors().Any(parent => parent.Name.LocalName == "metadata"))
                continue;
            if (!textEnabled && (element.Name.LocalName is "text" or "tspan" or "textPath" or "tref"))
                throw new NotSupportedException("SVG text requires explicit SvgFontOptions font registration.");
            if (!Elements.Contains(element.Name.LocalName))
                throw new NotSupportedException("Unsupported SVG element : " + element.Name);
            if (element.Name.LocalName == "style")
                CheckCss(element.Value, identifiers);
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;
                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("SVG event scripts are not executed.");
                if (attribute.Name.LocalName == "href" && element.Name.LocalName != "a")
                    CheckReference(attribute.Value.Trim(), identifiers, element.Name.LocalName is "image" or "feImage");
                if (attribute.Name.LocalName is "style" or "fill" or "stroke" or "filter" or "clip-path" or "mask"
                    or "marker" or "marker-start" or "marker-mid" or "marker-end" or "cursor")
                    CheckCss(attribute.Value, identifiers);
            }
        }
        return document;
    }
    private static void CheckCss(string value, HashSet<string> identifiers)
    {
        foreach (var target in SvgCssResources.Read(value))
            CheckReference(target.Trim(), identifiers, allowImage: true);
    }
    private static void CheckReference(string target, HashSet<string> identifiers, bool allowImage)
    {
        if (target.StartsWith('#'))
        {
            if (!identifiers.Contains(target[1..]))
                throw new InvalidDataException("Unresolved SVG fragment : " + target);
            return;
        }
        if (allowImage && target.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var comma = target.IndexOf(',');
            if (comma < 0 || !target[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Embedded SVG images must use base64 raster data.");
            _ = Convert.FromBase64String(target[(comma + 1)..]);
            return;
        }
        throw new NotSupportedException("SVG resources must be resolved local fragments or embedded raster images : " + target);
    }
    internal static string DecodeEscapes(string text) => Managed.SvgCssEscapes.Decode(text);
}
