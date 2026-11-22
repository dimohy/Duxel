using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe partial class StyledParagraphTests
{
    private readonly record struct Segment(string Text, string Family, string File, float Size, int Weight);
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct TextRange(uint Start, uint Length);
    [StructLayout(LayoutKind.Sequential)]
    private struct HitMetrics
    {
        internal uint Position, Length;
        internal float Left, Top, Width, Height;
        internal uint Level;
        internal int IsText, IsTrimmed;
    }
    private static void* Method(nint instance, int slot) => (*(void***)instance)[slot];

    internal static void Run(Action<bool, string> check)
    {
        RunRequestContracts(check);
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var cases = new[]
        {
            (Segments: new[] { new Segment("AV ", "Segoe UI", "segoeui.ttf", 14, 400), new("office ", "Segoe UI", "segoeuib.ttf", 20, 700), new("אב גד ", "Segoe UI", "segoeui.ttf", 14, 400), new("정", "Malgun Gothic", "malgun.ttf", 14, 400) }, Width: float.PositiveInfinity, Rtl: false),
            (Segments: new[] { new Segment("AV אב regular ", "Segoe UI", "segoeui.ttf", 14, 400), new("bold office ", "Segoe UI", "segoeuib.ttf", 20, 700), new("한글 123", "Malgun Gothic", "malgun.ttf", 14, 400) }, Width: 100f, Rtl: false),
            (Segments: new[] { new Segment("אב גד ", "Segoe UI", "segoeui.ttf", 14, 400), new("אב bold ", "Segoe UI", "segoeuib.ttf", 20, 700), new("정 한글", "Malgun Gothic", "malgun.ttf", 14, 400) }, Width: 100f, Rtl: true),
            (Segments: new[] { new Segment("첫 줄\r\n둘째 줄", "Malgun Gothic", "malgun.ttf", 14, 400) }, Width: 100f, Rtl: false)
        };
        IPlatformTextParagraphBackend backend = WindowsPlatformTextBackend.Instance;
        foreach (var scenario in cases)
        {
            var text = string.Concat(scenario.Segments.Select(segment => segment.Text));
            var styles = new List<PlatformTextParagraphStyle>();
            var position = 0;
            foreach (var segment in scenario.Segments)
            {
                styles.Add(new(position, segment.Text.Length, Path.Combine(fonts, segment.File), null, segment.Size));
                position += segment.Text.Length;
            }
            var request = new PlatformTextParagraphRequest(text, styles.ToArray(), scenario.Width,
                scenario.Rtl ? PlatformTextParagraphDirection.RightToLeft : PlatformTextParagraphDirection.LeftToRight);
            var nativePixels = new byte[DirectWriteLayoutOracle.CoverageWidth * DirectWriteLayoutOracle.CoverageHeight * 3];
            var expectedCarets = new PlatformTextParagraphCaret[text.Length + 1];
            var expected = DirectWriteLayoutOracle.Draw(scenario.Segments[0].Family, text, scenario.Rtl, layout =>
            {
                for (var index = 0; index < expectedCarets.Length; index++)
                {
                    float x = 0, y = 0;
                    HitMetrics hit = default;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                        (layout, (uint)index, 0, &x, &y, &hit));
                    expectedCarets[index] = new(x, y);
                }
            }, scenario.Segments[0].Size, layout =>
            {
                var start = 0;
                foreach (var segment in scenario.Segments)
                {
                    var range = new TextRange((uint)start, (uint)segment.Text.Length);
                    fixed (char* family = segment.Family)
                        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, TextRange, int>)Method(layout, 31))(layout, family, range));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 32))(layout, segment.Weight, range));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, TextRange, int>)Method(layout, 35))(layout, segment.Size, range));
                    start += segment.Text.Length;
                }
                if (float.IsFinite(scenario.Width))
                {
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, scenario.Width));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
                }
            }, nativePixels, absoluteParagraphPositions: true);
            var paragraph = backend.CreateParagraph(request, vectorMetrics: false);
            var actualGlyphs = paragraph.Runs.ToArray().SelectMany(run => run.Glyphs.ToArray()).OrderBy(glyph => glyph.Y).ThenBy(glyph => glyph.X).ThenBy(glyph => glyph.Index).ToArray();
            var nativeGlyphs = expected.OrderBy(glyph => glyph.Y).ThenBy(glyph => glyph.X).ThenBy(glyph => glyph.Index).ToArray();
            check(actualGlyphs.Length == nativeGlyphs.Length && actualGlyphs.Zip(nativeGlyphs).All(pair => pair.First.Index == pair.Second.Index
                && MathF.Abs(pair.First.X - pair.Second.X) < .002f && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f),
                "Styled native paragraph glyph placement : " + text);
            check(paragraph.Carets.Span.ToArray().Zip(expectedCarets).All(pair => MathF.Abs(pair.First.X - pair.Second.X) < .002f
                && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f), "Styled native paragraph original carets : " + text);
            check(paragraph.Lines.Span.ToArray().Sum(line => line.Length + line.NewlineLength) == text.Length
                && (!float.IsFinite(scenario.Width) || paragraph.Lines.Length > 1), "Styled native paragraph wraps and preserves hard newline source : " + text);
            var pixels = new byte[nativePixels.Length];
            var meshMatches = true;
            foreach (var run in paragraph.Runs.Span)
            {
                var raster = backend.RasterizeParagraphRun(run);
                try
                {
                    for (var y = 0; y < raster.Height; y++)
                        for (var x = 0; x < raster.Width; x++)
                        {
                            var targetX = DirectWriteLayoutOracle.CoverageLeft + (int)raster.Origin.X + x;
                            var targetY = DirectWriteLayoutOracle.CoverageBaseline + (int)raster.Origin.Y + y;
                            if ((uint)targetX >= DirectWriteLayoutOracle.CoverageWidth || (uint)targetY >= DirectWriteLayoutOracle.CoverageHeight)
                                throw new InvalidOperationException("Styled paragraph fixture exceeds the independent canvas.");
                            for (var channel = 0; channel < 3; channel++)
                            {
                                var target = (targetY * DirectWriteLayoutOracle.CoverageWidth + targetX) * 3 + channel;
                                var source = raster.RgbaPixels.Span[(y * raster.Width + x) * 4 + channel];
                                pixels[target] = (byte)(source + (pixels[target] * (255 - source) + 127) / 255);
                            }
                        }
                }
                finally { raster.PixelOwner?.Dispose(); }
                var mesh = backend.CreateParagraphRunMesh(run);
                meshMatches &= mesh.Glyphs.Length == run.Glyphs.Length && mesh.Glyphs.ToArray().Zip(run.Glyphs.ToArray())
                    .All(pair => pair.First.Offset.X == pair.Second.X && pair.First.Offset.Y == pair.Second.Y);
            }
            check(pixels.AsSpan().SequenceEqual(nativePixels), "Styled paragraph pixels equal direct native original-run analysis : " + text);
            check(meshMatches, "Styled paragraph vector paint consumes the same immutable glyph positions : " + text);
            check(paragraph.Runs.ToArray().All(run => run.StyleIndex < styles.Count && run.Glyphs.ToArray().All(glyph => glyph.SourcePosition >= styles[run.StyleIndex].Start
                && glyph.SourcePosition < styles[run.StyleIndex].Start + styles[run.StyleIndex].Length)), "Native paint markers preserve declared style slots : " + text);
        }
        RunCore(check);
        RunCore(check, owned: true);
        RunDirection(check);
        RunMarkdown(check);
        RunObjects(check);
        CheckTabAndHangulSourcePositions(backend, fonts, check);
    }

    private static void CheckTabAndHangulSourcePositions(IPlatformTextParagraphBackend backend, string fonts,
        Action<bool, string> check)
    {
        var font = Path.Combine(fonts, "malgun.ttf");
        const string tab = "A\t한Z";
        const string spaces = "A    한Z";
        const string composed = "A    한Z";
        var tabLayout = Layout(tab);
        var spacesLayout = Layout(spaces);
        var composedLayout = Layout(composed);
        int[] tabToSpaces = [0, 1, 5, 6, 7, 8, 9];
        int[] spacesToTab = [0, 1, 1, 1, 1, 2, 3, 4, 5];
        var tabGlyphs = Glyphs(tabLayout);
        var spacesGlyphs = Glyphs(spacesLayout);
        check(tabLayout.Carets.Span.SequenceEqual(tabToSpaces.Select(i => spacesLayout.Carets.Span[i]).ToArray())
            && spacesGlyphs.Select(glyph => glyph with { SourcePosition = spacesToTab[glyph.SourcePosition] }).SequenceEqual(tabGlyphs),
            "Styled DirectWrite tab expansion preserves original caret and glyph offsets after decomposed Hangul");
        check(spacesGlyphs.Any(glyph => glyph.SourcePosition == spaces.IndexOf('ᄒ'))
            && spacesGlyphs.Any(glyph => glyph.SourcePosition == spaces.IndexOf('Z'))
            && spacesGlyphs.Select(glyph => glyph with { SourcePosition = 0 })
                .SequenceEqual(Glyphs(composedLayout).Select(glyph => glyph with { SourcePosition = 0 })),
            "Styled DirectWrite composes decomposed Hangul and preserves later source offsets without tab expansion");

        PlatformTextParagraphLayout Layout(string text) => backend.CreateParagraph(new(text, [new(0, text.Length, font, null, 14)]), false);

        static PlatformTextParagraphGlyph[] Glyphs(PlatformTextParagraphLayout layout)
            => layout.Runs.ToArray().SelectMany(run => run.Glyphs.ToArray()).ToArray();
    }

    private static void RunCore(Action<bool, string> check, bool owned = false)
    {
        UiInlineTextRun[] spans = [new("abc אב "), new("גד office ", UiInlineTextWeight.Bold), new("123")];
        var text = string.Concat(spans.Select(span => span.Text));
        foreach (var width in new[] { 640f, 100f })
        {
            var expected = new PlatformTextParagraphCaret[text.Length + 1];
            DirectWriteLayoutOracle.Draw("Segoe UI", text, false, layout =>
            {
                for (var index = 0; index < expected.Length; index++)
                {
                    float x = 0, y = 0;
                    HitMetrics hit = default;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                        (layout, (uint)index, 0, &x, &y, &hit));
                    expected[index] = new(x, y);
                }
            }, 14, layout =>
            {
                var range = new TextRange((uint)spans[0].Text.Length, (uint)spans[1].Text.Length);
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 32))(layout, 700, range));
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, width));
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
            });
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using UiWidget inline = owned
                ? new UiInlineFlowWidget(spans.Select(span => (UiWidget)new UiInlineTextSpan(span.Text, span.Weight)).ToArray())
                : new UiInlineText(spans);
            var visual = inline is UiInlineText literal ? literal.VisualLines : ((UiInlineFlowWidget)inline).VisualLines;
            PlatformTextParagraphLayout? first = null;
            var correct = true;
            var reused = true;
            var builds = -1;
            for (var frame = 0; frame < 2; frame++)
            {
                context.NewFrame(new(1f / 60, new(width, 300), new(1, 1)),
                    new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, width, 300), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    inline.Render(ui);
                    var paragraph = visual.NativeParagraph;
                    correct &= paragraph is not null;
                    for (var index = 0; index < expected.Length; index++)
                    {
                        var line = visual.FindLine(index);
                        correct &= MathF.Abs(visual.X(index, line) - expected[index].X) < .002f;
                    }
                    if (frame == 0)
                    {
                        first = paragraph;
                        builds = visual.Builds;
                    }
                    else
                        reused &= ReferenceEquals(first, paragraph) && builds == visual.Builds;
                }));
                using var draw = context.GetDrawData();
            }
            check(correct, $"Actual mixed-weight Core paragraph uses native full-context carets : {width} / owned = {owned}");
            check(reused, $"Actual mixed-weight paragraph snapshot and layout are reused : {width} / owned = {owned}");
            check(visual.Count == first!.Lines.Length && (width == 640 || visual.Count > 1),
                $"Actual Core paragraph consumes native wrapped lines : {width} / owned = {owned}");
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
