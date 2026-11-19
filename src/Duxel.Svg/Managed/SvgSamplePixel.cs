using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Four packed premultiplied RGBA8 samples, retained only where sample colors diverge.</summary>
internal readonly record struct SvgSamplePixel(uint A, uint B, uint C, uint D)
{
    internal bool Uniform => A == B && A == C && A == D;

    internal static SvgSamplePixel FromUniform(Vector4 color)
    {
        var packed = Pack(color);
        return new(packed, packed, packed, packed);
    }

    internal SvgSamplePixel Over(Vector4 foreground, byte mask)
    {
        uint Blend(uint background, int bit) => (mask & bit) == 0 ? background : Pack(SvgCompositing.Over(foreground, Unpack(background)));
        return new(Blend(A, 1), Blend(B, 2), Blend(C, 4), Blend(D, 8));
    }

    internal SvgSamplePixel Composite(SvgSamplePixel source, float opacity, float coverage = 1, float sourceCoverage = 1) => new(
        Pack(SvgCompositing.Over(SvgCompositing.LayerSource(Unpack(source.A), opacity, sourceCoverage) * coverage, Unpack(A))),
        Pack(SvgCompositing.Over(SvgCompositing.LayerSource(Unpack(source.B), opacity, sourceCoverage) * coverage, Unpack(B))),
        Pack(SvgCompositing.Over(SvgCompositing.LayerSource(Unpack(source.C), opacity, sourceCoverage) * coverage, Unpack(C))),
        Pack(SvgCompositing.Over(SvgCompositing.LayerSource(Unpack(source.D), opacity, sourceCoverage) * coverage, Unpack(D))));

    internal SvgSamplePixel Scale(float opacity) => new(Pack(Unpack(A) * opacity), Pack(Unpack(B) * opacity),
        Pack(Unpack(C) * opacity), Pack(Unpack(D) * opacity));

    internal SvgSamplePixel Mask(SvgSamplePixel mask, bool luminance)
    {
        uint Apply(uint color, uint value)
        {
            var pixel = Unpack(value);
            var alpha = luminance ? .2126f * pixel.X + .7152f * pixel.Y + .0722f * pixel.Z : pixel.W;
            return Pack(Unpack(color) * alpha);
        }
        return new(Apply(A, mask.A), Apply(B, mask.B), Apply(C, mask.C), Apply(D, mask.D));
    }

    internal Vector4 Resolve()
    {
        // The observed GPU resolve rounds exact half-byte ties down.
        static float Channel(SvgSamplePixel pixel, int shift) => (((pixel.A >> shift & 255) + (pixel.B >> shift & 255) + (pixel.C >> shift & 255) + (pixel.D >> shift & 255) + 1) / 4) / 255f;
        return new(Channel(this, 0), Channel(this, 8), Channel(this, 16), Channel(this, 24));
    }

    private static uint Pack(Vector4 color) => (uint)SvgCompositing.Channel(color.X) |
        (uint)SvgCompositing.Channel(color.Y) << 8 | (uint)SvgCompositing.Channel(color.Z) << 16 | (uint)SvgCompositing.Channel(color.W) << 24;

    private static Vector4 Unpack(uint color) => new Vector4(color & 255, color >> 8 & 255, color >> 16 & 255, color >> 24) / 255;
}
