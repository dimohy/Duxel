// FBA: focused DirectText page-style texture upload bench.
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
        Title = "Duxel DirectText Page Upload Bench (FBA)",
        Width = 1280,
        Height = 760,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new DirectTextPageUploadBenchScreen()
});

public sealed class DirectTextPageUploadBenchScreen : UiScreen
{
    private enum PhaseMode
    {
        SamePageRegions,
        MultiPageRegions,
    }

    private readonly record struct PhaseSpec(string Name, PhaseMode Mode);

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_DTPAGE_UPLOAD_BENCH_OUT");
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS", 1.0d, minExclusive: 0.1d);
    private readonly int _warmupFrames = BenchOptions.ReadInt("DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES", 4, minInclusive: 0, maxInclusive: 120);
    private readonly List<PhaseSpec> _phases =
    [
        new PhaseSpec("same-page-append-regions", PhaseMode.SamePageRegions),
        new PhaseSpec("multi-page-append-regions", PhaseMode.MultiPageRegions),
    ];
    private readonly List<string> _records = [];
    private readonly UiTextureId[] _pageTextureIds;
    private readonly byte[][] _regionPixels;
    private readonly byte[][] _pageCreatePixels;
    private int _pageSize = BenchOptions.ReadInt("DUXEL_DTPAGE_UPLOAD_PAGE_SIZE", 1024, minInclusive: 128, maxInclusive: 2048);
    private int _regionWidth = BenchOptions.ReadInt("DUXEL_DTPAGE_UPLOAD_REGION_WIDTH", 96, minInclusive: 4, maxInclusive: 512);
    private int _regionHeight = BenchOptions.ReadInt("DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT", 32, minInclusive: 4, maxInclusive: 512);
    private int _regionsPerFrame = BenchOptions.ReadInt("DUXEL_DTPAGE_UPLOAD_REGIONS", 16, minInclusive: 1, maxInclusive: 256);
    private int _pageCount = BenchOptions.ReadInt("DUXEL_DTPAGE_UPLOAD_PAGES", 4, minInclusive: 1, maxInclusive: 64);
    private int _regionsPerRow;
    private int _rowsPerPage;
    private int _regionsPerPage;
    private int _phaseIndex;
    private int _frameCounter;
    private int _warmupFramesRemaining;
    private bool _pagesCreated;
    private double _phaseElapsed;
    private double _fpsSum;
    private int _samples;
    private double _lastTime;
    private float _fps;

    public DirectTextPageUploadBenchScreen()
    {
        _regionWidth = Math.Min(_regionWidth, _pageSize);
        _regionHeight = Math.Min(_regionHeight, _pageSize);
        _regionsPerRow = Math.Max(1, _pageSize / _regionWidth);
        _rowsPerPage = Math.Max(1, _pageSize / _regionHeight);
        _regionsPerPage = Math.Max(1, _regionsPerRow * _rowsPerPage);
        _regionsPerFrame = Math.Min(_regionsPerFrame, _regionsPerPage);

        _pageTextureIds = new UiTextureId[_pageCount];
        for (var i = 0; i < _pageTextureIds.Length; i++)
        {
            _pageTextureIds[i] = new UiTextureId((nuint)(2_200_000_000 + i));
        }

        var regionBytes = checked(_regionWidth * _regionHeight * 4);
        _regionPixels = new byte[Math.Max(_regionsPerFrame, _pageCount)][];
        for (var i = 0; i < _regionPixels.Length; i++)
        {
            _regionPixels[i] = new byte[regionBytes];
        }

        _pageCreatePixels = new byte[_pageCount][];
        for (var i = 0; i < _pageCreatePixels.Length; i++)
        {
            _pageCreatePixels[i] = new byte[regionBytes];
        }

        _warmupFramesRemaining = _warmupFrames;
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0.0001d, 0.05d);
        _lastTime = now;
        _fps = (float)(1d / delta);
        _frameCounter++;

        EnsurePages(ui);
        if (_warmupFramesRemaining > 0)
        {
            _warmupFramesRemaining--;
        }
        else
        {
            ApplyPhaseTextureUpdates(ui);
        }

        DrawScene(ui);
        if (_warmupFramesRemaining <= 0)
        {
            TickBenchmark(delta);
        }

