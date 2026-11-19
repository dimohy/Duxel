namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    // Integer GPU scissors can overlap at a fractional text-selection boundary.
    // Clip the textured quad itself so adjacent regions share one geometric edge.
    internal void AddTextImage(UiTextureId texture, UiVector2 minimum, UiVector2 maximum,
        UiVector2 uvMinimum, UiVector2 uvMaximum, UiColor color, UiRect clip)
    {
        if (!IsPresentationPass)
            return;
        clip = Intersect(clip, _currentClipRect);
        var left = MathF.Max(minimum.X, clip.X);
        var top = MathF.Max(minimum.Y, clip.Y);
        var right = MathF.Min(maximum.X, clip.X + clip.Width);
        var bottom = MathF.Min(maximum.Y, clip.Y + clip.Height);
        if (right <= left || bottom <= top)
            return;
        if (left == minimum.X && top == minimum.Y && right == maximum.X && bottom == maximum.Y)
        {
            AddImage(texture, minimum, maximum, uvMinimum, uvMaximum, color, clip);
            return;
        }
        var uScale = (uvMaximum.X - uvMinimum.X) / (maximum.X - minimum.X);
        var vScale = (uvMaximum.Y - uvMinimum.Y) / (maximum.Y - minimum.Y);
        var first = new UiVector2(uvMinimum.X + (left - minimum.X) * uScale,
            uvMinimum.Y + (top - minimum.Y) * vScale);
        var last = new UiVector2(uvMinimum.X + (right - minimum.X) * uScale,
            uvMinimum.Y + (bottom - minimum.Y) * vScale);
        AddImage(texture, new(left, top), new(right, bottom), first, last, color, clip);
    }
}
