using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed partial class ObserverServices(IUiImageDecoder imageDecoder, SvgFontOptions svgFonts, UiTypographyFonts? typographyFonts = null)
{
    internal UiTypographyFonts? TypographyFonts { get; } = typographyFonts;
    internal IUiImageDecoder ImageDecoder { get; } = imageDecoder ?? throw new ArgumentNullException(nameof(imageDecoder));
    internal SvgFontOptions SvgFonts { get; } = svgFonts ?? throw new ArgumentNullException(nameof(svgFonts));
}
