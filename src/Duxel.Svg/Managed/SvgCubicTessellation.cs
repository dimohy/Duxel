using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Device-space Wang subdivision and stable evaluation for uniformly tessellated cubic paths.</summary>
internal static class SvgCubicTessellation
{
    internal static int SegmentCount(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float tolerance)
    {
        var secondDifference = MathF.Max((a - 2 * b + c).Length(), (b - 2 * c + d).Length());
        var required = Math.Sqrt(.75 * secondDifference / tolerance);
        if (!double.IsFinite(required) || required > 524_288)
            throw new InvalidDataException("SVG cubic tessellation exceeds the edge limit.");
        var count = 1;
        while (count < required)
            count *= 2;
        return count;
    }

    internal static Vector2 Evaluate(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var ab = Vector2.Lerp(a, b, t);
        var bc = Vector2.Lerp(b, c, t);
        var cd = Vector2.Lerp(c, d, t);
        return Vector2.Lerp(Vector2.Lerp(ab, bc, t), Vector2.Lerp(bc, cd, t), t);
    }
}
