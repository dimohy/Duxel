using Duxel.Core;

internal static class InputLayerMetadataTests
{
    private static readonly UiRect Clip = new(0, 0, 320, 240);
    private static readonly UiFrameInfo Frame = new(.016f, new(320, 240), new(1, 1));
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<bool, string> check)
    {
        foreach (var directReplay in new[] { false, true })
        {
            RunDeadline(check, directReplay);
            RunBoundedAnimation(check, directReplay);
        }
        RunTextOwnership(check);
    }

    private static UiContext Context()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }

    private static void RunDeadline(Action<bool, string> check, bool directReplay)
    {
        using var context = Context();
        var builds = 0;
        var parentVersion = 0;
        var deadline = 1d;
        var observe = false;
        var observedDeadlines = new List<double?>();
        var phases = new List<UiRenderPhase>();
        var screen = new Screen(ui =>
        {
            if (observe)
                context.State.ClearScheduledFrame();
            if (ui.BeginLayer("deadline-parent", new(true, 1, default)
            {
                ContentVersion = parentVersion++
            }))
            {
                var options = new UiLayerOptions(true, 1, default) { ContentVersion = 1 };
                if (!directReplay || !ui.TryReplayLayer("deadline-child", options))
                {
                    if (ui.BeginLayer("deadline-child", options))
                    {
                        builds++;
                        deadline = context.State.TimeSeconds + 1;
                        ui.RequestFrameAt(deadline);
                        new UiControlContext(ui).Frame(new(20, 20, 40, 30), new(40, 120, 220), default, 0, 0);
                    }
                    ui.EndLayer();
                }
            }
            ui.EndLayer();
            if (observe)
            {
                observedDeadlines.Add(context.State.NextScheduledFrameTimeSeconds);
                phases.Add(ui.RenderPhase);
            }
        });
        context.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        context.Render(screen);
        using (context.GetDrawData())
        {
        }
        var originalDeadline = deadline;
        observe = true;
        context.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
        context.Render(screen);
        using (var final = context.GetDrawData())
        {
            var prefix = $"Input layer metadata / directReplay={directReplay}";
            check(observedDeadlines.Count == 3 && observedDeadlines.All(value => value == originalDeadline),
                prefix + " / cached child republishes its deadline during every input and final step");
            check(builds == 1 && phases.SequenceEqual(new[] { UiRenderPhase.Input, UiRenderPhase.Input, UiRenderPhase.Presentation })
                && context.InputProcessingMetrics is { CompletedSteps: 3, BuiltDrawings: 1 },
                prefix + " / nested parent rebuilds preserve cached child scheduling without painting intermediate frames");
            check(final.DrawLists.Any(list => list.Commands.Count > 0), prefix + " / final nested replay remains visible");
        }
        observedDeadlines.Clear();
        context.NewFrame(Frame with
        {
            DeltaTime = 2
        }, Idle, Clip, UiTextSettings.Default);
        context.Render(screen);
        using (context.GetDrawData())
            check(builds == 2 && deadline > originalDeadline && context.State.NextScheduledFrameTimeSeconds == deadline,
                $"Input layer metadata / directReplay={directReplay} / due child rebuilds and schedules its next deadline");
    }

    private static void RunBoundedAnimation(Action<bool, string> check, bool directReplay)
    {
        using var context = Context();
        var mode = "offscreen";
        var parentVersion = 0;
        var active = new List<bool>();
        var scheduled = new List<bool>();
        var screen = new Screen(ui =>
        {
            using var clip = new UiControlContext(ui).Clip(mode == "clipped" ? new(0, 0, 5, 5) : Clip);
            if (ui.BeginLayer("animation-parent", new(true, mode == "transparent" ? 0 : 1,
                mode == "offscreen" ? new(1000, 0) : default)
            {
                ContentVersion = parentVersion++
            }))
            {
                var options = new UiLayerOptions(true, 1, default) { ContentVersion = 1 };
                if (!directReplay || !ui.TryReplayLayer("animation-child", options))
                {
                    if (ui.BeginLayer("animation-child", options))
                    {
                        var bounds = new UiRect(20, 20, 40, 30);
                        ui.RequestControlAnimation(bounds);
                        new UiControlContext(ui).Frame(bounds, new(30, 140, 220), default, 0, 0);
                    }
                    ui.EndLayer();
                }
            }
            ui.EndLayer();
            active.Add(context.State.HasActiveAnimations);
            scheduled.Add(context.State.NextScheduledFrameTimeSeconds.HasValue);
        });
        context.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        context.Render(screen);
        using (context.GetDrawData())
        {
        }
        foreach (var modes in new[] { new[] { "offscreen", "transparent", "visible" }, new[] { "visible", "clipped", "transparent" } })
        {
            active.Clear();
            scheduled.Clear();
            var step = 0;
            context.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
            context.Render(new Screen(ui => { mode = modes[step++]; screen.Render(ui); }));
            using var final = context.GetDrawData();
            var expected = modes.Select(value => value == "visible").ToArray();
            var prefix = $"Input bounded animation / directReplay={directReplay}, final={modes[^1]}";
            check(active.SequenceEqual(expected), prefix + " / parent translation, clip and opacity determine activity at every logical step");
            check(scheduled.SequenceEqual(expected), prefix + " / only visible animation schedules another frame");
            check(context.InputProcessingMetrics is { CompletedSteps: 3, BuiltDrawings: 1 }
                && context.State.HasActiveAnimations == expected[^1],
                prefix + " / one final drawing retains final activity rather than an intermediate animation request");
        }
    }

    private static void RunTextOwnership(Action<bool, string> check)
    {
        using var context = Context();
        var parent = false;
        var removeChild = false;
        var removeParent = false;
        var pressure = false;
        var parentVersion = 0;
        var childBuilds = 0;
        var occupancy = (Entries: 0, Retained: 0, Limit: 0);
        var screen = new Screen(ui =>
        {
            if (removeChild)
                ui.RemoveLayer("text-child");
            if (removeParent)
                ui.RemoveLayer("text-parent");
            if (!removeParent)
            {
                var draw = !parent || ui.BeginLayer("text-parent", new(true, 1, default)
                {
                    ContentVersion = parentVersion
                });
                if (draw)
                {
                    if (ui.BeginLayer("text-child", new(true, 1, default)
                    {
                        ContentVersion = 1
                    }))
                    {
                        childBuilds++;
                        ui.SetCursorScreenPos(new(20, 20));
                        ui.Text("Nested retained dependency");
                    }
                    ui.EndLayer();
                }
                if (parent)
                    ui.EndLayer();
            }
            if (pressure)
                for (var index = 0; index < ui.DirectTextCacheOccupancy.Limit + 8; index++)
                {
                    ui.SetCursorScreenPos(new(20, 100));
                    ui.Text("Unretained pressure " + index);
                }
            occupancy = ui.DirectTextCacheOccupancy;
        });
        UiDrawData Build(bool batch)
        {
            if (batch)
                context.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
            else
                context.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
            context.Render(screen);
            return context.GetDrawData();
        }
        UiLayerFrameLifetime lifetime;
        {
            using var original = Build(false);
            lifetime = original.LayerFrameLease?.Owner ?? throw new InvalidOperationException("A retained drawing must own a frame lease.");
            var originalHash = RasterLayerLifetimeTests.Fingerprint(original);
            check(childBuilds == 1 && occupancy.Retained == 1, "Input layer text / child establishes one real retained raster-text key");
            parent = true;
            parentVersion++;
            using var adopted = Build(true);
            var adoptedHash = RasterLayerLifetimeTests.Fingerprint(adopted);
            check(childBuilds == 1 && occupancy.Retained == 1 && adopted.DrawLists.Any(list => list.Commands.Count > 0),
                "Input layer text / new nested parent adopts a replayed child dependency through input-only traversal");
            removeChild = true;
            pressure = true;
            using var pressured = Build(true);
            check(childBuilds == 1 && occupancy.Retained == 1,
                "Input layer text / removing child retains the cached parent's independent text ownership under pressure");
            check(RasterLayerLifetimeTests.Fingerprint(original) == originalHash && RasterLayerLifetimeTests.Fingerprint(adopted) == adoptedHash,
                "Input layer text / held original and adopted drawings remain immutable after child removal and cache pressure");
            pressure = false;
            removeParent = true;
            using var removed = Build(true);
            check(occupancy.Retained == 0 && RasterLayerLifetimeTests.Fingerprint(original) == originalHash
                && RasterLayerLifetimeTests.Fingerprint(adopted) == adoptedHash,
                "Input layer text / last parent removal releases text retention without mutating held frame buffers");
            check(context.InputProcessingMetrics is { Completed: true, CompletedSteps: 3, BuiltDrawings: 1 },
                "Input layer text / removal batch publishes once and completes every logical step");
            check(lifetime.State.Frames > 0 && lifetime.State.RetiredSources > 0,
                "Input layer text / removed geometry remains retired behind outstanding consumer frames");
        }
        check(lifetime.State == (0, 0),
            "Input layer text / final consumer disposal drains all frame leases and retired geometry");
    }

    internal static void RunGpu(Action<bool, string> check)
    {
        foreach (var slots in new[] { 1, 3 })
        {
            using var reference = new PresentationReuseTests.Fixture(slots, false);
            using var candidate = new PresentationReuseTests.Fixture(slots, true);
            using var redA = Fluent.Image(new(new(1, 1, [220, 40, 30, 255])));
            using var blueA = Fluent.Image(new(new(1, 1, [30, 70, 220, 255])));
            using var redB = Fluent.Image(new(new(1, 1, [220, 40, 30, 255])));
            using var blueB = Fluent.Image(new(new(1, 1, [30, 70, 220, 255])));
            foreach (var image in new[] { redA, blueA, redB, blueB })
                image.Size = new(64, 48);
            static void DrawImage(UiImmediateContext ui, UiImage image)
            {
                ui.SetCursorScreenPos(new(25, 30));
                image.Render(ui);
            }
            byte[] expectedOld;
            byte[] expectedNew;
            using (var old = reference.Build(ui => DrawImage(ui, redA), Idle))
                expectedOld = reference.Render(old);
            using (var next = reference.Build(ui => DrawImage(ui, blueA), Idle))
                expectedNew = reference.Render(next);
            UiLayerFrameLifetime lifetime;
            {
                using var original = candidate.Build(ui =>
                {
                    if (ui.BeginLayer("queued-layer", new(true, 1, default)
                    {
                        ContentVersion = 1
                    }))
                        DrawImage(ui, redB);
                    ui.EndLayer();
                }, Idle);
                var fingerprint = RasterLayerLifetimeTests.Fingerprint(original);
                lifetime = original.LayerFrameLease?.Owner ?? throw new InvalidOperationException("A queued retained frame must own a lease.");
                var step = 0;
                candidate.Context.NewInputBatch(candidate.FrameInfo, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
                candidate.Context.Render(new Screen(ui =>
                {
                    if (++step == 1)
                        ui.RemoveLayer("queued-layer");
                    if (ui.BeginLayer("queued-layer", new(true, 1, default)
                    {
                        ContentVersion = 2
                    }))
                        DrawImage(ui, blueB);
                    ui.EndLayer();
                }));
                using var final = candidate.Context.GetDrawData();
                var prefix = $"Input queued layer GPU / slots={slots}";
                check(step == 3 && candidate.Context.InputProcessingMetrics is { CompletedSteps: 3, BuiltDrawings: 1 },
                    prefix + " / removal and replacement traverse every event and publish once");
                check(RasterLayerLifetimeTests.Fingerprint(original) == fingerprint && lifetime.State.RetiredSources > 0,
                    prefix + " / replacement defers old geometry retirement while the unsubmitted frame stays immutable");
                var actualOld = candidate.Render(original);
                check(actualOld.AsSpan().SequenceEqual(expectedOld),
                    prefix + " / delayed original submission preserves exact original GPU pixels");
                var actualNew = candidate.Render(final);
                check(actualNew.AsSpan().SequenceEqual(expectedNew) && !actualNew.AsSpan().SequenceEqual(expectedOld),
                    prefix + " / replacement GPU pixels match independent direct repaint and visibly change");
                check(final.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Create),
                    prefix + " / texture created during input-only traversal survives to final submission");
            }
            check(lifetime.State == (0, 0),
                $"Input queued layer GPU / slots={slots} / both consumer disposals drain all frame and retired geometry owners");
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
