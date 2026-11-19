namespace Duxel.Core;

public readonly record struct UiShadowLayer(UiVector2 Offset, float BlurRadius, UiColor Color, float Opacity = 1)
{
    internal void Validate()
    {
        if (!float.IsFinite(Offset.X) || !float.IsFinite(Offset.Y) || !float.IsFinite(BlurRadius)
            || BlurRadius < 0 || !float.IsFinite(Opacity) || Opacity < 0 || Opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(UiShadowLayer));
    }
    internal bool Visible => Color.Rgba >> 24 != 0 && Opacity > 0;
}

/// <summary>Two outer shadows in front-to-back CSS order. Blur uses sigma = radius / 2.</summary>
public readonly record struct UiBoxShadowOptions(UiShadowLayer First, UiShadowLayer Second = default)
{
    public void Validate()
    {
        First.Validate();
        Second.Validate();
    }
    public UiRect PaintBounds(UiRect bounds)
    {
        Validate();
        var left = 0f;
        var top = 0f;
        var right = 0f;
        var bottom = 0f;
        void Include(UiShadowLayer layer)
        {
            if (!layer.Visible)
                return;
            var support = layer.BlurRadius * 2 + 1;
            left = MathF.Max(left, support - layer.Offset.X);
            right = MathF.Max(right, support + layer.Offset.X);
            top = MathF.Max(top, support - layer.Offset.Y);
            bottom = MathF.Max(bottom, support + layer.Offset.Y);
        }
        Include(First);
        Include(Second);
        return new(bounds.X - left, bounds.Y - top, bounds.Width + left + right, bounds.Height + top + bottom);
    }
}

public enum UiShadowElevation
{
    Shadow2, Shadow4, Shadow8, Shadow16, Shadow28, Shadow64
}

public static class UiFluentShadow
{
    public static UiBoxShadowOptions Resolve(UiFluentColorScheme scheme, UiShadowElevation elevation)
    {
        if ((uint)scheme > 1 || (uint)elevation > 5)
            throw new ArgumentOutOfRangeException(nameof(elevation));
        var blur = elevation switch
        {
            UiShadowElevation.Shadow2 => 2,
            UiShadowElevation.Shadow4 => 4,
            UiShadowElevation.Shadow8 => 8,
            UiShadowElevation.Shadow16 => 16,
            UiShadowElevation.Shadow28 => 28,
            UiShadowElevation.Shadow64 => 64,
            _ => throw new ArgumentOutOfRangeException(nameof(elevation))
        };
        var dark = scheme == UiFluentColorScheme.Dark;
        return new(new(default, blur >= 28 ? 8 : 2, new(0, 0, 0), dark ? .24f : .12f),
            new(new(0, blur * .5f), blur, new(0, 0, 0), dark ? .28f : .14f));
    }
}

/// <summary>Owns shadow texture leases. Borrow for painting and dispose after its consumers.</summary>
public sealed class UiBoxShadow : IDisposable
{
    private UiBoxShadowOptions _options;
    private readonly record struct ShadowLease(UiShadowRasterSource Source,
        UiRasterTextureCache<UiShadowRasterSource, UiShadowTextureProvider>.Lease Texture);
    private readonly List<ShadowLease> _leases = [];
    private bool _disposed;
    public UiBoxShadow(UiBoxShadowOptions options)
    {
        options.Validate();
        _options = options;
    }
    public UiBoxShadowOptions Options
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _options;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            value.Validate();
            if (_options == value)
                return;
            _options = value;
        }
    }
    internal UiTextureId Acquire(UiRasterTextureCache<UiShadowRasterSource, UiShadowTextureProvider> cache,
        UiVector2 body, float radius, float resolution)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (var i = _leases.Count - 1; i >= 0; i--)
        {
            var lease = _leases[i];
            if (lease.Texture.Owner is null)
            {
                _leases.RemoveAt(i);
                continue;
            }
            if (!ReferenceEquals(lease.Texture.Owner, cache))
                continue;
            if (lease.Source.Matches(body, radius, resolution, _options))
                return lease.Texture.Use();
            lease.Texture.Dispose();
            _leases.RemoveAt(i);
        }
        var source = new UiShadowRasterSource(body, radius, resolution, _options);
        var created = cache.Acquire(source, source.Width, source.Height);
        _leases.Add(new(source, created));
        return created.Use();
    }

    private void ReleaseLeases()
    {
        foreach (var lease in _leases)
            lease.Texture.Dispose();
        _leases.Clear();
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        ReleaseLeases();
        _disposed = true;
    }
}

internal readonly struct UiShadowTextureProvider : IUiRasterTextureProvider<UiShadowRasterSource>
{
    public static long Revision(UiShadowRasterSource source) => 0;
    public static UiTextureUpdate Prepare(UiShadowRasterSource source, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera)
        => source.Rasterize().CreateTextureUpdate(texture, width, height);
}

public readonly partial struct UiControlContext
{
    /// <summary>Paint only. Callers reserve Options.PaintBounds and retain the shadow owner.</summary>
    public void BoxShadow(UiBoxShadow shadow, UiRect bounds, float cornerRadius = 0) =>
        _ui.PaintBoxShadow(shadow, bounds, cornerRadius);
}

public sealed partial class UiImmediateContext
{
    private UiRasterTextureCache<UiShadowRasterSource, UiShadowTextureProvider>? _shadowTextureCache;
    private UiDrawListBuilder? _shadowDepthBuilder;
    public UiRasterTextureStatistics ShadowTextureStatistics => _shadowTextureCache?.Statistics ?? default;
    internal void PaintBoxShadow(UiBoxShadow shadow, UiRect bounds, float radius)
    {
        EnsureNotMeasuring();
        ArgumentNullException.ThrowIfNull(shadow);
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width)
            || !float.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0
            || !float.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        if (bounds.Width == 0 || bounds.Height == 0 || (!shadow.Options.First.Visible && !shadow.Options.Second.Visible))
            return;
        var paint = shadow.Options.PaintBounds(bounds);
        var clip = IntersectRect(CurrentClipRect, paint);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        var texture = shadow.Acquire(_shadowTextureCache ??= new(this), new(bounds.Width, bounds.Height), radius, ContentScale);
        var destination = _builder;
        if (_builder.LayerScene3D is { WriteDepth: true } scene)
        {
            // Soft coverage is translucent material, never an opaque depth-writing sample mask.
            destination = _shadowDepthBuilder ??= new(CurrentClipRect);
            destination._SetClipRect(CurrentClipRect);
            destination._ResetForNewFrame();
            destination.SetLayerScene3D(scene with
            {
                WriteDepth = false
            });
            destination._SetDrawListSharedData(GetDrawListSharedData());
        }
        destination.AddImage(texture, new(paint.X, paint.Y), new(paint.X + paint.Width, paint.Y + paint.Height),
            new(0, 0), new(1, 1), new(255, 255, 255), CurrentClipRect);
        if (!ReferenceEquals(destination, _builder))
            _builder.AppendDeferredFrom(destination);
    }
}
