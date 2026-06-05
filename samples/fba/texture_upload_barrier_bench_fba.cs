// FBA: focused texture upload/barrier bench — full updates, same-texture batched regions, and many-texture updates
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
        Title = "Duxel Texture Upload Barrier Bench (FBA)",
        Width = 1280,
        Height = 760,
        VSync = false
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new TextureUploadBarrierBenchScreen()
});

public sealed class TextureUploadBarrierBenchScreen : UiScreen
{
    private enum PhaseMode
    {
        FullSingleTexture,
        SameTextureRegions,
        ManyTextureRegions,
    }

    private readonly record struct PhaseSpec(string Name, PhaseMode Mode);

    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_TEXTURE_UPLOAD_BENCH_OUT");
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS", 1.0d, minExclusive: 0.1d);
    private readonly int _textureSize = BenchOptions.ReadInt("DUXEL_TEXTURE_UPLOAD_SIZE", 256, minInclusive: 32, maxInclusive: 2048);
    private readonly int _regionSize = BenchOptions.ReadInt("DUXEL_TEXTURE_UPLOAD_REGION_SIZE", 32, minInclusive: 4, maxInclusive: 512);
    private readonly int _regionCount = BenchOptions.ReadInt("DUXEL_TEXTURE_UPLOAD_REGIONS", 16, minInclusive: 1, maxInclusive: 256);
    private readonly int _textureCount = BenchOptions.ReadInt("DUXEL_TEXTURE_UPLOAD_TEXTURES", 16, minInclusive: 1, maxInclusive: 128);
    private readonly int _warmupFrames = BenchOptions.ReadInt("DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES", 4, minInclusive: 0, maxInclusive: 120);
    private readonly List<PhaseSpec> _phases =
    [
        new PhaseSpec("full-single-texture", PhaseMode.FullSingleTexture),
        new PhaseSpec("same-texture-batched-regions", PhaseMode.SameTextureRegions),
        new PhaseSpec("many-texture-region-updates", PhaseMode.ManyTextureRegions),
    ];
    private readonly List<string> _records = [];
    private readonly UiTextureId[] _textureIds;
    private readonly byte[] _fullPixels;
    private readonly byte[][] _regionPixels;
    private readonly int _effectiveRegionSize;
    private readonly int _regionsPerRow;

    private int _phaseIndex;
    private int _frameCounter;
    private int _warmupFramesRemaining;
    private bool _texturesCreated;
    private double _phaseElapsed;
    private double _fpsSum;
    private int _samples;
    private double _lastTime;
    private float _fps;

