namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderPopover<TPresentation>(string id, string label, UiScrollView viewport, UiPopoverInputState input,
        UiPopoverOptions options, bool? controlledOpen, bool? pending) where TPresentation : IUiPopoverPresentation
    {
        var context = new UiControlContext(this);
        if (RightToLeft)
            options = options with { Positioning = options.Positioning with { RightToLeft = true } };
        using var typography = TPresentation.BeginTriggerText(context);
        var size = context.ConstrainToLayout(TPresentation.MeasureTrigger(context, label));
        var origin = AllocateLayoutBox(size);
        var trigger = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        var surfaceId = ResolveCachedIdentifier(root, "##surface", false);
        input.Trigger = root;
        input.Surface = surfaceId;
        input.TriggerTier = _popupTierDepth;
        input.SeenFrame = GetFrameCount();
        input.Parent = _drawingOverlay;
        if (input.TriggerMode != options.TriggerMode)
        {
            input.TriggerMode = options.TriggerMode;
            input.HoverInside = input.ContextPress = false;
            input.HoverCloseAt = null;
            input.ContextPoint = null;
        }
        var clip = IntersectRect(CurrentClipRect, trigger);
        var ownGate = input.Open && input.WasExclusive && input.Tier == _blockingKeyboardTier;
        var available = IsControlEnabled && clip.Width > 0 && clip.Height > 0
            && (_drawingOverlay is null || _drawingOverlay.Open);
        try
        {
            if (!available && input.Open)
                CancelOverlay(input, false, true, true);
            if (available && controlledOpen == false && input.Open)
                CancelOverlay(input, OverlayContains(input, _state.FocusedId), false, true);
            var oldGate = _blockingKeyboardTier;
            if (ownGate)
                _blockingKeyboardTier = _popupTierDepth;
            UiButtonInteraction button;
            try
            {
                ClaimAccessibilityField(root);
                button = ProcessButton(id, trigger, UiButtonActivation.Release);
                button = ApplyAccessibilityInvoke(root, trigger, options.TriggerMode == UiPopoverTriggerMode.Context ? button with
                {
                    Activated = false
                } : button);
                ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.Button, label, trigger, valuePattern: false, literalLabel: true);
            }
            finally { _blockingKeyboardTier = oldGate; }
            var opened = false;
            if (available)
            {
                if (controlledOpen == true && !input.Open)
                {
                    input.Open = true;
                    if (options.TriggerMode == UiPopoverTriggerMode.Context)
                        input.ContextPoint ??= WidgetScreenPoint(new(trigger.X + trigger.Width / 2, trigger.Y + trigger.Height / 2));
                    (_overlays ??= []).Add(input);
                    input.Changed?.Invoke(true);
                    opened = true;
                    _requestFrame?.Invoke();
                }
                if (pending is { } wanted)
                {
                    var wasOpen = input.Open;
                    if (wanted && options.TriggerMode == UiPopoverTriggerMode.Context)
                        OpenPopoverAt(input, new(trigger.X + trigger.Width / 2, trigger.Y + trigger.Height / 2));
                    else
                        RequestOverlayChange(input, wanted, !wanted);
                    opened |= !wasOpen && input.Open;
                }
                var expansion = _accessibility?.Take(root, UiAccessibilityAction.Expand) ?? _accessibility?.Take(root, UiAccessibilityAction.Collapse);
                if (expansion is { } request)
                {
                    var wasOpen = input.Open;
                    var accepted = request.Action == UiAccessibilityAction.Expand && options.TriggerMode == UiPopoverTriggerMode.Context
                        ? OpenPopoverAt(input, new(trigger.X + trigger.Width / 2, trigger.Y + trigger.Height / 2))
                        : RequestOverlayChange(input, request.Action == UiAccessibilityAction.Expand, request.Action == UiAccessibilityAction.Collapse);
                    request.Result = accepted ? UiAccessibilityResult.Success : UiAccessibilityResult.InvalidOperation;
                    opened |= !wasOpen && input.Open;
                }
                else if (button.Activated)
                {
                    var wasOpen = input.Open;
                    if (options.TriggerMode == UiPopoverTriggerMode.Context)
                        OpenPopoverAt(input, new(trigger.X + trigger.Width / 2, trigger.Y + trigger.Height / 2));
                    else
                        RequestOverlayChange(input, !input.Open, input.Open);
                    opened |= !wasOpen && input.Open;
                }
            }
            var beforeTriggerEvents = input.Open;
            ProcessPopoverPointerTrigger(input, options, trigger, button.Hovered, available);
            opened |= !beforeTriggerEvents && input.Open;
            TPresentation.PaintTrigger(context, label, trigger, button with
            {
                Expanded = input.Open
            });
            if (!input.Open)
                return;
            trigger = WidgetScreenBounds(trigger);
            using var screenSpace = BeginWidgetScreenSpace();
            if (!opened && (_leftMousePressed || _rightMousePressed) && !OverlayPointerInside(input, trigger))
            {
                if (RequestOverlayChange(input, false, false))
                    return;
            }
            if (!opened && (options.CloseOnScroll || options.TriggerMode == UiPopoverTriggerMode.Context) && (_mouseWheel != 0 || _mouseWheelHorizontal != 0) && !OverlayPointerInside(input, trigger))
            {
                if (RequestOverlayChange(input, false, false))
                    return;
            }
            var ownFocus = OverlayOwnsEscape(input);
            if (!opened && ownFocus && string.IsNullOrEmpty(_imeHandler?.GetCompositionText())
                && _keyEvents.Any(key => key.IsDown && key.Key == UiKey.Escape && key.Modifiers == 0))
            {
                if (RequestOverlayChange(input, false, true))
                    return;
            }
            var within = OverlayContains(input, _state.FocusedId);
            var inFocusRegion = within || _state.FocusedId == root;
            if (options.CloseOnFocusLeave && input.HadFocusInRegion && !inFocusRegion
                && _state.FocusedId is not null && !opened)
            {
                input.HadFocusInRegion = false;
                if (RequestOverlayChange(input, false, false))
                    return;
            }
            if (options.TrapFocus && input.HadFocusWithin && !within && _state.FocusedId != root && !opened)
            {
                if (RequestOverlayChange(input, false, false))
                    return;
            }
            using var body = TPresentation.BeginBodyText(context);
            var padding = options.ContentPadding ?? TPresentation.Padding(options);
            if (!float.IsFinite(padding) || padding < 0)
                throw new InvalidOperationException("Popover padding must be finite and nonnegative.");
            var width = MathF.Min(options.Width, _displaySize.X);
            var maximumHeight = MathF.Min(options.MaximumHeight ?? _displaySize.Y, _displaySize.Y);
            var contentWidth = MathF.Max(0, width - 2 * padding);
            var contentSize = context.MeasureParentDataChild(viewport.Child, null, new(contentWidth, contentWidth, 0, float.PositiveInfinity));
            var height = MathF.Min(maximumHeight, contentSize.Y + 2 * padding);
            context.MeasureParentDataChild(viewport, null, UiBoxConstraints.Tight(new(contentWidth, MathF.Max(0, height - 2 * padding))));
            var positioning = options.Positioning;
            if (options.WithArrow && !positioning.CoverTarget)
            {
                var arrowHeight = options.Size == UiPopoverSize.Small ? 6 : 8;
                positioning = positioning with
                {
                    ArrowHeight = arrowHeight,
                    ArrowWidth = arrowHeight * 1.414f,
                    ArrowPadding = positioning.ArrowPadding ?? 8
                };
            }
            else
                positioning = positioning with
                {
                    ArrowHeight = 0
                };
            var target = input.ContextPoint is { } point ? new UiRect(point.X, point.Y, 0, 0) : trigger;
            var layout = UiPopupPlacement.Arrange(target, new(width, height), new(0, 0, _displaySize.X, _displaySize.Y), positioning);
            var rasterLayout = layout;
            if (opened && options.MotionEnabled)
                input.EnterStartedAt = _state.TimeSeconds;
            var motionActive = options.MotionEnabled && input.EnterStartedAt is { } started
                && _state.TimeSeconds - started < UiPopoverMotion.DurationSeconds;
            var opacity = 1f;
            input.MotionNextAt = null;
            if (motionActive)
            {
                var motion = UiPopoverMotion.Sample(_state.TimeSeconds - input.EnterStartedAt!.Value, layout.Position, positioning.RightToLeft);
                opacity = motion.Opacity;
                layout = UiPopoverMotion.Translate(layout, motion.Offset);
                input.MotionNextAt = Math.Min(input.EnterStartedAt.Value + UiPopoverMotion.DurationSeconds, _state.TimeSeconds + 1d / 60);
            }
            else
                input.ResetMotion();
            input.Bounds = layout.Bounds;
            input.Arrow = layout.Arrow;
            input.AdditionalBlockingBounds = null;
            if (layout.Arrow is { } blockingArrow)
            {
                var center = new UiVector2((blockingArrow.BaseStart.X + blockingArrow.BaseEnd.X) / 2,
                    (blockingArrow.BaseStart.Y + blockingArrow.BaseEnd.Y) / 2);
                var dx = blockingArrow.Tip.X - center.X;
                var dy = blockingArrow.Tip.Y - center.Y;
                var extent = MathF.Abs(dx) + MathF.Abs(dy);
                input.AdditionalBlockingBounds = dx == 0
                    ? new(center.X - extent, MathF.Min(center.Y, blockingArrow.Tip.Y), extent * 2, extent)
                    : new(MathF.Min(center.X, blockingArrow.Tip.X), center.Y - extent, extent, extent * 2);
            }
            _state.ReleasePreviousPopupKeyboard(surfaceId);
            _blockingKeyboardTier = Math.Max(_state.PreviousPopupKeyboardTier, _state.CurrentPopupKeyboardTier);
            var parentTier = _popupTierDepth;
            var layer = ResolveOverlayTier(input, parentTier, opened);
            _popupTierDepth = layer - 1;
            PushPopup();
            PushClipRect(new(0, 0, _displaySize.X, _displaySize.Y), false);
            var parent = _accessibilityParent;
            var widget = _accessibilityWidget;
            var drawing = _drawingOverlay;
            var start = _currentTabStops.Count;
            InteractivePaintCapture? motionCapture = null;
            var paintCompleted = false;
            try
            {
                input.Tier = _popupTierDepth;
                input.WasExclusive = options.TrapFocus;
                _state.AddPopupBlockingRect(layout.Bounds, input.Tier, options.TrapFocus ? UiPopupInputMode.Exclusive : UiPopupInputMode.NonModal, surfaceId);
                if (input.AdditionalBlockingBounds is { } arrowBlocking)
                    _state.AddPopupBlockingRect(arrowBlocking, input.Tier, UiPopupInputMode.NonModal, surfaceId);
                input.RegisteredFrame = GetFrameCount();
                _blockingKeyboardTier = Math.Max(_blockingKeyboardTier, _state.CurrentPopupKeyboardTier);
                if (motionActive)
                {
                    var paintBounds = TPresentation.PaintBounds(context, layout, options);
                    UiPopupPlacement.ValidateRect(paintBounds);
                    var viewportBounds = new UiRect(0, 0, _displaySize.X, _displaySize.Y);
                    var visibleBody = IntersectRect(layout.Bounds, viewportBounds);
                    if (visibleBody.Width > 0 && visibleBody.Height > 0 &&
                        (paintBounds.X > visibleBody.X || paintBounds.Y > visibleBody.Y
                        || paintBounds.X + paintBounds.Width < visibleBody.X + visibleBody.Width
                        || paintBounds.Y + paintBounds.Height < visibleBody.Y + visibleBody.Height))
                        throw new InvalidOperationException("Popover paint bounds must contain the visible surface.");
                    var captureBounds = IntersectRect(paintBounds, viewportBounds);
                    if (captureBounds.Width > 0 && captureBounds.Height > 0)
                        motionCapture = BeginInteractivePaintGroup(input.MotionPaint ??= new(), captureBounds);
                    input.Paint.MotionRasterLayout = rasterLayout;
                }
                try
                {
                    TPresentation.PaintSurface(context, input.Paint, layout, options);
                }
                finally { input.Paint.MotionRasterLayout = null; }
                _accessibilityWidget = null;
                _accessibilityParent = surfaceId;
                _drawingOverlay = input;
                ExposeAccessibility(surfaceId, parent, options.TrapFocus ? UiAccessibilityRole.Dialog : options.SurfaceRole,
                    label, layout.Bounds, focusable: false, valuePattern: false, literalLabel: true);
                _accessibility?.SetModal(surfaceId, options.TrapFocus);
                var contentBounds = new UiRect(layout.Bounds.X + padding, layout.Bounds.Y + padding, contentWidth, MathF.Max(0, height - 2 * padding));
                _accessibility?.CaptureScrollViewport(surfaceId, contentBounds, contentBounds,
                    new(new(contentBounds.Width, contentBounds.Height), new(contentBounds.Width, contentBounds.Height), default), independentRoot: true);
                PushStyleColor(UiStyleColor.Text, TPresentation.Foreground(context, options));
                try
                {
                    RenderParentDataChild(viewport, null, contentBounds, contentBounds);
                }
                finally { PopStyleColor(); }
                CompleteOverlayFocus(input, surfaceId, layout.Bounds, start, opened, !options.DisableAutoFocus);
                paintCompleted = true;
            }
            finally
            {
                try
                {
                    if (motionCapture is { } capture)
                        EndInteractivePaintGroup(capture, paintCompleted && input.Open, opacity);
                }
                finally { _drawingOverlay = drawing; _accessibilityParent = parent; _accessibilityWidget = widget; PopClipRect(); PopPopup(); _popupTierDepth = parentTier; }
            }
        }
        finally
        {
            _accessibility?.SetExpansion(root, input.Open ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed);
            if (input.Open)
                _accessibility?.SetControlledElement(root, surfaceId);
            FinishLayoutBox(origin, size);
        }
    }
}
