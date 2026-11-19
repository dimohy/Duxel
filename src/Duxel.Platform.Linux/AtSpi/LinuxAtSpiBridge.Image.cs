using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private static UiAccessibilityImageInfo Image(Node node) => node.Element?.Node.ImageInfo
        ?? throw Invalid("The object does not publish rendered image metadata.");

    private static GVariant ImageProperty(Node node, string property)
    {
        var image = Image(node);
        return property switch
        {
            "ImageDescription" => GVariant.String(image.Description),
            "ImageLocale" => GVariant.String(image.Locale ?? ""),
            _ => throw Invalid("Unknown image property: " + property)
        };
    }

    private GVariant ImageMethod(Node node, string method, GVariant input)
    {
        var image = Image(node);
        var bounds = PixelBounds(ToScreen(image.Bounds, _window));
        if (method == "GetImageSize")
            return GVariant.Tuple(GVariant.Int(bounds.Width), GVariant.Int(bounds.Height));
        bounds = CoordinateBounds(node, bounds, UInt(input, 0));
        return method switch
        {
            "GetImageExtents" => GVariant.Tuple(Rectangle(bounds)),
            "GetImagePosition" => GVariant.Tuple(GVariant.Int(bounds.X), GVariant.Int(bounds.Y)),
            _ => throw Invalid("Unknown image method: " + method)
        };
    }
}