    public TextureUploadBarrierBenchScreen()
    {
        _effectiveRegionSize = Math.Min(_regionSize, _textureSize);
        _regionsPerRow = Math.Max(1, _textureSize / _effectiveRegionSize);
        var uniqueRegionCapacity = Math.Max(1, _regionsPerRow * _regionsPerRow);
        if (_regionCount > uniqueRegionCapacity)
        {
            _regionCount = uniqueRegionCapacity;
        }

        _textureIds = new UiTextureId[_textureCount];
        for (var i = 0; i < _textureIds.Length; i++)
        {
            _textureIds[i] = new UiTextureId((nuint)(2_000_000_000 + i));
        }

        _fullPixels = new byte[checked(_textureSize * _textureSize * 4)];
        _regionPixels = new byte[_regionCount][];
        for (var i = 0; i < _regionPixels.Length; i++)
        {
            _regionPixels[i] = new byte[checked(_effectiveRegionSize * _effectiveRegionSize * 4)];
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

        EnsureTextures(ui);
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

    private void EnsureTextures(UiImmediateContext ui)
    {
        if (_texturesCreated)
        {
            return;
        }

        FillFullPixels(0);
        for (var i = 0; i < _textureIds.Length; i++)
        {
            ui.QueueTextureUpdate(new UiTextureUpdate(
                UiTextureUpdateKind.Create,
                _textureIds[i],
                UiTextureFormat.Rgba8Unorm,
                _textureSize,
                _textureSize,
                _fullPixels.ToArray()));
        }

        _texturesCreated = true;
    }

    private void ApplyPhaseTextureUpdates(UiImmediateContext ui)
    {
        if (_phaseIndex >= _phases.Count)
        {
            return;
        }

        var phase = _phases[_phaseIndex];
        switch (phase.Mode)
        {
            case PhaseMode.FullSingleTexture:
                FillFullPixels(_frameCounter);
                ui.QueueTextureUpdate(new UiTextureUpdate(
                    UiTextureUpdateKind.Update,
                    _textureIds[0],
                    UiTextureFormat.Rgba8Unorm,
                    _textureSize,
                    _textureSize,
                    _fullPixels));
                break;
            case PhaseMode.SameTextureRegions:
                QueueSameTextureRegionUpdates(ui);
                break;
            case PhaseMode.ManyTextureRegions:
                QueueManyTextureRegionUpdates(ui);
                break;
            default:
                throw new InvalidOperationException($"Unsupported phase: {phase.Mode}.");
        }
    }

    private void QueueSameTextureRegionUpdates(UiImmediateContext ui)
    {
        for (var i = 0; i < _regionCount; i++)
        {
            FillRegionPixels(i, _frameCounter);
            var (x, y) = GetRegionPosition(i);
            ui.QueueTextureUpdate(new UiTextureUpdate(
                UiTextureUpdateKind.Update,
                _textureIds[0],
                UiTextureFormat.Rgba8Unorm,
                _textureSize,
                _textureSize,
                _regionPixels[i],
                x,
                y,
                _effectiveRegionSize,
                _effectiveRegionSize));
        }
    }

    private void QueueManyTextureRegionUpdates(UiImmediateContext ui)
    {
        var count = Math.Min(_textureCount, _regionCount);
        for (var i = 0; i < count; i++)
        {
            FillRegionPixels(i, _frameCounter);
            var (x, y) = GetRegionPosition(i);
            ui.QueueTextureUpdate(new UiTextureUpdate(
                UiTextureUpdateKind.Update,
                _textureIds[i],
                UiTextureFormat.Rgba8Unorm,
                _textureSize,
                _textureSize,
                _regionPixels[i],
                x,
                y,
                _effectiveRegionSize,
                _effectiveRegionSize));
        }
    }

    private void DrawScene(UiImmediateContext ui)
    {
        ui.SetNextWindowPos(new UiVector2(16f, 16f));
        ui.SetNextWindowSize(new UiVector2(1248f, 710f));
        ui.BeginWindow("Texture Upload Barrier Bench");

        var phaseName = _phaseIndex < _phases.Count ? _phases[_phaseIndex].Name : "done";
        ui.TextV("Phase: {0}", phaseName);
        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Texture: {0}x{0}, region: {1}x{1}, regions: {2}, textures: {3}",
            _textureSize,
            _effectiveRegionSize,
            _regionCount,
            _textureCount);

        var drawList = ui.GetWindowDrawList();
        var origin = ui.GetCursorScreenPos();
        var previewSize = 96f;
        var cols = 8;
        var previewCount = Math.Min(_textureIds.Length, 24);
        for (var i = 0; i < previewCount; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var x = origin.X + col * (previewSize + 8f);
            var y = origin.Y + row * (previewSize + 24f);
            drawList.AddImage(
                _textureIds[i],
                new UiVector2(x, y),
                new UiVector2(x + previewSize, y + previewSize),
                new UiVector2(0f, 0f),
                new UiVector2(1f, 1f),
                new UiColor(0xFFFFFFFF));
            drawList.AddText(new UiVector2(x, y + previewSize + 4f), new UiColor(0xFFFFFFFF), $"T{i:00}");
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
        var updatesPerFrame = phase.Mode switch
        {
            PhaseMode.FullSingleTexture => 1,
            PhaseMode.SameTextureRegions => _regionCount,
            PhaseMode.ManyTextureRegions => Math.Min(_textureCount, _regionCount),
            _ => 0,
        };
        var expectedTransitionsPerFrame = phase.Mode switch
        {
            PhaseMode.FullSingleTexture => 2,
            PhaseMode.SameTextureRegions => 2,
            PhaseMode.ManyTextureRegions => updatesPerFrame * 2,
            _ => 0,
        };
        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"name\":\"{1}\",\"updatesPerFrame\":{2},\"expectedTransitionsPerFrame\":{3},\"textureSize\":{4},\"regionSize\":{5},\"avgFps\":{6:0.###},\"samples\":{7}}}",
            _phaseIndex,
            phase.Name,
            updatesPerFrame,
            expectedTransitionsPerFrame,
            _textureSize,
            _effectiveRegionSize,
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
            "{{\"phaseSeconds\":{0},\"warmupFrames\":{1},\"textureSize\":{2},\"regionSize\":{3},\"regionCount\":{4},\"textureCount\":{5},\"records\":[{6}]}}",
            _phaseSeconds,
            _warmupFrames,
            _textureSize,
            _effectiveRegionSize,
            _regionCount,
            _textureCount,
            string.Join(',', _records));
        File.WriteAllText(_benchOutputPath!, json);
    }

    private (int X, int Y) GetRegionPosition(int index)
    {
        var slot = index % Math.Max(1, _regionsPerRow * _regionsPerRow);
        var x = (slot % _regionsPerRow) * _effectiveRegionSize;
        var y = (slot / _regionsPerRow) * _effectiveRegionSize;
        return (x, y);
    }

    private void FillFullPixels(int seed)
    {
        for (var y = 0; y < _textureSize; y++)
        {
            for (var x = 0; x < _textureSize; x++)
            {
                var offset = ((y * _textureSize) + x) * 4;
                _fullPixels[offset + 0] = (byte)((x + seed * 3) & 0xFF);
                _fullPixels[offset + 1] = (byte)((y * 2 + seed * 5) & 0xFF);
                _fullPixels[offset + 2] = (byte)((x + y + seed * 7) & 0xFF);
                _fullPixels[offset + 3] = 0xFF;
            }
        }
    }

    private void FillRegionPixels(int regionIndex, int seed)
    {
        var pixels = _regionPixels[regionIndex];
        var r = (byte)((regionIndex * 31 + seed * 5) & 0xFF);
        var g = (byte)((regionIndex * 53 + seed * 3) & 0xFF);
        var b = (byte)((regionIndex * 17 + seed * 11) & 0xFF);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 0xFF;
        }
    }
}
