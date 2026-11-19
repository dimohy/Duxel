using System.Numerics;
using System.Runtime.ExceptionServices;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class CompiledCapacityTests
{
    internal static void Run(Action<bool, string> check)
    {
        var previous = Environment.GetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT");
        try
        {
            Environment.SetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT", null);
            check(VulkanRendererBackend.ValidationParseCompiledIndexLimit() == 2097152,
                "compiled index capacity defaults to two million binary entries");
            foreach (var capacity in new[] { 1048576, 2097152, 4194304, 8388608 })
            {
                Environment.SetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT", capacity.ToString());
                check(VulkanRendererBackend.ValidationParseCompiledIndexLimit() == capacity,
                    "compiled index capacity accepts supported bounded value " + capacity);
            }
            Environment.SetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT", "3145728");
            var rejected = false;
            try { VulkanRendererBackend.ValidationParseCompiledIndexLimit(); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "unsupported compiled index capacity fails explicitly");

            byte[][]? reference = null;
            foreach (var capacity in new[] { 1048576, 2097152 })
            {
                Environment.SetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT", capacity.ToString());
                ExceptionDispatchInfo? failure = null;
                var owner = new Thread(() =>
                {
                    try { RunGpuCase(check, capacity, ref reference); }
                    catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
                }) { IsBackground = true, Name = "DuxelCompiledCapacityOwner" };
                owner.Start();
                if (!owner.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("Compiled capacity GPU test timed out.");
                failure?.Throw();
            }
        }
        finally { Environment.SetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT", previous); }
    }

    private static void RunGpuCase(Action<bool, string> check, int capacity, ref byte[][]? reference)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 200, Height = 160, Title = "Duxel compiled capacity validation", VSync = false,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new(),
        });
        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(2, EnableVSync: false, MsaaSamples: 1));
        renderer.CreateDeviceObjects();
        var size = platform.FramebufferSize;
        var clip = new UiRect(0, 0, size.Width, size.Height);
        var white = new UiTextureId(2);
        const int indicesPerSource = 600006;
        UiDrawList Source(string tag, float offset, UiColor color)
        {
            // Degenerate triangles exercise the real index-capacity boundary
            // without adding hundreds of thousands of overlapping fragments.
            var indices = new uint[indicesPerSource];
            new uint[] { 0, 1, 2, 0, 2, 3 }.CopyTo(indices, indices.Length - 6);
            return new(UiPooledList<UiDrawVertex>.FromArray([
                    new(new(20 + offset, 20), default, color), new(new(60 + offset, 20), default, color),
                    new(new(60 + offset, 60), default, color), new(new(20 + offset, 60), default, color),
                ]), UiPooledList<uint>.FromArray(indices),
                UiPooledList<UiDrawCommand>.FromArray([new(clip, white, 0, indicesPerSource, 0)]),
                StaticGeometryKey: "duxel.layer.static:capacity." + tag);
        }
        var front = Source("front", 0, new(0xFF0000FF));
        var back = Source("back", 20, new(0xFFFF0000));
        var compiled = Environment.GetEnvironmentVariable("DUXEL_VK_COMPILED_LAYERS") != "0";
        var pixelsByFrame = new byte[3][];
        try
        {
            for (var frame = 0; frame < 3; frame++)
            {
                platform.PollEvents();
                var camera = Matrix4x4.CreateTranslation(frame == 0 ? 0 : .1f, 0, 0);
                UiDrawList Plane(UiDrawList source, float depth) => source with
                {
                    OwnsBuffers = false,
                    Scene3D = new(camera) { PlaneModel = new(default, depth) },
                };
                using var data = new UiDrawData(new(size.Width, size.Height), default, new(1, 1), 8,
                    indicesPerSource * 2, UiPooledList<UiDrawList>.FromArray([Plane(front, .2f), Plane(back, .7f)]),
                    UiPooledList<UiTextureUpdate>.FromArray(frame == 0
                        ? [new(UiTextureUpdateKind.Create, white, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 })]
                        : [])) { DrawDataFrameId = (ulong)frame + 1 };
                var pixels = renderer.RenderAndReadback(data);
                pixelsByFrame[frame] = pixels;
                var metrics = renderer.LastFrameMetrics;
                var expectedDraws = compiled ? capacity == 1048576 ? 2 : 1 : 0;
                check(metrics.CompiledIndexLimit == capacity && metrics.CompiledLayerDraws == expectedDraws
                    && metrics.PresentationSequence == (ulong)frame + 1,
                    $"capacity boundary keeps presentation identity and expected compiled draws: capacity={capacity} frame={frame}");
                check(metrics.CompiledIndexUploadBytes == (compiled && frame == 0 ? (ulong)indicesPerSource * 2 * sizeof(uint) : 0)
                    && (frame == 0 || metrics.DynamicUploadBytes == 0 && metrics.ResidentCreates == 0 && metrics.ResidentUpdates == 0),
                    $"capacity change retains camera-only geometry and compiled index uploads: capacity={capacity} frame={frame}");
                if (reference is not null)
                    check(pixels.AsSpan().SequenceEqual(reference[frame]),
                        "larger compiled group preserves exact GPU pixels at camera frame " + frame);
            }
            var redPixel = (30 * size.Width + 30) * 4;
            check(pixelsByFrame[0][redPixel] == 255 && pixelsByFrame[0][redPixel + 2] == 0,
                "large index stream renders nonempty foreground color rather than only degenerate triangles");
            reference ??= pixelsByFrame;
        }
        finally { front.ReleasePooled(); back.ReleasePooled(); }
    }
}
