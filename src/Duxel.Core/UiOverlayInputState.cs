namespace Duxel.Core;

/// <summary>Owns one retained overlay's focus, child portals and surface lifetime.</summary>
internal abstract class UiOverlayInputState : IDisposable
{
    internal bool Open, WasExclusive, HadFocusWithin, HadFocusInRegion;
    internal double? EnterStartedAt, MotionNextAt;
    internal UiPaintGroup? MotionPaint;
    internal string? Trigger, Surface;
    internal UiRect Bounds;
    internal UiRect? AdditionalBlockingBounds;
    internal int Tier, TriggerTier, SeenFrame = int.MinValue, RegisteredFrame = int.MinValue, TriggerSeenFrame = int.MinValue;
    internal UiOverlayInputState? Parent;
    internal readonly List<string> Members = [];
    internal Func<bool, bool>? CanChangeOpen;
    internal Action<bool>? Changed;
    internal UiPopupSurface Paint = new();
    private WeakReference<UiImmediateContext>? _context;
    internal UiImmediateContext? Context => _context is not null && _context.TryGetTarget(out var context) ? context : null;
    internal abstract double? InteractionDeadline
    {
        get;
    }
    internal abstract bool ContainsScreenPoint(UiVector2 point);
    internal abstract void CancelPendingDismissal();
    internal abstract void ResetInteraction(bool releaseResources);
    internal virtual void PrepareOpening()
    {
    }
    internal virtual void PrepareClosing()
    {
    }

    internal void Attach(UiImmediateContext context)
    {
        if (ReferenceEquals(Context, context))
            return;
        Context?.CancelOverlay(this, false, true, false);
        ResetSurface();
        Members.Clear();
        ResetInteraction(true);
        Trigger = Surface = null;
        SeenFrame = RegisteredFrame = TriggerSeenFrame = int.MinValue;
        _context ??= new(context);
        _context.SetTarget(context);
    }

    internal void ResetSurface()
    {
        ResetMotion();
        Paint.Dispose();
        Paint = new();
    }

    internal void ResetMotion()
    {
        EnterStartedAt = MotionNextAt = null;
        MotionPaint?.Dispose();
        MotionPaint = null;
    }

    internal void Detach()
    {
        _context = null;
        Trigger = Surface = null;
        Members.Clear();
        Open = false;
        ResetInteraction(true);
        Parent = null;
        RegisteredFrame = SeenFrame = TriggerSeenFrame = int.MinValue;
        ResetSurface();
    }

    public void Dispose()
    {
        Changed = null;
        CanChangeOpen = null;
        Context?.CancelOverlay(this, false, true, false);
        ResetMotion();
        Paint.Dispose();
        Members.Clear();
        _context = null;
        Parent = null;
        Trigger = Surface = null;
    }
}
