using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class StrokeDashTests
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] Paint(string attributes, string path = "M4 16H60", string cap = "butt", string before = "", string after = "") =>
            SvgManagedScene.Compile(XDocument.Parse("<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'>" + before +
                "<path fill='none' stroke='red' stroke-width='4' stroke-linecap='" + cap + "' d='" + path + "' " + attributes + "/>" + after + "</svg>")).Render(64, 64);
        byte Alpha(byte[] pixels, int x, int y) => pixels[(y * 64 + x) * 4 + 3];
        var basic = Paint("stroke-dasharray='8 4'");
        check(Alpha(basic, 8, 16) == 255 && Alpha(basic, 14, 16) == 0 && Alpha(basic, 20, 16) == 255,
            "SVG dashes and gaps follow authored user-space lengths");
        check(basic.SequenceEqual(Paint("stroke-dasharray='8,4'")) && basic.SequenceEqual(Paint("stroke-dasharray='8px 4px'")),
            "Dash lists accept comma separation and pixel units");
        check(basic.SequenceEqual(Paint("stroke-dasharray='12.5% 6.25%'")),
            "Percentage dashes resolve against the normalized viewport diagonal");
        check(Paint("stroke-dasharray='6 2 3'").SequenceEqual(Paint("stroke-dasharray='6 2 3 6 2 3'")),
            "An odd dash list repeats to form its even pattern");
        check(Paint("stroke-dasharray='0 0'").SequenceEqual(Paint("")) && Paint("stroke-dasharray='none'").SequenceEqual(Paint("")),
            "All-zero and none dash arrays retain solid stroke semantics");
        var positive = Paint("stroke-dasharray='8 4' stroke-dashoffset='2'");
        var negative = Paint("stroke-dasharray='8 4' stroke-dashoffset='-2'");
        check(Alpha(positive, 8, 16) == 255 && Alpha(positive, 11, 16) == 0 && Alpha(negative, 4, 16) == 0 && Alpha(negative, 8, 16) == 255,
            "Positive and negative offsets move the phase in the specified direction");
        check(positive.SequenceEqual(Paint("stroke-dasharray='8 4' stroke-dashoffset='14'")) &&
            negative.SequenceEqual(Paint("stroke-dasharray='8 4' stroke-dashoffset='10'")),
            "Dash offsets normalize to the repeated pattern period");
        check(basic.SequenceEqual(Paint("stroke-dasharray='4 2' pathLength='28'")),
            "Authored pathLength calibrates dash and gap distances");
        var multiple = Paint("stroke-dasharray='8 4'", "M4 16H22 M4 32H60");
        check(Alpha(multiple, 8, 32) == 255 && Alpha(multiple, 14, 32) == 0 && Alpha(multiple, 20, 32) == 255,
            "Each explicit moveto restarts the dash pattern independently");
        var dots = Paint("stroke-dasharray='0 8'", "M8 16H56", "round");
        check(Alpha(dots, 8, 16) == 255 && Alpha(dots, 16, 16) == 255 && Alpha(dots, 48, 16) == 255 && Alpha(dots, 12, 16) == 0,
            "Zero-length round dashes produce visible dots separated by their gaps");
        var squareDots = Paint("stroke-dasharray='0 8'", "M8 16H56", "square");
        check(Alpha(squareDots, 6, 14) == 255 && Alpha(squareDots, 12, 16) == 0,
            "Zero-length square dashes preserve their full cap shape");
        var seam = Paint("stroke-dasharray='100 20'", "M8 8H56V56H8Z");
        check(Alpha(seam, 6, 6) == 255,
            "A dash crossing a closed-path seam preserves the corner join rather than two butt caps");
        check(Paint("stroke-dasharray='1000 1'", "M8 8H56V56H8Z").SequenceEqual(Paint("", "M8 8H56V56H8Z")),
            "A single dash covering a closed contour keeps its complete closed stroke");
        check(Paint("stroke-dasharray='1000 1'", "M4 32Q32 4 60 32").SequenceEqual(Paint("", "M4 32Q32 4 60 32")),
            "Curve dashing reuses the existing retained curve flattening geometry");
        check(basic.SequenceEqual(Paint("", before: "<g stroke-dasharray='8 4'>", after: "</g>")) &&
            basic.SequenceEqual(Paint("class='d'", before: "<style>.d {stroke-dasharray:8 4}</style>")),
            "Dash properties participate in inherited and stylesheet styles");
        var separate = Paint("", "M4 16H16 M16 16V28");
        check(Alpha(separate, 17, 15) == 0,
            "Coincident endpoints of distinct subpaths retain separate caps and do not acquire a synthetic join");
        check(Paint("", "M8 16", "round").All(value => value == 0) && Alpha(Paint("", "M8 16L8 16", "round"), 8, 16) == 255,
            "A moveto alone is empty while a zero-length drawing segment receives its round cap");
        foreach (var invalid in new[] { "stroke-dasharray='-1 4'", "stroke-dasharray='8 4' stroke-dashoffset='1 2'", "stroke-dasharray='1e-30 1e-30'" })
        {
            var rejected = false;
            try { Paint(invalid); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Invalid or excessive dash work rejects before scene publication: " + invalid);
        }
    }
}
