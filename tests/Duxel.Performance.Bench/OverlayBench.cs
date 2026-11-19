using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;

internal static class OverlayBench
{
    internal static void Run(string output)
    {
        var results = new List<OverlayBenchResult>();
        foreach (var tiers in new[] { 0, 1, 8, 32 })
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            var screen = new OverlayScreen(tiers);
            var input = new UiInputState(new(80, 80), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var info = new UiFrameInfo(1f / 60, new(1280, 800), new(1, 1));
            void Frame()
            {
                context.NewFrame(info, input, new(0, 0, 1280, 800), UiTextSettings.Default);
                context.Render(screen);
                context.GetDrawData().ReleasePooled();
            }
            var timer = Stopwatch.StartNew();
            do { Frame(); } while (timer.Elapsed.TotalSeconds < .5);
            var recorder = new BenchFrameRecorder(1_048_576);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            var last = timer.ElapsedTicks;
            var frames = 0;
            do
            {
                Frame();
                var now = timer.ElapsedTicks;
                recorder.Record((double)(now - last) / Stopwatch.Frequency);
                last = now;
                frames++;
            } while (timer.Elapsed.TotalSeconds < 1 && frames < 900_000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var stats = recorder.Calculate();
            var raw = output + $".tiers-{tiers}.csv";
            using (var writer = new StreamWriter(raw))
            {
                writer.WriteLine("frameSeconds");
                foreach (var seconds in recorder.FrameSeconds) writer.WriteLine(seconds.ToString("R", CultureInfo.InvariantCulture));
            }
            // The timed loop never forces GC. This separate observation measures rooted managed storage.
            var rootedBytes = GC.GetTotalMemory(true);
            using var process = Process.GetCurrentProcess();
            results.Add(new(tiers,8,(double)allocated/stats.Samples,rootedBytes,process.PrivateMemorySize64,raw,stats));
            GC.KeepAlive(context);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new OverlayBenchReport("UI generation and draw-data release; no GPU/presentation",RuntimeInformation.FrameworkDescription,
            .5,1,results),BenchJsonContext.Default.OverlayBenchReport));
    }

    private sealed class OverlayScreen(int tiers) : UiScreen
    {
        private readonly string[] _ids = Enumerable.Range(0, tiers).Select(i => "overlay-bench-" + i).ToArray();
        private readonly string[] _labels = Enumerable.Range(0, 8).Select(i => "Action " + i).ToArray();
        private bool _opened;
        public override void Render(UiImmediateContext ui)
        {
            if (!_opened)
            {
                foreach (var id in _ids) ui.OpenPopup(id);
                _opened = true;
            }
            for (var i = 0; i < tiers; i++)
            {
                if (!ui.BeginPopup(_ids[i])) throw new InvalidOperationException("Benchmark popup unexpectedly closed.");
                foreach (var label in _labels) ui.MenuItem(label);
            }
            if (tiers > 0) ui.SetTooltip("Resident overlay and nested menu workload");
            for (var i = 0; i < tiers; i++) ui.EndPopup();
        }
    }
}
