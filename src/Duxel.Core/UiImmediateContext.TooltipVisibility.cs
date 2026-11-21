namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private sealed class ControlledTooltip(IUiTooltipTarget owner)
    {
        internal readonly IUiTooltipTarget Owner = owner;
        internal Action<UiTooltipVisibilityChange>? Callback;
        internal string Text = string.Empty;
        internal UiTooltipOptions Options;
        internal TooltipAccessibilityScope? Semantics;
        internal UiControlEnvironment Environment;
        internal UiRect TargetBounds, SurfaceBounds;
        internal UiVector2 ContentSize, ScrollOffset;
        internal TooltipSurfaceTexture? Texture;
        internal int TexturePaintFrame = -1;
        internal int Tier;
        internal long OpenOrder;
        internal bool Seen, Visible, Requested, Focused, Escape;
        internal double? Deadline;
    }

    private readonly record struct TooltipNotification(IUiTooltipTarget Owner,
        Action<UiTooltipVisibilityChange> Callback, UiTooltipVisibilityChange Change);

    private List<ControlledTooltip>? _controlledTooltips;
    private List<TooltipNotification>? _tooltipNotifications;
    private IUiTooltipTarget? _tooltipVisibleOwner;
    private long _tooltipOpenOrder, _tooltipAutoOpenOrder;
    private UiControlEnvironment _tooltipOwnerEnvironment;
    private Action<UiTooltipVisibilityChange>? _tooltipAutoCallback, _tooltipCandidateCallback;
    private UiHostVisibility _tooltipHostVisibility;

    private void ObserveTooltipHostVisibility(UiHostVisibility visibility)
    {
        var becameHidden = visibility.IsHidden && !_tooltipHostVisibility.IsHidden
            || visibility.HideSequence != _tooltipHostVisibility.HideSequence;
        _tooltipHostVisibility = visibility;
        if (!becameHidden)
        {
            return;
        }
        _tooltipSuppressed = _tooltipOwner ?? _tooltipSuppressed;
        CloseAutomaticTooltip(UiTooltipVisibilityReason.HostHidden);
        if (_controlledTooltips is null)
        {
            return;
        }
        foreach (var tooltip in _controlledTooltips)
        {
            if (tooltip.Owner.IsOpen && tooltip.Owner.Visible == true)
            {
                QueueTooltipVisibility(tooltip.Owner, tooltip.Owner.VisibleChangeRequested, false,
                    UiTooltipVisibilityReason.HostHidden);
            }
            CloseControlledTooltip(tooltip);
        }
        _controlledTooltips.Clear();
    }

    private void BeginControlledTooltips()
    {
        if (_controlledTooltips is null)
        {
            return;
        }
        foreach (var tooltip in _controlledTooltips)
        {
            tooltip.Seen = false;
            tooltip.Escape = false;
        }
    }

    private ControlledTooltip? FirstControlledTooltip()
    {
        ControlledTooltip? first = null;
        if (_controlledTooltips is null)
        {
            return null;
        }
        foreach (var tooltip in _controlledTooltips)
        {
            if (tooltip.Owner.IsOpen && tooltip.Owner.Visible == true
                && (first is null || tooltip.OpenOrder < first.OpenOrder))
            {
                first = tooltip;
            }
        }
        return first;
    }

    private void ResolveTooltipSurfaceInput()
    {
        if (_tooltipEscapeConsumed)
        {
            ReleaseTooltipScrollbar();
        }
        else if (ContinueTooltipScrollbarCapture())
        {
            return;
        }
        var hitTier = int.MinValue;
        if (_tooltipOwner is { IsOpen : true } owner && HitsTooltipSurface(_tooltipSurfaceBounds, _tooltipOwnerTier))
        {
            _tooltipSurfaceUnderPointer = owner;
            hitTier = _tooltipOwnerTier;
        }
        ControlledTooltip? controlledHit = null;
        if (_controlledTooltips is { } controlled)
        {
            // Controlled surfaces paint after the automatic surface, in list order within each tier.
            foreach (var tooltip in controlled)
            {
                if (tooltip.Owner.IsOpen && tooltip.Owner.Visible == true && tooltip.Tier >= hitTier
                    && HitsTooltipSurface(tooltip.SurfaceBounds, tooltip.Tier))
                {
                    controlledHit = tooltip;
                    _tooltipSurfaceUnderPointer = tooltip.Owner;
                    hitTier = tooltip.Tier;
                }
            }
        }
        if (_tooltipEscapeConsumed || !_leftMousePressed && _mouseWheel == 0 && _mouseWheelHorizontal == 0)
        {
            return;
        }
        if (controlledHit is { } hit)
        {
            BeginTooltipScrollbarCapture(hit.Owner, hit.SurfaceBounds, hit.ContentSize, hit.Options, ref hit.ScrollOffset);
            hit.ScrollOffset = ScrollTooltipSurface(hit.ScrollOffset, hit.ContentSize, hit.SurfaceBounds, hit.Options);
        }
        else if (_tooltipSurfaceUnderPointer is not null)
        {
            BeginTooltipScrollbarCapture(_tooltipSurfaceUnderPointer, _tooltipSurfaceBounds, _tooltipContentSize,
                _tooltipOwnerOptions, ref _tooltipScrollOffset);
            _tooltipScrollOffset = ScrollTooltipSurface(_tooltipScrollOffset, _tooltipContentSize,
                _tooltipSurfaceBounds, _tooltipOwnerOptions);
        }
    }

    private bool HitsTooltipSurface(UiRect bounds, int tier)
    {
        var visible = IntersectRect(bounds, _clipRect);
        return visible.Width > 0 && visible.Height > 0 && IsHovering(visible) && !IsMouseOverHigherTierPopup(tier + 1);
    }

    private UiVector2 ScrollTooltipSurface(UiVector2 offset, UiVector2 content, UiRect bounds, UiTooltipOptions options)
    {
        var step = options.ScrollStep ?? 48;
        var maximumX = MathF.Max(0, content.X - bounds.Width);
        var rightToLeft = options.Positioning.RightToLeft;
        var physicalX = TooltipPhysicalScrollX(offset.X, maximumX, rightToLeft);
        physicalX = ApplyScrollWheel(physicalX, maximumX, step, ref _mouseWheelHorizontal);
        return new(
            ReverseHorizontalScroll(physicalX, maximumX, rightToLeft),
            ApplyScrollWheel(offset.Y, MathF.Max(0, content.Y - bounds.Height), step, ref _mouseWheel));
    }

    private ControlledTooltip? FindControlledTooltip(IUiTooltipTarget owner)
    {
        if (_controlledTooltips is not null)
        {
            foreach (var tooltip in _controlledTooltips)
            {
                if (ReferenceEquals(tooltip.Owner, owner))
                {
                    return tooltip;
                }
            }
        }
        return null;
    }

    private void ObserveControlledTooltip<TBehavior>(IUiTooltipTarget owner, bool visible,
        Action<UiTooltipVisibilityChange>? callback, string text, UiTooltipOptions options,
        TooltipAccessibilityScope? semantics, UiRect bounds, bool available, bool hovered, bool focused)
        where TBehavior : IUiTooltipBehavior
    {
        var tooltip = FindControlledTooltip(owner);
        var surfaceHovered = available && visible && ReferenceEquals(_tooltipSurfaceUnderPointer, owner);
        var requested = available && TBehavior.Requested(new(hovered, focused, surfaceHovered));
        if (!available || tooltip is null && !visible && !requested)
        {
            return;
        }
        if (tooltip is null)
        {
            tooltip = new(owner);
            (_controlledTooltips ??= []).Add(tooltip);
        }
        tooltip.Seen = true;
        tooltip.Callback = callback;
        tooltip.Text = text;
        tooltip.Options = options;
        tooltip.Semantics = semantics;
        tooltip.TargetBounds = bounds;
        tooltip.Environment = ControlEnvironment;
        tooltip.Tier = _popupTierDepth;
        if (tooltip.Requested != requested)
        {
            tooltip.Requested = requested;
            var delay = requested ? options.ShowDelayAfterHandover(_tooltipVisibleOwner is not null)
                : tooltip.Focused && !focused ? 0 : options.HideDelay;
            tooltip.Deadline = requested == visible ? null : _state.TimeSeconds + delay;
        }
        tooltip.Focused = focused;
        tooltip.Visible = visible;
    }

    private void ResolveControlledTooltips(double now)
    {
        if (_controlledTooltips is null)
        {
            return;
        }
        for (var i = _controlledTooltips.Count - 1; i >= 0; i--)
        {
            var tooltip = _controlledTooltips[i];
            foreach (var popup in _state.CurrentPopupBlockingRects)
            {
                if (popup.Tier > tooltip.Tier)
                {
                    tooltip.Seen = false;
                    break;
                }
            }
            if (!tooltip.Seen || !tooltip.Visible && !tooltip.Requested && tooltip.Deadline is null)
            {
                CloseControlledTooltip(tooltip);
                _controlledTooltips.RemoveAt(i);
            }
            else if (!tooltip.Visible && tooltip.Owner.IsOpen)
            {
                CloseControlledTooltip(tooltip);
            }
        }
        foreach (var tooltip in _controlledTooltips)
        {
            if (tooltip.Visible && !tooltip.Owner.IsOpen)
            {
                tooltip.Owner.IsOpen = true;
                tooltip.OpenOrder = RegisterVisibleTooltip(tooltip.Owner);
            }
            if (tooltip.Escape && tooltip.Visible)
            {
                RequestControlledTooltipHide(tooltip, UiTooltipVisibilityReason.Escape);
            }
            if (tooltip.Deadline is not { } deadline)
            {
                continue;
            }
            if (tooltip.Visible == tooltip.Requested || tooltip.Callback is null)
            {
                tooltip.Deadline = null;
            }
            else if (now >= deadline)
            {
                tooltip.Deadline = null;
                QueueTooltipVisibility(tooltip.Owner, tooltip.Callback, tooltip.Requested, UiTooltipVisibilityReason.Interaction);
            }
            else
            {
                RequestFrameAt(deadline);
            }
        }
    }

    private void RequestControlledTooltipHide(ControlledTooltip tooltip, UiTooltipVisibilityReason reason)
    {
        tooltip.Deadline = null;
        QueueTooltipVisibility(tooltip.Owner, tooltip.Callback, false, reason);
    }

    private long RegisterVisibleTooltip(IUiTooltipTarget owner)
    {
        if (_tooltipVisibleOwner is { } previous && !ReferenceEquals(previous, owner))
        {
            if (ReferenceEquals(previous, _tooltipOwner))
            {
                CloseAutomaticTooltip(UiTooltipVisibilityReason.Handover);
            }
            else if (FindControlledTooltip(previous) is { Seen : true, Visible : true } controlled)
            {
                RequestControlledTooltipHide(controlled, UiTooltipVisibilityReason.Handover);
            }
        }
        _tooltipVisibleOwner = owner;
        return ++_tooltipOpenOrder;
    }

    private void UnregisterVisibleTooltip(IUiTooltipTarget owner)
    {
        if (ReferenceEquals(owner, _tooltipVisibleOwner))
        {
            _tooltipVisibleOwner = null;
        }
    }

    private void CloseAutomaticTooltip(UiTooltipVisibilityReason reason)
    {
        if (_tooltipOwner is { IsOpen : true } owner)
        {
            QueueTooltipVisibility(owner, _tooltipAutoCallback, false, reason);
        }
        ReleaseTooltipOwner();
    }

    private void QueueTooltipVisibility(IUiTooltipTarget owner, Action<UiTooltipVisibilityChange>? callback,
        bool visible, UiTooltipVisibilityReason reason)
    {
        if (callback is not null)
        {
            (_tooltipNotifications ??= []).Add(new(owner, callback, new(visible, reason)));
        }
    }

    private void DispatchTooltipVisibility()
    {
        if (_tooltipNotifications is not { Count : > 0 } notifications)
        {
            return;
        }
        try
        {
            for (var i = 0; i < notifications.Count; i++)
            {
                var notification = notifications[i];
                if (notification.Owner.IsAlive)
                {
                    notification.Callback(notification.Change);
                }
            }
        }
        finally
        {
            notifications.Clear();
        }
    }

    private void PaintControlledTooltips()
    {
        if (_controlledTooltips is null)
        {
            return;
        }
        foreach (var tooltip in _controlledTooltips)
        {
            if (!tooltip.Owner.IsOpen)
            {
                continue;
            }
            var savedTexture = _tooltipSurfaceTexture;
            var savedPaintFrame = _tooltipSurfacePaintFrame;
            _tooltipSurfaceTexture = tooltip.Texture;
            _tooltipSurfacePaintFrame = tooltip.TexturePaintFrame;
            try
            {
                PaintTooltip(tooltip.Owner, tooltip.Text, tooltip.Options, tooltip.Semantics, tooltip.TargetBounds,
                    tooltip.Tier, tooltip.Environment, ref tooltip.SurfaceBounds, ref tooltip.ContentSize, ref tooltip.ScrollOffset);
            }
            finally
            {
                tooltip.Texture = _tooltipSurfaceTexture;
                tooltip.TexturePaintFrame = _tooltipSurfacePaintFrame;
                _tooltipSurfaceTexture = savedTexture;
                _tooltipSurfacePaintFrame = savedPaintFrame;
            }
        }
    }

    private void CloseControlledTooltip(ControlledTooltip tooltip)
    {
        ReleaseTooltipScrollbar(tooltip.Owner);
        UnregisterVisibleTooltip(tooltip.Owner);
        tooltip.Owner.IsOpen = false;
        if (tooltip.Texture is { } texture)
        {
            DestroyTooltipSurface(texture);
            tooltip.Texture = null;
        }
        tooltip.ContentSize = tooltip.ScrollOffset = default;
    }

    private void ReleaseControlledTooltip(IUiTooltipTarget owner)
    {
        if (FindControlledTooltip(owner) is { } tooltip)
        {
            CloseControlledTooltip(tooltip);
            _controlledTooltips!.Remove(tooltip);
            _requestFrame?.Invoke();
        }
    }

    private void ReleaseControlledTooltips()
    {
        if (_controlledTooltips is not null)
        {
            foreach (var tooltip in _controlledTooltips)
            {
                CloseControlledTooltip(tooltip);
            }
            _controlledTooltips.Clear();
        }
        _tooltipNotifications?.Clear();
    }
}
