// FBA: focused dynamic widget-row ordering bench — alternating, grouped, and explicit-channel widget draws
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
        Title = "Duxel Dynamic Widget Ordering Bench (FBA)",
        Width = 1480,
        Height = 900,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new DynamicWidgetOrderingBenchScreen()
});

public sealed class DynamicWidgetOrderingBenchScreen : UiScreen
{
    private enum PhaseMode
    {
        Alternating,
        Grouped,
        Channelized,
        ChannelDrawLists,
    }

    private readonly record struct PhaseSpec(string Name, PhaseMode Mode);

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_DYN_WIDGET_ORDER_BENCH_OUT");
    private readonly int _itemCount = BenchOptions.ReadInt("DUXEL_DYN_WIDGET_ORDER_ITEMS", 180, minInclusive: 24, maxInclusive: 1200);
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS", 1.0d, minExclusive: 0.2d);
    private readonly double _warmupSeconds = BenchOptions.ReadDouble("DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS", 0.25d, minExclusive: 0d);
    private readonly bool _rowClips = ReadBool("DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS", defaultValue: true);
    private readonly List<PhaseSpec> _phases =
    [
        new PhaseSpec("alternating-widget-row", PhaseMode.Alternating),
        new PhaseSpec("grouped-widget-solids-then-text", PhaseMode.Grouped),
        new PhaseSpec("channelized-widget-solids-text", PhaseMode.Channelized),
        new PhaseSpec("channel-drawlists-widget-solids-text", PhaseMode.ChannelDrawLists),
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
            "{{\"phase\":{0},\"name\":\"{1}\",\"grouped\":{2},\"items\":{3},\"rowClips\":{4},\"avgFps\":{5:0.###},\"samples\":{6}}}",
            _phaseIndex,
            phase.Name,
            phase.Mode is not PhaseMode.Alternating ? "true" : "false",
            _itemCount,
            _rowClips ? "true" : "false",
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
        var json = $"{{\"phaseSeconds\":{_phaseSeconds.ToString(CultureInfo.InvariantCulture)},\"warmupSeconds\":{_warmupSeconds.ToString(CultureInfo.InvariantCulture)},\"items\":{_itemCount},\"rowClips\":{(_rowClips ? "true" : "false")},\"records\":[{string.Join(',', _benchRecords)}]}}";
        File.WriteAllText(_benchOutputPath!, json);
    }

    private void DrawScene(UiImmediateContext ui)
    {
        ui.SetNextWindowPos(new UiVector2(18f, 18f));
        ui.SetNextWindowSize(new UiVector2(1440f, 840f));
        ui.BeginWindow("Dynamic Widget Ordering");

        ui.TextV("Phase: {0}", _phaseIndex < _phases.Count ? _phases[_phaseIndex].Name : "done");
        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Warmup: {0:0.00}s", _warmupSeconds);
        ui.TextV("Items: {0}", _itemCount);
        ui.TextV("Row clips: {0}", _rowClips ? "ON" : "OFF");

        var origin = ui.GetCursorScreenPos();
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        var mode = _phaseIndex < _phases.Count ? _phases[_phaseIndex].Mode : PhaseMode.Alternating;
        var salt = (int)(ui.GetTime() * 60d) & 0x3FF;

        if (mode is PhaseMode.Grouped)
        {
            DrawWidgetSolids(drawList, white, origin);
            DrawWidgetLabels(drawList, origin, salt);
        }
        else if (mode is PhaseMode.Channelized or PhaseMode.ChannelDrawLists)
        {
            DrawChannelized(drawList, white, origin, salt, mergeAsDrawLists: mode is PhaseMode.ChannelDrawLists);
        }
        else
        {
            for (var i = 0; i < _itemCount; i++)
            {
                DrawWidgetSolids(drawList, white, origin, i);
                DrawWidgetLabel(drawList, origin, i, salt);
            }
        }

        ui.EndWindow();
    }

