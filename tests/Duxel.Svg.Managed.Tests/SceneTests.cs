using System.Numerics;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class SceneTests
{
    internal static void Run(Action<bool, string> check)
    {
        SvgManagedScene Compile(string body, string attributes = "width='32' height='32'") =>
            SvgManagedScene.Compile(XDocument.Parse("<svg xmlns='http://www.w3.org/2000/svg' " + attributes + ">" + body + "</svg>"));
        byte Alpha(byte[] pixels, int x, int y) => pixels[(y * 32 + x) * 4 + 3];
        byte Channel(byte[] pixels, int x, int y, int channel) => pixels[(y * 32 + x) * 4 + channel];
        var document = XDocument.Parse("<svg width='32' height='32'><rect x='4' y='4' width='24' height='24' fill='#e03020'/></svg>");
        var scene = SvgManagedScene.Compile(document);
        var retained = scene.Render(32, 32);
        document.Root!.RemoveNodes();
        check(retained.SequenceEqual(scene.Render(32, 32)) && Channel(retained, 8, 8, 0) == 224 && Alpha(retained, 0, 0) == 0,
            "Compiled scenes retain geometry and paints independently of later XML mutation");
        check(scene.Render(64, 64)[(16 * 64 + 16) * 4] == 224,
            "A compiled scene scales without revisiting the source XML");
        var inherited = Compile("<g fill='#2080e0' transform='translate(4 4) scale(2)'><rect width='8' height='8'/></g>").Render(32, 32);
        check(Channel(inherited, 8, 8, 2) == 224 && Alpha(inherited, 2, 8) == 0 && Alpha(inherited, 21, 8) == 0,
            "Inherited paints and ordered SVG transforms control the rendered viewport");
        var grouped = Compile("<g opacity='.5'><rect width='24' height='24' fill='red'/><rect x='8' width='24' height='24' fill='blue'/></g>").Render(32, 32);
        check(Alpha(grouped, 4, 4) == 127 && Alpha(grouped, 12, 4) == 127 && Channel(grouped, 12, 4, 2) == 255,
            "Group opacity composites once after its children rather than accumulating overlap alpha");
        var clippedGroup = Compile("<defs><clipPath id='c'><rect width='16' height='16'/></clipPath></defs><g opacity='.5' clip-path='url(#c)'><rect width='24' height='24' fill='red'/><rect x='8' width='24' height='24' fill='blue'/></g>").Render(32, 32);
        check(Alpha(clippedGroup, 4, 4) == 127 && Alpha(clippedGroup, 12, 4) == 127 &&
            Channel(clippedGroup, 12, 4, 2) == 255 && Alpha(clippedGroup, 20, 4) == 0,
            "A geometric clip preserves one group-opacity composite across overlapping paints");
        var use = Compile("<defs><rect id='tile' width='8' height='8' fill='currentColor'/></defs><use href='#tile' x='12' y='8' color='#20c060'/>").Render(32, 32);
        check(Channel(use, 14, 10, 1) == 192 && Alpha(use, 2, 2) == 0,
            "Local use compiles shared definitions with inherited currentColor without rendering defs");
        var view = Compile("<circle cx='12' cy='12' r='10' fill='lime'/>", "viewBox='0 0 24 24'").Render(32, 32);
        check(Alpha(view, 16, 16) == 255 && Alpha(view, 0, 0) == 0,
            "A viewBox-only icon retains its intrinsic size and centered aspect ratio");
        var rounded = Compile("<rect x='4' y='4' width='24' height='24' rx='6' fill='red'/>").Render(32, 32);
        check(Alpha(rounded, 4, 4) == 0 && Alpha(rounded, 16, 4) == 255,
            "Rounded rectangle corners preserve curved coverage and their straight edges");
        var stroke = Compile("<path d='M8 16H24' fill='none' stroke='#2080e0' stroke-width='4' stroke-linecap='round'/>").Render(32, 32);
        check(Channel(stroke, 16, 16, 2) == 224 && Alpha(stroke, 7, 16) > 150 && Alpha(stroke, 4, 16) == 0,
            "Managed stroke outlines preserve authored width, color and round endpoints");
        var butt = Compile("<path d='M8 16H24' fill='none' stroke='red' stroke-width='4'/>").Render(32, 32);
        check(Alpha(butt, 7, 16) == 0 && Alpha(butt, 8, 16) == 255 && Alpha(butt, 24, 16) == 0,
            "Default butt caps stop at the exact path endpoints");
        var miter = Compile("<path d='M8 24V8H24' fill='none' stroke='red' stroke-width='8'/>").Render(32, 32);
        check(Alpha(miter, 4, 4) > 200 && Alpha(miter, 12, 12) == 0,
            "Default miter joins fill the external intersection without filling the internal corner");
        var polygon = Compile("<polygon points='4,28 16,4 28,28' fill='red'/>").Render(32, 32);
        check(Alpha(polygon, 16, 16) == 255 && Alpha(polygon, 0, 0) == 0,
            "Polygon coordinate pairs compile into closed antialiased geometry");
        var hidden = Compile("<g display='none'><rect width='32' height='32'/></g>").Render(32, 32);
        check(hidden.All(value => value == 0), "Display-none subtrees produce no retained drawing commands");
        var invisible = Compile("<g visibility='hidden'><rect width='32' height='32'/><rect x='8' y='8' width='8' height='8' visibility='visible' fill='red'/></g>").Render(32, 32);
        check(Alpha(invisible, 4, 4) == 0 && Channel(invisible, 10, 10, 0) == 255 && Alpha(invisible, 10, 10) == 255,
            "Hidden visibility inherits without preventing descendants from becoming visible");
        var collapsed = Compile("<g visibility='collapse'><rect width='32' height='32'/><rect x='8' width='8' height='8' visibility='initial'/><rect x='24' width='8' height='8' visibility='inherit'/></g>").Render(32, 32);
        check(Alpha(collapsed, 4, 4) == 0 && Alpha(collapsed, 10, 4) == 255 && Alpha(collapsed, 26, 4) == 0,
            "Collapse suppresses paint while explicit initial visibility restores the default");
        var hiddenUse = Compile("<defs><g id='visible-child'><rect width='8' height='8' visibility='visible'/><rect x='16' width='8' height='8'/></g></defs><use href='#visible-child' visibility='hidden'/>").Render(32, 32);
        check(Alpha(hiddenUse, 4, 4) == 255 && Alpha(hiddenUse, 20, 4) == 0,
            "Use instances inherit hidden visibility and preserve explicit descendant overrides");
        var hiddenBounds = Compile("<defs><filter id='region' x='0' y='0' width='.5' height='1'><feGaussianBlur stdDeviation='0'/></filter></defs><g filter='url(#region)'><rect width='8' height='8'/><rect x='24' width='8' height='8' visibility='hidden'/></g>").Render(32, 32);
        check(Alpha(hiddenBounds, 6, 4) == 255 && Alpha(hiddenBounds, 26, 4) == 0,
            "Hidden graphics retain their geometry when resolving group filter bounding boxes");
        var hiddenClip = Compile("<defs><clipPath id='hidden'><rect width='8' height='8' visibility='hidden'/></clipPath></defs><rect width='32' height='32' clip-path='url(#hidden)'/>").Render(32, 32);
        check(hiddenClip.All(value => value == 0), "Hidden clip graphics do not expose a painted clipping region");
        foreach (var text in new[] { "translate(4)", "matrix(1 0 0 1 4 0)", "translate(4,0)" })
            check(Vector2.Transform(Vector2.One, SvgTransform.Parse(text)) == new Vector2(5, 1), "Equivalent SVG transform forms agree: " + text);
        check(SvgColor.Parse("#e0302080") == new Vector4(224f / 255, 48f / 255, 32f / 255, 128f / 255)
            && SvgColor.Parse("rgba(100%,0%,0%,50%)") == new Vector4(1, 0, 0, .5f),
            "Managed paints preserve hexadecimal alpha and percentage color channels");
        var overflow = false;
        try
        {
            SvgTransform.Parse("scale(1e38) scale(1e38)");
        }
        catch (InvalidDataException) { overflow = true; }
        check(overflow, "Overflowing transform composition rejects before scene publication");
        var cycle = false;
        try
        {
            Compile("<defs><g id='a'><use href='#a'/></g></defs><use href='#a'/>");
        }
        catch (InvalidDataException) { cycle = true; }
        check(cycle, "Cyclic use references reject at compilation before rendering");
        var linear = Compile("<defs><linearGradient id='g'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect width='32' height='32' fill='url(#g)'/>").Render(32, 32);
        check(Channel(linear, 2, 16, 0) > 230 && Channel(linear, 29, 16, 2) > 230 && Alpha(linear, 16, 16) == 255,
            "Linear gradients interpolate retained sRGB stops in the geometry bounding box");
        var radial = Compile("<defs><radialGradient id='g'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></radialGradient></defs><rect width='32' height='32' fill='url(#g)'/>").Render(32, 32);
        check(Channel(radial, 16, 16, 0) > 240 && Channel(radial, 31, 16, 2) > 240,
            "Radial gradients map authored center, radius and edge stops without native paints");
        var transformedGradient = Compile("<defs><linearGradient id='g' gradientUnits='userSpaceOnUse' x1='0' x2='16'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><g transform='translate(8 0)'><rect width='16' height='32' fill='url(#g)'/></g>").Render(32, 32);
        check(Channel(transformedGradient, 8, 16, 0) > 240 && Channel(transformedGradient, 23, 16, 2) > 240 && Alpha(transformedGradient, 7, 16) == 0,
            "User-space gradients follow object transforms while retaining their declared coordinates");
        var css = Compile("<style>/* retained comment */ rect {fill:blue} .tile {fill:#20c060} #hot {fill:red} g > .tile {stroke:white;stroke-width:2}</style><g><rect class='tile' width='16' height='16'/><rect id='hot' class='tile' x='16' width='16' height='16' style='fill:#2080e0'/></g>").Render(32, 32);
        check(Channel(css, 8, 8, 1) == 192 && Channel(css, 24, 8, 2) == 224 && Channel(css, 16, 8, 0) == 255,
            "Stylesheet specificity, child selectors and inline paints compile before rendering");
        var important = Compile("<style>.tile{fill:red !important}</style><rect class='tile' width='32' height='32' style='fill:blue'/>").Render(32, 32);
        check(Channel(important, 8, 8, 0) == 255 && Channel(important, 8, 8, 2) == 0,
            "Important stylesheet declarations outrank ordinary inline paints");
        var quoted = Compile("<style>.tile{font-family:'/* literal */; url(#not-a-resource)';fill:#20c060}</style><rect class='tile' width='32' height='32'/>").Render(32, 32);
        check(Channel(quoted, 8, 8, 1) == 192, "CSS comments and declaration delimiters inside quoted values retain their literal meaning");
        var clip = Compile("<defs><clipPath id='c'><circle cx='16' cy='16' r='8' fill='none'/></clipPath></defs><rect width='32' height='32' fill='red' clip-path='url(#c)'/>").Render(32, 32);
        check(Alpha(clip, 16, 16) == 255 && Alpha(clip, 4, 16) == 0,
            "Clip geometry masks its target independently of the clip shape's authored paint");
        var mask = Compile("<defs><mask id='m' maskUnits='userSpaceOnUse' x='0' y='0' width='32' height='32'><rect width='16' height='32' fill='white'/></mask></defs><rect width='32' height='32' fill='lime' mask='url(#m)'/>").Render(32, 32);
        check(Channel(mask, 8, 16, 1) == 255 && Alpha(mask, 8, 16) == 255 && Alpha(mask, 24, 16) == 0,
            "Luminance masks preserve authored opaque coverage and transparent exclusions");
        var symbol = Compile("<defs><symbol id='s' viewBox='0 0 8 8'><rect width='8' height='8' fill='currentColor'/></symbol></defs><use href='#s' x='8' y='8' width='16' height='16' color='#20c060'/>").Render(32, 32);
        check(Channel(symbol, 12, 12, 1) == 192 && Alpha(symbol, 25, 12) == 0,
            "Symbol viewBox instances honor their use viewport and inherited currentColor");
        var pattern = Compile("<defs><pattern id='p' patternUnits='userSpaceOnUse' width='8' height='8'><rect width='4' height='8' fill='#20c060'/></pattern></defs><rect width='32' height='32' fill='url(#p)'/>").Render(32, 32);
        check(Channel(pattern, 2, 12, 1) == 192 && Alpha(pattern, 6, 12) == 0 && Channel(pattern, 10, 12, 1) == 192,
            "Retained pattern tiles repeat authored color and transparency at their declared period");
        var marker = Compile("<defs><marker id='arrow' markerUnits='userSpaceOnUse' markerWidth='8' markerHeight='8' refX='0' refY='4' orient='auto'><path d='M0 0L8 4L0 8Z' fill='#e03020'/></marker></defs><path d='M4 16H24' fill='none' stroke='#2080e0' stroke-width='2' marker-end='url(#arrow)'/>").Render(32, 32);
        check(Channel(marker, 12, 16, 2) == 224 && Channel(marker, 26, 16, 0) == 224,
            "Endpoint markers retain independent paint and align with the authored path tangent");
    }
}
