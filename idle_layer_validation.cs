// FBA: Adaptive Frame Pacing + 다중 레이어 드래그 검증 샘플
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Duxel.App;
using Duxel.Core;

var screen = new IdleLayerValidationScreen();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Adaptive Frame Pacing / Layer Validation (FBA)",
        Width = 1700,
        Height = 980,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = screen
});

public sealed class IdleLayerValidationScreen : UiScreen
{
    private sealed class LayerCard
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required UiColor HeaderColor { get; init; }
        public UiVector2 Position;
        public UiVector2 Size;
        public int ZOrder;
        public int Density;
    }

    private readonly record struct LayerComposition(string Name, int[] Densities);
    private static readonly LayerComposition[] BuiltInCompositions =
    [
        new("baseline", [4200, 6200, 8600]),
        new("sparse", [1200, 1800, 2600]),
        new("uniform", [5000, 5000, 5000]),
        new("frontheavy", [12000, 5200, 2200]),
        new("backheavy", [2200, 5200, 12000]),
    ];

    private readonly List<LayerCard> _layers = [];
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Timer _metricsWakeTimer;
    private int _metricsTickPending;
    private double _lastTime;
    private float _fps;
    private int _fpsFrames;
    private double _fpsAccum;

    private double _cpuSampleTime;
    private TimeSpan _cpuSampleProcessTime;
    private float _cpuPercent;

    private float _frameTimeMsSmoothed = 16.67f;
    private double _metricsDisplayElapsed;
    private double _metricsSampleElapsed;
    private double _metricsSampleCount;
    private double _newFrameMsAccum;
    private double _renderMsAccum;
    private double _submitMsAccum;
    private double _frameDtMsAccum;
    private float _displayFps;
    private float _displayCpuPercent;
    private float _displayNewFrameMs;
    private float _displayRenderMs;
    private float _displaySubmitMs;
    private float _displayFrameTimeMs;

    private int _renderParticleCount = 6500;
    private float _renderSpeed = 1.2f;
    private bool _animateRender = true;
    private bool _enableLayerTextureCache = true;
    private UiLayerCacheBackend _layerCacheBackend = ReadBenchCacheBackend();
    private float _layerOpacity = ReadBenchLayerOpacity();
    private int _layerCacheBuildCount;

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_OUT");
    private readonly int[] _benchParticleCounts = ReadBenchParticleCounts();
    private readonly double _benchPhaseSeconds = ReadBenchPhaseSeconds();
    private readonly LayerComposition[] _benchLayerCompositions = ReadBenchLayerCompositions();
    private bool _disableFastRender = ReadBenchDisableFastRender();
    private int _benchPhaseIndex;
    private double _benchPhaseElapsed;
    private double _benchFpsSum;
    private double _benchCpuSum;
    private int _benchSamples;
    private readonly List<string> _benchRecords = [];
    private int _benchAppliedCompositionIndex = -1;
    private int _selectedCompositionIndex;

    private int _activeDragLayerId = -1;
    private UiVector2 _dragOffset;
    private int _zCounter;
    private readonly double _autoExitSeconds = ReadAutoExitSeconds();
    private double _elapsedSeconds;

    public IdleLayerValidationScreen()
    {
        _layers.Add(new LayerCard
        {
            Id = 1,
            Name = "Layer A",
            Position = new UiVector2(70f, 70f),
            Size = new UiVector2(360f, 240f),
            ZOrder = 1,
            Density = 4200,
            HeaderColor = new UiColor(0xFF4C6FFF)
        });
        _layers.Add(new LayerCard
        {
            Id = 2,
            Name = "Layer B",
            Position = new UiVector2(300f, 200f),
            Size = new UiVector2(390f, 260f),
            ZOrder = 2,
            Density = 6200,
            HeaderColor = new UiColor(0xFF2AA38F)
        });
        _layers.Add(new LayerCard
        {
            Id = 3,
            Name = "Layer C",
            Position = new UiVector2(560f, 320f),
            Size = new UiVector2(430f, 290f),
            ZOrder = 3,
            Density = 8600,
            HeaderColor = new UiColor(0xFF9F63D9)
        });

        ApplyLayerComposition(_benchLayerCompositions[0]);
        _selectedCompositionIndex = FindBuiltInCompositionIndex(_benchLayerCompositions[0].Name);

        _zCounter = 4;

        _metricsWakeTimer = new Timer(_ =>
        {
            Interlocked.Exchange(ref _metricsTickPending, 1);
            DuxelApp.RequestFrame();
        }, null, 1000, 1000);
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0.0d, 0.05d);
        _lastTime = now;
        _elapsedSeconds += delta;
        var metricsTick = Interlocked.Exchange(ref _metricsTickPending, 0) == 1;

        if (_autoExitSeconds > 0d && _elapsedSeconds >= _autoExitSeconds)
        {
            Environment.Exit(0);
        }

        UpdatePerfMetrics(ui, delta, now, metricsTick);
        TickBenchmark(ui, delta);
        if (ui.IsMouseReleased(0))
        {
            _activeDragLayerId = -1;
        }

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        DrawMetricsWindow(ui, bounds);
        DrawControlWindow(ui, bounds);
        DrawFastRenderWindow(ui, bounds, now);
        DrawLayerLabWindow(ui, bounds);

        if (_animateRender)
        {
            DuxelApp.RequestFrame();
        }
    }

    private static double ReadAutoExitSeconds()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_AUTO_EXIT_SECONDS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0d;
        }

        return double.TryParse(raw, out var seconds) && seconds > 0d ? seconds : 0d;
    }

    private static int[] ReadBenchParticleCounts()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_PARTICLES");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [3000, 9000, 18000];
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out var count) && count > 0)
            {
                values.Add(count);
            }
        }

        return values.Count > 0 ? values.ToArray() : [3000, 9000, 18000];
    }

    private static double ReadBenchPhaseSeconds()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_PHASE_SECONDS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 2.5d;
        }

        return double.TryParse(raw, out var seconds) && seconds >= 0.5d ? seconds : 2.5d;
    }

    private static LayerComposition[] ReadBenchLayerCompositions()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_LAYOUTS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [BuiltInCompositions[0]];
        }

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<LayerComposition>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (TryMapBuiltInComposition(tokens[i], out var composition))
            {
                list.Add(composition);
            }
        }

        return list.Count > 0 ? list.ToArray() : [BuiltInCompositions[0]];
    }

    private static bool TryMapBuiltInComposition(string token, out LayerComposition composition)
    {
        var key = token.Trim();
        if (string.Equals(key, "default", StringComparison.OrdinalIgnoreCase))
        {
            key = "baseline";
        }
        else if (string.Equals(key, "stacked", StringComparison.OrdinalIgnoreCase))
        {
            key = "frontheavy";
        }
        else if (string.Equals(key, "grid", StringComparison.OrdinalIgnoreCase))
        {
            key = "uniform";
        }

        for (var i = 0; i < BuiltInCompositions.Length; i++)
        {
            if (string.Equals(BuiltInCompositions[i].Name, key, StringComparison.OrdinalIgnoreCase))
            {
                composition = BuiltInCompositions[i];
                return true;
            }
        }

        composition = default;
        return false;
    }

    private static int FindBuiltInCompositionIndex(string name)
    {
        for (var i = 0; i < BuiltInCompositions.Length; i++)
        {
            if (string.Equals(BuiltInCompositions[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static bool ReadBenchDisableFastRender()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static UiLayerCacheBackend ReadBenchCacheBackend()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_BACKEND");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return UiLayerCacheBackend.DrawList;
        }

        return string.Equals(raw, "texture", StringComparison.OrdinalIgnoreCase)
            ? UiLayerCacheBackend.Texture
            : UiLayerCacheBackend.DrawList;
    }

    private static float ReadBenchLayerOpacity()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_BENCH_OPACITY");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1f;
        }

        if (!float.TryParse(raw, out var opacity))
        {
            return 1f;
        }

        return Math.Clamp(opacity, 0.2f, 1f);
    }

    private void UpdatePerfMetrics(UiImmediateContext ui, double delta, double now, bool forceDisplayRefresh)
    {
        _fpsFrames++;
        _fpsAccum += delta;
        if (_fpsAccum >= 0.4d)
        {
            _fps = (float)(_fpsFrames / _fpsAccum);
            _fpsFrames = 0;
            _fpsAccum = 0;
        }

        var frameMs = (float)(delta * 1000d);
        _frameTimeMsSmoothed = _frameTimeMsSmoothed * 0.9f + frameMs * 0.1f;

        if (_cpuSampleTime == 0d)
        {
            _cpuSampleTime = now;
            _cpuSampleProcessTime = _process.TotalProcessorTime;
        }
        else if (now - _cpuSampleTime >= 0.5d)
        {
            var procNow = _process.TotalProcessorTime;
            var cpuUsedMs = (procNow - _cpuSampleProcessTime).TotalMilliseconds;
            var elapsedMs = (now - _cpuSampleTime) * 1000d * Environment.ProcessorCount;
            _cpuPercent = elapsedMs > 0d ? (float)(cpuUsedMs / elapsedMs * 100d) : 0f;

            _cpuSampleProcessTime = procNow;
            _cpuSampleTime = now;
        }

        _metricsDisplayElapsed += delta;
        _metricsSampleElapsed += delta;
        _metricsSampleCount += 1d;
        _newFrameMsAccum += ui.GetNewFrameTimeMs();
        _renderMsAccum += ui.GetRenderTimeMs();
        _submitMsAccum += ui.GetSubmitTimeMs();
        _frameDtMsAccum += _frameTimeMsSmoothed;

        if (forceDisplayRefresh || _metricsDisplayElapsed >= 1.0d)
        {
            _displayFps = _fps;
            _displayCpuPercent = _cpuPercent;

            var sampleCount = Math.Max(1d, _metricsSampleCount);
            _displayNewFrameMs = (float)(_newFrameMsAccum / sampleCount);
            _displayRenderMs = (float)(_renderMsAccum / sampleCount);
            _displaySubmitMs = (float)(_submitMsAccum / sampleCount);
            _displayFrameTimeMs = (float)(_frameDtMsAccum / sampleCount);

            _metricsDisplayElapsed = 0d;
            _metricsSampleElapsed = 0d;
            _metricsSampleCount = 0d;
            _newFrameMsAccum = 0d;
            _renderMsAccum = 0d;
            _submitMsAccum = 0d;
            _frameDtMsAccum = 0d;
        }
    }

    private void DrawMetricsWindow(UiImmediateContext ui, UiRect bounds)
    {
        const float margin = 12f;
        const float gap = 12f;
        var leftWidth = 360f;
        var availableHeight = MathF.Max(0f, bounds.Height - (margin * 2f) - gap);
        var halfHeight = availableHeight * 0.5f;

        ui.SetNextWindowPos(new UiVector2(bounds.X + margin, bounds.Y + margin));
        ui.SetNextWindowSize(new UiVector2(leftWidth, halfHeight));
        ui.BeginWindow("Metrics");

        ui.TextV("FPS (1s): {0:0.0}", _displayFps);
        ui.TextV("CPU Usage (1s): {0:0.0}%", _displayCpuPercent);
        ui.Separator();
        ui.TextV("NewFrame: {0:0.00} ms", _displayNewFrameMs);
        ui.TextV("Render:   {0:0.00} ms", _displayRenderMs);
        ui.TextV("Submit:   {0:0.00} ms", _displaySubmitMs);
        ui.TextV("Frame dt: {0:0.00} ms", _displayFrameTimeMs);
        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            ui.Separator();
            var totalPhases = GetBenchPhaseCount();
            ui.TextV("Bench: phase {0}/{1}", Math.Min(_benchPhaseIndex + 1, Math.Max(1, totalPhases)), Math.Max(1, totalPhases));
            ui.TextV("Bench elapsed: {0:0.00}/{1:0.00}s", _benchPhaseElapsed, _benchPhaseSeconds);
        }

        var vsync = ui.GetVSync();
        if (ui.Checkbox("VSync", ref vsync))
        {
            ui.SetVSync(vsync);
        }

        ui.EndWindow();
    }

    private void DrawControlWindow(UiImmediateContext ui, UiRect bounds)
    {
        const float margin = 12f;
        const float gap = 12f;
        var leftWidth = 360f;
        var availableHeight = MathF.Max(0f, bounds.Height - (margin * 2f) - gap);
        var halfHeight = availableHeight * 0.5f;

        ui.SetNextWindowPos(new UiVector2(bounds.X + margin, bounds.Y + margin + halfHeight + gap));
        ui.SetNextWindowSize(new UiVector2(leftWidth, halfHeight));
        ui.BeginWindow("Control");

        ui.Text("Fast Render Window");
        ui.Checkbox("Animate", ref _animateRender);
        ui.Checkbox("Disable Fast Render", ref _disableFastRender);
        ui.SliderInt("Particle Count", ref _renderParticleCount, 1000, 30000);
        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            var composition = _benchLayerCompositions[Math.Clamp(_benchAppliedCompositionIndex, 0, _benchLayerCompositions.Length - 1)].Name;
            ui.TextV("Layer Layout (bench): {0}", composition);
            ui.TextV("Fast Render (bench): {0}", _disableFastRender ? "OFF" : "ON");
        }
        ui.SliderFloat("Animation Speed", ref _renderSpeed, 0.1f, 4.0f);

        ui.SeparatorText("Layer Drag Lab");
        ui.TextWrapped("레이어 카드를 드래그하고, 클릭으로 최상위(Z)로 올릴 수 있습니다.");
        var cacheEnabled = _enableLayerTextureCache;
        if (ui.Checkbox("Enable Layer Texture Cache", ref cacheEnabled))
        {
            _enableLayerTextureCache = cacheEnabled;
            MarkAllLayerCacheDirty(ui);
        }

        ui.TextV("Layer Cache Backend: {0}", _layerCacheBackend == UiLayerCacheBackend.Texture ? "Texture" : "DrawList");
        if (ui.Button("Backend: DrawList") && _layerCacheBackend != UiLayerCacheBackend.DrawList)
        {
            _layerCacheBackend = UiLayerCacheBackend.DrawList;
            MarkAllLayerCacheDirty(ui);
        }
        ui.SameLine();
        if (ui.Button("Backend: Texture") && _layerCacheBackend != UiLayerCacheBackend.Texture)
        {
            _layerCacheBackend = UiLayerCacheBackend.Texture;
            MarkAllLayerCacheDirty(ui);
        }

        ui.SeparatorText("Layer Complexity");
        ui.TextV("Current Preset: {0}", BuiltInCompositions[Math.Clamp(_selectedCompositionIndex, 0, BuiltInCompositions.Length - 1)].Name);
        if (ui.Button("Prev Preset") && _selectedCompositionIndex > 0)
        {
            _selectedCompositionIndex--;
            ApplyLayerComposition(BuiltInCompositions[_selectedCompositionIndex]);
            MarkAllLayerCacheDirty(ui);
        }
        ui.SameLine();
        if (ui.Button("Next Preset") && _selectedCompositionIndex < BuiltInCompositions.Length - 1)
        {
            _selectedCompositionIndex++;
            ApplyLayerComposition(BuiltInCompositions[_selectedCompositionIndex]);
            MarkAllLayerCacheDirty(ui);
        }

        for (var i = 0; i < _layers.Count; i++)
        {
            var density = _layers[i].Density;
            if (ui.SliderInt($"{_layers[i].Name} Density", ref density, 300, 20000))
            {
                _layers[i].Density = density;
                MarkAllLayerCacheDirty(ui);
            }
        }

        ui.SliderFloat("Layer Opacity", ref _layerOpacity, 0.2f, 1.0f);
        ui.TextV("Layer Cache Rebuild Count: {0}", _layerCacheBuildCount);
        ui.TextV("Layer Draw Cost (est): {0}", EstimateLayerDrawCost());
        ui.TextWrapped("Texture 백엔드는 단계적 적용 중입니다. 합성 패스 실험은 DUXEL_LAYER_TEXTURE_COMPOSE=1 로 켤 수 있습니다.");
        if (ui.Button("Reset Layer Positions"))
        {
            ResetLayers(ui);
        }
        if (ui.Button("Rebuild Layer Cache"))
        {
            MarkAllLayerCacheDirty(ui);
        }

        ui.SeparatorText("Adaptive Frame Pacing 기대 동작");
        ui.BulletText("Control/Metrics 창 유휴 시 낮은 갱신 빈도");
        ui.BulletText("Fast Render 창은 고속 갱신 유지");
        ui.BulletText("Layer Lab에서 카드 드래그 시 즉시 반응");

        ui.EndWindow();
    }

    private void DrawFastRenderWindow(UiImmediateContext ui, UiRect bounds, double now)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 388f, bounds.Y + 12f));
        ui.SetNextWindowSize(new UiVector2(bounds.Width - 400f, 320f));
        ui.BeginWindow("Fast Render");

        var origin = ui.GetCursorScreenPos();
        var avail = ui.GetContentRegionAvail();
        var width = MathF.Max(1f, avail.X);
        var height = MathF.Max(1f, avail.Y);
        var canvas = new UiRect(origin.X, origin.Y, width, height);

        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        drawList.PushTexture(white);
        drawList.PushClipRect(canvas);

        drawList.AddRectFilled(canvas, new UiColor(0xFF121212), white, canvas);

        var t = (float)now;
        var disableFastRenderNow = _disableFastRender;
        var count = disableFastRenderNow ? 0 : _renderParticleCount;
        for (var i = 0; i < count; i++)
        {
            var idx = i + 1;
            var phase = idx * 0.017f;
            var speed = _animateRender ? _renderSpeed : 0f;
            var x = canvas.X + ((MathF.Sin(t * speed * 1.7f + phase) * 0.5f + 0.5f) * (canvas.Width - 8f));
            var y = canvas.Y + ((MathF.Cos(t * speed * 1.3f + phase * 1.4f) * 0.5f + 0.5f) * (canvas.Height - 8f));
            var radius = 1.5f + (idx % 4) * 0.6f;
            var c = (uint)((idx * 23) & 0xFF);
            var color = new UiColor(0xFF000000 | (c << 16) | ((255u - c) << 8) | (120u + (c % 120u)));
            drawList.AddCircleFilled(new UiVector2(x, y), radius, color, white, canvas, 8);
        }

        drawList.PopClipRect();
        drawList.PopTexture();

        ui.SetCursorScreenPos(new UiVector2(origin.X, origin.Y));
        ui.Dummy(new UiVector2(width, height));
        ui.EndWindow();
    }

    private void DrawLayerLabWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 388f, bounds.Y + 346f));
        ui.SetNextWindowSize(new UiVector2(bounds.Width - 400f, bounds.Height - 358f));
        ui.BeginWindow("Layer Lab");

        var origin = ui.GetCursorScreenPos();
        var avail = ui.GetContentRegionAvail();
        var width = MathF.Max(1f, avail.X);
        var height = MathF.Max(1f, avail.Y);
        var canvas = new UiRect(origin.X, origin.Y, width, height);

        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        drawList.PushTexture(white);
        drawList.PushClipRect(canvas);

        drawList.AddRectFilled(canvas, new UiColor(0xFF1A1A1A), white, canvas);
        DrawGrid(drawList, canvas);

        _layers.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));
        for (var i = 0; i < _layers.Count; i++)
        {
            DrawLayerCard(ui, drawList, canvas, _layers[i]);
        }

        drawList.PopClipRect();
        drawList.PopTexture();

        ui.SetCursorScreenPos(new UiVector2(origin.X, origin.Y));
        ui.Dummy(new UiVector2(width, height));
        ui.EndWindow();
    }

    private void DrawGrid(UiDrawListBuilder drawList, UiRect canvas)
    {
        const float step = 24f;
        var lineColor = new UiColor(0x332E2E2E);

        for (var x = canvas.X; x < canvas.X + canvas.Width; x += step)
        {
            drawList.AddLine(new UiVector2(x, canvas.Y), new UiVector2(x, canvas.Y + canvas.Height), lineColor, 1f);
        }

        for (var y = canvas.Y; y < canvas.Y + canvas.Height; y += step)
        {
            drawList.AddLine(new UiVector2(canvas.X, y), new UiVector2(canvas.X + canvas.Width, y), lineColor, 1f);
        }
    }

    private void DrawLayerCard(UiImmediateContext ui, UiDrawListBuilder drawList, UiRect canvas, LayerCard layer)
    {
        var cursorBackup = ui.GetCursorScreenPos();

        var layerRect = new UiRect(
            canvas.X + layer.Position.X,
            canvas.Y + layer.Position.Y,
            layer.Size.X,
            layer.Size.Y);

        var headerHeight = 26f;
        var headerRect = new UiRect(layerRect.X, layerRect.Y, layerRect.Width, headerHeight);
        var bodyRect = new UiRect(layerRect.X, layerRect.Y + headerHeight, layerRect.Width, layerRect.Height - headerHeight);
        var localBodyRect = new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height);
        var localClipRect = new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height);
        var layerBodyId = $"layer_body_{layer.Id}";

        drawList.AddRectFilled(layerRect, new UiColor(0xCC242424), ui.WhiteTextureId, canvas);

        var layerOptions = new UiLayerOptions(
            StaticCache: _enableLayerTextureCache,
            Opacity: _layerOpacity,
            Translation: new UiVector2(bodyRect.X, bodyRect.Y),
            CacheBackend: _enableLayerTextureCache ? _layerCacheBackend : UiLayerCacheBackend.DrawList);
        var shouldDrawBody = ui.BeginLayer(layerBodyId, layerOptions);
        if (shouldDrawBody)
        {
            DrawLayerBodyPrimitives(ui.GetWindowDrawList(), localBodyRect, localClipRect, layer, ui.WhiteTextureId);
            if (_enableLayerTextureCache)
            {
                _layerCacheBuildCount++;
            }
        }
        ui.EndLayer();

        drawList.AddRectFilled(headerRect, layer.HeaderColor, ui.WhiteTextureId, canvas);
        drawList.AddRect(layerRect, new UiColor(0xFFA0A0A0), 0f, 1.2f);

        ui.SetCursorScreenPos(new UiVector2(layerRect.X, layerRect.Y));
        ui.InvisibleButton($"layer_drag_{layer.Id}", new UiVector2(layerRect.Width, layerRect.Height));

        if (ui.IsItemClicked())
        {
            layer.ZOrder = _zCounter++;
            _activeDragLayerId = layer.Id;
            var mouse = ui.GetMousePos();
            _dragOffset = new UiVector2(mouse.X - layerRect.X, mouse.Y - layerRect.Y);
        }

        if (_activeDragLayerId == layer.Id && ui.IsMouseDown(0))
        {
            var mouse = ui.GetMousePos();
            var nextX = mouse.X - canvas.X - _dragOffset.X;
            var nextY = mouse.Y - canvas.Y - _dragOffset.Y;

            nextX = Math.Clamp(nextX, 0f, MathF.Max(0f, canvas.Width - layer.Size.X));
            nextY = Math.Clamp(nextY, 0f, MathF.Max(0f, canvas.Height - layer.Size.Y));
            layer.Position = new UiVector2(nextX, nextY);
        }

        ui.SetCursorScreenPos(new UiVector2(layerRect.X + 8f, layerRect.Y + 5f));
        ui.TextV("{0} (Z:{1})", layer.Name, layer.ZOrder);

        ui.SetCursorScreenPos(cursorBackup);
    }

    private void ResetLayers(UiImmediateContext ui)
    {
        _layers[0].Position = new UiVector2(70f, 70f);
        _layers[1].Position = new UiVector2(300f, 200f);
        _layers[2].Position = new UiVector2(560f, 320f);

        _layers[0].ZOrder = 1;
        _layers[1].ZOrder = 2;
        _layers[2].ZOrder = 3;

        _zCounter = 4;
        _activeDragLayerId = -1;
        MarkAllLayerCacheDirty(ui);
    }

    private void TickBenchmark(UiImmediateContext ui, double delta)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath) || _benchParticleCounts.Length == 0)
        {
            return;
        }

        var totalPhases = GetBenchPhaseCount();

        if (_benchPhaseIndex >= totalPhases)
        {
            return;
        }

        if (_benchPhaseElapsed <= 0.0001d && _benchSamples == 0)
        {
            ApplyBenchmarkPhase(ui, _benchPhaseIndex);
        }

        _benchPhaseElapsed += delta;
        if (_fps > 0f)
        {
            _benchFpsSum += _fps;
            _benchCpuSum += _cpuPercent;
            _benchSamples++;
        }

        if (_benchPhaseElapsed < _benchPhaseSeconds)
        {
            return;
        }

        var avgFps = _benchSamples > 0 ? _benchFpsSum / _benchSamples : 0d;
        var avgCpu = _benchSamples > 0 ? _benchCpuSum / _benchSamples : 0d;

        _benchRecords.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"layout\":\"{1}\",\"backend\":\"{2}\",\"particles\":{3},\"cache\":{4},\"avgFps\":{5:0.###},\"avgCpu\":{6:0.###},\"samples\":{7}}}",
            _benchPhaseIndex,
            _benchLayerCompositions[Math.Clamp(_benchAppliedCompositionIndex, 0, _benchLayerCompositions.Length - 1)].Name,
            _layerCacheBackend == UiLayerCacheBackend.Texture ? "texture" : "drawlist",
            _renderParticleCount,
            _enableLayerTextureCache ? "true" : "false",
            avgFps,
            avgCpu,
            _benchSamples));

        _benchPhaseIndex++;
        _benchPhaseElapsed = 0d;
        _benchFpsSum = 0d;
        _benchCpuSum = 0d;
        _benchSamples = 0;

        if (_benchPhaseIndex >= totalPhases)
        {
            WriteBenchmarkOutput();
            Environment.Exit(0);
        }
    }

    private void ApplyBenchmarkPhase(UiImmediateContext ui, int phaseIndex)
    {
        var phasesPerLayout = Math.Max(1, _benchParticleCounts.Length * 2);
        var compositionIndex = Math.Clamp(phaseIndex / phasesPerLayout, 0, _benchLayerCompositions.Length - 1);
        var phaseInLayout = phaseIndex % phasesPerLayout;
        var particleIndex = Math.Clamp(phaseInLayout / 2, 0, _benchParticleCounts.Length - 1);
        var cacheOn = (phaseInLayout % 2) == 1;
        var prevCache = _enableLayerTextureCache;
        var compositionChanged = _benchAppliedCompositionIndex != compositionIndex;

        if (compositionChanged)
        {
            ApplyLayerComposition(_benchLayerCompositions[compositionIndex]);
            _benchAppliedCompositionIndex = compositionIndex;
        }

        _renderParticleCount = _benchParticleCounts[particleIndex];
        _enableLayerTextureCache = cacheOn;
        if (compositionChanged || prevCache != _enableLayerTextureCache)
        {
            MarkAllLayerCacheDirty(ui);
            _layerCacheBuildCount = 0;
        }
    }

    private int GetBenchPhaseCount()
    {
        var layoutCount = Math.Max(1, _benchLayerCompositions.Length);
        var particleCount = Math.Max(1, _benchParticleCounts.Length);
        return layoutCount * particleCount * 2;
    }

    private void WriteBenchmarkOutput()
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
        sb.Append(_benchPhaseSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(",\"results\":[");
        for (var i = 0; i < _benchRecords.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(_benchRecords[i]);
        }

        sb.Append("]}");
        File.WriteAllText(_benchOutputPath, sb.ToString());
    }

    private int EstimateLayerDrawCost()
    {
        if (_enableLayerTextureCache)
        {
            return _layers.Count;
        }

        var sum = 0;
        for (var i = 0; i < _layers.Count; i++)
        {
            sum += _layers[i].Density;
        }

        return sum;
    }

    private void MarkAllLayerCacheDirty(UiImmediateContext ui)
    {
        ui.MarkAllLayersDirty();
    }

    private void ApplyLayerComposition(LayerComposition composition)
    {
        for (var i = 0; i < _layers.Count; i++)
        {
            var densityIndex = Math.Clamp(i, 0, composition.Densities.Length - 1);
            _layers[i].Density = composition.Densities[densityIndex];
        }
    }

    private static void DrawLayerBodyPrimitives(UiDrawListBuilder drawList, UiRect bodyRect, UiRect clip, LayerCard layer, UiTextureId whiteTexture)
    {
        drawList.AddRectFilled(bodyRect, new UiColor(0xCC1C1E26), whiteTexture, clip);

        var lineCount = Math.Max(24, layer.Density / 20);
        for (var i = 0; i < lineCount; i++)
        {
            var baseY = bodyRect.Y + (i + 1) * (bodyRect.Height / (lineCount + 1));
            var waveA = MathF.Sin((i + layer.Id * 0.7f) * 0.41f);
            var waveB = MathF.Cos((i + layer.Id * 1.3f) * 0.27f);
            var waveC = MathF.Sin((i + 1) * (i + layer.Id) * 0.007f);
            var wobble = (waveA * 8f) + (waveB * 5f) + (waveC * 3f);
            var y = baseY + waveB * 4f;
            drawList.AddLine(
                new UiVector2(bodyRect.X + 4f, y + wobble),
                new UiVector2(bodyRect.X + bodyRect.Width - 4f, y - wobble),
                new UiColor((uint)(0x88C0442Au + (uint)((i * 41) & 0x2Fu))),
                1f + (i % 3) * 0.35f);
        }

        var dotCount = layer.Density * 2;
        for (var i = 0; i < dotCount; i++)
        {
            var u = (i * 37 % 211) / 211f;
            var v = (i * 61 % 197) / 197f;
            var curveA = MathF.Sin((u * 9.7f + v * 6.3f + layer.Id * 0.31f) * 3.14159f);
            var curveB = MathF.Cos((u * 7.9f - v * 5.1f + layer.Id * 0.53f) * 3.14159f);
            var curveC = MathF.Sin((curveA + curveB) * 2.7f);
            u = Math.Clamp(u + curveA * 0.018f + curveC * 0.012f, 0f, 1f);
            v = Math.Clamp(v + curveB * 0.018f - curveC * 0.012f, 0f, 1f);
            var x = bodyRect.X + 4f + u * Math.Max(1f, bodyRect.Width - 8f);
            var y = bodyRect.Y + 4f + v * Math.Max(1f, bodyRect.Height - 8f);
            var size = 1.5f + (i % 4) * 0.6f;
            var color = new UiColor((uint)(0xCC5A38A8u + (uint)((i * 19) & 0x3F)));
            drawList.AddRectFilled(new UiRect(x, y, size, size), color, whiteTexture, clip);
        }

        var bubbleCount = Math.Max(12, layer.Density / 90);
        for (var i = 0; i < bubbleCount; i++)
        {
            var u = (i * 19 % 97) / 97f;
            var v = (i * 43 % 89) / 89f;
            var pulse = MathF.Sin((i + layer.Id * 3f) * 0.37f) * 0.5f + 0.5f;
            var x = bodyRect.X + 14f + u * Math.Max(1f, bodyRect.Width - 28f);
            var y = bodyRect.Y + 14f + v * Math.Max(1f, bodyRect.Height - 28f);
            var radius = 3.5f + (i % 5) * 1.8f + pulse * 1.2f;
            var color = new UiColor((uint)(0x66C06A3Bu + (uint)((i * 13) & 0x2F)));
            drawList.AddCircleFilled(new UiVector2(x, y), radius, color, whiteTexture, clip, 12);
        }
    }
}
