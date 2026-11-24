namespace Duxel.Core;

public enum UiPopoverSize
{
    Medium, Small, Large
}
public enum UiPopoverTriggerMode
{
    Click, Hover, Context
}
public enum UiPopoverAppearance
{
    Neutral, Inverted, Brand
}
public readonly record struct UiPopoverOptions(float Width = 280, UiPopoverSize Size = UiPopoverSize.Medium,
    bool TrapFocus = false, bool DisableAutoFocus = false, bool CloseOnScroll = false,
    UiPopoverAppearance Appearance = UiPopoverAppearance.Neutral, float? MaximumHeight = null,
    UiPopupPositioning Positioning = default, bool WithArrow = false,
    UiPopoverTriggerMode TriggerMode = UiPopoverTriggerMode.Click, double MouseLeaveDelaySeconds = .5, bool MotionEnabled = true)
{
    /// <summary>Dismisses after focus leaves the trigger and owned content; preserves the new focus target.</summary>
    public bool CloseOnFocusLeave
    {
        get; init;
    }
    public UiAccessibilityRole SurfaceRole { get; init; } = UiAccessibilityRole.Group;
    internal float? ContentPadding { get; init; }

    public UiPopoverOptions() : this(280, UiPopoverSize.Medium, false, false, false, UiPopoverAppearance.Neutral, null, default, false, UiPopoverTriggerMode.Click, .5, true) { }
    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0 || (uint)Size > 2 || (uint)Appearance > 2
            || (uint)TriggerMode > 2 || !double.IsFinite(MouseLeaveDelaySeconds) || MouseLeaveDelaySeconds < 0
            || SurfaceRole is not (UiAccessibilityRole.Group or UiAccessibilityRole.Note)
            || MaximumHeight is { } h && (!float.IsFinite(h) || h <= 0))
            throw new ArgumentOutOfRangeException(nameof(UiPopoverOptions));
        Positioning.Validate();
    }
}
public interface IUiPopoverPresentation
{
    static abstract UiControlTypographyScope BeginTriggerText(UiControlContext context);
    static abstract UiControlTypographyScope BeginBodyText(UiControlContext context);
    static abstract UiVector2 MeasureTrigger(UiControlContext context, string label);
    static abstract void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction);
    static abstract float Padding(UiPopoverOptions options);
    static abstract UiColor Foreground(UiControlContext context, UiPopoverOptions options);
    /// <summary>Pure screen-space paint extent for entry compositing. Include the visible body and all decoration; defaults to the current viewport.</summary>
    static virtual UiRect PaintBounds(UiControlContext context, UiPopupLayout layout, UiPopoverOptions options) => context.ClipRect;
    static abstract void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options);
}
internal sealed class UiPopoverInputState : UiOverlayInputState
{
    internal bool HoverInside, ContextPress;
    internal UiPopoverTriggerMode TriggerMode;
    internal double? HoverCloseAt;
    internal UiVector2? ContextPoint;
    internal UiPopupArrow? Arrow;
    internal override double? InteractionDeadline => HoverCloseAt;
    internal override bool ContainsScreenPoint(UiVector2 point)
    {
        if (point.X >= Bounds.X && point.X < Bounds.X + Bounds.Width
            && point.Y >= Bounds.Y && point.Y < Bounds.Y + Bounds.Height)
            return true;
        if (Arrow is not { } arrow)
            return false;
        var center = new UiVector2((arrow.BaseStart.X + arrow.BaseEnd.X) / 2 - Bounds.X,
            (arrow.BaseStart.Y + arrow.BaseEnd.Y) / 2 - Bounds.Y);
        var dx = arrow.Tip.X - Bounds.X - center.X;
        var dy = arrow.Tip.Y - Bounds.Y - center.Y;
        var key = new UiImmediateContext.FluentPopupSurfaceKey(Bounds.Width, Bounds.Height, 0, 0, 1, center,
            dy > 0 ? 0 : dy < 0 ? 1 : dx < 0 ? 2 : 3, false, false, MathF.Abs(dx) + MathF.Abs(dy));
        return UiImmediateContext.FluentPopupArrowContains(point.X - Bounds.X, point.Y - Bounds.Y, key);
    }
    internal override void CancelPendingDismissal() => HoverCloseAt = null;
    internal override void ResetInteraction(bool releaseResources)
    {
        HoverCloseAt = null;
        ContextPress = false;
        ContextPoint = null;
        if (releaseResources)
            HoverInside = false;
    }
}

