using System.Buffers;
using System.Numerics;

namespace Duxel.Core;

/// <summary>Refines a published bounding rectangle against the actual rendered action surface, in client coordinates.</summary>
public interface IUiAccessibilityPointGeometry
{
    bool ContainsPoint(float x, float y);
}

internal sealed record UiWidgetClipGeometry(UiRect Bounds, Matrix3x2 FromScreen, UiWidgetClipGeometry? Parent)
{
    internal static bool Contains(UiRect bounds, UiVector2 point) => point.X >= bounds.X && point.Y >= bounds.Y
        && point.X < bounds.X + bounds.Width && point.Y < bounds.Y + bounds.Height;

    internal bool ContainsPoint(UiVector2 screen)
    {
        for (var clip = this; clip is not null; clip = clip.Parent)
            if (!Contains(clip.Bounds, UiWidgetAffineLayout.MapPoint(screen, clip.FromScreen)))
                return false;
        return true;
    }
}

internal sealed record UiWidgetPointGeometry(UiRect LocalBounds, Matrix3x2 ToScreen, Matrix3x2 FromScreen,
    UiWidgetClipGeometry? Clips) : IUiAccessibilityPointGeometry
{
    public bool ContainsPoint(float x, float y)
    {
        var screen = new UiVector2(x, y);
        return UiWidgetClipGeometry.Contains(LocalBounds, UiWidgetAffineLayout.MapPoint(screen, FromScreen))
            && (Clips?.ContainsPoint(screen) ?? true);
    }

    internal bool VisibleGeometry(out UiRect bounds, out UiVector2 interiorPoint)
        => ClipPolygon(LocalBounds, ToScreen, Matrix3x2.Identity, Clips, out bounds, out interiorPoint);

    internal static bool ClipPolygon(UiRect localBounds, Matrix3x2 localToPolygon, Matrix3x2 polygonToScreen,
        UiWidgetClipGeometry? clips, out UiRect bounds, out UiVector2 interiorPoint)
    {
        var depth = 0;
        for (var clip = clips; clip is not null; clip = clip.Parent)
            depth++;
        var capacity = checked(4 + depth * 4);
        UiVector2[]? rentedA = null, rentedB = null;
        Span<UiVector2> input = capacity <= 64 ? stackalloc UiVector2[capacity]
            : (rentedA = ArrayPool<UiVector2>.Shared.Rent(capacity)).AsSpan(0, capacity);
        Span<UiVector2> output = capacity <= 64 ? stackalloc UiVector2[capacity]
            : (rentedB = ArrayPool<UiVector2>.Shared.Rent(capacity)).AsSpan(0, capacity);
        try
        {
            input[0] = UiWidgetAffineLayout.MapPoint(new(localBounds.X, localBounds.Y), localToPolygon);
            input[1] = UiWidgetAffineLayout.MapPoint(new(localBounds.X + localBounds.Width, localBounds.Y), localToPolygon);
            input[2] = UiWidgetAffineLayout.MapPoint(new(localBounds.X + localBounds.Width, localBounds.Y + localBounds.Height), localToPolygon);
            input[3] = UiWidgetAffineLayout.MapPoint(new(localBounds.X, localBounds.Y + localBounds.Height), localToPolygon);
            var count = 4;
            for (var clip = clips; clip is not null && count > 0; clip = clip.Parent)
            {
                var matrix = polygonToScreen * clip.FromScreen;
                for (var side = 0; side < 4 && count > 0; side++)
                {
                    (double a, double b, double c) = side switch
                    {
                        0 => (matrix.M11, matrix.M21, (double)matrix.M31 - clip.Bounds.X),
                        1 => (-matrix.M11, -matrix.M21, (double)clip.Bounds.X + clip.Bounds.Width - matrix.M31),
                        2 => (matrix.M12, matrix.M22, (double)matrix.M32 - clip.Bounds.Y),
                        _ => (-matrix.M12, -matrix.M22, (double)clip.Bounds.Y + clip.Bounds.Height - matrix.M32)
                    };
                    count = ClipPlane(input[..count], output, a, b, c);
                    var swap = input;
                    input = output;
                    output = swap;
                }
            }
            bounds = default;
            interiorPoint = default;
            if (count < 3)
                return false;
            var first = input[0];
            var left = first.X;
            var right = first.X;
            var top = first.Y;
            var bottom = first.Y;
            double sumX = 0, sumY = 0, area = 0;
            for (var i = 0; i < count; i++)
            {
                var point = input[i];
                var next = input[(i + 1) % count];
                left = MathF.Min(left, point.X);
                right = MathF.Max(right, point.X);
                top = MathF.Min(top, point.Y);
                bottom = MathF.Max(bottom, point.Y);
                sumX += point.X;
                sumY += point.Y;
                area += (double)point.X * next.Y - (double)next.X * point.Y;
            }
            if (area == 0 || right <= left || bottom <= top)
                return false;
            bounds = new(left, top, right - left, bottom - top);
            interiorPoint = new((float)(sumX / count), (float)(sumY / count));
            return true;
        }
        finally
        {
            if (rentedA is not null)
                ArrayPool<UiVector2>.Shared.Return(rentedA);
            if (rentedB is not null)
                ArrayPool<UiVector2>.Shared.Return(rentedB);
        }
    }

    private static int ClipPlane(ReadOnlySpan<UiVector2> input, Span<UiVector2> output, double a, double b, double c)
    {
        var count = 0;
        var previous = input[^1];
        var previousDistance = a * previous.X + b * previous.Y + c;
        foreach (var point in input)
        {
            var distance = a * point.X + b * point.Y + c;
            if ((distance >= 0) != (previousDistance >= 0))
            {
                var ratio = previousDistance / (previousDistance - distance);
                output[count++] = new((float)(previous.X + (point.X - previous.X) * ratio),
                    (float)(previous.Y + (point.Y - previous.Y) * ratio));
            }
            if (distance >= 0)
                output[count++] = point;
            previous = point;
            previousDistance = distance;
        }
        return count;
    }
}
