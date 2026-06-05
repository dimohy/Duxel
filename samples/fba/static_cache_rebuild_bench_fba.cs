// FBA: focused static cache rebuild benchmark
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

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Static Cache Rebuild Bench (FBA)",
        Width = 1280,
        Height = 760,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new StaticCacheRebuildBenchScreen()
});

public sealed class StaticCacheRebuildBenchScreen : UiScreen
{
    private enum BenchPhase
    {
        Dynamic,
        SteadyCache,
        SingleRebuild,
        AllRebuild,
        SingleMutatingRebuild,
        AllMutatingRebuild,
    }

    private enum PrimitiveMode
    {
        Circles,
        Rects,
        Mixed,
    }

    private readonly Process _process = Process.GetCurrentProcess();
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT");
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS", 0.8d, minExclusive: 0.1d);
    private readonly double _warmupSeconds = BenchOptions.ReadDouble("DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS", 0.25d, minExclusive: 0d);
    private readonly int _layerCount = BenchOptions.ReadInt("DUXEL_STATIC_CACHE_REBUILD_LAYERS", 18, minInclusive: 1, maxInclusive: 96);
    private readonly int _densityPerLayer = BenchOptions.ReadInt("DUXEL_STATIC_CACHE_REBUILD_DENSITY", 900, minInclusive: 50, maxInclusive: 12000);
    private readonly int _gpuOverdraw = BenchOptions.ReadInt("DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW", 0, minInclusive: 0, maxInclusive: 1024);
    private readonly PrimitiveMode _primitiveMode = ReadPrimitiveMode();
    private readonly int _circleSegments = BenchOptions.ReadInt("DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS", 8, minInclusive: 3, maxInclusive: 64);
    private readonly int[] _layerRevisions;
    private readonly List<string> _records = [];

    private int _phaseIndex;
    private BenchPhase _lastPhase = (BenchPhase)(-1);
    private int _singleDirtyCursor;
    private double _phaseElapsed;
    private double _fpsSum;
    private double _cpuSum;
    private long _allocatedBytesSum;
    private int _samples;
    private int _measuredFrames;
    private int _measuredBodyDrawCount;
    private int _measuredCacheBuildCount;
    private double _lastTime;
    private float _liveFps;
    private double _cpuSampleTime;
    private TimeSpan _cpuSampleProcessTime;
    private float _cpuPercent;
    private UiRect _lastCanvas;
    private bool _hasLastCanvas;
    private bool _measureThisFrame;

    public StaticCacheRebuildBenchScreen()
    {
        _layerRevisions = new int[_layerCount];
        _cpuSampleTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        _cpuSampleProcessTime = _process.TotalProcessorTime;
    }

    public override void Render(UiImmediateContext ui)
    {
        var frameAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0.0001d, 0.05d);
        _lastTime = now;
        _liveFps = (float)(1d / delta);

        UpdateCpu(now);

        var phase = CurrentPhase;
        if (phase != _lastPhase)
        {
            OnPhaseChanged(ui, phase);
        }

        ApplyDirtyStrategy(ui, phase);
        _measureThisFrame = ShouldMeasure;

        DrawScene(ui, phase);
        var frameAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - frameAllocatedBefore;
        TickBenchmark(delta, phase, frameAllocatedBytes);

