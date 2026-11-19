using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ToastParagraphTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var toast = new UiToastWidget(new("مرحبا אבג 한글 English 123", "English אבג 한글 مرحبا mixed paragraph")
            { Timeout = null, Progress = new(.25f, "התקדמות 25% English"), CanDismiss = true });
        void Frame()
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); toast.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        foreach (var rtl in new[] { false, true })
        {
            toast.RightToLeft = rtl; Frame();
            foreach (var item in new[] { (Widget : toast.Title, Semibold : true, Size : 14f),
                (Widget : toast.Body, Semibold : false, Size : 14f), (Widget : toast.Status, Semibold : false, Size : 12f) })
            {
                var actual = item.Widget.VisualLines.NativeParagraph;
                var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                var font = Path.Combine(fonts, item.Semibold ? "seguisb.ttf" : "segoeui.ttf");
                var secondary = Path.Combine(fonts, item.Semibold ? "malgunbd.ttf" : "malgun.ttf");
                var expected = WindowsPlatformTextBackend.Instance.CreateParagraph(new(item.Widget.Text,
                    [new(0, item.Widget.Text.Length, font, secondary, item.Size)], item.Widget.VisualLines.EffectiveWidth,
                    rtl ? PlatformTextParagraphDirection.RightToLeft : PlatformTextParagraphDirection.LeftToRight), false);
                check(actual is not null && SameRuns(actual, expected),
                    (rtl ? "RTL" : "LTR") + " Toast paragraph forwards original text, native bidi direction and typography : " + item.Size + "/" + item.Semibold);
            }
            check(toast.Title.VisualLines.LineHeight(0) == 20 && toast.Body.VisualLines.LineHeight(0) == 20
                && toast.Status.VisualLines.LineHeight(0) == 16,
                (rtl ? "RTL" : "LTR") + " native Toast paragraph preserves title/body twenty-pixel and caption sixteen-pixel line boxes");
            check(toast.Title.HorizontalAlignment == (rtl ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left)
                && toast.Body.HorizontalAlignment == toast.Title.HorizontalAlignment && toast.Status.HorizontalAlignment == toast.Title.HorizontalAlignment,
                (rtl ? "RTL" : "LTR") + " Toast aligns title, body and caption at their logical leading edge");
            var title = toast.Title.VisualLines.NativeParagraph;
            var status = toast.Status.VisualLines.NativeParagraph;
            Frame();
            check(ReferenceEquals(title, toast.Title.VisualLines.NativeParagraph) && ReferenceEquals(status, toast.Status.VisualLines.NativeParagraph),
                (rtl ? "RTL" : "LTR") + " unchanged Toast paragraphs retain native layouts across frames");
        }
    }
    private static bool SameRuns(PlatformTextParagraphLayout actual, PlatformTextParagraphLayout expected)
    {
        if (actual.Runs.Length != expected.Runs.Length || actual.Lines.Length != expected.Lines.Length) return false;
        for (var i = 0; i < actual.Lines.Length; i++)
        {
            var a = actual.Lines.Span[i]; var b = expected.Lines.Span[i];
            if (a.Start != b.Start || a.Length != b.Length || a.NewlineLength != b.NewlineLength
                || a.Top != b.Top || a.Height != b.Height || a.Baseline != b.Baseline || a.Advance != b.Advance) return false;
        }
        for (var i = 0; i < actual.Runs.Length; i++)
        {
            var a = actual.Runs.Span[i]; var b = expected.Runs.Span[i];
            if (a.FontPath != b.FontPath || a.FontSize != b.FontSize || a.BidiLevel != b.BidiLevel || a.Origin != b.Origin
                || !a.Glyphs.Span.SequenceEqual(b.Glyphs.Span)) return false;
        }
        return true;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
