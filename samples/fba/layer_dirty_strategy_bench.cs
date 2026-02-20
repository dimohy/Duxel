// FBA: Layer Dirty Strategy Benchmark (all vs single)
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Duxel.App;
using Duxel.Core;

var screen = new LayerDirtyStrategyBenchScreen();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Layer Dirty Strategy Bench (FBA)",
        Width = 1720,
        Height = 980,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = screen
});

public sealed class LayerDirtyStrategyBenchScreen : UiScreen
{
    private sealed class LayerInfo
    {
        public required int Id { get; init; }
        public required UiVector2 Position { get; init; }
        public required UiVector2 Size { get; init; }
        public required UiColor HeaderColor { get; init; }
        public required int Density { get; init; }
        public required string Name { get; init; }
    }

    private readonly List<LayerInfo> _layers = [];
    private readonly Process _process = Process.GetCurrentProcess();

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_DIRTY_BENCH_OUT");
    private readonly double _phaseSeconds = ReadPhaseSeconds();
    private readonly int _layerCount = ReadLayerCount();
    private readonly int _densityPerLayer = ReadDensityPerLayer();
    private readonly UiLayerCacheBackend _cacheBackend = ReadCacheBackend();

    private int _phaseIndex;
    private double _phaseElapsed;
    private double _fpsSum;
    private double _cpuSum;
    private int _samples;
    private int _singleDirtyCursor;
    private readonly List<string> _records = [];

    private double _lastTime;
    private double _cpuSampleTime;
    private TimeSpan _cpuSampleProcessTime;
    private float _cpuPercent;
    private int _cacheBuildCount;

    public LayerDirtyStrategyBenchScreen()
    {
        BuildLayers();
        _cpuSampleTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        _cpuSampleProcessTime = _process.TotalProcessorTime;
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0.0001d, 0.05d);
        _lastTime = now;

