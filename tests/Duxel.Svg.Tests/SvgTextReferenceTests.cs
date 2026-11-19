using Duxel.Svg;

internal static class SvgTextReferenceTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        using var store = new SvgStore(fonts : new("UI", [new("UI", fontPath)]));
        const string head = "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='200' height='80'>";
        const string textStart = "<text x='8' y='52' font-size='36' fill='#e03020'>";
        string Write(string name, string body)
        {
            var path = Path.Combine(output, "text-reference-" + name + ".svg");
            File.WriteAllText(path, head + body + "</svg>");
            return path;
        }
        var literal = store.Load(Write("literal", textStart + "ABC</text>"));
        var expected = literal.Rasterize(200, 80);
        foreach (var attribute in new[] { "href", "xlink:href" })
        {
            var path = Write(attribute == "href" ? "href" : "xlink",
                "<defs><text id='label' fill='blue' font-size='8'>ABC</text></defs>" + textStart + "<tref " + attribute + "='#label'/></text>");
            var asset = store.Load(path);
            check(asset.Rasterize(200, 80).Pixels.Span.SequenceEqual(expected.Pixels.Span),
                "SVG tref characters use the referring text style and match explicit literal pixels : " + attribute);
            var source = store.Statistics;
            var fonts = store.FontStatistics;
            asset.Rasterize(400, 160);
            for (var replay = 0; replay < 100; replay++) asset.Rasterize(200, 80);
            check(store.Statistics.XmlParses == source.XmlParses && store.Statistics.SceneCompiles == source.SceneCompiles &&
                store.Statistics.Rasterizations == source.Rasterizations + 1 && store.FontStatistics == fonts,
                "SVG tref replay and enlargement resolve neither source XML nor fonts again : " + attribute);
        }
        var spans = store.Load(Write("nested", "<defs><text id='label'>A<tspan fill='blue'>B</tspan>C</text></defs>" + textStart + "<tref href='#label'/></text>"));
        check(spans.Rasterize(200, 80).Pixels.Span.SequenceEqual(expected.Pixels.Span),
            "SVG tref gathers referenced nested characters without inheriting the target tspan paint");
        const string curve = "<defs><path id='curve' d='M8 64Q100 0 190 64'/><text id='label'>ABC SVG</text></defs>";
        const string curveText = "<text font-size='24' fill='#e03020'><textPath href='#curve'>";
        var literalCurve = store.Load(Write("curve-literal", curve + curveText + "ABC SVG</textPath></text>"));
        var referencedCurve = store.Load(Write("curve-reference", curve + curveText + "<tref href='#label'/></textPath></text>"));
        var actualCurve = referencedCurve.Rasterize(200, 80);
        var expectedCurve = literalCurve.Rasterize(200, 80);
        File.WriteAllBytes(Path.Combine(output, "curve-reference.rgba"), actualCurve.Pixels.ToArray());
        File.WriteAllBytes(Path.Combine(output, "curve-literal.rgba"), expectedCurve.Pixels.ToArray());
        check(actualCurve.Pixels.Span.SequenceEqual(expectedCurve.Pixels.Span),
            "SVG tref nested in textPath preserves the same shaped curved-text pixels as literal characters");
        var styledReference = store.Load(Write("curve-styled-reference", curve +
            "<style>tref.special#styledReference {fill:#20c060;font-size:28px} tspan {fill:blue}</style>" + curveText +
            "<tref id='styledReference' class='special' href='#label' dx='3' dy='-4'/></textPath></text>"));
        var styledLiteral = store.Load(Write("curve-styled-literal", curve + curveText +
            "<tspan fill='#20c060' font-size='28' dx='3' dy='-4'>ABC SVG</tspan></textPath></text>"));
        check(styledReference.Rasterize(200, 80).Pixels.Span.SequenceEqual(styledLiteral.Rasterize(200, 80).Pixels.Span),
            "SVG tref compilation preserves original CSS selector identity, inherited style and positioning attributes");
        var entityReference = store.Load(Write("entities-reference", "<defs><text id='label'>A&amp;B</text></defs>" + textStart +
            "<tref href='#label'/> | <tref href='#label'/></text>"));
        var entityLiteral = store.Load(Write("entities-literal", textStart + "A&amp;B | A&amp;B</text>"));
        check(entityReference.Rasterize(200, 80).Pixels.Span.SequenceEqual(entityLiteral.Rasterize(200, 80).Pixels.Span),
            "SVG repeated tref nodes preserve entity-decoded characters and surrounding sibling text");
        var beforeReplay = store.Statistics;
        var beforeFonts = store.FontStatistics;
        referencedCurve.Rasterize(400, 160);
        var decoder = new SvgImageDecoder(store, new BitmapDecoder());
        var bitmap = decoder.Decode(referencedCurve.SourcePath);
        for (var replay = 0; replay < 100; replay++) referencedCurve.Rasterize(200, 80);
        check(bitmap.RgbaPixels.AsSpan().SequenceEqual(expectedCurve.Pixels.Span) && store.FontStatistics == beforeFonts &&
            store.Statistics.XmlParses == beforeReplay.XmlParses && store.Statistics.SceneCompiles == beforeReplay.SceneCompiles &&
            store.Statistics.Rasterizations == beforeReplay.Rasterizations + 1,
            "SVG curved tref shares ordinary bitmap pixels and retained scaling/replay without XML or font resolution");
        var changed = Write("curve-reference", curve.Replace("ABC SVG", "XYZ SVG", StringComparison.Ordinal) + curveText +
            "<tref href='#label'/></textPath></text>");
        var expectedChanged = store.Load(Write("curve-changed-literal", curve + curveText + "XYZ SVG</textPath></text>"));
        store.Reload(changed);
        check(referencedCurve.Revision == 1 && referencedCurve.Rasterize(200, 80).Pixels.Span.SequenceEqual(expectedChanged.Rasterize(200, 80).Pixels.Span)
            && !actualCurve.Pixels.Span.SequenceEqual(referencedCurve.Rasterize(200, 80).Pixels.Span),
            "SVG source reload recompiles changed referenced characters while preserving earlier immutable pixels");
    }
}
