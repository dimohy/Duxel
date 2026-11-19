namespace Duxel.Core;

public readonly record struct UiTransitionOptions(double DurationSeconds = .16,
    UiAnimationEasing Easing = UiAnimationEasing.OutCubic, bool ReducedMotion = false,
    double FrameIntervalSeconds = UiTransitionOptions.DefaultFrameIntervalSeconds)
{
    internal const double DefaultFrameIntervalSeconds = 1d / 60;

    /// <summary>An explicit timing curve overrides Easing without changing duration, retargeting or scheduling.</summary>
    public UiCubicBezier? Curve
    {
        get; init;
    }
    public UiTransitionOptions() : this(.16, UiAnimationEasing.OutCubic, false, DefaultFrameIntervalSeconds) { }
    public UiTransitionOptions Validate()
    {
        if (!double.IsFinite(DurationSeconds) || DurationSeconds < 0
            || !double.IsFinite(FrameIntervalSeconds) || FrameIntervalSeconds <= 0 || !Enum.IsDefined(Easing))
            throw new ArgumentOutOfRangeException(nameof(UiTransitionOptions));
        Curve?.Validate();
        return this;
    }
}
public readonly record struct UiTransitionFrame(float Value, bool Running, double? NextFrameTimeSeconds);

/// <summary>An owned scalar timeline. Retargeting starts at its sampled value; a settled timeline requests no frames.</summary>
public struct UiFloatTransition
{
    private float _from, _target;
    private double _started, _lastTime;
    private UiTransitionOptions _options;
    private bool _initialized, _configured;

    public UiFloatTransition(float initialValue)
    {
        if (!float.IsFinite(initialValue))
            throw new ArgumentOutOfRangeException(nameof(initialValue));
        this = default;
        _from = _target = initialValue;
        _initialized = true;
    }
    public void Reset(float value) => this = new(value);
    public UiTransitionFrame Update(float target, double timeSeconds, UiTransitionOptions? options = null)
    {
        if (!float.IsFinite(target) || !double.IsFinite(timeSeconds) || timeSeconds < 0 || _configured && timeSeconds < _lastTime)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "A transition requires finite values and a monotonic UI clock.");
        var settings = (options ?? new UiTransitionOptions()).Validate();
        if (!double.IsFinite(timeSeconds + settings.DurationSeconds))
            throw new ArgumentOutOfRangeException(nameof(options), "The transition end must fit the finite UI clock.");
        if (!_initialized)
        {
            _from = _target = target;
            _initialized = true;
        }
        var current = Sample(timeSeconds);
        if (!_configured || _target != target || _options != settings)
        {
            _from = current;
            _target = target;
            _started = timeSeconds;
            _options = settings;
            _configured = true;
        }
        _lastTime = timeSeconds;
        if (settings.ReducedMotion || settings.DurationSeconds == 0)
            _from = _target;
        var value = Sample(timeSeconds);
        var end = _started + settings.DurationSeconds;
        var running = _from != _target && timeSeconds < end;
        return new(value, running, running ? Math.Min(end, timeSeconds + settings.FrameIntervalSeconds) : null);
    }
    internal float Sample(double time)
    {
        if (!_configured || _options.ReducedMotion || _options.DurationSeconds == 0)
            return _target;
        var progress = (float)Math.Clamp((time - _started) / _options.DurationSeconds, 0, 1);
        var eased = _options.Curve is { } curve ? curve.Evaluate(progress) : UiAnimationCurve.Evaluate(progress, _options.Easing);
        var value = (float)(_from + ((double)_target - _from) * eased);
        if (!float.IsFinite(value))
            throw new InvalidOperationException("Transition interpolation must remain representable as a finite value.");
        return value;
    }
}

public static class UiAnimationCurve
{
    public static float Evaluate(float progress, UiAnimationEasing easing)
    {
        if (!float.IsFinite(progress))
            throw new ArgumentOutOfRangeException(nameof(progress));
        progress = Math.Clamp(progress, 0, 1);
        return easing switch
        {
            UiAnimationEasing.Linear => progress,
            UiAnimationEasing.InOutSine => .5f - MathF.Cos(MathF.PI * progress) * .5f,
            UiAnimationEasing.OutCubic => 1 - MathF.Pow(1 - progress, 3),
            _ => throw new ArgumentOutOfRangeException(nameof(easing))
        };
    }
}

public sealed partial class UiImmediateContext
{
    /// <summary>Samples an owned transition and declares only its finite next deadline.</summary>
    public float AnimateFloat(ref UiFloatTransition transition, float target, UiTransitionOptions? options = null)
    {
        EnsureNotMeasuring();
        var frame = transition.Update(target, _state.TimeSeconds, options);
        if (frame.NextFrameTimeSeconds is { } deadline)
            RequestFrameAt(deadline);
        return frame.Value;
    }
}
