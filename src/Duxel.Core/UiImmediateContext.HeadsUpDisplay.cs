using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private static readonly UiCubicBezier HudLinear = new(0, 0, 1, 1);
    internal void PrepareHeadsUpDisplayOpening(UiHeadsUpDisplayInputState input) =>
        input.EntryFrom = ResolveOverlayEntry(input, new(1.3f, 0, 0));
    internal void PrepareHeadsUpDisplayClosing(UiHeadsUpDisplayInputState input)
    {
        if (!input.MotionEnabled || input.SurfacePaint is null) return;
        PrepareOverlaySurfaceExit(input, ref input.SurfacePaint, input.NaturalBounds, default,
            input.LastMotion, .2, .8f, HudLinear, HudLinear);
    }
    internal void RenderHeadsUpDisplay<TBehavior, TPresentation>(UiHeadsUpDisplayWidget<TBehavior, TPresentation> widget,
        string id, bool? controlled, bool? pending,
        UiControlActivationOrigin? origin)
        where TBehavior : IUiHeadsUpDisplayBehavior
        where TPresentation : IUiHeadsUpDisplayPresentation
    {
        var input = widget.Input;
        var options = widget.Options;
        widget.Content.HostReducedMotion = options.ReducedMotion;
        var surfaceId = ResolveCachedIdentifier(ResolveId(id), "##surface", false);
        input.Surface = surfaceId;
        input.SeenFrame = GetFrameCount();
        input.Parent = _drawingOverlay;
        input.MotionEnabled = !options.ReducedMotion && !widget.Content.Options.ReducedMotion;
        if (!input.MotionEnabled)
        {
            CancelOverlayExit(input);
            input.SurfacePaint?.Dispose();
            input.SurfacePaint = null;
        }
        if (!IsControlEnabled || _drawingOverlay is { Open: false })
        {
            CancelOverlayExit(input);
            if (input.Open) CancelOverlay(input, false, true, true);
            return;
        }
        if (controlled == false && input.Open)
            CancelOverlay(input, OverlayContains(input, _state.FocusedId), false, true);
        var wasOpen = input.Open;
        if (controlled == true && !input.Open || pending == true && !input.Open)
        {
            if (origin is { } invoking)
            {
                if (!ReferenceEquals(invoking.ContextToken, _controlActivationToken))
                    throw new InvalidOperationException("A HUD cannot use an activation origin from another UI context.");
                input.Trigger = invoking.Id;
                input.TriggerTier = invoking.Tier;
                input.TriggerSeenFrame = invoking.Frame;
            }
            else
            {
                input.Trigger = _state.FocusedId;
                input.TriggerTier = _popupTierDepth;
            }
            RequestOverlayChange(input, true, false);
        }
        if (pending == false && input.Open) RequestOverlayChange(input, false, true);
        if (!input.Open) return;
        var opened = !wasOpen;
        if (opened || pending == true || input.Persistent != options.IsPersistent)
        {
            input.Persistent = options.IsPersistent;
            input.AutoDismissAt = options.IsPersistent ? null : _state.TimeSeconds + widget.AutoDismissSeconds;
        }
        if (input.AutoDismissAt is { } deadline && _state.TimeSeconds >= deadline)
        {
            // A controlled owner may reject dismissal. The request is made once, never polled every frame.
            RequestOverlayChange(input, false, true);
            if (!input.Open) return;
        }
        if (_displaySize.X <= 0 || _displaySize.Y <= 0)
        {
            input.MotionNextAt = null;
            return;
        }
        using var screen = BeginWidgetScreenSpace();
        var availableHeight = MathF.Max(0, _displaySize.Y - options.KeyboardHeight - (options.KeyboardHeight > 0 ? 50 : 0));
        var context = new UiControlContext(this);
        var size = context.MeasureParentDataChild(widget.Content, null, new(0, _displaySize.X, 0, availableHeight));
        var bounds = new UiRect((_displaySize.X - size.X) / 2, (availableHeight - size.Y) / 2, size.X, size.Y);
        if ((opened || pending == true) && input.MotionEnabled && input.EntryFrom != new UiOverlayMotionSample(1, 1, 0))
            input.EnterStartedAt = _state.TimeSeconds;
        var active = input.MotionEnabled && input.EnterStartedAt is { } started && _state.TimeSeconds - started < .2;
        var progress = active ? (float)Math.Clamp((_state.TimeSeconds - input.EnterStartedAt!.Value) / .2, 0, 1) : 1;
        var from = input.EntryFrom;
        var motion = new UiOverlayMotionSample(from.Scale + (1 - from.Scale) * progress,
            from.Opacity + (1 - from.Opacity) * progress, 0);
        var matrix = Matrix3x2.CreateScale(motion.Scale, new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        input.Bounds = UiWidgetAffineLayout.MapBounds(bounds, matrix);
        input.NaturalBounds = bounds;
        input.LastMotion = motion;
        var hasArea = bounds.Width > 0 && bounds.Height > 0;
        input.MotionNextAt = active && hasArea ? Math.Min(input.EnterStartedAt!.Value + .2, _state.TimeSeconds + 1d / 60) : null;
        if (!active) input.ResetMotion();
        var exclusive = options.IsBlocking;
        var focusOnActivation = opened || exclusive && !input.WasExclusive;
        input.AdditionalBlockingBounds = exclusive ? new(0, 0, _displaySize.X, _displaySize.Y) : null;
        _state.ReleasePreviousPopupKeyboard(surfaceId);
        _blockingKeyboardTier = Math.Max(_state.PreviousPopupKeyboardTier, _state.CurrentPopupKeyboardTier);
        var parentTier = _popupTierDepth;
        _popupTierDepth = ResolveOverlayTier(input, parentTier, opened) - 1;
        PushPopup();
        PushClipRect(new(0, 0, _displaySize.X, _displaySize.Y), false);
        var parent = _accessibilityParent;
        var parentWidget = _accessibilityWidget;
        var drawing = _drawingOverlay;
        var start = _currentTabStops.Count;
        InteractivePaintCapture? capture = null;
        WidgetAffineSpaceScope? coordinates = null;
        var completed = false;
        try
        {
            input.Tier = _popupTierDepth;
            if (input.WasExclusive != exclusive) _requestFrame?.Invoke();
            input.WasExclusive = exclusive;
            _state.AddPopupBlockingRect(input.Bounds, input.Tier, exclusive ? UiPopupInputMode.Exclusive : UiPopupInputMode.NonModal, surfaceId);
            if (input.AdditionalBlockingBounds is { } blocking)
                _state.AddPopupBlockingRect(blocking, input.Tier, UiPopupInputMode.NonModal, surfaceId);
            input.RegisteredFrame = GetFrameCount();
            _blockingKeyboardTier = Math.Max(_blockingKeyboardTier, _state.CurrentPopupKeyboardTier);
            if (input.MotionEnabled && hasArea) capture = BeginInteractivePaintGroup(input.SurfacePaint ??= new(), bounds);
            else if (!hasArea)
            {
                input.SurfacePaint?.Dispose();
                input.SurfacePaint = null;
            }
            if (active && hasArea) coordinates = BeginWidgetAffineSpace(matrix, motion.Scale,
                new(0, 0, _displaySize.X, _displaySize.Y), rasterScale : 1);
            _accessibilityWidget = null;
            _accessibilityParent = surfaceId;
            _drawingOverlay = input;
            ExposeAccessibility(surfaceId, parent, UiAccessibilityRole.Dialog,
                widget.AccessibleName ?? widget.Content.Options.Label ?? widget.Content.Options.Kind.ToString(), bounds,
                focusable : false, valuePattern : false, literalLabel : true, busy : widget.Content.Options.Kind == UiHeadsUpDisplayKind.Activity);
            _accessibility?.SetModal(surfaceId, exclusive);
            var previousFocus = _state.FocusedId;
            RenderParentDataChild(widget.Content, null, bounds, bounds);
            if (exclusive || widget.Content.Tapped is not null)
                CompleteOverlayFocus(input, surfaceId, bounds, start, focusOnActivation, exclusive, exclusive, previousFocus);
            completed = true;
        }
        finally
        {
            try
            {
                coordinates?.Dispose();
                if (capture is { } group)
                    EndInteractivePaintGroup(group, completed && input.Open, motion.Opacity, UiWidgetAffineLayout.MapBounds(group.Bounds, matrix));
            }
            finally
            {
                _drawingOverlay = drawing;
                _accessibilityParent = parent;
                _accessibilityWidget = parentWidget;
                PopClipRect();
                PopPopup();
                _popupTierDepth = parentTier;
            }
        }
    }
}
