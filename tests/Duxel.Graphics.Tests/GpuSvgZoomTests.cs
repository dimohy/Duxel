using System.Diagnostics;
using System.Text.Json;
using Duxel.Core;
using Duxel.Svg;
using Duxel.Vulkan;

internal static class GpuSvgZoomTests
{
    internal static void Run(string output, UiContext context, VulkanRendererBackend renderer,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int snapshotWidth, int snapshotHeight, bool cameraMode = false)
    {
        using var store = new SvgStore();
        var id = new UiTextureId(2_300_000_010);
        foreach (var name in new[] { "TheStructorr_Lamborghini_Gallardo", "Red_Gallardo" })
        {
            var started = Stopwatch.GetTimestamp();
            var scene = store.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "Browser", name + ".svg")).GpuScene;
            var preparation = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long? uploaded = null;
            foreach (var zoom in new[] { .25f, 1f, 1.01f, 2f, 4f })
            {
                var fullWidth = (int)MathF.Ceiling(764 * zoom);
                var fullHeight = (int)MathF.Ceiling(372 * zoom);
                var width = cameraMode ? Math.Min(fullWidth, 512) : fullWidth;
                var height = cameraMode ? Math.Min(fullHeight, 256) : fullHeight;
                UiGpuVectorCamera? camera = cameraMode && (fullWidth != width || fullHeight != height)
                    ? new(new(fullWidth, fullHeight), new((fullWidth - width) / 2, (fullHeight - height) / 2)) : null;
                Console.WriteLine($"START {name} / {zoom:F2} / {width} x {height}");
                var allocated = GC.GetTotalAllocatedBytes(true);
                started = Stopwatch.GetTimestamp();
                var pixels = capture(name + "-" + zoom.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), ui =>
                {
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, id, UiTextureFormat.Rgba8Unorm,
                        width, height, ReadOnlyMemory<byte>.Empty)
                    {
                        VectorScene = scene,
                        VectorCamera = camera,
                        IsColorImage = true
                    });
                    ui.GetWindowDrawList().AddImage(id, new(16, 16), new(272, 144), default, new(1, 1), new(255, 255, 255));
                });
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var bytes = GC.GetTotalAllocatedBytes(true) - allocated;
                BrowserPng.Write(Path.Combine(output, name + "-" + zoom.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png"),
                    snapshotWidth, snapshotHeight, pixels);
                using (var stream = File.Create(Path.Combine(output, name + "-" + zoom.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".json")))
                using (var json = new Utf8JsonWriter(stream, new()
                {
                    Indented = true
                }))
                {
                    json.WriteStartObject();
                    json.WriteString("runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
                    json.WriteString("source", name);
                    json.WriteNumber("zoom", zoom);
                    json.WriteNumber("width", width);
                    json.WriteNumber("height", height);
                    json.WriteNumber("virtualWidth", fullWidth);
                    json.WriteNumber("virtualHeight", fullHeight);
                    json.WriteBoolean("camera", cameraMode);
                    json.WriteNumber("preparationMilliseconds", preparation);
                    json.WriteNumber("synchronousRenderReadbackMilliseconds", elapsed);
                    json.WriteNumber("allocatedBytes", bytes);
                    json.WriteNumber("cpuRasterizations", store.Statistics.Rasterizations);
                    json.WriteNumber("sceneUploads", renderer.VectorSceneDiagnostics.Uploads);
                    json.WriteNumber("sceneCommands", scene.CommandCount);
                    json.WriteNumber("sceneBytes", scene.Data.Length * 16);
                    json.WriteNumber("stackCapacity", scene.StackCapacity);
                    json.WriteEndObject();
                }
                Console.WriteLine($"END {name} / {zoom:F2} / {elapsed:F2} ms / CPU raster : {store.Statistics.Rasterizations}");
                check(store.Statistics.Rasterizations == 0, "Complex SVG renders on GPU without CPU pixels : " + name + "/" + zoom);
                check(pixels.Where((_, index) => index % 4 != 3).Distinct().Count() > 32,
                    "Complex SVG GPU output contains varied painted colors : " + name + "/" + zoom);
                uploaded ??= renderer.VectorSceneDiagnostics.Uploads;
                check(renderer.VectorSceneDiagnostics.Uploads == uploaded,
                    "Complex SVG zoom keeps the uploaded path and filter scenes : " + name + "/" + zoom);
            }
        }
        capture("complex-gpu-svg-destroy", _ => context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, id,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty)));
        check(renderer.VectorSceneDiagnostics is { Scenes: 0, Outputs: 0 }, "Complex SVG GPU output and scenes retire after use");
    }
}
