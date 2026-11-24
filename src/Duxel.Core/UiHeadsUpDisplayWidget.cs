namespace Duxel.Core;

public sealed record UiHeadsUpDisplayOptions(bool IsBlocking = true, bool IsPersistent = true,
    bool ReducedMotion = false, float KeyboardHeight = 0)
{
    internal void Validate()
    {
        if (!float.IsFinite(KeyboardHeight) || KeyboardHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(KeyboardHeight));
    }
}

[UiWidgetStyle(typeof(UiHeadsUpDisplayWidget))]
[UiComponent("HeadsUpDisplay", UiComponentExposure.Fluent)]
public partial class UiFluentHeadsUpDisplay;

internal sealed class UiHeadsUpDisplayInputState : UiOverlayInputState
{
    internal bool MotionEnabled, Persistent = true;
    internal double? AutoDismissAt;
    internal UiPaintGroup? SurfacePaint;
    internal UiRect NaturalBounds;
    internal UiOverlayMotionSample EntryFrom = new(1.3f, 0, 0), LastMotion = new(1, 1, 0);
    internal override double? InteractionDeadline => AutoDismissAt;
    internal override bool ContainsScreenPoint(UiVector2 point) => point.X >= Bounds.X && point.X < Bounds.X + Bounds.Width
        && point.Y >= Bounds.Y && point.Y < Bounds.Y + Bounds.Height;
    internal override void PrepareOpening() => Context?.PrepareHeadsUpDisplayOpening(this);
    internal override void PrepareClosing() => Context?.PrepareHeadsUpDisplayClosing(this);
    internal override void CancelPendingDismissal() => AutoDismissAt = null;
    internal override void ResetInteraction(bool releaseResources)
    {
        AutoDismissAt = null;
        if (!releaseResources) return;
        SurfacePaint?.Dispose();
        SurfacePaint = null;
    }
}

public interface IUiHeadsUpDisplayBehavior : IUiPointerActivationBehavior
{
    static virtual TimeSpan AutoDismissDelay => TimeSpan.FromSeconds(1);
}

public readonly struct UiDefaultHeadsUpDisplayBehavior : IUiHeadsUpDisplayBehavior
{
    public static UiButtonActivation PointerActivation => UiButtonActivation.Release;
}

/// <summary>Owns a centered HUD portal with independent input, temporary lifetime, and surface presentation.</summary>
public class UiHeadsUpDisplayWidget<TBehavior, TPresentation> : UiControlWidget
    where TBehavior : IUiHeadsUpDisplayBehavior
    where TPresentation : IUiHeadsUpDisplayPresentation
{
    private UiHeadsUpDisplayOptions _options;
    private readonly UiSurfaceVisibility _visibility;
    internal readonly UiHeadsUpDisplayInputState Input = new();
    internal double AutoDismissSeconds { get; }
    public UiHeadsUpDisplayContent Content { get; }
    public UiHeadsUpDisplayWidget(UiHeadsUpDisplayContentOptions? contentOptions = null,
        UiHeadsUpDisplayOptions? options = null, Action? tapped = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        _options = options ?? new();
        _options.Validate();
        AutoDismissSeconds = TBehavior.AutoDismissDelay.TotalSeconds;
        if (AutoDismissSeconds <= 0)
            throw new InvalidOperationException("HUD automatic dismissal requires a positive delay.");
        Content = new(contentOptions, tapped, UiWidgetIdentity.Instance, TBehavior.PointerActivation, TPresentation.PaintSurface);
        OwnChild("content", Content);
        _visibility = new(Input, RequestRebuild);
    }
    public UiHeadsUpDisplayOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public bool IsOpen => Input.Open;
    public UiBinding<bool>? Open
    {
        get => _visibility.Binding;
        set { ThrowIfDisposed(); ThrowIfBuilding(); _visibility.SetBinding(value); }
    }
    public Action<bool>? OpenChanged
    {
        get => _visibility.Changed;
        set { ThrowIfDisposed(); _visibility.Changed = value; }
    }
    public void Show()
    {
        ThrowIfDisposed();
        _visibility.Show(RenderingContext?.ControlActivationOrigin);
    }
    public bool Hide() { ThrowIfDisposed(); return _visibility.Close(); }
    public void ShowSuccess(string? label = null)
    {
        Content.Options = Content.Options with { Kind = UiHeadsUpDisplayKind.Success, Label = label };
        Options = _options with { IsPersistent = false };
        Show();
    }
    public void ShowFailure(string? label = null)
    {
        Content.Options = Content.Options with { Kind = UiHeadsUpDisplayKind.Failure, Label = label };
        Options = _options with { IsPersistent = false };
        Show();
    }
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => default;
    protected override void RenderControl(UiImmediateContext ui)
    {
        Input.Attach(ui);
        var (pending, origin) = _visibility.TakePending();
        ui.RenderHeadsUpDisplay(this, PrimaryControlId, Open?.Value, pending, origin);
    }
    private protected override void ReleaseOwnedResources()
    {
        _visibility.Dispose();
        base.ReleaseOwnedResources();
    }
}

/// <summary>HUD with the default Fluent backplate, release activation, and one-second temporary lifetime.</summary>
public class UiHeadsUpDisplayWidget : UiHeadsUpDisplayWidget<UiDefaultHeadsUpDisplayBehavior, UiFluentHeadsUpDisplayPresentation>
{
    public UiHeadsUpDisplayWidget(UiHeadsUpDisplayContentOptions? contentOptions = null,
        UiHeadsUpDisplayOptions? options = null, Action? tapped = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(contentOptions, options, tapped, identity) { }
}