    private void DrawChannelized(UiDrawListBuilder drawList, UiTextureId white, UiVector2 origin, int salt, bool mergeAsDrawLists)
    {
        drawList.Split(2);
        drawList.SetCurrentChannel(0);
        DrawWidgetSolids(drawList, white, origin);

        drawList.SetCurrentChannel(1);
        DrawWidgetLabels(drawList, origin, salt);

        if (mergeAsDrawLists)
        {
            drawList.MergeChannelsAsDrawLists();
        }
        else
        {
            drawList.Merge();
        }
    }

    private void DrawWidgetSolids(UiDrawListBuilder drawList, UiTextureId white, UiVector2 origin)
    {
        for (var i = 0; i < _itemCount; i++)
        {
            DrawWidgetSolids(drawList, white, origin, i);
        }
    }

    private void DrawWidgetLabels(UiDrawListBuilder drawList, UiVector2 origin, int salt)
    {
        for (var i = 0; i < _itemCount; i++)
        {
            DrawWidgetLabel(drawList, origin, i, salt);
        }
    }

    private void DrawWidgetSolids(UiDrawListBuilder drawList, UiTextureId white, UiVector2 origin, int index)
    {
        var row = GetItemRect(origin, index);
        var clip = _rowClips ? row : default;
        var stateColor = (index % 4) switch
        {
            0 => new UiColor(0xFF2F8CCC),
            1 => new UiColor(0xFF38A16B),
            2 => new UiColor(0xFFE1A443),
            _ => new UiColor(0xFFC66CD8),
        };
        var bg = (index & 1) == 0 ? new UiColor(0xF01C2129) : new UiColor(0xF0252B34);
        AddRectFilled(drawList, row, bg, white, clip);
        AddRectFilled(drawList, new UiRect(row.X + 3f, row.Y + 3f, 4f, row.Height - 6f), stateColor, white, clip);

        var progressWidth = MathF.Max(8f, (row.Width - 82f) * ((index % 17) + 1f) / 18f);
        AddRectFilled(
            drawList,
            new UiRect(row.X + 74f, row.Y + row.Height - 7f, progressWidth, 3f),
            new UiColor(0xAA78D5FF),
            white,
            clip);

        var indicatorCenter = new UiVector2(row.X + row.Width - 14f, row.Y + row.Height * 0.5f);
        if (_rowClips)
        {
            drawList.AddCircleFilled(indicatorCenter, 4.2f, stateColor, white, clip, segments: 10);
        }
        else
        {
            drawList.AddCircleFilled(indicatorCenter, 4.2f, stateColor, white, segments: 10);
        }
    }

    private void DrawWidgetLabel(UiDrawListBuilder drawList, UiVector2 origin, int index, int salt)
    {
        var row = GetItemRect(origin, index);
        if (_rowClips)
        {
            drawList.PushClipRect(row, true);
        }

        drawList.AddText(
            new UiVector2(row.X + 12f, row.Y + 5f),
            new UiColor(0xFFF3F7FA),
            $"W{index:000}");
        drawList.AddText(
            new UiVector2(row.X + 75f, row.Y + 5f),
            new UiColor(0xFFDDE7EF),
            $"value {(index * 13 + salt) % 997:000}");

        if (_rowClips)
        {
            drawList.PopClipRect();
        }
    }

    private UiRect GetItemRect(UiVector2 origin, int index)
    {
        const int columns = 6;
        const float cellWidth = 224f;
        const float cellHeight = 24f;
        var col = index % columns;
        var row = index / columns;
        return new UiRect(
            origin.X + 8f + col * cellWidth,
            origin.Y + 8f + row * cellHeight,
            cellWidth - 10f,
            cellHeight - 4f);
    }

    private static void AddRectFilled(UiDrawListBuilder drawList, UiRect rect, UiColor color, UiTextureId white, UiRect clip)
    {
        if (clip.Width > 0f && clip.Height > 0f)
        {
            drawList.AddRectFilled(rect, color, white, clip);
        }
        else
        {
            drawList.AddRectFilled(rect, color, white);
        }
    }

    private static bool ReadBool(string name, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        var value = raw.Trim();
        if (value == "1"
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value == "0"
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return defaultValue;
    }
}
