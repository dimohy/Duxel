using System.Diagnostics;

namespace Duxel.Core;

/// <summary>One Render call. Preparation includes input dispatch and context reset; composition includes
/// callbacks, layout and paint generation. Timings and current-render-thread allocation are opt-in,
/// exclude NewFrame and GPU work, and include profiling overhead.</summary>
public readonly record struct UiInputProcessingMetrics(
    bool ProfilingEnabled, int RequestedSteps, int CompletedSteps, int BuiltDrawings, bool Completed,
    double PreparationMilliseconds, double CompositionMilliseconds, double FinalizationMilliseconds,
    double ElapsedMilliseconds, long AllocatedBytesOnRenderThread);

internal struct UiInputProcessingMeasurement
{
    private readonly bool _enabled;
    private readonly int _requested;
    private readonly long _start, _allocationStart;
    private long _preparation, _composition, _finalization;
    private int _steps, _drawings;

    internal UiInputProcessingMeasurement(bool enabled, int requested)
    {
        _enabled = enabled;
        _requested = requested;
        _start = enabled ? Stopwatch.GetTimestamp() : 0;
        _allocationStart = enabled ? GC.GetAllocatedBytesForCurrentThread() : 0;
    }

    internal readonly long StartStage() => _enabled ? Stopwatch.GetTimestamp() : 0;
    internal void EndPreparation(long start)
    {
        if (_enabled)
            _preparation += Stopwatch.GetTimestamp() - start;
    }
    internal void EndComposition(long start)
    {
        if (_enabled)
            _composition += Stopwatch.GetTimestamp() - start;
        _steps++;
    }
    internal void EndFinalization(long start)
    {
        if (_enabled)
            _finalization += Stopwatch.GetTimestamp() - start;
        _drawings++;
    }
    internal readonly UiInputProcessingMetrics Finish(bool completed)
    {
        const double milliseconds = 1000d;
        var scale = milliseconds / Stopwatch.Frequency;
        return new(_enabled, _requested, _steps, _drawings, completed,
            _preparation * scale, _composition * scale, _finalization * scale,
            _enabled ? (Stopwatch.GetTimestamp() - _start) * scale : 0,
            _enabled ? GC.GetAllocatedBytesForCurrentThread() - _allocationStart : 0);
    }
}

public sealed partial class UiState
{
    internal bool InputProfilingEnabled
    {
        get; set;
    }
    internal UiInputProcessingMetrics InputProcessingMetrics
    {
        get; set;
    }
}

public sealed partial class UiContext
{
    public bool InputProfilingEnabled
    {
        get => _state.InputProfilingEnabled;
        set => _state.InputProfilingEnabled = value;
    }
    public UiInputProcessingMetrics InputProcessingMetrics => _state.InputProcessingMetrics;
}

public sealed partial class UiImmediateContext
{
    /// <summary>Enables timing/allocation sampling for the next Render call; no frame is requested.</summary>
    public void SetInputProfilingEnabled(bool enabled) => _state.InputProfilingEnabled = enabled;
    /// <summary>Returns the last finished Render, including partial counts after a failed callback.</summary>
    public UiInputProcessingMetrics GetInputProcessingMetrics() => _state.InputProcessingMetrics;
}
