namespace Duxel.Core;

/// <summary>A statically selected transition implementation with widget-owned value-type state.</summary>
public interface IUiProgressBehavior<TSelf> where TSelf : struct, IUiProgressBehavior<TSelf>
{
    static abstract UiProgressValue Evaluate(ref TSelf state, UiProgressValue target, double timeSeconds, out bool animating);
    static abstract void Release(ref TSelf state);
}

/// <summary>Fluent's 300ms CSS ease width transition, including its reset-to-zero threshold.</summary>
public struct UiFluentProgressBehavior : IUiProgressBehavior<UiFluentProgressBehavior>
{
    private bool _initialized;
    private float _from, _target;
    private double _start;
    public static void Release(ref UiFluentProgressBehavior state) => state = default;
    public static UiProgressValue Evaluate(ref UiFluentProgressBehavior state, UiProgressValue value, double timeSeconds, out bool animating)
    {
        animating = false;
        if (value.Value is not { } raw)
        {
            state = default;
            return value;
        }
        var target = value.Fraction!.Value;
        if (!state._initialized || value.ReducedMotion || Math.Clamp(raw, 0, value.Maximum) <= .01f)
        {
            state._initialized = true;
            state._from = target;
            state._target = target;
            state._start = timeSeconds;
            return value;
        }
        if (state._target != target)
        {
            state._from = state.Sample(timeSeconds);
            state._target = target;
            state._start = timeSeconds;
        }
        var elapsed = timeSeconds - state._start;
        var fraction = state._from != state._target && elapsed < .3 ? state.Sample(timeSeconds) : state._target;
        animating = fraction != state._target;
        return value with
        {
            Value = fraction * value.Maximum
        };
    }
    private readonly float Sample(double timeSeconds)
    {
        var time = (float)Math.Clamp((timeSeconds - _start) / .3, 0, 1);
        return _from + (_target - _from) * CssEase(time);
    }
    internal static float CssEase(float time)
    {
        if (time <= 0)
            return 0;
        if (time >= 1)
            return 1;
        var parameter = time;
        // Invert x(u)=u^3-.75u^2+.75u; its derivative stays strictly positive.
        for (var i = 0; i < 5; i++)
            parameter -= (((parameter - .75f) * parameter + .75f) * parameter - time) / ((3 * parameter - 1.5f) * parameter + .75f);
        return ((-1.7f * parameter + 2.4f) * parameter + .3f) * parameter;
    }
}
