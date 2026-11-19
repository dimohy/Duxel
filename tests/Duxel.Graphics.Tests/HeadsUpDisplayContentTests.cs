using Duxel.Core;
using Duxel.Gallery;

internal static class HeadsUpDisplayContentTests
{
    internal static void Run(Action<bool, string> check, bool galleryOnly = false)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        UiVector2 Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(.016f, new(500, 500), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 500, 500), UiTextSettings.Default);
            var size = default(UiVector2);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                render(ui);
                size = ui.GetItemRectSize();
            }));
            using var draw = context.GetDrawData();
            return size;
        }
        if (!galleryOnly)
        {
            using var hud = new UiHeadsUpDisplayContent();
            check(Frame(hud.Render) == new UiVector2(100, 100) && context.State.HasActiveAnimations,
                "unlabelled HUD reserves the official100pixel square and animates activity");
            var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node;
            check(node.Busy && !node.Focusable && !node.ValuePattern,
                "noninteractive HUD exposes a busy image without a value or focus target");
            hud.Options = hud.Options with { Label = "저장 중" };
            var labelled = Frame(hud.Render);
            check(labelled.X == labelled.Y && labelled.X >= 100 && labelled.X <= 192,
                "labelled HUD grows as a square within the100to192pixel token range");
            var fullLabel = string.Concat(Enumerable.Repeat("한글 e\u0301 저장 상태를 확인합니다. ", 30));
            hud.Options = hud.Options with { Label = fullLabel, ReducedMotion = true };
            check(Frame(hud.Render) == new UiVector2(192, 192) && !context.State.HasActiveAnimations,
                "very long labels stay within the maximum square without motion requests");
            node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node;
            check(node.Name == fullLabel && node.Busy, "visually shortened HUD labels retain their full accessible name and busy state");
            hud.Options = hud.Options with { Kind = UiHeadsUpDisplayKind.Success, Label = "저장 완료", ReducedMotion = false };
            Frame(hud.Render);
            node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node;
            check(!node.Busy && !context.State.HasActiveAnimations, "success HUD stops activity and schedules no further frames");
            hud.Options = hud.Options with { Kind = UiHeadsUpDisplayKind.Failure };
            Frame(hud.Render);
            check(!context.State.HasActiveAnimations, "failure HUD remains static");
            var taps = 0;
            hud.Tapped = () => taps++;
            Frame(hud.Render);
            var button = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button);
            check(button.Node.Focusable && tree.RequestAction(button.Node.Id, button.RuntimeId,
                UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "HUD with a tap callback accepts ordinary accessibility invocation");
            Frame(hud.Render);
            check(taps == 1, "HUD tap invokes the application callback once without cancelling the operation itself");
            var before = hud.Options;
            var rejected = false;
            try { hud.Options = before with { Kind = UiHeadsUpDisplayKind.Custom }; }
            catch (ArgumentException) { rejected = true; }
            check(rejected && hud.Options == before, "missing custom HUD icons fail before replacing live options");
            hud.Options = before with { Kind = UiHeadsUpDisplayKind.Custom, Icon = UiMenuIcon.Create<TestIcon, TestIcon>() };
            Frame(hud.Render);
            check(TestIcon.Paints > 0 && TestIcon.Bounds.Width == 36 && TestIcon.Bounds.Height == 36,
                "custom HUD icon receives the shared36pixel template slot");
            hud.AccessibleName = "Custom status";
            Frame(hud.Render);
            check(tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button).Node.Name == "Custom status",
                "HUD honors the common widget accessible name override");
            using var fontProbe = new UiText<ProbePresentation>("Scale");
            Frame(fontProbe.Render);
            check(ProbePresentation.Scaled.X < ProbePresentation.Full.X && ProbePresentation.Scaled.Y < ProbePresentation.Full.Y
                && ProbePresentation.After == ProbePresentation.Full,
                "scaled typography measures smaller and restores its outer font and line metrics");
            using var zero = Dux.ConstrainedBox(new UiHeadsUpDisplayContent(), new(0, 0, 0, 0));
            check(Frame(zero.Render) == default && !context.State.HasActiveAnimations,
                "zero allocation retires HUD paint and animation requests without throwing");
            using var narrow = Dux.ConstrainedBox(new UiHeadsUpDisplayContent(new(Kind : UiHeadsUpDisplayKind.Success)), new(0, 50, 0, 50));
            check(Frame(narrow.Render) == new UiVector2(50, 50), "tight HUD allocation clips its natural minimum square without a layout exception");
        }
        using var page = new HeadsUpDisplayPage();
        using var frame = new GalleryPageFrame(page);
        Frame(frame.Render); Frame(frame.Render);
        check(page.Samples.Count == 5 && page.Samples.Count(sample => sample.Source.Contains("new UiHeadsUpDisplayContent(", StringComparison.Ordinal)) == 4
            && Fluent.ComponentNames.Contains("HeadsUpDisplay") && !Fluent.MissingComponentNames.Contains("HeadsUpDisplay"),
            "Gallery exposes the centered HUD and four body examples with adjacent source");
        var reduce = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "HUD 모션 줄이기"
            && element.Node.Role == UiAccessibilityRole.Button);
        tree.RequestAction(reduce.Node.Id, reduce.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame(frame.Render); Frame(frame.Render);
        check(page.Samples.All(sample => sample.Source.Contains("ReducedMotion : true", StringComparison.Ordinal)),
            "HUD Gallery source follows the actual reduced-motion setting");
    }
    internal static void RunGalleryGpu(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1280, 900));
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true };
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
        fixture.Context.SetAccessibilityTree(tree);
        gallery.SelectedPage = "HeadsUpDisplay";
        void Draw(UiImmediateContext ui)
        {
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, 1280, 900), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            gallery.Render(ui);
        }
        fixture.Capture(Draw);
        var pixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-hud-preview.png"), 1280, 900, pixels);
        var nodes = tree.CaptureSnapshot().Elements;
        check(gallery.SelectedNavigationLabel == "HeadsUpDisplay"
            && nodes.Any(element => element.Node.Role == UiAccessibilityRole.Image && element.Node.Name == "저장 중" && element.Node.Busy),
            "actual Gallery route displays the HUD preview instead of the empty pending page");
        var reduce = nodes.Single(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "HUD 모션 줄이기");
        check(tree.RequestAction(reduce.Node.Id, reduce.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "actual Gallery HUD motion option accepts activation");
        fixture.Capture(Draw); fixture.Capture(Draw);
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Image
            && element.Node.Name == "저장 중" && element.Node.Busy),
            "Gallery reduced motion retains the actual busy status");
        var show = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "성공 표시");
        tree.RequestAction(show.Node.Id, show.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        fixture.Capture(Draw);
        pixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-hud-centered.png"), 1280, 900, pixels);
        var dialog = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Dialog).Node;
        check(dialog.Modal && dialog.Name == "저장 완료" && MathF.Abs(dialog.Bounds.X + dialog.Bounds.Width / 2 - 640) < .01f
            && MathF.Abs(dialog.Bounds.Y + dialog.Bounds.Height / 2 - 450) < .01f,
            "actual Gallery success action opens its centered modal HUD");
        var dismiss = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "저장 완료");
        tree.RequestAction(dismiss.Node.Id, dismiss.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        fixture.Capture(Draw);
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog),
            "actual Gallery HUD tap dismisses the portal through its application callback");
    }
    internal static void RunGpu(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        using var hud = new UiHeadsUpDisplayContent(new(Label : "저장 중"));
        void Draw(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); hud.Render(ui); }
        var first = capture("hud-activity", Draw);
        context.State.AdvanceTime(.1875f);
        var moving = capture("hud-activity-moving", Draw);
        check(!first.AsSpan().SequenceEqual(moving), "HUD paints a changing native activity arc");
        hud.Options = hud.Options with { Kind = UiHeadsUpDisplayKind.Success, Label = "완료" };
        var success = capture("hud-success", Draw);
        hud.Options = hud.Options with { Kind = UiHeadsUpDisplayKind.Failure };
        var failure = capture("hud-failure", Draw);
        check(!success.AsSpan().SequenceEqual(failure), "HUD success and failure have distinct vector geometry");
        context.State.AdvanceTime(.4f);
        check(failure.AsSpan().SequenceEqual(capture("hud-failure-stable", Draw)) && !context.State.HasActiveAnimations,
            "completed HUD paint remains exactly stable with no animation requests");
        hud.Options = hud.Options with { Label = string.Concat(Enumerable.Repeat("한글 e\u0301 긴 작업 설명 ", 30)) };
        var longLabel = capture("hud-long-label", Draw);
        check(!failure.AsSpan().SequenceEqual(longLabel), "HUD paints its bounded long-label layout");
        hud.Options = hud.Options with { Kind = UiHeadsUpDisplayKind.Custom, Icon = UiMenuIcon.Create<TestIcon, TestIcon>() };
        check(!longLabel.AsSpan().SequenceEqual(capture("hud-custom", Draw)), "custom HUD icons paint through the same template color and clip pipeline");
        var empty = capture("hud-empty", ui => ui.EnableRootViewportContentLayout(contentPadding : 0));
        using var zero = Dux.ConstrainedBox(new UiHeadsUpDisplayContent(), new(0, 0, 0, 0));
        var zeroPixels = capture("hud-zero", ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); zero.Render(ui); });
        check(zeroPixels.AsSpan().SequenceEqual(empty) && !context.State.HasActiveAnimations,
            "zero allocation produces exactly empty Vulkan output without activity requests");
        using var narrow = Dux.ConstrainedBox(new UiHeadsUpDisplayContent(new(Kind : UiHeadsUpDisplayKind.Success)), new(0, 50, 0, 50));
        var narrowPixels = capture("hud-narrow", ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); narrow.Render(ui); });
        var width = 320;
        var outsideUnchanged = true;
        for (var i = 0; i < narrowPixels.Length; i += 4)
        {
            var pixel = i / 4;
            if (pixel % width < 50 && pixel / width < 50) continue;
            if (!narrowPixels.AsSpan(i, 4).SequenceEqual(empty.AsSpan(i, 4))) outsideUnchanged = false;
        }
        check(outsideUnchanged && !narrowPixels.AsSpan().SequenceEqual(empty),
            "tight allocation paints its clipped HUD and preserves every pixel outside its assigned box");
    }
    private readonly struct TestIcon : IUiButtonIcon
    {
        internal static int Paints;
        internal static UiRect Bounds;
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
        {
            Paints++;
            Bounds = bounds;
            context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), 12, color);
        }
    }
    private readonly struct ProbePresentation : IUiTextPresentation
    {
        internal static UiVector2 Full, Scaled, After;
        public static UiVector2 Measure(UiControlContext context, string text)
        {
            using var outer = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
            Full = context.MeasureTextBlock(text).Size;
            using (var inner = context.BeginTypography<UiRegularTypeface, UiFluentType300>(scale : .6f))
                Scaled = context.MeasureTextBlock(text).Size;
            After = context.MeasureTextBlock(text).Size;
            return Full;
        }
        public static void Render(UiControlContext context, string text, UiColor? color)
        {
            Measure(context, text);
            using var font = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
            context.PaintText(text, context.LayoutText(text), color ?? context.Theme.Text);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
