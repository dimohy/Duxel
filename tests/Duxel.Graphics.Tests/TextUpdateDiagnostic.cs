using System.Diagnostics;
using System.Globalization;
using Duxel.Core;
using Duxel.Vulkan;

internal static class TextUpdateDiagnostic
{
    internal static void Run(string output, VulkanRendererBackend renderer, Func<Action<UiImmediateContext>, UiDrawData> build)
    {
        using var text = Fluent.Body1("한글 텍스트 갱신 English 00000000");
        using var writer = new StreamWriter(Path.Combine(output, "text-update.csv"));
        writer.WriteLine("run,phase,frame,buildMs,submitReadbackMs,gpuUs,draws,uploadBytes");
        for (var run = 0; run < 3; run++)
            foreach (var changing in run % 2 == 0 ? new[] { false, true } : new[] { true, false })
                for (var frame = 0; frame < 96 + 256; frame++)
                {
                    // Unique equal-length values prevent a tiny repeating-string cache from hiding editing costs.
                    text.Source = "한글 텍스트 갱신 English " + (changing ? run * 10000 + frame : 0).ToString("D8", CultureInfo.InvariantCulture);
                    var start = Stopwatch.GetTimestamp();
                    using var data = build(ui =>
                    {
                        ui.SetRepaintHighlights(false);
                        ui.EnableRootViewportContentLayout(contentPadding: 12);
                        text.Render(ui);
                    });
                    var buildMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    start = Stopwatch.GetTimestamp();
                    renderer.RenderAndReadback(data);
                    var submit = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    var m = renderer.LastFrameMetrics;
                    if (frame >= 96)
                        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"{run},{(changing ? "changing" : "unchanged")},{frame - 96},{buildMs:R},{submit:R},{m.GpuRenderMicroseconds:R},{m.DrawCalls},{m.DynamicUploadBytes + m.StagingUploadBytes}"));
                }
        File.WriteAllText(Path.Combine(output, "text-update-scope.txt"),
            "Three alternating runs; 96 warmup + 256 samples per phase. Equal-length native Fluent text, VSync off, highlights off. "
            + "Serialized readback is included in submitReadbackMs. This is not interactive Observer FPS or fixed RC3 NativeAOT performance.");
    }
}
