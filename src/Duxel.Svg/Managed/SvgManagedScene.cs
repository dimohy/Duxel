using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace Duxel.Svg.Managed;

/// <summary>Immutable compiled scene. Rendering never walks XML or parses source properties.</summary>
internal sealed partial class SvgManagedScene
{
    private readonly Node _root;
    private readonly Matrix3x2 _view;
    internal float Width
    {
        get;
    }
    internal float Height
    {
        get;
    }
    private sealed record Style(string Fill = "black", string Stroke = "none", string Color = "black",
        float FillOpacity = 1, float StrokeOpacity = 1, float StrokeWidth = 1, string Cap = "butt",
        string Join = "miter", float MiterLimit = 4, bool EvenOdd = false, string DashArray = "none", string DashOffset = "0",
        string? FontFamily = null, float FontSize = 16, int FontWeight = 400, string FontStyle = "normal", string Direction = "ltr", string Anchor = "start",
        bool Visible = true);
    private SvgManagedScene(Node root, float width, float height, Matrix3x2 view) => (_root, Width, Height, _view) = (root, width, height, view);

    internal byte[] Render(int width, int height, Vector4 background = default)
    {
        var viewport = SvgViewport.Fit(Width, Height, width, height);
        var surface = new SvgRasterSurface(width, height, background, viewport.Letterboxed ? viewport.Clip : null);
        _root.Render(surface, _view * viewport.Transform);
        return surface.CopyRgba();
    }

