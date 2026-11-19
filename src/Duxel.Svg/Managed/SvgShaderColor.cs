using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Premultiplied shader-color precision before RGBA8 destination blending.</summary>
internal static class SvgShaderColor
{
    // Independent Canvas/WebGL readbacks establish this source precision; destination blending stays RGBA8.
    internal static Vector4 Quantize(Vector4 color) => new(QuantizeChannel(color.X), QuantizeChannel(color.Y), QuantizeChannel(color.Z), QuantizeChannel(color.W));

    internal static float QuantizeChannel(float value)
    {
        var quantized = MathF.Floor(value * 4096) / 4096;
        // Preserve the observed downward half tie through the nearest-even destination conversion.
        return quantized == .5f ? MathF.BitDecrement(quantized) : quantized;
    }
}
