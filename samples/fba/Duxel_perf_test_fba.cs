// FBA: 대량 폴리곤 물리 시뮬레이션 성능 벤치마크 — DrawList 프리미티브 렌더링 부하 테스트
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.Json;
using System.Numerics;
using Duxel.Core.Simulation;
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

var profile = ReadStartupProfile();

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Performance Test (FBA)",
        Width = BenchOptions.ReadInt("DUXEL_PERF_WINDOW_WIDTH", 1920, minInclusive: 640),
        Height = BenchOptions.ReadInt("DUXEL_PERF_WINDOW_HEIGHT", 1080, minInclusive: 480),
        VSync = false
    },
    Renderer = new DuxelRendererOptions
    {
        Profile = profile
    },

    Frame = new()
    {
        EnableRenderPipeline = BenchOptions.ReadBool("DUXEL_PERF_PIPELINE", true),
        UseEventDrivenPlatformLoop = true,
    },
    Screen = new PerfTestScreen(profile)
});

static DuxelPerformanceProfile ReadStartupProfile()
{
    if (TryParseProfile(Environment.GetEnvironmentVariable("DUXEL_PERF_PROFILE"), out var profile))
    {
        return profile;
    }

    if (TryParseProfile(Environment.GetEnvironmentVariable("DUXEL_APP_PROFILE"), out profile))
    {
        return profile;
    }

    return DuxelPerformanceProfile.Render;
}

static bool TryParseProfile(string? raw, out DuxelPerformanceProfile profile)
{
    profile = DuxelPerformanceProfile.Display;
    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    var value = raw.Trim();
    if (string.Equals(value, "render", StringComparison.OrdinalIgnoreCase))
    {
        profile = DuxelPerformanceProfile.Render;
        return true;
    }

    if (string.Equals(value, "display", StringComparison.OrdinalIgnoreCase))
    {
        profile = DuxelPerformanceProfile.Display;
        return true;
    }

    return false;
}

public sealed class PerfTestScreen : UiScreen
{
    private const int RenderProfileMsaaSamples = 1;
    private const int DisplayProfileMsaaSamples = 4;
    private const float CollisionHitDecayPerSecond = 2.4f;
    private const float CollisionHitShrinkMax = 0.22f;
    private const float CollisionHitRedWeight = 0.28f;
    private const float CollisionAngularDampingPerSecond = 0.7f;

    private readonly CircleCollisionSolver2D _collisionSolver = new();
    private readonly List<PolygonBody> _polygons = [];
    private readonly bool _gpuSimulation = ReadGpuSimulation();
    private readonly bool _cpuJacobiSimulation = BenchOptions.ReadString("DUXEL_PERF_SIMULATION", "gpu") == "cpu-jacobi";
    private ParticleJacobiSimulation? _jacobiSimulation;
    private UiGpuParticleSystem? _gpuParticles;
    private long _gpuResetRevision, _gpuLoadedRevision = -1;
    private readonly int _gpuCollisionIterations = BenchOptions.ReadInt("DUXEL_PERF_GPU_ITERATIONS", 2, minInclusive: 1);
    private double _gpuDispatchSum, _gpuInitialUploadSum;
    private UiRegularPolygon[] _polygonInstances = [];
    private bool _dynamicCanvas = BenchOptions.ReadBool("DUXEL_PERF_DYNAMIC_CANVAS", true);
    private UiRect _canvasBounds;
    private long _canvasRevision;
    private (UiRect Bounds, float Size, int Sides, int Count, bool Cache) _canvasState;
    private readonly Random _random = new(1337);
    private float _baseSpeed = 160f;
    private float _baseSize = (float)BenchOptions.ReadDouble("DUXEL_PERF_INITIAL_SIZE", 6d, minExclusive: 0d);
    private int _baseSides = 5;
    private float _rotationSpeed = 1.8f;
    private bool _paused = BenchOptions.ReadBool("DUXEL_PERF_PAUSED", false);
    private double _lastTime;
    private readonly UiFpsCounter _fpsCounter = new(0.5d);
    private float _fps;
    private readonly double _benchDurationSeconds = ReadBenchDurationSeconds();
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_PERF_BENCH_OUT");
    private readonly int _initialPolygons = ReadInitialPolygonCount();
    private DuxelPerformanceProfile _activeProfile;
    private bool _initialized;
    private double _benchElapsedSeconds;
    private readonly double _benchWarmupSeconds = BenchOptions.ReadDouble("DUXEL_PERF_WARMUP_SECONDS", 1d, minExclusive: 0d);
    private readonly BenchFrameRecorder _benchFrames = new(262144);
    private long _physicsTicks;
    private long _collisionTicks;
    private long _geometryTicks;
    private double _physicsMsSum, _collisionMsSum, _geometryMsSum, _gpuUsSum, _drawCallsSum, _uploadBytesSum;
    private double _stagingUploadSum, _layerStateUploadSum, _compiledIndexUploadSum;
    private int _gpuTimingSamples, _rendererAllocationSamples;
    private long _lastUiAllocated;
    private double _uiAllocatedSum, _rendererAllocatedSum;
    private bool _benchCompleted;
    private bool _enableGlobalStaticCache = ReadGlobalStaticCacheEnabled();
    private UiRect _backdropBounds;
    private UiTextureId _backdropTexture;
    private long _backdropRevision;
    private bool _backdropWasCached;

