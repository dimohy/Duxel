using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static class LinuxParagraphTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var passed = new List<string>();
        var failed = new List<string>();
        void Check(bool condition, string name)
        {
            Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
            (condition ? passed : failed).Add(name);
        }
        var fonts = PresentationTestPlatform.Fonts;
        var globalConfig = PangoLineOracle.CurrentFontConfig;
        using var backend = new LinuxPlatformTextBackend();
        using var oracle = new PangoLineOracle(fonts[0], fonts[2]);
        foreach (var fontSize in new[] { 8f, 13.3f, 14f, 14.4f, 24f })
            foreach (var source in new[] { "AV", "office", "abc אב 123", "abc لا 123", "بِت لا 123", "정세일 AV 한글", "e\u0301 A\u030a", "A\tB", "", "\u200B", "한" })
            {
                Console.WriteLine("Font size : " + fontSize);
                var expected = oracle.Capture(source, fontSize);
                backend.TryLayout(new(fonts[0], fonts[2], source, fontSize), out var metrics);
                Check(MathF.Abs(metrics.Advance - expected.Advance) < .002f, "Native complete-row advance : " + source);
                backend.TryRasterize(new(fonts[0], fonts[2], source, fontSize), out var raster);
                var actual = new byte[PangoLineOracle.Width * PangoLineOracle.Height];
                try
                {
                    var left = 20 + (int)raster.OriginX;
                    var top = 20 + (int)(oracle.LastBaseline - raster.Baseline);
                    for (var y = 0; y < raster.Height; y++)
                        for (var x = 0; x < raster.Width; x++)
                            if (x + left >= 0 && x + left < PangoLineOracle.Width && y + top >= 0 && y + top < PangoLineOracle.Height)
                                actual[(y + top) * PangoLineOracle.Width + x + left] = raster.RgbaPixels.Span[(y * raster.Width + x) * 4 + 3];
                }
                finally { raster.PixelOwner?.Dispose(); }
                var equal = actual.AsSpan().SequenceEqual(expected.Pixels);
                Check(equal, "Exact native Pango drawing versus extracted Cairo glyph pixels : " + source);
                if (!equal)
                {
                    var translated = oracle.Capture(source, fontSize, translateOrigin: true);
                    Console.WriteLine("Native integer-translation difference : " + expected.Pixels.Zip(translated.Pixels).Count(pair => pair.First != pair.Second)
                        + " / Product versus translated native : " + actual.Zip(translated.Pixels).Count(pair => pair.First != pair.Second));
                    var name = Path.Combine(output, "case-" + passed.Count + "-" + failed.Count);
                    File.WriteAllBytes(name + ".actual.a8", actual);
                    File.WriteAllBytes(name + ".native.a8", expected.Pixels);
                }
                IPlatformTextBackend capability = backend;
                if (capability is not IPlatformTextGeometryBackend geometryBackend)
                {
                    Check(false, "Original-source native geometry capability : " + source);
                    continue;
                }
                var geometry = geometryBackend.CreateLineGeometry(new(fonts[0], fonts[2], source, fontSize), false);
                var boundaries = StringInfo.ParseCombiningCharacters(source).Append(source.Length).ToArray();
                Check(boundaries.All(index => MathF.Abs(geometry.CaretX(index) - expected.Carets[index]) < .002f),
                    "Native original-grapheme caret positions : " + source);
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                context.SetPlatformTextBackend(backend);
                PresentationTestPlatform.ConfigureFonts(context);
                using var inline = new UiInlineText([new(source)]);
                var matches = true;
                var hitMatches = true;
                var selectionMatches = true;
                var builds = -1;
                for (var frame = 0; frame < 2; frame++)
                {
                    context.NewFrame(new(1f / 60, new(640, 300), new(1, 1)),
                        new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                        new(0, 0, 640, 300), UiTextSettings.Default with
                        {
                            Scale = fontSize / 14
                        });
                    context.Render(new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        inline.Measure(ui, new(640, 300));
                        matches &= boundaries.All(index => MathF.Abs(inline.VisualLines.X(index, 0) - expected.Carets[index]) < .002f);
                        matches &= frame == 0 || builds == inline.VisualLines.Builds;
                        builds = inline.VisualLines.Builds;
                        foreach (var hit in expected.Hits)
                        {
                            var actualHit = inline.VisualLines.Hit(hit.X, 1, 20);
                            var hitEqual = actualHit == hit.Position && MathF.Abs(inline.VisualLines.X(actualHit, 0) - hit.CaretX) < .002f;
                            if (hitMatches && !hitEqual)
                                Console.WriteLine($"Hit mismatch at {hit.X} : native {hit.Position}/{hit.CaretX},actual {actualHit}/{inline.VisualLines.X(actualHit, 0)},size {fontSize},source {source}");
                            hitMatches &= hitEqual;
                        }
                        foreach (var selection in expected.Selections)
                        {
                            var fragments = inline.VisualLines.SelectionFragments(selection.Start, selection.End, 0);
                            var edges = fragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })
                                .Concat(selection.Fragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })).Distinct().Order().ToArray();
                            for (var edge = 1; edge < edges.Length; edge++)
                            {
                                if (edges[edge] - edges[edge - 1] < .002f)
                                    continue;
                                var point = (edges[edge] + edges[edge - 1]) / 2;
                                selectionMatches &= fragments.Any(fragment => point > fragment.Left && point < fragment.Left + fragment.Width)
                                    == selection.Fragments.Any(fragment => point > fragment.Left && point < fragment.Left + fragment.Width);
                            }
                        }
                    }));
                    using var draw = context.GetDrawData();
                }
                Check(matches, "Actual retained Core inline consumer uses the native row geometry : " + source);
                Check(hitMatches, "Native pointer positions and trailing affinity match actual Core flow : " + source);
                Check(selectionMatches, "Native visual selection ranges match actual Core flow : " + source);
            }
        for (var size = 7; size < 48; size++)
            backend.TryLayout(new(fonts[0], fonts[2], "A정 AV אב", size), out _);
        Check(backend.TryRasterize(new(fonts[0], fonts[2], "A정 AV אב", 14), out var afterEviction) && !afterEviction.RgbaPixels.IsEmpty,
            "Font-cache eviction preserves live private native paragraph resources");
        afterEviction.PixelOwner?.Dispose();
        using (var independent = new LinuxPlatformTextBackend())
            independent.TryLayout(new(fonts[0], fonts[2], "abc אב 123", 14), out _);
        Check(backend.TryLayout(new(fonts[0], fonts[2], "abc אב 123", 14), out var afterDisposal) && afterDisposal.Advance > 0,
            "Disposing an independent font map does not invalidate another backend");
        const string measuredSource = "A정 AV 123";
        backend.TryLayout(new(fonts[0], fonts[2], measuredSource, 14), out var measured);
        for (var size = 40; size < 80; size++)
            backend.TryGetFontMetrics(fonts[0], size, out _);
        backend.TryRasterize(new(fonts[0], fonts[2], measuredSource, 14), out var measuredThenEvicted);
        using (var independent = new LinuxPlatformTextBackend())
        {
            independent.TryRasterize(new(fonts[0], fonts[2], measuredSource, 14), out var direct);
            try
            {
                Check(measured.Width == measuredThenEvicted.Width && measured.Height == measuredThenEvicted.Height
                    && measured.Baseline == measuredThenEvicted.Baseline && measured.Advance == measuredThenEvicted.Advance
                    && measured.OriginX == measuredThenEvicted.OriginX && measured.FontAscent == measuredThenEvicted.FontAscent
                    && measuredThenEvicted.RgbaPixels.Span.SequenceEqual(direct.RgbaPixels.Span),
                    "Measurement followed by font eviction retains exact metrics and drawing");
            }
            finally
            {
                direct.PixelOwner?.Dispose();
                measuredThenEvicted.PixelOwner?.Dispose();
            }
        }

        var rejectsUnknown = false;
        try
        {
            backend.TryLayout(new(fonts[0], fonts[2], "\U0010FFFF", 14), out _);
        }
        catch (NotSupportedException) { rejectsUnknown = true; }
        Check(rejectsUnknown, "Missing explicit-font glyph fails instead of system-font substitution or tofu");
        Check(PangoLineOracle.CurrentFontConfig == globalConfig, "Private font maps preserve the process global Fontconfig selection");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxParagraphResult(passed.Count, failed.Count, passed, failed), LinuxParagraphJson.Default.LinuxParagraphResult));
        if (failed.Count > 0)
            throw new InvalidOperationException("Native Linux paragraph failures : " + failed.Count);
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
internal sealed record LinuxParagraphResult(int Passed, int Failed, List<string> Checks, List<string> Failures);
[JsonSerializable(typeof(LinuxParagraphResult))]
internal partial class LinuxParagraphJson : JsonSerializerContext;
