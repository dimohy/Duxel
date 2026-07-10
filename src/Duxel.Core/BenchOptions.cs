using System.Globalization;
using System.Runtime.CompilerServices;

namespace Duxel.Core;

public readonly struct BenchOptionsReader
{
    private readonly string _prefix;

    internal BenchOptionsReader(string prefix)
    {
        _prefix = prefix ?? string.Empty;
    }

    private string Key(string name) => string.Concat(_prefix, name);

    public string String(string name, string defaultValue = "")
        => BenchOptions.ReadString(Key(name), defaultValue);

    public bool Bool(string name, bool defaultValue = false)
        => BenchOptions.ReadBool(Key(name), defaultValue);

    public int Int(string name, int defaultValue, int minInclusive = int.MinValue, int maxInclusive = int.MaxValue)
        => BenchOptions.ReadInt(Key(name), defaultValue, minInclusive, maxInclusive);

    public double Double(string name, double defaultValue, double minExclusive = double.NegativeInfinity, double maxInclusive = double.PositiveInfinity)
        => BenchOptions.ReadDouble(Key(name), defaultValue, minExclusive, maxInclusive);

    public int[] IntCsv(string name, ReadOnlySpan<int> fallback, int minInclusive = int.MinValue, int maxInclusive = int.MaxValue)
        => BenchOptions.ReadIntCsv(Key(name), fallback, minInclusive, maxInclusive);
}

public static class BenchOptions
{
    public static BenchOptionsReader FromEnvironment(string prefix)
        => new(prefix);

    public static string ReadString(string key, string defaultValue = "")
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    public static bool ReadBool(string key, bool defaultValue = false)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "on" or "yes" => true,
            "0" or "false" or "off" or "no" => false,
            _ => defaultValue,
        };
    }

    public static int ReadInt(string key, int defaultValue, int minInclusive = int.MinValue, int maxInclusive = int.MaxValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value)
            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < minInclusive
            || parsed > maxInclusive)
        {
            return defaultValue;
        }

        return parsed;
    }

    public static double ReadDouble(string key, double defaultValue, double minExclusive = double.NegativeInfinity, double maxInclusive = double.PositiveInfinity)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || parsed <= minExclusive
            || parsed > maxInclusive)
        {
            return defaultValue;
        }

        return parsed;
    }

    public static int[] ReadIntCsv(string key, ReadOnlySpan<int> fallback, int minInclusive = int.MinValue, int maxInclusive = int.MaxValue)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback.ToArray();
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                continue;
            }

            if (parsed < minInclusive || parsed > maxInclusive)
            {
                continue;
            }

            values.Add(parsed);
        }

        return values.Count > 0 ? values.ToArray() : fallback.ToArray();
    }
}

public readonly record struct BenchFrameStatistics(
    int Samples,
    double MeasuredSeconds,
    double AverageFps,
    double MedianFrameMs,
    double P95FrameMs,
    double P99FrameMs,
    double Low1PctFps);

public sealed class BenchFrameRecorder
{
    private readonly double[] _frameSeconds;
    private readonly double[] _sortedFrameSeconds;
    private int _count;
    private double _measuredSeconds;

    public BenchFrameRecorder(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _frameSeconds = new double[capacity];
        _sortedFrameSeconds = new double[capacity];
    }

    public int Count => _count;
    public double MeasuredSeconds => _measuredSeconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Record(double frameSeconds)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        }

        if (_count >= _frameSeconds.Length)
        {
            throw new InvalidOperationException($"Benchmark frame capacity {_frameSeconds.Length} was exceeded.");
        }

        _frameSeconds[_count++] = frameSeconds;
        _measuredSeconds += frameSeconds;
    }

    public BenchFrameStatistics Calculate()
    {
        if (_count is 0)
        {
            throw new InvalidOperationException("No benchmark frames were recorded.");
        }

        Array.Copy(_frameSeconds, _sortedFrameSeconds, _count);
        Array.Sort(_sortedFrameSeconds, 0, _count);

        var slowFrameCount = Math.Max(1, (int)Math.Ceiling(_count * 0.01d));
        var slowFrameSeconds = 0d;
        for (var i = _count - slowFrameCount; i < _count; i++)
        {
            slowFrameSeconds += _sortedFrameSeconds[i];
        }

        return new BenchFrameStatistics(
            _count,
            _measuredSeconds,
            _count / _measuredSeconds,
            Percentile(0.50d) * 1000d,
            Percentile(0.95d) * 1000d,
            Percentile(0.99d) * 1000d,
            slowFrameCount / slowFrameSeconds);
    }

    public void Reset()
    {
        _count = 0;
        _measuredSeconds = 0d;
    }

    private double Percentile(double percentile)
    {
        var position = (_count - 1) * percentile;
        var lowerIndex = (int)position;
        var upperIndex = Math.Min(lowerIndex + 1, _count - 1);
        var fraction = position - lowerIndex;
        return _sortedFrameSeconds[lowerIndex]
            + ((_sortedFrameSeconds[upperIndex] - _sortedFrameSeconds[lowerIndex]) * fraction);
    }
}
