// FBA: 심플 혼합 시나리오 — Layer Cache Backend + Dynamic Widgets + Primitive Load
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Layer/Widget Mix Bench (FBA)",
        Width = 1560,
        Height = 920,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new LayerWidgetMixBenchScreen()
});

public sealed class LayerWidgetMixBenchScreen : UiScreen
{
    private sealed class LayerCard
    {
        public required string Id { get; init; }
        public required UiColor HeaderColor { get; init; }
        public UiVector2 Position;
        public UiVector2 Size;
        public int Density;
    }

    private readonly record struct PhaseSpec(string Name, bool EnableStaticCache, UiLayerCacheBackend Backend, int DensityScale);

    private readonly List<LayerCard> _layers =
    [
        new LayerCard { Id = "L0", Position = new UiVector2(48f, 46f), Size = new UiVector2(360f, 230f), Density = 3800, HeaderColor = new UiColor(0xFF3A68D8) },
        new LayerCard { Id = "L1", Position = new UiVector2(290f, 200f), Size = new UiVector2(400f, 250f), Density = 5400, HeaderColor = new UiColor(0xFF2E9B85) },
        new LayerCard { Id = "L2", Position = new UiVector2(610f, 345f), Size = new UiVector2(430f, 280f), Density = 7600, HeaderColor = new UiColor(0xFF8E59D0) },
    ];

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_LAYER_WIDGET_BENCH_OUT");
    private readonly int[] _densityScales = ReadDensityScales();
    private readonly double _phaseSeconds = ReadPhaseSeconds();

    private readonly List<string> _benchRecords = [];
    private readonly List<PhaseSpec> _phases = [];

    private bool _initialized;
    private bool _cacheEnabled;
    private UiLayerCacheBackend _cacheBackend = UiLayerCacheBackend.DrawList;
    private int _densityScale = 100;

    private int _phaseIndex;
    private double _phaseElapsed;
    private double _phaseFpsSum;
    private int _phaseSampleCount;

    private double _lastTime;
    private float _fps;
    private int _fpsFrameCount;
    private double _fpsAccum;

    private int _sliderValue = 36;
    private bool _checkA = true;
    private bool _checkB;
    private string _searchText = "layer cache";
    private string _filterText = "texture";

    public override void Render(UiImmediateContext ui)
    {
        InitializeIfNeeded();

        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0d, 0.05d);
        _lastTime = now;

        UpdateFps(delta);
        TickBenchmark(ui, delta);

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        DrawControlWindow(ui, bounds);
        DrawSceneWindow(ui, bounds, now);
        DrawWidgetWindow(ui, bounds);

