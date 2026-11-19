namespace Duxel.Core;

/// <summary>A compile-time-selected single-child layout; dynamic options remain ordinary state.</summary>
public interface IUiSingleChildLayout<TOptions> where TOptions:struct,IEquatable<TOptions>
{
    static virtual bool CanRetainMeasurement=>false;
    static abstract void Validate(TOptions options);
    static abstract UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,TOptions options,UiBoxConstraints constraints);
}

/// <summary>Shared ownership, invalidation, optional retention and painting for one-child layouts.</summary>
public class UiSingleChildBox<TLayout,TOptions,TPresentation>:UiControlWidget
    where TOptions:struct,IEquatable<TOptions>
    where TLayout:IUiSingleChildLayout<TOptions>
    where TPresentation:IUiContainerPresentation
{
    private TOptions _options;
    private UiWidget? _child;
    private UiRetainedLayout<(UiContainerLayoutResult Layout, TOptions Options)>? _retained;
    protected virtual bool CanRetainArrangement=>TLayout.CanRetainMeasurement;
    protected virtual TOptions LayoutOptions => _options;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true } cache && cache.CachedResult.Options.Equals(LayoutOptions);
    internal override void InvalidateRetainedMeasurement()=>_retained?.Invalidate();
    public UiSingleChildBox(UiWidget? child, TOptions options, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(child, options, identity, null, "auxiliary") { }

    private protected UiSingleChildBox(UiWidget? child, TOptions options, UiWidgetIdentity identity,
        UiWidget? auxiliaryChild, string auxiliaryKey) : base(identity)
    {
        TLayout.Validate(options);
        ArgumentException.ThrowIfNullOrEmpty(auxiliaryKey);
        if (auxiliaryKey == "content" || auxiliaryChild is not null && ReferenceEquals(child, auxiliaryChild))
            throw new ArgumentException("Primary and auxiliary children require distinct instances and keys.", nameof(auxiliaryChild));
        if (child is not null) ValidateOwnedChild("content", child);
        if (auxiliaryChild is not null) ValidateOwnedChild(auxiliaryKey, auxiliaryChild);
        _options = options;
        if (child is not null) OwnChild("content", child);
        if (auxiliaryChild is not null) OwnChild(auxiliaryKey, auxiliaryChild);
        _child = child;
    }
    public UiWidget? Child {get{ThrowIfDisposed();return _child;}}
    public TOptions Options
    {
        get=>_options;
        set{ThrowIfDisposed();TLayout.Validate(value);if(_options.Equals(value))return;ThrowIfBuilding();_options=value;RequestRebuild();}
    }
    protected virtual TOptions ResolveLayoutOptions(UiControlContext context) => LayoutOptions;
    protected UiContainerLayoutResult GetLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        var options = ResolveLayoutOptions(context);
        if (_retained is { } previous)
        {
            if (previous.IsValid && !previous.CachedResult.Options.Equals(options)) RequestRebuild();
            if (previous.TryGet(context, render, out var result, allowed)) return result.Layout;
        }
        _retained?.Clear();
        var retain = allowed && (_child is null || _child.RetainsMeasurement);
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var constraints = context.LayoutConstraints;
        var layout = TLayout.Measure(context, _child, options, constraints).Validate();
        if (layout.Size.X < constraints.MinWidth || layout.Size.X > constraints.MaxWidth
            || layout.Size.Y < constraints.MinHeight || layout.Size.Y > constraints.MaxHeight)
            throw new InvalidOperationException($"Container layout size {layout.Size} is outside its parent constraints {constraints}.");
        cache.Commit(context, attempt, (layout, options), layout.Size, retain);
        return layout;
    }
    protected override UiVector2 MeasureContent(UiControlContext context)=>GetLayout(context).Size;
    protected override void RenderControl(UiImmediateContext ui)=>ui.RenderContainer<TPresentation>(_child,GetLayout(new(ui),render:true));
    private protected override void ReleaseOwnedResources(){_child=null;_retained=null;base.ReleaseOwnedResources();}
}
