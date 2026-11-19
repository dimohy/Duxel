using System.Numerics;

namespace Duxel.Core;

public enum UiWidgetTransformMode
{
    Layout,
    Visual
}

/// <summary>Uniform scale and clockwise rotation. Visual mode preserves the layout anchor; layout mode normalizes transformed bounds.</summary>
public sealed record UiWidgetTransform(float Scale = 1, float RotationDegrees = 0,
    UiVector2? Origin = null, UiWidgetTransformMode Mode = UiWidgetTransformMode.Layout)
{
    public static UiWidgetTransform Identity { get; } = new();
    internal bool IsIdentity => ReferenceEquals(this, Identity) || Scale == 1 && RotationDegrees % 360 == 0;

    internal void Validate()
    {
        if (!float.IsFinite(Scale) || Scale <= 0 || !float.IsFinite(RotationDegrees))
            throw new ArgumentOutOfRangeException(nameof(Scale));
        if (Origin is { } origin && (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y)
            || origin.X < 0 || origin.X > 1 || origin.Y < 0 || origin.Y > 1))
            throw new ArgumentOutOfRangeException(nameof(Origin));
        if (Mode is not (UiWidgetTransformMode.Layout or UiWidgetTransformMode.Visual))
            throw new ArgumentOutOfRangeException(nameof(Mode));
    }

    internal UiVector2 ContentMaximum(UiVector2 maximum)
    {
        if (Mode == UiWidgetTransformMode.Visual || IsIdentity)
            return maximum;
        var (sin, cos) = UiWidgetAffineLayout.RotationSinCos(RotationDegrees);
        double a = MathF.Abs(cos * Scale), b = MathF.Abs(sin * Scale);
        double x = maximum.X, y = maximum.Y;
        if (double.IsPositiveInfinity(x) && double.IsPositiveInfinity(y))
            return maximum;
        if (b == 0)
            return new((float)(x / a), (float)(y / a));
        if (a == 0)
            return new((float)(y / b), (float)(x / b));
        // The feasible local rectangle lies below two linear width/height constraints.
        // An area maximum is a constraint midpoint or their positive-quadrant intersection.
        double bestWidth = 0, bestHeight = 0, bestArea = -1;
        void Consider(double width, double height)
        {
            if (width < 0 || height < 0 || !double.IsFinite(width) || !double.IsFinite(height))
                return;
            var projectedX = a * width + b * height;
            var projectedY = b * width + a * height;
            var fit = 1d;
            if (projectedX > x)
                fit = Math.Min(fit, x / projectedX);
            if (projectedY > y)
                fit = Math.Min(fit, y / projectedY);
            width *= fit;
            height *= fit;
            var area = width * height;
            if (area <= bestArea)
                return;
            (bestWidth, bestHeight, bestArea) = (width, height, area);
        }
        if (double.IsFinite(x))
            Consider(x / (2 * a), x / (2 * b));
        if (double.IsFinite(y))
            Consider(y / (2 * b), y / (2 * a));
        var determinant = a * a - b * b;
        if (determinant != 0 && double.IsFinite(x) && double.IsFinite(y))
            Consider((x * a - y * b) / determinant, (y * a - x * b) / determinant);
        return new((float)bestWidth, (float)bestHeight);
    }

    internal UiWidgetAffineLayout Arrange(UiVector2 content, UiVector2 position)
    {
        var (sin, cos) = UiWidgetAffineLayout.RotationSinCos(RotationDegrees);
        var matrix = new Matrix3x2(cos * Scale, sin * Scale, -sin * Scale, cos * Scale, 0, 0);
        var projected = UiWidgetAffineLayout.MapBounds(new(0, 0, content.X, content.Y), matrix);
        if (!float.IsFinite(projected.X) || !float.IsFinite(projected.Y)
            || !float.IsFinite(projected.Width) || !float.IsFinite(projected.Height))
            throw new ArgumentOutOfRangeException(nameof(Scale), "Transformed widget bounds exceed the numeric range.");
        if (Mode == UiWidgetTransformMode.Layout)
            matrix.Translation = new(position.X - projected.X, position.Y - projected.Y);
        else
        {
            var origin = Origin ?? new(.5f, .5f);
            var pivot = new Vector2(content.X * origin.X, content.Y * origin.Y);
            matrix.Translation = new Vector2(position.X, position.Y) + pivot - Vector2.Transform(pivot, matrix);
        }
        return new(content, Mode == UiWidgetTransformMode.Layout ? new(projected.Width, projected.Height) : content, matrix);
    }
}

internal readonly record struct UiWidgetAffineLayout(UiVector2 ContentSize, UiVector2 Extent, Matrix3x2 Matrix)
{
    internal static (float Sine, float Cosine) RotationSinCos(float rotationDegrees)
    {
        var angle = rotationDegrees % 360;
        if (angle < 0)
            angle += 360;
        return angle switch
        {
            0 => (0f, 1f),
            90 => (1f, 0f),
            180 => (0f, -1f),
            270 => (-1f, 0f),
            _ => MathF.SinCos(angle * (MathF.PI / 180))
        };
    }

    internal static bool TryInvert(Matrix3x2 matrix, out Matrix3x2 inverse)
    {
        var determinant = (double)matrix.M11 * matrix.M22 - (double)matrix.M12 * matrix.M21;
        inverse = new((float)(matrix.M22 / determinant), (float)(-matrix.M12 / determinant),
            (float)(-matrix.M21 / determinant), (float)(matrix.M11 / determinant),
            (float)(((double)matrix.M21 * matrix.M32 - (double)matrix.M31 * matrix.M22) / determinant),
            (float)(((double)matrix.M31 * matrix.M12 - (double)matrix.M11 * matrix.M32) / determinant));
        return determinant != 0 && float.IsFinite(inverse.M11) && float.IsFinite(inverse.M12)
            && float.IsFinite(inverse.M21) && float.IsFinite(inverse.M22) && float.IsFinite(inverse.M31) && float.IsFinite(inverse.M32);
    }

    internal static UiVector2 MapPoint(UiVector2 point, Matrix3x2 matrix)
    {
        var mapped = Vector2.Transform(new(point.X, point.Y), matrix);
        return new(mapped.X, mapped.Y);
    }

    internal static UiRect MapBounds(UiRect bounds, Matrix3x2 matrix)
    {
        var a = MapPoint(new(bounds.X, bounds.Y), matrix);
        var b = MapPoint(new(bounds.X + bounds.Width, bounds.Y), matrix);
        var c = MapPoint(new(bounds.X, bounds.Y + bounds.Height), matrix);
        var d = MapPoint(new(bounds.X + bounds.Width, bounds.Y + bounds.Height), matrix);
        var left = MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X));
        var top = MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y));
        return new(left, top, MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X)) - left,
            MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y)) - top);
    }

    internal static Matrix4x4 ToClipMatrix(Matrix3x2 matrix, UiRect display)
    {
        var pixel = new Matrix4x4(matrix.M11, matrix.M12, 0, 0, matrix.M21, matrix.M22, 0, 0,
            0, 0, 1, 0, matrix.M31, matrix.M32, 0, 1);
        return Matrix4x4.CreateScale(display.Width / 2, display.Height / 2, 1)
            * Matrix4x4.CreateTranslation(display.X + display.Width / 2, display.Y + display.Height / 2, 0) * pixel
            * Matrix4x4.CreateTranslation(-display.X - display.Width / 2, -display.Y - display.Height / 2, 0)
            * Matrix4x4.CreateScale(2 / display.Width, 2 / display.Height, 1);
    }
}