        DuxelApp.RequestFrame();
    }

    private void EnsurePages(UiImmediateContext ui)
    {
        if (_pagesCreated)
        {
            return;
        }

        for (var page = 0; page < _pageTextureIds.Length; page++)
        {
            FillRegionPixels(_pageCreatePixels[page], page, _frameCounter);
            ui.QueueTextureUpdate(new UiTextureUpdate(
                UiTextureUpdateKind.Create,
                _pageTextureIds[page],
                UiTextureFormat.Rgba8Unorm,
                _pageSize,
                _pageSize,
                _pageCreatePixels[page],
                0,
                0,
                _regionWidth,
                _regionHeight));
        }

        _pagesCreated = true;
    }

    private void ApplyPhaseTextureUpdates(UiImmediateContext ui)
    {
        if (_phaseIndex >= _phases.Count)
        {
            return;
        }

        var phase = _phases[_phaseIndex];
        if (phase.Mode is PhaseMode.SamePageRegions)
        {
            QueueSamePageRegionUpdates(ui);
            return;
        }

        QueueMultiPageRegionUpdates(ui);
    }

    private void QueueSamePageRegionUpdates(UiImmediateContext ui)
    {
        var start = (_frameCounter * _regionsPerFrame) % _regionsPerPage;
        for (var i = 0; i < _regionsPerFrame; i++)
        {
            FillRegionPixels(_regionPixels[i], i, _frameCounter);
            var (x, y) = GetRegionPosition(start + i);
            ui.QueueTextureUpdate(new UiTextureUpdate(
                UiTextureUpdateKind.Update,
                _pageTextureIds[0],
                UiTextureFormat.Rgba8Unorm,
                _pageSize,
                _pageSize,
                _regionPixels[i],
                x,
                y,
                _regionWidth,
                _regionHeight));
        }
    }

    private void QueueMultiPageRegionUpdates(UiImmediateContext ui)
    {
        for (var page = 0; page < _pageCount; page++)
        {
            FillRegionPixels(_regionPixels[page], page, _frameCounter);
            var (x, y) = GetRegionPosition(_frameCounter + page);
            ui.QueueTextureUpdate(new UiTextureUpdate(
                UiTextureUpdateKind.Update,
                _pageTextureIds[page],
                UiTextureFormat.Rgba8Unorm,
                _pageSize,
                _pageSize,
                _regionPixels[page],
                x,
                y,
                _regionWidth,
                _regionHeight));
        }
    }

    private void DrawScene(UiImmediateContext ui)
    {
        ui.SetNextWindowPos(new UiVector2(16f, 16f));
        ui.SetNextWindowSize(new UiVector2(1248f, 710f));
        ui.BeginWindow("DirectText Page Upload");

        var phaseName = _phaseIndex < _phases.Count ? _phases[_phaseIndex].Name : "done";
        ui.TextV("Phase: {0}", phaseName);
        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Page: {0}x{0}, region: {1}x{2}, regions/frame: {3}, pages: {4}",
            _pageSize,
            _regionWidth,
            _regionHeight,
            _regionsPerFrame,
            _pageCount);

        var drawList = ui.GetWindowDrawList();
        var origin = ui.GetCursorScreenPos();
        var previewSize = 128f;
        var cols = 6;
        var previewCount = Math.Min(_pageTextureIds.Length, 18);
        for (var i = 0; i < previewCount; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var x = origin.X + col * (previewSize + 10f);
            var y = origin.Y + row * (previewSize + 24f);
            drawList.AddImage(
                _pageTextureIds[i],
                new UiVector2(x, y),
                new UiVector2(x + previewSize, y + previewSize),
                new UiVector2(0f, 0f),
                new UiVector2(1f, 1f),
                new UiColor(0xFFFFFFFF));
            drawList.AddText(new UiVector2(x, y + previewSize + 4f), new UiColor(0xFFFFFFFF), $"P{i:00}");
        }

        ui.EndWindow();
    }

    private void TickBenchmark(double delta)
    {
        if (string.IsNullOrWhiteSpace(_benchOutputPath) || _phaseIndex >= _phases.Count)
        {
            return;
        }

        _phaseElapsed += delta;
        if (_fps > 0f)
        {
            _fpsSum += _fps;
            _samples++;
        }

        if (_phaseElapsed < _phaseSeconds)
        {
            return;
        }

        var avgFps = _samples > 0 ? _fpsSum / _samples : 0d;
        var phase = _phases[_phaseIndex];
        var updatesPerFrame = phase.Mode is PhaseMode.SamePageRegions
            ? _regionsPerFrame
            : _pageCount;
        var expectedTransitionsPerFrame = phase.Mode is PhaseMode.SamePageRegions
            ? 2
            : updatesPerFrame * 2;

        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"name\":\"{1}\",\"updatesPerFrame\":{2},\"expectedTransitionsPerFrame\":{3},\"avgFps\":{4:0.###},\"samples\":{5}}}",
            _phaseIndex,
            phase.Name,
            updatesPerFrame,
            expectedTransitionsPerFrame,
            avgFps,
            _samples));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _fpsSum = 0d;
        _samples = 0;

        if (_phaseIndex >= _phases.Count)
        {
            WriteBenchmarkOutput();
            Environment.Exit(0);
        }
    }

    private void WriteBenchmarkOutput()
    {
        var json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phaseSeconds\":{0},\"warmupFrames\":{1},\"pageSize\":{2},\"regionWidth\":{3},\"regionHeight\":{4},\"regionsPerFrame\":{5},\"pageCount\":{6},\"records\":[{7}]}}",
            _phaseSeconds,
            _warmupFrames,
            _pageSize,
            _regionWidth,
            _regionHeight,
            _regionsPerFrame,
            _pageCount,
            string.Join(',', _records));
        File.WriteAllText(_benchOutputPath!, json);
    }

    private (int X, int Y) GetRegionPosition(int index)
    {
        var slot = index % _regionsPerPage;
        var x = (slot % _regionsPerRow) * _regionWidth;
        var y = (slot / _regionsPerRow) * _regionHeight;
        return (x, y);
    }

    private static void FillRegionPixels(byte[] pixels, int regionIndex, int seed)
    {
        var r = (byte)((regionIndex * 29 + seed * 7) & 0xFF);
        var g = (byte)((regionIndex * 47 + seed * 5) & 0xFF);
        var b = (byte)((regionIndex * 13 + seed * 11) & 0xFF);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 0xFF;
        }
    }
}
