using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal sealed class SvgEmbeddedImageDecoder : ISvgImageDecoder
{
    public SvgDecodedImage Decode(byte[] bytes)
    {
        if (OperatingSystem.IsWindows()) return SvgWicImageDecoder.Decode(bytes);
        if (OperatingSystem.IsLinux()) return SvgGdkImageDecoder.Decode(bytes);
        throw new PlatformNotSupportedException("Embedded SVG bitmaps require Windows WIC or Linux GdkPixbuf.");
    }
}
