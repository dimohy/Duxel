namespace Duxel.Core;

/// <summary>Shared monotone-interval contract for SVG and native glyph cubic curves.</summary>
internal static class UiCubicBezierMath
{
    internal static (float First, float Second, int Count) YExtrema(float a, float b, float c, float d)
    {
        var first = 0f;
        var second = 0f;
        var count = 0;
        var quadratic = -(double)a + 3 * b - 3 * c + d;
        var linear = 2 * ((double)a - 2 * b + c);
        var constant = (double)b - a;
        void Root(double t)
        {
            if (t <= 0 || t >= 1)
                return;
            if (count == 0)
                first = (float)t;
            else
                second = (float)t;
            count++;
        }
        if (Math.Abs(quadratic) < 1e-20)
        {
            if (linear != 0)
                Root(-constant / linear);
        }
        else
        {
            var discriminant = linear * linear - 4 * quadratic * constant;
            if (discriminant >= 0)
            {
                var square = Math.Sqrt(discriminant);
                Root((-linear + square) / (2 * quadratic));
                Root((-linear - square) / (2 * quadratic));
            }
        }
        if (count == 2 && first > second)
            (first, second) = (second, first);
        return (first, second, count);
    }
}
