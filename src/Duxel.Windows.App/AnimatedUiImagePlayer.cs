using System.Diagnostics;
using System.Runtime.Versioning;
using Duxel.Platform.Windows;

namespace Duxel.Core;

[SupportedOSPlatform("windows")]
public sealed class AnimatedUiImagePlayer
{
    private readonly UiImageTexture[] _frames;
    private readonly float[] _durationsSec;
    private readonly bool _isAnimatedGif;
    private int _frameIndex;
    private double _accumulator;
    private long _lastTicks;

    private AnimatedUiImagePlayer(UiImageTexture[] frames, float[] durationsSec, bool isAnimatedGif)
    {
        _frames = frames;
        _durationsSec = durationsSec;
        _isAnimatedGif = isAnimatedGif;
        _lastTicks = Stopwatch.GetTimestamp();
    }

    public bool IsAnimatedGif => _isAnimatedGif;
    public int Width => _frames[0].Width;
    public int Height => _frames[0].Height;

    public static AnimatedUiImagePlayer Load(string path, uint baseTextureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Image file not found.", path);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".gif")
        {
            var single = UiImageTexture.LoadFromFile(path, new UiTextureId((nuint)baseTextureId));
            return new AnimatedUiImagePlayer([single], [0.1f], false);
        }

        var animation = WindowsWicImageCodec.Decode(path);
        var frameCount = animation.Frames.Length;
        if (frameCount == 1)
        {
            var frame = animation.Frames[0];
            var single = new UiImageTexture(new UiTextureId((nuint)baseTextureId), frame.Width, frame.Height, frame.RgbaPixels);
            return new AnimatedUiImagePlayer([single], [0.1f], false);
        }

        var frames = new UiImageTexture[frameCount];
        var durations = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frame = animation.Frames[i];
            frames[i] = new UiImageTexture(new UiTextureId((nuint)(baseTextureId + (uint)i)), frame.Width, frame.Height, frame.RgbaPixels);
            durations[i] = animation.DurationsSec[i];
        }

        return new AnimatedUiImagePlayer(frames, durations, animation.IsAnimated);
    }

    public void Prepare(UiImmediateContext ui, in UiImageEffects effects)
    {
        if (_isAnimatedGif && _frames.Length > 1)
        {
            AdvanceFrame();
        }

        _frames[_frameIndex].Prepare(ui, effects);
    }

    public void DrawInCurrentRegion(UiImmediateContext ui, float zoom, float rotationDeg, float alpha)
    {
        _frames[_frameIndex].DrawInCurrentRegion(ui, zoom, rotationDeg, alpha);
    }

    private void AdvanceFrame()
    {
        var now = Stopwatch.GetTimestamp();
        var deltaSec = (now - _lastTicks) / (double)Stopwatch.Frequency;
        _lastTicks = now;
        _accumulator += deltaSec;

        while (_accumulator >= _durationsSec[_frameIndex])
        {
            _accumulator -= _durationsSec[_frameIndex];
            _frameIndex = (_frameIndex + 1) % _frames.Length;
        }
    }

}
