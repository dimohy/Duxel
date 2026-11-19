using System.Runtime.InteropServices;
using Duxel.Core;

internal static unsafe partial class StyledParagraphTests
{
    private static void RunMarkdown(Action<bool, string> check)
    {
        const string source = "abc **אב גד** 123";
        const string displayed = "abc אב גד 123";
        int[] sourceMap = [0, 1, 2, 3, 4, 4, 4, 5, 6, 7, 8, 9, 9, 9, 10, 11, 12, 13];
        foreach (var width in new[] { 640f, 65f })
        {
            var expected = new PlatformTextParagraphCaret[displayed.Length + 1];
            DirectWriteLayoutOracle.Draw("Segoe UI", displayed, false, layout =>
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
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 32))
                    (layout, 700, new(4, 5)));
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, width));
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
            });
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var styled = new UiMarkdownTextSource(new(source));
            var visual = new UiVisualTextLines();
            PlatformTextParagraphLayout? first = null;
            for (var frame = 0; frame < 2; frame++)
            {
                context.NewFrame(new(1f / 60, new(width, 300), new(1, 1)),
                    new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, width, 300), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    visual.Prepare(ui, source, width, 300, 20, 0, styled: styled);
                    var native = visual.NativeParagraph;
                    check(native is not null && native.Carets.Length == displayed.Length + 1 && styled.Projection.Text == displayed,
                        "Markdown uses projected whole-paragraph native text : " + width);
                    check(styled.Projection.SourceToDisplay.AsSpan().SequenceEqual(sourceMap), "Markdown independent raw-source mapping : " + width);
                    var correct = true;
                    for (var index = 0; index < sourceMap.Length; index++)
                    {
                        var line = visual.FindLine(index);
                        var display = sourceMap[index];
                        // The trailing edge of a soft-wrapped row has a distinct native affinity.
                        if (line + 1 < visual.Count && display == native!.Lines.Span[line + 1].Start)
                            continue;
                        correct &= MathF.Abs(visual.X(index, line) - expected[display].X) < .002f;
                    }
                    check(correct, "Markdown original-source carets equal independent styled DirectWrite layout : " + width);
                    check(visual[0].Start == 0 && visual[visual.Count - 1].End == source.Length,
                        "Markdown wrapped lines retain hidden source syntax boundaries : " + width);
                    if (frame == 0)
                        first = native;
                    else
                        check(ReferenceEquals(first, native), "Markdown native paragraph snapshot is retained : " + width);
                }));
                using var draw = context.GetDrawData();
            }
            visual.Clear();
            styled.Release();
        }
    }
}
