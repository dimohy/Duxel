namespace Duxel.Core;

public sealed partial class UiState
{
    /// <summary>Earliest absolute UI-clock deadline declared by this frame. Null waits for external work.</summary>
    public double? NextScheduledFrameTimeSeconds
    {
        get; private set;
    }

    internal void ScheduleFrameAt(double timeSeconds)
    {
        if (!double.IsFinite(timeSeconds) || timeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        if (NextScheduledFrameTimeSeconds is not { } previous || timeSeconds < previous)
            NextScheduledFrameTimeSeconds = timeSeconds;
    }
    /// <summary>Returns0 when due, a rounded-up timeout, or-1 when no deadline is scheduled.</summary>
    public int GetScheduledFrameWaitMilliseconds(double nowSeconds)
    {
        if (!double.IsFinite(nowSeconds) || nowSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(nowSeconds));
        var diagnostics = RendererFrameMetrics;
        var diagnosticWait = diagnostics.RepaintHighlightActive
            ? (int)Math.Clamp(Math.Ceiling((diagnostics.RepaintHighlightDeadlineTicks - System.Diagnostics.Stopwatch.GetTimestamp()) * 1000d
                / System.Diagnostics.Stopwatch.Frequency), 0, int.MaxValue) : Timeout.Infinite;
        if (NextScheduledFrameTimeSeconds is not { } deadline)
            return diagnosticWait;
        var remaining = deadline - nowSeconds;
        if (remaining <= 0)
            return 0;
        var scheduled = (int)Math.Min(int.MaxValue, Math.Ceiling(remaining * 1000));
        return diagnosticWait == Timeout.Infinite ? scheduled : Math.Min(scheduled, diagnosticWait);
    }
    internal void ClearScheduledFrame() => NextScheduledFrameTimeSeconds = null;
}

public readonly partial struct UiControlContext
{
    public void RequestFrameAt(double timeSeconds) => _ui.RequestFrameAt(timeSeconds);
}
public sealed partial class UiImmediateContext
{
    /// <summary>Declares a future frame without marking a continuous animation or allocating a timer.</summary>
    public void RequestFrameAt(double timeSeconds)
    {
        EnsureNotMeasuring();
        _state.ScheduleFrameAt(timeSeconds);
        TrackLayerFrameDeadline(timeSeconds);
    }
}
