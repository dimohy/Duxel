using System.Globalization;
using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe class ParagraphFontContextTests
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct TextRange(uint Start, uint Length);

    internal static void Run(Action<bool, string> check)
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var primary = Path.Combine(directory, "segoeui.ttf");
        var secondary = Path.Combine(directory, "malgun.ttf");
        foreach (var parts in new[]
        {
            new[] { "אב ", "정 ", "אב" },
            new[] { "אב 123 ", "정 ", "גד" },
            new[] { "A \u202Bאב ", "정 ", "גד\u202C Z" },
            new[] { "e\u0301 A\u030a ", "정 ", "אב 123" },
            new[] { "بِت ", "정 ", "لا 123" }
        })
        {
            var text = string.Concat(parts);
            void ConfigureFonts(nint layout)
            {
                // This exact fixture interval is independent of product coverage selection.
                fixed (char* family = "Malgun Gothic")
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, TextRange, int>)Method(layout, 31))
                        (layout, family, new((uint)parts[0].Length, (uint)parts[1].Length)));
            }
            var carets = new float[text.Length + 1];
            var nativeCoverage = new byte[DirectWriteLayoutOracle.CoverageWidth * DirectWriteLayoutOracle.CoverageHeight * 3];
            var native = DirectWriteLayoutOracle.Draw("Segoe UI", text, false, layout =>
            {
                for (var index = 0; index < carets.Length; index++)
                {
                    float x = 0, y = 0;
                    HitMetrics hit = default;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                        (layout, (uint)index, 0, &x, &y, &hit));
                    carets[index] = x;
                }
            }, 14, ConfigureFonts, nativeCoverage);
            var probes = DirectWriteLayoutOracle.GeometryProbes(text, ConfigureFonts);
            var backend = WindowsPlatformTextBackend.Instance;
            backend.TryLayout(new(primary, secondary, text, 14), out var measured);
            check(MathF.Abs(measured.Advance - native.Sum(glyph => glyph.Advance)) < .002f,
                "Cross-font paragraph advance matches independent complete TextLayout : " + text);
            backend.TryRasterize(new(primary, secondary, text, 14), out var raster);
            var actualCoverage = new byte[nativeCoverage.Length];
            try
            {
                for (var y = 0; y < raster.Height; y++)
                    for (var x = 0; x < raster.Width; x++)
                    {
                        var targetX = DirectWriteLayoutOracle.CoverageLeft + (int)raster.OriginX + x;
                        var targetY = DirectWriteLayoutOracle.CoverageBaseline - (int)raster.Baseline + y;
                        if ((uint)targetX >= DirectWriteLayoutOracle.CoverageWidth || (uint)targetY >= DirectWriteLayoutOracle.CoverageHeight)
                            throw new InvalidOperationException("Product paragraph coverage exceeds the independent canvas.");
                        for (var channel = 0; channel < 3; channel++)
                            actualCoverage[(targetY * DirectWriteLayoutOracle.CoverageWidth + targetX) * 3 + channel]
                                = raster.RgbaPixels.Span[(y * raster.Width + x) * 4 + channel];
                    }
                check(actualCoverage.AsSpan().SequenceEqual(nativeCoverage),
                    "Actual cross-font raster bytes equal direct native glyph-run analysis : " + text);
            }
            finally { raster.PixelOwner?.Dispose(); }
            var mesh = backend.CreateTextMesh(new(primary, secondary, text, 14));
            var nativeX = native.Select(glyph => glyph.X).Order().ToArray();
            var actualX = mesh.Glyphs.Span.ToArray().Select(glyph => glyph.Offset.X).Order().ToArray();
            check(nativeX.Length == actualX.Length && nativeX.Zip(actualX).All(pair => MathF.Abs(pair.First - pair.Second) < .002f),
                "Actual cross-font vector glyph origins equal the native paragraph : " + text);
            var geometry = backend.CreateLineGeometry(new(primary, secondary, text, 14), false);
            var boundaries = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
            check(boundaries.All(index => MathF.Abs(geometry.CaretX(index) - carets[index]) < .002f),
                "Cross-font paragraph original carets preserve complete bidi context : " + text);
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var inline = new UiInlineText([new(text)]);
            context.NewFrame(new(1f / 60, new(640, 300), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 300), UiTextSettings.Default);
            var matches = false;
            var hitMatches = true;
            var selectionMatches = true;
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                inline.Measure(ui, new(640, 300));
                matches = boundaries.All(index => MathF.Abs(inline.VisualLines.X(index, 0) - carets[index]) < .002f);
                foreach (var hit in probes.Hits)
                {
                    var actual = inline.VisualLines.Hit(hit.X, 1, 20);
                    var equal = actual == hit.Position && MathF.Abs(inline.VisualLines.X(actual, 0) - hit.CaretX) < .002f;
                    if (hitMatches && !equal)
                        Console.WriteLine($"Cross-font hit mismatch at {hit.X} : native {hit.Position}/{hit.CaretX},actual {actual}/{inline.VisualLines.X(actual, 0)} / {text}");
                    hitMatches &= equal;
                }
                foreach (var range in probes.Ranges)
                {
                    if (!boundaries.Contains(range.Start) || !boundaries.Contains(range.End))
                        continue;
                    var fragments = inline.VisualLines.SelectionFragments(range.Start, range.End, 0);
                    var edges = fragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })
                        .Concat(range.Fragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })).Distinct().Order().ToArray();
                    for (var index = 1; index < edges.Length; index++)
                    {
                        if (edges[index] - edges[index - 1] < .002f)
                            continue;
                        var point = (edges[index] + edges[index - 1]) / 2;
                        selectionMatches &= fragments.Any(fragment => point > fragment.Left && point < fragment.Left + fragment.Width)
                            == range.Fragments.Any(fragment => point > fragment.Left && point < fragment.Left + fragment.Width);
                    }
                }
            }));
            using var draw = context.GetDrawData();
            check(matches, "Actual Core cross-font row uses complete contextual carets : " + text);
            check(hitMatches, "Actual Core cross-font pointer positions and affinity match the native layout : " + text);
            check(selectionMatches, "Actual Core cross-font visual selection coverage matches the native layout : " + text);
        }
        var empty = WindowsPlatformTextBackend.Instance.CreateLineGeometry(new(primary, secondary, string.Empty, 14), false);
        check(empty.Length == 0 && empty.Advance == 0, "Empty contextual geometry preserves the original empty snapshot contract");
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HitMetrics
    {
        internal uint Position, Length;
        internal float Left, Top, Width, Height;
        internal uint Level;
        internal int IsText, IsTrimmed;
    }
    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