/// <summary>Owns arbitrary retained content and its scrolling viewport; visibility and focus belong to one context.</summary>
public class UiPopover<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiPointerActivationBehavior
    where TPresentation : IUiPopoverPresentation
{
    private UiPopoverOptions _options;
    private UiBinding<bool>? _open;
    private bool? _pending;
    private Action<bool>? _changed;
    private readonly UiPopoverInputState _input = new();
    private readonly UiScrollView _viewport;
    private string _label;
    public string Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed();
            ValidateLabel(value);
            if (_label == value)
                return;
            ThrowIfBuilding();
            _label = value;
            RequestRebuild();
        }
    }
    public UiWidget Content
    {
        get;
    }
    public bool IsOpen => _input.Open;
    public UiPopoverOptions Options
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
    public UiBinding<bool>? Open
    {
        get => _open;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_open, value))
                return;
            ThrowIfBuilding();
            _open = value;
            RequestRebuild();
        }
    }
    public Action<bool>? OpenChanged
    {
        get => _changed; set
        {
            ThrowIfDisposed();
            _changed = value;
        }
    }
    public UiPopover(string label, UiWidget content, UiPopoverOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ValidateLabel(label);
        ArgumentNullException.ThrowIfNull(content);
        _options = options ?? new();
        _options.Validate();
        _label = label;
        Content = content;
        _viewport = Dux.ScrollView(content);
        OwnChild("surface-content", _viewport);
        _input.CanChangeOpen = RequestChange;
        _input.Changed = value => _changed?.Invoke(value);
    }
    private bool RequestChange(bool value)
    {
        var binding = _open;
        if (binding is null || binding.Value == value)
            return true;
        binding.Value = value;
        return binding.Value == value;
    }
    public void Show()
    {
        ThrowIfDisposed();
        _pending = true;
        RequestRebuild();
    }
    public bool Close()
    {
        ThrowIfDisposed();
        _pending = null;
        if (_input.Open && _input.Context is { } context)
            return context.RequestOverlayChange(_input, false, true);
        if (!RequestChange(false))
            return false;
        _pending = false;
        RequestRebuild();
        return true;
    }
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasurePopover<TPresentation>(context);
    protected UiVector2 MeasurePopover<TStyle>(UiControlContext context) where TStyle : IUiPopoverPresentation
    {
        using var typography = TStyle.BeginTriggerText(context);
        return context.LayoutConstraints.Constrain(TStyle.MeasureTrigger(context, Label));
    }
    protected override void RenderControl(UiImmediateContext ui) => RenderPopover<TPresentation>(ui);
    protected void RenderPopover<TStyle>(UiImmediateContext ui) where TStyle : IUiPopoverPresentation
    {
        _input.Attach(ui);
        var pending = _pending;
        _pending = null;
        ui.RenderPopover<TStyle>(PrimaryInputId(Label), Label, _viewport, _input, _options, _open?.Value, pending,
            TBehavior.PointerActivation);
    }
    private static void ValidateLabel(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (UiAccessibilityTree.ContainsControl(label, false))
            throw new ArgumentException("Popover label must be single-line.", nameof(label));
    }
    private protected override void ReleaseOwnedResources()
    {
        _changed = null;
        _open = null;
        _pending = null;
        _input.Dispose();
        base.ReleaseOwnedResources();
    }
}

/// <summary>Popover whose click trigger activates on pointer release.</summary>
public class UiPopover<TPresentation> : UiPopover<UiReleaseButtonBehavior, TPresentation>
    where TPresentation : IUiPopoverPresentation
{
    public UiPopover(string label, UiWidget content, UiPopoverOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(label, content, options, identity) { }
}

[UiWidgetStyle(typeof(UiPopover<>), typeof(UiFluentPopoverPresentation))]
[UiComponent("Popover", UiComponentExposure.Fluent)]
public partial class UiFluentPopover;
