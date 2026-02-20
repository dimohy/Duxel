// FBA: Global Dirty Strategy Benchmark (all-dynamic vs global-static-tag)
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Duxel.App;
using Duxel.Core;

var screen = new GlobalDirtyStrategyBenchScreen();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Global Dirty Strategy Bench (FBA)",
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

public sealed class GlobalDirtyStrategyBenchScreen : UiScreen
{
    private const string GlobalStaticTag = "duxel.global.static:bench:bg:v2";

    private readonly Process _process = Process.GetCurrentProcess();
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_GLOBAL_DIRTY_BENCH_OUT");
    private readonly double _phaseSeconds = ReadPhaseSeconds();
    private readonly int _density = ReadDensity();
    private readonly int _tileColumns = ReadTileColumns();
    private readonly int _tileRows = ReadTileRows();

    private int _phaseIndex;
    private double _phaseElapsed;
    private double _fpsSum;
    private double _cpuSum;
    private int _samples;
    private readonly List<string> _records = [];

    private double _lastTime;
    private float _liveFps;
    private double _cpuSampleTime;
    private TimeSpan _cpuSampleProcessTime;
    private float _cpuPercent;
    private bool _manualUseGlobalStaticCache = true;
    private double _dynamicAvgFps;
    private double _cacheAvgFps;
    private bool _hasDynamicAvgFps;
    private bool _hasCacheAvgFps;
    private UiPooledList<UiDrawList>? _cachedBackground;
    private UiRect _cachedCanvas;

    public GlobalDirtyStrategyBenchScreen()
    {
        _cpuSampleTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        _cpuSampleProcessTime = _process.TotalProcessorTime;
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0.0001d, 0.05d);
        _lastTime = now;
        _liveFps = (float)(1d / delta);

