namespace Duxel.Svg.Managed;

internal readonly record struct SvgDecodedImage(int Width, int Height, byte[] Rgba);
internal interface ISvgImageDecoder
{
    SvgDecodedImage Decode(byte[] bytes);
}
