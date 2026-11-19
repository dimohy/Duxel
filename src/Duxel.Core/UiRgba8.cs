namespace Duxel.Core;

/// <summary>Shared byte-precision conversions at the straight/premultiplied RGBA8 boundary.</summary>
internal static class UiRgba8
{
    internal static byte Quantize(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);

    internal static byte Unpremultiply(byte value, byte alpha) => alpha == 0 ? (byte)0
        : (byte)Math.Clamp((value * 255 + alpha / 2) / alpha, 0, 255);

    internal static void Premultiply(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length != destination.Length || source.Length % 4 != 0)
            throw new ArgumentException("RGBA8 conversion requires equally sized complete pixels.");
        for (var i = 0; i < source.Length; i += 4)
        {
            var alpha = source[i + 3];
            destination[i] = (byte)((source[i] * alpha + 127) / 255);
            destination[i + 1] = (byte)((source[i + 1] * alpha + 127) / 255);
            destination[i + 2] = (byte)((source[i + 2] * alpha + 127) / 255);
            destination[i + 3] = alpha;
        }
    }
}
