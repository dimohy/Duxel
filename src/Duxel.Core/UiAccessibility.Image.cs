namespace Duxel.Core;

/// <summary>The complete rendered image rectangle in logical UI coordinates, before viewport clipping.</summary>
public readonly record struct UiAccessibilityImageInfo(UiRect Bounds, string Description, string? Locale = null)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Description);
        if (!float.IsFinite(Bounds.X) || !float.IsFinite(Bounds.Y) || !float.IsFinite(Bounds.Width) ||
            !float.IsFinite(Bounds.Height) || Bounds.Width <= 0 || Bounds.Height <= 0 ||
            !float.IsFinite(Bounds.X + Bounds.Width) || !float.IsFinite(Bounds.Y + Bounds.Height))
            throw new ArgumentOutOfRangeException(nameof(Bounds), "An accessible image requires a finite positive rendered rectangle.");
    }
}
