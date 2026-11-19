using Duxel.Core;
using Duxel.Gallery;

internal static class ActivityIndicatorTests
{
    internal static void Run(Action<bool, string> check)
    {
        var sizes = new[] { UiActivityIndicatorSize.ExtraSmall, UiActivityIndicatorSize.Small, UiActivityIndicatorSize.Medium,
            UiActivityIndicatorSize.Large, UiActivityIndicatorSize.ExtraLarge };
        check(sizes.Select(UiFluentActivityIndicatorPresentation.Diameter).SequenceEqual(new float[] { 12, 16, 24, 32, 36 })
            && sizes.Select(UiFluentActivityIndicatorPresentation.Thickness).SequenceEqual(new float[] { 1, 1, 2, 3, 4 }),
            "ActivityIndicator geometry matches all five official size and thickness tokens");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        UiVector2 Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 1280, 6000), UiTextSettings.Default);
            var size = default(UiVector2);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); render(ui); size = ui.GetItemRectSize(); }));
            using var draw = context.GetDrawData();
            return size;
        }
        using var indicator = new UiActivityIndicator<Probe>();
        var hiddenSize = Frame(indicator.Render);
        check(hiddenSize == new UiVector2(24, 24) && !context.State.HasActiveAnimations
            && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Image),
            "default stopped ActivityIndicator hides paint and semantics while retaining its24pixel square");
        indicator.Start();
        Frame(indicator.Render);
        var start = context.State.TimeSeconds;
        check(Probe.Angle == 0 && context.State.HasActiveAnimations,
            "starting ActivityIndicator begins at zero and schedules paced rotation");
        var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node;
        check(node.Name == "Animating" && node.Busy && !node.Focusable && !node.ValuePattern && node.Range is null,
            "ActivityIndicator follows the official named image trait with indeterminate busy semantics");
        context.State.AdvanceTime(.1875f); Frame(indicator.Render);
        var expected = (float)((context.State.TimeSeconds - start) / .75 % 1 * Math.Tau);
        check(MathF.Abs(Probe.Angle - expected) < .001f, "ActivityIndicator rotation uses the official750ms linear cycle");
        indicator.Stop();
        check(Frame(indicator.Render) == hiddenSize && !context.State.HasActiveAnimations,
            "stopping ActivityIndicator preserves layout and cancels animation requests");
        indicator.Options = indicator.Options with { HidesWhenStopped = false };
        Frame(indicator.Render);
        node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node;
        check(Probe.Angle == 0 && node.Name == "Stopped" && !node.Busy && !context.State.HasActiveAnimations,
            "visible stopped ActivityIndicator resets rotation and reports its stopped state");
        indicator.Options = indicator.Options with { IsAnimating = true, ReducedMotion = true, AccessibilityLabel = "Loading profile" };
        Frame(indicator.Render);
        check(Probe.Angle == 0 && !context.State.HasActiveAnimations
            && tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node.Name == "Loading profile",
            "native reduced-motion extension keeps a named static indicator without frame requests");
        indicator.AccessibleName = "Custom loading name";
        indicator.Options = indicator.Options with { Size = UiActivityIndicatorSize.ExtraLarge };
        check(Frame(indicator.Render) == new UiVector2(36, 36)
            && tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Image).Node.Name == "Custom loading name",
            "size and ordinary accessible-name changes update the actual widget");
        var rejected = false;
        try { indicator.Options = indicator.Options with { Size = (UiActivityIndicatorSize)7 }; }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "invalid ActivityIndicator sizes fail before replacing live options");
        check(Fluent.ComponentNames.Contains("ActivityIndicator") && !Fluent.MissingComponentNames.Contains("ActivityIndicator"),
            "ActivityIndicator has a generated public factory and leaves the pending catalog");
        using var page = new ActivityIndicatorPage();
        using var frame = new GalleryPageFrame(page);
        Frame(frame.Render); Frame(frame.Render);
        check(page.Samples.Count == 5 && page.Samples.All(sample => sample.Source.Contains("Fluent.ActivityIndicator", StringComparison.Ordinal)),
            "each ActivityIndicator size has its own adjacent Gallery source");
        var button = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "활동 시작·중지"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery start-stop option accepts actual activation");
        Frame(frame.Render); Frame(frame.Render);
        check(page.Samples.All(sample => sample.Source.Contains("IsAnimating : false", StringComparison.Ordinal)),
            "every ActivityIndicator snippet follows the actual stopped state");
    }
    internal static void RunGpu(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        var empty = capture("activity-empty", ui => ui.EnableRootViewportContentLayout(contentPadding : 0));
        using var indicator = Fluent.ActivityIndicator(new(Size : UiActivityIndicatorSize.ExtraLarge, IsAnimating : true, Color : new UiColor(240, 70, 40)));
        void Draw(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); indicator.Render(ui); }
        var first = capture("activity-start", Draw);
        check(!first.AsSpan().SequenceEqual(empty), "ActivityIndicator paints its actual native arc");
        context.State.AdvanceTime(.1875f);
        var quarter = capture("activity-quarter", Draw);
        check(!first.AsSpan().SequenceEqual(quarter), "ActivityIndicator rotates its actual Vulkan arc over time");
        indicator.Stop();
        var hidden = capture("activity-hidden", Draw);
        check(hidden.AsSpan().SequenceEqual(empty), "stopped hidden ActivityIndicator leaves no painted pixels");
        indicator.Options = indicator.Options with { HidesWhenStopped = false };
        var stopped = capture("activity-stopped", Draw);
        check(stopped.AsSpan().SequenceEqual(first), "visible stopped ActivityIndicator restores its exact initial angle");
        indicator.Options = indicator.Options with { IsAnimating = true, ReducedMotion = true };
        var reduced = capture("activity-reduced", Draw);
        context.State.AdvanceTime(.4f);
        var stable = capture("activity-reduced-stable", Draw);
        check(reduced.AsSpan().SequenceEqual(stable) && !context.State.HasActiveAnimations,
            "reduced-motion ActivityIndicator preserves exact Vulkan pixels without animation requests");
        var colored = 0;
        for (var i = 0; i < first.Length; i += 4) if (first[i] > first[i + 1] * 2 && first[i + 1] > 10) colored++;
        check(colored > 80, "ActivityIndicator honors its explicit color on the real Vulkan output");
    }
    private readonly struct Probe : IUiActivityIndicatorPresentation
    {
        internal static float Angle;
        public static bool CanRetainMeasurement => true;
        public static UiVector2 Measure(UiControlContext context, UiActivityIndicatorOptions options) =>
            UiFluentActivityIndicatorPresentation.Measure(context, options);
        public static void Paint(UiControlContext context, UiActivityIndicatorOptions options, float angle, UiRect bounds) => Angle = angle;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
