// FBA: automatic resident presentation of menus, popups and tooltips.
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.App;
using Duxel.Core;

Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_GPU_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "1000000");
var screen = new OverlayLayerLab();
DuxelApp.Run(new DuxelAppOptions
{
    Window = new() { Title = "Duxel / Overlay Layer Lab", Width = 1600, Height = 1000, VSync = false, Resizable = !screen.MeasurementEnabled },
    Renderer = new() { MsaaSamples = 1 },
    Frame = new() { EnableRenderPipeline = true, UseEventDrivenPlatformLoop = false, EnableIdleFrameSkip = false },
    Font = new() { FontSize = 15 },
    Screen = screen,
});
screen.Save();

public sealed class OverlayLayerLab : UiScreen
{
    private readonly int _count = BenchOptions.ReadInt("DUXEL_OVERLAY_COUNT", 48, 1, 512);
    private readonly double _seconds = BenchOptions.ReadDouble("DUXEL_OVERLAY_SECONDS", 0, 0);
    private readonly string _output = BenchOptions.ReadString("DUXEL_OVERLAY_OUT", "");
    private readonly string _scenario = BenchOptions.ReadString("DUXEL_OVERLAY_SCENARIO", "motion");
    private readonly int _swatches = BenchOptions.ReadInt("DUXEL_OVERLAY_SWATCHES", 64, 0, 512);
    private readonly string[] _labels = ["Inspect properties", "Bring to front", "Duplicate selection", "Copy reference", "Delete selection"];
    private readonly UiRendererFrameMetrics[] _history = new UiRendererFrameMetrics[64];
    private readonly BenchFrameRecorder _frames = new(262144);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly string[] _ids;
    private IUiPresentationReader? _reader;
    private long _epoch;
    private bool _animate = true;
    private int _warmupFrames;
    private long _warmupStart;
    private double _nextStatus;
    private string _status = "Preparing automatic dynamic layers...";
    private long _allocatedTotal, _measuredUiFrames;
    private double _gpuSum, _drawSum, _uploadSum, _residentSum;
    private int _gpuSamples;
    private long _reusedLists, _rebuiltLists;
    private readonly List<(double Seconds, long Private, long Working, long Managed)> _memory = [];
    private CancellationTokenSource? _memoryStop;
    private Task? _memoryTask;
    private PlatformSize _framebuffer;
    private bool _measurementComplete;
    public bool MeasurementEnabled => _output.Length > 0;
    private string _lastAction = "Right-click the workspace for a context menu.";

