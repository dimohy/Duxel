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
        check(page.Samples.Count == 6 && page.Samples.Take(2).All(sample => sample.Source.Contains("Fluent.ProgressIndicator", StringComparison.Ordinal))
            && page.Samples[2].Source.Contains("Fluent.CircularProgressIndicator", StringComparison.Ordinal)
            && page.Samples[3].Source.Contains("FinishIndeterminateCycle = true", StringComparison.Ordinal)
            && page.Samples[4].Source.Contains("new UiFluentAndroidLinearProgressIndicator", StringComparison.Ordinal)
            && page.Samples[5].Source.Contains("new UiFluentIosProgressBar", StringComparison.Ordinal),
            "Web and mobile progress Gallery examples own adjacent public source");
        var button = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "파일 업로드 진행률 증가"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery progress option accepts actual activation");
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples[0].Source.Contains("PercentComplete : 0.5f", StringComparison.Ordinal),
            "ProgressIndicator inline source follows its actual updated completion");
        var reverse = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "진행 방향 뒤집기"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(reverse.Node.Id, reverse.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Android linear Gallery direction control accepts activation");
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples[4].Source.Contains("RightToLeft = true", StringComparison.Ordinal),
            "Android linear Gallery source follows the live direction setting");
        var start = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "iOS 진행 애니메이션 시작"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(start.Node.Id, start.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "the iOS Gallery start button accepts activation");
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples[5].Source.Contains("IsAnimating : true", StringComparison.Ordinal),
            "the iOS Gallery source follows the live animation setting");

        using var android = new UiFluentAndroidLinearProgressIndicator(new(Label : "Android upload", PercentComplete : .25f));
        Frame(android.Render);
        var androidId = Progress().Id;
        android.Options = android.Options with { PercentComplete = .75f, RightToLeft = true };
        Frame(android.Render);
        check(Progress().Id == androidId && Progress().Range?.Value == 75 && context.State.HasActiveAnimations,
            "Android linear progress preserves its node and publishes the requested value during the transition");
        android.Options = android.Options with { PercentComplete = null, ReducedMotion = true };
        Frame(android.Render);
        check(Progress().Id == androidId && Progress().Busy && Progress().Range is null && !context.State.HasActiveAnimations,
            "Android linear reduced waiting preserves its node and busy state without continuous frames");
        CheckAndroidLinearMotion(check);
        CheckIosProgress(context, tree, Frame, check);

        using var custom = new CustomIndicator(new(Label : "Custom upload", Description : "Upload in progress",
            PercentComplete : .8f, Width : 200));
        var primitives = new List<UiRectFilledPrimitive>();
        context.NewFrame(new(.016f, new(1280, 5000), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 1280, 5000), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); custom.Render(ui); }));
        using (var draw = context.GetDrawData())
            foreach (var list in draw.DrawLists)
                if (list.RectFilledPrimitives is { } rectangles)
                    primitives.AddRange(rectangles.AsSpan().ToArray());
        var painted = primitives.SingleOrDefault(primitive => primitive.Color == CustomPresentation.Fill);
        check(painted.Rect.Width == 80 && painted.Rect.Height == 6 && painted.CornerRadius == 3,
            "a derived indicator uses the selected presentation geometry and the selected behavior's displayed fraction");
        node = Progress();
        check(node.Name == "Custom upload" && node.GetHelpText() == "Upload in progress"
            && node.Bounds.Width == 200 && node.Bounds.Height == 6 && node.Range?.Value == 80 && !node.Focusable,
            "customized progress still publishes the requested value, actual track bounds and shared label and help text");
        check(!context.State.HasActiveAnimations, "a custom indicator with no interpolation avoids scheduling continuous frames");

        using var circular = Fluent.CircularProgressIndicator(.25f);
        circular.AccessibleName = "Sync progress";
        Frame(circular.Render);
        node = Progress();
        check(node.Name == "Sync progress" && node.Range?.Value == .25 && node.Range?.Maximum == 1
            && node.ReadOnly && !node.Focusable && !node.Busy && node.Bounds.Width == 12 && node.Bounds.Height == 12,
            "circular progress uses the 12-pixel default and publishes the requested fraction");
        circular.Size = new(40, 56);
        circular.Value = .75f;
        Frame(circular.Render);
        node = Progress();
        check(node.Bounds.Width == 40 && node.Bounds.Height == 40 && node.Range?.Value == .75 && context.State.HasActiveAnimations,
            "a circular track measures square and reports the target before the visual transition ends");
        context.State.AdvanceTime(.75f);
        Frame(circular.Render);
        check(!context.State.HasActiveAnimations, "circular progress stops scheduling after the 750ms transition");
        circular.Value = null;
        Frame(circular.Render);
        check(Progress().Busy && Progress().Range is null && context.State.HasActiveAnimations,
            "an indeterminate ring publishes busy state without a range and requests animation");
        circular.ReducedMotion = true;
        Frame(circular.Render);
        check(Progress().Busy && Progress().Range is null && !context.State.HasActiveAnimations,
            "reduced motion keeps an indeterminate ring busy without scheduling rotation");
        circular.Value = 0;
        Frame(circular.Render);
        check(!Progress().Busy && Progress().Range?.Value == 0 && !context.State.HasActiveAnimations,
            "a zero-valued ring is determinate and remains at rest");

        VerifyWebTransition(check);
        using var failed = Fluent.ProgressIndicator(new(Label : "Document", Description : "Check your connection and retry.",
            PercentComplete : .25f, ValueText : "Upload failed") { Color = UiProgressColor.Error });
        Frame(failed.Render);
        node = Progress();
        check(node.Range?.Value == 25 && node.ItemStatus == "Upload failed"
            && node.GetHelpText() == "Check your connection and retry." && !node.Busy,
            "a failed indicator preserves the last known completion and exposes both the error and recovery text");
    }

    private static void CheckAndroidLinearMotion(Action<bool, string> check)
    {
        var state = new UiFluentAndroidLinearProgressBehavior();
        var target = new UiProgressValue(0);
        UiProgressValue Sample(double time, out bool running) =>
            UiFluentAndroidLinearProgressBehavior.Evaluate(ref state, target, time, out running);
        Sample(0, out _);
        target = target with { Value = 1 };
        Sample(1, out _);
        check(MathF.Abs(Sample(1.2, out var running).Value!.Value - .5f) < 1e-5f && running,
            "Android determinate progress samples LinearOutSlowIn at one fifth of its one-second transition");
        check(Sample(1.75, out running).Value < 1 && running && Sample(2, out running).Value == 1 && !running,
            "Android determinate progress settles at exactly one second");
        target = target with { Value = null };
        check(Sample(5, out _).IndeterminateElapsedSeconds == 0 && Sample(5.5, out _).IndeterminateElapsedSeconds == .5,
            "Android waiting cycle measures time since entering the waiting state");
        UiFluentAndroidLinearProgressBehavior.OnHidden(ref state);
        check(Sample(9, out _).IndeterminateElapsedSeconds == 0, "showing the Android track starts a new waiting cycle");
        var first = UiFluentAndroidLinearProgressPresentation.WaitingEdges(.30625);
        var second = UiFluentAndroidLinearProgressPresentation.WaitingEdges(.74325);
        var last = UiFluentAndroidLinearProgressPresentation.WaitingEdges(1.32);
        check(first.Tail == 0 && MathF.Abs(first.Head - .5f) < 1e-5f && MathF.Abs(second.Tail - .5f) < 1e-5f
            && second.Head > .9f && last == (1, 1) && UiFluentAndroidLinearProgressPresentation.WaitingEdges(1.375) == default,
            "Android waiting gradient moves the leading edge first, delays the trailing edge by 437ms and repeats after 1375ms");
    }

    private static void CheckIosProgress(UiContext context, UiAccessibilityTree tree, Action<Action<UiImmediateContext>> frame,
        Action<bool, string> check)
    {
        using var ios = new UiFluentIosProgressBar(new(Width : 240, AccessibilityLabel : "iOS download"));
        using var following = Fluent.ProgressIndicator(new(Label : "Next task", PercentComplete : .5f));
        UiAccessibilityNode? Node(string name) => tree.CaptureSnapshot().Elements
            .Select(element => element.Node)
            .Where(node => node.Role == UiAccessibilityRole.ProgressBar && node.Name == name)
            .Select(node => (UiAccessibilityNode?)node).SingleOrDefault();
        void Draw(UiImmediateContext ui) { ios.Render(ui); following.Render(ui); }
        frame(Draw);
        var hiddenNextBounds = Node("Next task")!.Value.Bounds;
        check(Node("iOS download") is null && !context.State.HasActiveAnimations,
            "the default stopped iOS progress bar is absent from the accessibility tree and does not animate");
        ios.Start();
        frame(Draw);
        check(Node("iOS download") is { Busy: true, Range: null, ReadOnly: true }
            && Node("Next task")!.Value.Bounds == hiddenNextBounds && context.State.HasActiveAnimations,
            "starting iOS progress publishes a busy node and uses the same reserved layout");
        ios.Options = ios.Options with { HidesWhenStopped = false };
        ios.Stop();
        frame(Draw);
        check(Node("iOS download") is { Busy: false, Range: null } && !context.State.HasActiveAnimations,
            "a visible stopped iOS progress bar has neither busy state nor numeric range");
        ios.Options = ios.Options with { Progress = .625f, HidesWhenStopped = true };
        frame(Draw);
        check(Node("iOS download") is { Busy: false, Value: "63%" } determinate && determinate.Range?.Value == 62.5
            && Node("Next task")!.Value.Bounds == hiddenNextBounds,
            "determinate iOS progress stays visible when stopped and exposes the range and rounded percentage");
        ios.Options = ios.Options with { Progress = null, ReducedMotion = true };
        ios.Start();
        frame(Draw);
        check(Node("iOS download") is { Busy: true, Range: null } && !context.State.HasActiveAnimations,
            "reduced motion leaves indeterminate iOS progress busy without scheduling frames");

        var motion = new UiFluentIosProgressMotion();
        motion.SampleValue(0, 0, false);
        motion.SampleValue(1, 0, false);
        var middle = motion.SampleValue(1, .1, false);
        var end = motion.SampleValue(1, .2, false);
        check(middle.Running && middle.Value == .5f && !end.Running && end.Value == 1,
            "the iOS determinate transition is linear and settles after 200ms");
        check(motion.WaitingStart(10, false) == -1 && motion.WaitingStart(10.875, false) == 0
            && motion.WaitingStart(11.75, false) == -1, "the iOS gradient crosses two track widths every 1750ms");
        motion.StopWaiting();
        check(motion.WaitingStart(20, false) == -1, "restarting the iOS waiting state resets the gradient");
    }

    private static void VerifyWebTransition(Action<bool, string> check)
    {
        var waiting = new UiProgressValue(null) { FinishIndeterminateCycle = true };
        var target = waiting with { Value = .75f };
        UiFluentProgressBehavior state = default;
        UiFluentProgressBehavior.Evaluate(ref state, waiting, 2.4, out _);
        check(UiFluentProgressBehavior.Evaluate(ref state, target, 2.6, out _).Value is null
            && UiFluentProgressBehavior.Evaluate(ref state, target with { Value = 1 }, 3, out _).Value == 1,
            "a requested completion keeps the current cycle and then displays the latest target");

        UiFluentProgressBehavior.Evaluate(ref state, waiting, 5.9, out _);
        check(UiFluentProgressBehavior.Evaluate(ref state, target, 6.01, out _).Value == .75f,
            "a request first evaluated after the cycle boundary does not wait through another cycle");

        UiFluentProgressBehavior.Evaluate(ref state, waiting, 7.1, out _);
        check(UiFluentProgressBehavior.Evaluate(ref state, target with { Color = UiProgressColor.Error }, 7.2, out _).Value == .75f,
            "an error can replace the waiting track immediately");

        UiFluentProgressBehavior.Evaluate(ref state, waiting, 7.3, out _);
        check(UiFluentProgressBehavior.Evaluate(ref state, target with { ReducedMotion = true }, 7.4, out _).Value == .75f,
            "reduced motion bypasses the moving-cycle hold");

        UiFluentProgressBehavior.Evaluate(ref state, waiting, 7.5, out _);
        check(UiFluentProgressBehavior.Evaluate(ref state, target with { FinishIndeterminateCycle = false }, 7.6, out _).Value == .75f,
            "the existing immediate wait-to-value transition is the default");

        UiFluentProgressBehavior.Evaluate(ref state, waiting, 7.7, out _);
        UiFluentProgressBehavior.OnHidden(ref state);
        check(UiFluentProgressBehavior.Evaluate(ref state, target, 7.8, out _).Value == .75f,
            "hiding a waiting track clears the pending cycle");
    }

    private sealed class CustomIndicator(UiProgressIndicatorOptions options)
        : UiProgressIndicatorWidget<HalfFillBehavior, CustomPresentation>(options);

    private struct HalfFillBehavior : IUiProgressBehavior<HalfFillBehavior>
    {
        public static UiProgressValue Evaluate(ref HalfFillBehavior state, UiProgressValue target, double timeSeconds, out bool animating)
        {
            animating = false;
            return target with { Value = target.Value * .5f };
        }

        public static void Release(ref HalfFillBehavior state) => state = default;
    }

    private readonly struct CustomPresentation : IUiProgressPresentation
    {
        internal static readonly UiColor Fill = new(225, 41, 118);
        public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 requestedSize) => new(requestedSize.X, 6);
        public static UiVector2 Arrange(UiControlContext context, UiVector2 naturalSize, UiVector2 allocatedSize) => allocatedSize;
        public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds) =>
            context.Frame(bounds with { Width = bounds.Width * value.Fraction.GetValueOrDefault() }, Fill, default, 3, 0);
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

        using var android = new UiFluentAndroidLinearProgressIndicator(new(PercentComplete : .25f, BarHeight : 4, Width : 240));
        void DrawAndroid(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); android.Render(ui); }
        var left = capture("android-linear-left", DrawAndroid);
        android.Options = android.Options with { RightToLeft = true };
        var right = capture("android-linear-right", DrawAndroid);
        check(left.AsSpan(20 * 4, 4).SequenceEqual(right.AsSpan(220 * 4, 4))
            && left.AsSpan(220 * 4, 4).SequenceEqual(right.AsSpan(20 * 4, 4))
            && !left.AsSpan(20 * 4, 4).SequenceEqual(left.AsSpan(220 * 4, 4)),
            "Android linear Vulkan fill switches sides when the direction changes");
        android.Options = android.Options with { PercentComplete = null };
        capture("android-linear-wait-start", DrawAndroid);
        context.State.AdvanceTime(.30625f);
        var waiting = capture("android-linear-wait-later", DrawAndroid);
        context.State.AdvanceTime(.4f);
        var advanced = capture("android-linear-wait-advanced", DrawAndroid);
        check(!waiting.AsSpan().SequenceEqual(advanced), "Android linear waiting gradient advances on Vulkan");
        android.Options = android.Options with { ReducedMotion = true };
        var reduced = capture("android-linear-reduced", DrawAndroid);
        context.State.AdvanceTime(.4f);
        check(reduced.AsSpan().SequenceEqual(capture("android-linear-reduced-stable", DrawAndroid)),
            "Android linear reduced waiting keeps the same pixels on Vulkan");

        using var ios = new UiFluentIosProgressBar(new(Width : 240));
        void DrawIos(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 0); ios.Render(ui); }
        var iosHidden = capture("ios-linear-hidden", DrawIos);
        ios.Options = ios.Options with { HidesWhenStopped = false };
        var iosStopped = capture("ios-linear-stopped", DrawIos);
        ios.Start();
        var iosStart = capture("ios-linear-start", DrawIos);
        context.State.AdvanceTime(.6f);
        var iosMoving = capture("ios-linear-moving", DrawIos);
        ios.Stop();
        var iosReset = capture("ios-linear-reset", DrawIos);
        check(!iosHidden.AsSpan().SequenceEqual(iosStopped) && iosStopped.AsSpan().SequenceEqual(iosStart)
            && !iosStopped.AsSpan().SequenceEqual(iosMoving) && iosStopped.AsSpan().SequenceEqual(iosReset),
            "Vulkan iOS stopped and reset tracks match while the moving gradient paints a different frame");
        ios.Options = ios.Options with { Progress = .25f };
        var iosLeft = capture("ios-linear-left", DrawIos);
        ios.Options = ios.Options with { RightToLeft = true };
        var iosRight = capture("ios-linear-right", DrawIos);
        check(iosLeft.AsSpan(20 * 4, 4).SequenceEqual(iosRight.AsSpan(220 * 4, 4))
            && !iosLeft.AsSpan(20 * 4, 4).SequenceEqual(iosRight.AsSpan(20 * 4, 4)),
            "Vulkan iOS determinate fill follows the requested layout direction");
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
