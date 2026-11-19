namespace Duxel.Core;

/// <summary>Pure measurement samples the same owned timeline that rendering retargets and schedules.</summary>
internal struct UiDrawerInlineMotion
{
    private UiFloatTransition _progress;
    private bool _initialized;
    internal void Reset() => this = default;
    internal float Sample(bool open, bool enabled, double time)
        => !_initialized || !enabled ? open ? 1 : 0 : _progress.Sample(time);
    internal UiTransitionFrame Update(bool open, bool enabled, double time)
    {
        if (!_initialized)
        {
            _progress.Reset(open ? 1 : 0);
            _initialized = true;
        }
        return _progress.Update(open ? 1 : 0, time, new(UiDrawerMotion.DurationSeconds, ReducedMotion : !enabled)
        {
            Curve = open ? UiDrawerMotion.Entry : UiDialogMotion.Exit
        });
    }
    internal static UiVector2 Allocation(UiVector2 natural, float progress, bool bottom)
        => progress <= 0 ? default : bottom ? new(natural.X, natural.Y * progress) : new(natural.X * progress, natural.Y);
}