    public PerfTestScreen(DuxelPerformanceProfile startupProfile)
    {
        _activeProfile = startupProfile;
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var rawDelta = _lastTime == 0 ? 0 : now - _lastTime;
        var delta = _lastTime == 0 ? 0.016 : Math.Clamp(rawDelta, 0.0, 0.05);
        _lastTime = now;
        if (TickBenchMode(ui, rawDelta)) return;
        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        UpdateFps(delta);
        var renderBounds = DrawRenderWindow(ui, bounds, delta);
        DrawControls(ui, bounds, renderBounds);
        DrawRendererStatusOverlay(ui);
        _lastUiAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedStart;

        DuxelApp.RequestFrame();
    }

    private static double ReadBenchDurationSeconds()
    {
        return BenchOptions.ReadDouble("DUXEL_PERF_BENCH_SECONDS", 0d, minExclusive: 0d);
    }

    private static int ReadInitialPolygonCount()
    {
        return BenchOptions.ReadInt("DUXEL_PERF_INITIAL_POLYGONS", 5000, minInclusive: 1);
    }

    private static bool ReadGpuSimulation() => BenchOptions.ReadString("DUXEL_PERF_SIMULATION", "gpu") switch
    {
        "cpu" => false,
        "cpu-jacobi" => false,
        "gpu" => true,
        _ => throw new ArgumentException("DUXEL_PERF_SIMULATION must be cpu, cpu-jacobi or gpu."),
    };

    private static bool ReadGlobalStaticCacheEnabled()
    {
        return BenchOptions.ReadBool("DUXEL_PERF_GLOBAL_STATIC_CACHE", defaultValue: true);
    }

