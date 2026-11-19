using System.Globalization;
using Duxel.Core;

internal static class InlineTextTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }

        UiInlineTextRun[] runs = [new("제목 **literal** ", UiInlineTextWeight.Semibold), new("한글 body and English.")];
        using var text = new UiInlineText(runs);
        var expectedText = string.Concat(runs.Select(x => x.Text));
        runs[0] = new("External replacement");
        check(text.Text == expectedText && text.Runs[0].Weight == UiInlineTextWeight.Semibold,
            "Inline text snapshots caller runs and preserves literal markup rather than parsing a document");
        Frame(ui =>
        {
            var position = ui.GetCursorScreenPos();
            var size = text.Measure(ui, new(400, 300));
            check(size.Y == 20 && ui.GetCursorScreenPos() == position,
                "Inline measurement has one Body1 line and does not move the caller cursor");
            check(tree.CaptureSnapshot().Elements.Count == 0,
                "Inline measurement does not publish accessibility or process input");
            text.Render(ui);
            check(ui.GetItemRectSize().Y == 20, "Mixed title and body occupy one line rather than two stacked labels");
        });
        var firstNode = tree.CaptureSnapshot().Elements.Single().Node;
        check(firstNode.Name == expectedText && firstNode.Role == UiAccessibilityRole.Label
            && !firstNode.Focusable && !firstNode.ValuePattern && firstNode.ReadOnly,
            "Inline root exposes complete literal text as one passive accessibility label");
        var builds = text.LayoutBuilds;
        for (var frame = 0; frame < 4; frame++)
            Frame(text.Render);
        check(text.LayoutBuilds == builds, "Repeated frames retain mixed-weight layout without reparsing or remeasuring");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "Static inline text schedules no animation or periodic frame");
        Frame(ui =>
        {
            using (ui.BeginStyledTypography(new(Flags: UiStyledTextFlags.Semibold)))
                check(ui.CurrentControlTextLayoutKey.Primary!.EndsWith("seguisb.ttf", StringComparison.OrdinalIgnoreCase),
                    "Inline semibold selects the actual600 face rather than the700 bold face");
            using (ui.BeginStyledTypography(new(Flags: UiStyledTextFlags.Bold)))
                check(ui.CurrentControlTextLayoutKey.Primary!.EndsWith("segoeuib.ttf", StringComparison.OrdinalIgnoreCase),
                    "Existing styled bold continues selecting the700 face");
            text.Render(ui);
        });
        text.Color = new(255, 0, 0);
        Frame(text.Render);
        check(text.LayoutBuilds == builds, "Changing foreground alone preserves inline measurements");
        text.Runs = text.Runs.ToArray();
        Frame(text.Render);
        check(text.LayoutBuilds == builds, "Replacing runs with equal values preserves inline measurements");

        text.Runs = [new("Title ", UiInlineTextWeight.Semibold), new("Several words 한글 must wrap without truncating the body or title.")];
        Frame(ui => text.Measure(ui, new(90, 300)));
        var narrow = text.VisualLines.Height;
        check(text.VisualLines.Count > 1 && text.VisualLines.Width <= 90.01f,
            "Mixed-weight content wraps at the actual available width");
        check(string.Concat(Enumerable.Range(0, text.VisualLines.Count).Select(x => text.VisualLines[x].Text)) == text.Text,
            "Wrapped inline lines retain every literal source character");
        var narrowBuilds = text.LayoutBuilds;
        Frame(ui => text.Measure(ui, new(90, 300)));
        check(text.LayoutBuilds == narrowBuilds, "Stable narrow measurements reuse the retained visual lines");
        Frame(ui => text.Measure(ui, new(400, 300)));
        check(text.VisualLines.Height < narrow && text.LayoutBuilds == narrowBuilds + 1,
            "A wider allocation invalidates once and reflows back to fewer lines");
        var wide = text.VisualLines.Height;
        for (var frame = 0; frame < 4; frame++)
            Frame(ui =>
            {
                var first = text.Measure(ui, new(90, 300));
                var second = text.Measure(ui, new(400, 300));
                check(first.Y == narrow && second.Y == wide, "Alternating text widths preserve their respective line breaks");
            });
        check(text.LayoutBuilds == narrowBuilds + 1,
            "Warm scrollbar width probes reuse both native layouts across frames");

        Frame(text.Render);
        check(tree.CaptureSnapshot().Elements.Single().Node.Id == firstNode.Id,
            "Inline content changes preserve passive identity within the same parent scope");
        using var sized = Dux.SizedBox(text, 90, 140);
        Frame(sized.Render);
        var allocatedId = tree.CaptureSnapshot().Elements.Single().Node.Id;
        check(tree.CaptureSnapshot().Elements.Single().Node.Bounds == new UiRect(0, 0, 90, 140),
            "Inline root honors its parent's assigned width and height");
        Frame(sized.Render);
        check(tree.CaptureSnapshot().Elements.Single().Node.Id == allocatedId,
            "Repeated inline parent allocation preserves its scoped passive identity");
        Frame(ui => { using var clip = new UiControlContext(ui).Clip(new(7, 11, 35, 24)); sized.Render(ui); });
        check(tree.CaptureSnapshot().Elements.Single().Node.Bounds == new UiRect(7, 11, 35, 24),
            "Inline accessibility follows the actual parent clip");
        sized.Transform = new(RotationDegrees: 90);
        Frame(sized.Render);
        var rotated = tree.CaptureSnapshot().Elements.Single().Node.Bounds;
        check(MathF.Abs(rotated.Width - 140) < .01f && MathF.Abs(rotated.Height - 90) < .01f,
            "Inline text uses the common widget transform path");
        sized.Transform = new(RotationDegrees: 0);
        Frame(ui => { ui.BeginDisabled(); try { sized.Render(ui); } finally { ui.EndDisabled(); } });
        check(!tree.CaptureSnapshot().Elements.Single().Node.Enabled, "Inline accessibility inherits disabled state");

        text.Wrap = false;
        Frame(ui => text.Measure(ui, new(90, 300)));
        check(text.VisualLines.Count == 1 && text.VisualLines.Width > 90,
            "Explicit unwrapped inline content retains its full measured advance without ellipsis");
        text.Wrap = true;
        // The native paragraph contract requires explicit coverage; Segoe UI semibold has no family-emoji glyphs.
        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var emoji = Path.Combine(fontDirectory, "seguiemj.ttf");
        if (!File.Exists(emoji))
            throw new FileNotFoundException("The grapheme-wrap fixture requires its explicit Segoe UI Emoji font.", emoji);
        context.SetTypographyFonts(new(Path.Combine(fontDirectory, "segoeui.ttf"), emoji, Path.Combine(fontDirectory, "segoeuib.ttf"),
            Path.Combine(fontDirectory, "malgun.ttf"), Path.Combine(fontDirectory, "malgun.ttf"), Path.Combine(fontDirectory, "malgunbd.ttf"))
        {
            Monospace = Path.Combine(fontDirectory, "consola.ttf")
        });
        text.Runs = [new("A\u0301 "), new("👩‍👩‍👧‍👦", UiInlineTextWeight.Semibold), new(" 정세일")];
        Frame(ui => text.Measure(ui, new(1, 300)));
        var boundaries = StringInfo.ParseCombiningCharacters(text.Text).Append(text.Text.Length).ToHashSet();
        check(Enumerable.Range(0, text.VisualLines.Count).All(x => boundaries.Contains(text.VisualLines[x].Start)
            && boundaries.Contains(text.VisualLines[x].End)), "Narrow inline wrap never splits a combining mark or emoji grapheme");
        ControlFontFixture.Configure(context);
        text.Runs = [new("첫 줄\r\n", UiInlineTextWeight.Semibold), new("둘째 줄\n셋째 줄")];
        Frame(ui => text.Measure(ui, new(400, 300)));
        check(text.VisualLines.Count == 3 && text.VisualLines.Height == 60,
            "Explicit CRLF and LF line breaks preserve the Body1 line rhythm");
        var joined = new UiInlineTextSource();
        joined.Replace([new("A"), new(string.Empty, UiInlineTextWeight.Bold), new("\u0301 AV")]);
        check(joined.Runs.Count == 1 && joined.Runs[0].Display == "A\u0301 AV",
            "Adjacent equal faces shape as one run, including combining marks and kerning across caller boundaries");
        var splitRejected = false;
        try
        {
            joined.Replace([new("A"), new("\u0301", UiInlineTextWeight.Semibold)]);
        }
        catch (ArgumentException) { splitRejected = true; }
        check(splitRejected && joined.Source == "A\u0301 AV",
            "A face change inside one Unicode text element fails atomically rather than breaking its glyph");
        Frame(ui =>
        {
            using var repeated = new UiInlineText([new("same"), new(" same")]);
            repeated.Render(ui);
            check(repeated.VisualLines.StyledSource!.Projection.Runs.Length == 1,
                "The actual widget consumes the merged literal projection rather than retaining split shaping runs");
        });
        text.Runs = [];
        Frame(text.Render);
        check(text.Text.Length == 0 && text.VisualLines.Count == 1 && text.VisualLines.Height == 20,
            "Empty inline content is a valid one-line text root");
        sized.Options = new(0, 0);
        Frame(sized.Render);
        check(tree.CaptureSnapshot().Elements.Single().Node.Bounds == default,
            "Zero parent allocation does not fabricate visible inline geometry");
        Frame(_ => { });
        check(tree.CaptureSnapshot().Elements.Count == 0, "Unmounted inline text retires its passive accessibility root");

        foreach (var invalid in new UiInlineTextRun[][] { [new(null!)], [new("invalid", (UiInlineTextWeight)3)] })
        {
            var rejected = false;
            try
            {
                text.Runs = invalid;
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected && text.Text.Length == 0, "Invalid inline run replacement fails atomically without losing valid content");
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
