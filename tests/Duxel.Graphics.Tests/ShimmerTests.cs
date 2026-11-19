using Duxel.Core;
using Duxel.Gallery;

internal static class ShimmerTests
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
        var child = new Probe();
        var placeholder = Fluent.Skeleton(Fluent.SkeletonItem(new(Width : 180, Size : 24)));
        placeholder.AccessibleName = "Loading test";
        using var shimmer = Fluent.Shimmer(child, placeholder);
        Frame(shimmer.Render);
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Loading test" && element.Node.Busy)
            && child.Paints == 0, "loading Shimmer exposes busy placeholder and does not render hidden content");
        check(context.State.HasActiveAnimations, "visible loading Shimmer schedules the existing Skeleton wave");
        shimmer.IsDataLoaded = true;
        Frame(shimmer.Render);
        check(child.Paints > 0 && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Busy),
            "loaded content becomes live while exiting placeholder is paint-only");
        check(context.State.NextScheduledFrameTimeSeconds > context.State.TimeSeconds,
            "loaded Shimmer schedules a finite crossfade deadline");
        context.State.AdvanceTime(.3f);
        Frame(shimmer.Render);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "settled loaded Shimmer stops placeholder motion and requests no frames");
        for (var i = 0; i < 4; i++) Frame(shimmer.Render);
        var measurements = child.Measurements;
        Frame(shimmer.Render); Frame(shimmer.Render);
        check(child.Measurements == measurements, "settled Shimmer retains stable child measurement");
        shimmer.IsDataLoaded = false;
        Frame(shimmer.Render);
        var paints = child.Paints;
        context.State.AdvanceTime(.08f);
        Frame(shimmer.Render);
        check(child.Paints == paints && tree.CaptureSnapshot().Elements.Any(element => element.Node.Busy),
            "reverse transition freezes retired content paint and restores loading semantics");
        shimmer.Options = new(ReducedMotion : true);
        Frame(shimmer.Render);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "reduced-motion Shimmer freezes the default inherited wave and snaps the fade");
        shimmer.IsDataLoaded = true;
        Frame(shimmer.Render);
        check(!context.State.HasActiveAnimations && child.Paints > paints,
            "reduced-motion load is immediate and restores live content");
        var rejected = false;
        try { _ = new UiShimmerOptions { Transition = new(-1) }.Transition.Validate(); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "invalid Shimmer transition duration is rejected");
        check(Fluent.ComponentNames.Contains("Shimmer") && !Fluent.MissingComponentNames.Contains("Shimmer"),
            "Shimmer has a generated factory and leaves the pending catalog");
        using var page = new ShimmerPage();
        using var pageFrame = new GalleryPageFrame(page);
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples.Count == 1 && page.Samples[0].Source.Contains("Fluent.Shimmer", StringComparison.Ordinal),
            "Shimmer Gallery exposes adjacent public factory source");
        var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "불러오기 상태 전환"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery accepts actual load-state activation");
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples[0].Source.Contains("IsDataLoaded = true", StringComparison.Ordinal),
            "Shimmer inline source follows the actual loaded state");
    }
    internal static void RunGpu(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        var placeholder = Fluent.Skeleton(Fluent.SkeletonItem(new(Width : 180, Size : 24, Paused : true)));
        using var shimmer = Fluent.Shimmer(Fluent.Button("Ready"), placeholder);
        void Draw(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); shimmer.Render(ui); }
        var loading = capture("shimmer-loading", Draw);
        shimmer.IsDataLoaded = true;
        var first = capture("shimmer-fade-start", Draw);
        context.State.AdvanceTime(.1f);
        var middle = capture("shimmer-fade-middle", Draw);
        context.State.AdvanceTime(.3f);
        var loaded = capture("shimmer-loaded", Draw);
        check(!loading.AsSpan().SequenceEqual(loaded), "Shimmer actual Vulkan loading and content images differ");
        check(!middle.AsSpan().SequenceEqual(first) && !middle.AsSpan().SequenceEqual(loaded),
            "Shimmer GPU crossfade produces a distinct intermediate image");
        var stable = capture("shimmer-stable", Draw);
        check(loaded.AsSpan().SequenceEqual(stable), "settled Shimmer preserves exact pixels");
        shimmer.IsDataLoaded = false;
        capture("shimmer-reverse-start", Draw);
        context.State.AdvanceTime(.3f);
        var reversed = capture("shimmer-reversed", Draw);
        check(loading.AsSpan().SequenceEqual(reversed), "Shimmer reverse transition restores the exact paused placeholder");
    }
    private sealed class Probe : UiControlWidget
    {
        internal int Measurements, Paints;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measurements++; return new(160, 24); }
        protected override void RenderControl(UiImmediateContext ui) { Paints++; ui.Dummy(new(160, 24)); }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
