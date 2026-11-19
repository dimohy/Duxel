namespace Duxel.Core;

/// <summary>Hosts a borrowed sequential view inside a finite declarative layout allocation.</summary>
public sealed class UiViewHost(IUiView content) : UiControlWidget
{
    private readonly IUiView _content = content ?? throw new ArgumentNullException(nameof(content));
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var constraints = context.LayoutConstraints.Validate();
        if (!float.IsFinite(constraints.MaxWidth) || !float.IsFinite(constraints.MaxHeight))
            throw new InvalidOperationException("A sequential view host requires finite layout bounds.");
        return new(constraints.MaxWidth, constraints.MaxHeight);
    }
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderViewHost(_content, AccessibleName);
}

public sealed partial class UiImmediateContext
{
    private UiRect? _flowRegionBounds;
    private int _flowRegionChildDepth;
    private int _flowRegionTier;
    private string? _flowRegionWindowId;
    private bool HasCurrentFlowRegion => _flowRegionBounds is not null && _flowRegionTier == _popupTierDepth
        && _flowRegionWindowId == _currentWindowId;
    internal void RenderViewHost(IUiView content, string? accessibleName)
    {
        var context = new UiControlContext(this);
        var size = context.LayoutConstraints;
        if (!float.IsFinite(size.MaxWidth) || !float.IsFinite(size.MaxHeight))
            throw new InvalidOperationException("A sequential view host requires finite layout bounds.");
        var extent = new UiVector2(size.MaxWidth, size.MaxHeight);
        var origin = AllocateLayoutBox(extent);
        var bounds = new UiRect(origin.X, origin.Y, extent.X, extent.Y);
        var parent = _accessibilityParent;
        var id = _accessibility is null || string.IsNullOrEmpty(accessibleName) ? null : ResolveId("##view-host");
        var widget = SwapAccessibilityWidget(null);
        if (id is not null)
            _accessibilityParent = id;
        var saved = (_inputSlotBounds, _layoutChildConstraints, _flowRegionBounds, _flowRegionChildDepth, _flowRegionTier, _flowRegionWindowId);
        var depth = _layouts.Count;
        _inputSlotBounds = null;
        _layoutChildConstraints = false;
        _flowRegionBounds = bounds;
        _flowRegionChildDepth = _childStack.Count;
        _flowRegionTier = _popupTierDepth;
        _flowRegionWindowId = _currentWindowId;
        _layouts.Push(new(origin, false, 0, origin.X));
        using var clip = BeginControlClip(bounds);
        var completed = false;
        try { content.Render(this); completed = true; }
        finally
        {
            var balanced = _layouts.Count == depth + 1;
            while (_layouts.Count > depth) _layouts.Pop();
            (_inputSlotBounds, _layoutChildConstraints, _flowRegionBounds, _flowRegionChildDepth, _flowRegionTier, _flowRegionWindowId) = saved;
            _accessibilityParent = parent;
            SwapAccessibilityWidget(widget);
            FinishLayoutBox(origin, extent);
            if (completed && !balanced) throw new InvalidOperationException("A hosted sequential view left an unbalanced layout scope.");
        }
        if (id is not null)
            ExposeAccessibility(id, parent, UiAccessibilityRole.Pane, accessibleName!, bounds,
                readOnly : true, focusable : false, valuePattern : false, literalLabel : true);
    }
}
