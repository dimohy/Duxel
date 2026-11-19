namespace Duxel.Core;

public sealed partial class UiState
{
    private double? _inputTimeSeconds;
    /// <summary>Input event time, projected by frame deltas during idle frames; animations use TimeSeconds.</summary>
    public double InputTimeSeconds => _inputTimeSeconds ?? _timeSeconds;
    internal void ApplyInputTime(double? time)
    {
        if (time is { } value)
            _inputTimeSeconds = value;
    }
}
