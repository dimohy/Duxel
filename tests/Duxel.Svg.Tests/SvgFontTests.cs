using System.Text;
using Duxel.Svg;
using System.Security.Cryptography;

internal static class SvgFontTests
{
    internal static SvgFontStatistics Run(string output, Action<bool, string> check)
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(directory, "segoeui.ttf");
        var bold = Path.Combine(directory, "segoeuib.ttf");
        var korean = Path.Combine(directory, "malgun.ttf");
        foreach (var file in new[] { regular, bold, korean })
            if (!File.Exists(file))
                throw new FileNotFoundException("SVG font fixture requires its explicit registered font file.", file);
        using var store = new SvgStore(fonts: new("UI",
            [new("UI", regular), new("UI", bold), new("Korean", korean), new("Quoted, Family", regular)]));
        var path = Path.Combine(output, "font-text.svg");
        const string plain = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='80'><text x='8' y='52' font-size='36' fill='#e03020'>ABC</text></svg>";
        File.WriteAllText(path, plain, Encoding.UTF8);
        var asset = store.Load(path);
        check(asset.IntrinsicSize == new Duxel.Core.UiVector2(200, 80), "SVG text preserves the authored viewport dimensions : " + asset.IntrinsicSize);
        var raster = asset.Rasterize(200, 80);
        File.WriteAllBytes(Path.Combine(output, "font-reference.rgba"), raster.Pixels.ToArray());
        var visible = 0;
        for (var index = 3; index < raster.Pixels.Length; index += 4)
        if (raster.Pixels.Span[index] != 0)
            visible++;
        check(visible > 400 && raster.Pixels.Span[3] == 0 && store.Statistics is { XmlParses: 1, SceneCompiles: 1 },
            "SVG text renders with explicitly registered default font and transparent exterior");
        var bitmapDecoder = new BitmapDecoder();
        var decoder = new SvgImageDecoder(store, bitmapDecoder);
        var image = decoder.Decode(path);
        check(image.Width == 200 && image.Height == 80 && image.RgbaPixels.AsSpan().SequenceEqual(raster.Pixels.Span) &&
            bitmapDecoder.Calls == 0,
            "SVG text ordinary bitmap decoding matches the retained document pixels without a second font or XML path");
        var previousDecoder = Duxel.Core.UiImageTexture.ImageDecoder;
        try
        {
            Duxel.Core.UiImageTexture.ImageDecoder = decoder;
            var texture = Duxel.Core.UiImageTexture.LoadFromFile(path, new(102));
            check(texture.Width == 200 && texture.Height == 80 && store.Statistics is { XmlParses: 1, SceneCompiles: 1 },
                "the ordinary image-file API accepts SVG text through the same explicitly registered font resource");
        }
        finally { Duxel.Core.UiImageTexture.ImageDecoder = previousDecoder; }
        {
            // Independent native outline captured before removing the old renderer.
            check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(regular))) == "704EADA12961A27FD271F7D3B09E2066520E758390C1776744EFF430FEE30F6E",
                "The explicit reference font matches the preserved independent-outline oracle");
            var left = 200;
            var top = 80;
            var right = 0;
            var bottom = 0;
            for (var y = 0; y < 80; y++)
                for (var x = 0; x < 200; x++)
                    if (raster.Pixels.Span[(y * 200 + x) * 4 + 3] != 0)
                    {
                        left = Math.Min(left, x);
                        top = Math.Min(top, y);
                        right = Math.Max(right, x + 1);
                        bottom = Math.Max(bottom, y + 1);
                    }
            check(Math.Abs(left - 8.386719) < 2 && Math.Abs(top - 26.371094) < 2 &&
                Math.Abs(right - 72.31836) < 2 && Math.Abs(bottom - 52.421875) < 2,
                "SVG Latin text baseline and glyph bounds match an independent native font outline");
        }
        var fonts = store.FontStatistics;
        using (var referenceStore = new SvgStore())
        {
            var reference = referenceStore.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SvgFontOutlineReference.svg")).Rasterize(200, 80);
            var equal = true;
            for (var index = 0; index < raster.Pixels.Length; index++)
                equal &= Math.Abs(raster.Pixels.Span[index] - reference.Pixels.Span[index]) <= 1;
            check(equal, "SVG text full RGBA pixels match the preserved independent native font geometry");
        }
        for (var repeat = 0; repeat < 100; repeat++)
            asset.Rasterize(200, 80);
        asset.Rasterize(400, 160);
        check(store.FontStatistics == fonts && fonts is { LoadedFaces: 3, GlyphChecks: > 0 } &&
            store.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 2 },
            "SVG text scaling/replay performs no font discovery, glyph validation or XML processing after compilation");
        var boldPath = Path.Combine(output, "font-bold.svg");
        File.WriteAllText(boldPath, plain.Replace("font-size='36'", "font-size='36' font-weight='700'", StringComparison.Ordinal));
        var boldRaster = store.Load(boldPath).Rasterize(200, 80);
        check(!boldRaster.Pixels.Span.SequenceEqual(raster.Pixels.Span),
            "SVG font-weight selects an explicitly registered bold face rather than synthesizing an unrelated style");
        foreach (var family in new[] { "Unknown, UI", "&quot;Quoted, Family&quot;" })
        {
            var familyPath = Path.Combine(output, "font-family-" + (family.StartsWith("Unknown", StringComparison.Ordinal) ? "stack" : "quoted") + ".svg");
            File.WriteAllText(familyPath, plain.Replace("font-size='36'", "font-size='36' font-family='" + family + "'", StringComparison.Ordinal));
            check(store.Load(familyPath).Rasterize(200, 80).Pixels.Span.SequenceEqual(raster.Pixels.Span),
                "SVG family parsing preserves explicitly registered font output : " + family);
        }
        var mixedPath = Path.Combine(output, "font-mixed.svg");
        File.WriteAllText(mixedPath, "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='80'><style>.label{font-family:'UI','Korean';font-size:32px;fill:#20c060}</style><g class='label'><text x='8' y='50'>A한B글</text></g></svg>");
        var mixed = store.Load(mixedPath).Rasterize(240, 80);
        var explicitPath = Path.Combine(output, "font-explicit-spans.svg");
        File.WriteAllText(explicitPath, "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='80'><text font-family='UI' font-size='32' fill='#20c060' x='8' y='50'>A<tspan font-family='Korean'>한</tspan><tspan font-family='UI'>B</tspan><tspan font-family='Korean'>글</tspan></text></svg>");
        var explicitSpans = store.Load(explicitPath).Rasterize(240, 80);
        check(mixed.Pixels.Span.SequenceEqual(explicitSpans.Pixels.Span),
            "SVG CSS/inherited mixed-script family stack matches explicit authored per-font tspans pixel-for-pixel");
        var spanPath = Path.Combine(output, "font-spans.svg");
        File.WriteAllText(spanPath, "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='100'><text x='8' y='40' font-size='28'>ABC<tspan fill='#2080e0' font-family='Korean' dx='8'>한글</tspan><tspan x='8' y='80' font-weight='700'>Bold</tspan></text></svg>");
        check(store.Load(spanPath).Rasterize(240, 100).Pixels.Span.Contains((byte)224),
            "SVG tspan retains explicit font/style, positioning and authored colors");
        var curvePath = Path.Combine(output, "font-text-path.svg");
        File.WriteAllText(curvePath, "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='100'><defs><path id='curve' d='M8 64 Q100 0 224 64'/></defs><text font-size='24'><textPath href='#curve'>ABC SVG</textPath></text></svg>");
        check(store.Load(curvePath).Rasterize(240, 100).Pixels.Span.Contains((byte)255),
            "SVG textPath uses the explicit shaped font along retained path geometry");
        var rtlPath = Path.Combine(output, "font-rtl.svg");
        File.WriteAllText(rtlPath, "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='100'><text x='200' y='60' font-size='36' direction='rtl'>سلام</text></svg>");
        check(store.Load(rtlPath).Rasterize(240, 100).Pixels.Span.Contains((byte)255),
            "SVG right-to-left text uses registered glyph coverage and backend shaping");
        foreach (var (name, content) in new[]
        {
            ("unknown-family", plain.Replace("font-size='36'", "font-size='36' font-family='NotRegistered'", StringComparison.Ordinal)),
            ("unknown-style", plain.Replace("font-size='36'", "font-size='36' font-weight='900'", StringComparison.Ordinal)),
            ("missing-glyph", plain.Replace("ABC", "\u0378", StringComparison.Ordinal))
        })
        {
            File.WriteAllText(path, content);
            var rejected = false;
            try
            {
                store.Reload(path);
            }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && asset.Revision == 0 && ReferenceEquals(raster, asset.Rasterize(200, 80)),
                "invalid SVG text rejects before scene publication and preserves committed resource : " + name);
        }
        var invalidDefault = false;
        try
        {
            using var invalid = new SvgStore(fonts: new("Missing", [new("UI", regular)]));
        }
        catch (ArgumentException) { invalidDefault = true; }
        check(invalidDefault, "SVG font options reject an unregistered default family before loading a document");
        var faceRule = Path.Combine(output, "font-rule.svg");
        File.WriteAllText(faceRule, plain.Replace("<text", "<style>@font-face{font-family:UI;src:local('Other')}</style><text", StringComparison.Ordinal));
        var unsupportedFace = false;
        try
        {
            store.Load(faceRule);
        }
        catch (NotSupportedException) { unsupportedFace = true; }
        check(unsupportedFace, "unsupported SVG font-face rules reject explicitly instead of silently overriding registered font selection");
        var fontCopy = Path.Combine(output, "snapshot-font.ttf");
        File.Copy(regular, fontCopy, overwrite: true);
        using (var snapshot = new SvgStore(fonts: new("UI", [new("UI", fontCopy)])))
        {
            var snapshotSvg = Path.Combine(output, "font-snapshot.svg");
            File.WriteAllText(snapshotSvg, plain);
            var retained = snapshot.Load(snapshotSvg);
            var originalPixels = retained.Rasterize(200, 80);
            var originalFonts = snapshot.FontStatistics;
            File.WriteAllBytes(fontCopy, [0, 1, 2, 3]);
            retained.Rasterize(400, 160);
            check(snapshot.FontStatistics == originalFonts && ReferenceEquals(originalPixels, retained.Rasterize(200, 80)),
                "SVG compiled fonts are immutable snapshots and scaling does not reread changed font files");
            var badFont = false;
            try
            {
                using var invalid = new SvgStore(fonts: new("UI", [new("UI", fontCopy)]));
            }
            catch (InvalidDataException) { badFont = true; }
            check(badFont, "a new SVG store rejects invalid explicit font bytes instead of selecting an installed font");
        }
        byte[] VisibilityText(string name, string attributes, string content)
        {
            var file = Path.Combine(output, "font-visibility-" + name + ".svg");
            File.WriteAllText(file, "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='80'><text x='8' y='52' font-size='36' " + attributes + ">" + content + "</text></svg>");
            return store.Load(file).Rasterize(200, 80).Pixels.ToArray();
        }
        check(VisibilityText("root", "visibility='hidden'", "ABC").All(value => value == 0),
            "Hidden SVG text retains layout without painting its glyphs");
        var visibleChild = VisibilityText("override", "visibility='hidden'", "<tspan visibility='visible'>A</tspan><tspan>BC</tspan>");
        check(visibleChild.AsSpan().SequenceEqual(VisibilityText("literal", "", "A")),
            "Visible text spans override a hidden parent without revealing adjacent glyphs");
        var hiddenAdvance = VisibilityText("advance", "", "<tspan visibility='hidden'><tspan visibility='unset'>A</tspan></tspan>B");
        check(hiddenAdvance.AsSpan().SequenceEqual(VisibilityText("transparent-advance", "", "<tspan fill-opacity='0'>A</tspan>B")),
            "Nested unset visibility preserves hidden-span inheritance and the following glyph's original advance");
        return fonts;
    }
}