    internal static SvgManagedScene Compile(XDocument document, ISvgTextCompiler? textCompiler = null, ISvgImageDecoder? imageDecoder = null)
    {
        document = SvgCssCascade.Apply(document);
        var root = document.Root ?? throw new InvalidDataException("SVG root is required.");
        if (root.Name.LocalName != "svg" || root.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg"))
            throw new InvalidDataException("SVG root is required.");
        var viewBox = root.Attribute("viewBox") is { } box ? Numbers(box.Value) : null;
        if (viewBox is not null && (viewBox.Length != 4 || viewBox[2] <= 0 || viewBox[3] <= 0))
            throw new InvalidDataException("SVG viewBox requires four numbers and a positive size.");
        var width = Length(root, "width", viewBox?[2] ?? 300);
        var height = Length(root, "height", viewBox?[3] ?? 150);
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("SVG viewport must be positive.");
        var view = Matrix3x2.Identity;
        if (viewBox is not null)
        {
            var sx = width / viewBox[2];
            var sy = height / viewBox[3];
            var aspect = (string?)root.Attribute("preserveAspectRatio") ?? "xMidYMid meet";
            if (aspect == "none")
                view = Matrix3x2.CreateTranslation(-viewBox[0], -viewBox[1]) * Matrix3x2.CreateScale(sx, sy);
            else if (aspect == "xMidYMid meet" || aspect == "xMidYMid")
            {
                var scale = MathF.Min(sx, sy);
                view = Matrix3x2.CreateTranslation(-viewBox[0], -viewBox[1]) * Matrix3x2.CreateScale(scale)
                    * Matrix3x2.CreateTranslation((width - viewBox[2] * scale) / 2, (height - viewBox[3] * scale) / 2);
            }
            else
                throw new NotSupportedException("Managed SVG aspect alignment is not implemented: " + aspect);
        }
        var definitions = new Dictionary<string, XElement>(StringComparer.Ordinal);
        Dictionary<string, SvgDecodedImage> images = new(StringComparer.Ordinal);
        foreach (var element in root.DescendantsAndSelf())
            if (element.Attribute("id") is { } id && !definitions.TryAdd(id.Value, element))
                throw new InvalidDataException("Duplicate SVG identifier.");
        var templates = new SvgPaintTemplates(definitions);
        return new(Build(root, new(), 0), width, height, view);

        Node Build(XElement element, Style inherited, int depth, bool clipping = false, SvgGeometry? replacement = null)
        {
            if (depth > 64)
                throw new InvalidDataException("SVG nesting/reference depth exceeds 64.");
            var kind = element.Name.LocalName;
            if (element.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg"))
                return Empty();
            if (kind is "title" or "desc" or "metadata" or "defs")
                return Empty();
            if (element.Attribute("style") is not null || kind == "style")
                throw new NotSupportedException("Managed SVG stylesheets are pending migration.");
            foreach (var property in new[] { "marker-mid" })
                if (element.Attribute(property) is { } unsupported && unsupported.Value.Trim() != "none")
                    throw new NotSupportedException("Managed SVG property is pending migration: " + property);
            if ((string?)element.Attribute("display") == "none")
                return Empty();
            var fillRule = (string?)element.Attribute(clipping ? "clip-rule" : "fill-rule");
            if (fillRule is not (null or "nonzero" or "evenodd"))
                throw new InvalidDataException("Invalid SVG fill rule.");
            var style = inherited with
            {
                Fill = (string?)element.Attribute("fill") ?? inherited.Fill,
                Visible = ((string?)element.Attribute("visibility"))?.Trim() switch
                {
                    null or "inherit" or "unset" => inherited.Visible,
                    "visible" or "initial" => true,
                    "hidden" or "collapse" => false,
                    _ => throw new NotSupportedException("Unsupported SVG visibility value.")
                },
                Stroke = (string?)element.Attribute("stroke") ?? inherited.Stroke,
                Color = (string?)element.Attribute("color") ?? inherited.Color,
                FillOpacity = Opacity(element, "fill-opacity", inherited.FillOpacity),
                StrokeOpacity = Opacity(element, "stroke-opacity", inherited.StrokeOpacity),
                StrokeWidth = Length(element, "stroke-width", inherited.StrokeWidth),
                DashArray = (string?)element.Attribute("stroke-dasharray") is { } dashArray && dashArray != "inherit" ? dashArray : inherited.DashArray,
                DashOffset = (string?)element.Attribute("stroke-dashoffset") is { } dashOffset && dashOffset != "inherit" ? dashOffset : inherited.DashOffset,
                MiterLimit = Length(element, "stroke-miterlimit", inherited.MiterLimit),
                Cap = (string?)element.Attribute("stroke-linecap") ?? inherited.Cap,
                Join = (string?)element.Attribute("stroke-linejoin") ?? inherited.Join,
                EvenOdd = fillRule is null ? inherited.EvenOdd : fillRule == "evenodd"
            };
            style = style with
            {
                FontFamily = (string?)element.Attribute("font-family") ?? inherited.FontFamily,
                FontSize = Length(element, "font-size", inherited.FontSize),
                FontWeight = element.Attribute("font-weight") is { } weight ? weight.Value switch
                {
                    "normal" => 400,
                    "bold" => 700,
                    _ => checked((int)Length(element, "font-weight"))
                } : inherited.FontWeight,
                FontStyle = (string?)element.Attribute("font-style") ?? inherited.FontStyle,
                Direction = (string?)element.Attribute("direction") ?? inherited.Direction,
                Anchor = (string?)element.Attribute("text-anchor") ?? inherited.Anchor
            };
            var transform = SvgTransform.Parse((string?)element.Attribute("transform") ?? "");
            var opacity = clipping ? 1 : Opacity(element, "opacity", 1);
            if (clipping)
                style = style with
                {
                    Fill = "white",
                    Stroke = "none",
                    FillOpacity = 1
                };
            Node Effects(Node node)
            {
                var luminanceMask = true;
                Node? Resource(string property, string expected, bool clip)
                {
                    if (element.Attribute(property) is not { } value || value.Value == "none")
                        return null;
                    var text = value.Value.Trim();
                    if (!text.StartsWith("url(", StringComparison.Ordinal) || !text.EndsWith(')'))
                        throw new InvalidDataException("SVG effects require a local url.");
                    var reference = text[4..^1].Trim().Trim('\'', '"');
                    if (!reference.StartsWith('#') || !definitions.TryGetValue(reference[1..], out var resource) || resource.Name.LocalName != expected)
                        throw new InvalidDataException("SVG effect definition is missing or has the wrong type.");
                    var units = (string?)resource.Attribute(clip ? "clipPathUnits" : "maskContentUnits");
                    if (expected == "mask")
                        luminanceMask = ((string?)resource.Attribute("mask-type"))?.Trim() switch
                        {
                            null or "luminance" => true,
                            "alpha" => false,
                            _ => throw new NotSupportedException("Unsupported SVG mask type.")
                        };
                    if (units == "objectBoundingBox")
                        throw new NotSupportedException("Object-bounding-box SVG effects are pending migration.");
                    return Build(resource, new(), depth + 1, clip);
                }
                GaussianFilter? filter = null;
                if (element.Attribute("filter") is { } filterValue && filterValue.Value != "none")
                {
                    var reference = filterValue.Value.Trim();
                    if (!reference.StartsWith("url(", StringComparison.Ordinal) || !reference.EndsWith(')'))
                        throw new NotSupportedException("SVG filters require a local URL reference.");
                    reference = reference[4..^1].Trim().Trim('\'', '"');
                    if (!reference.StartsWith('#') || !definitions.TryGetValue(reference[1..], out var definition) || definition.Name.LocalName != "filter")
                        throw new InvalidDataException("SVG filter definition is missing.");
                    filter = CompileFilter(definition, node.Bounds ?? Vector4.Zero, new(width / view.M11, height / view.M22));
                }
                return FoldRectangularClip(FoldSinglePaintOpacity(node with
                {
                    Clip = Resource("clip-path", "clipPath", true),
                    Mask = Resource("mask", "mask", false),
                    LuminanceMask = luminanceMask,
                    Filter = filter
                }, opacity));
            }
            if (kind == "text")
            {
                if (textCompiler is null)
                    throw new NotSupportedException("SVG text requires explicit registered fonts.");
                var parts = textCompiler.Compile(element, new(style.FontFamily, style.FontSize, style.FontWeight, style.FontStyle, style.Direction, style.Anchor));
                return Effects(new(transform, opacity, null, default, false, null, default,
                    parts.Select(part => Build(part.Properties, style, depth + 1, clipping, part.Geometry)).ToArray()));
            }
            if (kind == "image")
            {
                if (imageDecoder is null)
                    throw new NotSupportedException("SVG embedded images require a bitmap decoder.");
                var uri = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value
                    ?? throw new InvalidDataException("SVG image href is required.");
                if (!images.TryGetValue(uri, out var image))
                {
                    var comma = uri.IndexOf(',');
                    if (comma < 0 || !uri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ||
                        !uri[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException("SVG embedded bitmaps require base64 image data.");
                    image = imageDecoder.Decode(Convert.FromBase64String(uri[(comma + 1)..]));
                    images.Add(uri, image);
                }
                var bounds = new Vector4(Length(element, "x"), Length(element, "y"), Length(element, "width", image.Width), Length(element, "height", image.Height));
                if (bounds.Z <= 0 || bounds.W <= 0)
                    return Empty();
                var rectangle = new SvgGeometry([new(SvgSegmentKind.Move, new(bounds.X, bounds.Y)),
                    new(SvgSegmentKind.Line, new(bounds.X + bounds.Z, bounds.Y)),
                    new(SvgSegmentKind.Line, new(bounds.X + bounds.Z, bounds.Y + bounds.W)),
                    new(SvgSegmentKind.Line, new(bounds.X, bounds.Y + bounds.W)), new(SvgSegmentKind.Close, new(bounds.X, bounds.Y))]);
                var aspect = (string?)element.Attribute("preserveAspectRatio") ?? "xMidYMid meet";
                if (aspect is not ("none" or "xMidYMid" or "xMidYMid meet"))
                    throw new NotSupportedException("SVG image aspect alignment is pending migration.");
                return Effects(new(transform, opacity, rectangle, new SvgImagePaint(image, bounds, aspect == "none"), false, null, null, [], Visible: style.Visible));
            }
            if (kind == "use")
            {
                var href = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
                if (href is null || !href.StartsWith('#') || !definitions.TryGetValue(href[1..], out var referenced))
                    throw new InvalidDataException("SVG use requires an existing local reference.");
                var instance = Build(referenced, style, depth + 1, clipping);
                if (referenced.Name.LocalName == "symbol" && referenced.Attribute("viewBox") is { } symbolBox)
                {
                    var values = Numbers(symbolBox.Value);
                    if (values.Length != 4 || values[2] <= 0 || values[3] <= 0)
                        throw new InvalidDataException("Invalid SVG symbol viewBox.");
                    var viewportWidth = Length(element, "width", width);
                    var viewportHeight = Length(element, "height", height);
                    var scale = MathF.Min(viewportWidth / values[2], viewportHeight / values[3]);
                    var symbolTransform = Matrix3x2.CreateTranslation(-values[0], -values[1]) * Matrix3x2.CreateScale(scale)
                        * Matrix3x2.CreateTranslation((viewportWidth - values[2] * scale) / 2, (viewportHeight - values[3] * scale) / 2);
                    instance = instance with
                    {
                        Transform = instance.Transform * symbolTransform
                    };
                }
                return Effects(new(Matrix3x2.CreateTranslation(Length(element, "x"), Length(element, "y")) * transform, opacity,
                    null, default, false, null, default, [instance]));
            }
            if (kind is "svg" or "g" or "a" or "clipPath" or "mask" or "pattern" or "symbol" or "marker")
                return Effects(new(transform, opacity, null, default, false, null, default,
                    element.Elements().Where(child => child.Name.LocalName is not ("linearGradient" or "radialGradient" or "pattern" or "clipPath" or "mask" or "symbol" or "marker" or "filter"))
                    .Select(child => Build(child, style, depth + 1, clipping)).ToArray()));
            var geometry = replacement ?? Geometry(element);
            var fill = style.Fill == "none" ? null : geometry;
            var dash = SvgDashPattern.Parse(style.DashArray, style.DashOffset, width / view.M11, height / view.M22);
            var calibratedLength = element.Attribute("pathLength") is null ? (float?)null : Length(element, "pathLength");
            var stroke = style.Stroke == "none" ? null : new SvgStrokePlan(geometry, style.StrokeWidth, style.Cap, style.Join, style.MiterLimit, dash, calibratedLength);
            ISvgPaint Paint(string color, float alpha)
            {
                if (color.StartsWith("url(", StringComparison.Ordinal))
                {
                    var id = color[4..].TrimEnd(')').Trim().Trim('\'', '"');
                    if (!id.StartsWith('#') || !definitions.TryGetValue(id[1..], out var definition))
                        throw new InvalidDataException("SVG paint requires an existing local definition.");
                    definition = templates.Resolve(definition);
                    if (definition.Name.LocalName == "pattern")
                    {
                        var userSpace = (string?)definition.Attribute("patternUnits") == "userSpaceOnUse";
                        var tile = new Vector4(Length(definition, "x"), Length(definition, "y"), Length(definition, "width"), Length(definition, "height"));
                        var bounds = geometry.Bounds;
                        if (!userSpace)
                            tile = new(bounds.X + tile.X * bounds.Z, bounds.Y + tile.Y * bounds.W, tile.Z * bounds.Z, tile.W * bounds.W);
                        if (tile.Z <= 0 || tile.W <= 0)
                            return new SvgSolidPaint(Vector4.Zero);
                        var mapping = SvgTransform.Parse((string?)definition.Attribute("patternTransform") ?? "");
                        var content = Build(definition, new(), depth + 1);
                        if ((string?)definition.Attribute("patternContentUnits") == "objectBoundingBox")
                            content = content with
                            {
                                Transform = Matrix3x2.CreateScale(bounds.Z, bounds.W) * Matrix3x2.CreateTranslation(bounds.X, bounds.Y) * content.Transform
                            };
                        return new SvgOpacityPaint(new PatternPaint(content, tile, mapping), SvgCompositing.PaintServerOpacity(alpha));
                    }
                    var gradient = Gradient(definition, geometry.Bounds, new(width / view.M11, height / view.M22));
                    ISvgPaint painted = new SvgOpacityPaint(gradient, SvgCompositing.PaintServerOpacity(alpha));
                    return gradient is SvgSolidPaint ? painted : new SvgOrderedDither(painted);
                }
                var value = SvgColor.Parse(color == "currentColor" ? style.Color : color);
                value.W *= SvgCompositing.PaintOpacity(alpha);
                return new SvgSolidPaint(value);
            }
            List<Node> markers = [];
            void Marker(string property, bool atStart)
            {
                if (element.Attribute(property) is not { } value || value.Value == "none")
                    return;
                var text = value.Value.Trim();
                if (!text.StartsWith("url(", StringComparison.Ordinal) || !text.EndsWith(')'))
                    throw new InvalidDataException("SVG marker requires a local URL.");
                var reference = text[4..^1].Trim().Trim('\'', '"');
                if (!reference.StartsWith('#') || !definitions.TryGetValue(reference[1..], out var marker) || marker.Name.LocalName != "marker")
                    throw new InvalidDataException("SVG marker reference is invalid.");
                var segments = geometry.Segments;
                if (segments.Length < 2)
                    return;
                Vector2 position;
                Vector2 tangent;
                if (atStart)
                {
                    position = segments[0].End;
                    tangent = (segments[1].Kind is SvgSegmentKind.Cubic or SvgSegmentKind.Conic ? segments[1].Control1 : segments[1].End) - position;
                    if (tangent == Vector2.Zero)
                        tangent = segments[1].End - position;
                }
                else
                {
                    var last = segments[^1];
                    position = last.End;
                    tangent = position - (last.Kind == SvgSegmentKind.Conic ? last.Control1
                        : last.Kind == SvgSegmentKind.Cubic ? last.Control2 : segments[^2].End);
                    if (tangent == Vector2.Zero)
                        tangent = position - segments[^2].End;
                }
                var orient = (string?)marker.Attribute("orient") ?? "0";
                var radians = orient is "auto" or "auto-start-reverse" ? MathF.Atan2(tangent.Y, tangent.X) : Length(marker, "orient") * MathF.PI / 180;
                if (atStart && orient == "auto-start-reverse")
                    radians += MathF.PI;
                var scale = (string?)marker.Attribute("markerUnits") == "userSpaceOnUse" ? 1 : style.StrokeWidth;
                var child = Build(marker, new(), depth + 1);
                var placement = Matrix3x2.CreateTranslation(-Length(marker, "refX"), -Length(marker, "refY"))
                    * Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(radians) * Matrix3x2.CreateTranslation(position);
                markers.Add(child with
                {
                    Transform = child.Transform * placement
                });
            }
            if (!clipping)
            {
                Marker("marker-start", true);
                Marker("marker-end", false);
            }
            return Effects(new(transform, opacity, fill, fill is null ? default : Paint(style.Fill, style.FillOpacity), style.EvenOdd,
                stroke, stroke is null ? default : Paint(style.Stroke, style.StrokeOpacity), markers.ToArray(), ObjectBounds: geometry.Bounds, Visible: style.Visible));
        }
        static Node Empty() => new(Matrix3x2.Identity, 1, null, default, false, null, default, []);
    }

    private static float[] Numbers(string source)
    {
        var reader = new SvgNumberReader(source);
        List<float> result = [];
        while (!reader.End)
            result.Add(reader.Number());
        return result.ToArray();
    }
    private static float Length(XElement element, string property, float defaultValue = 0)
    {
        if (element.Attribute(property) is not { } value)
            return defaultValue;
        var text = value.Value.AsSpan().Trim();
        if (text.EndsWith("px", StringComparison.Ordinal))
            text = text[..^2];
        var reader = new SvgNumberReader(text);
        var result = reader.Number();
        if (!reader.End)
            throw new NotSupportedException("Managed SVG length unit is pending migration: " + value.Value);
        return result;
    }
    private static float Opacity(XElement element, string property, float defaultValue)
    {
        if (element.Attribute(property) is not { } value)
            return defaultValue;
        var text = value.Value.Trim();
        var percentage = text.EndsWith('%');
        var reader = new SvgNumberReader(percentage ? text.AsSpan(0, text.Length - 1) : text);
        var result = reader.Number();
        if (!reader.End)
            throw new InvalidDataException("Invalid SVG opacity.");
        return Math.Clamp(result / (percentage ? 100 : 1), 0, 1);
    }
}
