// FBA: moving static-layer scheduling bench
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
        Title = "Duxel Moving Static Layer Ordering Bench (FBA)",
        Width = 1280,
        Height = 760,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new StaticLayerMovingOrderBenchScreen()
});

public sealed class StaticLayerMovingOrderBenchScreen : UiScreen
{
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT");
    private readonly int _itemCount = BenchOptions.ReadInt("DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS", 240, minInclusive: 16, maxInclusive: 2000);
    private readonly float _moveAmplitude = (float)BenchOptions.ReadDouble("DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE", 96d, minExclusive: 0d);
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS", 1.2d, minExclusive: 0.2d);
    private readonly List<string> _benchRecords = [];
    private readonly UiFpsCounter _fpsCounter = new(0.25d);

    private bool _done;
    private double _elapsed;
    private double _fpsSum;
    private int _fpsSampleCount;
    private double _lastTime;
    private float _fps;

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0d, 0.05d);
        _lastTime = now;
        _fps = _fpsCounter.Tick(delta).Fps;

        TickBenchmark(delta);
        DrawScene(ui, now);
        DuxelApp.RequestFrame();
    }

    private void TickBenchmark(double delta)
    {
        if (_done || string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        _elapsed += delta;
        if (_fps > 0f)
        {
            _fpsSum += _fps;
            _fpsSampleCount++;
        }

        if (_elapsed < _phaseSeconds)
        {
            return;
        }

        var avgFps = _fpsSampleCount > 0 ? _fpsSum / _fpsSampleCount : 0d;
        _benchRecords.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"name\":\"moving-static-layer\",\"items\":{0},\"amplitude\":{1:0.###},\"avgFps\":{2:0.###},\"samples\":{3}}}",
            _itemCount,
            _moveAmplitude,
            avgFps,
            _fpsSampleCount));

        var json = $"{{\"phaseSeconds\":{_phaseSeconds.ToString(CultureInfo.InvariantCulture)},\"records\":[{string.Join(',', _benchRecords)}]}}";
        File.WriteAllText(_benchOutputPath!, json);
        _done = true;
        Environment.Exit(0);
    }

    private void DrawScene(UiImmediateContext ui, double now)
    {
        ui.SetNextWindowPos(new UiVector2(18f, 18f));
        ui.SetNextWindowSize(new UiVector2(1240f, 700f));
        ui.BeginWindow("Moving Static Layer Ordering");

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Items: {0}", _itemCount);
        ui.TextV("Amplitude: {0:0.0}", _moveAmplitude);

        var origin = ui.GetCursorScreenPos();
        var x = MathF.Sin((float)now * 2.4f) * _moveAmplitude;
        var translation = new UiVector2(x, 0f);
        if (ui.BeginLayer("moving-static-order-grid", new UiLayerOptions(StaticCache: true, Opacity: 1f, Translation: translation)))
        {
            var drawList = ui.GetWindowDrawList();
            var white = ui.WhiteTextureId;
            for (var i = 0; i < _itemCount; i++)
            {
                DrawSolid(drawList, white, origin, i);
                DrawLabel(drawList, origin, i);
            }
        }

        ui.EndLayer();
        ui.EndWindow();
    }

    private static void DrawSolid(UiDrawListBuilder drawList, UiTextureId white, UiVector2 origin, int index)
    {
        var cols = 12;
        var col = index % cols;
        var row = index / cols;
        var x = origin.X + 8f + col * 98f;
        var y = origin.Y + 8f + row * 28f;
        var rect = new UiRect(x, y, 88f, 22f);
        var color = (index % 3) switch
        {
            0 => new UiColor(0xFF2E8FBC),
            1 => new UiColor(0xFF3A9F65),
            _ => new UiColor(0xFFA46AC7),
        };
        drawList.AddRectFilled(rect, color, white);
    }

    private static void DrawLabel(UiDrawListBuilder drawList, UiVector2 origin, int index)
    {
        var cols = 12;
        var col = index % cols;
        var row = index / cols;
        var x = origin.X + 13f + col * 98f;
        var y = origin.Y + 11f + row * 28f;
        drawList.AddText(new UiVector2(x, y), new UiColor(0xFFFFFFFF), $"M{index:000}");
    }
}
