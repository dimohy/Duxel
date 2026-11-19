using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Duxel.Core;
using Duxel.Svg;
using Duxel.Vulkan;

internal static class GpuSvgCameraBenchmark
{
    internal static void Run(string output, UiContext context, VulkanRendererBackend renderer,
        Func<Action<UiImmediateContext>, UiDrawData> build, bool readback = true)
    {
        const int warmup = 16;
        const int measured = 32;
        var csv = Path.Combine(output, "frames.csv");
        if (File.Exists(csv))
            throw new IOException("Specify a fresh camera benchmark directory.");
        using var store = new SvgStore();
        using var writer = new StreamWriter(csv);
        writer.WriteLine("source,zoom,sample,pan,width,height,buildMs," + (readback ? "renderReadbackMs" : "renderCallMs") + ",gpuPriorSlotUs,textureUpdateUs,buildThreadBytes,renderThreadBytes,sceneUploads,xmlParses,cpuRasterizations,presentationSequence,pixelSha256");
        File.WriteAllText(Path.Combine(output, "device.txt"), renderer.ValidationVectorDevicePolicy);
        var texture = new UiTextureId(2_300_000_011);
        foreach (var name in new[] { "TheStructorr_Lamborghini_Gallardo", "Red_Gallardo" })
        {
            var source = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "Browser", name + ".svg");
            var scene = store.Load(source).GpuScene;
            File.WriteAllText(Path.Combine(output, name + ".source.sha256"), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
            long? uploaded = null;
            foreach (var zoom in new[] { .25f, 1f, 1.01f, 2f, 4f })
            {
                var fullWidth = (int)MathF.Ceiling(764 * zoom);
                var fullHeight = (int)MathF.Ceiling(372 * zoom);
                var width = Math.Min(fullWidth, 512);
                var height = Math.Min(fullHeight, 256);
                for (var frame = 0; frame < warmup + measured; frame++)
                {
                    // Alternate the device origin to require fresh GPU pixels without new source geometry.
                    var pan = frame & 1;
                    var camera = new UiGpuVectorCamera(new(fullWidth, fullHeight),
                        new((fullWidth - width) / 2 + pan, (fullHeight - height) / 2));
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var started = Stopwatch.GetTimestamp();
                    using var data = build(ui =>
                    {
                        context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                            UiTextureFormat.Rgba8Unorm, width, height, ReadOnlyMemory<byte>.Empty)
                        {
                            VectorScene = scene,
                            VectorCamera = camera,
                            IsColorImage = true
                        });
                        ui.GetWindowDrawList().AddImage(texture, new(16, 16), new(272, 144),
                            default, new(1, 1), new(255, 255, 255));
                    });
                    var buildMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    var buildBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    allocated = GC.GetAllocatedBytesForCurrentThread();
                    started = Stopwatch.GetTimestamp();
                    byte[]? pixels = null;
                    if (readback)
                        pixels = renderer.RenderVectorAndReadback(data, texture, width, height);
                    else if (!renderer.TryRenderDrawData(data))
                        throw new InvalidOperationException("Camera benchmark presentation was rejected.");
                    var renderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    var renderBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    var metrics = renderer.LastFrameMetrics;
                    if (readback && pixels!.Length != width * height * 4 || metrics.VSyncEnabled || metrics.MsaaSamples != 1)
                        throw new InvalidOperationException("Camera benchmark requires full RGBA readback, VSync off and MSAA1.");
                    if (frame < warmup)
                        continue;
                    if (!double.IsFinite(metrics.GpuRenderMicroseconds) || metrics.GpuRenderMicroseconds <= 0)
                        throw new InvalidOperationException("Camera benchmark requires GPU timestamp samples.");
                    uploaded ??= renderer.VectorSceneDiagnostics.Uploads;
                    if (renderer.VectorSceneDiagnostics.Uploads != uploaded || store.Statistics.Rasterizations != 0)
                        throw new InvalidOperationException("Camera movement must retain GPU scenes and produce no CPU rasters.");
                    writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{name},{zoom:R},{frame - warmup},{pan},{width},{height},{buildMs:R},{renderMs:R},{metrics.GpuRenderMicroseconds:R},{renderer.ValidationTextureUpdateMicroseconds:R},{buildBytes},{renderBytes},{uploaded},{store.Statistics.XmlParses},{store.Statistics.Rasterizations},{metrics.PresentationSequence},{(pixels is null ? "" : Convert.ToHexString(SHA256.HashData(pixels)))}"));
                    if (pixels is not null && frame - warmup < 2)
                        File.WriteAllBytes(Path.Combine(output, name + "-" + zoom.ToString("R", CultureInfo.InvariantCulture) + "-" + pan + ".rgba"), pixels);
                }
                if (!readback)
                {
                    // Validate the same retained output after the measured presentation-only block.
                    for (var pan = 0; pan < 2; pan++)
                    {
                        var camera = new UiGpuVectorCamera(new(fullWidth, fullHeight),
                            new((fullWidth - width) / 2 + pan, (fullHeight - height) / 2));
                        using var data = build(ui =>
                        {
                            context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                                UiTextureFormat.Rgba8Unorm, width, height, ReadOnlyMemory<byte>.Empty)
                            {
                                VectorScene = scene,
                                VectorCamera = camera,
                                IsColorImage = true
                            });
                            ui.GetWindowDrawList().AddImage(texture, new(16, 16), new(272, 144),
                                default, new(1, 1), new(255, 255, 255));
                        });
                        var pixels = renderer.RenderVectorAndReadback(data, texture, width, height);
                        File.WriteAllBytes(Path.Combine(output, name + "-" + zoom.ToString("R", CultureInfo.InvariantCulture) + "-" + pan + ".rgba"), pixels);
                    }
                }
                writer.Flush();
                Console.WriteLine($"Camera benchmark : {name} / {zoom:R} / {measured} samples");
            }
        }
        using (var data = build(_ => context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty))))
            renderer.RenderAndReadback(data);
        if (renderer.VectorSceneDiagnostics is not { Scenes: 0, Outputs: 0 })
            throw new InvalidOperationException("Camera benchmark retained outputs after destruction.");
        var work = renderer.VectorWorkDiagnostics;
        File.WriteAllLines(Path.Combine(output, "vector-work-cache.txt"),
        [
            "Native work allocations/reuses : " + work.Allocations + " / " + work.Reuses,
            "Cached/peak bytes : " + work.CachedBytes + " / " + work.PeakCachedBytes,
            "Cached/pending count : " + work.CachedCount + " / " + work.PendingCount,
            "Logical reusable buffer bytes, not total VRAM or process private/working set."
        ]);
        File.WriteAllLines(Path.Combine(output, "scope.txt"),
        [
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            "Two unchanged complex SVGs; five zooms; 16 warmup + 32 measured frames per source/zoom; one-pixel alternating pan.",
            readback ? "Full premultiplied RGBA texture readback, bounded to 512 x 256; hashes and first two raw outputs retained per case."
                : "No CPU readback during measured frames. Two full raw outputs per case are captured after its measured block, outside timers.",
            "CPU build, render call and texture update times are separate. Texture preparation is included in the render call. Allocation columns cover the calling thread only.",
            "GPU timestamps cover prior reused frame-slot command buffers, including vector compute and UI render, not isolated SVG compute.",
            "Warmup exceeds three frame slots; each measured block has the same source/zoom and alternating pan as its prior-slot samples.",
            "Source preparation, file writes and pixel hashing are outside frame timers. Scene uploads must stay constant during camera movement.",
            "Managed functional/performance diagnostic; not application FPS, physical Linux GPU proof or the original RC3 NativeAOT workload."
        ]);
    }
}
