namespace Duxel.Core;

/// <summary>Controls which edges of a queried rectangle must contain the entire source cell.</summary>
public enum UiAccessibilityTextClip
{
    None,
    Minimum,
    Maximum,
    Both
}

/// <summary>A nonempty, end-exclusive Unicode scalar range in the original text content.</summary>
public readonly record struct UiAccessibilityTextRange(int Start, int End);

public sealed partial class UiAccessibilityTextLayout
{
    /// <summary>Returns visible source-cell runs, split at visual lines and unpainted source gaps.</summary>
    public UiAccessibilityTextRange[] GetBoundedRanges(UiRect bounds,
        UiAccessibilityTextClip xClip = UiAccessibilityTextClip.None,
        UiAccessibilityTextClip yClip = UiAccessibilityTextClip.None)
    {
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y)
            || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)
            || bounds.Width < 0 || bounds.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if ((uint)xClip > 3)
            throw new ArgumentOutOfRangeException(nameof(xClip));
        if ((uint)yClip > 3)
            throw new ArgumentOutOfRangeException(nameof(yClip));
        if (bounds.Width == 0 || bounds.Height == 0 || !Intersects(bounds, ClipBounds))
            return [];
        var ranges = new List<UiAccessibilityTextRange>();
        foreach (var line in _lines)
        {
            var start = -1;
            for (var index = line.Start; index < line.End; index++)
            {
                var box = GetCharacterExtents(index);
                var included = box.Width > 0 && box.Height > 0
                    && Intersects(box, ClipBounds)
                    && ContainsAxis(box.X, box.Width, bounds.X, bounds.Width, xClip)
                    && ContainsAxis(box.Y, box.Height, bounds.Y, bounds.Height, yClip);
                if (included && start < 0)
                    start = index;
                else if (!included && start >= 0)
                {
                    ranges.Add(new(start, index));
                    start = -1;
                }
            }
            if (start >= 0)
                ranges.Add(new(start, line.End));
        }
        return ranges.ToArray();
    }

    private static bool ContainsAxis(float position, float size, float minimum, float extent, UiAccessibilityTextClip clip)
    {
        var end = (double)position + size;
        var maximum = (double)minimum + extent;
        return position < maximum && end > minimum
            && (((int)clip & 1) == 0 || position >= minimum)
            && (((int)clip & 2) == 0 || end <= maximum);
    }

    private static bool Intersects(UiRect left, UiRect right) => right.Width > 0 && right.Height > 0
        && left.X < (double)right.X + right.Width && (double)left.X + left.Width > right.X
        && left.Y < (double)right.Y + right.Height && (double)left.Y + left.Height > right.Y;
}
