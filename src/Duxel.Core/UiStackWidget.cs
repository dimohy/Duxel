namespace Duxel.Core;

public enum UiStackFit
{
    Loose, Expand, Passthrough
}
public readonly record struct UiStackOptions(UiAlignment? Alignment = null, UiStackFit Fit = UiStackFit.Loose)
{
    public UiStackOptions Validate()
    {
        Alignment?.Validate();
        if ((uint)Fit > 2)
            throw new ArgumentOutOfRangeException(nameof(Fit));
        return this;
    }
}
public readonly record struct UiPositionedOptions(float? Left = null, float? Top = null, float? Right = null,
    float? Bottom = null, float? Width = null, float? Height = null)
{
    public bool IsPositioned => Left.HasValue || Top.HasValue || Right.HasValue || Bottom.HasValue || Width.HasValue || Height.HasValue;
    public UiPositionedOptions Validate()
    {
        static bool Invalid(float? v) => v.HasValue && !float.IsFinite(v.Value);
        if (Invalid(Left) || Invalid(Top) || Invalid(Right) || Invalid(Bottom) || Invalid(Width) || Invalid(Height)
            || Width < 0 || Height < 0 || Left.HasValue && Right.HasValue && Width.HasValue
            || Top.HasValue && Bottom.HasValue && Height.HasValue)
            throw new ArgumentOutOfRangeException(nameof(UiPositionedOptions));
        return this;
    }
}

/// <summary>Supplies edge constraints to the nearest Stack through composition-only widgets.</summary>
public class UiPositionedWidget : UiControlWidget
{
    private UiWidget? _child;
    private UiPositionedOptions _options;
    public UiWidget Child
    {
        get
        {
            ThrowIfDisposed();
            return _child!;
        }
    }
    protected override bool CanRetainMeasurement => Child.RetainsMeasurement;
    public UiPositionedOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiPositionedWidget(UiWidget child, UiPositionedOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(child);
        options.Validate();
        OwnChild("content", child);
        _child = child;
        _options = options;
    }
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        context.RequireStackParent(this);
        return context.MeasureConstrained(Child, context.LayoutConstraints);
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        ui.RequireStackParent(this);
        var context = new UiControlContext(ui);
        var size = context.AllocatedLayoutSize;
        var origin = ui.AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        ui.RenderLayoutChild(Child, bounds, bounds);
        ui.FinishLayoutBox(origin, size);
    }
    private protected override void ReleaseOwnedResources()
    {
        _child = null;
        base.ReleaseOwnedResources();
    }
}

