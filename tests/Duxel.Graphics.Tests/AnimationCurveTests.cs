using Duxel.Core;

internal static class AnimationCurveTests
{
    internal static void Run(Action<bool, string> check)
    {
        UiCubicBezier[] curves = [new(0, 0, 0, 1), new(.8, 0, .78, 1), new(.33, 0, .67, 1),
            new(1, 0, 1, 1), new(0, 0, 1, 1), new(.3, -1, .7, 2)];
        foreach (var curve in curves)
        {
            check(curve.Evaluate(-1) == 0 && curve.Evaluate(0) == 0 && curve.Evaluate(1) == 1 && curve.Evaluate(2) == 1,
                "Timing curve clamps endpoints : " + curve);
            foreach (var t in new[] { .01, .1, .25, .5, .8, .99 })
            {
                // Parameter-space points independently define exact (time,value) observations.
                var inverse = 1 - t;
                var x = 3 * inverse * inverse * t * curve.X1 + 3 * inverse * t * t * curve.X2 + t * t * t;
                var y = 3 * inverse * inverse * t * curve.Y1 + 3 * inverse * t * t * curve.Y2 + t * t * t;
                check(Math.Abs(curve.Evaluate(x) - y) < 1e-7, "Timing curve inverts its time coordinate : " + curve + " / " + t);
            }
        }
        check(new UiCubicBezier(.3, 2, .7, 2).Evaluate(.5) > 1,
            "Timing curves preserve intentional value overshoot instead of silently clamping it");
        var rejected = 0;
        foreach (var curve in new UiCubicBezier[] { new(-.01, 0, 1, 1), new(0, 0, 1.01, 1),
            new(double.NaN, 0, 1, 1), new(0, double.PositiveInfinity, 1, 1) })
        {
            try
            {
                curve.Validate();
            }
            catch (ArgumentOutOfRangeException) { rejected++; }
        }
        check(rejected == 4, "Timing curve rejects invalid finite/time control contracts");
        var rejectedTime = false;
        try
        {
            curves[0].Evaluate(double.NaN);
        }
        catch (ArgumentOutOfRangeException) { rejectedTime = true; }
        check(rejectedTime, "Timing curve rejects nonfinite progress");

        var transition = new UiFloatTransition(0);
        var options = new UiTransitionOptions(.25) { Curve = curves[0] };
        var start = transition.Update(1, 0, options);
        var half = transition.Update(1, .125, options);
        check(start is { Value: 0, Running: true } && start.NextFrameTimeSeconds == 1d / 60
            && Math.Abs(half.Value - curves[0].Evaluate(.5)) < 1e-6,
            "Explicit curve drives the owned transition while retaining its finite frame deadline");
        var retarget = transition.Update(0, .125, options with
        {
            Curve = curves[1]
        });
        check(retarget.Value == half.Value && retarget.Running,
            "Changing direction and timing curve starts at the displayed value without a jump");
        var settled = transition.Update(0, .375, options with
        {
            Curve = curves[1]
        });
        check(settled is { Value: 0, Running: false, NextFrameTimeSeconds: null },
            "Explicit-curve transition settles at the exact endpoint without an idle repaint deadline");
        var reduced = transition.Update(1, .4, options with
        {
            ReducedMotion = true
        });
        check(reduced is { Value: 1, Running: false, NextFrameTimeSeconds: null },
            "Reduced motion bypasses explicit timing curves without scheduling extra work");
        var cold = new UiFloatTransition(0);
        var firstStep = new UiTransitionOptions(.2, UiAnimationEasing.Linear) { KeepFirstVisibleStep = true };
        cold.Update(1, 0, firstStep);
        var firstVisible = cold.Update(1, .3, firstStep, previousFrameDelayed : true);
        check(firstVisible.Running && Math.Abs(firstVisible.Value - 1f / 12) < 1e-5f,
            "A delayed first switch frame retains an intermediate position even after the nominal end time");
        var nextVisible = cold.Update(1, .5, firstStep);
        check(nextVisible is { Value: 1, Running: false, NextFrameTimeSeconds: null },
            "The switch timeline uses normal elapsed time after the first visible step");
        var overflow = new UiFloatTransition(0);
        var huge = new UiTransitionOptions(1) { Curve = new(.3, double.MaxValue, .7, double.MaxValue) };
        overflow.Update(1, 0, huge);
        var overflowRejected = false;
        try
        {
            overflow.Update(1, .5, huge);
        }
        catch (InvalidOperationException) { overflowRejected = true; }
        check(overflowRejected, "Unrepresentable transition overshoot fails explicitly instead of publishing nonfinite geometry");

        var progressBehavior = new UiFluentProgressBehavior();
        UiFluentProgressBehavior.Evaluate(ref progressBehavior, new(0), 0, out _);
        var interpolated = UiFluentProgressBehavior.Evaluate(ref progressBehavior, new(1), 0, out var moving);
        check(moving && interpolated.Value == 0,
            "Fluent determinate progress retains its normal width transition");
        var immediate = UiFluentProgressBehavior.Evaluate(ref progressBehavior, new(1, ReducedMotion: true), .05, out moving);
        check(!moving && immediate.Value == 1,
            "Reduced-motion determinate progress cancels its owned width tween at the requested value");
        UiFluentProgressBehavior.Evaluate(ref progressBehavior, new(1), .06, out moving);
        check(!moving, "Reenabling progress motion does not restart an already settled value");

        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        using var page = new Duxel.Gallery.DialogPage();
        context.SetAccessibilityTree(tree);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(float delta = .01f)
        {
            context.NewFrame(new(delta, new(960, 900), new(1, 1)), idle, new(0, 0, 960, 900), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); page.Render(ui); }));
            using var data = context.GetDrawData();
        }
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "Observer motion preview starts settled without a periodic rendering request");
        foreach (var label in new[] { "등장 곡선", "퇴장 곡선", "배경 곡선" })
        {
            var button = tree.CaptureSnapshot().Elements.Single(x => x.Node.Role == UiAccessibilityRole.Button && x.Node.Name == label);
            check(!button.Node.Offscreen
                || tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
                "Observer motion sample is visible or can be revealed below its compact source blocks : " + label);
            for (var revealFrame = 0; revealFrame < 3; revealFrame++) Frame();
            var scrollbarMotion = new UiScrollbarMotionOptions();
            context.State.AdvanceTime((float)(scrollbarMotion.Enter.DurationSeconds + scrollbarMotion.HoldSeconds + scrollbarMotion.Exit.DurationSeconds));
            Frame();
            check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "Observer exposes a real preview action : " + label);
            Frame();
            Frame();
            check(context.State.NextScheduledFrameTimeSeconds is not null,
                "Observer preview schedules only its active finite transition : " + label);
            Frame(.3f);
            Frame();
            var settledRequests = requests;
            Frame();
            Frame();
            check(context.State.NextScheduledFrameTimeSeconds is null && requests == settledRequests,
                "Observer preview stops requesting frames after completion : " + label);
        }

    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
