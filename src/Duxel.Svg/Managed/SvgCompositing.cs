using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>One premultiplied source-over contract for fills, filtered layers and final target composition.</summary>
internal static class SvgCompositing
{
    internal static Vector4 Premultiply(Vector4 color) => new(color.X * color.W, color.Y * color.W, color.Z * color.W, color.W);
    internal static Vector4 Unpremultiply(Vector4 color) => color.W == 0 ? Vector4.Zero : new(color.X / color.W, color.Y / color.W, color.Z / color.W, color.W);
    internal static Vector4 Over(Vector4 foreground, Vector4 background)
    {
        // Store all source channels before blending, including the destination attenuation alpha.
        foreground = Quantize(foreground);
        return Quantize(foreground + background * (1 - foreground.W));
    }
    internal static Vector4 Quantize(Vector4 color) => new Vector4(Channel(color.X), Channel(color.Y), Channel(color.Z), Channel(color.W)) / 255;
    // A stored layer is sampled first; authored opacity is applied at source precision before the target write.
    internal static Vector4 LayerSource(Vector4 color, float opacity, float coverage = 1) => opacity == 1
        ? color * coverage : SvgShaderColor.Quantize(color * opacity * coverage);
    // Folded solid paint alpha uses nearest-byte conversion with downward half ties.
    // Gradient paint-server opacity has a separate conversion contract.
    internal static float PaintOpacity(float opacity) => Math.Clamp(MathF.Ceiling(opacity * 255 - .5f), 0, 255) / 255;
    internal static float PaintServerOpacity(float opacity) => Math.Clamp(MathF.Floor(opacity * 255 + .5f), 0, 255) / 255;
    internal static byte Channel(float value) => Duxel.Core.UiRgba8.Quantize(value);
}
