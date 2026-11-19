using System.Diagnostics;
using System.Globalization;
using Duxel.Core;
using Duxel.Gallery;
using Duxel.Vulkan;

internal static class ObserverMeasurementDiagnostic
{
    internal static void Run(string output, ObserverScreen screen, VulkanRendererBackend renderer,
        Func<Action<UiImmediateContext>, UiDrawData> build)
    {
        screen.SelectedPage = "측정 재사용";
        renderer.SetMsaaSamples(4);
        using var writer = new StreamWriter(Path.Combine(output, "observer-measurement.csv"));
        writer.WriteLine("frame,buildMs,buildAllocatedBytes,submitReadbackMs,draws,uploadBytes,measurements,form,tab,delegated,owned");
        for (var frame = 0; frame < 96 + 256; frame++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            using var data = build(screen.Render);
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            start = Stopwatch.GetTimestamp();
            renderer.RenderAndReadback(data);
            var readback = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (frame < 96)
                continue;
            var m = renderer.LastFrameMetrics;
            var p = screen.MeasurementPage;
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{frame - 96},{elapsed:R},{allocated},{readback:R},{m.DrawCalls},{m.DynamicUploadBytes + m.StagingUploadBytes},{p.Measurements},{p.FormMeasurements},{p.TabMeasurements},{p.DelegatedMeasurements},{p.OwnedSizingMeasurements}"));
        }
        File.WriteAllText(Path.Combine(output, "observer-measurement-scope.txt"),
            "Actual MeasurementPage, native Windows typography, application-style MissingGlyphObserver, 1600x1166 requested window, "
            + "MSAA4, VSync/highlights off, 96 warmup + 256 measured frames. CPU build and serialized GPU readback are separate; not interactive FPS or RC3 NativeAOT.");
    }
}
