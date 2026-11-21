using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static class LinuxStyledParagraphTests
{
    internal static void Run(string output, bool checkWholeRaster = false)
    {
        Directory.CreateDirectory(output);
        var passed = new List<string>();
        var failed = new List<string>();
        void Check(bool condition, string name)
        {
            (condition ? passed : failed).Add(name);
            Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        }
        var fonts = PresentationTestPlatform.Fonts;
        using var backend = new LinuxPlatformTextBackend();
        using var oracle = new PangoLineOracle(fonts[0], fonts[1], [fonts[2], fonts[3]]);
        PangoLineOracle.StyledSpan[][] cases =
        [
            [new("AV ", 0, 14), new("office ", 1, 20), new("אב גד 123", 0, 14)],
            [new("abc אב ", 0, 14), new("גד office ", 1, 14), new("123", 0, 14)],
            [new("بِت ", 0, 14), new("لا 123 ", 1, 20), new("e\u0301 A\u030a", 0, 14)],
            [new("첫 줄\r\n둘째 줄", 2, 14)],
            [new("AV\t", 0, 14), new("한글 ", 3, 20), new("123", 0, 14)],
            [new("", 0, 14)],
            [new("\u200B", 0, 14)],
            [new("AV  ", 0, 14)],
            [new("אב  ", 0, 14)]
        ];
        foreach (var spans in cases)
            foreach (var width in new[] { float.PositiveInfinity, 100f })
                foreach (var rtl in new[] { false, true })
                {
                    var text = string.Concat(spans.Select(span => span.Text));
                    var styles = new List<PlatformTextParagraphStyle>();
                    var start = 0;
                    foreach (var span in spans)
                    {
                        styles.Add(new(start, span.Text.Length, fonts[span.Face], null, span.Size));
                        start += span.Text.Length;
                    }
                    var expected = oracle.CaptureStyled(spans, width, rtl);
                    var request = new PlatformTextParagraphRequest(text, styles.ToArray(), width,
                        rtl ? PlatformTextParagraphDirection.RightToLeft : PlatformTextParagraphDirection.LeftToRight);
                    var name = $"{text} / {width} / {rtl}";
                    if (checkWholeRaster)
                    {
                        CompareWholeRaster(output, backend.RasterizeWholeParagraph(request), expected.Pixels, name, Check);
                        continue;
                    }
                    var paragraph = backend.CreateParagraph(request, false);
                    var nativeGlyphs = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true);
                    Check(nativeGlyphs.Pixels.AsSpan().SequenceEqual(expected.Pixels), "Independent original glyph drawing equals whole native layout : " + name);
                    var expectedGlyphs = oracle.LastNativeGlyphs.OrderBy(glyph => glyph.Index).ThenBy(glyph => glyph.X).ThenBy(glyph => glyph.Y).ToArray();
                    var actualGlyphs = paragraph.Runs.ToArray().SelectMany(run => run.Glyphs.ToArray())
                        .OrderBy(glyph => glyph.Index).ThenBy(glyph => glyph.X).ThenBy(glyph => glyph.Y).ToArray();
                    var positionsMatch = expectedGlyphs.Length == actualGlyphs.Length && expectedGlyphs.Zip(actualGlyphs).All(pair =>
                        pair.First.Index == pair.Second.Index && Math.Abs(pair.First.X - pair.Second.X) < .00001 && Math.Abs(pair.First.Y - pair.Second.Y) < .00001);
                    Check(positionsMatch, "Exact original native styled glyph positions : " + name);
                    if (!positionsMatch)
                        Console.WriteLine("Native glyphs : " + string.Join(";", expectedGlyphs.Select(glyph => $"{glyph.Index}@{glyph.X},{glyph.Y}"))
                            + "\nActual glyphs : " + string.Join(";", actualGlyphs.Select(glyph => $"{glyph.Index}@{glyph.X},{glyph.Y}")));
                    Check(MathF.Abs(paragraph.Width - expected.Width) < .002f && MathF.Abs(paragraph.Height - expected.Height) < .002f,
                        "Native styled paragraph dimensions : " + name);
                    Check(paragraph.Carets.ToArray().Zip(expected.Carets).All(pair => MathF.Abs(pair.First.X - pair.Second.X) < .002f
                        && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f), "Native styled source caret positions : " + name);
                    Check(paragraph.Lines.Length == expected.Lines.Length && paragraph.Lines.ToArray().Zip(expected.Lines)
                        .All(pair => pair.First.Start == pair.Second.Start && pair.First.Start + pair.First.Length == pair.Second.End),
                        "Native styled wrapping and CRLF source lines : " + name);
                    Check(paragraph.Lines.ToArray().Zip(expected.Lines).All(pair =>
                        MathF.Abs(pair.First.WidthWithoutTrailingWhitespace - pair.Second.FormattedWidth) < .002f),
                        "Native formatted width excludes whitespace without removing source carets : " + name);
                    var pixels = new byte[PangoLineOracle.Width * PangoLineOracle.Height];
                    var diagnostics = new List<string>();
                    foreach (var run in paragraph.Runs.Span)
                    {
                        var raster = backend.RasterizeParagraphRun(run);
                        diagnostics.Add($"font={run.FontPath},size={run.FontSize},origin={raster.Origin},size={raster.Width}x{raster.Height},baseline={run.Origin.Y},glyphs="
                            + string.Join(";", run.Glyphs.ToArray().Select(glyph => $"{glyph.Index}@{glyph.X},{glyph.Y}")));
                        for (var y = 0; y < raster.Height; y++)
                            for (var x = 0; x < raster.Width; x++)
                            {
                                var targetX = 20 + (int)raster.Origin.X + x;
                                var targetY = 20 + (int)raster.Origin.Y + y;
                                if ((uint)targetX >= PangoLineOracle.Width || (uint)targetY >= PangoLineOracle.Height)
                                    throw new InvalidOperationException("Styled test canvas is too small.");
                                var target = targetY * PangoLineOracle.Width + targetX;
                                var alpha = raster.RgbaPixels.Span[(y * raster.Width + x) * 4 + 3];
                                pixels[target] = (byte)(alpha + (pixels[target] * (255 - alpha) + 127) / 255);
                            }
                        var mesh = backend.CreateParagraphRunMesh(run);
                        Check(mesh.Glyphs.Span.ToArray().All(glyph => run.Glyphs.Span.ToArray().Any(source =>
                            glyph.Offset == new UiVector2(source.X, source.Y))), "Vector snapshot positions : " + name);
                    }
                    var sequential = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true,
                        runSurface: PangoLineOracle.RunSurface.SequentialLine);
                    Check(pixels.AsSpan().SequenceEqual(sequential.Pixels), "Exact independent native run-mask composition : " + name);
                    var equal = pixels.AsSpan().SequenceEqual(expected.Pixels);
                    Check(equal, "Exact native styled Pango pixels : " + name);
                    if (!equal)
                    {
                        var offsets = Enumerable.Range(0, pixels.Length).Where(index => pixels[index] != expected.Pixels[index]).ToArray();
                        var originalNative = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, traceOffsets: offsets);
                        var translatedNative = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, translatedGlyphPaint: true);
                        var croppedNative = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, runSurface: PangoLineOracle.RunSurface.Cropped);
                        var visibleNative = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, omitInkless: true);
                        var croppedVisible = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, runSurface: PangoLineOracle.RunSurface.Cropped, omitInkless: true);
                        var widerVisible = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, runSurface: PangoLineOracle.RunSurface.Cropped, omitInkless: true, cropPadding: 8);
                        var separate = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, runSurface: PangoLineOracle.RunSurface.FullSize, traceOffsets: offsets);
                        var separateVisible = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true,
                            runSurface: PangoLineOracle.RunSurface.FullSize, omitInkless: true);
                        var marked = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true, runSurface: PangoLineOracle.RunSurface.FullSizeMarked);
                        var croppedMarked = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true,
                            runSurface: PangoLineOracle.RunSurface.CroppedMarked);
                        var widerMarked = oracle.CaptureStyled(spans, width, rtl, nativeGlyphPaint: true,
                            runSurface: PangoLineOracle.RunSurface.CroppedMarked, cropPadding: 8);
                        Console.WriteLine("Original native glyph paint versus native layout : " + originalNative.Pixels.Zip(expected.Pixels).Count(pair => pair.First != pair.Second)
                            + " / versus product : " + originalNative.Pixels.Zip(pixels).Count(pair => pair.First != pair.Second));
                        Console.WriteLine("Native translated CTM versus native layout : " + translatedNative.Pixels.Zip(expected.Pixels).Count(pair => pair.First != pair.Second));
                        Console.WriteLine("Native cropped runs versus layout : " + croppedNative.Pixels.Zip(expected.Pixels).Count(pair => pair.First != pair.Second)
                            + " / versus product : " + croppedNative.Pixels.Zip(pixels).Count(pair => pair.First != pair.Second));
                        foreach (var (label, snapshot) in new[] { ("full visible", visibleNative), ("cropped visible", croppedVisible), ("wider cropped visible", widerVisible) })
                            Console.WriteLine($"Visible glyph diagnostic {failed.Count}, {label}, layout {snapshot.Pixels.Zip(expected.Pixels).Count(pair => pair.First != pair.Second)}, product {snapshot.Pixels.Zip(pixels).Count(pair => pair.First != pair.Second)}");
                        Console.WriteLine($"Full-size separate runs {failed.Count}, all to direct {separate.Pixels.Zip(originalNative.Pixels).Count(pair => pair.First != pair.Second)}, visible to direct {separateVisible.Pixels.Zip(visibleNative.Pixels).Count(pair => pair.First != pair.Second)}, visible to product {separateVisible.Pixels.Zip(pixels).Count(pair => pair.First != pair.Second)}");
                        Console.WriteLine($"Marked scratch destination {failed.Count}, to direct {marked.Pixels.Zip(originalNative.Pixels).Count(pair => pair.First != pair.Second)}, to empty scratch {marked.Pixels.Zip(separate.Pixels).Count(pair => pair.First != pair.Second)}");
                        Console.WriteLine($"Marked cropped destination {failed.Count}, tight to direct {croppedMarked.Pixels.Zip(originalNative.Pixels).Count(pair => pair.First != pair.Second)}, wider to direct {widerMarked.Pixels.Zip(originalNative.Pixels).Count(pair => pair.First != pair.Second)}");
                        using var trace = File.CreateText(Path.Combine(output, $"failure-{failed.Count}.alpha.tsv"));
                        trace.WriteLine("x\ty\trun\tdirect\tmask\tcomposite");
                        for (var pixel = 0; pixel < offsets.Length; pixel++)
                            for (var run = 0; run < separate.Trace.Count; run++)
                                trace.WriteLine($"{offsets[pixel] % PangoLineOracle.Width - 20}\t{offsets[pixel] / PangoLineOracle.Width - 20}\t{run}\t{originalNative.Trace[run].Destination[pixel]}\t{separate.Trace[run].Mask[pixel]}\t{separate.Trace[run].Destination[pixel]}");
                        File.WriteAllLines(Path.Combine(output, $"failure-{failed.Count}.runs.txt"), diagnostics);
                        File.WriteAllBytes(Path.Combine(output, $"failure-{failed.Count}.native.a8"), expected.Pixels);
                        File.WriteAllBytes(Path.Combine(output, $"failure-{failed.Count}.actual.a8"), pixels);
                    }
                }
        if (!checkWholeRaster)
        {
            RunCore(backend, oracle, Check);
            var glyph = new PlatformTextParagraphGlyph(42, 0, 8, 0, 0, 3, 17);
            var context = new PlatformTextRunRasterContext(-4, -16, 40, 30, false);
            PlatformTextParagraphRun Run(UiVector2 origin, PlatformTextParagraphGlyph value, PlatformTextRunRasterContext raster)
                => new(0, fonts[0], 14, 0, origin, [value], raster);
            var initial = Run(new(3, 17), glyph, context);
            Check(initial.RasterEquals(Run(new(13, 37), glyph with { X = 13, Y = 37 }, context)),
                "Integer relocation with identical line context reuses cached coverage");
            Check(!initial.RasterEquals(Run(new(3, 17), glyph, context with { Width = 41 })),
                "Changing line bounds invalidates cached coverage");
            Check(!initial.RasterEquals(Run(new(3, 17), glyph, context with { PreviousInk = true })),
                "Changing preceding Cairo ink state invalidates cached coverage");

            const string source = "AV office";
            PlatformTextParagraphStyle first = new(0, 3, fonts[0], null, 14) { PaintGroup = 1 };
            PlatformTextParagraphStyle second = new(3, source.Length - 3, fonts[0], null, 14) { PaintGroup = 1 };
            var joined = backend.CreateParagraph(new(source, [first, second]), false);
            var split = backend.CreateParagraph(new(source, [first, second with { PaintGroup = 2 }]), false);
            Check(joined.Runs.Length == 1 && split.Runs.Length == 2
                && split.Runs.Span[0].StyleIndex == 0 && split.Runs.Span[1].StyleIndex == 1
                && joined.Runs.Span[0].Glyphs.Span.SequenceEqual(split.Runs.ToArray().SelectMany(run => run.Glyphs.ToArray()).ToArray()),
                "Native groups retain identical paint and split distinct paint without changing glyphs");
            var codeLayouts = new UiCodeParagraphLayouts();
            const string code = "AV office\nAV office";
            PlatformTextParagraphStyle[] codeStyles = [first, second, first with { Start = 9, Length = 4 },
                second with { Start = 13 }];
            var codeLayout = codeLayouts.Prepare(backend, code, codeStyles, float.PositiveInfinity, false, default);
            Check(codeLayout.Runs.Length == 2 && codeLayout.Runs.ToArray().All(run => run.RasterContext is not null),
                "Code paragraph snapshots retain run raster context when rebasing lines");
            var changedStyles = (PlatformTextParagraphStyle[])codeStyles.Clone();
            changedStyles[3] = changedStyles[3] with { PaintGroup = 2 };
            var recolored = codeLayouts.Prepare(backend, code, changedStyles, float.PositiveInfinity, false, default);
            Check(recolored.Runs.Length == 3 && recolored.Runs.Span[^1].StyleIndex == 3 && codeLayouts.NativeBuilds == 3,
                "An unchanged code string rebuilds only the line whose paint grouping changed");
            var editedPrefix = (PlatformTextParagraphStyle[])changedStyles.Clone();
            editedPrefix[1] = editedPrefix[1] with { PaintGroup = 3 };
            var prefixChanged = codeLayouts.Prepare(backend, code, editedPrefix, float.PositiveInfinity, false, default);
            Check(prefixChanged.Runs.Length == 4 && codeLayouts.NativeBuilds == 4,
                "Recoloring an earlier line preserves unchanged native lines after it");
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxStyledResult(passed.Count, failed.ToArray()),
            LinuxStyledJson.Default.LinuxStyledResult));
        if (failed.Count != 0)
            throw new InvalidOperationException("Linux styled paragraph failures : " + failed.Count);
    }

    private static void CompareWholeRaster(string output, PlatformTextParagraphRaster raster, byte[] expected,
        string name, Action<bool, string> check)
    {
        using var owner = raster.PixelOwner;
        var pixels = new byte[PangoLineOracle.Width * PangoLineOracle.Height];
        var left = 20 + checked((int)raster.Origin.X);
        var top = 20 + checked((int)raster.Origin.Y);
        if (left < 0 || top < 0 || left + raster.Width > PangoLineOracle.Width || top + raster.Height > PangoLineOracle.Height)
            throw new InvalidOperationException("Whole paragraph fixture exceeds the reference surface.");
        var data = raster.RgbaPixels.Span;
        for (var y = 0; y < raster.Height; y++)
            for (var x = 0; x < raster.Width; x++)
            {
                var at = (y * raster.Width + x) * 4;
                var alpha = data[at + 3];
                if (data[at] != alpha || data[at + 1] != alpha || data[at + 2] != alpha)
                    throw new InvalidOperationException("Whole paragraph texture does not contain grayscale coverage.");
                pixels[(top + y) * PangoLineOracle.Width + left + x] = alpha;
            }
        var equal = pixels.AsSpan().SequenceEqual(expected);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels));
        Console.WriteLine($"WHOLE {raster.Width} {raster.Height} {hash} {equal}");
        if (!equal)
        {
            File.WriteAllBytes(Path.Combine(output, hash + ".whole.a8"), pixels);
            File.WriteAllBytes(Path.Combine(output, hash + ".expected.a8"), expected);
        }
        check(equal, "Whole native paragraph cropped raster : " + name);
    }

    private static void RunCore(LinuxPlatformTextBackend backend, PangoLineOracle oracle, Action<bool, string> check)
    {
        foreach (var width in new[] { 640f, 100f })
        {
            PangoLineOracle.StyledSpan[] spans = [new("abc אב ", 0, 14), new("גד office ", 1, 14), new("123", 0, 14)];
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            PresentationTestPlatform.ConfigureFonts(context);
            using var inline = new UiInlineText([new(spans[0].Text), new(spans[1].Text, UiInlineTextWeight.Bold), new(spans[2].Text)]);
            PlatformTextParagraphLayout? previous = null;
            var source = inline.Runs;
            foreach (var rtl in new[] { false, true, false })
            {
                var expected = oracle.CaptureStyled(spans, width, rtl);
                inline.RightToLeft = rtl;
                PlatformTextParagraphLayout? first = null;
                var matches = true;
                var reused = true;
                var builds = -1;
                for (var frame = 0; frame < 2; frame++)
                {
                    inline.RightToLeft = rtl;
                    context.NewFrame(new(1f / 60, new(width, 300), new(1, 1)),
                        new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                        new(0, 0, width, 300), UiTextSettings.Default);
                    context.Render(new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        inline.Render(ui);
                        var paragraph = inline.VisualLines.NativeParagraph;
                        matches &= paragraph is not null && paragraph.Carets.ToArray().Zip(expected.Carets).All(pair =>
                            MathF.Abs(pair.First.X - pair.Second.X) < .002f && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f);
                        if (frame == 0)
                        {
                            first = paragraph;
                            builds = inline.VisualLines.Builds;
                        }
                        else
                            reused &= ReferenceEquals(first, paragraph) && builds == inline.VisualLines.Builds;
                    }));
                    using var data = context.GetDrawData();
                }
                check(matches, $"Actual Linux Core consumes public paragraph direction : {width} / {rtl}");
                check(reused, $"Unchanged Linux paragraph direction retains layout : {width} / {rtl}");
                check(previous is null || !ReferenceEquals(previous, first), $"Changed Linux direction replaces the paragraph : {width} / {rtl}");
                check(ReferenceEquals(source, inline.Runs), $"Linux direction changes preserve the literal source snapshot : {width} / {rtl}");
                previous = first;
            }
        }
        var fonts = PresentationTestPlatform.Fonts;
        var text = new string('A', 40);
        var styles = Enumerable.Range(0, 40).Select(index => new PlatformTextParagraphStyle(index, 1, fonts[0], null, 10 + index / 4f)).ToArray();
        var many = backend.CreateParagraph(new(text, styles), false);
        check(many.Carets.Length == 41 && many.Runs.Length >= 40
            && many.Runs.ToArray().All(run => backend.RasterizeParagraphRun(run).Width > 0),
            "More than32 live paragraph font sizes survive extraction and post-snapshot cache eviction");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
internal sealed record LinuxStyledResult(int Passed, string[] Failures);
[JsonSerializable(typeof(LinuxStyledResult))]
internal sealed partial class LinuxStyledJson : JsonSerializerContext;