    private void EnsureInitialized(UiRect bounds)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_initialPolygons <= 0)
        {
            return;
        }

        EnsurePolygonCapacity(_initialPolygons);

        for (var i = 0; i < _initialPolygons; i++)
        {
            AddPolygon(bounds);
        }
    }

    private bool TickBenchMode(UiImmediateContext ui, double delta)
    {
        if (_benchCompleted || _benchDurationSeconds <= 0d)
        {
            return false;
        }

        _benchElapsedSeconds += delta;
        if (_benchElapsedSeconds <= _benchWarmupSeconds)
        {
            return false;
        }
        _benchFrames.Record(delta);
        var tickMs = 1000d / Stopwatch.Frequency;
        _physicsMsSum += _physicsTicks * tickMs;
        _collisionMsSum += _collisionTicks * tickMs;
        _geometryMsSum += _geometryTicks * tickMs;
        var metrics = ui.GetRendererFrameMetrics();
        if (double.IsFinite(metrics.GpuRenderMicroseconds))
        {
            _gpuUsSum += metrics.GpuRenderMicroseconds;
            _gpuTimingSamples++;
        }
        _drawCallsSum += metrics.DrawCalls;
        _uploadBytesSum += metrics.DynamicUploadBytes;
        _stagingUploadSum += metrics.StagingUploadBytes;
        _layerStateUploadSum += metrics.LayerStateUploadBytes;
        _compiledIndexUploadSum += metrics.CompiledIndexUploadBytes;
        _uiAllocatedSum += _lastUiAllocated;
        if (metrics.RendererAllocatedBytes is { } rendererAllocated)
        {
            _rendererAllocatedSum += rendererAllocated;
            _rendererAllocationSamples++;
        }
        _gpuDispatchSum += metrics.GpuParticleDispatches;
        _gpuInitialUploadSum += metrics.GpuParticleInitialUploadBytes;
        if (_benchFrames.MeasuredSeconds < _benchDurationSeconds)
        {
            return false;
        }

        _benchCompleted = true;
        var stats = _benchFrames.Calculate();
        var vsync = ui.GetVSync();
        var msaaSamples = ui.GetMsaaSamples();

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            using var stream = File.Create(_benchOutputPath);
            using var json = new Utf8JsonWriter(stream, new() { Indented = true });
            json.WriteStartObject();
            json.WriteString("clock", "ui-frame-start-monotonic-unclamped");
            json.WriteString("runtime", RuntimeInformation.FrameworkDescription);
            json.WriteString("architecture", RuntimeInformation.ProcessArchitecture.ToString());
            json.WriteBoolean("renderPipeline", BenchOptions.ReadBool("DUXEL_PERF_PIPELINE", true));
            json.WriteNumber("avgFps", stats.AverageFps);
            json.WriteNumber("samples", stats.Samples);
            json.WriteNumber("elapsedSeconds", stats.MeasuredSeconds);
            json.WriteNumber("warmupSeconds", _benchWarmupSeconds);
            json.WriteNumber("medianFrameMs", stats.MedianFrameMs);
            json.WriteNumber("p95FrameMs", stats.P95FrameMs);
            json.WriteNumber("p99FrameMs", stats.P99FrameMs);
            json.WriteNumber("low1PctFps", stats.Low1PctFps);
            json.WriteNumber("physicsMs", _physicsMsSum / stats.Samples);
            json.WriteNumber("collisionMs", _collisionMsSum / stats.Samples);
            json.WriteNumber("geometryMs", _geometryMsSum / stats.Samples);
            if (_gpuTimingSamples > 0) json.WriteNumber("gpuUs", _gpuUsSum / _gpuTimingSamples);
            else json.WriteNull("gpuUs");
            json.WriteNumber("gpuTimingSamples", _gpuTimingSamples);
            json.WriteNumber("drawCalls", _drawCallsSum / stats.Samples);
            json.WriteNumber("dynamicUploadBytes", _uploadBytesSum / stats.Samples);
            json.WriteNumber("stagingUploadBytes", _stagingUploadSum / stats.Samples);
            json.WriteNumber("layerStateUploadBytes", _layerStateUploadSum / stats.Samples);
            json.WriteNumber("compiledIndexUploadBytes", _compiledIndexUploadSum / stats.Samples);
            json.WriteBoolean("profilingEnabled", metrics.ProfilingEnabled);
            json.WriteNumber("uiAllocatedBytesPerFrame", _uiAllocatedSum / stats.Samples);
            if (_rendererAllocationSamples > 0) json.WriteNumber("rendererAllocatedBytesPerFrame", _rendererAllocatedSum / _rendererAllocationSamples);
            else json.WriteNull("rendererAllocatedBytesPerFrame");
            json.WriteNumber("rendererAllocationSamples", _rendererAllocationSamples);
            json.WriteNumber("framebufferWidth", metrics.FramebufferSize.Width);
            json.WriteNumber("framebufferHeight", metrics.FramebufferSize.Height);
            json.WriteString("simulation", _gpuSimulation ? "gpu-jacobi" : _cpuJacobiSimulation ? "cpu-jacobi" : "cpu-ordered");
            json.WriteNumber("collisionIterations", _gpuSimulation || _cpuJacobiSimulation ? _gpuCollisionIterations : 1);
            json.WriteBoolean("collisionTimingIncludesIntegration", _cpuJacobiSimulation);
            json.WriteNumber("gpuParticleDispatches", _gpuDispatchSum / stats.Samples);
            json.WriteNumber("gpuParticleInitialUploadBytes", _gpuInitialUploadSum / stats.Samples);
            json.WriteBoolean("vsync", vsync);
            json.WriteNumber("msaa", msaaSamples);
            json.WriteNumber("count", _polygons.Count);
            json.WriteNumber("size", _baseSize);
            json.WriteNumber("viewportWidth", ui.GetMainViewport().Size.X);
            json.WriteNumber("viewportHeight", ui.GetMainViewport().Size.Y);
            json.WriteNumber("canvasWidth", _canvasBounds.Width);
            json.WriteNumber("canvasHeight", _canvasBounds.Height);
            json.WriteBoolean("dynamicCanvas", _dynamicCanvas);
            json.WriteBoolean("globalStaticCache", _enableGlobalStaticCache);
            json.WriteString("profile", GetProfileName(_activeProfile));
            json.WriteEndObject();
            using var frames = new StreamWriter(_benchOutputPath + ".frames.csv");
            frames.WriteLine("frameSeconds");
            foreach (var seconds in _benchFrames.FrameSeconds) frames.WriteLine(seconds.ToString("R", CultureInfo.InvariantCulture));
        }
        DuxelApp.Exit();
        return true;
    }

    private UiRect DrawRenderWindow(UiImmediateContext ui, UiRect viewportBounds, double delta)
    {
        const float controlsWidth = 320f;
        const float margin = 16f;
        var renderX = viewportBounds.X + controlsWidth + margin * 2f;
        var renderY = viewportBounds.Y + margin;
        var renderWidth = MathF.Max(240f, viewportBounds.Width - controlsWidth - margin * 3f);
        var renderHeight = MathF.Max(240f, viewportBounds.Height - margin * 2f);

        ui.SetNextWindowPos(new UiVector2(renderX, renderY));
        ui.SetNextWindowSize(new UiVector2(renderWidth, renderHeight));
        ui.BeginPanel("Render");

        var contentPos = ui.GetCursorScreenPos();
        var contentAvail = ui.GetContentRegionAvail();
        var bounds = new UiRect(
            contentPos.X,
            contentPos.Y,
            MathF.Max(1f, contentAvail.X),
            MathF.Max(1f, contentAvail.Y)
        );

        EnsureInitialized(bounds);
        _canvasBounds = bounds;
        var start = Stopwatch.GetTimestamp();
        UpdatePolygons(delta, bounds);
        _physicsTicks = Stopwatch.GetTimestamp() - start;
        start = Stopwatch.GetTimestamp();
        DrawScene(ui, bounds, delta);
        _geometryTicks = Stopwatch.GetTimestamp() - start;

        ui.EndPanel();
        return bounds;
    }

    private void DrawScene(UiImmediateContext ui, UiRect bounds, double delta)
    {
        var state = (bounds, _baseSize, _baseSides, _polygons.Count, _enableGlobalStaticCache);
        if (_canvasState != state) { _canvasState = state; _canvasRevision++; }
        if (_dynamicCanvas && !ui.BeginLayer("perf.canvas", new UiLayerOptions(false, 1, default)
            { Scene3D = new UiLayer3D(Matrix4x4.Identity, WriteDepth: false) { Viewport = bounds }, ContentVersion = _paused ? _canvasRevision : null }))
        {
            ui.EndLayer();
            return;
        }
        var drawList = ui.GetWindowDrawList();
        var clip = bounds;
        var whiteTexture = ui.WhiteTextureId;

        drawList.PushTexture(whiteTexture);
        drawList.PushClipRect(clip);

        if (_enableGlobalStaticCache)
        {
            if (_backdropBounds != clip || _backdropTexture != whiteTexture)
            {
                _backdropBounds = clip; _backdropTexture = whiteTexture; _backdropRevision++;
            }
            if (ui.BeginLayer("perf.backdrop", new UiLayerOptions(true,1,default) { ContentVersion = _backdropRevision }))
                DrawStaticBackdrop(ui.GetWindowDrawList(), clip, whiteTexture);
            ui.EndLayer();
            _backdropWasCached = true;
        }
        else
        {
            if (_backdropWasCached) { ui.RemoveLayer("perf.backdrop"); _backdropWasCached = false; }
            DrawStaticBackdrop(drawList, clip, whiteTexture);
        }

        var polygons = CollectionsMarshal.AsSpan(_polygons);
        if (_gpuSimulation)
        {
            EnsureGpuParticles(polygons);
            if (_gpuParticles is not null)
                drawList.AddGpuParticles(new(_gpuParticles,
                    new(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, bounds.Height - 16),
                    _paused ? 0 : (float)delta, _baseSize, _baseSpeed, _baseSides,
                    MathF.Max(.35f, _rotationSpeed * 2.5f), _gpuCollisionIterations));
        }
        else
        {
        if (_polygonInstances.Length < polygons.Length) _polygonInstances = new UiRegularPolygon[polygons.Length];
        for (var i = 0; i < polygons.Length; i++)
        {
            ref var body = ref polygons[i];
            var hit = body.HitResponse;
            var radiusScale = 1f - hit * CollisionHitShrinkMax;
            var radius = _baseSize * body.SizeScale * radiusScale;
            var center = body.Position;
            var sides = Math.Max(3, _baseSides + body.SideOffset);
            _polygonInstances[i] = new(center, radius, sides, body.Rotation, BlendHitColor(body.BaseColor, hit));
        }
        drawList.AddRegularPolygons(_polygonInstances.AsSpan(0, polygons.Length));
        }

        drawList.PopClipRect();
        drawList.PopTexture();
        if (_dynamicCanvas) ui.EndLayer();
    }

    private void EnsureGpuParticles(ReadOnlySpan<PolygonBody> bodies)
    {
        if (_gpuLoadedRevision != _gpuResetRevision || bodies.IsEmpty)
        {
            _gpuParticles?.Dispose(); _gpuParticles = null; _jacobiSimulation = null;
            _gpuLoadedRevision = _gpuResetRevision;
        }
        if (bodies.IsEmpty) return;
        if (_gpuParticles is not null && _gpuParticles.Count > bodies.Length)
            _gpuParticles.Truncate(bodies.Length);
        var preserved = _gpuParticles?.Count ?? 0;
        if (preserved == bodies.Length) return;
        var initial = new UiGpuParticle[bodies.Length - preserved];
        for (var i = preserved; i < bodies.Length; i++)
        {
            ref readonly var body = ref bodies[i];
            initial[i - preserved] = new() { Position = body.Position, Direction = body.Direction, Rotation = body.Rotation,
                AngularSpeed = (float)body.AngularSpeed, HitResponse = body.HitResponse,
                SpeedScale = body.SpeedScale, SizeScale = body.SizeScale, SideOffset = body.SideOffset, Color = body.BaseColor.Rgba };
        }
        if (_gpuParticles is null) _gpuParticles = new(initial, 12);
        else _gpuParticles.Append(initial);
    }

    private void DrawControls(UiImmediateContext ui, UiRect viewportBounds, UiRect renderBounds)
    {
        const float margin = 16f;
        ui.SetNextWindowPos(new UiVector2(viewportBounds.X + margin, viewportBounds.Y + margin));
        ui.SetNextWindowSize(new UiVector2(320f, MathF.Max(320f, viewportBounds.Height - margin * 2f)));
        ui.BeginPanel("Controls");
        if (ui.IsWindowAppearing())
        {
            ui.SetScrollY(0f);
        }

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Count: {0}", _polygons.Count);
        ui.Text(_gpuSimulation ? "Simulation: GPU (parallel)" : _cpuJacobiSimulation ? "Simulation: CPU (parallel model)" : "Simulation: CPU (ordered)");
        if (_gpuSimulation) ui.Text("Add/Remove preserves existing motion.");
        ui.Checkbox("Dynamic Canvas Layer", ref _dynamicCanvas);
        var vsync = ui.GetVSync();
        if (ui.Checkbox("VSync", ref vsync))
        {
            ui.SetVSync(vsync);
        }

        var msaaSamples = ui.GetMsaaSamples();
        if (ui.SliderInt("MSAA", ref msaaSamples, 1, 8))
        {
            ui.SetMsaaSamples(msaaSamples);
            _activeProfile = msaaSamples <= RenderProfileMsaaSamples
                ? DuxelPerformanceProfile.Render
                : DuxelPerformanceProfile.Display;
        }
        ui.TextV("MSAA: {0}x", ui.GetMsaaSamples());

        var globalStaticCache = _enableGlobalStaticCache;
        if (ui.Checkbox("Global Static Cache", ref globalStaticCache))
        {
            _enableGlobalStaticCache = globalStaticCache;

        }

        ui.SeparatorText("Profile");
        ui.TextV("Current: {0}", GetProfileLabel(_activeProfile));
        var renderProfile = _activeProfile == DuxelPerformanceProfile.Render;
        if (ui.Checkbox("Render Profile", ref renderProfile))
        {
            ApplyProfile(ui, renderProfile ? DuxelPerformanceProfile.Render : DuxelPerformanceProfile.Display);
        }
        if (ui.Checkbox("Paused", ref _paused))
        {
            if (_paused)
            {
                _lastTime = ui.GetTime();
            }
        }

        ui.SeparatorText("Polygons");
        ui.SliderFloat("Speed", ref _baseSpeed, 20f, 600f, 0f, "0");
        ui.SliderFloat("Size", ref _baseSize, 6f, 80f, 0f, "0");
        ui.SliderInt("Sides", ref _baseSides, 3, 10);
        ui.SliderFloat("Rotation", ref _rotationSpeed, 0f, 6f, 0f, "0.00");

        if (ui.Button("Add"))
        {
            EnsurePolygonCapacity(100);
            for (var i = 0; i < 100; i++)
            {
                AddPolygon(renderBounds);
            }
        }
        ui.SameLine();
        if (ui.Button("Remove"))
        {
            for (var i = 0; i < 100; i++)
            {
                RemovePolygon();
            }
        }
        ui.SameLine();
        if (ui.Button("Clear"))
        {
            _polygons.Clear(); _gpuResetRevision++; _canvasRevision++;
        }

        if (ui.Button("Reset"))
        {
            ResetSimulation(renderBounds);
        }

        ui.EndPanel();
    }

    private void DrawRendererStatusOverlay(UiImmediateContext ui)
    {
        var vsync = ui.GetVSync() ? "ON" : "OFF";
        var msaa = ui.GetMsaaSamples();
        var text = $"Renderer: {GetProfileLabel(_activeProfile)} | VSync {vsync} | MSAA {msaa}x";
        ui.DrawOverlayText(
            text,
            new UiColor(210, 210, 210),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Top,
            background: null,
            margin: new UiVector2(8f, 6f));
    }

    private void ApplyProfile(UiImmediateContext ui, DuxelPerformanceProfile profile)
    {
        ui.SetMsaaSamples(GetProfileMsaaSamples(profile));
        _activeProfile = profile;
    }

    private static int GetProfileMsaaSamples(DuxelPerformanceProfile profile) =>
        profile == DuxelPerformanceProfile.Render
            ? RenderProfileMsaaSamples
            : DisplayProfileMsaaSamples;

    private static string GetProfileLabel(DuxelPerformanceProfile profile) =>
        profile == DuxelPerformanceProfile.Render ? "Render" : "Display";

    private static string GetProfileName(DuxelPerformanceProfile profile) =>
        profile == DuxelPerformanceProfile.Render ? "render" : "display";

    private static void DrawStaticBackdrop(UiDrawListBuilder drawList, UiRect bounds, UiTextureId whiteTexture)
    {
        drawList.AddRectFilled(bounds, new UiColor(0xFF121318), whiteTexture, bounds);

        var cols = Math.Max(16, (int)(bounds.Width / 28f));
        var rows = Math.Max(10, (int)(bounds.Height / 28f));
        var cellWidth = MathF.Max(1f, bounds.Width / cols);
        var cellHeight = MathF.Max(1f, bounds.Height / rows);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                var idx = y * cols + x + 1;
                var hash = (uint)(idx * 1103515245 + 12345);
                var hue = (hash >> 9) & 0x3F;
                var alpha = 34u + (hash & 0x1F);
                var r = 22u + hue;
                var g = 26u + ((hue * 3u) & 0x2F);
                var b = 36u + ((hue * 5u) & 0x3F);

                var x0 = bounds.X + x * cellWidth + 1f;
                var y0 = bounds.Y + y * cellHeight + 1f;
                var x1 = x0 + cellWidth - 2f;
                var y1 = y0 + cellHeight - 2f;
                drawList.AddRectFilled(new UiRect(x0, y0, MathF.Max(1f, x1 - x0), MathF.Max(1f, y1 - y0)), new UiColor((alpha << 24) | (r << 16) | (g << 8) | b), whiteTexture, bounds);

                if ((idx & 3) == 0)
                {
                    var cx = x0 + (cellWidth * 0.5f);
                    var cy = y0 + (cellHeight * 0.5f);
                    var radius = MathF.Max(0.8f, MathF.Min(cellWidth, cellHeight) * 0.14f);
                    drawList.AddCircleFilled(new UiVector2(cx, cy), radius, new UiColor(0xFF8FA6C7), whiteTexture, bounds, 10);
                }
            }
        }
    }

    private void UpdateFps(double delta)
    {
        _fps = _fpsCounter.Tick(delta).Fps;
    }

    private void UpdatePolygons(double delta, UiRect bounds)
    {
        if (_paused || _polygons.Count == 0)
        {
            return;
        }

        var dt = (float)delta;
        _canvasRevision++;
        if (_gpuSimulation) { _collisionTicks = 0; return; }
        if (_cpuJacobiSimulation)
        {
            var bodies = CollectionsMarshal.AsSpan(_polygons);
            EnsureGpuParticles(bodies);
            _jacobiSimulation ??= new(_gpuParticles!);
            var start = Stopwatch.GetTimestamp();
            _jacobiSimulation.Step(new(_gpuParticles!, new(bounds.X+8,bounds.Y+8,bounds.Width-16,bounds.Height-16),
                dt,_baseSize,_baseSpeed,_baseSides,MathF.Max(.35f,_rotationSpeed*2.5f),_gpuCollisionIterations));
            _collisionTicks = Stopwatch.GetTimestamp() - start;
            for (var i = 0; i < bodies.Length; i++)
            {
                ref var body = ref bodies[i]; ref readonly var state = ref _jacobiSimulation.Particles[i];
                body.Position = state.Position; body.Direction = state.Direction; body.Rotation = state.Rotation;
                body.AngularSpeed = state.AngularSpeed; body.HitResponse = state.HitResponse;
            }
            return;
        }
        var speed = _baseSpeed;
        var minX = bounds.X + 8f;
        var minY = bounds.Y + 8f;
        var maxX = bounds.X + bounds.Width - 8f;
        var maxY = bounds.Y + bounds.Height - 8f;

        var polygons = CollectionsMarshal.AsSpan(_polygons);
        for (var i = 0; i < polygons.Length; i++)
        {
            ref var body = ref polygons[i];
            var hit = MathF.Max(0f, body.HitResponse - dt * CollisionHitDecayPerSecond);
            body.HitResponse = hit;
            var radius = _baseSize * body.SizeScale * (1f - hit * CollisionHitShrinkMax);
            var dir = body.Direction;
            var pos = body.Position;
            var vx = dir.X * speed * body.SpeedScale;
            var vy = dir.Y * speed * body.SpeedScale;
            var px = pos.X + vx * dt;
            var py = pos.Y + vy * dt;

            if (px - radius < minX)
            {
                px = minX + radius;
                dir = new UiVector2(-dir.X, dir.Y);
            }
            else if (px + radius > maxX)
            {
                px = maxX - radius;
                dir = new UiVector2(-dir.X, dir.Y);
            }

            if (py - radius < minY)
            {
                py = minY + radius;
                dir = new UiVector2(dir.X, -dir.Y);
            }
            else if (py + radius > maxY)
            {
                py = maxY - radius;
                dir = new UiVector2(dir.X, -dir.Y);
            }

            body.Direction = dir;
            body.Position = new UiVector2(px, py);
            var angularDamping = MathF.Max(0f, 1f - dt * CollisionAngularDampingPerSecond);
            body.AngularSpeed *= angularDamping;
            body.Rotation += (float)(body.AngularSpeed * dt);
        }

        var collisionStart = Stopwatch.GetTimestamp();
        _collisionSolver.Resolve(polygons, _baseSize, MathF.Max(0.35f, _rotationSpeed * 2.5f), _random,
            new UiRect(minX, minY, maxX - minX, maxY - minY));
        _collisionTicks = Stopwatch.GetTimestamp() - collisionStart;
    }

    private void ResetSimulation(UiRect bounds)
    {
        _canvasRevision++;
        _gpuResetRevision++;
        var count = _polygons.Count;
        _polygons.Clear();
        EnsurePolygonCapacity(count);
        for (var i = 0; i < count; i++)
        {
            AddPolygon(bounds);
        }
    }

    private static UiColor BlendHitColor(UiColor baseColor, float hit)
    {
        if (hit <= 0.0001f)
        {
            return baseColor;
        }

        var rgba = baseColor.Rgba;
        var r = (byte)((rgba >> 16) & 0xFFu);
        var g = (byte)((rgba >> 8) & 0xFFu);
        var b = (byte)(rgba & 0xFFu);
        var t = Math.Clamp(hit * CollisionHitRedWeight, 0f, 1f);
        var outR = (byte)Math.Clamp((int)MathF.Round(r + (255f - r) * (t * 0.45f)), 0, 255);
        var outG = (byte)Math.Clamp((int)MathF.Round(g * (1f - t * 0.22f)), 0, 255);
        var outB = (byte)Math.Clamp((int)MathF.Round(b * (1f - t * 0.35f)), 0, 255);
        return new UiColor(outR, outG, outB, 255);
    }

    private void EnsurePolygonCapacity(int additionalCount)
    {
        if (additionalCount <= 0)
        {
            return;
        }

        var required = _polygons.Count + additionalCount;
        if (_polygons.Capacity < required)
        {
            _polygons.Capacity = required;
        }
    }

    private void AddPolygon(UiRect bounds)
    {
        var radius = _baseSize * (0.6f + (float)_random.NextDouble() * 0.8f);
        var x = bounds.X + radius + (float)_random.NextDouble() * MathF.Max(1f, bounds.Width - radius * 2f);
        var y = bounds.Y + radius + (float)_random.NextDouble() * MathF.Max(1f, bounds.Height - radius * 2f);
        var angle = (float)(_random.NextDouble() * MathF.Tau);
        var direction = new UiVector2(MathF.Cos(angle), MathF.Sin(angle));

        _polygons.Add(new PolygonBody
        {
            Position = new UiVector2(x, y),
            Direction = direction,
            SpeedScale = 0.6f + (float)_random.NextDouble() * 0.9f,
            SizeScale = 0.6f + (float)_random.NextDouble() * 0.8f,
            SideOffset = _random.Next(-2, 3),
            Rotation = (float)(_random.NextDouble() * MathF.Tau),
            AngularSpeed = (_random.NextDouble() * 2.0 - 1.0) * _rotationSpeed,
            BaseColor = RandomColor(),
            HitResponse = 0f
        });
    }

    private void RemovePolygon()
    {
        if (_polygons.Count == 0)
        {
            return;
        }

        _polygons.RemoveAt(_polygons.Count - 1);
        if (_polygons.Count == 0) _gpuResetRevision++;
    }

    private UiColor RandomColor()
    {
        var r = (uint)(80 + _random.Next(160));
        var g = (uint)(80 + _random.Next(160));
        var b = (uint)(80 + _random.Next(160));
        return new UiColor(0xFF000000u | (r << 16) | (g << 8) | b);
    }

    private struct PolygonBody : ICircleCollisionBody2D
    {
        public UiVector2 Position { get; set; }
        public UiVector2 Direction { get; set; }
        public float SpeedScale;
        public float SizeScale { get; set; }
        public int SideOffset;
        public float Rotation;
        public double AngularSpeed { get; set; }
        public UiColor BaseColor;
        public float HitResponse { get; set; }
    }
}

