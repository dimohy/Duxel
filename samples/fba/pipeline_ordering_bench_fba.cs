// FBA: focused pipeline ordering bench — alternating, grouped, copy-merge channel, and draw-list channel solid/text draws
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Pipeline Ordering Bench (FBA)",
        Width = 1280,
        Height = 760,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new PipelineOrderingBenchScreen()
});

public sealed class PipelineOrderingBenchScreen : UiScreen
{
    private enum PhaseMode
    {
        Alternating,
        Grouped,
        Channelized,
        ChannelDrawLists,
    }

    private readonly record struct PhaseSpec(string Name, PhaseMode Mode);

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_PIPELINE_ORDER_BENCH_OUT");
    private readonly int _itemCount = BenchOptions.ReadInt("DUXEL_PIPELINE_ORDER_ITEMS", 240, minInclusive: 16, maxInclusive: 2000);
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_PIPELINE_ORDER_PHASE_SECONDS", 1.2d, minExclusive: 0.2d);
    private readonly double _warmupSeconds = BenchOptions.ReadDouble("DUXEL_PIPELINE_ORDER_WARMUP_SECONDS", 0.25d, minExclusive: 0d);
    private readonly List<PhaseSpec> _phases =
    [
        new PhaseSpec("alternating-solid-text", PhaseMode.Alternating),
        new PhaseSpec("grouped-solid-then-text", PhaseMode.Grouped),
        new PhaseSpec("channelized-solid-text", PhaseMode.Channelized),
        new PhaseSpec("channel-drawlists-solid-text", PhaseMode.ChannelDrawLists),
    ];
    private readonly List<string> _benchRecords = [];
    private readonly UiFpsCounter _fpsCounter = new(0.25d);

    private int _phaseIndex;
    private double _phaseElapsed;
    private double _phaseMeasuredSeconds;
    private int _phaseSampleCount;
    private double _lastWallTime;
    private float _fps;

    public override void Render(UiImmediateContext ui)
    {
        var wallNow = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var delta = _lastWallTime == 0d ? 0.016d : Math.Max(0.000001d, wallNow - _lastWallTime);
        _lastWallTime = wallNow;
        _fps = _fpsCounter.Tick(delta).Fps;

        TickBenchmark(ui, delta);
        DrawScene(ui);
        DuxelApp.RequestFrame();
    }

    private void TickBenchmark(UiImmediateContext ui, double delta)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath) || _phaseIndex >= _phases.Count)
        {
            return;
        }

        _phaseElapsed += delta;
        if (_phaseElapsed >= _warmupSeconds && delta > 0d)
        {
            _phaseMeasuredSeconds += delta;
            _phaseSampleCount++;
        }

        if (_phaseElapsed < _warmupSeconds + _phaseSeconds)
        {
            return;
        }

        var avgFps = _phaseMeasuredSeconds > 0d ? _phaseSampleCount / _phaseMeasuredSeconds : 0d;
        var phase = _phases[_phaseIndex];
        _benchRecords.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"name\":\"{1}\",\"grouped\":{2},\"items\":{3},\"avgFps\":{4:0.###},\"samples\":{5}}}",
            _phaseIndex,
            phase.Name,
            phase.Mode is not PhaseMode.Alternating ? "true" : "false",
            _itemCount,
            avgFps,
            _phaseSampleCount));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _phaseMeasuredSeconds = 0d;
        _phaseSampleCount = 0;

        if (_phaseIndex >= _phases.Count)
        {
            WriteBenchmarkOutput();
            Environment.Exit(0);
            return;
        }

        ui.MarkAllLayersDirty();
    }

    private void WriteBenchmarkOutput()
    {
        var json = $"{{\"phaseSeconds\":{_phaseSeconds.ToString(CultureInfo.InvariantCulture)},\"warmupSeconds\":{_warmupSeconds.ToString(CultureInfo.InvariantCulture)},\"items\":{_itemCount},\"records\":[{string.Join(',', _benchRecords)}]}}";
        File.WriteAllText(_benchOutputPath!, json);
    }

    private void DrawScene(UiImmediateContext ui)
    {
        ui.SetNextWindowPos(new UiVector2(18f, 18f));
        ui.SetNextWindowSize(new UiVector2(1240f, 700f));
        ui.BeginWindow("Pipeline Ordering");

        ui.TextV("Phase: {0}", _phaseIndex < _phases.Count ? _phases[_phaseIndex].Name : "done");
        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Warmup: {0:0.00}s", _warmupSeconds);
        ui.TextV("Items: {0}", _itemCount);

        var origin = ui.GetCursorScreenPos();
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        var mode = _phaseIndex < _phases.Count ? _phases[_phaseIndex].Mode : PhaseMode.Alternating;

        if (mode is PhaseMode.Grouped)
        {
            DrawSolids(drawList, white, origin);
            DrawLabels(drawList, origin);
        }
        else if (mode is PhaseMode.Channelized or PhaseMode.ChannelDrawLists)
        {
            DrawChannelized(drawList, white, origin, mergeAsDrawLists: mode is PhaseMode.ChannelDrawLists);
        }
        else
        {
            for (var i = 0; i < _itemCount; i++)
            {
                DrawSolid(drawList, white, origin, i);
                DrawLabel(drawList, origin, i);
            }
        }

        ui.EndWindow();
    }

    private void DrawChannelized(UiDrawListBuilder drawList, UiTextureId white, UiVector2 origin, bool mergeAsDrawLists)
    {
        drawList.Split(2);
        drawList.SetCurrentChannel(0);
        for (var i = 0; i < _itemCount; i++)
        {
            DrawSolid(drawList, white, origin, i);
        }

        drawList.SetCurrentChannel(1);
        for (var i = 0; i < _itemCount; i++)
        {
            DrawLabel(drawList, origin, i);
        }

        if (mergeAsDrawLists)
        {
            drawList.MergeChannelsAsDrawLists();
        }
        else
        {
            drawList.Merge();
        }
    }

    private void DrawSolids(UiDrawListBuilder drawList, UiTextureId white, UiVector2 origin)
    {
        for (var i = 0; i < _itemCount; i++)
        {
            DrawSolid(drawList, white, origin, i);
        }
    }

    private void DrawLabels(UiDrawListBuilder drawList, UiVector2 origin)
    {
        for (var i = 0; i < _itemCount; i++)
        {
            DrawLabel(drawList, origin, i);
        }
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
        drawList.AddText(new UiVector2(x, y), new UiColor(0xFFFFFFFF), $"P{index:000}");
    }
}
