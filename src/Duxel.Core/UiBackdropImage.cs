namespace Duxel.Core;

/// <summary>Owns two image leases for immutable black/white reference renders of one affine paint operation.</summary>
public sealed class UiBackdropImage : IDisposable
{
    internal UiImage Black
    {
        get;
    }
    internal UiImage White
    {
        get;
    }
    private bool _disposed;

    public UiBackdropImage(UiImageSource black, UiImageSource white)
    {
        ArgumentNullException.ThrowIfNull(black);
        ArgumentNullException.ThrowIfNull(white);
        if (black.Width != white.Width || black.Height != white.Height)
            throw new ArgumentException("Backdrop reference images must have identical dimensions.");
        if ((long)black.Width * black.Height * 8 > 64 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(black), "Both reference images must fit the 64 MiB image cache budget.");
        var first = black.Raster.Pixels.Span;
        var second = white.Raster.Pixels.Span;
        for (var i = 0; i < first.Length; i += 4)
            if (second[i] < first[i] || second[i + 1] < first[i + 1] || second[i + 2] < first[i + 2]
                || second[i + 3] != 255)
                throw new ArgumentException("White reference RGB must not be below black reference RGB, and its alpha must be opaque.");
        Black = new(black.AsBackdropReference());
        White = new(white.AsBackdropReference());
    }

    internal void ValidateOwner() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        if (_disposed)
            return;
        Black.Dispose();
        White.Dispose();
        _disposed = true;
    }
}

public readonly partial struct UiControlContext
{
    public void BackdropImage(UiBackdropImage image, UiRect bounds, float opacity = 1) =>
        _ui.BackdropImage(image, bounds, opacity);
    public void BackdropImage(UiBackdropImage image, UiRect bounds, UiHorizontalOpacityMask mask, float opacity = 1) =>
        _ui.BackdropImage(image, bounds, mask, opacity);
    public void BackdropImage(UiBackdropImage image, UiRect bounds, UiImageUvTransform uv, UiHorizontalOpacityMask mask, float opacity = 1) =>
        _ui.BackdropImage(image, bounds, uv, mask, opacity);
}

public sealed partial class UiImmediateContext
{
    public void BackdropImage(UiBackdropImage image, UiRect bounds, float opacity = 1)
        => BackdropImageCore(image, bounds, opacity, null);

    public void BackdropImage(UiBackdropImage image, UiRect bounds, UiHorizontalOpacityMask mask, float opacity = 1)
        => BackdropImageCore(image, bounds, opacity, mask);

    public void BackdropImage(UiBackdropImage image, UiRect bounds, UiImageUvTransform uv, UiHorizontalOpacityMask mask, float opacity = 1)
        => BackdropImageCore(image, bounds, opacity, mask, uv);

    private void BackdropImageCore(UiBackdropImage image, UiRect bounds, float opacity, UiHorizontalOpacityMask? mask,
        UiImageUvTransform? uv = null)
    {
        EnsureNotMeasuring();
        mask?.Validate();
        uv?.Validate();
        ArgumentNullException.ThrowIfNull(image);
        image.ValidateOwner();
        UiDrawListBuilder.ValidateBackdropBounds(bounds, opacity);
        if (bounds.Width == 0 || bounds.Height == 0 || opacity == 0)
            return;
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        var cache = _imageTextureCache ??= new(this);
        var black = image.Black.Acquire(cache);
        var white = image.White.Acquire(cache);
        if (uv is { } source) _builder.AddBackdropComposite(black, white, bounds, source, mask ?? default, opacity);
        else if (mask is { } edges) _builder.AddBackdropComposite(black, white, bounds, edges, opacity);
        else _builder.AddBackdropComposite(black, white, bounds, opacity);
    }
}
