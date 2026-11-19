using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;
using System.Runtime.ExceptionServices;

internal static class CpuTimingTests
{
    internal static void Run(Action<bool, string> check)
    {
        var oldMode = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_TIMING_SAMPLING");
        var oldInterval = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY");
        var oldGpu = Environment.GetEnvironmentVariable("DUXEL_VK_GPU_PROFILE");
        var oldDamage = Environment.GetEnvironmentVariable("DUXEL_VK_DAMAGE");
        try
        {
            // This fixture isolates timing collection and deliberately requires one draw per frame.
            Environment.SetEnvironmentVariable("DUXEL_VK_DAMAGE", "0");
            Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_TIMING_SAMPLING", null);
            check(VulkanRendererBackend.ValidationParseCpuTimingSampling(), "CPU timing sampling defaults to log frames");
            Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_TIMING_SAMPLING", "0");
            check(!VulkanRendererBackend.ValidationParseCpuTimingSampling(), "legacy CPU timing mode retains every-frame durations");
            Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_TIMING_SAMPLING", "invalid");
            var invalidRejected = false;
            try { VulkanRendererBackend.ValidationParseCpuTimingSampling(); }
            catch (ArgumentException) { invalidRejected = true; }
            check(invalidRejected, "invalid CPU timing mode fails explicitly rather than silently selecting a different mode");
            Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "2");
            Environment.SetEnvironmentVariable("DUXEL_VK_GPU_PROFILE", "1");
            byte[]? reference = null;
            foreach (var sampled in new[] { false, true })
            {
                Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_TIMING_SAMPLING", sampled ? "1" : "0");
                ExceptionDispatchInfo? failure = null;
                var owner = new Thread(() =>
                {
                    try { RunGpuCase(check, sampled, ref reference); }
                    catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
                }) { IsBackground = true, Name = "DuxelCpuTimingTestOwner" };
                owner.Start();
                if (!owner.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("CPU timing GPU validation did not finish.");
                failure?.Throw();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_TIMING_SAMPLING", oldMode);
            Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", oldInterval);
            Environment.SetEnvironmentVariable("DUXEL_VK_GPU_PROFILE", oldGpu);
            Environment.SetEnvironmentVariable("DUXEL_VK_DAMAGE", oldDamage);
        }
    }

    private static void RunGpuCase(Action<bool, string> check, bool sampled, ref byte[]? reference)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 160, Height = 120, Title = "Duxel CPU timing validation", VSync = false,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new(),
        });
        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(2, EnableVSync: false, MsaaSamples: 1));
        renderer.CreateDeviceObjects();
        var size = platform.FramebufferSize;
        var clip = new UiRect(0, 0, size.Width, size.Height);
        var white = new UiTextureId(2);
        var list = new UiDrawList(
            UiPooledList<UiDrawVertex>.FromArray([
                new(new(20,20), default, new(0xFF0000FF)), new(new(80,20), default, new(0xFF0000FF)),
                new(new(80,80), default, new(0xFF0000FF)), new(new(20,80), default, new(0xFF0000FF)),
            ]),
            UiPooledList<uint>.FromArray([0,1,2,0,2,3]),
            UiPooledList<UiDrawCommand>.FromArray([new(clip, white, 0, 6, 0)]));
        var gpuSamples = 0;
        try
        {
            var borrowed = list with { OwnsBuffers = false };
            for (var frame = 1; frame <= 6; frame++)
            {
                platform.PollEvents();
                using var data = new UiDrawData(new(size.Width, size.Height), default, new(1,1), 4, 6,
                    UiPooledList<UiDrawList>.FromArray([borrowed]),
                    UiPooledList<UiTextureUpdate>.FromArray(frame == 1
                        ? [new(UiTextureUpdateKind.Create, white, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] {255,255,255,255})]
                        : [])) { DrawDataFrameId = (ulong)frame };
                var pixels = renderer.RenderAndReadback(data);
                var metrics = renderer.LastFrameMetrics;
                var expectedTiming = !sampled || frame % 2 == 0;
                var clock = renderer.ValidationCpuTimingClock();
                check(metrics.CpuTimingCollected == expectedTiming && (clock.Start > 0) == expectedTiming
                    && clock.Elapsed >= 0 && clock.EmptyElapsed == 0,
                    $"CPU timing clock follows the log interval without fabricated zero-start elapsed time: sampled={sampled} frame={frame}");
                reference ??= pixels;
                check(metrics.ProfilingEnabled && metrics.DrawCalls == 1 && metrics.DynamicUploadBytes > 0
                    && metrics.PresentationSequence == (ulong)frame && metrics.DrawDataFrameId == (ulong)frame
                    && pixels.AsSpan().SequenceEqual(reference),
                    $"timing sampling preserves GPU pixels, draws, uploads and presentation/source identity: sampled={sampled} frame={frame}");
                if (double.IsFinite(metrics.GpuRenderMicroseconds)) gpuSamples++;
            }
            check(gpuSamples > 0, $"GPU timestamp collection stays active independently of CPU timing sampling: sampled={sampled}");
        }
        finally { list.ReleasePooled(); }
    }
}
