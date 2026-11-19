using Duxel.Core;

namespace Duxel.Svg;

/// <summary>Explicit format dispatch: SVG uses the shared store, ordinary formats use the supplied bitmap decoder.</summary>
public sealed class SvgImageDecoder(SvgStore store, IUiImageDecoder bitmapDecoder) : IUiImageDecoder
{
    private readonly SvgStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IUiImageDecoder _bitmap = bitmapDecoder ?? throw new ArgumentNullException(nameof(bitmapDecoder));
    public bool CanDecode(string path) => IsSvg(path) || _bitmap.CanDecode(path);
    public UiImageData Decode(string path)
    {
        if (!IsSvg(path)) return _bitmap.Decode(path);
        var svg = _store.Load(path);
        var size = svg.IntrinsicSize;
        return svg.Rasterize(checked((int)MathF.Ceiling(size.X)), checked((int)MathF.Ceiling(size.Y))).CopyImageData();
    }
    private static bool IsSvg(string path) => string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase);
}
