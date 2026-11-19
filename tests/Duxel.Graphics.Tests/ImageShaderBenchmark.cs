using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Duxel.Core;
using Duxel.Vulkan;

internal static class ImageShaderBenchmark
{
    internal static void Run(string output, VulkanRendererBackend renderer,
        Func<Action<UiImmediateContext>, UiDrawData> build, UiTextureId white, UiRect clip,
        bool circleMask = false, bool colorImages = false, bool roundedMask = false, bool projectedClip = false)
    {
        if (projectedClip && (circleMask || roundedMask))
            throw new ArgumentException("The projected clip workload uses its own clip geometry.");
        const int warmup = 96;
        const int measured = 256;
        using var writer = new StreamWriter(Path.Combine(output, "frames.csv"));
        writer.WriteLine("frame,elapsedMs,gpuUs,draws,uploadBytes,pixelSha256");
        var validGpuSamples = 0;
        var imageTexture = new UiTextureId(99_000_001);
        for (var frame = 0; frame < warmup + measured; frame++)
        {
            var shade = (byte)(240 + frame % 2);
            using var data = build(ui =>
            {
                if (colorImages && frame == 0)
                    ui.QueueTextureUpdate(new(UiTextureUpdateKind.Create, imageTexture, UiTextureFormat.Rgba8Unorm, 2, 1,
                        new byte[] { 255, 0, 0, 255, 0, 0, 255, 0 })
                    {
                        IsColorImage = true
                    });
                if (projectedClip)
                {
                    DrawProjectedClip(ui, shade, colorImages ? imageTexture : null);
                    return;
                }
                var list = ui.GetWindowDrawList();
                list.AddRectFilled(clip, new(0, 0, 0), white, clip);
                using var mask = circleMask ? list.CircleCutout(new(160, 120), 48) : default;
                using var rounded = roundedMask ? list.RoundedClip(new(8, 8, 304, 224), 24) : default;
                for (var index = 0; index < 256; index++)
                {
                    var bounds = new UiRect(4 + index % 16 * 19, 4 + index / 16 * 14, 17, 12);
                    if (colorImages)
                    {
                        ui.SetCursorScreenPos(new(bounds.X, bounds.Y));
                        ui.Image(imageTexture, new(bounds.Width, bounds.Height), new(shade, 255, 255));
                    }
                    else
                        list.AddRectFilledRounded(bounds, new(shade, 128, 64), white, 4, clip);
                }
            });
            var start = Stopwatch.GetTimestamp();
            var pixels = renderer.RenderAndReadback(data);
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var metrics = renderer.LastFrameMetrics;
            if (metrics.DrawCalls == 0 || metrics.MsaaSamples != 1 || metrics.VSyncEnabled)
                throw new InvalidOperationException("Shader diagnostic requires actual draws, MSAA1 and VSync off.");
            if (frame < warmup)
                continue;
            if (double.IsFinite(metrics.GpuRenderMicroseconds) && metrics.GpuRenderMicroseconds > 0)
                validGpuSamples++;
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{frame - warmup},{elapsed:R},{metrics.GpuRenderMicroseconds:R},{metrics.DrawCalls},{metrics.DynamicUploadBytes},{Convert.ToHexString(SHA256.HashData(pixels))}"));
        }
        if (validGpuSamples == 0)
            throw new InvalidOperationException("Shader diagnostic produced no GPU timestamp samples.");
        File.WriteAllLines(Path.Combine(output, "scope.txt"),
        [
            RuntimeInformation.FrameworkDescription,
            "96 warmup + 256 measured frames; 256 draws; MSAA1; VSync off; serialized readback.",
            "Circle mask : " + circleMask,
            "Rounded mask : " + roundedMask,
            "Color images : " + colorImages + "; one source upload before warmup, then retained texture sampling.",
            "Projected clip : " + projectedClip + "; fractional rectangles or images with distinct local clips.",
            "Diagnostic measures GPU timestamps and serialized readback time separately."
        ]);
    }

    private static void DrawProjectedClip(UiImmediateContext ui, byte shade, UiTextureId? texture)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        ui.BeginLayer("projected-clip-bench", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
        try
        {
            var context = new UiControlContext(ui);
            for (var index = 0; index < 256; index++)
            {
                var bounds = new UiRect(4.25f + index % 16 * 19, 4.75f + index / 16 * 14, 17, 12);
                using var clipping = context.Clip(bounds with { Height = 10.5f });
                if (texture is { } image)
                {
                    ui.SetCursorScreenPos(new(bounds.X, bounds.Y));
                    ui.Image(image, new(bounds.Width, bounds.Height), new(shade, 255, 255));
                }
                else
                    context.Frame(bounds, new(shade, 128, 64), new(220, 210, 190), 0, 1);
            }
        }
        finally
        {
            ui.EndLayer();
        }
    }
}
