using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Maps an intrinsic document viewport into a target while keeping its clip separate from viewBox geometry.</summary>
internal readonly record struct SvgViewport(Matrix3x2 Transform, Vector4 Clip, bool Letterboxed)
{
    internal static float Coverage(Vector4 rectangle, int x, int y)
    {
        var horizontal = Math.Clamp(MathF.Min(x + 1, rectangle.X + rectangle.Z) - MathF.Max(x, rectangle.X), 0, 1);
        var vertical = Math.Clamp(MathF.Min(y + 1, rectangle.Y + rectangle.W) - MathF.Max(y, rectangle.Y), 0, 1);
        return MathF.Min(horizontal, vertical);
    }

    internal static SvgViewport Fit(float width, float height, int targetWidth, int targetHeight)
    {
        var scale = MathF.Min(targetWidth / width, targetHeight / height);
        var clip = new Vector4((targetWidth - width * scale) / 2, (targetHeight - height * scale) / 2, width * scale, height * scale);
        return new(Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(clip.X, clip.Y), clip, clip.X != 0 || clip.Y != 0);
    }
}