        DuxelApp.RequestFrame();
    }

    private BenchPhase CurrentPhase => _phaseIndex switch
    {
        0 => BenchPhase.Dynamic,
        1 => BenchPhase.SteadyCache,
        2 => BenchPhase.SingleRebuild,
        3 => BenchPhase.AllRebuild,
        4 => BenchPhase.SingleMutatingRebuild,
        _ => BenchPhase.AllMutatingRebuild,
    };

    private bool ShouldMeasure => !string.IsNullOrWhiteSpace(_benchOutputPath) && _phaseElapsed >= _warmupSeconds;

    private void OnPhaseChanged(UiImmediateContext ui, BenchPhase phase)
    {
        _lastPhase = phase;
        _singleDirtyCursor = 0;
        _phaseElapsed = 0d;
        _fpsSum = 0d;
        _cpuSum = 0d;
        _allocatedBytesSum = 0;
        _samples = 0;
        _measuredFrames = 0;
        _measuredBodyDrawCount = 0;
        _measuredCacheBuildCount = 0;

        if (phase != BenchPhase.Dynamic)
        {
            ui.MarkAllLayersDirty();
        }
    }

    private void ApplyDirtyStrategy(UiImmediateContext ui, BenchPhase phase)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        if (phase == BenchPhase.SingleRebuild)
        {
            ui.MarkLayerDirty(GetLayerId(_singleDirtyCursor));
            _singleDirtyCursor = (_singleDirtyCursor + 1) % _layerCount;
        }
        else if (phase == BenchPhase.AllRebuild)
        {
            ui.MarkAllLayersDirty();
        }
        else if (phase == BenchPhase.SingleMutatingRebuild)
        {
            unchecked
            {
                _layerRevisions[_singleDirtyCursor]++;
            }

            ui.MarkLayerDirty(GetLayerId(_singleDirtyCursor));
            _singleDirtyCursor = (_singleDirtyCursor + 1) % _layerCount;
        }
        else if (phase == BenchPhase.AllMutatingRebuild)
        {
            for (var i = 0; i < _layerRevisions.Length; i++)
            {
                unchecked
                {
                    _layerRevisions[i]++;
                }
            }

            ui.MarkAllLayersDirty();
        }
    }

    private void DrawScene(UiImmediateContext ui, BenchPhase phase)
    {
        ui.SetNextWindowPos(new UiVector2(18f, 18f));
        ui.SetNextWindowSize(new UiVector2(1240f, 700f));
        ui.BeginWindow("Static Cache Rebuild");

        ui.TextV("Phase: {0}", GetPhaseName(phase));
        ui.TextV("FPS: {0:0.0}", _liveFps);
        ui.TextV("Layers: {0}", _layerCount);
        ui.TextV("Density / Layer: {0}", _densityPerLayer);
        ui.TextV("Primitive Mode: {0}", GetPrimitiveModeName(_primitiveMode));
        ui.TextV("Circle Segments: {0}", _circleSegments);
        ui.TextV("GPU Overdraw: {0}", _gpuOverdraw);
        ui.TextV("Warmup: {0:0.00}s", _warmupSeconds);

        var origin = ui.GetCursorScreenPos();
        var avail = ui.GetContentRegionAvail();
        var canvas = new UiRect(origin.X, origin.Y + 8f, MathF.Max(1f, avail.X), MathF.Max(1f, avail.Y - 8f));
        if (!_hasLastCanvas || !NearlySame(canvas, _lastCanvas))
        {
            ui.MarkAllLayersDirty();
            _lastCanvas = canvas;
            _hasLastCanvas = true;
        }

        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        drawList.PushTexture(white);
        drawList.PushClipRect(canvas);
        drawList.AddRectFilled(canvas, new UiColor(0xFF111419), white, canvas);

        var columns = ComputeColumnCount(_layerCount);
        var rows = (int)MathF.Ceiling(_layerCount / (float)columns);
        const float gap = 8f;
        var panelWidth = MathF.Max(64f, (canvas.Width - gap * (columns + 1)) / columns);
        var panelHeight = MathF.Max(72f, (canvas.Height - gap * (rows + 1)) / rows);

        for (var i = 0; i < _layerCount; i++)
        {
            DrawLayer(ui, drawList, canvas, phase, i, columns, gap, panelWidth, panelHeight, white);
        }

        DrawGpuOverdraw(drawList, canvas, white);

        drawList.PopClipRect();
        drawList.PopTexture();

        ui.SetCursorScreenPos(new UiVector2(origin.X, origin.Y));
        ui.Dummy(new UiVector2(canvas.Width, canvas.Height + 8f));
        ui.EndWindow();
    }

    private void DrawLayer(
        UiImmediateContext ui,
        UiDrawListBuilder drawList,
        UiRect canvas,
        BenchPhase phase,
        int index,
        int columns,
        float gap,
        float panelWidth,
        float panelHeight,
        UiTextureId white)
    {
        var col = index % columns;
        var row = index / columns;
        var panel = new UiRect(
            canvas.X + gap + col * (panelWidth + gap),
            canvas.Y + gap + row * (panelHeight + gap),
            panelWidth,
            panelHeight);
        var header = new UiRect(panel.X, panel.Y, panel.Width, 16f);
        var body = new UiRect(panel.X + 2f, panel.Y + 18f, MathF.Max(1f, panel.Width - 4f), MathF.Max(1f, panel.Height - 20f));
        var hue = (uint)((index * 47) & 0xFF);

        drawList.AddRectFilled(panel, new UiColor(0xFF20252D), white, canvas);
        drawList.AddRectFilled(header, new UiColor(0xFF000000 | (hue << 16) | ((255u - hue) << 8) | 0x80u), white, canvas);
        drawList.AddRect(panel, new UiColor(0xFF3E4652), 0f, 1f);

        var staticCache = phase != BenchPhase.Dynamic;
        var options = new UiLayerOptions(
            StaticCache: staticCache,
            Opacity: 1f,
            Translation: new UiVector2(body.X, body.Y));

        var shouldDraw = ui.BeginLayer(GetLayerId(index), options);
        if (shouldDraw)
        {
            DrawHeavyLayerBody(
                ui.GetWindowDrawList(),
                new UiRect(0f, 0f, body.Width, body.Height),
                index,
                _layerRevisions[index],
                _densityPerLayer,
                _primitiveMode,
                _circleSegments,
                white);
            if (_measureThisFrame)
            {
                _measuredBodyDrawCount++;
                if (staticCache)
                {
                    _measuredCacheBuildCount++;
                }
            }
        }
        ui.EndLayer();
    }

    private void DrawGpuOverdraw(UiDrawListBuilder drawList, UiRect canvas, UiTextureId white)
    {
        for (var i = 0; i < _gpuOverdraw; i++)
        {
            var seed = HashUInt((uint)(i * 2654435761u));
            var color = new UiColor(0x08000000u
                | ((seed & 0x7Fu) << 16)
                | (((seed >> 8) & 0x7Fu) << 8)
                | ((seed >> 16) & 0x7Fu));
            drawList.AddRectFilled(canvas, color, white, canvas);
        }
    }

    private static void DrawHeavyLayerBody(
        UiDrawListBuilder drawList,
        UiRect rect,
        int layerIndex,
        int revision,
        int density,
        PrimitiveMode primitiveMode,
        int circleSegments,
        UiTextureId white)
    {
        var revisionShade = (uint)((revision * 31) & 0x3Fu);
        drawList.AddRectFilled(rect, new UiColor(0xCC181C22u + revisionShade), white, rect);
        var inner = new UiRect(rect.X + 3f, rect.Y + 3f, MathF.Max(1f, rect.Width - 6f), MathF.Max(1f, rect.Height - 6f));
        drawList.AddRectFilled(inner, new UiColor(0x552A3340), white, rect);

        for (var i = 0; i < density; i++)
        {
            var seed = HashUInt((uint)((layerIndex + 1) * 73856093) ^ (uint)(revision * 83492791) ^ (uint)(i * 19349663));
            var px = inner.X + HashToUnit(seed) * inner.Width;
            var py = inner.Y + HashToUnit(HashUInt(seed ^ 0x9E3779B9u)) * inner.Height;
            var radius = 0.55f + HashToUnit(HashUInt(seed ^ 0x85EBCA6Bu)) * 1.15f;
            var shade = HashUInt(seed ^ 0xC2B2AE35u) & 0xFFu;
            var color = new UiColor(0xFF000000 | (shade << 16) | ((255u - shade) << 8) | (96u + (shade & 0x7Fu)));
            if (ShouldDrawRectPrimitive(primitiveMode, i))
            {
                var halfWidth = radius * (1.35f + HashToUnit(HashUInt(seed ^ 0x27D4EB2Fu)) * 0.9f);
                var halfHeight = radius * (1.05f + HashToUnit(HashUInt(seed ^ 0x165667B1u)) * 0.7f);
                var primitiveRect = new UiRect(px - halfWidth, py - halfHeight, halfWidth * 2f, halfHeight * 2f);
                drawList.AddRectFilled(primitiveRect, color, white, rect);
            }
            else
            {
                drawList.AddCircleFilled(new UiVector2(px, py), radius, color, white, rect, circleSegments);
            }
        }
    }

    private void TickBenchmark(double delta, BenchPhase phase, long frameAllocatedBytes)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        var measuring = ShouldMeasure;
        if (measuring)
        {
            _fpsSum += 1d / delta;
            _cpuSum += _cpuPercent;
            _allocatedBytesSum += frameAllocatedBytes;
            _samples++;
            _measuredFrames++;
        }

        _phaseElapsed += delta;
        if (_phaseElapsed < _warmupSeconds + _phaseSeconds)
        {
            return;
        }

        var avgFps = _samples > 0 ? _fpsSum / _samples : 0d;
        var avgCpu = _samples > 0 ? _cpuSum / _samples : 0d;
        var avgAllocatedBytes = _samples > 0 ? _allocatedBytesSum / _samples : 0L;
        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"name\":\"{1}\",\"layers\":{2},\"density\":{3},\"avgFps\":{4:0.###},\"avgCpu\":{5:0.###},\"avgAllocatedBytes\":{6},\"frames\":{7},\"bodyDraws\":{8},\"cacheBuilds\":{9},\"samples\":{10}}}",
            _phaseIndex,
            GetPhaseName(phase),
            _layerCount,
            _densityPerLayer,
            avgFps,
            avgCpu,
            avgAllocatedBytes,
            _measuredFrames,
            _measuredBodyDrawCount,
            _measuredCacheBuildCount,
            _samples));

        _phaseIndex++;
        if (_phaseIndex > 5)
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
        sb.Append(_phaseSeconds.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"warmupSeconds\":");
        sb.Append(_warmupSeconds.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"gpuOverdraw\":");
        sb.Append(_gpuOverdraw.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"primitiveMode\":\"");
        sb.Append(GetPrimitiveModeName(_primitiveMode));
        sb.Append('"');
        sb.Append(",\"circleSegments\":");
        sb.Append(_circleSegments.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"records\":[");
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
        _cpuPercent = (float)Math.Clamp(cpuDelta / (elapsed * cores) * 100d, 0d, 100d);

        _cpuSampleTime = now;
        _cpuSampleProcessTime = processTime;
    }

    private static int ComputeColumnCount(int layerCount)
    {
        return Math.Clamp((int)MathF.Ceiling(MathF.Sqrt(layerCount * 1.6f)), 1, Math.Max(1, layerCount));
    }

    private static string GetLayerId(int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"static_cache_rebuild_layer_{index}");
    }

    private static string GetPhaseName(BenchPhase phase) => phase switch
    {
        BenchPhase.Dynamic => "dynamic",
        BenchPhase.SteadyCache => "steady-cache",
        BenchPhase.SingleRebuild => "single-rebuild",
        BenchPhase.AllRebuild => "all-rebuild",
        BenchPhase.SingleMutatingRebuild => "single-mutating-rebuild",
        _ => "all-mutating-rebuild",
    };

    private static PrimitiveMode ReadPrimitiveMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE");
        if (string.Equals(raw, "rects", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "rect", StringComparison.OrdinalIgnoreCase))
        {
            return PrimitiveMode.Rects;
        }

        if (string.Equals(raw, "mixed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "mix", StringComparison.OrdinalIgnoreCase))
        {
            return PrimitiveMode.Mixed;
        }

        return PrimitiveMode.Circles;
    }

    private static string GetPrimitiveModeName(PrimitiveMode mode) => mode switch
    {
        PrimitiveMode.Rects => "rects",
        PrimitiveMode.Mixed => "mixed",
        _ => "circles",
    };

    private static bool ShouldDrawRectPrimitive(PrimitiveMode mode, int index)
    {
        return mode == PrimitiveMode.Rects
            || (mode == PrimitiveMode.Mixed && (index & 1) == 0);
    }

    private static bool NearlySame(UiRect a, UiRect b)
    {
        return MathF.Abs(a.X - b.X) < 0.5f
            && MathF.Abs(a.Y - b.Y) < 0.5f
            && MathF.Abs(a.Width - b.Width) < 0.5f
            && MathF.Abs(a.Height - b.Height) < 0.5f;
    }

    private static uint HashUInt(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }

    private static float HashToUnit(uint value)
    {
        return (value & 0x00FFFFFFu) / 16777215f;
    }
}
