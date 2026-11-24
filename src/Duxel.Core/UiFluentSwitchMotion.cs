using System.Runtime.InteropServices;

namespace Duxel.Core;

internal readonly record struct UiSwitchMotionFrame(float ThumbPosition, UiSwitchPaint Paint);

internal struct UiFluentSwitchMotion
{
    internal static readonly UiTransitionOptions Timing = new(.2) { Curve = new(.33, 0, .67, 1), KeepFirstVisibleStep = true };

    private UiFloatTransition _thumb;
    private UiFloatTransition _colorProgress;
    private UiSwitchPaint _from, _target;
    private UiFluentColorScheme _scheme;
    private bool _initialized, _forced;
    internal int LastFrame;

    internal UiSwitchMotionFrame Sample(UiImmediateContext ui, bool on, UiSwitchPaint paint, UiFluentColorScheme scheme,
        bool forced, bool reducedMotion)
    {
        var timing = Timing with { ReducedMotion = reducedMotion };
        var thumb = ui.AnimateFloat(ref _thumb, on ? 1 : 0, timing);
        if (!_initialized || forced || _forced || _scheme != scheme || reducedMotion)
        {
            _initialized = true;
            _forced = forced;
            _scheme = scheme;
            _from = _target = paint;
            _colorProgress.Reset(1);
            return new(thumb, paint);
        }

        if (_target != paint)
        {
            _from = Blend(_from, _target, _colorProgress.Sample(ui.GetTime()));
            _target = paint;
            _colorProgress.Reset(0);
        }
        return new(thumb, Blend(_from, _target, ui.AnimateFloat(ref _colorProgress, 1, timing)));
    }

    private static UiSwitchPaint Blend(UiSwitchPaint from, UiSwitchPaint to, float progress) => new(
        UiDisclosureMotion.Blend(from.Background, to.Background, progress),
        UiDisclosureMotion.Blend(from.Border, to.Border, progress),
        UiDisclosureMotion.Blend(from.Thumb, to.Thumb, progress), to.Label);
}

public readonly partial struct UiControlContext
{
    internal UiSwitchMotionFrame SampleSwitchMotion(bool on, UiSwitchPaint paint)
        => MotionKey is { } key ? _ui.SampleSwitchMotion(key, on, paint, ReducedToggleMotion) : new(on ? 1 : 0, paint);
}

public sealed partial class UiImmediateContext
{
    private Dictionary<string, UiFluentSwitchMotion>? _switchMotion;
    private int _switchMotionSweepFrame;
    internal bool HostPrefersReducedMotion { get; set; }

    internal UiSwitchMotionFrame SampleSwitchMotion(string id, bool on, UiSwitchPaint paint, bool reducedMotion)
    {
        _switchMotion ??= new(StringComparer.Ordinal);
        var frame = GetFrameCount();
        if (unchecked((uint)(frame - _switchMotionSweepFrame)) >= 256)
        {
            foreach (var entry in _switchMotion)
                if (unchecked((uint)(frame - entry.Value.LastFrame)) >= 256)
                    _switchMotion.Remove(entry.Key);
            _switchMotionSweepFrame = frame;
        }
        ref var motion = ref CollectionsMarshal.GetValueRefOrAddDefault(_switchMotion, id, out _);
        motion.LastFrame = frame;
        return motion.Sample(this, on, paint, _theme.FluentColorScheme, _theme.ForcedColors.HasValue,
            reducedMotion || HostPrefersReducedMotion);
    }
}
