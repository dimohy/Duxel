namespace Duxel.Core;

public enum UiTooltipRelationship
{
    Description,
    Label,
    Inaccessible
}

public readonly record struct UiTooltipOptions
{
    public UiTooltipRelationship Relationship { get; init; }
    public double? ShowDelaySeconds { get; init; }
    public double? HideDelaySeconds { get; init; }
    /// <summary>Apply this tooltip's show delay even when another tooltip is visible.</summary>
    public bool UseShowDelayOnHandover { get; init; }
    public UiPopupPositioning Positioning { get; init; }
    public float? ScrollStep { get; init; }
    public bool ShowScrollbars { get; init; }
    internal double ShowDelay => ShowDelaySeconds ?? 0.25;
    internal double HideDelay => HideDelaySeconds ?? 0.25;
    internal double ShowDelayAfterHandover(bool hasVisibleTooltip) =>
        hasVisibleTooltip && !UseShowDelayOnHandover ? 0 : ShowDelay;

    internal void Validate()
    {
        Positioning.Validate();
        if (!double.IsFinite(ShowDelay) || ShowDelay < 0 || !double.IsFinite(HideDelay) || HideDelay < 0
            || !float.IsFinite(ScrollStep ?? 48) || (ScrollStep ?? 48) <= 0
            || (uint)Relationship > (uint)UiTooltipRelationship.Inaccessible)
        {
            throw new ArgumentOutOfRangeException(nameof(UiTooltipOptions));
        }
    }
}

public readonly record struct UiTooltipInteraction(bool TargetHovered, bool TargetFocused, bool SurfaceHovered)
{
    internal bool ProgrammaticFocusWithoutKeyboard { get; init; }
}

public enum UiTooltipVisibilityReason
{
    Interaction,
    Escape,
    Handover,
    HostHidden
}

public readonly record struct UiTooltipVisibilityChange(bool Visible, UiTooltipVisibilityReason Reason);

public interface IUiTooltipBehavior
{
    static abstract bool Requested(UiTooltipInteraction interaction);
}

public readonly struct UiHoverFocusTooltipBehavior : IUiTooltipBehavior
{
    public static bool Requested(UiTooltipInteraction interaction) =>
        interaction.TargetHovered || interaction.SurfaceHovered
        || interaction.TargetFocused && !interaction.ProgrammaticFocusWithoutKeyboard;
}

/// <summary>Statically selected popup geometry and painting, independent of target measurement.</summary>
public interface IUiTooltipPresentation
{
    static abstract UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
        UiTooltipOptions options);
    static abstract void Paint(UiControlContext context, string text, UiPopupLayout layout);
}

internal interface IUiTooltipTarget
{
    string Text { get; }
    UiTooltipOptions Options { get; }
    bool? Visible { get; }
    Action<UiTooltipVisibilityChange>? VisibleChangeRequested { get; }
    bool IsAlive { get; }
    bool IsOpen { get; set; }
    bool PointerDismissed { get; set; }
    UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport, UiTooltipOptions options);
    void Paint(UiControlContext context, string text, UiPopupLayout layout);
}

/// <summary>Owns a real target and defers tooltip painting until after target rendering.</summary>
public class UiTooltip<TBehavior, TPresentation> : UiWidget, IUiTooltipTarget
    where TBehavior : IUiTooltipBehavior
    where TPresentation : IUiTooltipPresentation
{
    private UiWidget? _target;
    private string _text;
    private UiTooltipOptions _options;
    private bool? _visible;
    private bool _visibilityModeFixed;
    private Action<UiTooltipVisibilityChange>? _visibleChangeRequested;
    private WeakReference<UiImmediateContext>? _context;
    private readonly TargetView _view;
    private UiRetainedLayout<UiVector2>? _targetMeasurement;
    internal override void InvalidateRetainedMeasurement()
    {
        _targetMeasurement?.Invalidate();
        _targetMeasurement?.Clear();
    }

    public UiTooltip(UiWidget target, string text, UiTooltipOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        options.Validate();
        OwnChild("target", target);
        _target = target;
        _text = text;
        _options = options;
        _view = new(this);
    }

    public UiWidget Target => _target ?? throw new ObjectDisposedException(nameof(UiTooltip<TBehavior, TPresentation>));
    public bool IsOpen { get; private set; }
    bool IUiTooltipTarget.IsOpen { get => IsOpen; set => IsOpen = value; }
    bool IUiTooltipTarget.PointerDismissed { get; set; }
    bool IUiTooltipTarget.IsAlive => _target is not null;

    /// <summary>Null selects automatic visibility. A Boolean selects caller-owned visibility.</summary>
    public bool? Visible
    {
        get => _visible;
        set
        {
            ThrowIfDisposed();
            if (_visible == value)
            {
                return;
            }
            if (_visibilityModeFixed && _visible.HasValue != value.HasValue)
            {
                throw new InvalidOperationException("Tooltip visibility mode is fixed after its first render.");
            }
            ThrowIfBuilding();
            _visible = value;
            RequestRebuild();
        }
    }

    /// <summary>Receives visibility requests from interaction, Escape, and tooltip handover.</summary>
    public Action<UiTooltipVisibilityChange>? VisibleChangeRequested
    {
        get => _visibleChangeRequested;
        set
        {
            ThrowIfDisposed();
            ThrowIfBuilding();
            _visibleChangeRequested = value;
            RequestRebuild();
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (_text == value)
            {
                return;
            }
            ThrowIfBuilding();
            _text = value;
            RequestRebuild();
        }
    }

    public UiTooltipOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
            {
                return;
            }
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }

    protected override IUiView Build(UiBuildContext context)
    {
        context.Child("target", () => Target);
        return _view;
    }

    protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(Target);
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        MeasureOwnedChild(this, Target, context, context.LayoutConstraints, ref _targetMeasurement, false);

    private sealed class TargetView(UiTooltip<TBehavior, TPresentation> owner) : IUiView
    {
        public void Render(UiImmediateContext ui)
        {
            if (owner._context is null)
            {
                owner._context = new(ui);
            }
            else if (!owner._context.TryGetTarget(out var previous) || !ReferenceEquals(previous, ui))
            {
                previous?.ReleaseTooltip(owner);
                owner._context.SetTarget(ui);
            }
            owner._visibilityModeFixed = true;
            ui.RenderTooltipTarget<TBehavior>(owner, owner.Target, owner, ref owner._targetMeasurement);
        }
    }

    UiPopupLayout IUiTooltipTarget.Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
        UiTooltipOptions options) => TPresentation.Arrange(context, text, target, viewport, options);

    void IUiTooltipTarget.Paint(UiControlContext context, string text, UiPopupLayout layout) =>
        TPresentation.Paint(context, text, layout);

    private protected override void ReleaseOwnedResources()
    {
        if (_context is not null && _context.TryGetTarget(out var ui))
        {
            ui.ReleaseTooltip(this);
        }
        _context = null;
        _target = null;
        _targetMeasurement = null;
        _visibleChangeRequested = null;
        _text = string.Empty;
        IsOpen = false;
        base.ReleaseOwnedResources();
    }
}