/// <summary>Measures ordinary children before edge-positioned children and clips paint and input to its box.</summary>
public class UiStackWidget : UiMultiChildWidget
{
    private UiRect[] _rects;
    private UiPositionedWidget?[] _positioned;
    private UiStackOptions _options;
    private UiRetainedLayout<UiVector2>? _retained;
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();
    public UiStackOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiStackWidget(UiWidget[] children, UiStackOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(ValidateOptions(children, options), identity)
    {
        _options = options;
        _rects = new UiRect[children.Length];
        _positioned = new UiPositionedWidget?[children.Length];
    }
    private static UiWidget[] ValidateOptions(UiWidget[] children, UiStackOptions options)
    {
        options.Validate();
        return children;
    }
    private UiVector2 MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } previous && previous.TryGet(context, render, out var size, allowed))
            return size;
        _retained?.Clear();
        var retain = allowed && ChildrenRetainMeasurement;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var result = MeasureUncachedLayout(context);
        cache.Commit(context, attempt, result, result, retain);
        return result;
    }
    private UiVector2 MeasureUncachedLayout(UiControlContext context)
    {
        var constraints = context.LayoutConstraints.Validate();
        var alignment = _options.Alignment ?? UiAlignment.TopLeft;
        var maximum = new UiVector2(constraints.MaxWidth, constraints.MaxHeight);
        if (_options.Fit == UiStackFit.Expand && (!float.IsFinite(maximum.X) || !float.IsFinite(maximum.Y)))
            throw new InvalidOperationException("Stack Expand requires bounded width and height.");
        var childConstraints = _options.Fit switch
        {
            UiStackFit.Expand => UiBoxConstraints.Tight(maximum),
            UiStackFit.Passthrough => constraints,
            _ => UiBoxConstraints.Loose(maximum)
        };
        var desired = new UiVector2(constraints.MinWidth, constraints.MinHeight);
        var ordinary = 0;
        for (var i = 0; i < Children.Length; i++)
        {
            var positioned = context.ResolveLayoutRoot(Children[i]) as UiPositionedWidget;
            _positioned[i] = positioned;
            if (positioned?.Options.IsPositioned == true)
                continue;
            var size = context.MeasureParentDataChild(Children[i], positioned, childConstraints);
            _rects[i] = new(0, 0, size.X, size.Y);
            ordinary++;
            desired = new(MathF.Max(desired.X, size.X), MathF.Max(desired.Y, size.Y));
        }
        if (ordinary == 0 && Children.Length > 0 || _options.Fit == UiStackFit.Expand)
        {
            if (!float.IsFinite(maximum.X) || !float.IsFinite(maximum.Y))
                throw new InvalidOperationException("A Stack without ordinary children requires bounded width and height.");
            desired = maximum;
        }
        var stackSize = constraints.Constrain(desired);
        for (var i = 0; i < Children.Length; i++)
        {
            var p = _positioned[i];
            var options = p?.Options ?? default;
            var rect = _rects[i];
            if (options.IsPositioned)
            {
                static float? Extent(float? start, float? end, float? extent, float available)
                {
                    if (!start.HasValue || !end.HasValue)
                        return extent;
                    var result = Math.Max(0, (double)available - start.Value - end.Value);
                    if (result > float.MaxValue)
                        throw new InvalidOperationException("Positioned constraints exceed finite layout capacity.");
                    return (float)result;
                }
                var width = Extent(options.Left, options.Right, options.Width, stackSize.X);
                var height = Extent(options.Top, options.Bottom, options.Height, stackSize.Y);
                var size = context.MeasureParentDataChild(Children[i], p, new(width ?? 0, width ?? float.PositiveInfinity, height ?? 0, height ?? float.PositiveInfinity));
                rect = new(0, 0, size.X, size.Y);
            }
            var x = options.Left ?? (options.Right is { } right ? stackSize.X - right - rect.Width : (stackSize.X - rect.Width) * (alignment.X + 1) * .5f);
            var y = options.Top ?? (options.Bottom is { } bottom ? stackSize.Y - bottom - rect.Height : (stackSize.Y - rect.Height) * (alignment.Y + 1) * .5f);
            if (!float.IsFinite(x) || !float.IsFinite(y))
                throw new InvalidOperationException("Positioned offsets exceed finite layout capacity.");
            _rects[i] = rect with
            {
                X = x,
                Y = y
            };
        }
        return stackSize;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var size = MeasureLayout(new(ui), render: true);
        var origin = ui.AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var topmost = -1;
        for (var i = Children.Length - 1; i >= 0; i--)
        {
            var rect = _rects[i];
            if (ui.IsLayoutPointerInside(new(origin.X + rect.X, origin.Y + rect.Y, rect.Width, rect.Height), root))
            {
                topmost = i;
                break;
            }
        }
        for (var i = 0; i < Children.Length; i++)
        {
            var rect = _rects[i];
            var bounds = rect with
            {
                X = origin.X + rect.X,
                Y = origin.Y + rect.Y
            };
            ui.RenderParentDataChild(Children[i], _positioned[i], bounds, root, blockPointer: topmost >= 0 && i != topmost);
        }
        ui.FinishLayoutBox(origin, size);
    }
    private protected override void ReleaseOwnedResources()
    {
        _rects = [];
        _positioned = [];
        _retained = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiStackWidget))]
[UiComponent("Stack", UiComponentExposure.Infrastructure)]
public partial class UiStack;
[UiWidgetStyle(typeof(UiPositionedWidget))]
[UiComponent("Positioned", UiComponentExposure.Infrastructure)]
public partial class UiPositioned;
public static partial class Dux
{
    public static UiStack Stack(UiStackOptions options, params UiWidget[] children) => new(children, options);
    public static UiPositioned Positioned(UiWidget child, UiPositionedOptions options = default) => new(child, options);
}
public readonly partial struct UiControlContext
{
    internal UiWidget? ResolveLayoutRoot(UiWidget widget) => widget.ResolveLayoutRoot(_ui);
    internal void RequireStackParent(UiWidget widget) => _ui.RequireStackParent(widget);
    internal UiVector2 MeasureParentDataChild(UiWidget child, UiWidget? target, UiBoxConstraints constraints)
        => _ui.MeasureParentDataChild(child, target, constraints);
}
public sealed partial class UiImmediateContext
{
    private int _layoutPointerBlockedTier = -1;
    private bool _layoutPointerBlocked => _layoutPointerBlockedTier == _popupTierDepth || _widgetPointerBlockedTier == _popupTierDepth;
    internal bool IsLayoutPointerInside(UiRect bounds, UiRect root)
    {
        var clipped = IntersectRect(CurrentClipRect, IntersectRect(root, bounds));
        return clipped.Width > 0 && clipped.Height > 0 && IsHovering(clipped);
    }
    internal void RequireStackParent(UiWidget widget)
    {
        if (!ReferenceEquals(widget, _layoutParentDataTarget))
            throw new InvalidOperationException("Positioned requires a Stack parent through composition-only widgets.");
    }
}
