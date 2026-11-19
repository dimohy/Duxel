using System.Diagnostics;

namespace Duxel.Core;

public readonly record struct UiFpsSample(float Fps, int Frames, double ElapsedSeconds, bool Updated);

public sealed class UiFpsCounter
{
    private readonly double _sampleWindowSeconds;
    private long _lastTimestamp;
    private int _frameCount;
    private double _accumulatedSeconds;
    private float _currentFps;

    public UiFpsCounter(double sampleWindowSeconds = 0.5d)
    {
        _sampleWindowSeconds = sampleWindowSeconds > 0d ? sampleWindowSeconds : 0.5d;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    public float CurrentFps => _currentFps;

    public UiFpsSample Tick() => TickExcludingIdle(0);

    /// <summary>Samples frame cadence without counting time spent explicitly waiting for new work.</summary>
    public UiFpsSample TickExcludingIdle(double idleWaitSeconds)
    {
        var now = Stopwatch.GetTimestamp();
        var deltaSeconds = Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        _lastTimestamp = now;
        return idleWaitSeconds == 0 ? RecordFrame(deltaSeconds) : Tick(deltaSeconds - idleWaitSeconds);
    }

    public UiFpsSample Tick(double deltaSeconds)
    {
        if (deltaSeconds <= 0d)
        {
            return new UiFpsSample(_currentFps, 0, 0d, false);
        }

        return RecordFrame(deltaSeconds);
    }

    private UiFpsSample RecordFrame(double deltaSeconds)
    {
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
        _lastTimestamp = Stopwatch.GetTimestamp();
    }
}
