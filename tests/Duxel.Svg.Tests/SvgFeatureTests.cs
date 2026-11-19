using Duxel.Core;
using Duxel.Svg;

internal static class SvgFeatureTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var store = new SvgStore();
        UiSvgRaster Paint(string name, string body)
        {
            var path = Path.Combine(output, "feature-" + name + ".svg");
            File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='64' height='48'>" + body + "</svg>");
            return store.Load(path).Rasterize(64, 48);
        }
        const string triangle = "<path d='M4 4H28V28Z' fill='red'/>";
        const string complement = "<path d='M4 4L28 28H4Z' fill='red'/>";
        var adjacentSamples = Paint("adjacent-samples", triangle + complement);
        check(Channel(adjacentSamples, 16, 16, 3) == 255, "Public SVG adjacent paths retain fully opaque shared sample coverage");
        var duplicateSamples = Paint("duplicate-samples", triangle + triangle);
        check(Channel(duplicateSamples, 16, 16, 3) == 127, "Public SVG repeated opaque paths preserve their original covered samples");
        var alphaMask = Paint("alpha-mask", "<defs><mask id='alpha' mask-type='alpha'>" + triangle + "</mask></defs><g mask='url(#alpha)'>" + triangle + "</g>" + complement);
        check(Channel(alphaMask, 16, 16, 3) == 159, "Public SVG alpha masks resolve their image before subsequent path compositing");
        var symbol = Paint("symbol", "<defs><symbol id='s' viewBox='0 0 10 10'><path d='M0 0H10V10H0Z' fill='currentColor'/></symbol></defs><use href='#s' x='8' y='8' width='20' height='20' color='#20c060'/>");
        var visibility = Paint("visibility", "<style>.off { visibility:hidden !important }</style><g visibility='hidden'><rect width='64' height='48'/><rect x='8' y='8' width='8' height='8' visibility='visible' fill='red'/><rect x='24' width='8' height='8' visibility='visible' class='off'/></g>");
        check(Is(visibility, 4, 4, 0, 0, 0, 0) && Is(visibility, 12, 12, 255, 0, 0, 255) && Is(visibility, 28, 4, 0, 0, 0, 0),
            "Public SVG visibility preserves inherited hiding, visible descendants and stylesheet importance");
        check(Is(symbol, 12, 12, 32, 192, 96, 255) && Is(symbol, 30, 12, 0, 0, 0, 0),
            "SVG symbol/use applies its viewBox, viewport and inherited currentColor");
        var xlink = Paint("xlink", "<defs><rect id='r' width='16' height='16' fill='#e03020'/></defs><use xlink:href='#r' x='32' y='8'/>");
        check(Is(xlink, 36, 12, 224, 48, 32, 255) && Is(xlink, 4, 4, 0, 0, 0, 0),
            "SVG legacy xlink use shares local geometry without drawing its defs directly");
        var pattern = Paint("pattern", "<defs><pattern id='p' patternUnits='userSpaceOnUse' width='8' height='8'><rect width='4' height='8' fill='#20c060'/></pattern></defs><rect width='64' height='48' fill='url(#p)'/>");
        check(Is(pattern, 2, 12, 32, 192, 96, 255) && Is(pattern, 6, 12, 0, 0, 0, 0) && Is(pattern, 10, 12, 32, 192, 96, 255),
            "SVG user-space patterns repeat authored color and transparency at the declared tile period");
        var mask = Paint("mask", "<defs><mask id='m' maskUnits='userSpaceOnUse' x='0' y='0' width='64' height='48'><rect width='32' height='48' fill='white'/></mask></defs><rect width='64' height='48' fill='#20c060' mask='url(#m)'/>");
        check(Is(mask, 16, 24, 32, 192, 96, 255) && Is(mask, 48, 24, 0, 0, 0, 0),
            "SVG luminance mask preserves its visible half and transparent excluded half");
        var radial = Paint("radial", "<defs><radialGradient id='g'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></radialGradient></defs><rect width='64' height='48' fill='url(#g)'/>");
        check(Channel(radial, 32, 24, 0) > 230 && Channel(radial, 60, 24, 2) > 210 && Channel(radial, 32, 24, 3) == 255,
            "SVG radial gradient preserves authored center/edge stops and opacity");
        var marker = Paint("marker", "<defs><marker id='arrow' markerUnits='userSpaceOnUse' markerWidth='8' markerHeight='8' refX='0' refY='4' orient='auto'><path d='M0 0L8 4L0 8Z' fill='#e03020'/></marker></defs><path d='M8 24H48' stroke='#2080e0' stroke-width='2' marker-end='url(#arrow)'/>");
        check(Is(marker, 20, 24, 32, 128, 224, 255) && Is(marker, 50, 24, 224, 48, 32, 255),
            "SVG marker-end uses the endpoint transform and preserves independent stroke/marker colors");
        // Captured before backend removal; see artifacts/svg-managed-2026-11-14/legacy-oracle.json.
        const string encoded = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAABHNCSVQICAgIfAhkiAAAAA1JREFUCJljeGCg0AAABNUBsTmap/UAAAAASUVORK5CYII=";
        var embedded = Paint("image", "<image x='8' y='8' width='16' height='16' preserveAspectRatio='none' href='data:image/png;base64," + encoded + "'/>");
        check(Is(embedded, 16, 16, 223, 48, 32, 128) && Is(embedded, 30, 16, 0, 0, 0, 0),
            "SVG embedded PNG preserves its straight-alpha color, viewport and transparent exterior");
        var comments = Paint("comments", "<style>/* Documentation only: url(https://example.invalid/example.png) @import 'example.css'; @font-face {} */ .tile { fill:#20c060; }</style><rect width='32' height='24' class='tile'/>");
        check(Is(comments, 8, 8, 32, 192, 96, 255),
            "SVG CSS resource validation ignores comments without rejecting a valid stylesheet");
        var quoted = Paint("quoted", "<style>.tile { font-family:'@import url(#missing) @font-face'; fill:#20c060; }</style><rect width='32' height='24' class='tile'/>");
        check(Is(quoted, 8, 8, 32, 192, 96, 255),
            "SVG CSS resource validation distinguishes quoted property text from resource tokens");
        var references = SvgCssResources.Read("/* url(#ignored) */ fill:u\\72l( '#valid' ); font-family:'url(#quoted)'; stroke:URL(\"#second\");").ToArray();
        check(references.SequenceEqual(new[] { "#valid", "#second" }),
            "SVG CSS resource tokens retain escaped identifiers, quoted URLs and original reference order");
        check(SvgCssResources.Read("fill:u\\72\r\nl(#valid)").Single() == "#valid" &&
            SvgCssResources.Read("fill:url('#a\\29 b')").Single() == "#a)b",
            "SVG CSS escapes consume CRLF terminators and preserve escaped URL punctuation");
        var rejectedCss = new[]
        {
            "fill:url(https://example.invalid/image.png)",
            "fill:u\\72l('https://example.invalid/image.png')",
            "/* comment */ @im\\70ort 'https://example.invalid/style.css';",
            "@\\66ont-face {font-family:Other;src:local(Other)}",
            "fill:url(#missing)",
            "fill:url(",
            "fill:url('#missing' trailing)",
            "font-family:'unterminated",
            "/* unterminated"
        };
        for (var index = 0; index < rejectedCss.Length; index++)
        {
            var rejected = false;
            var source = "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='48'><style>.tile{" + rejectedCss[index] + "}</style><rect class='tile' width='32' height='24'/></svg>";
            try
            {
                SvgSource.Parse(System.Text.Encoding.UTF8.GetBytes(source), 65536);
            }
            catch (Exception error) when (error is NotSupportedException or InvalidDataException) { rejected = true; }
            check(rejected, "SVG CSS rejects active unsupported or malformed resources before backend execution : " + index);
        }
        var incompleteEscape = false;
        try
        {
            SvgCssResources.Read("fill:incomplete\\").ToArray();
        }
        catch (InvalidDataException) { incompleteEscape = true; }
        check(incompleteEscape, "SVG CSS resource tokens reject an actual end-of-input escape");
    }

    private static byte Channel(UiSvgRaster image, int x, int y, int channel) => image.Pixels.Span[(y * image.Width + x) * 4 + channel];
    private static bool Is(UiSvgRaster image, int x, int y, int r, int g, int b, int a) =>
        Math.Abs(Channel(image, x, y, 0) - r) <= 1 && Math.Abs(Channel(image, x, y, 1) - g) <= 1 &&
        Math.Abs(Channel(image, x, y, 2) - b) <= 1 && Math.Abs(Channel(image, x, y, 3) - a) <= 1;
}
