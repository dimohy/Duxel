// FBA: Scrolling Static Layer Benchmark
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

var screen = new ScrollingStaticLayerBenchScreen();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Scrolling Static Layer Bench (FBA)",
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

public sealed class ScrollingStaticLayerBenchScreen : UiScreen
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

    private enum BenchPhase
    {
        DynamicScroll,
        StaticScroll,
        StaticSlidingClip,
    }

    private readonly List<LayerInfo> _layers = [];
    private readonly Process _process = Process.GetCurrentProcess();

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_SCROLL_STATIC_LAYER_BENCH_OUT");
    private readonly double _phaseSeconds = ReadPhaseSeconds();
    private readonly int _layerCount = ReadLayerCount();
    private readonly int _densityPerLayer = ReadDensityPerLayer();
    private readonly float _scrollSpeed = ReadScrollSpeed();
    private readonly int _startPhaseIndex = ReadStartPhaseIndex();
    private readonly int _phaseCount = ReadPhaseCount();

    private int _phaseIndex;
    private int _finalPhaseIndex;
    private double _phaseElapsed;
    private double _fpsSum;
    private double _cpuSum;
    private int _samples;
    private int _cacheBuildCount;
    private int _layerBodyDrawCount;
    private readonly List<string> _records = [];

    private double _lastTime;
    private double _cpuSampleTime;
    private TimeSpan _cpuSampleProcessTime;
    private float _cpuPercent;
    private float _scrollY;
    private float _scrollDirection = 1f;
    private float _clipProbeOffset;
    private bool _clipProbePrimed;
    private BenchPhase _lastPhase = BenchPhase.DynamicScroll;

    public ScrollingStaticLayerBenchScreen()
    {
        _phaseIndex = _startPhaseIndex;
        _finalPhaseIndex = Math.Min(2, _startPhaseIndex + _phaseCount - 1);
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

        var phase = CurrentPhase;
        if (phase != _lastPhase)
        {
            OnPhaseChanged(ui, phase);
        }

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        UpdateAnimation(delta, phase);
        DrawScrollingCanvasWindow(ui, bounds, phase);
        DrawClipProbeWindow(ui, bounds, phase);
        DrawStatsWindow(ui, bounds, delta, phase);

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            TickBenchmark(delta, phase);
        }

        DuxelApp.RequestFrame();
    }

    private BenchPhase CurrentPhase => _phaseIndex switch
    {
        0 => BenchPhase.DynamicScroll,
        1 => BenchPhase.StaticScroll,
        _ => BenchPhase.StaticSlidingClip,
    };

    private void OnPhaseChanged(UiImmediateContext ui, BenchPhase phase)
    {
        _lastPhase = phase;
        _scrollY = 0f;
        _scrollDirection = 1f;
        _clipProbeOffset = 0f;
        _clipProbePrimed = false;

        if (phase == BenchPhase.StaticSlidingClip)
        {
            ui.MarkLayerDirty("clip_probe_body");
        }
    }

    private void UpdateAnimation(double delta, BenchPhase phase)
    {
        if (phase is BenchPhase.DynamicScroll or BenchPhase.StaticScroll)
        {
            var maxScroll = GetVirtualScrollMax();
            _scrollY += _scrollDirection * _scrollSpeed * (float)delta;
            if (_scrollY >= maxScroll)
            {
                _scrollY = maxScroll;
                _scrollDirection = -1f;
            }
            else if (_scrollY <= 0f)
            {
                _scrollY = 0f;
                _scrollDirection = 1f;
            }
        }
        else
        {
            const float probeTravel = 260f;
            _clipProbeOffset += _scrollDirection * _scrollSpeed * 0.65f * (float)delta;
            if (_clipProbeOffset >= probeTravel)
            {
                _clipProbeOffset = probeTravel;
                _scrollDirection = -1f;
            }
            else if (_clipProbeOffset <= 0f)
            {
                _clipProbeOffset = 0f;
                _scrollDirection = 1f;
            }
        }
    }

    private void DrawScrollingCanvasWindow(UiImmediateContext ui, UiRect bounds, BenchPhase phase)
    {
        var windowHeight = bounds.Height - 270f;
        ui.SetNextWindowPos(new UiVector2(bounds.X + 12f, bounds.Y + 12f));
        ui.SetNextWindowSize(new UiVector2(bounds.Width - 420f, windowHeight));
        ui.SetNextWindowContentSize(new UiVector2(1320f, GetVirtualCanvasHeight()));
        ui.SetNextWindowScroll(0f, phase is BenchPhase.DynamicScroll or BenchPhase.StaticScroll ? _scrollY : 0f);
        ui.BeginWindow("Scrolling Static Layer Canvas");

        var origin = ui.GetCursorScreenPos();
        var avail = ui.GetContentRegionAvail();
        var visibleCanvas = new UiRect(origin.X, origin.Y, MathF.Max(1f, avail.X), MathF.Max(1f, windowHeight - 70f));
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;

        drawList.PushTexture(white);
        drawList.PushClipRect(visibleCanvas);
        drawList.AddRectFilled(visibleCanvas, new UiColor(0xFF14171D), white, visibleCanvas);

        if (phase != BenchPhase.StaticSlidingClip)
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                DrawLayer(ui, visibleCanvas, _layers[i], phase == BenchPhase.StaticScroll, phase);
            }
        }
        else
        {
            DrawStaticClipProbeHint(drawList, visibleCanvas, white);
        }

        DrawScrollMarkers(drawList, visibleCanvas, white);
        drawList.PopClipRect();
        drawList.PopTexture();

        ui.SetCursorScreenPos(new UiVector2(origin.X, origin.Y));
        ui.Dummy(new UiVector2(1320f, GetVirtualCanvasHeight()));
        ui.EndWindow();
    }

    private void DrawClipProbeWindow(UiImmediateContext ui, UiRect bounds, BenchPhase phase)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + bounds.Width - 396f, bounds.Y + 12f));
        ui.SetNextWindowSize(new UiVector2(384f, 470f));
        ui.BeginWindow("Static Clip Probe");

        ui.Text("Targets command-signature changes.");
        ui.Text("The static body translation is fixed.");
        ui.Text("Only the parent clip window slides.");

        var origin = ui.GetCursorScreenPos();
        var canvas = new UiRect(origin.X, origin.Y + 8f, 342f, 342f);
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;

        drawList.PushTexture(white);
        drawList.PushClipRect(canvas);
        drawList.AddRectFilled(canvas, new UiColor(0xFF101010), white, canvas);

        var cardPos = new UiVector2(18f, 18f);
        var cardSize = new UiVector2(306f, 300f);
        ui.DrawLayerCardInteractive(
            canvas,
            cardPos,
            cardSize,
            new UiColor(0xFF5D7BFF),
            "Fixed Static Layer",
            out _,
            out var bodyRect,
            out _,
            bodyBackground: new UiColor(0xCC202020),
            borderColor: new UiColor(0xFFB8B8B8),
            headerHeight: 24f,
            borderThickness: 1f,
            headerTextInsetX: 6f,
            headerTextInsetY: 4f,
            hitTestId: "clip_probe_card");

        var slidingClip = new UiRect(bodyRect.X, bodyRect.Y + _clipProbeOffset, bodyRect.Width, 82f);
        var useSlidingClip = phase == BenchPhase.StaticSlidingClip && _clipProbePrimed;
        if (useSlidingClip)
        {
            ui.PushClipRect(slidingClip, true);
        }

        var shouldDraw = ui.BeginLayer(
            "clip_probe_body",
            new UiLayerOptions(
                StaticCache: true,
                Opacity: 1f,
                Translation: new UiVector2(bodyRect.X, bodyRect.Y)));

        if (shouldDraw)
        {
            DrawHeavyLayerBody(
                ui.GetWindowDrawList(),
                new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height),
                Math.Max(400, _densityPerLayer / 2),
                ui.WhiteTextureId,
                labelSeed: 8000);
            _cacheBuildCount++;
            _layerBodyDrawCount++;
            if (phase == BenchPhase.StaticSlidingClip)
            {
                _clipProbePrimed = true;
            }
        }
        ui.EndLayer();

        if (useSlidingClip)
        {
            ui.PopClipRect();
        }

        drawList.AddRectFilled(new UiRect(slidingClip.X, slidingClip.Y, slidingClip.Width, 2f), new UiColor(0xFFFFE066), white, canvas);
        drawList.AddRectFilled(new UiRect(slidingClip.X, slidingClip.Y + slidingClip.Height - 2f, slidingClip.Width, 2f), new UiColor(0xFFFFE066), white, canvas);
        drawList.PopClipRect();
        drawList.PopTexture();

        ui.SetCursorScreenPos(new UiVector2(origin.X, canvas.Y + canvas.Height + 8f));
        ui.TextV("Probe primed: {0}", _clipProbePrimed ? "yes" : "no");
        ui.TextV("Clip Y: {0:0.0}", _clipProbeOffset);
        ui.EndWindow();
    }

    private void DrawLayer(UiImmediateContext ui, UiRect canvas, LayerInfo layer, bool staticCache, BenchPhase phase)
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
            hitTestId: $"scroll_layer_card_{layer.Id}");

        var layerId = phase == BenchPhase.StaticScroll
            ? $"scroll_static_body_{layer.Id}"
            : $"scroll_dynamic_body_{layer.Id}";

        var shouldDraw = ui.BeginLayer(
            layerId,
            new UiLayerOptions(
                StaticCache: staticCache,
                Opacity: 1f,
                Translation: new UiVector2(bodyRect.X, bodyRect.Y)));

        if (shouldDraw)
        {
            DrawHeavyLayerBody(
                ui.GetWindowDrawList(),
                new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height),
                layer.Density,
                ui.WhiteTextureId,
                labelSeed: layer.Id * 100);
            _layerBodyDrawCount++;
            if (staticCache)
            {
                _cacheBuildCount++;
            }
        }
        ui.EndLayer();
    }

    private static void DrawHeavyLayerBody(UiDrawListBuilder drawList, UiRect rect, int density, UiTextureId white, int labelSeed)
    {
        drawList.AddRectFilled(rect, new UiColor(0xAA2B2B2B), white, rect);
        var width = MathF.Max(1f, rect.Width - 8f);
        var height = MathF.Max(1f, rect.Height - 8f);

        for (var i = 0; i < density; i++)
        {
            var t = i + 1 + labelSeed;
            var px = 4f + ((t * 37) % 1000) * 0.001f * width;
            var py = 4f + ((t * 73) % 1000) * 0.001f * height;
            var r = 0.8f + ((t * 13) % 5) * 0.35f;
            var c = (uint)((t * 29) & 0xFF);
            var color = new UiColor(0xFF000000 | (c << 16) | ((255u - c) << 8) | (120u + (c % 120u)));
            drawList.AddCircleFilled(new UiVector2(px, py), r, color, white, rect, 8);
        }

        const int stripes = 9;
        for (var i = 0; i < stripes; i++)
        {
            var y = rect.Y + 8f + i * (rect.Height - 16f) / Math.Max(1, stripes - 1);
            var color = (i & 1) == 0 ? new UiColor(0x66FFFFFF) : new UiColor(0x6688C0FF);
            drawList.AddRectFilled(new UiRect(rect.X + 8f, y, rect.Width - 16f, 2f), color, white, rect);
        }
    }

    private static void DrawScrollMarkers(UiDrawListBuilder drawList, UiRect canvas, UiTextureId white)
    {
        const int markerCount = 12;
        for (var i = 0; i < markerCount; i++)
        {
            var y = canvas.Y + 12f + i * 52f;
            var color = (i & 1) == 0 ? new UiColor(0xFF3D566E) : new UiColor(0xFF4C3E63);
            drawList.AddRectFilled(new UiRect(canvas.X + canvas.Width - 28f, y, 16f, 24f), color, white, canvas);
        }
    }

    private static void DrawStaticClipProbeHint(UiDrawListBuilder drawList, UiRect canvas, UiTextureId white)
    {
        var panel = new UiRect(canvas.X + 28f, canvas.Y + 28f, MathF.Min(520f, canvas.Width - 56f), 92f);
        drawList.AddRectFilled(panel, new UiColor(0xDD243142), white, canvas);
        drawList.AddRect(panel, new UiColor(0xFF86A9FF), 0f, 1f);
        for (var i = 0; i < 10; i++)
        {
            var y = panel.Y + 14f + i * 7f;
            var width = 80f + i * 34f;
            var color = (i & 1) == 0 ? new UiColor(0xFF86A9FF) : new UiColor(0xFFFFE066);
            drawList.AddRectFilled(new UiRect(panel.X + 16f, y, MathF.Min(width, panel.Width - 32f), 3f), color, white, panel);
        }
    }

    private void DrawStatsWindow(UiImmediateContext ui, UiRect bounds, double delta, BenchPhase phase)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 12f, bounds.Y + bounds.Height - 246f));
        ui.SetNextWindowSize(new UiVector2(740f, 232f));
        ui.BeginWindow("Scrolling Static Layer Bench Stats");

        ui.TextV("Phase: {0}", PhaseName(phase));
        ui.TextV("Layer Count: {0}", _layerCount);
        ui.TextV("Density / Layer: {0}", _densityPerLayer);
        ui.TextV("Scroll Y: {0:0.0} / {1:0.0}", _scrollY, GetVirtualScrollMax());
        ui.TextV("Frame dt: {0:0.000} ms", delta * 1000.0);
        ui.TextV("CPU: {0:0.0}%", _cpuPercent);
        ui.TextV("Cache Build Count: {0}", _cacheBuildCount);
        ui.TextV("Layer Body Draw Count: {0}", _layerBodyDrawCount);
        ui.Text("Visual check: scrolled cards and clip probe body must not show black/blank holes.");

        ui.EndWindow();
    }

    private void TickBenchmark(double delta, BenchPhase phase)
    {
        if (_phaseIndex > _finalPhaseIndex)
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
            "{{\"phase\":{0},\"name\":\"{1}\",\"layers\":{2},\"density\":{3},\"scrollSpeed\":{4:0.###},\"avgFps\":{5:0.###},\"avgCpu\":{6:0.###},\"cacheBuildCount\":{7},\"layerBodyDrawCount\":{8},\"samples\":{9}}}",
            _phaseIndex,
            PhaseName(phase),
            _layerCount,
            _densityPerLayer,
            _scrollSpeed,
            avgFps,
            avgCpu,
            _cacheBuildCount,
            _layerBodyDrawCount,
            _samples));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _fpsSum = 0d;
        _cpuSum = 0d;
        _samples = 0;
        _cacheBuildCount = 0;
        _layerBodyDrawCount = 0;

        if (_phaseIndex > _finalPhaseIndex)
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

        const float startX = 20f;
        const float startY = 20f;
        const float gapX = 12f;
        const float gapY = 12f;
        const float layerW = 250f;
        const float layerH = 168f;
        const int columns = 5;

        for (var id = 1; id <= _layerCount; id++)
        {
            var index = id - 1;
            var col = index % columns;
            var row = index / columns;
            var x = startX + col * (layerW + gapX);
            var y = startY + row * (layerH + gapY);
            var hue = (uint)((id * 37) & 0xFF);
            var color = new UiColor(0xFF000000 | (hue << 16) | ((255u - hue) << 8) | 180u);

            _layers.Add(new LayerInfo
            {
                Id = id,
                Name = $"S{id:00}",
                Position = new UiVector2(x, y),
                Size = new UiVector2(layerW, layerH),
                HeaderColor = color,
                Density = _densityPerLayer
            });
        }
    }

    private float GetVirtualCanvasHeight()
    {
        if (_layers.Count == 0)
        {
            return 600f;
        }

        var maxY = 0f;
        for (var i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            maxY = MathF.Max(maxY, layer.Position.Y + layer.Size.Y);
        }
        return maxY + 40f;
    }

    private float GetVirtualScrollMax()
    {
        const float visibleEstimate = 650f;
        return MathF.Max(1f, GetVirtualCanvasHeight() - visibleEstimate);
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

    private static string PhaseName(BenchPhase phase) => phase switch
    {
        BenchPhase.DynamicScroll => "dynamic-scroll",
        BenchPhase.StaticScroll => "static-scroll",
        BenchPhase.StaticSlidingClip => "static-sliding-clip",
        _ => "unknown",
    };

    private static double ReadPhaseSeconds()
    {
        return BenchOptions.ReadDouble("DUXEL_SCROLL_STATIC_LAYER_PHASE_SECONDS", 2d, minExclusive: 0.1d);
    }

    private static int ReadLayerCount()
    {
        return BenchOptions.ReadInt("DUXEL_SCROLL_STATIC_LAYER_LAYERS", 60, minInclusive: 5, maxInclusive: 160);
    }

    private static int ReadDensityPerLayer()
    {
        return BenchOptions.ReadInt("DUXEL_SCROLL_STATIC_LAYER_DENSITY", 1800, minInclusive: 200, maxInclusive: 12000);
    }

    private static float ReadScrollSpeed()
    {
        return (float)BenchOptions.ReadDouble("DUXEL_SCROLL_STATIC_LAYER_SPEED", 420d, minExclusive: 1d, maxInclusive: 2200d);
    }

    private static int ReadStartPhaseIndex()
    {
        return BenchOptions.ReadInt("DUXEL_SCROLL_STATIC_LAYER_START_PHASE", 0, minInclusive: 0, maxInclusive: 2);
    }

    private static int ReadPhaseCount()
    {
        return BenchOptions.ReadInt("DUXEL_SCROLL_STATIC_LAYER_PHASE_COUNT", 3, minInclusive: 1, maxInclusive: 3);
    }
}