    public OverlayLayerLab()
    {
        _ids = Enumerable.Range(0, _count).Select(i => "lab.palette." + i).ToArray();
        if (_output.Length == 0) return;
        if (_seconds <= 0) throw new ArgumentException("A measurement requires positive DUXEL_OVERLAY_SECONDS.");
        if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(_output))))
            throw new DirectoryNotFoundException("Create the measurement output directory before launching.");
        if (File.Exists(_output) || File.Exists(_output + ".frames.csv") || File.Exists(_output + ".memory.csv"))
            throw new IOException("Measurement outputs already exist.");
        _memoryStop = new();
        _memoryTask = SampleMemoryAsync(_memoryStop.Token);
    }

    private async Task SampleMemoryAsync(CancellationToken stop)
    {
        using var process = Process.GetCurrentProcess();
        while (!stop.IsCancellationRequested)
        {
            var epoch = Volatile.Read(ref _epoch);
            if (epoch != 0)
            {
                process.Refresh();
                var sample = (Stopwatch.GetElapsedTime(epoch).TotalSeconds, process.PrivateMemorySize64,
                    process.WorkingSet64, GC.GetTotalMemory(false));
                if (!stop.IsCancellationRequested) _memory.Add(sample);
            }
            await Task.Delay(epoch == 0 ? 25 : 250).ConfigureAwait(false);
        }
    }

    public override void Render(UiImmediateContext ui)
    {
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        _reader ??= ui.GetPresentationReader();
        var received = _reader.Drain(_history);
        for (var i = 0; i < received; i++)
        {
            var metrics = _history[i];
            if (_warmupStart == 0) _warmupStart = metrics.PresentationTimestampTicks;
            if (_epoch == 0)
            {
                _warmupFrames++;
                if (_warmupFrames >= 96 && metrics.PresentationTimestampTicks - _warmupStart >= Stopwatch.Frequency)
                    Volatile.Write(ref _epoch, metrics.PresentationTimestampTicks);
                continue;
            }
            if (metrics.PresentationIntervalSeconds <= 0) continue;
            if (_seconds > 0 && metrics.PresentationTimestampTicks >= _epoch + _seconds * Stopwatch.Frequency)
            { _measurementComplete = true; _memoryStop?.Cancel(); DuxelApp.Exit(); return; }
            if (_output.Length == 0) continue;
            if (_framebuffer == default) _framebuffer = metrics.FramebufferSize;
            else if (_framebuffer != metrics.FramebufferSize)
                throw new InvalidOperationException($"Framebuffer changed during measurement: {_framebuffer.Width}x{_framebuffer.Height} -> {metrics.FramebufferSize.Width}x{metrics.FramebufferSize.Height}, presentation={metrics.PresentationSequence}.");
            _frames.Record(metrics.PresentationIntervalSeconds);
            _drawSum += metrics.DrawCalls;
            _uploadSum += metrics.DynamicUploadBytes;
            _residentSum += metrics.ResidentHits;
            if (double.IsFinite(metrics.GpuRenderMicroseconds) && metrics.GpuRenderMicroseconds > 0)
            { _gpuSum += metrics.GpuRenderMicroseconds; _gpuSamples++; }
        }
        var now = _clock.Elapsed.TotalSeconds;
        if (_output.Length > 0 && now > _seconds + 30) throw new TimeoutException("Overlay presentation measurement did not complete.");
        var viewport = ui.GetMainViewport();
        var top = viewport.WorkPos.Y;
        var size = viewport.WorkSize;
        var draw = ui.GetWindowDrawList();
        draw.AddRectFilled(new(0, top, size.X, size.Y), new(0xFF211A15), ui.WhiteTextureId);
        for (var i = 0; i < 1800; i++)
            draw.AddRectFilled(new(10 + i % 60 * 31f, top + 110 + i / 60 * 29f, 24, 20),
                new(i % 3 == 0 ? 0xFF66513Bu : 0xFF3D3228u), ui.WhiteTextureId);
        ui.SetCursorScreenPos(new(18, top + 10));
        if (_output.Length == 0)
        {
            ui.Text("OVERLAY LAYER LAB / automatic engine residency");
            ui.Checkbox("Animate one palette", ref _animate);
            ui.SameLine();
            if (ui.Button("Open context menu")) ui.OpenPopup("lab.context");
            ui.Text(_status);
            ui.Text(_lastAction);
        }
        var columns = Math.Max(1, (int)(size.X / 242));
        for (var i = 0; i < _count; i++)
        {
            ui.OpenPopup(_ids[i]);
            var x = 14 + i % columns * 242f;
            var y = top + 150 + i / columns % 4 * 166f;
            if (_animate && i == 0 && _scenario == "motion")
            { x += (float)Math.Sin(now * .8) * 10; y += (float)Math.Cos(now * .6) * 6; }
            ui.SetNextPopupPosition(new(x, y));
            if (ui.BeginPopup(_ids[i]))
            {
                ui.PushID(_ids[i]);
                foreach (var label in _labels)
                    if (ui.MenuItem(label, selected: label == _labels[0])) _lastAction = label;
                var preview = ui.GetWindowDrawList();
                var swatchWidth = 208f / Math.Max(1, _swatches);
                for (var swatch = 0; swatch < _swatches; swatch++)
                    preview.AddRectFilled(new(x + 6 + swatch * swatchWidth,
                        y + ui.GetFrameHeight() * 6 - 6, swatchWidth, 4),
                        new((byte)(swatch * 17), (byte)(swatch * 29), (byte)(swatch * 47)), ui.WhiteTextureId);
                ui.PopID();
                ui.EndPopup();
            }
        }
        if (_output.Length == 0)
        {
            if (ui.IsMouseClicked((int)UiMouseButton.Right)) ui.OpenPopup("lab.context");
            if (ui.BeginPopup("lab.context"))
            {
                if (ui.MenuItem("Toggle motion")) { _animate = !_animate; ui.CloseCurrentPopup(); }
                if (ui.BeginMenu("Layer actions"))
                {
                    if (ui.MenuItem("Inspect nested menu")) _lastAction = "Nested menu selected.";
                    if (ui.MenuItem("Close context")) ui.CloseCurrentPopup();
                    ui.EndMenu();
                }
                ui.EndPopup();
            }
            if (!ui.IsPopupOpen("lab.context") &&
                ui.IsMouseHoveringRect(new(0, top), new(size.X, top + 140)) && ui.BeginTooltip())
            {
                ui.Text("Screen-anchored tooltip");
                ui.Text("Move the pointer; palette content stays resident.");
                ui.EndTooltip();
            }
        }
        if (_output.Length == 0 && now >= _nextStatus)
        {
            _nextStatus = now + .25;
            var m = ui.GetRendererFrameMetrics();
            var cache = ui.GetOverlayLayerMetrics();
            _status = string.Create(CultureInfo.InvariantCulture, $"{_count} palettes | FPS {(m.PresentationIntervalSeconds > 0 ? 1 / m.PresentationIntervalSeconds : 0):F0} | draws {m.DrawCalls} | upload {m.DynamicUploadBytes:N0} B | GPU {m.GpuRenderMicroseconds:F1} us | cache R{cache.ReusedLists}/B{cache.RebuiltLists}");
        }
        if (_epoch > 0 && Stopwatch.GetTimestamp() >= _epoch)
        {
            var overlays = ui.GetOverlayLayerMetrics();
            _reusedLists += overlays.ReusedLists;
            _rebuiltLists += overlays.RebuiltLists;
            _allocatedTotal += GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            _measuredUiFrames++;
        }
    }

    public void Save()
    {
        _memoryStop?.Cancel();
        _memoryTask?.GetAwaiter().GetResult();
        _memoryStop?.Dispose();
        if (_output.Length == 0) return;
        if (!_measurementComplete) throw new InvalidOperationException("Measurement stopped before its presentation deadline.");
        if (_frames.Count == 0) throw new InvalidOperationException("No accepted presentations were measured.");
        if (_memory.Count < 3) throw new InvalidOperationException("Too few active-window memory samples.");
        var stats = _frames.Calculate();
        using (var writer = new StreamWriter(_output + ".frames.csv"))
        {
            writer.WriteLine("frameSeconds");
            foreach (var frame in _frames.FrameSeconds) writer.WriteLine(frame.ToString("R", CultureInfo.InvariantCulture));
        }
        using (var writer = new StreamWriter(_output + ".memory.csv"))
        {
            writer.WriteLine("seconds,privateBytes,workingSetBytes,managedBytesWithoutCollection");
            foreach (var sample in _memory)
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{sample.Seconds:R},{sample.Private},{sample.Working},{sample.Managed}"));
        }
        using var stream = File.Create(_output);
        using var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteBoolean("complete", true);
        json.WriteString("framework", RuntimeInformation.FrameworkDescription);
        json.WriteString("scenario", _scenario);
        json.WriteString("timingBasis", "accepted-presentation-history; not physical display refresh");
        json.WriteNumber("palettes", _count);
        json.WriteNumber("swatchesPerPalette", _swatches);
        json.WriteNumber("samples", stats.Samples);
        json.WriteNumber("averageFps", stats.AverageFps);
        json.WriteNumber("medianFrameMs", stats.MedianFrameMs);
        json.WriteNumber("p95FrameMs", stats.P95FrameMs);
        json.WriteNumber("p99FrameMs", stats.P99FrameMs);
        json.WriteNumber("low1PctFps", stats.Low1PctFps);
        json.WriteNumber("drawCalls", _drawSum / stats.Samples);
        json.WriteNumber("dynamicUploadBytes", _uploadSum / stats.Samples);
        json.WriteNumber("residentHits", _residentSum / stats.Samples);
        json.WriteNumber("gpuMicroseconds", _gpuSamples == 0 ? 0 : _gpuSum / _gpuSamples);
        json.WriteNumber("uiRenderAllocatedBytes", _measuredUiFrames == 0 ? 0 : (double)_allocatedTotal / _measuredUiFrames);
        json.WriteNumber("privateBytes", Median(_memory.Select(sample => sample.Private).ToArray()));
        json.WriteNumber("workingSetBytes", Median(_memory.Select(sample => sample.Working).ToArray()));
        json.WriteString("memorySampling", "background sampling every 250ms while active; no forced collection; median");
        json.WriteNumber("memorySamples", _memory.Count);
        json.WriteNumber("framebufferWidth", _framebuffer.Width);
        json.WriteNumber("framebufferHeight", _framebuffer.Height);
        json.WriteNumber("presentationHistoryOverwrites", _reader!.OverwrittenSamples);
        json.WriteNumber("reusedOverlayLists", _measuredUiFrames == 0 ? 0 : (double)_reusedLists / _measuredUiFrames);
        json.WriteNumber("rebuiltOverlayLists", _measuredUiFrames == 0 ? 0 : (double)_rebuiltLists / _measuredUiFrames);
        json.WriteEndObject();
    }

    private static double Median(long[] values)
    {
        Array.Sort(values);
        var middle = values.Length / 2;
        return values.Length % 2 == 0 ? values[middle - 1] / 2d + values[middle] / 2d : values[middle];
    }
}