        UpdateCpu(now);

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            ApplyDirtyStrategy(ui);
        }

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);
        DrawCanvasWindow(ui, bounds);
        DrawStatsWindow(ui, bounds, delta);

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            TickBenchmark(delta);
        }

        DuxelApp.RequestFrame();
    }

    private void DrawCanvasWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 12f, bounds.Y + 12f));
        ui.SetNextWindowSize(new UiVector2(bounds.Width - 24f, bounds.Height - 220f));
        ui.BeginWindow("Layer Canvas");

        var origin = ui.GetCursorScreenPos();
        var avail = ui.GetContentRegionAvail();
        var canvas = new UiRect(origin.X, origin.Y, MathF.Max(1f, avail.X), MathF.Max(1f, avail.Y));

        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        drawList.PushTexture(white);
        drawList.PushClipRect(canvas);
        drawList.AddRectFilled(canvas, new UiColor(0xFF161616), white, canvas);

        for (var i = 0; i < _layers.Count; i++)
        {
            DrawLayer(ui, drawList, canvas, _layers[i]);
        }

        drawList.PopClipRect();
        drawList.PopTexture();

        ui.SetCursorScreenPos(new UiVector2(origin.X, origin.Y));
        ui.Dummy(new UiVector2(canvas.Width, canvas.Height));
        ui.EndWindow();
    }

    private void DrawLayer(UiImmediateContext ui, UiDrawListBuilder drawList, UiRect canvas, LayerInfo layer)
    {
        ui.DrawLayerCardInteractive(
            canvas,
            layer.Position,
            layer.Size,
            layer.HeaderColor,
            layer.Name,
            out _,
            out var bodyRect,
            out _,
            bodyBackground: new UiColor(0xCC202020),
            borderColor: new UiColor(0xFF8A8A8A),
            headerHeight: 22f,
            borderThickness: 1f,
            headerTextInsetX: 6f,
            headerTextInsetY: 3f,
            hitTestId: $"layer_card_{layer.Id}");

        var layerOptions = new UiLayerOptions(
            StaticCache: true,
            Opacity: 1f,
            Translation: new UiVector2(bodyRect.X, bodyRect.Y),
            CacheBackend: _cacheBackend);

        var layerId = $"layer_body_{layer.Id}";
        var shouldDraw = ui.BeginLayer(layerId, layerOptions);
        if (shouldDraw)
        {
            DrawHeavyLayerBody(ui.GetWindowDrawList(), new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height), layer.Density, ui.WhiteTextureId);
            _cacheBuildCount++;
        }
        ui.EndLayer();
    }

    private static void DrawHeavyLayerBody(UiDrawListBuilder drawList, UiRect rect, int density, UiTextureId white)
    {
        drawList.AddRectFilled(rect, new UiColor(0xAA2B2B2B), white, rect);
        var width = MathF.Max(1f, rect.Width - 6f);
        var height = MathF.Max(1f, rect.Height - 6f);

        for (var i = 0; i < density; i++)
        {
            var t = i + 1;
            var px = 3f + ((t * 37) % 1000) * 0.001f * width;
            var py = 3f + ((t * 73) % 1000) * 0.001f * height;
            var r = 0.8f + ((t * 13) % 5) * 0.35f;
            var c = (uint)((t * 29) & 0xFF);
            var color = new UiColor(0xFF000000 | (c << 16) | ((255u - c) << 8) | (120u + (c % 120u)));
            drawList.AddCircleFilled(new UiVector2(px, py), r, color, white, rect, 8);
        }
    }

    private void DrawStatsWindow(UiImmediateContext ui, UiRect bounds, double delta)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 12f, bounds.Y + bounds.Height - 196f));
        ui.SetNextWindowSize(new UiVector2(560f, 184f));
        ui.BeginWindow("Dirty Bench Stats");

        ui.TextV("Phase: {0}", _phaseIndex == 0 ? "all-dirty" : "single-dirty");
        ui.TextV("Layer Count: {0}", _layerCount);
        ui.TextV("Density / Layer: {0}", _densityPerLayer);
        ui.TextV("Cache Backend: {0}", _cacheBackend == UiLayerCacheBackend.Texture ? "Texture" : "DrawList");
        ui.TextV("Frame dt: {0:0.000} ms", delta * 1000.0);
        ui.TextV("CPU: {0:0.0}%", _cpuPercent);
        ui.TextV("Layer Cache Build Count: {0}", _cacheBuildCount);

        ui.EndWindow();
    }

    private void ApplyDirtyStrategy(UiImmediateContext ui)
    {
        if (_phaseIndex == 0)
        {
            ui.MarkAllLayersDirty();
            return;
        }

        if (_layers.Count == 0)
        {
            return;
        }

        var layerId = _layers[_singleDirtyCursor].Id;
        ui.MarkLayerDirty($"layer_body_{layerId}");
        _singleDirtyCursor = (_singleDirtyCursor + 1) % _layers.Count;
    }

    private void TickBenchmark(double delta)
    {
        if (_phaseIndex > 1)
        {
            return;
        }

        _phaseElapsed += delta;
        _fpsSum += 1.0 / delta;
        _cpuSum += _cpuPercent;
        _samples++;

        if (_phaseElapsed < _phaseSeconds)
        {
            return;
        }

        var avgFps = _samples > 0 ? _fpsSum / _samples : 0d;
        var avgCpu = _samples > 0 ? _cpuSum / _samples : 0d;

        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"strategy\":\"{1}\",\"layers\":{2},\"density\":{3},\"backend\":\"{4}\",\"avgFps\":{5:0.###},\"avgCpu\":{6:0.###},\"cacheBuildCount\":{7},\"samples\":{8}}}",
            _phaseIndex,
            _phaseIndex == 0 ? "all" : "single",
            _layerCount,
            _densityPerLayer,
            _cacheBackend == UiLayerCacheBackend.Texture ? "texture" : "drawlist",
            avgFps,
            avgCpu,
            _cacheBuildCount,
            _samples));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _fpsSum = 0d;
        _cpuSum = 0d;
        _samples = 0;
        _cacheBuildCount = 0;

        if (_phaseIndex > 1)
        {
            WriteResults();
            Environment.Exit(0);
        }
    }

    private void WriteResults()
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_benchOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sb = new StringBuilder();
        sb.Append("{\"phaseSeconds\":");
        sb.Append(_phaseSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",\"results\":[");
        for (var i = 0; i < _records.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(_records[i]);
        }
        sb.Append("]}");

        File.WriteAllText(_benchOutputPath, sb.ToString());
    }

    private void BuildLayers()
    {
        _layers.Clear();

        var columns = Math.Max(2, (int)MathF.Sqrt(_layerCount));
        var rows = (int)MathF.Ceiling(_layerCount / (float)columns);
        const float startX = 20f;
        const float startY = 20f;
        const float gapX = 8f;
        const float gapY = 8f;
        const float layerW = 220f;
        const float layerH = 150f;

        var id = 1;
        for (var r = 0; r < rows && id <= _layerCount; r++)
        {
            for (var c = 0; c < columns && id <= _layerCount; c++)
            {
                var x = startX + c * (layerW + gapX);
                var y = startY + r * (layerH + gapY);
                var hue = (uint)((id * 37) & 0xFF);
                var color = new UiColor(0xFF000000 | (hue << 16) | ((255u - hue) << 8) | 180u);

                _layers.Add(new LayerInfo
                {
                    Id = id,
                    Name = $"L{id:00}",
                    Position = new UiVector2(x, y),
                    Size = new UiVector2(layerW, layerH),
                    HeaderColor = color,
                    Density = _densityPerLayer
                });

                id++;
            }
        }
    }

    private void UpdateCpu(double now)
    {
        var elapsed = now - _cpuSampleTime;
        if (elapsed < 0.25d)
        {
            return;
        }

        var processTime = _process.TotalProcessorTime;
        var cpuDelta = (processTime - _cpuSampleProcessTime).TotalSeconds;
        var cores = Math.Max(1, Environment.ProcessorCount);
        _cpuPercent = (float)Math.Clamp(cpuDelta / (elapsed * cores) * 100.0, 0.0, 100.0);

        _cpuSampleTime = now;
        _cpuSampleProcessTime = processTime;
    }

    private static double ReadPhaseSeconds()
    {
        return BenchOptions.ReadDouble("DUXEL_DIRTY_BENCH_PHASE_SECONDS", 2d, minExclusive: 0.1d);
    }

    private static int ReadLayerCount()
    {
        return BenchOptions.ReadInt("DUXEL_DIRTY_BENCH_LAYERS", 36, minInclusive: 4, maxInclusive: 128);
    }

    private static int ReadDensityPerLayer()
    {
        return BenchOptions.ReadInt("DUXEL_DIRTY_BENCH_DENSITY", 2200, minInclusive: 200, maxInclusive: 12000);
    }

    private static UiLayerCacheBackend ReadCacheBackend()
    {
        var raw = BenchOptions.ReadString("DUXEL_DIRTY_BENCH_BACKEND");
        if (string.Equals(raw, "texture", StringComparison.OrdinalIgnoreCase))
        {
            return UiLayerCacheBackend.Texture;
        }

        return UiLayerCacheBackend.DrawList;
    }
}
