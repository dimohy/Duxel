using Duxel.Core;
using Duxel.Gallery;

internal static class ProgressIndicatorTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(.016f, new(1280, 5000), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 1280, 5000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); render(ui); }));
            using var draw = context.GetDrawData();
        }
        using var indicator = Fluent.ProgressIndicator(new(Label : "Upload", Description : "Files are being sent.",
            PercentComplete : .25f, Width : 300, ValueText : "One of four files"));
        UiAccessibilityNode Progress() => tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.ProgressBar).Node;
        Frame(indicator.Render);
        var node = Progress();
        check(node.Name == "Upload" && node.Range?.Value == 25 && node.Range?.Maximum == 100
            && !node.Focusable && !node.ValuePattern && !node.Busy,
            "ProgressIndicator publishes a named read-only nonfocusable requested percentage");
        check(node.GetHelpText() == "Files are being sent." && node.ItemStatus == "One of four files",
            "ProgressIndicator associates description and alternative status with its native progress node");
        check(node.Bounds.Width == 300 && node.Bounds.Height == 2, "ProgressIndicator default bar is two logical pixels high");
        indicator.Options = indicator.Options with { PercentComplete = .75f };
        Frame(indicator.Render);
        check(Progress().Range?.Value == 75, "native percentage reports requested progress during visual interpolation");
        context.State.AdvanceTime(.5f); Frame(indicator.Render);
        check(!context.State.HasActiveAnimations, "settled determinate progress requests no continuous animation");
        indicator.Options = indicator.Options with { ProgressHidden = true, PercentComplete = null };
        Frame(indicator.Render);
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.ProgressBar)
            && tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Upload") && !context.State.HasActiveAnimations,
            "hiding the bar keeps label and description without progress semantics or animation");
        indicator.Options = indicator.Options with { ProgressHidden = false };
        Frame(indicator.Render);
        check(Progress().Busy && Progress().Range is null && context.State.HasActiveAnimations,
            "unspecified completion exposes indeterminate progress and visible paced motion");
        indicator.Options = indicator.Options with { ReducedMotion = true, BarHeight = 6 };
        Frame(indicator.Render);
        CheckReducedMotion(context, Progress(), check);
        indicator.Options = indicator.Options with { Label = null, Description = null, AccessibleLabel = "Background operation", PercentComplete = 2 };
        Frame(indicator.Render);
        check(Progress().Name == "Background operation" && Progress().Range?.Value == 100,
            "accessible-only labels and finite completion above one clamp to100percent");
        indicator.Options = indicator.Options with { PercentComplete = -1 };
        indicator.AccessibleName = "Override";
        Frame(indicator.Render);
        check(Progress().Name == "Override" && Progress().Range?.Value == 0,
            "ordinary AccessibleName overrides naming and negative completion clamps to zero");
        var before = indicator.Options;
        var rejected = false;
        try { indicator.Options = before with { Width = float.NaN }; }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && indicator.Options == before, "invalid geometry fails before changing live options");
        check(Fluent.ComponentNames.Contains("ProgressIndicator") && !Fluent.MissingComponentNames.Contains("ProgressIndicator"),
            "ProgressIndicator is exposed through the generated factory and leaves the pending catalog");
        using var page = new ProgressIndicatorPage();
        using var pageFrame = new GalleryPageFrame(page);
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples.Count == 2 && page.Samples.All(sample => sample.Source.Contains("Fluent.ProgressIndicator", StringComparison.Ordinal)),
            "determinate and waiting Gallery examples each own adjacent public factory source");
        var button = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "파일 업로드 진행률 증가"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery progress option accepts actual activation");
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples[0].Source.Contains("PercentComplete : 0.5f", StringComparison.Ordinal),
            "ProgressIndicator inline source follows its actual updated completion");
    }
    internal static void RunGpu(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        using var indicator = Fluent.ProgressIndicator(new(Label : "Upload", Description : "Sending files", PercentComplete : .25f, Width : 240));
        void Draw(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); indicator.Render(ui); }
        var first = capture("indicator-quarter", Draw);
        indicator.Options = indicator.Options with { PercentComplete = .75f };
        capture("indicator-transition", Draw);
        context.State.AdvanceTime(.5f);
        var changed = capture("indicator-three-quarters", Draw);
        check(!first.AsSpan().SequenceEqual(changed), "ProgressIndicator real Vulkan fill changes with completion");
        var stable = capture("indicator-stable", Draw);
        check(changed.AsSpan().SequenceEqual(stable), "settled ProgressIndicator preserves exact pixels");
        indicator.Options = indicator.Options with { ProgressHidden = true };
        var hidden = capture("indicator-hidden", Draw);
        check(!hidden.AsSpan().SequenceEqual(changed), "hiding ProgressIndicator removes its actual track and closes layout gaps");
        indicator.Options = indicator.Options with { ProgressHidden = false, PercentComplete = .25f };
        capture("indicator-restore-start", Draw);
        context.State.AdvanceTime(.5f);
        var restored = capture("indicator-restored", Draw);
        check(first.AsSpan().SequenceEqual(restored), "restoring ProgressIndicator completion restores exact original pixels");
    }
    private static void CheckReducedMotion(UiContext context, UiAccessibilityNode node, Action<bool, string> check)
        => check(context.State.HasActiveAnimations && node.Busy && node.Range is null && node.Bounds.Height == 6,
            "reduced motion uses the existing uniform opacity pulse with a custom-height indeterminate bar");
    internal static void RunReducedMotion(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var indicator = Fluent.ProgressIndicator(new(ReducedMotion : true, BarHeight : 6));
        context.NewFrame(new(.016f, new(320, 240), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); indicator.Render(ui); }));
        using var draw = context.GetDrawData();
        CheckReducedMotion(context, tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.ProgressBar).Node, check);
    }
    internal static void RunReducedMotionGpu(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiContext context, int width)
    {
        using var indicator = Fluent.ProgressIndicator(new(ReducedMotion : true, BarHeight : 6, Width : 240));
        void Draw(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); indicator.Render(ui); }
        context.State.AdvanceTime((float)((3 - context.State.TimeSeconds % 3) % 3 + .25));
        var first = capture("indicator-reduced-first", Draw);
        context.State.AdvanceTime(.75f);
        var second = capture("indicator-reduced-second", Draw);
        ReadOnlySpan<byte> Pixel(byte[] data, int x) => data.AsSpan((2 * width + x) * 4, 4);
        check(Pixel(first, 20).SequenceEqual(Pixel(first, 200)) && !Pixel(first, 20).SequenceEqual(Pixel(first, 300)),
            "reduced-motion Vulkan track is uniformly filled rather than moving horizontally");
        check(!Pixel(first, 20).SequenceEqual(Pixel(second, 20)) && Pixel(second, 20).SequenceEqual(Pixel(second, 200)),
            "reduced-motion Vulkan opacity changes over time without spatial translation");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
