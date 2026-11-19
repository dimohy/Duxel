using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using Duxel.Core;
using System.Numerics;

if (args.Length == 3 && args[1] == "--directtext")
{
    DirectTextBench.Run(args[0],args[2]);
    return;
}

if (args.Length == 2 && args[1] == "--widgets")
{
    WidgetScaleBench.Run(args[0]);
    return;
}
if (args.Length == 2 && args[1] == "--overlays")
{
    OverlayBench.Run(args[0]);
    return;
}
if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--retained"))
    throw new ArgumentException("Supply an output JSON path and optional --retained.");
if (args.Length == 2)
{
    RunRetainedBatcher(args[0]);
    return;
}
var results = new List<DynamicBenchResult>();
foreach (var layerCount in new[] { 1, 8 })
foreach (var primitives in new[] { 100, 2000 })
{
    var screen = new DynamicScreen(layerCount, primitives);
    var atlas = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]);
    var context = new UiContext(atlas, new(1), new(2));
    var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
    var frameInfo = new UiFrameInfo(1f / 60, new(1280, 720), new(1, 1));
    var bounds = new UiRect(0, 0, 1280, 720);
    void Frame()
    {
        context.NewFrame(frameInfo, input, bounds, UiTextSettings.Default);
        context.Render(screen);
        context.GetDrawData().ReleasePooled();
    }
    var warmup = Stopwatch.StartNew();
    do { Frame(); } while (warmup.Elapsed.TotalSeconds < .25);
    var recorder = new BenchFrameRecorder(1_048_576);
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    var last = watch.ElapsedTicks;
    do
    {
        Frame();
        var current = watch.ElapsedTicks;
        recorder.Record((double)(current - last) / Stopwatch.Frequency);
        last = current;
    } while (watch.Elapsed.TotalSeconds < 1);
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    var stats = recorder.Calculate();
    results.Add(new(layerCount,primitives,(double)allocated/stats.Samples,stats));
    context.Dispose();
}
File.WriteAllText(args[0], JsonSerializer.Serialize(new DynamicBenchReport(RuntimeInformation.FrameworkDescription,.25,1,results),BenchJsonContext.Default.DynamicBenchReport));

static void RunRetainedBatcher(string output)
{
    var results = new List<RetainedBenchResult>();
    foreach (var workload in new[] { (1200, 4, 1), (1200, 32, 1), (5000, 4, 1), (1200, 4, 8), (1200, 4, 1200) })
    {
        var (layerCount, commands, sameSceneRun) = workload;
        var atlas = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]);
        using var context = new UiContext(atlas, new(1), new(2));
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(1f / 60, new(1280, 720), new(1, 1)), input, new(0, 0, 1280, 720), UiTextSettings.Default);
        context.Render(new RetainedScreen(layerCount, commands, sameSceneRun));
        var data = context.GetDrawData();
        using var batcher = new UiRetainedLayerBatcher();
        try
        {
            void Frame() { using var borrowed = batcher.Prepare(data); }
            var warmup = Stopwatch.StartNew();
            do { Frame(); } while (warmup.Elapsed.TotalSeconds < .25);
            var recorder = new BenchFrameRecorder(1_048_576);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            var last = watch.ElapsedTicks;
            do
            {
                Frame();
                var current = watch.ElapsedTicks;
                recorder.Record((double)(current - last) / Stopwatch.Frequency);
                last = current;
            } while (watch.Elapsed.TotalSeconds < 1);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var stats = recorder.Calculate();
            var rawFrames = output + $".{layerCount}-{commands}-{sameSceneRun}.frames.csv";
            using (var writer = new StreamWriter(rawFrames))
            {
                writer.WriteLine("frameSeconds");
                foreach (var value in recorder.FrameSeconds)
                    writer.WriteLine(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            results.Add(new(layerCount,commands,sameSceneRun,(double)allocated/stats.Samples,rawFrames,stats));
        }
        finally { data.ReleasePooled(); }
    }
    File.WriteAllText(output, JsonSerializer.Serialize(new RetainedBenchReport("retained-batcher-only; not application FPS",RuntimeInformation.FrameworkDescription,
        .25,1,results),BenchJsonContext.Default.RetainedBenchReport));
}

sealed class RetainedScreen(int layerCount, int commands, int sameSceneRun) : UiScreen
{
    public override void Render(UiImmediateContext ui)
    {
        for (var i = 0; i < layerCount; i++)
        {
            var scene = new UiLayer3D(Matrix4x4.CreateTranslation(0, 0, .2f))
            { PlaneModel = new(default, (i / sameSceneRun) * .0001f) };
            if (ui.BeginLayer("retained-bench-" + i, new(false, 1, default) { Scene3D = scene, ContentVersion = 0 }))
            {
                var draw = ui.GetWindowDrawList();
                for (var command = 0; command < commands; command++)
                {
                    if ((command & 1) == 0)
                        draw.AddRectFilledRounded(new(10 + command * 5, 10, 4, 4), new(0xFFFFFFFF), ui.WhiteTextureId, 1, draw.GetClipRect());
                    else draw.AddCircleFilled(new(12 + command * 5, 12), 2, new(0xFFFFFFFF), ui.WhiteTextureId);
                }
            }
            ui.EndLayer();
        }
    }
}

sealed class DynamicScreen(int layerCount, int primitives) : UiScreen
{
    private readonly string[] _ids = Enumerable.Range(0, layerCount).Select(i => "bench-" + i).ToArray();
    private int _frame;
    public override void Render(UiImmediateContext ui)
    {
        _frame++;
        for (var layer = 0; layer < layerCount; layer++)
        {
            if (!ui.BeginLayer(_ids[layer], new(false, 1, new(layer, layer))))
                throw new InvalidOperationException("Dynamic layer unexpectedly reused a cache.");
            var draw = ui.GetWindowDrawList();
            for (var i = 0; i < primitives; i++)
            {
                var x = (i * 13 + _frame) % 1200;
                var y = (i * 7 + _frame) % 680;
                var color = new UiColor((uint)(0xFF000000 | ((i * 17 + _frame) & 0xFFFFFF)));
                if ((i & 1) == 0)
                    draw.AddTriangleFilled(new(x, y), new(x + 4, y), new(x, y + 4), color);
                else
                    draw.AddRectFilled(new(x, y, 4, 4), color, ui.WhiteTextureId);
            }
            ui.EndLayer();
        }
    }
}
