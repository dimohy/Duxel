namespace Duxel.Core;

/// <summary>Owns one live popup surface raster lease; ordinary movement reuses its shape.</summary>
public sealed class UiPopupSurface : IDisposable
{
    private UiPopupSurfaceRaster? _source;
    private UiRasterTextureCache<UiPopupSurfaceRaster, UiPopupSurfaceTextureProvider>.Lease? _lease;
    private bool _disposed;
    internal UiPopupLayout? MotionRasterLayout;
    internal UiTextureId Acquire(UiRasterTextureCache<UiPopupSurfaceRaster, UiPopupSurfaceTextureProvider> cache,
        UiImmediateContext.FluentPopupSurfaceKey key, int margin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_lease?.Owner != cache || _source?.Key != key)
        {
            _lease?.Dispose();
            _source = new(key, margin);
            _lease = cache.Acquire(_source, _source.Width, _source.Height);
        }
        return _lease!.Use();
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lease?.Dispose();
        _lease = null;
        _source = null;
    }
}

internal sealed class UiPopupSurfaceRaster
{
    internal UiImmediateContext.FluentPopupSurfaceKey Key
    {
        get;
    }
    internal int Margin
    {
        get;
    }
    internal int Width
    {
        get;
    }
    internal int Height
    {
        get;
    }
    internal UiPopupSurfaceRaster(UiImmediateContext.FluentPopupSurfaceKey key, int margin)
    {
        var width = Math.Ceiling(key.Width * (double)key.Scale + key.PhaseX) + 2d * margin;
        var height = Math.Ceiling(key.Height * (double)key.Scale + key.PhaseY) + 2d * margin;
        if (width > 4096 || height > 4096 || width * height > 4_194_304)
            throw new NotSupportedException("The popup surface exceeds the bounded raster size.");
        (Key, Margin, Width, Height) = (key, margin, (int)width, (int)height);
    }
}
internal readonly struct UiPopupSurfaceTextureProvider : IUiRasterTextureProvider<UiPopupSurfaceRaster>
{
    public static long Revision(UiPopupSurfaceRaster source) => 0;
    public static UiTextureUpdate Prepare(UiPopupSurfaceRaster source, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera)
    {
        var image = UiImmediateContext.RasterizeFluentPopupSurface(source.Key, source.Margin);
        return new UiSvgRaster(image.Width, image.Height, image.RgbaPixels).CreateTextureUpdate(texture, width, height);
    }
}

public readonly partial struct UiControlContext
{
    /// <summary>Paints the rounded body and optional arrow as one source before sequential Fluent drop shadows.</summary>
    public void FluentPopupSurface(UiPopupSurface surface, UiPopupLayout layout, UiColor fill)
        => _ui.PaintFluentPopupSurface(surface, layout, fill);
}
public sealed partial class UiImmediateContext
{
    private UiRasterTextureCache<UiPopupSurfaceRaster, UiPopupSurfaceTextureProvider>? _popupSurfaceTextureCache;
    public UiRasterTextureStatistics PopupSurfaceTextureStatistics => _popupSurfaceTextureCache?.Statistics ?? default;
    internal void PaintFluentPopupSurface(UiPopupSurface owner, UiPopupLayout layout, UiColor fill)
    {
        EnsureNotMeasuring();
        ArgumentNullException.ThrowIfNull(owner);
        UiPopupPlacement.ValidateRect(layout.Bounds);
        if ((byte)(fill.Rgba >> 24) != 255)
            throw new ArgumentException("Fluent popup surface fill must be opaque.", nameof(fill));
        var rasterLayout = owner.MotionRasterLayout ?? layout;
        var body = rasterLayout.Bounds;
        var scale = ContentScale;
        if (!float.IsFinite(scale) || scale <= 0 || !float.IsFinite(body.X * scale) || !float.IsFinite(body.Y * scale))
            throw new ArgumentOutOfRangeException(nameof(layout));
        var pixelX = MathF.Floor(body.X * scale);
        var pixelY = MathF.Floor(body.Y * scale);
        var center = default(UiVector2);
        var direction = -1;
        var arrowHeight = 0f;
        if (rasterLayout.Arrow is { } arrow)
        {
            center = new((arrow.BaseStart.X + arrow.BaseEnd.X) / 2 - body.X, (arrow.BaseStart.Y + arrow.BaseEnd.Y) / 2 - body.Y);
            var dx = arrow.Tip.X - body.X - center.X;
            var dy = arrow.Tip.Y - body.Y - center.Y;
            arrowHeight = MathF.Abs(dx) + MathF.Abs(dy);
            var edge = MathF.Abs(arrow.BaseEnd.X - arrow.BaseStart.X) + MathF.Abs(arrow.BaseEnd.Y - arrow.BaseStart.Y);
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(arrowHeight)
                || arrowHeight <= 0 || dx != 0 && dy != 0 || MathF.Abs(edge - arrowHeight * 1.414f) > .002f)
                throw new ArgumentException("A Fluent arrow must face one axis and use the source square dimensions.", nameof(layout));
            direction = dy > 0 ? 0 : dy < 0 ? 1 : dx < 0 ? 2 : 3;
        }
        var key = new FluentPopupSurfaceKey(body.Width, body.Height, body.X * scale - pixelX, body.Y * scale - pixelY,
            scale, center, direction, ControlFluentColorScheme == UiFluentColorScheme.Dark, false, arrowHeight, fill, 16, 8);
        var marginPixels = Math.Ceiling((72d + arrowHeight) * scale);
        if (marginPixels > 2048)
            throw new NotSupportedException("The popup scale exceeds the bounded raster size.");
        var margin = (int)marginPixels;
        var texture = owner.Acquire(_popupSurfaceTextureCache ??= new(this), key, margin);
        var width = Math.Ceiling(body.Width * (double)scale + key.PhaseX) + 2d * margin;
        var height = Math.Ceiling(body.Height * (double)scale + key.PhaseY) + 2d * margin;
        var translation = new UiVector2(layout.Bounds.X - body.X, layout.Bounds.Y - body.Y);
        var origin = new UiVector2((pixelX - margin) / scale + translation.X,
            (pixelY - margin) / scale + translation.Y);
        _builder.AddImage(texture, origin, new(origin.X + (float)width / scale, origin.Y + (float)height / scale),
            default, new(1, 1), new(255, 255, 255), CurrentClipRect);
    }
}
