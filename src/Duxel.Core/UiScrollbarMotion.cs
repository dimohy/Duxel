namespace Duxel.Core;

/// <summary>Shared visibility policy for every scrollbar family.</summary>
public readonly record struct UiScrollbarMotionOptions(double HoldSeconds = .4, bool RevealOnHover = true,
    bool ReducedMotion = false)
{
    public float IdleOpacity { get; init; } = .28f;
    public UiTransitionOptions Enter { get; init; } = new(.12);
    public UiTransitionOptions Exit { get; init; } = new(.16);
    public UiScrollbarMotionOptions() : this(.4, true, false) { }
    public UiScrollbarMotionOptions Validate()
    {
        if (!double.IsFinite(HoldSeconds) || HoldSeconds < 0 || !float.IsFinite(IdleOpacity) || IdleOpacity < 0 || IdleOpacity > 1)
            throw new ArgumentOutOfRangeException(nameof(HoldSeconds));
        Enter.Validate();
        Exit.Validate();
        return this;
    }
}
public readonly record struct UiScrollbarMotionFrame(float Opacity, bool Animating, double? NextFrameTimeSeconds);

/// <summary>Owned, clock-driven entry/hold/exit state, independent of scrollbar geometry or input delivery.</summary>
public struct UiScrollbarMotion
{
    private UiFloatTransition _opacity;
    private double _offset, _until, _lastTime;
    private bool _initialized, _engaged, _shown;
    private bool _requested;
    public bool TargetVisible => _shown;
    internal void Reveal() => _requested = true;

    public void Reset() => this = default;
    public UiScrollbarMotionFrame Update(double offset, bool active, bool hovered, double timeSeconds,
        UiScrollbarMotionOptions? options = null, bool alwaysVisible = false)
    {
        var settings = (options ?? new UiScrollbarMotionOptions()).Validate();
        if (!double.IsFinite(offset) || !double.IsFinite(timeSeconds) || timeSeconds < 0
            || _initialized && timeSeconds < _lastTime || !double.IsFinite(timeSeconds + settings.HoldSeconds))
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        var enter = settings.Enter with
        {
            ReducedMotion = settings.ReducedMotion || settings.Enter.ReducedMotion
        };
        var exit = settings.Exit with
        {
            ReducedMotion = settings.ReducedMotion || settings.Exit.ReducedMotion
        };
        if (!_initialized)
        {
            _opacity.Reset(settings.IdleOpacity);
            _initialized = true;
        }
        // A late event samples the exit from its real deadline before retargeting.
        if (!_engaged && _shown && timeSeconds > _until)
        {
            _opacity.Update(settings.IdleOpacity, Math.Max(_lastTime, _until), exit);
            _shown = false;
        }
        var engaged = active || hovered && settings.RevealOnHover;
        if (_requested || offset != _offset || engaged || _engaged && !engaged)
            _until = timeSeconds + settings.HoldSeconds;
        _requested = false;
        _offset = offset;
        _engaged = engaged;
        _lastTime = timeSeconds;
        _shown = alwaysVisible || engaged || timeSeconds < _until;
        if (alwaysVisible)
        {
            _opacity.Reset(1);
            return new(1, false, null);
        }
        var frame = _opacity.Update(_shown ? 1 : settings.IdleOpacity, timeSeconds, _shown ? enter : exit);
        var next = frame.NextFrameTimeSeconds;
        if (_shown && !engaged && _until > timeSeconds && (next is null || _until < next))
            next = _until;
        return new(frame.Value, frame.Running, next);
    }
}

public sealed partial class UiState
{
    private Dictionary<string, UiScrollbarMotion>? _scrollbarMotions;
    internal int ScrollbarMotionCount => _scrollbarMotions?.Count ?? 0;
    internal void ReleaseScrollbarMotion(string id) => _scrollbarMotions?.Remove(id);
    internal void ReleaseScrollbarMotions() => _scrollbarMotions = null;
    internal void RequestScrollbarReveal(string id)
    {
        _scrollbarMotions ??= new(StringComparer.Ordinal);
        ref var motion = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_scrollbarMotions, id, out _);
        motion.Reveal();
    }
    internal UiScrollbarMotionFrame SampleScrollbarMotion(string id, double offset, bool active, bool hovered,
        UiScrollbarMotionOptions options, bool alwaysVisible)
    {
        _scrollbarMotions ??= new(StringComparer.Ordinal);
        ref var motion = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_scrollbarMotions, id, out _);
        return motion.Update(offset, active, hovered, TimeSeconds, options, alwaysVisible);
    }
}

public sealed partial class UiImmediateContext
{
    internal UiScrollbarMotionOptions ScrollbarMotionOptions => _controlSkins.Scrollbar.Motion ?? new UiScrollbarMotionOptions();
    private float SampleScrollbarMotion(string id, double offset, bool active, bool hovered)
    {
        var options = ScrollbarMotionOptions;
        if (_controlSkins.Scrollbar.RecentInteractionSeconds is { } hold)
            options = options with
            {
                HoldSeconds = hold
            };
        return ScheduleScrollbarMotion(_state.SampleScrollbarMotion(id, offset, active, hovered, options, _controlSkins.Scrollbar.AlwaysShowThumb));
    }
    private float SampleScrollbarMotion(ref UiScrollbarMotion motion, double offset, bool active, bool hovered,
        UiScrollbarMotionOptions? options = null) =>
        ScheduleScrollbarMotion(motion.Update(offset, active, hovered, _state.TimeSeconds, options ?? ScrollbarMotionOptions,
            _controlSkins.Scrollbar.AlwaysShowThumb));
    private float ScheduleScrollbarMotion(UiScrollbarMotionFrame frame)
    {
        if (frame.NextFrameTimeSeconds is { } deadline)
            RequestFrameAt(deadline);
        return frame.Opacity;
    }
    private void PaintScrollbarVisual(UiRect track, UiRect thumb, UiRect clip, float opacity, bool active, bool hovered, float? radius = null)
    {
        if (opacity <= 0)
            return;
        var permanent = _controlSkins.Scrollbar.AlwaysShowThumb;
        var color = active ? _theme.ScrollbarGrabActive : hovered ? _theme.ScrollbarGrabHovered
            : permanent ? _theme.ScrollbarGrab : _theme.ScrollbarGrabActive;
        var trackAlpha = active ? .32f : hovered || !permanent ? .18f : .06f;
        _builder.AddRectFilled(track, ApplyAlpha(_theme.ScrollbarBg, opacity * trackAlpha), _whiteTexture, clip);
        _builder.AddRectFilledRounded(thumb, ApplyAlpha(color, opacity * (permanent ? 1 : .86f)), _whiteTexture,
            radius ?? _controlSkins.Scrollbar.ThumbCornerRadius ?? 0, clip);
    }
}
