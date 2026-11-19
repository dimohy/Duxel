namespace Duxel.Core;

public enum UiSpinnerSize
{
    Medium, ExtraTiny, Tiny, ExtraSmall, Small, Large, ExtraLarge, Huge
}
public enum UiSpinnerLabelPosition
{
    After, Before, Above, Below
}
public enum UiSpinnerAppearance
{
    Primary, Inverted
}
public readonly record struct UiSpinnerOptions(UiSpinnerSize Size = UiSpinnerSize.Medium,
    UiSpinnerLabelPosition LabelPosition = UiSpinnerLabelPosition.After, UiSpinnerAppearance Appearance = UiSpinnerAppearance.Primary,
    bool ReducedMotion = false, bool RightToLeft = false, float DelaySeconds = 0)
{
    /// <summary>Freezes the animation timeline and stops continuous frame requests. Delay still reveals once.</summary>
    public bool Paused { get; init; }
    public float Width
    {
        get; init;
    }
    public UiSpinnerOptions Validate()
    {
        if ((uint)Size > 7 || (uint)LabelPosition > 3 || (uint)Appearance > 1 || !float.IsFinite(DelaySeconds) || DelaySeconds < 0 || !float.IsFinite(Width) || Width < 0)
            throw new ArgumentOutOfRangeException(nameof(UiSpinnerOptions));
        return this;
    }
}
public readonly record struct UiSpinnerFrame(bool Visible, float Rotation, float TailRotation, float Before, float After, bool ReducedMotion, bool RightToLeft)
{
    public double? NextFrameTimeSeconds
    {
        get; init;
    }
}
public interface IUiSpinnerBehavior<TSelf> where TSelf : struct, IUiSpinnerBehavior<TSelf>
{
    /// <summary>Visibility cannot change without an options update; evaluation and animation still run on every render.</summary>
    static virtual bool CanRetainMeasurement(in TSelf state, UiSpinnerOptions options) => false;
    static abstract bool IsVisible(in TSelf state, UiSpinnerOptions options, double time);
    static abstract UiSpinnerFrame Evaluate(ref TSelf state, UiSpinnerOptions options, double time);
}
public struct UiFluentSpinnerBehavior : IUiSpinnerBehavior<UiFluentSpinnerBehavior>
{
    public static bool CanRetainMeasurement(in UiFluentSpinnerBehavior state, UiSpinnerOptions options) => state._shown || options.DelaySeconds == 0;
    private bool _started, _shown, _paused;
    private double _mounted, _visibleSince, _pausedAt, _pausedDuration;
    private float _delay;
    public static bool IsVisible(in UiFluentSpinnerBehavior state, UiSpinnerOptions options, double time) =>
        state._shown || options.DelaySeconds == 0 || state._started && state._delay == options.DelaySeconds && time - state._mounted >= options.DelaySeconds;
    public static UiSpinnerFrame Evaluate(ref UiFluentSpinnerBehavior state, UiSpinnerOptions options, double time)
    {
        options.Validate();
        if (!double.IsFinite(time) || time < 0)
            throw new ArgumentOutOfRangeException(nameof(time));
        if (!state._started || !state._shown && state._delay != options.DelaySeconds)
        {
            state._started = true;
            state._mounted = time;
            state._delay = options.DelaySeconds;
        }
        if (!state._shown && time - state._mounted >= options.DelaySeconds)
        {
            state._shown = true;
            state._visibleSince = time;
        }
        if (!state._shown)
            return new()
            {
                NextFrameTimeSeconds = state._mounted + options.DelaySeconds
            };
        if (options.Paused && !state._paused)
            state._pausedAt = time;
        else if (!options.Paused && state._paused)
            state._pausedDuration += time - state._pausedAt;
        state._paused = options.Paused;
        var elapsed = Math.Max(0, (state._paused ? state._pausedAt : time) - state._visibleSince - state._pausedDuration);
        var phase = (float)(elapsed / (options.ReducedMotion ? 1.8 : 1.5) % 1);
        var local = phase < .5f ? phase * 2 : (phase - .5f) * 2;
        var ease = EasyEase(local);
        var expand = phase < .5f ? ease : 1 - ease;
        return new(true, phase * 360, phase < .5f ? -135 + 135 * ease : 225 * ease, 105 * expand, 225 * expand, options.ReducedMotion, options.RightToLeft);
    }
    // CSS cubic-bezier(0.33,0,0.67,1), solved by monotonic bisection.
    private static float EasyEase(float x)
    {
        if (x == 0 || x == 1)
            return x;
        var low = 0f;
        var high = 1f;
        for (var i = 0; i < 20; i++)
        {
            var t = (low + high) * .5f;
            var u = 1 - t;
            var value = 3 * u * u * t * .33f + 3 * u * t * t * .67f + t * t * t;
            if (value < x)
                low = t;
            else
                high = t;
        }
        var p = (low + high) * .5f;
        return p * p * (3 - 2 * p);
    }
}
public readonly record struct UiSpinnerLayout(UiVector2 Size, UiRect Ring, UiRect Label, UiControlTextBlock Text);
public interface IUiSpinnerPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiControlTypographyScope BeginLabel(UiControlContext context, UiSpinnerOptions options, bool hasLabel);
    static abstract UiSpinnerLayout Measure(UiControlContext context, string? label, UiSpinnerOptions options);
    static virtual UiSpinnerLayout Arrange(UiControlContext context, UiSpinnerOptions options, in UiSpinnerLayout natural, UiVector2 allocatedSize)
        => throw new NotSupportedException("This Spinner presentation must implement Arrange to accept a different parent allocation.");
    static abstract void Paint(UiControlContext context, UiSpinnerOptions options, in UiSpinnerFrame frame, in UiSpinnerLayout layout, UiVector2 origin);
}
public class UiSpinner<TBehavior, TPresentation> : UiControlWidget
    where TBehavior : struct, IUiSpinnerBehavior<TBehavior> where TPresentation : IUiSpinnerPresentation
{
    private TBehavior _behavior;
    private string? _label;
    private UiSpinnerOptions _options;
    public UiSpinner(string? label = null, UiSpinnerOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        _label = label;
        _options = options.Validate();
    }
    public string? Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed();
            if (_label == value)
                return;
            ThrowIfBuilding();
            _label = value;
            RequestRebuild();
        }
    }
    public UiSpinnerOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public float Width
    {
        get => _options.Width;
        set => Options = _options with { Width = value };
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement && TBehavior.CanRetainMeasurement(in _behavior, _options);
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        if (!TBehavior.IsVisible(_behavior, _options, context.TimeSeconds))
            return context.ConstrainToLayout(new(_options.Width > 0 ? _options.Width : context.AvailableWidth, 0));
        return context.MeasureSpinner<TPresentation>(_label, _options);
    }
    protected override void RenderControl(UiImmediateContext ui) => ui.Spinner<TBehavior, TPresentation>(PrimaryInputId(_label ?? "spinner"), ref _behavior, _label, _options, constrainLayout : true);
    private protected override void ReleaseOwnedResources()
    {
        _label = null;
        _behavior = default;
        base.ReleaseOwnedResources();
    }
}
[UiWidgetStyle(typeof(UiSpinner<,>), typeof(UiFluentSpinnerBehavior), typeof(UiFluentSpinnerPresentation))]
[UiComponent("Spinner", UiComponentExposure.Fluent)]
public partial class UiFluentSpinner;