        DuxelApp.RequestFrame();
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        BuildPhases();
        ApplyPhase(0);
    }

    private static int[] ReadDensityScales()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_WIDGET_DENSITY_SCALES");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [100, 170];
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out var value) && value >= 50 && value <= 400)
            {
                values.Add(value);
            }
        }

        return values.Count > 0 ? values.ToArray() : [100, 170];
    }

    private static double ReadPhaseSeconds()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_WIDGET_PHASE_SECONDS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 2.2d;
        }

        return double.TryParse(raw, out var seconds) && seconds >= 0.8d ? seconds : 2.2d;
    }

    private void BuildPhases()
    {
        _phases.Clear();

        for (var i = 0; i < _densityScales.Length; i++)
        {
            var scale = _densityScales[i];
            _phases.Add(new PhaseSpec($"nocache-drawlist-{scale}", EnableStaticCache: false, UiLayerCacheBackend.DrawList, scale));
            _phases.Add(new PhaseSpec($"cache-drawlist-{scale}", EnableStaticCache: true, UiLayerCacheBackend.DrawList, scale));
            _phases.Add(new PhaseSpec($"cache-texture-{scale}", EnableStaticCache: true, UiLayerCacheBackend.Texture, scale));
        }
    }

    private void ApplyPhase(int index)
    {
        if (index < 0 || index >= _phases.Count)
        {
            return;
        }

        var phase = _phases[index];
        _cacheEnabled = phase.EnableStaticCache;
        _cacheBackend = phase.Backend;
        _densityScale = phase.DensityScale;
    }

    private void TickBenchmark(UiImmediateContext ui, double delta)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        if (_phaseIndex >= _phases.Count)
        {
            return;
        }

        _phaseElapsed += delta;
        if (_fps > 0f)
        {
            _phaseFpsSum += _fps;
            _phaseSampleCount++;
        }

        if (_phaseElapsed < _phaseSeconds)
        {
            return;
        }

        var avgFps = _phaseSampleCount > 0 ? _phaseFpsSum / _phaseSampleCount : 0d;
        var phase = _phases[_phaseIndex];
        _benchRecords.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"name\":\"{1}\",\"cache\":{2},\"backend\":\"{3}\",\"densityScale\":{4},\"avgFps\":{5:0.###},\"samples\":{6}}}",
            _phaseIndex,
            phase.Name,
            phase.EnableStaticCache ? "true" : "false",
            phase.Backend == UiLayerCacheBackend.Texture ? "texture" : "drawlist",
            phase.DensityScale,
            avgFps,
            _phaseSampleCount));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _phaseFpsSum = 0d;
        _phaseSampleCount = 0;

        if (_phaseIndex >= _phases.Count)
        {
            WriteBenchmarkOutput();
            Environment.Exit(0);
            return;
        }

        ApplyPhase(_phaseIndex);
        ui.MarkAllLayersDirty();
    }

    private void WriteBenchmarkOutput()
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        var json = $"{{\"phaseSeconds\":{_phaseSeconds.ToString(CultureInfo.InvariantCulture)},\"records\":[{string.Join(',', _benchRecords)}]}}";
        File.WriteAllText(_benchOutputPath!, json);
    }

    private void UpdateFps(double delta)
    {
        _fpsAccum += delta;
        _fpsFrameCount++;
        if (_fpsAccum < 0.25d)
        {
            return;
        }

        _fps = (float)(_fpsFrameCount / _fpsAccum);
        _fpsFrameCount = 0;
        _fpsAccum = 0d;
    }

    private void DrawControlWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 14f, bounds.Y + 14f));
        ui.SetNextWindowSize(new UiVector2(330f, 220f));
        ui.BeginWindow("Bench Control");

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Phase: {0}/{1}", Math.Min(_phaseIndex + 1, Math.Max(1, _phases.Count)), Math.Max(1, _phases.Count));
        ui.TextV("Cache: {0}", _cacheEnabled ? "ON" : "OFF");
        ui.TextV("Backend: {0}", _cacheBackend == UiLayerCacheBackend.Texture ? "Texture" : "DrawList");
        ui.TextV("Density Scale: {0}%", _densityScale);

        if (ui.Button("Mark All Layers Dirty"))
        {
            ui.MarkAllLayersDirty();
        }

        ui.EndWindow();
    }

    private void DrawSceneWindow(UiImmediateContext ui, UiRect bounds, double now)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 360f, bounds.Y + 14f));
        ui.SetNextWindowSize(new UiVector2(MathF.Max(640f, bounds.Width - 374f), MathF.Max(480f, bounds.Height - 300f)));
        ui.BeginWindow("Layer Scene");

        var canvasPos = ui.GetCursorScreenPos();
        var canvasSize = ui.GetContentRegionAvail();
        var canvas = new UiRect(canvasPos.X, canvasPos.Y, MathF.Max(1f, canvasSize.X), MathF.Max(1f, canvasSize.Y));
        var drawList = ui.GetWindowDrawList();

        DrawGrid(drawList, canvas);

        for (var i = 0; i < _layers.Count; i++)
        {
            DrawLayerCard(ui, drawList, canvas, _layers[i], now);
        }

        ui.Dummy(new UiVector2(canvas.Width, canvas.Height));
        ui.EndWindow();
    }

    private void DrawWidgetWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 14f, bounds.Y + 250f));
        ui.SetNextWindowSize(new UiVector2(330f, MathF.Max(280f, bounds.Height - 264f)));
        ui.BeginWindow("Dynamic Widgets");

        ui.InputText("Search", ref _searchText, 128);
        ui.InputText("Filter", ref _filterText, 128);
        ui.Checkbox("Feature A", ref _checkA);
        ui.Checkbox("Feature B", ref _checkB);
        ui.SliderInt("Count", ref _sliderValue, 1, 120);

        for (var i = 0; i < 12; i++)
        {
            ui.TextV("Row {0:00} :: {1} / {2}", i, _searchText, _sliderValue + i);
        }

        ui.EndWindow();
    }

    private void DrawLayerCard(UiImmediateContext ui, UiDrawListBuilder drawList, UiRect canvas, LayerCard layer, double now)
    {
        var layerRect = new UiRect(
            canvas.X + layer.Position.X,
            canvas.Y + layer.Position.Y,
            layer.Size.X,
            layer.Size.Y);

        var headerHeight = 25f;
        var headerRect = new UiRect(layerRect.X, layerRect.Y, layerRect.Width, headerHeight);
        var bodyRect = new UiRect(layerRect.X, layerRect.Y + headerHeight, layerRect.Width, layerRect.Height - headerHeight);

        drawList.AddRectFilled(layerRect, new UiColor(0xC91E2026), ui.WhiteTextureId, canvas);

        var options = new UiLayerOptions(
            StaticCache: _cacheEnabled,
            Opacity: 1f,
            Translation: new UiVector2(bodyRect.X, bodyRect.Y),
            CacheBackend: _cacheEnabled ? _cacheBackend : UiLayerCacheBackend.DrawList);

        var shouldDraw = ui.BeginLayer($"mix_layer_{layer.Id}", options);
        if (shouldDraw)
        {
            var localBody = new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height);
            DrawLayerBody(ui.GetWindowDrawList(), localBody, layer, _densityScale, ui.WhiteTextureId);
        }
        ui.EndLayer();

        drawList.AddRectFilled(headerRect, layer.HeaderColor, ui.WhiteTextureId, canvas);
        drawList.AddRect(layerRect, new UiColor(0xFFA6A6A6), 0f, 1.1f);

        var pulse = (MathF.Sin((float)now * 2.1f + layerRect.X * 0.01f) + 1f) * 0.5f;
        var markerX = layerRect.X + 10f + pulse * (layerRect.Width - 20f);
        drawList.AddCircleFilled(new UiVector2(markerX, layerRect.Y + 12f), 3.2f, new UiColor(0xFFFCF18B), ui.WhiteTextureId, canvas, 12);

        var backup = ui.GetCursorScreenPos();
        ui.SetCursorScreenPos(new UiVector2(layerRect.X + 8f, layerRect.Y + 5f));
        ui.TextV("{0} ({1}%)", layer.Id, _densityScale);
        ui.SetCursorScreenPos(backup);
    }

    private static void DrawGrid(UiDrawListBuilder drawList, UiRect canvas)
    {
        const float step = 28f;
        var lineColor = new UiColor(0x2AFFFFFF);

        for (var x = canvas.X; x <= canvas.X + canvas.Width; x += step)
        {
            drawList.AddLine(new UiVector2(x, canvas.Y), new UiVector2(x, canvas.Y + canvas.Height), lineColor, 1f);
        }

        for (var y = canvas.Y; y <= canvas.Y + canvas.Height; y += step)
        {
            drawList.AddLine(new UiVector2(canvas.X, y), new UiVector2(canvas.X + canvas.Width, y), lineColor, 1f);
        }
    }

    private static void DrawLayerBody(UiDrawListBuilder drawList, UiRect localBody, LayerCard layer, int densityScale, UiTextureId whiteTexture)
    {
        drawList.PushClipRect(localBody, true);

        drawList.AddRectFilled(localBody, new UiColor(0xBC151821), whiteTexture, localBody);

        var scaledDensity = Math.Max(600, layer.Density * densityScale / 100);
        var lineCount = Math.Max(20, scaledDensity / 28);
        for (var i = 0; i < lineCount; i++)
        {
            var t = (i + 1f) / (lineCount + 1f);
            var y = localBody.Y + t * localBody.Height;
            var wave = MathF.Sin((i + 1) * 0.23f + layer.Position.X * 0.02f) * 6f;
            drawList.AddLine(
                new UiVector2(localBody.X + 4f, y + wave),
                new UiVector2(localBody.X + localBody.Width - 4f, y - wave),
                new UiColor((uint)(0x7FB4D56Cu + (uint)((i * 17) & 0x3F))),
                1.1f);
        }

        var dotCount = scaledDensity;
        for (var i = 0; i < dotCount; i++)
        {
            var u = (i * 31 % 251) / 251f;
            var v = (i * 57 % 241) / 241f;
            var x = localBody.X + 4f + u * MathF.Max(1f, localBody.Width - 8f);
            var y = localBody.Y + 4f + v * MathF.Max(1f, localBody.Height - 8f);
            var s = 1.2f + (i % 3) * 0.5f;
            drawList.AddRectFilled(new UiRect(x, y, s, s), new UiColor((uint)(0x88A94BE2u + (uint)((i * 23) & 0x3F))), whiteTexture, localBody);
        }

        drawList.PopClipRect();
    }
}
