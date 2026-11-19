using System.Numerics;
using Duxel.Svg.Managed;

internal static class BlurAxisTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var width in new[] { 1, 3, 17, 31 })
            foreach (var sigma in new[] { .35f, 2.2f, 9f })
                foreach (var sign in new[] { -1, 1 })
                {
                    const int height = 7;
                    var surface = new SvgRasterSurface(width, height);
                    var input = new Vector4[width * height];
                    for (var y = 0; y < height; y++)
                        for (var x = 0; x < width; x++)
                        {
                            var color = new Vector4((x * 31 % 256) / 255f, (y * 37 % 256) / 255f, .7f, (x + y) % 3 / 2f);
                            var geometry = new SvgGeometry([new(SvgSegmentKind.Move, new(x, y)), new(SvgSegmentKind.Line, new(x + 1, y)),
                    new(SvgSegmentKind.Line, new(x + 1, y + 1)), new(SvgSegmentKind.Line, new(x, y + 1)), new(SvgSegmentKind.Close, new(x, y))]);
                            surface.Fill(geometry, Matrix3x2.Identity, color);
                            input[y * width + x] = SvgCompositing.Quantize(SvgCompositing.Premultiply(color));
                        }
                    // Independent scalar integer-grid convolution, with zero outside the surface.
                    Vector4[] Convolve(Vector4[] source, bool horizontal)
                    {
                        var radius = (int)MathF.Ceiling(3 * sigma);
                        var kernel = new float[2 * radius + 1];
                        double total = 0;
                        for (var k = -radius; k <= radius; k++)
                        {
                            kernel[k + radius] = (float)Math.Exp(-.5 * k * k / ((double)sigma * sigma));
                            total += kernel[k + radius];
                        }
                        for (var k = 0; k < kernel.Length; k++)
                            kernel[k] /= (float)total;
                        var result = new Vector4[source.Length];
                        for (var y = 0; y < height; y++)
                            for (var x = 0; x < width; x++)
                                for (var k = -radius; k <= radius; k++)
                                {
                                    var sx = horizontal ? x + k * sign : x;
                                    var sy = horizontal ? y : y + k * sign;
                                    if ((uint)sx < (uint)width && (uint)sy < height)
                                        result[y * width + x] += source[sy * width + sx] * kernel[k + radius];
                                }
                        return result;
                    }
                    var expected = Convolve(Convolve(input, true), false);
                    surface.GaussianBlur(new(sign * sigma, 0), new(0, sign * sigma), false);
                    var actual = surface.CopyRgba();
                    var matches = true;
                    for (var i = 0; i < expected.Length; i++)
                    {
                        var value = SvgCompositing.Quantize(expected[i]);
                        var alpha = SvgCompositing.Channel(value.W);
                        matches &= actual[i * 4 + 3] == alpha;
                        for (var channel = 0; channel < 3; channel++)
                            matches &= actual[i * 4 + channel] == (alpha == 0 ? 0 : Duxel.Core.UiRgba8.Unpremultiply(SvgCompositing.Channel(value[channel]), alpha));
                    }
                    check(matches, $"Axis blur matches scalar convolution including edge padding and SIMD tails : width={width}, sigma={sigma}, sign={sign}");
                }
    }
}
