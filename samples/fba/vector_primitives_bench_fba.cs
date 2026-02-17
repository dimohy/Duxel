// FBA: Vector Primitives 전용 벤치 (라인/사각형/원)
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
        Title = "Duxel Vector Primitives Bench (FBA)",
        Width = 1600,
        Height = 920,
        VSync = false,
    },
    Screen = new VectorPrimitivesBenchScreen(),
});

public sealed class VectorPrimitivesBenchScreen : UiScreen
{
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_VECTOR_BENCH_OUT");
    private readonly int[] _phasePrimitiveCounts = ReadPrimitiveCounts();
    private readonly double _phaseSeconds = ReadPhaseSeconds();

    private readonly List<string> _records = [];
    private int _phaseIndex;
    private double _phaseElapsed;
    private double _phaseFpsSum;
    private int _phaseSamples;

    private double _lastTime;
    private float _fps;
    private int _fpsFrames;
    private double _fpsAccum;

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0d, 0.05d);
        _lastTime = now;

        UpdateFps(delta);
        TickBench(delta);

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        DrawInfoWindow(ui, bounds);
        DrawVectorWindow(ui, bounds, now);

        DuxelApp.RequestFrame();
    }

    private static int[] ReadPrimitiveCounts()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VECTOR_BENCH_COUNTS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [6000, 12000, 24000];
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<int>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out var value) && value >= 1000)
            {
                list.Add(value);
            }
        }

        return list.Count > 0 ? list.ToArray() : [6000, 12000, 24000];
    }

    private static double ReadPhaseSeconds()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VECTOR_BENCH_PHASE_SECONDS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1.2d;
        }

        return double.TryParse(raw, out var value) && value >= 0.8d ? value : 1.2d;
    }

    private void UpdateFps(double delta)
    {
        _fpsAccum += delta;
        _fpsFrames++;
        if (_fpsAccum < 0.25d)
        {
            return;
        }

        _fps = (float)(_fpsFrames / _fpsAccum);
        _fpsFrames = 0;
        _fpsAccum = 0d;
    }

    private void TickBench(double delta)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            return;
        }

        if (_phaseIndex >= _phasePrimitiveCounts.Length)
        {
            return;
        }

        _phaseElapsed += delta;
        if (_fps > 0f)
        {
            _phaseFpsSum += _fps;
            _phaseSamples++;
        }

        if (_phaseElapsed < _phaseSeconds)
        {
            return;
        }

        var avgFps = _phaseSamples > 0 ? _phaseFpsSum / _phaseSamples : 0d;
        var primitives = _phasePrimitiveCounts[_phaseIndex];
        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"primitives\":{1},\"avgFps\":{2:0.###},\"samples\":{3}}}",
            _phaseIndex,
            primitives,
            avgFps,
            _phaseSamples));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _phaseFpsSum = 0d;
        _phaseSamples = 0;

        if (_phaseIndex >= _phasePrimitiveCounts.Length)
        {
            var json = $"{{\"phaseSeconds\":{_phaseSeconds.ToString(CultureInfo.InvariantCulture)},\"records\":[{string.Join(',', _records)}]}}";
            File.WriteAllText(_benchOutputPath!, json);
            Environment.Exit(0);
        }
    }

    private void DrawInfoWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 14f, bounds.Y + 14f));
        ui.SetNextWindowSize(new UiVector2(330f, 180f));
        ui.BeginWindow("Vector Bench");

        var phaseTotal = Math.Max(1, _phasePrimitiveCounts.Length);
        var phaseDisplay = Math.Min(_phaseIndex + 1, phaseTotal);
        var currentPrims = _phasePrimitiveCounts[Math.Clamp(_phaseIndex, 0, _phasePrimitiveCounts.Length - 1)];

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Phase: {0}/{1}", phaseDisplay, phaseTotal);
        ui.TextV("Primitives: {0}", currentPrims);
        ui.Text("Workload: line + rect + circle");

        ui.EndWindow();
    }

    private void DrawVectorWindow(UiImmediateContext ui, UiRect bounds, double now)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 360f, bounds.Y + 14f));
        ui.SetNextWindowSize(new UiVector2(MathF.Max(700f, bounds.Width - 374f), MathF.Max(540f, bounds.Height - 28f)));
        ui.BeginWindow("Vector Canvas");

        var canvasPos = ui.GetCursorScreenPos();
        var canvasSize = ui.GetContentRegionAvail();
        var canvas = new UiRect(canvasPos.X, canvasPos.Y, MathF.Max(1f, canvasSize.X), MathF.Max(1f, canvasSize.Y));
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;

        drawList.AddRectFilled(canvas, new UiColor(0xFF111318), white, canvas);

        var primitiveCount = _phasePrimitiveCounts[Math.Clamp(_phaseIndex, 0, _phasePrimitiveCounts.Length - 1)];
        var tick = (float)now;

        for (var i = 0; i < primitiveCount; i++)
        {
            var u = (i * 37 % 997) / 997f;
            var v = (i * 53 % 991) / 991f;
            var x = canvas.X + 8f + u * MathF.Max(1f, canvas.Width - 16f);
            var y = canvas.Y + 8f + v * MathF.Max(1f, canvas.Height - 16f);

            var wobbleX = MathF.Sin((i * 0.013f) + tick * 0.9f) * 6f;
            var wobbleY = MathF.Cos((i * 0.017f) + tick * 1.1f) * 6f;

            var x2 = Math.Clamp(x + wobbleX + 12f, canvas.X + 2f, canvas.X + canvas.Width - 2f);
            var y2 = Math.Clamp(y + wobbleY + 12f, canvas.Y + 2f, canvas.Y + canvas.Height - 2f);

            var lineColor = new UiColor((uint)(0xAA67B7FFu + (uint)((i * 13) & 0x2Fu)));
            drawList.AddLine(new UiVector2(x, y), new UiVector2(x2, y2), lineColor, 1.1f);

            var rectW = 2f + (i % 5);
            var rectH = 2f + (i % 4);
            drawList.AddRectFilled(new UiRect(x, y, rectW, rectH), new UiColor((uint)(0x8894E06Cu + (uint)((i * 7) & 0x3Fu))), white, canvas);

            if ((i & 7) == 0)
            {
                var radius = 2.2f + (i % 6) * 0.35f;
                drawList.AddCircle(new UiVector2(x2, y2), radius, new UiColor((uint)(0xCCF0D56Bu + (uint)((i * 11) & 0x1Fu))), 10, 1.0f);
            }
        }

        ui.Dummy(new UiVector2(canvas.Width, canvas.Height));
        ui.EndWindow();
    }
}
