using System.Runtime.InteropServices;
using Duxel.Core;

internal static unsafe partial class StyledParagraphTests
{
    private static void RunDirection(Action<bool, string> check)
    {
        UiInlineTextRun[] spans = [new("abc אב "), new("גד office ", UiInlineTextWeight.Bold), new("123")];
        var text = string.Concat(spans.Select(span => span.Text));
        foreach (var width in new[] { 640f, 100f })
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var inline = new UiInlineText(spans);
            var content = inline.Runs;
            PlatformTextParagraphLayout? previous = null;
            foreach (var rtl in new[] { false, true, false })
            {
                var expected = new PlatformTextParagraphCaret[text.Length + 1];
                DirectWriteLayoutOracle.Draw("Segoe UI", text, rtl, layout =>
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
                    var bold = new TextRange((uint)spans[0].Text.Length, (uint)spans[1].Text.Length);
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, TextRange, int>)Method(layout, 32))(layout, 700, bold));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, int>)Method(layout, 28))(layout, width));
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Method(layout, 5))(layout, 0));
                });
                inline.RightToLeft = rtl;
                PlatformTextParagraphLayout? current = null;
                var matched = true;
                var retained = true;
                var builds = -1;
                for (var frame = 0; frame < 2; frame++)
                {
                    // Assigning an unchanged direction must not invalidate source or geometry.
                    inline.RightToLeft = rtl;
                    context.NewFrame(new(1f / 60, new(width, 300), new(1, 1)),
                        new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                        new(0, 0, width, 300), UiTextSettings.Default);
                    context.Render(new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        inline.Render(ui);
                        var paragraph = inline.VisualLines.NativeParagraph!;
                        matched &= paragraph.Carets.ToArray().Zip(expected).All(pair =>
                            MathF.Abs(pair.First.X - pair.Second.X) < .002f && MathF.Abs(pair.First.Y - pair.Second.Y) < .002f);
                        if (frame == 0)
                        {
                            current = paragraph;
                            builds = inline.VisualLines.Builds;
                        }
                        else
                            retained &= ReferenceEquals(current, paragraph) && inline.VisualLines.Builds == builds;
                    }));
                    using var data = context.GetDrawData();
                }
                check(matched, $"Actual public paragraph direction uses native carets : {width} / {rtl}");
                check(retained, $"Unchanged public paragraph direction retains native layout : {width} / {rtl}");
                check(previous is null || !ReferenceEquals(previous, current), $"Changed direction replaces the native paragraph : {width} / {rtl}");
                check(inline.Text == text && ReferenceEquals(content, inline.Runs), $"Direction changes preserve original literal source and owned runs : {width} / {rtl}");
                previous = current;
            }
        }
        using var noBackend = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var unsupported = new UiInlineText([new("abc אב")]) { RightToLeft = true };
        var rejected = false;
        try
        {
            noBackend.NewFrame(new(1f / 60, new(200, 100), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 200, 100), UiTextSettings.Default);
            noBackend.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); unsupported.Render(ui); }));
        }
        catch (NotSupportedException) { rejected = true; }
        check(rejected, "Unsupported explicit direction fails instead of silently drawing a different paragraph");
    }
}
