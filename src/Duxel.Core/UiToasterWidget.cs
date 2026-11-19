namespace Duxel.Core;

public enum UiToastPosition { TopStart, Top, TopEnd, BottomStart, Bottom, BottomEnd }
public sealed record UiToasterOptions(UiToastPosition Position = UiToastPosition.BottomEnd, float Width = 292,
    float Margin = 16, float Gap = 16, bool RightToLeft = false, bool ReducedMotion = false, bool IsWindowActive = true)
{
    internal void Validate()
    {
        if ((uint)Position > 5 || !float.IsFinite(Width) || Width <= 0 || !float.IsFinite(Margin) || Margin < 0
            || !float.IsFinite(Gap) || Gap < 0) throw new ArgumentOutOfRangeException(nameof(UiToasterOptions));
    }
}
internal sealed class UiToastOverlayState : UiOverlayInputState
{
    internal UiPaintGroup? SurfacePaint;
    internal override double? InteractionDeadline => null;
    internal override bool ContainsScreenPoint(UiVector2 point) => point.X >= Bounds.X && point.X < Bounds.X + Bounds.Width
        && point.Y >= Bounds.Y && point.Y < Bounds.Y + Bounds.Height;
    internal override void CancelPendingDismissal() { }
    internal override void ResetInteraction(bool releaseResources)
    {
        if (!releaseResources) return;
        SurfacePaint?.Dispose(); SurfacePaint = null;
    }
}
internal sealed class UiToastSurfaceEntry : IDisposable
{
    internal UiToastSnapshot Snapshot;
    internal readonly UiToastOverlayState Input = new();
    internal readonly UiToastWidget Content;
    internal UiFloatTransition Opacity = new(0), Y;
    internal float LastOpacity, ExitFrom;
    internal bool Exiting;
    internal UiRect PaintBounds;
    internal UiToastAnnouncementState Announcement;
    internal UiToastSurfaceEntry(UiToastSnapshot snapshot, UiToastController controller)
    {
        Snapshot = snapshot;
        Content = new(snapshot.Options, () => controller.Dismiss(snapshot.Id)) { IsPortalContent = true };
        if (snapshot.Status == UiToastStatus.Visible) Opacity.Reset(1);
    }
    public void Dispose() { Input.Dispose(); Content.Dispose(); }
}

/// <summary>A nonmodal viewport portal. The caller owns the controller; the widget owns only surface and input resources.</summary>
public class UiToasterWidget : UiControlWidget
{
    internal readonly Dictionary<string, UiToastSurfaceEntry> Entries = new(StringComparer.Ordinal);
    internal readonly WeakReference<UiImmediateContext> ContextOwner = new(null!);
    private UiToasterOptions _options;
    private readonly IDisposable _hostLease;
    public UiToastController Controller { get; }
    public UiToasterOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed(); ThrowIfBuilding(); ArgumentNullException.ThrowIfNull(value); value.Validate();
            if (_options == value) return;
            _options = value; RequestRebuild();
        }
    }
    public UiToasterWidget(UiToastController controller, UiToasterOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _options = options ?? new(); _options.Validate();
        _hostLease = Controller.AcquireHost();
        Controller.Changed += OnChanged;
    }
    private void OnChanged() => RequestRebuild();
    protected override UiVector2 MeasureContent(UiControlContext context) => default;
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderToaster(this, PrimaryControlId);
    private protected override void ReleaseOwnedResources()
    {
        Controller.Changed -= OnChanged;
        try
        {
            _hostLease.Dispose();
        }
        finally
        {
            foreach (var entry in Entries.Values) entry.Dispose();
            Entries.Clear(); ContextOwner.SetTarget(null!);
            base.ReleaseOwnedResources();
        }
    }
}
