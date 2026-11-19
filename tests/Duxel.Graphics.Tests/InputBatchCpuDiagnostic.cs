using System.Runtime.InteropServices;
using Duxel.Core;

internal sealed record InputBatchCpuRow(int StaticButtons, int InputSteps, UiInputProcessingMetrics[] Samples);
internal sealed record InputBatchCpuResult(string Framework, string Scope, int WarmupFrames, int MeasuredFrames, InputBatchCpuRow[] Rows, string[] Failures);

internal static class InputBatchCpuDiagnostic
{
    internal static InputBatchCpuResult Run()
    {
        const int warmup = 8, measured = 24;
        var rows = new List<InputBatchCpuRow>();
        var failures = new List<string>();
        foreach (var buttonCount in new[] { 0, 64 })
            foreach (var steps in new[] { 1, 8, 32 })
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.Configure(context);
                context.InputProfilingEnabled = true;
                var value = "a";
                using var input = Fluent.Input("CPU input", new UiBinding<string>(() => value, text => value = text), 1024, 240);
                var buttons = Enumerable.Range(0, buttonCount).Select(index => Fluent.Button("Static " + index, () => { })).ToArray();
                var callbacks = 0;
                var screen = new Screen(ui =>
                {
                    callbacks++;
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    ui.SetCursorScreenPos(default);
                    input.Render(ui);
                    for (var index = 0; index < buttons.Length; index++)
                    {
                        ui.SetCursorScreenPos(new(index % 8 * 100, 64 + index / 8 * 50));
                        buttons[index].Render(ui);
                    }
                });
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                var frame = new UiFrameInfo(.016f, new(900, 600), new(1, 1));
                var clip = new UiRect(0, 0, 900, 600);
                context.NewFrame(frame, idle with
                {
                    MousePosition = new(10, 10),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                }, clip, UiTextSettings.Default);
                context.Render(screen);
                using (context.GetDrawData())
                {
                }
                var id = context.State.ActiveId ?? throw new InvalidOperationException("CPU diagnostic input was not focused.");
                var states = Enumerable.Repeat(idle with
                {
                    CharEvents = [new('X')]
                }, steps).ToArray();
                var samples = new List<UiInputProcessingMetrics>();
                for (var index = 0; index < warmup + measured; index++)
                {
                    value = "a";
                    context.State.SetCursor(id, 1);
                    context.State.SetSelection(id, new(1, 1));
                    callbacks = 0;
                    context.NewInputBatch(frame, states, clip, UiTextSettings.Default);
                    context.Render(screen);
                    using var data = context.GetDrawData();
                    var m = context.InputProcessingMetrics;
                    if (value != "a" + new string('X', steps) || callbacks != steps || !m.Completed || m.CompletedSteps != steps)
                        failures.Add($"buttons={buttonCount}, steps={steps}, iteration={index}: text/callback/step mismatch");
                    if (index >= warmup)
                        samples.Add(m);
                }
                rows.Add(new(buttonCount, steps, samples.ToArray()));
                foreach (var button in buttons)
                    button.Dispose();
            }
        return new(RuntimeInformation.FrameworkDescription,
            "Managed diagnostic, Core Render only, no GPU/native input or NativeAOT performance claim; includes profiler overhead, excludes NewFrame and model reset", warmup, measured, rows.ToArray(), failures.ToArray());
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
