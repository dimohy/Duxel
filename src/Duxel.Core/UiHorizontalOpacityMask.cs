namespace Duxel.Core;

/// <summary>Linear edge fades in normalized destination coordinates; the plateau remains fully opaque.</summary>
public readonly record struct UiHorizontalOpacityMask(float LeftFadeFraction = 0, float RightFadeFraction = 0)
{
    public UiHorizontalOpacityMask Validate()
    {
        if (!float.IsFinite(LeftFadeFraction) || !float.IsFinite(RightFadeFraction)
            || LeftFadeFraction < 0 || RightFadeFraction < 0 || LeftFadeFraction + RightFadeFraction > 1)
            throw new ArgumentOutOfRangeException(nameof(UiHorizontalOpacityMask));
        return this;
    }
    public float Evaluate(float normalizedX)
    {
        Validate();
        if (!float.IsFinite(normalizedX) || normalizedX < 0 || normalizedX > 1)
            throw new ArgumentOutOfRangeException(nameof(normalizedX));
        if (LeftFadeFraction > 0 && normalizedX < LeftFadeFraction) return normalizedX / LeftFadeFraction;
        if (RightFadeFraction > 0 && normalizedX > 1 - RightFadeFraction) return (1 - normalizedX) / RightFadeFraction;
        return 1;
    }
    public bool IsIdentity => LeftFadeFraction == 0 && RightFadeFraction == 0;
}
