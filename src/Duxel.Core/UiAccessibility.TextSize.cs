namespace Duxel.Core;

/// <summary>Nominal direct-renderer em pixels, logical 96-DIP/in units and 72-point/in size; not glyph ink bounds.</summary>
public readonly record struct UiAccessibilityTextSize(float PhysicalPixels, float ContentScale)
{
    public float LogicalPixels => PhysicalPixels / ContentScale;
    public float Points => LogicalPixels * (72f / 96f);
    public void Validate()
    {
        if (!float.IsFinite(PhysicalPixels) || PhysicalPixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(PhysicalPixels));
        if (!float.IsFinite(ContentScale) || ContentScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(ContentScale));
        if (!float.IsFinite(LogicalPixels) || LogicalPixels <= 0 || !float.IsFinite(Points) || Points <= 0)
            throw new ArgumentException("Nominal font-size conversion must remain finite and positive.");
    }
}
