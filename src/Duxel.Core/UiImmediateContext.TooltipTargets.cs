namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private IUiTooltipTarget? _tooltipOwner;
    private IUiTooltipTarget? _tooltipCandidate;
    private IUiTooltipTarget? _tooltipSuppressed;
    private IUiTooltipTarget? _tooltipSurfaceUnderPointer;
    private string _tooltipOwnerText = string.Empty;
    private string _tooltipCandidateText = string.Empty;
    private UiTooltipOptions _tooltipOwnerOptions;
    private UiTooltipOptions _tooltipCandidateOptions;
    private TooltipAccessibilityScope? _tooltipOwnerSemantics;
    private TooltipAccessibilityScope? _tooltipCandidateSemantics;
    private UiRect _tooltipTargetBounds;
    private UiRect _tooltipSurfaceBounds;
    private UiVector2 _tooltipContentSize;
    private UiVector2 _tooltipScrollOffset;
    private UiRect _tooltipCandidateBounds;
    private UiControlEnvironment _tooltipCandidateEnvironment;
    private int _tooltipOwnerTier;
    private int _tooltipCandidateTier;
    private int _tooltipCandidatePriority;
    private int _tooltipCandidateDepth;
    private int _tooltipTargetDepth;
    private bool _tooltipOwnerSeen;
    private bool _tooltipSuppressedSeen;
    private bool _tooltipEscapeConsumed;
    private double _tooltipDeadline;
    private bool _tooltipHiding;
    private bool _tooltipOwnerFocused;
    private bool _tooltipFocusLost;
    private bool _tooltipCandidateFocused;
    private List<UiKeyEvent>? _tooltipFilteredKeys;

    internal void BeginTooltipTargets(UiHostVisibility visibility)
    {
        _tooltipCandidate = null;
        _tooltipCandidateText = string.Empty;
        _tooltipCandidateSemantics = null;
        _tooltipCandidatePriority = 0;
        _tooltipCandidateDepth = 0;
        _tooltipOwnerSeen = false;
        _tooltipSuppressedSeen = false;
        _tooltipEscapeConsumed = false;
        _tooltipSurfaceUnderPointer = null;
        _tooltipFocusLost = false;
        ObserveTooltipHostVisibility(visibility);
        if (visibility.IsHidden)
        {
            return;
        }
        BeginControlledTooltips();
        var owner = _tooltipOwner is { IsOpen: true } ? _tooltipOwner : null;
        var firstControlled = FirstControlledTooltip();
        if (owner is null && firstControlled is null)
        {
            return;
        }
        foreach (var key in _keyEvents)
        {
            if (key.Key != UiKey.Escape || !key.IsDown)
            {
                continue;
            }
            if (firstControlled is not null && (owner is null || firstControlled.OpenOrder < _tooltipAutoOpenOrder))
            {
                firstControlled.Escape = true;
            }
            else
            {
                _tooltipSuppressed = owner;
                CloseAutomaticTooltip(UiTooltipVisibilityReason.Escape);
            }
            _tooltipEscapeConsumed = true;
            break;
        }
        ResolveTooltipSurfaceInput();
        if (!_tooltipEscapeConsumed)
        {
            return;
        }
        // All widget handlers see the same consumed event stream, including legacy input.
        var filtered = _tooltipFilteredKeys ??= [];
        filtered.Clear();
        foreach (var key in _keyEvents)
        {
            if (key.Key != UiKey.Escape)
            {
                filtered.Add(key);
            }
        }
        _keyEvents = filtered;
    }

    internal void RenderTooltipTarget<TBehavior>(IUiTooltipTarget owner, UiWidget target, UiWidget measurementOwner,
        ref UiRetainedLayout<UiVector2>? retained)
        where TBehavior : IUiTooltipBehavior
    {
        EnsureNotMeasuring();
        var size = UiWidget.MeasureOwnedChild(measurementOwner, target, new(this), CurrentLayoutConstraints, ref retained, true);
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var text = owner.Text;
        var options = owner.Options;
        var visible = owner.Visible;
        var visibilityCallback = owner.VisibleChangeRequested;
        var depth = ++_tooltipTargetDepth;
        var captured = BeginTooltipAccessibility(owner, text);
        bool focused;
        TooltipAccessibilityScope? semantics = null;
        try
        {
            focused = RenderAllocatedChild(_builder, target, bounds, bounds, constrainLayout: true);
        }
        finally
        {
            semantics = EndTooltipAccessibility(captured);
            _tooltipTargetDepth--;
        }
        var clip = IntersectRect(CurrentClipRect, bounds);
        var targetVisible = clip.Width > 0 && clip.Height > 0 && _popupTierDepth >= _blockingKeyboardTier;
        var pointerRoutedToTarget = _layoutPointerBlockedTier != _popupTierDepth;
        // A sibling can own the pointer while the caller still keeps this tooltip open.
        var available = targetVisible && (pointerRoutedToTarget || visible == true);
        var hovered = targetVisible && pointerRoutedToTarget && ItemHoverable(ResolveId("\u001ftooltip-target"), bounds,
            allowDisabled: true, setHovered: false);
        _lastItemPos = origin;
        _lastItemSize = size;
        _lastItemId = null;
        ObserveTooltipTarget<TBehavior>(owner, text, options, semantics, bounds, available, hovered, focused, depth, visible, visibilityCallback);
    }

    private void ObserveTooltipTarget<TBehavior>(IUiTooltipTarget owner, string text, UiTooltipOptions options,
        TooltipAccessibilityScope? semantics, UiRect bounds, bool available, bool hovered, bool focused, int depth,
        bool? visible = null, Action<UiTooltipVisibilityChange>? visibilityCallback = null) where TBehavior : IUiTooltipBehavior
    {
        bounds = WidgetScreenBounds(bounds);
        if (!owner.IsAlive || _tooltipHostVisibility.IsHidden)
        {
            return;
        }
        if (RightToLeft && !options.Positioning.RightToLeft)
            options = options with { Positioning = options.Positioning with { RightToLeft = true } };
        if (visible is { } externalVisible)
        {
            ObserveControlledTooltip<TBehavior>(owner, externalVisible, visibilityCallback, text, options, semantics,
                bounds, available, hovered, focused);
            return;
        }
        if (ReferenceEquals(_tooltipOwner, owner))
        {
            _tooltipAutoCallback = visibilityCallback;
            _tooltipOwnerSeen = available;
            _tooltipOwnerText = text;
            _tooltipOwnerOptions = options;
            _tooltipOwnerEnvironment = ControlEnvironment;
            _tooltipOwnerSemantics = semantics;
            _tooltipTargetBounds = bounds;
            _tooltipFocusLost = _tooltipOwnerFocused && !focused;
            _tooltipOwnerFocused = focused;
        }
        var surfaceHovered = available && ReferenceEquals(_tooltipSurfaceUnderPointer, owner);
        var requested = available && TBehavior.Requested(new(hovered, focused, surfaceHovered));
        if (ReferenceEquals(_tooltipSuppressed, owner))
        {
            _tooltipSuppressedSeen = requested;
        }
        else if (requested)
        {
            var priority = hovered ? 3 : surfaceHovered ? 2 : 1;
            if (priority > _tooltipCandidatePriority
                || priority == _tooltipCandidatePriority && depth >= _tooltipCandidateDepth)
            {
                _tooltipCandidate = owner;
                _tooltipCandidateCallback = visibilityCallback;
                _tooltipCandidateText = text;
                _tooltipCandidateOptions = options;
                _tooltipCandidateSemantics = semantics;
                _tooltipCandidateBounds = bounds;
                _tooltipCandidateTier = _popupTierDepth;
                _tooltipCandidateEnvironment = ControlEnvironment;
                _tooltipCandidateFocused = focused;
                _tooltipCandidatePriority = priority;
                _tooltipCandidateDepth = depth;
            }
        }
    }

    internal void EndTooltipTargets()
    {
        _tooltipSurfaceUnderPointer = null;
        if (_tooltipHostVisibility.IsHidden)
        {
            DispatchTooltipVisibility();
            return;
        }
        if (!_tooltipSuppressedSeen)
        {
            _tooltipSuppressed = null;
        }
        var now = _state.TimeSeconds;
        foreach (var popup in _state.CurrentPopupBlockingRects)
        {
            if (_tooltipCandidate is not null && popup.Tier > _tooltipCandidateTier)
            {
                _tooltipCandidate = null;
            }
            if (_tooltipOwner is not null && popup.Tier > _tooltipOwnerTier)
            {
                _tooltipOwnerSeen = false;
            }
        }
        var handover = _tooltipVisibleOwner is not null;
        if (!_tooltipOwnerSeen && _tooltipOwner is not null)
        {
            ReleaseTooltipOwner();
        }
        if (_tooltipCandidate is { } candidate)
        {
            if (!ReferenceEquals(candidate, _tooltipOwner))
            {
                CloseAutomaticTooltip(UiTooltipVisibilityReason.Handover);
                _tooltipOwner = candidate;
                _tooltipDeadline = now + _tooltipCandidateOptions.ShowDelayAfterHandover(handover);
            }
            _tooltipHiding = false;
            _tooltipTargetBounds = _tooltipCandidateBounds;
            _tooltipOwnerText = _tooltipCandidateText;
            _tooltipOwnerOptions = _tooltipCandidateOptions;
            _tooltipOwnerSemantics = _tooltipCandidateSemantics;
            _tooltipOwnerTier = _tooltipCandidateTier;
            _tooltipOwnerEnvironment = _tooltipCandidateEnvironment;
            _tooltipAutoCallback = _tooltipCandidateCallback;
            _tooltipOwnerFocused = _tooltipCandidateFocused;
            if (!candidate.IsOpen && now >= _tooltipDeadline)
            {
                candidate.IsOpen = true;
                _tooltipAutoOpenOrder = RegisterVisibleTooltip(candidate);
                QueueTooltipVisibility(candidate, _tooltipAutoCallback, true, UiTooltipVisibilityReason.Interaction);
            }
        }
        else if (_tooltipOwner is { } owner)
        {
            if (!owner.IsOpen || _tooltipFocusLost)
            {
                CloseAutomaticTooltip(UiTooltipVisibilityReason.Interaction);
            }
            else
            {
                if (!_tooltipHiding)
                {
                    _tooltipHiding = true;
                    _tooltipDeadline = now + _tooltipOwnerOptions.HideDelay;
                }
                if (now >= _tooltipDeadline)
                {
                    CloseAutomaticTooltip(UiTooltipVisibilityReason.Interaction);
                }
            }
        }
        _tooltipCandidate = null;
        _tooltipCandidateCallback = null;
        _tooltipCandidateText = string.Empty;
        _tooltipCandidateSemantics = null;
        ResolveControlledTooltips(now);
        DispatchTooltipVisibility();
        if (_tooltipOwner is { } active)
        {
            if (!active.IsOpen || _tooltipHiding)
            {
                RequestFrameAt(_tooltipDeadline);
            }
            if (active.IsOpen)
            {
                PaintTooltip(active, _tooltipOwnerText, _tooltipOwnerOptions, _tooltipOwnerSemantics, _tooltipTargetBounds,
                    _tooltipOwnerTier, _tooltipOwnerEnvironment, ref _tooltipSurfaceBounds, ref _tooltipContentSize, ref _tooltipScrollOffset);
            }
        }
        PaintControlledTooltips();
    }

    private void PaintTooltip(IUiTooltipTarget active, string text, UiTooltipOptions options,
        TooltipAccessibilityScope? semantics, UiRect targetBounds, int tier, UiControlEnvironment environment,
        ref UiRect surfaceBounds, ref UiVector2 contentSize, ref UiVector2 scrollOffset)
    {
        var context = new UiControlContext(this);
        using var scope = new UiControlThemeScope(this);
        var previousTier = _popupTierDepth;
        RestoreControlEnvironment(environment);
        _popupTierDepth = tier;
        PushPopup();
        PushClipRect(_clipRect, false);
        try
        {
            var layout = active.Arrange(context, text, targetBounds, _clipRect, options);
            var surface = layout.Bounds;
            if (!float.IsFinite(surface.X) || !float.IsFinite(surface.Y) || !float.IsFinite(surface.Width)
                || !float.IsFinite(surface.Height) || surface.Width <= 0 || surface.Height <= 0)
            {
                throw new InvalidOperationException("Tooltip presentation returned invalid surface geometry.");
            }
            if (!float.IsFinite(layout.ContentSize.X) || !float.IsFinite(layout.ContentSize.Y)
                || layout.ContentSize.X < 0 || layout.ContentSize.Y < 0)
            {
                throw new InvalidOperationException("Tooltip presentation returned invalid content geometry.");
            }
            surfaceBounds = surface;
            contentSize = layout.ContentSize;
            var maximumX = MathF.Max(0, layout.ContentSize.X - surface.Width);
            var rightToLeft = options.Positioning.RightToLeft;
            var nextOffset = new UiVector2(
                TooltipPhysicalScrollX(scrollOffset.X, maximumX, rightToLeft),
                Math.Clamp(scrollOffset.Y, 0, MathF.Max(0, layout.ContentSize.Y - surface.Height)));
            var accessibility = PrepareTooltipSurface(semantics, text, layout, options, ref nextOffset);
            layout = layout with
            {
                ScrollOffset = nextOffset
            };
            active.Paint(context, text, layout);
            if (options.ShowScrollbars)
            {
                PaintTooltipScrollbars(active, layout, rightToLeft);
            }
            scrollOffset = nextOffset with
            {
                X = ReverseHorizontalScroll(nextOffset.X, maximumX, rightToLeft)
            };
            if (accessibility is { } node)
            {
                _accessibility?.Add(node);
            }
        }
        finally
        {
            PopClipRect();
            PopPopup();
            _popupTierDepth = previousTier;
        }
    }

    private void ReleaseTooltipOwner()
    {
        ReleaseTooltipSurface();
        if (_tooltipOwner is { } owner)
        {
            ReleaseTooltipScrollbar(owner);
            UnregisterVisibleTooltip(owner);
            owner.IsOpen = false;
        }
        _tooltipOwner = null;
        _tooltipAutoCallback = null;
        _tooltipOwnerText = string.Empty;
        _tooltipOwnerSemantics = null;
        _tooltipContentSize = default;
        _tooltipScrollOffset = default;
        _tooltipHiding = false;
        _tooltipOwnerFocused = false;
    }

    internal void ReleaseTooltip(IUiTooltipTarget owner)
    {
        ReleaseControlledTooltip(owner);
        if (ReferenceEquals(_tooltipOwner, owner))
        {
            ReleaseTooltipOwner();
            _requestFrame?.Invoke();
        }
        if (ReferenceEquals(_tooltipCandidate, owner))
        {
            _tooltipCandidate = null;
            _tooltipCandidateCallback = null;
            _tooltipCandidateText = string.Empty;
            _tooltipCandidateSemantics = null;
        }
        if (ReferenceEquals(_tooltipSuppressed, owner))
        {
            _tooltipSuppressed = null;
        }
    }

    private void ReleaseTooltipTargets()
    {
        ReleaseTooltipOwner();
        ReleaseControlledTooltips();
        _tooltipSurfaceUnderPointer = null;
        _tooltipCandidate = _tooltipSuppressed = null;
        _tooltipCandidateCallback = null;
        _tooltipCandidateText = string.Empty;
        _tooltipCandidateSemantics = null;
        _tooltipFilteredKeys = null;
        _tooltipAccessibilityScopes = null;
    }
}
