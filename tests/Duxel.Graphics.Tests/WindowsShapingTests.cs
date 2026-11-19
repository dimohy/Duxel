using Duxel.Core;
using Duxel.Platform.Windows;

internal static class WindowsShapingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var latin = Path.Combine(fonts, "segoeui.ttf");
        var korean = Path.Combine(fonts, "malgun.ttf");
        var arabic = Path.Combine(fonts, "arial.ttf");
        foreach (var path in new[] { latin, korean, arabic })
            if (!File.Exists(path))
                throw new FileNotFoundException("Shaping fixture font is required.", path);
        var backend = WindowsPlatformTextBackend.Instance;
        void Test(Func<bool> probe, string name)
        {
            try
            {
                check(probe(), name);
            }
            catch (NotSupportedException error) { check(false, name + " : " + error.Message); }
            catch (InvalidOperationException error) { check(false, name + " : " + error.Message); }
            catch (ArgumentException error) { check(false, name + " : " + error.Message + " / HRESULT : " + error.HResult.ToString("X8")); }
            catch (System.Runtime.InteropServices.ExternalException error) { check(false, name + " : " + error.Message + " / HRESULT : " + error.HResult.ToString("X8")); }
            catch (InvalidDataException error) { check(false, name + " : " + error.Message); }
        }
        PlatformTextMeshResult Mesh(string font, string text) => backend.CreateTextMesh(new(font, null, text, 32, .001f));
        static bool Same(PlatformTextMeshResult a, PlatformTextMeshResult b) => a.Width == b.Width && a.Glyphs.Length == b.Glyphs.Length
            && a.Glyphs.Span.ToArray().Zip(b.Glyphs.Span.ToArray()).All(pair => pair.First.Offset == pair.Second.Offset
                && pair.First.Vertices.Span.SequenceEqual(pair.Second.Vertices.Span) && pair.First.Indices.Span.SequenceEqual(pair.Second.Indices.Span));
        Test(() => Same(Mesh(korean, "한"), Mesh(korean, "한")), "vector shaping composes Hangul Jamo with the same glyphs and advances as NFC");
        Test(() => Same(Mesh(latin, "é"), Mesh(latin, "e\u0301")), "vector shaping composes Latin base and combining acute without advancing the mark");
        Test(() => Mesh(latin, "AV").Width < Mesh(latin, "A").Width + Mesh(latin, "V").Width,
            "vector shaping applies the font's contextual pair placement");
        Test(() => Mesh(arabic, "لا").Width < Mesh(arabic, "ل").Width + Mesh(arabic, "ا").Width, "vector shaping substitutes the Arabic lam-alef ligature");
        Test(() => Mesh(arabic, "ل\u200Cا").Width > Mesh(arabic, "لا").Width,
            "Arabic zero-width non-joiner preserves its shaping effect");
        Test(() => backend.TryRasterize(new(korean, null, "한", 32), out var nfc)
            && backend.TryRasterize(new(korean, null, "한", 32), out var nfd)
            && nfc.Width == nfd.Width && nfc.Height == nfd.Height && nfc.RgbaPixels.Span.SequenceEqual(nfd.RgbaPixels.Span),
            "raster shaping renders NFC and NFD Hangul to identical pixels");
        Test(() => WindowsDirectWriteGlyphRasterizer.ShapeForValidation(korean, "한", 32) is [{ Cluster: 0 }],
            "decomposed Hangul retains the original UTF16 cluster start");
        Test(() => WindowsDirectWriteGlyphRasterizer.ShapeForValidation(latin, "e\u0301", 32) is [{ Cluster: 0 }],
            "combining acute retains the original UTF16 source cluster");
        Test(() =>
        {
            var glyphs = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(arabic, "ل\u200Cا", 32);
            return glyphs.Any(glyph => glyph.Advance == 0) && glyphs.All(glyph => glyph.Cluster < 3);
        }, "nonjoining control retains a legal zero advance and original cluster range");
        Test(() =>
        {
            var glyphs = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(arabic, "abc لا 123", 32, true);
            return glyphs.All(glyph => glyph.Cluster < 10 && float.IsFinite(glyph.Advance))
                && glyphs.Any(glyph => glyph.Cluster == 0) && glyphs.Any(glyph => glyph.Cluster == 7);
        }, "mixed bidi analysis preserves original Latin and digit source positions");
        Test(() =>
        {
            var glyphs = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(latin, "\u200B", 32);
            return glyphs.All(glyph => glyph.Advance == 0) && Mesh(latin, "\u200B").Width == 0;
        }, "zero-width format text does not acquire a fabricated half-em advance");
        Test(() =>
        {
            foreach (var size in new[] { 8f, 32f })
            {
                const string single = "\u200B";
                var multiple = new string('\u200B', 40000);
                if (!backend.TryLayout(new(latin, null, single, size), out var expected)
                    || !backend.TryLayout(new(latin, null, multiple, size), out var actual)
                    || expected != actual || actual.Width != 0 || actual.Height != 0 || actual.Advance != 0 || actual.FontAscent <= 0)
                    return false;
                if (!backend.TryRasterize(new(latin, null, multiple, size), out var raster))
                    return false;
                try
                {
                    if (raster.Width != 0 || raster.Height != 0 || !raster.RgbaPixels.IsEmpty || raster.Advance != 0
                        || raster.Baseline != actual.Baseline || raster.FontAscent != actual.FontAscent)
                        return false;
                }
                finally { raster.PixelOwner?.Dispose(); }
            }
            return true;
        }, "public layout and raster preserve empty no-visual results across short/long and oversampled/normal sizes");
        foreach (var selector in new[] { "\u180B", "\u180F", "\uFE0F", "\U000E0100" })
            Test(() =>
            {
                var text = "A" + selector;
                var runs = WindowsPlatformTextBackend.BuildFontRuns(latin, null, text);
                return runs.Count == 1 && runs[0].Text == text && runs[0].FontPath == latin
                    && backend.TryLayout(new(latin, null, text, 32), out var layout) && layout.Advance > 0;
            }, "Native variation selector retains its base font and original source : " + char.ConvertToUtf32(selector, 0));
        foreach (var layoutCapability in new[] { true, false })
            Test(() =>
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.Configure(context);
                if (!layoutCapability)
                    context.SetPlatformTextBackend(new RasterOnlyBackend());
                using var text = new UiInlineText([new("\u200B")]);
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                var builds = -1;
                for (var frame = 0; frame < 2; frame++)
                {
                    var size = default(UiVector2);
                    context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(new MeasureScreen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        size = text.Measure(ui, new(400, 300));
                    }));
                    using var draw = context.GetDrawData();
                    if (size.X != 0 || size.Y != 20 || text.VisualLines.Width != 0
                        || frame > 0 && text.VisualLines.Builds != builds)
                        return false;
                    builds = text.VisualLines.Builds;
                }
                return true;
            }, "Core preserves zero logical advance and retained layout without fabricated padding : layout capability " + layoutCapability);
        DirectTextLayoutTests.VerifyAdvanceContracts(check);
        Test(() =>
        {
            try
            {
                Mesh(latin, "\U0010FFFF");
                return false;
            }
            catch (NotSupportedException) { return true; }
        }, "absent selected-font glyph fails explicitly instead of replacing source text");
        Test(() =>
        {
            for (var syllable = 0xAC00; syllable <= 0xD7A3; syllable++)
            {
                var expected = ((char)syllable).ToString();
                var source = expected.Normalize(System.Text.NormalizationForm.FormD);
                var input = Duxel.Interop.Windows.DirectWriteHangulInput.Prepare(source);
                if (input.Text != expected || input.OriginalPositions is not { } positions || positions[0] != 0)
                    return false;
            }
            return true;
        }, "all11172 modern Hangul syllables compose against the independent Unicode normalization oracle with original starts");
        Test(() =>
        {
            var input = Duxel.Interop.Windows.DirectWriteHangulInput.Prepare("A한🙂e\u0301");
            return input.Text == "A한🙂e\u0301" && input.OriginalPositions is { } positions
                && positions.AsSpan(0, input.Text.Length).SequenceEqual(new uint[] { 0, 1, 4, 5, 6, 7 });
        }, "Hangul glyph input preserves following surrogate and combining-character UTF16 source positions");
        Test(() =>
        {
            var partial = Duxel.Interop.Windows.DirectWriteHangulInput.Prepare("한");
            var blocked = Duxel.Interop.Windows.DirectWriteHangulInput.Prepare("ᄒ\u0301ᅡ");
            return partial.Text == "한" && partial.OriginalPositions is [0, ..]
                && blocked.Text == "ᄒ\u0301ᅡ" && blocked.OriginalPositions is null;
        }, "LV plus trailing Jamo composes while an intervening mark blocks composition");
        Test(() =>
        {
            var source = new string('a', 40_000);
            var shaped = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(latin, source, 32);
            return shaped.Length == source.Length && shaped[0].Cluster == 0 && shaped[^1].Cluster == 39_999;
        }, "Windows shaping does not inherit the former SVG32768-character limit");
        foreach (var length in new[] { 16383, 16384, 16385, 32767, 32768, 32769 })
            Test(() =>
            {
                var shaped = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(latin, new string('a', length), 32);
                return shaped.Length == length && shaped[^1].Cluster == length - 1;
            }, "native shaping boundary preserves all characters : " + length);
        Test(() =>
        {
            var shaped = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(korean, string.Concat(Enumerable.Repeat("한", 35000)), 32);
            return shaped.Length == 35000 && shaped[0].Cluster == 0 && shaped[^1].Cluster == 104997;
        }, "long canonical Hangul retains original source positions beyond UINT16");
        Test(() =>
        {
            var oblique = backend.GetFontVariantResource(latin, PlatformFontSimulation.Oblique);
            var shaped = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(oblique, new string('a', 40000), 32);
            return shaped.Length == 40000;
        }, "long explicit-font layout preserves the selected oblique simulation");
        Test(() =>
        {
            var path = Path.Combine(output, "long-explicit-memory-font.svg");
            File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><text xml:space='preserve' font-family='Fixture' font-size='12' y='12'>"
                + new string(' ', 40000) + "A</text></svg>");
            using var store = new Duxel.Svg.SvgStore(fonts: new("Fixture", [new("Fixture", latin)]));
            var asset = store.Load(path);
            var again = store.Load(path);
            return ReferenceEquals(asset, again) && store.Statistics is { XmlParses: 1, SceneCompiles: 1 }
                && store.FontStatistics is { LoadedFaces: 1, TypefaceRequests: 1 };
        }, "actual SVG long input uses its explicit in-memory face and reuses the compiled asset");
        Test(() =>
        {
            const string start = "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><text xml:space='preserve' font-family='Fixture' font-size='24' y='24'>";
            const string end = "A</text></svg>";
            var longPath = Path.Combine(output, "long-memory-font-visible.svg");
            var referencePath = Path.Combine(output, "short-memory-font-visible.svg");
            File.WriteAllText(longPath, start + new string('\u200B', 40000) + end);
            File.WriteAllText(referencePath, start + end);
            using var store = new Duxel.Svg.SvgStore(fonts: new("Fixture", [new("Fixture", latin)]));
            var actual = store.Load(longPath).Rasterize(32, 32).Pixels.ToArray();
            var expected = store.Load(referencePath).Rasterize(32, 32).Pixels.ToArray();
            return actual.AsSpan().SequenceEqual(expected)
                && expected.Where((value, index) => index % 4 == 3).Any(value => value != 0);
        }, "long SVG memory-font pixels retain the visible glyph after40000zero-width characters");
        foreach (var (family, path, source, rtl, referenceSource) in new[]
        {
            ("Malgun Gothic", korean, "한", false, "한"),
            ("Malgun Gothic", korean, "한글", false, (string?)null),
            ("Segoe UI", latin, "AV e\u0301", false, null),
            ("Arial", arabic, "لا", true, null),
            ("Arial", arabic, "ل\u200Cا", true, null),
            ("Arial", arabic, "بِت", true, null),
            ("Arial", arabic, "abc لا 123", true, null),
            ("Arial", arabic, "abc אב 123", false, null),
            ("Segoe UI", latin, string.Concat(Enumerable.Repeat("office AV e\u0301 ", 3000)), false, null),
            ("Arial", arabic, string.Concat(Enumerable.Repeat("بِت لا 123 ", 4000)), true, null),
            ("Malgun Gothic", korean, string.Concat(Enumerable.Repeat("한", 35000)), false, new string('한', 35000))
        })
        {
            Test(() =>
            {
                // System TextLayout leaves modern Hangul Jamo decomposed. The visual oracle
                // uses canonical composition; production retains original UTF16 cluster positions.
                var native = DirectWriteLayoutOracle.Draw(family, referenceSource ?? source, rtl);
                var shaped = WindowsDirectWriteGlyphRasterizer.ShapeForValidation(path, source, 32, rtl);
                var pen = 0f;
                var actual = shaped.Select(glyph =>
                {
                    var positioned = new DirectWriteLayoutOracle.PositionedGlyph(glyph.Index, pen + glyph.OffsetX, glyph.OffsetY, glyph.Advance);
                    pen += glyph.Advance;
                    return positioned;
                }).OrderBy(glyph => glyph.X).ThenBy(glyph => glyph.Y).ThenBy(glyph => glyph.Index).ToArray();
                var expected = native.OrderBy(glyph => glyph.X).ThenBy(glyph => glyph.Y).ThenBy(glyph => glyph.Index).ToArray();
                var equal = actual.Length == expected.Length && actual.Zip(expected).All(pair =>
                    pair.First.Index == pair.Second.Index && MathF.Abs(pair.First.X - pair.Second.X) < .002f
                    && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f && MathF.Abs(pair.First.Advance - pair.Second.Advance) < .002f);
                if (!equal)
                {
                    Console.WriteLine("Source : " + source[..Math.Min(160, source.Length)] + " / Length : " + source.Length + " / RTL : " + rtl
                        + " / Expected count : " + expected.Length + " / Actual count : " + actual.Length
                        + " / Expected prefix : " + string.Join("; ", expected.Take(8))
                        + " / Actual prefix : " + string.Join("; ", actual.Take(8))
                        + " / Cluster prefix : " + string.Join(", ", shaped.Take(8).Select(glyph => glyph.Cluster)));
                    for (var index = 0; index < Math.Min(actual.Length, expected.Length); index++)
                        if (actual[index].Index != expected[index].Index || MathF.Abs(actual[index].X - expected[index].X) >= .002f
                            || MathF.Abs(actual[index].Y - expected[index].Y) >= .002f || MathF.Abs(actual[index].Advance - expected[index].Advance) >= .002f)
                        {
                            Console.WriteLine("First mismatch : " + index + " / Expected : " + expected[index] + " / Actual : " + actual[index]);
                            break;
                        }
                }
                return equal;
            }, "independent TextLayout glyph positions match : " + family + " / " + source[..Math.Min(64, source.Length)] + " / Length " + source.Length + " / RTL " + rtl);
        }
    }

    private sealed class MeasureScreen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class RasterOnlyBackend : IPlatformTextBackend, IPlatformFontMetricsBackend
    {
        public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result) => WindowsPlatformTextBackend.Instance.TryMeasure(request, out result);
        public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result) => WindowsPlatformTextBackend.Instance.TryRasterize(request, out result);
        public bool TryGetFontMetrics(string path, float size, out PlatformFontMetrics metrics) => WindowsPlatformTextBackend.Instance.TryGetFontMetrics(path, size, out metrics);
    }
}
