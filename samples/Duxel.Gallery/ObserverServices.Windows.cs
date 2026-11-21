using Duxel.Platform.Windows;

namespace Duxel.Gallery;

internal sealed partial class ObserverServices
{
    internal static ObserverServices CreateWindows()
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return new(WindowsUiImageDecoder.Shared, new("Observer UI",
        [
            new("Observer UI", Path.Combine(fonts, "segoeui.ttf")),
            new("Observer UI", Path.Combine(fonts, "segoeuib.ttf")),
            new("Observer Korean", Path.Combine(fonts, "malgun.ttf"))
        ]), GalleryText.ConfigureFonts(new()).Typography);
    }
}
