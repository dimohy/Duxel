namespace Duxel.Core;

public sealed record UiFluentIosProgressBarOptions(float? Progress = null, bool IsAnimating = false,
    bool HidesWhenStopped = true, float Width = 0, bool RightToLeft = false, bool ReducedMotion = false,
    string? AccessibilityLabel = null)
{
    internal float? Fraction => Progress is { } value ? Math.Clamp(value, 0, 1) : null;

    internal UiFluentIosProgressBarOptions Validate()
    {
        if (Progress is { } value && !float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(Progress));
        if (!float.IsFinite(Width) || Width < 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
        return this;
    }
}

/// <summary>A Fluent iOS-style progress bar with explicit start, stop, and hide behavior for waiting.</summary>
public sealed class UiFluentIosProgressBar : UiControlWidget
{
    private UiFluentIosProgressBarOptions _options;
    private UiFluentIosProgressMotion _motion;

    public UiFluentIosProgressBar(UiFluentIosProgressBarOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity) => _options = (options ?? new()).Validate();

    public UiFluentIosProgressBarOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            if (value.Progress.HasValue != _options.Progress.HasValue)
                _motion = default;
            else if (!value.IsAnimating)
                _motion.StopWaiting();
            _options = value;
            RequestRebuild();
        }
    }

    public void Start() => Options = Options with { IsAnimating = true };
    public void Stop() => Options = Options with { IsAnimating = false };

    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        context.ConstrainToLayout(UiFluentMobileProgressTrack.Measure(context, new(_options.Width, 0)));
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderIosProgressBar(PrimaryControlId, ref _motion, _options);
}

internal struct UiFluentIosProgressMotion
{
    private const double WaitingCycleSeconds = 1.75;
    private static readonly UiTransitionOptions Determinate = new(.2, UiAnimationEasing.Linear);
    private UiFloatTransition _value;
    private double? _waitingSince;

    internal void StopWaiting() => _waitingSince = null;
    internal UiTransitionFrame SampleValue(float value, double time, bool reducedMotion) =>
        _value.Update(value, time, Determinate with { ReducedMotion = reducedMotion });

    internal float WaitingStart(double time, bool reducedMotion)
    {
        _waitingSince ??= time;
        return reducedMotion ? 0 : (float)((time - _waitingSince.Value) % WaitingCycleSeconds / WaitingCycleSeconds * 2 - 1);
    }
}

public sealed partial class UiImmediateContext
{
    internal void RenderIosProgressBar(string id, ref UiFluentIosProgressMotion motion, UiFluentIosProgressBarOptions options)
    {
        var context = new UiControlContext(this);
        var bounds = PlaceProgress(ConstrainToLayout(UiFluentMobileProgressTrack.Measure(context, new(options.Width, 0))));
        var fraction = options.Fraction;
        var busy = fraction is null && options.IsAnimating;
        if (fraction is null && !busy && options.HidesWhenStopped)
            return;

        var frame = fraction is { } numeric ? motion.SampleValue(numeric, GetTime(), options.ReducedMotion) : default;
        var gradient = busy ? motion.WaitingStart(GetTime(), options.ReducedMotion) : -1;
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width > 0 && clip.Height > 0)
        {
            if (frame.Running || busy && !options.ReducedMotion)
                context.RequestAnimation(bounds);
            using var root = context.Clip(bounds);
            var (track, fill) = UiFluentMobileProgressTrack.Colors(context);
            if (fraction.HasValue)
                UiFluentMobileProgressTrack.PaintDeterminate(context, bounds, track, fill, frame.Value, options.RightToLeft);
            else if (busy)
                context.ProgressGradient(UiFluentMobileProgressTrack.Segment(bounds, gradient, gradient + 1, options.RightToLeft),
                    bounds, track, fill);
            else
                context.Frame(bounds, track, track, 0, 0);
        }

        if (_accessibility is null)
            return;
        var resolved = ResolveId(id);
        ClaimAccessibilityField(resolved);
        var label = options.AccessibilityLabel ?? (busy || fraction.HasValue ? "Animating" : "Stopped");
        var valueText = fraction is { } value ? FormattableString.Invariant($"{MathF.Round(value * 100, MidpointRounding.AwayFromZero)}%") : null;
        ExposeAccessibility(resolved, _accessibilityParent, UiAccessibilityRole.ProgressBar, label, bounds, valueText,
            readOnly : true, focusable : false, valuePattern : false, literalLabel : true, busy : busy,
            range : fraction is { } progress ? new(progress * 100, 0, 100, 0, 0) : null);
    }
}
