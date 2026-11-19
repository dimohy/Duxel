namespace Duxel.Core;

public enum UiScrollAxis
{
    Vertical,
    Horizontal,
    Both
}

public readonly record struct UiScrollOptions(UiScrollAxis Axis = UiScrollAxis.Vertical, float? Width = null, float? Height = null)
{
    public bool RightToLeft { get; init; }
    internal bool Vertical => Axis != UiScrollAxis.Horizontal;
    internal bool Horizontal => Axis != UiScrollAxis.Vertical;

    public UiScrollOptions Validate()
    {
        if (Axis is < UiScrollAxis.Vertical or > UiScrollAxis.Both
            || Width is { } width && (!float.IsFinite(width) || width < 0)
            || Height is { } height && (!float.IsFinite(height) || height < 0))
            throw new ArgumentOutOfRangeException(nameof(UiScrollOptions));
        return this;
    }
}

internal readonly record struct UiScrollLayout(UiVector2 Size, UiVector2 Content, UiVector2 Viewport, float ScrollbarThickness);

/// <summary>Owns one naturally measured child and a finite, clipped scrolling viewport.</summary>
[UiComponent("ScrollView", UiComponentExposure.Infrastructure)]
public class UiScrollView : UiControlWidget
{
    private UiWidget? _child;
    private UiScrollOptions _options;
    private UiVector2 _offset;
    private UiRetainedLayout<UiScrollLayout>? _retained;
    private readonly UiScrollOwner _owner = new();

    public UiScrollView(UiWidget child, UiScrollOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(child);
        options.Validate();
        OwnChild("content", child);
        _child = child;
        _options = options;
    }

    public UiWidget Child
    {
        get
        {
            ThrowIfDisposed();
            return _child!;
        }
    }

    public UiScrollOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            if (_options.RightToLeft != value.RightToLeft)
                _owner.Release();
            _options = value;
            RequestRebuild();
        }
    }

    /// <summary>Requested content offset from the logical start. Rendering clamps it to the current content range.</summary>
    public UiVector2 Offset
    {
        get
        {
            ThrowIfDisposed();
            return _offset;
        }
        set
        {
            ThrowIfDisposed();
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < 0 || value.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_offset == value)
                return;
            ThrowIfBuilding();
            _offset = value;
            RequestRebuild();
        }
    }

    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();

    private UiScrollLayout GetLayout(UiControlContext context, bool render = false)
    {
        var allowed = context.CanRetainLayoutMeasurement && Child.RetainsMeasurement;
        if (_retained is { } cached && cached.TryGet(context, render, out var previous, allowed)
            && previous.ScrollbarThickness == context.ScrollViewScrollbarThickness)
            return previous;
        _retained?.Clear();
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var layout = context.MeasureScrollView(Child, _options);
        cache.Commit(context, attempt, layout, layout.Size, allowed);
        return layout;
    }

    protected override UiVector2 MeasureContent(UiControlContext context) => GetLayout(context).Size;

    protected override void RenderControl(UiImmediateContext ui)
    {
        var layout = GetLayout(new(ui), render : true);
        _offset = ui.RenderScrollView(PrimaryInputId("ScrollView"), Child, layout, _offset, _owner, _options.RightToLeft || ui.RightToLeft);
    }

    private protected override void ReleaseOwnedResources()
    {
        _owner.Release();
        _child = null;
        _retained = null;
        base.ReleaseOwnedResources();
    }
}

internal sealed class UiScrollOwner
{
    private readonly WeakReference<UiImmediateContext> _context = new(null!);
    private string? _id;
    private byte _axes;
    internal string VerticalId { get; private set; } = string.Empty;
    internal string HorizontalId { get; private set; } = string.Empty;

    internal bool IsBoundTo(UiImmediateContext context, string id) =>
        _id == id && _context.TryGetTarget(out var previous) && ReferenceEquals(previous, context);

    internal void ReleaseAuxiliary(string suffix)
    {
        if (_id is not null && _context.TryGetTarget(out var context))
            context.ReleaseOwnedScrollbar(_id + suffix);
    }

    internal void Bind(UiImmediateContext context, string id)
    {
        if (_id == id && _context.TryGetTarget(out var previous) && ReferenceEquals(previous, context))
            return;
        Release();
        _id = id;
        VerticalId = id + "##scroll-v";
        HorizontalId = id + "##scroll-h";
        _context.SetTarget(context);
    }

    internal void Release()
    {
        ReleaseAxis(horizontal : false);
        ReleaseAxis(horizontal : true);
        _context.SetTarget(null!);
        _id = null;
        VerticalId = string.Empty;
        HorizontalId = string.Empty;
    }

    internal void UseAxis(bool horizontal) => _axes |= (byte)(horizontal ? 2 : 1);

    internal void ReleaseAxis(bool horizontal)
    {
        var mask = (byte)(horizontal ? 2 : 1);
        if ((_axes & mask) == 0)
            return;
        if (_context.TryGetTarget(out var context))
            context.ReleaseOwnedScrollbar(horizontal ? HorizontalId : VerticalId);
        _axes &= (byte)~mask;
    }
}

public static partial class Dux
{
    public static UiScrollView ScrollView(UiWidget child, UiScrollOptions options = default) => new(child, options);
}
