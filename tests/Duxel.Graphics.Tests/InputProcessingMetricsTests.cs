using Duxel.Core;

internal static class InputProcessingMetricsTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var frame = new UiFrameInfo(.016f, new(400, 300), new(1, 1));
        var calls = 0;
        var requested = 0;
        context.SetRequestFrameCallback(() => requested++);
        void Begin(int count) => context.NewInputBatch(frame, Enumerable.Repeat(idle, count).ToArray(), new(0, 0, 400, 300), UiTextSettings.Default);
        foreach (var enabled in new[] { false, true, false })
        {
            context.InputProfilingEnabled = enabled;
            Begin(4);
            context.Render(new Screen(ui => { calls++; ui.Text("CPU stages"); }));
            using var data = context.GetDrawData();
            var m = context.InputProcessingMetrics;
            check(m.RequestedSteps == 4 && m.CompletedSteps == 4 && m.BuiltDrawings == 1 && m.Completed, "Input metrics / exact completed logical steps and one final drawing");
            check(m.ProfilingEnabled == enabled && (enabled ? m.ElapsedMilliseconds > 0 && m.AllocatedBytesOnRenderThread >= 0
                : m.ElapsedMilliseconds == 0 && m.AllocatedBytesOnRenderThread == 0), "Input metrics / timing and allocation are opt-in and reset when disabled");
            check(m.PreparationMilliseconds >= 0 && m.CompositionMilliseconds >= 0 && m.FinalizationMilliseconds >= 0
                && m.ElapsedMilliseconds + .000001 >= m.PreparationMilliseconds + m.CompositionMilliseconds + m.FinalizationMilliseconds,
                "Input metrics / nonoverlapping completed stage intervals fit inside Render");
        }
        check(calls == 12, "Input metrics / profiling preserves every callback");
        context.InputProfilingEnabled = true;
        Begin(3);
        var attempt = 0;
        try
        {
            context.Render(new Screen(_ => { if (++attempt == 2) throw new InvalidOperationException("Owned metrics failure"); }));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Owned metrics failure") { }
        var partial = context.InputProcessingMetrics;
        check(!partial.Completed && partial.RequestedSteps == 3 && partial.CompletedSteps == 1 && partial.BuiltDrawings == 0,
            "Input metrics / failed callbacks expose partial work without reporting completion");
        context.InputProfilingEnabled = false;
        context.NewFrame(frame, idle, new(0, 0, 400, 300), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            check(ui.GetInputProcessingMetrics() == partial, "Input metrics / immediate API reads the last finished call");
            ui.SetInputProfilingEnabled(true);
        }));
        using (context.GetDrawData())
        {
        }
        check(context.InputProfilingEnabled && !context.InputProcessingMetrics.ProfilingEnabled, "Input metrics / enabling inside a callback takes effect next call");
        context.NewFrame(frame, idle, new(0, 0, 400, 300), UiTextSettings.Default);
        context.Render(new Screen(_ => { }));
        using (context.GetDrawData())
        {
        }
        check(context.InputProcessingMetrics is { ProfilingEnabled: true, RequestedSteps: 1, CompletedSteps: 1, BuiltDrawings: 1, Completed: true },
            "Input metrics / ordinary rendering uses the same contract");
        check(requested == 0, "Input metrics / collecting and reading measurements requests no frame");
        using var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        check(!other.InputProfilingEnabled && other.InputProcessingMetrics == default, "Input metrics / contexts do not share mutable profiling state");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
