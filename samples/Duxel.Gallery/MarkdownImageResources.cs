using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MarkdownImageResources : IDisposable
{
    private Duxel.Svg.SvgStore? _imageStore;
    private readonly Dictionary<string, UiImageSource> _imageSources = new(StringComparer.Ordinal);
    internal Uri BaseUri { get; } = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg") + Path.DirectorySeparatorChar);

    internal UiImageSource? Resolve(Uri uri)
    {
        if (_imageSources.TryGetValue(uri.OriginalString, out var known))
            return known;
        UiImageSource source;
        if (uri.OriginalString == "memory:palette")
        {
            var pixels = new byte[160 * 48 * 4];
            for (var y = 0; y < 48; y++)
                for (var x = 0; x < 160; x++)
                {
                    var at = (y * 160 + x) * 4;
                    pixels[at] = (byte)(x * 255 / 159);
                    pixels[at + 1] = (byte)(y * 255 / 47);
                    pixels[at + 2] = 180;
                    pixels[at + 3] = 255;
                }
            source = new(new(160, 48, pixels));
        }
        else if (uri.IsAbsoluteUri && uri == new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "markdown-image.svg")))
        {
            var asset = (_imageStore ??= new()).Load(uri.LocalPath);
            source = new(asset.Rasterize(240, 96).CopyImageData());
        }
        else
            return null;
        _imageSources.Add(uri.OriginalString, source);
        return source;
    }
    public void Dispose()
    {
        _imageSources.Clear();
        _imageStore?.Dispose();
    }
}