        UpdateCpu(now);

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);
        DrawCanvasWindow(ui, bounds, now);
        DrawStatsWindow(ui, bounds, delta);

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            TickBenchmark(delta);
        }

        DuxelApp.RequestFrame();
    }

    private void DrawCanvasWindow(UiImmediateContext ui, UiRect bounds, double now)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 12f, bounds.Y + 12f));
        ui.SetNextWindowSize(new UiVector2(bounds.Width - 24f, bounds.Height - 220f));
        ui.BeginWindow("Global Dirty Canvas");

        var canvas = ui.BeginWindowCanvas(new UiColor(0xFF111111), ui.WhiteTextureId, clipToCanvas: true);

        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;

        drawList.Split(2);

        drawList.SetCurrentChannel(0);
        var useGlobalStaticCache = string.IsNullOrWhiteSpace(_benchOutputPath)
            ? _manualUseGlobalStaticCache
            : _phaseIndex == 1;
        if (useGlobalStaticCache)
        {
            EnsureBackgroundCache(canvas, white);
            if (_cachedBackground is not null)
            {
                for (var i = 0; i < _cachedBackground.Count; i++)
                {
                    drawList.Append(_cachedBackground[i]);
                }
            }
        }
        else
        {
            DrawHeavyBackground(drawList, canvas, _tileColumns, _tileRows, _density, white);
        }

        drawList.SetCurrentChannel(1);
        DrawDynamicOverlay(drawList, canvas, now, white);

        drawList.Merge();

        _ = ui.EndWindowCanvas();
        ui.EndWindow();
    }

    private void EnsureBackgroundCache(UiRect canvas, UiTextureId white)
    {
        if (_cachedBackground is not null
            && MathF.Abs(_cachedCanvas.Width - canvas.Width) < 0.5f
            && MathF.Abs(_cachedCanvas.Height - canvas.Height) < 0.5f)
        {
            return;
        }

        ReleaseBackgroundCache();

        var builder = new UiDrawListBuilder(canvas);
        builder.PushTexture(white);
        builder.PushClipRect(canvas);
        builder.PushCommandUserData(GlobalStaticTag);
        DrawHeavyBackground(builder, canvas, _tileColumns, _tileRows, _density, white);
        builder.PopCommandUserData();
        builder.PopClipRect();
        builder.PopTexture();

        _cachedBackground = builder.Build();
        _cachedCanvas = canvas;
    }

    private void ReleaseBackgroundCache()
    {
        if (_cachedBackground is null)
        {
            return;
        }

        for (var i = 0; i < _cachedBackground.Count; i++)
        {
            _cachedBackground[i].ReleasePooled();
        }

        _cachedBackground.Return();
        _cachedBackground = null;
    }

    private static void DrawHeavyBackground(UiDrawListBuilder drawList, UiRect canvas, int cols, int rows, int density, UiTextureId white)
    {
        var inner = new UiRect(canvas.X + 10f, canvas.Y + 10f, canvas.Width - 20f, canvas.Height - 20f);
        drawList.AddRectFilled(inner, new UiColor(0xFF181818), white, canvas);

        var tileW = inner.Width / Math.Max(1, cols);
        var tileH = inner.Height / Math.Max(1, rows);

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var x = inner.X + c * tileW;
                var y = inner.Y + r * tileH;
                var tile = new UiRect(x + 2f, y + 2f, MathF.Max(8f, tileW - 4f), MathF.Max(8f, tileH - 4f));
                var hue = (uint)(((r * 97) + (c * 53)) & 0xFF);
                var tileColor = new UiColor(0xFF000000 | (hue << 16) | ((255u - hue) << 8) | 120u);
                drawList.AddRectFilled(tile, tileColor, white, canvas);

                var dotCount = Math.Max(8, density / Math.Max(1, rows * cols));
                for (var i = 0; i < dotCount; i++)
                {
                    var t = i + 1;
                    var px = tile.X + ((t * 37) % 1000) * 0.001f * tile.Width;
                    var py = tile.Y + ((t * 73) % 1000) * 0.001f * tile.Height;
                    var radius = 0.7f + ((t * 13) % 5) * 0.3f;
                    var s = (uint)((t * 29 + r * 11 + c * 17) & 0xFF);
                    var color = new UiColor(0xFF000000 | (s << 16) | ((255u - s) << 8) | (100u + (s % 130u)));
                    drawList.AddCircleFilled(new UiVector2(px, py), radius, color, white, canvas, 8);
                }
            }
        }
    }

    private static void DrawDynamicOverlay(UiDrawListBuilder drawList, UiRect canvas, double now, UiTextureId white)
    {
        var t = (float)now;
        var cx = canvas.X + canvas.Width * (0.5f + 0.35f * MathF.Sin(t * 1.4f));
        var cy = canvas.Y + canvas.Height * (0.5f + 0.35f * MathF.Cos(t * 1.1f));

        var markerRect = new UiRect(cx - 56f, cy - 56f, 112f, 112f);
        drawList.AddRectFilled(markerRect, new UiColor(0xAA203040), white, canvas);
        drawList.AddCircleFilled(new UiVector2(cx, cy), 18f, new UiColor(0xFFE0E050), white, canvas, 24);
        drawList.AddCircle(new UiVector2(cx, cy), 44f, new UiColor(0xFFF5F5F5), 28, 2f);

        for (var i = 0; i < 12; i++)
        {
            var angle = (float)i / 12f * MathF.Tau + t * 1.8f;
            var px = cx + MathF.Cos(angle) * 62f;
            var py = cy + MathF.Sin(angle) * 62f;
            drawList.AddCircleFilled(new UiVector2(px, py), 3f, new UiColor(0xFF60D0FF), white, canvas, 10);
        }
    }

    private void DrawStatsWindow(UiImmediateContext ui, UiRect bounds, double delta)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 12f, bounds.Y + bounds.Height - 196f));
        ui.SetNextWindowSize(new UiVector2(640f, 184f));
        ui.BeginWindow("Global Dirty Bench Stats");

        var benchMode = !string.IsNullOrWhiteSpace(_benchOutputPath);
        ui.TextV("Mode: {0}", benchMode ? "Auto Benchmark" : "Manual Toggle");
        if (benchMode)
        {
            ui.TextV("Phase: {0}", _phaseIndex == 0 ? "all-dynamic" : "global-static-cache");
            ui.TextV("Phase Elapsed: {0:0.00}/{1:0.00} sec", _phaseElapsed, _phaseSeconds);
        }
        else
        {
            ui.Checkbox("Use Global Static Cache", ref _manualUseGlobalStaticCache);
        }

        ui.TextV("FPS: {0:0.0}", _liveFps);
        ui.TextV("Tiles: {0}x{1}", _tileColumns, _tileRows);
        ui.TextV("Density: {0}", _density);
        ui.TextV("Frame dt: {0:0.000} ms", delta * 1000.0);
        ui.TextV("CPU: {0:0.0}%", _cpuPercent);

        if (_hasDynamicAvgFps)
        {
            ui.TextV("Avg(all-dynamic): {0:0.###} FPS", _dynamicAvgFps);
        }
        if (_hasCacheAvgFps)
        {
            ui.TextV("Avg(global-static-cache): {0:0.###} FPS", _cacheAvgFps);
        }
        if (_hasDynamicAvgFps && _hasCacheAvgFps && _dynamicAvgFps > 0d)
        {
            var improvement = (_cacheAvgFps - _dynamicAvgFps) / _dynamicAvgFps * 100.0;
            ui.TextV("Improvement: {0:+0.###;-0.###;0}%", improvement);
        }

        ui.EndWindow();
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

        if (_phaseIndex == 0)
        {
            _dynamicAvgFps = avgFps;
            _hasDynamicAvgFps = true;
        }
        else if (_phaseIndex == 1)
        {
            _cacheAvgFps = avgFps;
            _hasCacheAvgFps = true;
        }

        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"strategy\":\"{1}\",\"tiles\":\"{2}x{3}\",\"density\":{4},\"avgFps\":{5:0.###},\"avgCpu\":{6:0.###},\"samples\":{7}}}",
            _phaseIndex,
            _phaseIndex == 0 ? "all-dynamic" : "global-static-cache",
            _tileColumns,
            _tileRows,
            _density,
            avgFps,
            avgCpu,
            _samples));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _fpsSum = 0d;
        _cpuSum = 0d;
        _samples = 0;
        if (_phaseIndex == 1)
        {
            ReleaseBackgroundCache();
        }

        if (_phaseIndex > 1)
        {
            WriteResults();
            ReleaseBackgroundCache();
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
        return BenchOptions.ReadDouble("DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS", 2d, minExclusive: 0.1d);
    }

    private static int ReadDensity()
    {
        return BenchOptions.ReadInt("DUXEL_GLOBAL_DIRTY_BENCH_DENSITY", 9600, minInclusive: 400, maxInclusive: 32000);
    }

    private static int ReadTileColumns()
    {
        return BenchOptions.ReadInt("DUXEL_GLOBAL_DIRTY_BENCH_COLS", 8, minInclusive: 2, maxInclusive: 16);
    }

    private static int ReadTileRows()
    {
        return BenchOptions.ReadInt("DUXEL_GLOBAL_DIRTY_BENCH_ROWS", 6, minInclusive: 2, maxInclusive: 12);
    }
}
