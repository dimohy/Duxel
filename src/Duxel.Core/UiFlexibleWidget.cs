namespace Duxel.Core;

public interface IUiFlexFit { static abstract bool Tight { get; } static virtual bool CanRetainMeasurement => false; }
public readonly struct UiTightFlexFit : IUiFlexFit { public static bool Tight => true; public static bool CanRetainMeasurement => true; }
public readonly struct UiLooseFlexFit : IUiFlexFit { public static bool Tight => false; public static bool CanRetainMeasurement => true; }

internal readonly record struct UiFlexParentData(UiWidget? Target, int Flex, bool Tight);
internal interface IUiFlexParentData { UiFlexParentData FlexParentData { get; } }

/// <summary>Owns a child whose validated flex weight and statically selected fit are consumed by Row or Column.</summary>
public class UiFlexibleWidget<TFit> : UiControlWidget, IUiFlexParentData where TFit : IUiFlexFit
{
    private UiWidget? _child;
    private int _flex;
    public int Flex
    {
        get => _flex;
        set
        {
            ThrowIfDisposed(); ValidateFlex(value); if (_flex == value) return;
            ThrowIfBuilding(); _flex = value; RequestRebuild();
        }
    }
    protected virtual void ValidateFlex(int value)
    { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); }
    public UiWidget Child { get { ThrowIfDisposed(); return _child!; } }

    public UiFlexibleWidget(UiWidget child, int flex = 1, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (flex < 0) throw new ArgumentOutOfRangeException(nameof(flex));
        OwnChild("content", child); _child = child; _flex = flex;
    }

    UiFlexParentData IUiFlexParentData.FlexParentData => new(this, Flex, TFit.Tight);
    protected override bool CanRetainMeasurement => TFit.CanRetainMeasurement && Child.RetainsMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        context.RequireFlexParent(this);
        return context.MeasureConstrained(Child, context.LayoutConstraints);
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        ui.RequireFlexParent(this);
        var context = new UiControlContext(ui);
        var size = context.AllocatedLayoutSize;
        var origin = ui.AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        ui.RenderLayoutChild(Child, bounds, bounds); ui.FinishLayoutBox(origin, size);
    }
    private protected override void ReleaseOwnedResources() { _child = null; base.ReleaseOwnedResources(); }
}

public class UiFlexSpacer : UiFlexibleWidget<UiTightFlexFit>
{
    public UiFlexSpacer(int flex = 1, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(CreateSpace(flex), flex, identity) { }
    protected override void ValidateFlex(int value)
    { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); }
    private static UiWidget CreateSpace(int flex)
    {
        if (flex <= 0) throw new ArgumentOutOfRangeException(nameof(flex));
        return new Space();
    }
    private sealed class Space : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(default);
        protected override void RenderControl(UiImmediateContext ui) => ui.Dummy(new UiControlContext(ui).ConstrainToLayout(default));
    }
}

[UiWidgetStyle(typeof(UiFlexibleWidget<>), typeof(UiTightFlexFit))]
[UiComponent("Expanded", UiComponentExposure.Infrastructure)]
public partial class UiExpanded;
[UiWidgetStyle(typeof(UiFlexibleWidget<>), typeof(UiLooseFlexFit))]
[UiComponent("Flexible", UiComponentExposure.Infrastructure)]
public partial class UiFlexible;
[UiWidgetStyle(typeof(UiFlexSpacer))]
[UiComponent("Spacer", UiComponentExposure.Infrastructure)]
public partial class UiSpacer;

public static partial class Dux
{
    public static UiExpanded Expanded(UiWidget child, int flex = 1) => new(child, flex);
    public static UiFlexible Flexible(UiWidget child, int flex = 1) => new(child, flex);
    public static UiSpacer FlexSpacer(int flex = 1) => new(flex);
}

