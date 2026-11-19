using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.Vulkan;

// Focused MSAA coverage throughput probe; this is not the ERD FPS acceptance test.
internal static class CoverageBenchmark
{
    internal static void Run(VulkanRendererBackend renderer, Func<Action<UiImmediateContext>, UiDrawData> build,
        float width, float height, UiTextureId white, string output)
    {
        renderer.SetMsaaSamples(4);
        var samples = new List<double>();
        var clock = Stopwatch.StartNew();
        var measurementStart = 0.0;
        var previousEnd = 0.0;
        var camera = 0f;
        var frames = 0;
        var drawCalls = 0;
        var gpuMicroseconds = 0.0;
        var gpuSamples = 0;
        var scene = new Action<UiImmediateContext>(c =>
        {
            var clip = new UiRect(0, 0, width, height);
            for (var layer = 0; layer < 16; layer++)
            {
                var model = Matrix4x4.CreateTranslation(camera, 0, .15f + layer * .04f);
                if (c.BeginLayer("coverage-probe-" + layer, new(false, 1, default)
                    { ContentVersion = 0, Scene3D = new(model) }))
                {
                    var draw = c.GetWindowDrawList();
                    for (var shape = 0; shape < 128; shape++)
                    {
                        var x = (shape % 16) * width / 17 + layer * .13f;
                        var y = (shape / 16) * height / 9 + layer * .17f;
                        draw.AddRectFilledRounded(new(x, y, width / 20, height / 12),
                            new(0xFF6080C0), white, 4.3f, clip);
                    }
                }
                c.EndLayer();
            }
        });
        while (clock.Elapsed.TotalSeconds < 8)
        {
            var start = clock.Elapsed.TotalSeconds;
            camera = MathF.Sin((float)start) * .08f;
            var data = build(scene);
            try
            {
                if (!renderer.TryRenderDrawData(data)) throw new InvalidOperationException("Coverage benchmark frame was skipped.");
            }
            finally { data.Dispose(); }
            var end = clock.Elapsed.TotalSeconds;
            if (start < 2) continue;
            if (frames == 0) measurementStart = previousEnd = start;
            frames++;
            samples.Add(end - previousEnd);
            previousEnd = end;
            drawCalls = renderer.LastFrameMetrics.DrawCalls;
            var gpu = renderer.LastFrameMetrics.GpuRenderMicroseconds;
            if (gpu > 0)
            {
                gpuMicroseconds += gpu;
                gpuSamples++;
            }
        }
        var elapsed = previousEnd - measurementStart;
        File.WriteAllLines(Path.Combine(output, "coverage-frame-seconds.txt"),
            samples.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        samples.Sort();
        double Percentile(double p)
        {
            var position = (samples.Count - 1) * p;
            var lower = (int)position;
            var upper = Math.Min(lower + 1, samples.Count - 1);
            return (samples[lower] + (samples[upper] - samples[lower]) * (position - lower)) * 1000;
        }
        var slowestCount = Math.Max(1, (int)Math.Ceiling(samples.Count * .01));
        var result = new
        {
            framework = RuntimeInformation.FrameworkDescription,
            scope = "2048 retained rounded shapes / 16 camera-transformed planes / MSAA4; not ERD acceptance",
            width, height, warmupSeconds = 2, requestedSeconds = 6, frames, elapsed,
            fps = frames / elapsed, medianMs = Percentile(.5), p95Ms = Percentile(.95), p99Ms = Percentile(.99),
            onePercentLowFps = slowestCount / samples.TakeLast(slowestCount).Sum(), drawCalls,
            gpuMicroseconds = gpuSamples > 0 ? gpuMicroseconds / gpuSamples : (double?)null,
            gpuSamples,
            shader = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_FRAGMENT_SHADER"),
            coverage = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_OPAQUE_COVERAGE") ?? "default",
        };
        File.WriteAllText(Path.Combine(output, "coverage-benchmark.json"), JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Coverage probe: {frames / elapsed:F2} FPS; {drawCalls} draws; {frames} frames.");
    }
}
