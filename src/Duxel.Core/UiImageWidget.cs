namespace Duxel.Core;

/// <summary>Immutable copied RGBA pixels shared by widgets and contexts. File loading decodes once, before rendering.</summary>
public sealed class UiImageSource
{
    internal UiSvgRaster Raster
    {
        get;
    }
    internal bool PreserveColorChannels
    {
        get;
    }
    public int Width => Raster.Width;
    public int Height => Raster.Height;

    public UiImageSource(UiImageData image)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image.Height);
        ArgumentNullException.ThrowIfNull(image.RgbaPixels);
        var bytes = checked((long)image.Width * image.Height * 4);
        if (bytes > 64 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(image), "An image must fit the 64 MiB context texture budget.");
        if (image.RgbaPixels.Length != bytes)
            throw new ArgumentException("RGBA size does not match dimensions.", nameof(image));
        Raster = new(image.Width, image.Height, image.RgbaPixels.ToArray());
    }

    private UiImageSource(UiSvgRaster reference)
    {
        Raster = reference;
        PreserveColorChannels = true;
    }

    // Reference RGB stores affine coefficients, including channels beneath zero
    // alpha. Give it a distinct immutable cache identity without changing its source.
    internal UiImageSource AsBackdropReference() => new(Raster);

    public static UiImageSource LoadFromFile(string path)
    {
        var (width, height, pixels) = UiImageTexture.DecodeFile(path);
        return new(new UiImageData(width, height, pixels));
    }
}

internal readonly struct UiImageTextureProvider : IUiRasterTextureProvider<UiImageSource>
{
    public static long Revision(UiImageSource source) => 0;
    public static UiTextureUpdate Prepare(UiImageSource source, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera)
    {
        if (source.Width != width || source.Height != height)
            throw new InvalidOperationException("Bitmap texture uploads must preserve source dimensions.");
        return source.Raster.CreateTextureUpdate(texture, width, height) with
        {
            PreserveColorChannels = source.PreserveColorChannels
        };
    }
}

/// <summary>A decorative image borrowing immutable pixels and pinning one native-resolution texture per context.</summary>
public class UiImage : UiControlWidget
{
    private UiImageSource? _source;
    private readonly List<UiRasterTextureCache<UiImageSource, UiImageTextureProvider>.Lease> _leases = [];
    private UiVector2 _size;
    private UiColor _tint = new(0xFFFFFFFF);
    private float _cornerRadius;
    private UiImageFit _fit;

    public UiImage(UiImageSource source, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public UiImageSource Source
    {
        get
        {
            ThrowIfDisposed();
            return _source!;
        }
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_source, value))
                return;
            ThrowIfBuilding();
            ReleaseLeases();
            _source = value;
            RequestRebuild();
        }
    }

    /// <summary>Logical viewport. Zero uses native pixel dimensions. Parent allocation can override it.</summary>
    public UiVector2 Size
    {
        get => _size;
        set
        {
            ThrowIfDisposed();
            if (value != default && (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X <= 0 || value.Y <= 0))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_size == value)
                return;
            ThrowIfBuilding();
            _size = value;
            RequestRebuild();
        }
    }

    public UiColor Tint
    {
        get => _tint;
        set
        {
            ThrowIfDisposed();
            if (_tint == value)
                return;
            ThrowIfBuilding();
            _tint = value;
            RequestRebuild();
        }
    }

    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            ThrowIfDisposed();
            if (!float.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_cornerRadius == value)
                return;
            ThrowIfBuilding();
            _cornerRadius = value;
            RequestRebuild();
        }
    }
    public UiImageFit Fit
    {
        get => _fit;
        set
        {
            ThrowIfDisposed();
            if ((uint)value > 2)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_fit == value)
                return;
            ThrowIfBuilding();
            _fit = value;
            RequestRebuild();
        }
    }

    internal UiVector2 NaturalSize => _size == default ? new(Source.Width, Source.Height) : _size;
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        context.HasBoxConstraints ? context.LayoutConstraints.Constrain(NaturalSize) : NaturalSize;
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderImage(this);

    internal UiTextureId Acquire(UiRasterTextureCache<UiImageSource, UiImageTextureProvider> cache)
    {
        for (var i = _leases.Count - 1; i >= 0; i--)
        {
            var lease = _leases[i];
            if (lease.Owner is null)
            {
                _leases.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(lease.Owner, cache))
                return lease.Use();
        }
        var created = cache.Acquire(Source, Source.Width, Source.Height);
        _leases.Add(created);
        return created.Use();
    }

    private void ReleaseLeases()
    {
        foreach (var lease in _leases)
            lease.Dispose();
        _leases.Clear();
    }
    private protected override void ReleaseOwnedResources()
    {
        ReleaseLeases();
        _source = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiImage))]
[UiComponent("Image", UiComponentExposure.Fluent)]
public partial class UiFluentImage;

public sealed partial class UiImmediateContext
{
    private UiRasterTextureCache<UiImageSource, UiImageTextureProvider>? _imageTextureCache;
    public UiRasterTextureStatistics ImageTextureStatistics => _imageTextureCache?.Statistics ?? default;

    internal void RenderImage(UiImage image)
    {
        EnsureNotMeasuring();
        var size = ConstrainToLayout(image.NaturalSize);
        var cursor = AdvanceCursor(size);
        var bounds = new UiRect(cursor.X, cursor.Y, size.X, size.Y);
        PaintImage(image, bounds, image.Tint, image.CornerRadius, image.Fit);
    }

    internal void PaintImage(UiImage image, UiRect bounds, UiColor tint, float radius, UiImageFit fit)
    {
        EnsureNotMeasuring();
        ArgumentNullException.ThrowIfNull(image);
        _ = image.Source;
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width)
            || !float.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0
            || !float.IsFinite(radius) || radius < 0 || (uint)fit > 2)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        var texture = image.Acquire(_imageTextureCache ??= new(this));
        if (radius == 0 && fit == UiImageFit.Stretch)
            PaintImage(texture, bounds, tint);
        else
            _builder.AddImageRounded(texture, bounds,
                UiImageUvTransform.ForFit(new(image.Source.Width, image.Source.Height), new(bounds.Width, bounds.Height), fit),
                tint, radius, CurrentClipRect);
    }
}
