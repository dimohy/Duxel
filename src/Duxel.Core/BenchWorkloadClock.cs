namespace Duxel.Core;

public readonly record struct BenchWorkloadStep(bool Running, bool Complete, long TimestampTicks, double DeltaSeconds);

/// <summary>A monotonic workload epoch that starts only after preparation is ready.</summary>
public sealed class BenchWorkloadClock
{
    private readonly long _frequency;
    private readonly long _durationTicks;
    private long _lastTimestamp;
    private bool _observed;
    private bool _complete;

    public BenchWorkloadClock(long frequency, double durationSeconds)
    {
        if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        _frequency = frequency;
        _durationTicks = checked((long)Math.Ceiling(durationSeconds * frequency));
        if (_durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
    }

    public bool Started { get; private set; }
    public long StartTimestampTicks { get; private set; }
    public long DeadlineTimestampTicks { get; private set; }
    public double DurationSeconds => _durationTicks / (double)_frequency;

    public BenchWorkloadStep Advance(long timestampTicks, long readyTimestampTicks)
    {
        if (timestampTicks < 0 || _observed && timestampTicks < _lastTimestamp)
            throw new ArgumentOutOfRangeException(nameof(timestampTicks), "The workload clock must be monotonic.");
        var previous = _lastTimestamp;
        _lastTimestamp = timestampTicks;
        _observed = true;
        if (!Started)
        {
            if (timestampTicks < readyTimestampTicks) return new(false, false, timestampTicks, 0);
            StartTimestampTicks = timestampTicks;
            DeadlineTimestampTicks = checked(timestampTicks + _durationTicks);
            Started = true;
            return new(true, false, timestampTicks, 0);
        }
        if (_complete || timestampTicks >= DeadlineTimestampTicks)
        {
            _complete = true;
            return new(false, true, timestampTicks, 0);
        }
        return new(true, false, timestampTicks, (timestampTicks - previous) / (double)_frequency);
    }

    public bool Contains(long sourceTimestampTicks)
        => Started && sourceTimestampTicks >= StartTimestampTicks && sourceTimestampTicks < DeadlineTimestampTicks;
}
