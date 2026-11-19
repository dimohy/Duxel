namespace Duxel.Core;

public enum UiSkeletonAnimation
{
    Wave, Pulse
}
public enum UiSkeletonAppearance
{
    Opaque, Translucent
}
public enum UiSkeletonShape
{
    Rectangle, Square, Circle
}

/// <summary>Null values inherit the enclosing Skeleton. Width is local and never inherited.</summary>
public readonly record struct UiSkeletonOptions(
    UiSkeletonAnimation? Animation = null, UiSkeletonAppearance? Appearance = null,
    int? Size = null, UiSkeletonShape? Shape = null, bool? ReducedMotion = null,
    bool? Paused = null, float? Width = null)
{
    public UiSkeletonOptions Validate()
    {
        if (Animation is { } animation && (uint)animation > 1
            || Appearance is { } appearance && (uint)appearance > 1
            || Shape is { } shape && (uint)shape > 2
            || Size is { } size && size is not (8 or 12 or 14 or 16 or 20 or 22 or 24 or 28 or 32 or 36 or 40 or 48 or 52 or 56 or 64 or 72 or 92 or 96 or 120 or 128)
            || Width is { } width && (!float.IsFinite(width) || width < 0))
            throw new ArgumentOutOfRangeException(nameof(UiSkeletonOptions));
        return this;
    }

    public UiSkeletonStyle Resolve(UiSkeletonStyle parent)
    {
        Validate();
        return new(Animation ?? parent.Animation, Appearance ?? parent.Appearance,
            Size ?? parent.Size, Shape ?? parent.Shape,
            ReducedMotion ?? parent.ReducedMotion, Paused ?? parent.Paused);
    }
}

public readonly record struct UiSkeletonStyle(UiSkeletonAnimation Animation, UiSkeletonAppearance Appearance,
    int Size, UiSkeletonShape Shape, bool ReducedMotion, bool Paused)
{
    public static UiSkeletonStyle Default => new(UiSkeletonAnimation.Wave, UiSkeletonAppearance.Opaque,
        16, UiSkeletonShape.Rectangle, false, false);

    public UiSkeletonStyle Validate()
    {
        new UiSkeletonOptions(Animation, Appearance, Size, Shape).Validate();
        return this;
    }
}

public readonly record struct UiSkeletonFrame(float WaveOffset, float Opacity, bool Animated);

public static class UiSkeletonMotion
{
    private static readonly UiCubicBezier Ease = new(.42, 0, .58, 1);

    /// <summary>Shared absolute time synchronizes items, including items mounted later.</summary>
    public static UiSkeletonFrame Sample(UiSkeletonStyle style, double timeSeconds)
    {
        style.Validate();
        if (!double.IsFinite(timeSeconds) || timeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        if (style.ReducedMotion || style.Paused)
            return new(style.ReducedMotion ? 1 : -1, 1, false);
        if (style.Animation == UiSkeletonAnimation.Wave)
            return new((float)(2 * Ease.Evaluate(timeSeconds / 3 % 1) - 1), 1, true);
        var phase = timeSeconds % 1;
        var half = phase < .5 ? phase * 2 : (phase - .5) * 2;
        var eased = (float)Ease.Evaluate(half);
        return new(0, phase < .5 ? 1 - .6f * eased : .4f + .6f * eased, true);
    }
}
