using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class PaintEffectTests
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] sourcePixels = [224, 48, 32, 128, 0, 0, 255, 0];
        var premultiplied = new byte[sourcePixels.Length];
        Duxel.Core.UiRgba8.Premultiply(sourcePixels, premultiplied);
        check(premultiplied.SequenceEqual(new byte[] { 112, 24, 16, 128, 0, 0, 0, 0 }) && sourcePixels[6] == 255,
            "Core RGBA8 conversion clears invisible color and preserves immutable source bytes for SVG and GPU uploads");
        var alphaScene = SvgManagedScene.Compile(XDocument.Parse("<svg width='1' height='1'><rect width='1' height='1' fill='red' opacity='.5'/></svg>"));
        var opaqueTarget = alphaScene.Render(1, 1, System.Numerics.Vector4.One);
        check(opaqueTarget.SequenceEqual(new byte[] { 255, 128, 128, 255 }),
            "Opaque render-target compositing precedes RGBA8 export and avoids an extra alpha quantization stage");
        var analyticRectangle = SvgManagedScene.Compile(XDocument.Parse("<svg width='64' height='64'><rect x='48.25' y='.25' width='7.5' height='7.5'/></svg>"));
        var rectangleTarget = analyticRectangle.Render(64, 64, System.Numerics.Vector4.One);
        // Independent native Chromium capture: paint-calibration.svg, pixel(48,0) = (64,64,64,255).
        check(rectangleTarget[48 * 4] == 64 && rectangleTarget[48 * 4 + 1] == 64 && rectangleTarget[48 * 4 + 2] == 64,
            "Fractional rectangle corners match the captured browser analytic edge coverage");
        var clippedScene = SvgManagedScene.Compile(XDocument.Parse("<svg width='32' height='16'><rect x='-100' y='-100' width='232' height='216' fill='red'/></svg>"));
        var viewport = clippedScene.Render(32, 32);
        check(viewport[3] == 0 && viewport[(16 * 32 + 16) * 4] == 255 && viewport[(31 * 32 + 16) * 4 + 3] == 0,
            "Intrinsic viewport clipping prevents off-document geometry from bleeding into letterbox padding");
        var authoredStroke = SvgManagedScene.Compile(XDocument.Parse("<svg width='32' height='32'><path d='M4 16Q16 0 28 16' fill='none' stroke='red' stroke-width='2' stroke-linecap='round'/></svg>"));
        var enlargedStroke = SvgManagedScene.Compile(XDocument.Parse("<svg width='512' height='512'><path d='M64 256Q256 0 448 256' fill='none' stroke='red' stroke-width='32' stroke-linecap='round'/></svg>"));
        check(authoredStroke.Render(512, 512).SequenceEqual(enlargedStroke.Render(512, 512)),
            "Sixteen-fold stroke enlargement matches independently scaled authored geometry and round caps pixel for pixel");
        byte[] Paint(string body) => SvgManagedScene.Compile(XDocument.Parse("<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'>" + body + "</svg>")).Render(32, 32);
        byte A(byte[] pixels, int x, int y) => pixels[(y * 32 + x) * 4 + 3];
        var stops = "<stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/>";
        var plain = Paint("<defs><linearGradient id='g'>" + stops + "</linearGradient></defs><rect width='32' height='32' fill='url(#g)'/>");
        var inherited = Paint("<defs><linearGradient id='a'>" + stops + "</linearGradient><linearGradient id='b' href='#a'/><linearGradient id='g' href='#b'><title>color</title></linearGradient></defs><rect width='32' height='32' fill='url(#g)'/>");
        check(plain.SequenceEqual(inherited), "Multi-hop gradient templates retain stops while descriptive content does not replace them");
        var user = Paint("<defs><linearGradient id='g' gradientUnits='userSpaceOnUse'>" + stops + "</linearGradient></defs><rect width='32' height='32' fill='url(#g)'/>");
        var percent = Paint("<defs><linearGradient id='g' gradientUnits='userSpaceOnUse' x2='100%'>" + stops + "</linearGradient></defs><rect width='32' height='32' fill='url(#g)'/>");
        check(plain.SequenceEqual(user) && plain.SequenceEqual(percent), "User-space gradient defaults and percentages use the viewport rather than one user unit");
        var sampled = SvgRasterSampling.Bilinear([255, 0, 0, 255, 0, 0, 255, 0], 2, 1, .25f, 0, repeat: false);
        check(sampled.X == 1 && sampled.Y == 0 && sampled.Z == 0 && sampled.W == .75f,
            "Shared bitmap/pattern interpolation filters premultiplied color without transparent blue fringes");
        var degenerate = Paint("<defs><radialGradient id='g' r='0'>" + stops + "</radialGradient></defs><rect width='32' height='32' fill='url(#g)'/>");
        check(degenerate[2] == 255 && degenerate[0] == 0, "A zero-radius radial gradient uses its final stop color");
        var tile = "<rect width='4' height='8' fill='red'/>";
        var pattern = Paint("<defs><pattern id='g' patternUnits='userSpaceOnUse' width='8' height='8'>" + tile + "</pattern></defs><rect width='32' height='32' fill='url(#g)'/>");
        var template = Paint("<defs><pattern id='a' patternUnits='userSpaceOnUse' width='8' height='8'>" + tile + "</pattern><pattern id='g' href='#a'/></defs><rect width='32' height='32' fill='url(#g)'/>");
        check(pattern.SequenceEqual(template) && A(pattern, 2, 2) == 255 && A(pattern, 6, 2) == 0, "Pattern templates retain tile dimensions and graphic content");
        var metadata = Paint("<editor:namedview xmlns:editor='urn:editor'><rect width='32' height='32'/></editor:namedview><rect width='4' height='4'/>");
        check(A(metadata, 2, 2) == 255 && A(metadata, 10, 10) == 0, "Foreign editor data and its descendants do not become SVG graphics");
        var foreignCss = Paint("<editor:style xmlns:editor='urn:editor'>rect{fill:blue}</editor:style><metadata><style>invalid private stylesheet</style></metadata><rect width='32' height='32' fill='red'/>");
        check(foreignCss[0] == 255 && foreignCss[2] == 0, "Foreign and metadata stylesheets cannot leak into the rendered SVG cascade");
        var blurDefinition = "<defs><filter id='b' filterUnits='userSpaceOnUse' x='0' y='0' width='32' height='32'><feGaussianBlur stdDeviation='2'/></filter></defs>";
        var clippedChild = "<g clip-path='url(#c)'><rect width='32' height='32'/></g>";
        var clipDefinition = "<clipPath id='c'><rect x='8' y='8' width='8' height='8'/></clipPath>";
        var objectFilter = Paint("<defs>" + clipDefinition + "<filter id='b' x='0' y='0' width='1' height='1'><feGaussianBlur stdDeviation='2'/></filter></defs><g filter='url(#b)'>" + clippedChild + "</g>");
        var explicitFilter = Paint(blurDefinition + "<defs>" + clipDefinition + "</defs><g filter='url(#b)'>" + clippedChild + "</g>");
        check(objectFilter.SequenceEqual(explicitFilter),
            "Geometric clip folding preserves authored object bounds for parent filter regions");
        var blur = Paint(blurDefinition + "<rect x='12' y='12' width='8' height='8' filter='url(#b)'/>");
        check(A(blur, 9, 16) > 0 && A(blur, 16, 16) > 200 && A(blur, 16, 16) < 255,
            "Gaussian filtering spreads premultiplied alpha outside the source geometry");
        var shifted = Paint("<defs><filter id='b' filterUnits='userSpaceOnUse' x='-16' y='0' width='64' height='32'><feGaussianBlur stdDeviation='2'/></filter></defs>" +
            "<rect x='-6' y='12' width='4' height='8' filter='url(#b)'/>");
        check(A(shifted, 0, 16) > 5, "Off-viewport source pixels contribute through blur before the final viewport composite");
        var transparent = Paint(blurDefinition + "<g opacity='.5'><rect x='12' y='12' width='8' height='8' filter='url(#b)'/></g>");
        check(Math.Abs(A(transparent, 16, 16) * 2 - A(blur, 16, 16)) <= 1, "Group opacity applies after its child filter");
        foreach (var body in new[]
        {
            "<defs><linearGradient id='a' href='#b'/><linearGradient id='b' href='#a'/></defs><rect width='32' height='32' fill='url(#a)'/>",
            "<defs><pattern id='a' href='#missing'/></defs><rect width='32' height='32' fill='url(#a)'/>",
            "<defs><filter id='b'><feGaussianBlur stdDeviation='-1'/></filter></defs><rect width='32' height='32' filter='url(#b)'/>",
            "<defs><filter id='b'><feTurbulence/></filter></defs><rect width='32' height='32' filter='url(#b)'/>"
        })
        {
            var rejected = false;
            try
            {
                Paint(body);
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException) { rejected = true; }
            check(rejected, "Invalid or unsupported paint/effect fails explicitly: " + body);
        }
    }
}
