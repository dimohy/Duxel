namespace Duxel.Core;

public enum UiRectGeometryVariant : uint
{
    Filled = 0, Border = 1, LinearGradient = 2, Image = 3
}

/// <summary>A three-stop linear color field in the draw list's local coordinate space.</summary>
public readonly record struct UiLinearGradient(UiVector2 Start, UiVector2 End,
    UiColor StartColor, UiColor MiddleColor, UiColor EndColor, float MiddlePosition = .5f);

public sealed partial class UiDrawListBuilder
{
    // A gradient occupies two existing32B slots. Slot0 holds root/radius/start/end colors;
    // slot1 holds line origin/direction, middle position and middle color. A separate
    // geometry variant prevents either slot from being interpreted as a solid rectangle.
    public void AddRectFilledGradient(UiRect rect, UiLinearGradient gradient, UiTextureId texture, UiRect clip, float radius = 0)
    {
        var direction = new UiVector2(gradient.End.X - gradient.Start.X, gradient.End.Y - gradient.Start.Y);
        var lengthSquared = direction.X * direction.X + direction.Y * direction.Y;
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0 || !float.IsFinite(gradient.Start.X) || !float.IsFinite(gradient.Start.Y))
            throw new ArgumentException("Gradient endpoints must define a finite nonzero line.", nameof(gradient));
        if (!float.IsFinite(gradient.MiddlePosition) || gradient.MiddlePosition <= 0 || gradient.MiddlePosition >= 1)
            throw new ArgumentOutOfRangeException(nameof(gradient), "The middle stop must lie strictly between the endpoints.");
        if (!float.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height))
            throw new ArgumentOutOfRangeException(nameof(rect));
        if (rect.Width <= 0 || rect.Height <= 0)
            return;
        if (!IsPresentationPass)
            return;
        var offset = checked((uint)_rectFilledPrimitives.Count);
        _rectFilledPrimitives.Add(new(rect, gradient.StartColor, MathF.Min(radius, MathF.Min(rect.Width, rect.Height) / 2), 0, gradient.EndColor));
        _rectFilledPrimitives.Add(new(new(gradient.Start.X, gradient.Start.Y, direction.X, direction.Y),
            gradient.MiddleColor, gradient.MiddlePosition));
        AddRectFilledPrimitiveCommand(Intersect(clip, _currentClipRect), texture, offset, rect,
            (uint)UiRectGeometryVariant.LinearGradient, 2,
            radius > 0f && gradient.StartColor.Rgba >> 24 == 255
            && gradient.MiddleColor.Rgba >> 24 == 255 && gradient.EndColor.Rgba >> 24 == 255);
    }
}
