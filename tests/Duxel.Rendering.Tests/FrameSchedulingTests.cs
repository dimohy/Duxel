using Duxel.Core;

internal static class FrameSchedulingTests
{
    internal static void Run(Action<bool, string> check)
    {
        var state = new UiState();
        state.BeginFrame();
        check(state.GetScheduledFrameWaitMilliseconds(0) == Timeout.Infinite, "no scheduled work waits for external events");
        state.ScheduleFrameAt(3);
        state.ScheduleFrameAt(2);
        state.ScheduleFrameAt(4);
        check(state.NextScheduledFrameTimeSeconds == 2 && state.GetScheduledFrameWaitMilliseconds(1) == 1000, "frame deadlines coalesce to the earliest caller request");
        check(state.GetScheduledFrameWaitMilliseconds(1.9999) == 1 && state.GetScheduledFrameWaitMilliseconds(2) == 0 && state.GetScheduledFrameWaitMilliseconds(3) == 0, "deadline timeouts round up and expired work renders immediately");
        state.BeginFrame();
        state.MarkControlAnimation();
        check(state.HasActiveAnimations && state.NextScheduledFrameTimeSeconds == state.TimeSeconds + 1d / 60
            && state.GetScheduledFrameWaitMilliseconds(state.TimeSeconds) == 17,
            "control animation waits for its next sample instead of forcing an immediate render loop");
        state.BeginFrame();
        state.MarkControlAnimation(false, bounded: true);
        check(!state.HasActiveAnimations && state.NextScheduledFrameTimeSeconds is null,
            "invisible animation retains no global sample deadline");
        state.BeginFrame();
        state.ScheduleFrameAt(.001);
        state.MarkControlAnimation();
        check(state.NextScheduledFrameTimeSeconds == .001,
            "animation cadence preserves an earlier explicit repeat or input deadline");
        state.BeginFrame();
        state.ScheduleFrameAt(double.MaxValue);
        check(state.GetScheduledFrameWaitMilliseconds(0) == int.MaxValue, "distant deadlines use the largest supported interruptible timeout");
        state.BeginFrame();
        check(state.NextScheduledFrameTimeSeconds is null, "new frames cancel declarations that are no longer present");
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1d })
        {
            var rejected = false;
            try
            {
                state.ScheduleFrameAt(invalid);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "invalid scheduling clocks are rejected explicitly");
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; i++)
        {
            state.BeginFrame();
            state.ScheduleFrameAt(2);
            _ = state.GetScheduledFrameWaitMilliseconds(1);
        }
        check(GC.GetAllocatedBytesForCurrentThread() == before, "frame scheduling uses no per-request heap allocation");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        void Step(Action<UiImmediateContext> draw, float delta = .25f)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(draw));
            using var data = context.GetDrawData();
        }
        using var spinner = Fluent.Spinner(options: new(DelaySeconds: 1));
        Step(spinner.Render);
        var deadline = context.State.NextScheduledFrameTimeSeconds;
        check(deadline == 1.25 && !context.State.HasActiveAnimations && requests == 0, "delayed Spinner declares a deadline without continuous animation or immediate wake requests");
        Step(_ => { });
        check(context.State.NextScheduledFrameTimeSeconds is null, "removing a pending Spinner cancels its wake without a timer callback");
        using var cached = Fluent.Spinner(options: new(DelaySeconds: 1));
        var builds = 0;
        var staticBuilds = 0;
        var shown = true;
        var options = new UiLayerOptions(true, 1, default) { ContentVersion = 1 };
        void Layers(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout();
            if (!ui.TryReplayLayer("scheduled", options))
            {
                if (ui.BeginLayer("scheduled", options))
                {
                    builds++;
                    ui.SetCursorScreenPos(new(10, 10));
                    ui.GetWindowDrawList().AddRectFilled(new(0, 0, 120, 20), new(200, 0, 0), ui.WhiteTextureId);
                    if (shown)
                        cached.Render(ui);
                }
                ui.EndLayer();
            }
            if (ui.BeginLayer("unrelated-static", options))
            {
                staticBuilds++;
                ui.GetWindowDrawList().AddRectFilled(new(400, 200, 80, 20), new(0, 200, 0), ui.WhiteTextureId);
            }
            ui.EndLayer();
        }
        Step(Layers);
        var retainedDeadline = context.State.NextScheduledFrameTimeSeconds;
        Step(Layers);
        check(builds == 1 && context.State.NextScheduledFrameTimeSeconds == retainedDeadline, "retained layer replay preserves its pending deadline without rebuilding geometry");
        Step(Layers, .75f);
        check(builds == 2 && context.State.HasActiveAnimations, "a due cached-layer deadline rebuilds the child and starts real animation");
        Step(Layers);
        check(builds == 3 && staticBuilds == 1, "continuous animation redraws its retained layer while unrelated static content stays cached");
        shown = false;
        Step(Layers);
        var stopped = builds;
        Step(Layers);
        check(builds == stopped && !context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null, "removing the animated child returns its layer to reusable static content");
        using var moving = Fluent.Spinner();
        var movingBuilds = 0;
        var movingOptions = new UiLayerOptions(true, 1, new(1000, 0)) { ContentVersion = 1 };
        void Moving(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout();
            if (ui.TryReplayLayer("moving-animation", movingOptions))
                return;
            if (ui.BeginLayer("moving-animation", movingOptions))
            {
                movingBuilds++;
                ui.SetCursorScreenPos(new(10, 10));
                moving.Render(ui);
            }
            ui.EndLayer();
        }
        var requestsBefore = requests;
        Step(Moving);
        check(!context.State.HasActiveAnimations && requests == requestsBefore, "an offscreen translated retained Spinner does not wake the app continuously");
        Step(Moving);
        check(movingBuilds == 1, "unrelated frames replay the hidden animated layer without rebuilding its widgets or geometry");
        movingOptions = movingOptions with
        {
            ContentVersion = 2
        };
        Step(Moving);
        Step(Moving);
        check(movingBuilds == 2, "a new hidden content revision rebuilds once and then returns to cached replay");
        Step(ui => { ui.MarkLayerDirty("moving-animation"); Moving(ui); });
        check(movingBuilds == 3, "explicit invalidation still rebuilds a hidden animated layer");
        movingOptions = movingOptions with
        {
            Translation = default
        };
        Step(Moving);
        check(context.State.HasActiveAnimations && requests == requestsBefore && context.State.NextScheduledFrameTimeSeconds > context.State.TimeSeconds, "a retained Spinner resumes animation when its layer returns onscreen");
        movingOptions = movingOptions with
        {
            Opacity = 0
        };
        requestsBefore = requests;
        Step(Moving);
        check(!context.State.HasActiveAnimations && requests == requestsBefore, "a transparent retained Spinner keeps its content without continuous wake requests");
        var transparentBuilds = movingBuilds;
        Step(Moving);
        check(movingBuilds == transparentBuilds, "transparent animated content stays cached during unrelated frames");
        movingOptions = movingOptions with
        {
            Opacity = 1
        };
        Step(Moving);
        check(context.State.HasActiveAnimations, "restoring layer opacity resumes visible animation");
        using var nested = Fluent.Spinner();
        var outerOptions = new UiLayerOptions(true, 1, new(1000, 0)) { ContentVersion = 1 };
        var innerOptions = new UiLayerOptions(true, 1, new(-1000, 0)) { ContentVersion = 1 };
        var tightClip = false;
        void Nested(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout();
            if (ui.BeginLayer("animation-outer", outerOptions))
            {
                using var clip = new UiControlContext(ui).Clip(tightClip ? new(0, 0, 1, 1) : new(-2000, -2000, 4000, 4000));
                if (ui.BeginLayer("animation-inner", innerOptions))
                {
                    ui.SetCursorScreenPos(new(10, 10));
                    nested.Render(ui);
                }
                ui.EndLayer();
            }
            ui.EndLayer();
        }
        Step(Nested);
        check(context.State.HasActiveAnimations, "opposite nested layer translations preserve visible animation");
        innerOptions = innerOptions with
        {
            Translation = default
        };
        requestsBefore = requests;
        Step(Nested);
        check(!context.State.HasActiveAnimations && requests == requestsBefore, "a translated ancestor suppresses its offscreen child animation wake");
        outerOptions = outerOptions with
        {
            Translation = default
        };
        Step(Nested);
        check(context.State.HasActiveAnimations, "nested cached animation resumes after its ancestor returns");
        tightClip = true;
        requestsBefore = requests;
        Step(Nested);
        check(!context.State.HasActiveAnimations && requests == requestsBefore, "an intermediate parent clip suppresses a hidden nested animation");
        tightClip = false;
        outerOptions = outerOptions with
        {
            Opacity = 0
        };
        Step(Nested);
        check(!context.State.HasActiveAnimations, "a transparent ancestor suppresses nested animation wakes");
        outerOptions = outerOptions with
        {
            Opacity = 1
        };
        Step(ui => { Nested(ui); movingOptions = movingOptions with { Opacity = 0 }; Moving(ui); });
        check(context.State.HasActiveAnimations, "a hidden control does not cancel another visible control's animation");
        Step(ui => { Moving(ui); new UiControlContext(ui).RequestAnimation(); });
        check(context.State.HasActiveAnimations, "unbounded animation requests still advance nonvisual logical work");
        outerOptions = outerOptions with
        {
            Scene3D = new(System.Numerics.Matrix4x4.Identity, false),
            Translation = new(1000, 0)
        };
        Step(Nested);
        check(context.State.HasActiveAnimations, "projected scenes retain animation until actual projected visibility is available");
        outerOptions = outerOptions with
        {
            Opacity = 0
        };
        Step(Nested);
        check(!context.State.HasActiveAnimations, "zero ancestor opacity suppresses projected animation without guessing its projected bounds");
        outerOptions = outerOptions with
        {
            Scene3D = null,
            Opacity = 1,
            Translation = new(1000, 0),
            ContentVersion = 2
        };
        innerOptions = innerOptions with
        {
            Translation = default
        };
        Step(Nested);
        Step(Nested);
        outerOptions = outerOptions with
        {
            Translation = default
        };
        Step(Nested);
        check(context.State.HasActiveAnimations, "replaying a hidden child propagates its bounds to a recaptured ancestor for later visible restoration");
        var logicalBuilds = 0;
        void Logical(UiImmediateContext ui)
        {
            if (ui.BeginLayer("logical-animation", new(true, 0, new(1000, 0))
            {
                ContentVersion = 1
            }))
            {
                logicalBuilds++;
                new UiControlContext(ui).RequestAnimation();
            }
            ui.EndLayer();
        }
        Step(Logical);
        Step(Logical);
        check(logicalBuilds == 2 && context.State.HasActiveAnimations, "unbounded logical animation inside a hidden cached layer keeps rebuilding and advancing");
        context.State.ScheduleFrameAt(100);
        context.Dispose();
        check(context.State.NextScheduledFrameTimeSeconds is null, "disposing a context clears its pending deadline");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
