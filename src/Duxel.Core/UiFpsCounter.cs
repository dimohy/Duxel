using System.Diagnostics;

namespace Duxel.Core;

public readonly record struct UiFpsSample(float Fps, int Frames, double ElapsedSeconds, bool Updated);

public sealed class UiFpsCounter
{
    private readonly double _sampleWindowSeconds;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastTickMilliseconds;
    private int _frameCount;
    private double _accumulatedSeconds;
    private float _currentFps;

    public UiFpsCounter(double sampleWindowSeconds = 0.5d)
    {
        _sampleWindowSeconds = sampleWindowSeconds > 0d ? sampleWindowSeconds : 0.5d;
        _lastTickMilliseconds = _clock.ElapsedMilliseconds;
    }

    public float CurrentFps => _currentFps;

    public UiFpsSample Tick()
    {
        var nowMilliseconds = _clock.ElapsedMilliseconds;
        var deltaMilliseconds = nowMilliseconds - _lastTickMilliseconds;
        _lastTickMilliseconds = nowMilliseconds;
        return Tick(deltaMilliseconds * 0.001d);
    }

    public UiFpsSample Tick(double deltaSeconds)
    {
        if (deltaSeconds <= 0d)
        {
            return new UiFpsSample(_currentFps, 0, 0d, false);
        }

        _frameCount++;
        _accumulatedSeconds += deltaSeconds;
        if (_accumulatedSeconds < _sampleWindowSeconds)
        {
            return new UiFpsSample(_currentFps, _frameCount, _accumulatedSeconds, false);
        }

        _currentFps = (float)(_frameCount / _accumulatedSeconds);
        var sample = new UiFpsSample(_currentFps, _frameCount, _accumulatedSeconds, true);
        _frameCount = 0;
        _accumulatedSeconds = 0d;
        return sample;
    }

    public void Reset()
    {
        _frameCount = 0;
        _accumulatedSeconds = 0d;
        _currentFps = 0f;
        _lastTickMilliseconds = _clock.ElapsedMilliseconds;
    }
}