public abstract partial class UiWidget
{
    internal virtual UiWidget? ResolveLayoutRoot(UiImmediateContext ui)
    {
        ThrowIfDisposed();
        if (_measuring || _rendering) throw new InvalidOperationException("A widget cannot recursively resolve layout parent data.");
        _measuring = true;
        try { return ui.ResolveComposedLayoutRoot(this); }
        finally { _measuring = false; }
    }
    internal UiWidget? ResolveBuiltLayoutRoot(UiImmediateContext ui)
        =>PrepareBuiltContent() is UiWidget root ? root.ResolveLayoutRoot(ui) : null;
}

public readonly partial struct UiControlContext
{
    internal UiVector2 AllocatedLayoutSize
    {
        get
        {
            var constraints = LayoutConstraints.Validate();
            if (constraints.MinWidth != constraints.MaxWidth || constraints.MinHeight != constraints.MaxHeight)
                throw new InvalidOperationException("A parent-data widget requires a tight render allocation.");
            return new(constraints.MinWidth, constraints.MinHeight);
        }
    }
    /// <summary>Applies explicit measurement or parent layout allocation to a control's desired size.</summary>
    public UiVector2 ConstrainToLayout(UiVector2 desired) => _ui.ConstrainToLayout(desired);
    internal void RequireFlexParent(UiWidget widget) => _ui.RequireFlexParent(widget);
    internal UiFlexParentData? ResolveFlexParentData(UiWidget widget)
        => widget.ResolveLayoutRoot(_ui) is IUiFlexParentData data ? data.FlexParentData : null;
    internal UiVector2 MeasureFlexChild(UiWidget child, UiFlexParentData data, UiBoxConstraints constraints)
        => _ui.MeasureFlexChild(child, data, constraints);
}

public sealed partial class UiImmediateContext
{
    private UiWidget? _layoutParentDataTarget;
    internal void RequireFlexParent(UiWidget widget)
    {
        if (!ReferenceEquals(widget, _layoutParentDataTarget)) throw new InvalidOperationException("Expanded, Flexible and Spacer require a Row or Column parent through composition-only widgets.");
    }
    internal UiVector2 MeasureFlexChild(UiWidget child, UiFlexParentData data, UiBoxConstraints constraints)
        => MeasureParentDataChild(child, data.Target, constraints);
    internal UiVector2 MeasureParentDataChild(UiWidget child, UiWidget? target, UiBoxConstraints constraints)
    {
        var previous = _layoutParentDataTarget; _layoutParentDataTarget = target;
        try { return child.MeasureConstrained(this, constraints); }
        finally { _layoutParentDataTarget = previous; }
    }
    internal void RenderFlexChild(UiWidget child, UiFlexParentData data, UiRect bounds, UiRect root)
        => RenderParentDataChild(child, data.Target, bounds, root);
    internal void RenderParentDataChild(UiWidget child, UiWidget? target, UiRect bounds, UiRect root, bool blockPointer = false,
        bool clipToAllocation = true)
    {
        var previous = _layoutParentDataTarget; _layoutParentDataTarget = target;
        var previousBlocked = _layoutPointerBlockedTier;
        if (blockPointer) _layoutPointerBlockedTier = _popupTierDepth;
        try { RenderAllocatedChild(_builder, child, bounds, root, constrainLayout: true, clipToAllocation: clipToAllocation); }
        finally { _layoutParentDataTarget = previous; _layoutPointerBlockedTier = previousBlocked; }
    }
    internal UiVector2 ConstrainToLayout(UiVector2 desired)
    {
        if (IsMeasuring) return _measureConstraints?.Constrain(desired) ?? desired;
        return _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds is {} bounds
            ? new(bounds.Width, bounds.Height) : desired;
    }
    internal float ConstrainLayoutWidth(float desired)
    {
        if(IsMeasuring&&_measureConstraints is {} constraints)return Math.Clamp(desired,constraints.MinWidth,constraints.MaxWidth);
        return _layoutChildConstraints&&_inputSlotBoundsTier==_popupTierDepth&&_inputSlotBounds is {} bounds?bounds.Width:desired;
    }
}
