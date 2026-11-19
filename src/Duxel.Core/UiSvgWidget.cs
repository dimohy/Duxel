namespace Duxel.Core;

/// <summary>A borrowed retained SVG, rendered through context-owned shared textures. Dispose before the document owner.</summary>
public class UiSvg : UiControlWidget
{
    private readonly IUiSvgDocument _document;
    private readonly List<UiRasterTextureCache<IUiSvgDocument, UiSvgTextureProvider>.Lease> _leases = [];
    private UiVector2 _size;
    private float _rasterScale = 1;
    private bool _useFixedResolutionPreview;
    private UiColor _tint = new(0xFFFFFFFF);
    private float _cornerRadius;
    private UiColor _background;

    public UiSvg(IUiSvgDocument document, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        document.Changed += DocumentChanged;
    }

    public IUiSvgDocument Document => _document;
    /// <summary>Initial vector target color, before SVG painting. Transparent by default; distinct from final image tint.</summary>
    public UiColor Background
    {
        get => _background;
        set
        {
            ThrowIfDisposed();
            if (_background == value)
                return;
            ThrowIfBuilding();
            _background = value;
            RequestRebuild();
        }
    }
    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_cornerRadius == value)
                return;
            ThrowIfBuilding();
            _cornerRadius = value;
            RequestRebuild();
        }
    }
    /// <summary>Untransformed logical content size. Zero uses the document's intrinsic size.</summary>
    public UiVector2 Size
    {
        get => _size;
        set
        {
            ThrowIfDisposed();
            if (value != default)
                ValidateSize(value);
            if (_size == value)
                return;
            _size = value;
            RequestRebuild();
        }
    }
    public float Zoom
    {
        get => Transform.Scale;
        set
        {
            ThrowIfDisposed();
            ValidatePositive(value);
            if (Zoom == value)
                return;
            Transform = Transform with
            {
                Scale = value
            };
        }
    }
    /// <summary>GPU output resolution multiplier. SVG widgets never invoke the bitmap rasterizer.</summary>
    public float RasterScale
    {
        get => _rasterScale;
        set
        {
            ThrowIfDisposed();
            ValidatePositive(value);
            if (_rasterScale == value)
                return;
            _rasterScale = value;
            RequestRebuild();
        }
    }
    /// <summary>
    /// Reuses a full GPU texture at the untransformed resolution during a gesture.
    /// Disable after the gesture to render the visible area at its exact transformed resolution.
    /// </summary>
    public bool UseFixedResolutionPreview
    {
        get => _useFixedResolutionPreview;
        set
        {
            ThrowIfDisposed();
            if (_useFixedResolutionPreview == value)
                return;
            ThrowIfBuilding();
            _useFixedResolutionPreview = value;
            RequestRebuild();
        }
    }
    /// <summary>Compatibility shortcut to the common widget rotation. Layout mode reserves the transformed bounds.</summary>
    public float RotationDegrees
    {
        get => Transform.RotationDegrees;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value);
            if (RotationDegrees == value)
                return;
            Transform = Transform with
            {
                RotationDegrees = value
            };
        }
    }
    /// <summary>Compatibility shorthand for signed leading/trailing margins. Prefer explicit Margin edges.</summary>
    public UiVector2 Offset
    {
        get => new(Margin.Left - Margin.Right, Margin.Top - Margin.Bottom);
        set => Margin = new(MathF.Max(value.X, 0), MathF.Max(value.Y, 0), MathF.Max(-value.X, 0), MathF.Max(-value.Y, 0));
    }
    /// <summary>Multiplies authored RGBA colors; white preserves the SVG. This does not rewrite currentColor.</summary>
    public UiColor Tint
    {
        get => _tint;
        set
        {
            ThrowIfDisposed();
            if (_tint == value)
                return;
            _tint = value;
            RequestRebuild();
        }
    }
    private void DocumentChanged() => RequestRebuild();
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var size = NaturalSize();
        return context.HasBoxConstraints ? context.LayoutConstraints.Constrain(size) : size;
    }
    internal UiVector2 NaturalSize()
    {
        var size = ContentSize();
        ValidateSize(size);
        return size;
    }
    private UiVector2 ContentSize()
    {
        var size = Size == default ? _document.IntrinsicSize : Size;
        ValidateSize(size);
        return size;
    }
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSvg(this);
    internal string AccessibilityInputId(string name) => PrimaryInputId(name);
    internal void ObservePaint(UiImmediateContext ui)
    {
        ThrowIfDisposed();
        RememberFrameRequest(ui.FrameRequestCallback);
    }
    internal UiTextureId Acquire(UiRasterTextureCache<IUiSvgDocument, UiSvgTextureProvider> cache, int width, int height, UiGpuVectorCamera? camera = null)
    {
        for (var i = _leases.Count - 1; i >= 0; i--)
        {
            var lease = _leases[i];
            if (lease.Owner is null)
            {
                _leases.RemoveAt(i);
                continue;
            }
            if (!ReferenceEquals(lease.Owner, cache))
                continue;
            if (lease.Matches(_document, width, height, camera, _background))
                return lease.Use();
            var next = cache.Replace(lease, _document, width, height, camera, _background);
            _leases[i] = next;
            return next.Use();
        }
        var created = cache.Acquire(_document, width, height, camera, _background);
        _leases.Add(created);
        return created.Use();
    }
    private protected override void ReleaseOwnedResources()
    {
        _document.Changed -= DocumentChanged;
        foreach (var lease in _leases)
            lease.Dispose();
        _leases.Clear();
        base.ReleaseOwnedResources();
    }
    private static void ValidateSize(UiVector2 size)
    {
        ValidatePositive(size.X);
        ValidatePositive(size.Y);
    }
    private static void ValidatePositive(float value)
    {
        ValidateFinite(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
    }
    private static void ValidateFinite(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "SVG geometry must be finite.");
    }
}
