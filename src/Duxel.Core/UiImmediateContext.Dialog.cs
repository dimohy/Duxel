using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderDialog<TPresentation>(UiDialog<TPresentation> widget, string id, bool? controlled, bool? pending,
        UiControlActivationOrigin? origin)
        where TPresentation : IUiDialogPresentation
    {
        var input = widget.Input;
        var options = widget.Options;
        var metrics = TPresentation.Metrics;
        if (!float.IsFinite(metrics.Padding) || metrics.Padding < 0 || !float.IsFinite(metrics.BorderWidth) || metrics.BorderWidth < 0
            || !float.IsFinite(metrics.Gap) || metrics.Gap < 0 || !float.IsFinite(metrics.CornerRadius) || metrics.CornerRadius < 0
            || !float.IsFinite(metrics.MinimumContentHeight) || metrics.MinimumContentHeight < 0)
            throw new InvalidOperationException("Dialog presentation metrics must be finite and nonnegative.");
        var root = ResolveId(id);
        var surfaceId = ResolveCachedIdentifier(root, "##surface", false);
        input.Surface = surfaceId;
        input.SeenFrame = GetFrameCount();
        input.Parent = _drawingOverlay;
        input.MotionEnabled = options.MotionEnabled;
        if (!options.MotionEnabled)
        {
            CancelOverlayExit(input);
            input.SurfacePaint?.Dispose();
            input.SurfacePaint = null;
        }
        if (!IsControlEnabled || _drawingOverlay is { Open: false })
        {
            CancelOverlayExit(input);
            if (input.Open)
                CancelOverlay(input, false, true, true);
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
                    throw new InvalidOperationException("A Dialog cannot use an activation origin from another UI context.");
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
        if (pending == false && input.Open)
            RequestOverlayChange(input, false, true);
        if (!input.Open)
            return;
        var opened = !wasOpen;
        using var screen = BeginWidgetScreenSpace();
        if (!opened && OverlayOwnsEscape(input, includeTrigger: false) && string.IsNullOrEmpty(_imeHandler?.GetCompositionText())
            && _keyEvents.Any(key => key.IsDown && key.Key == UiKey.Escape && key.Modifiers == 0)
            && RequestOverlayChange(input, false, true))
            return;
        var width = MathF.Min(options.Width, _displaySize.X);
        var maximumHeight = MathF.Min(options.MaximumHeight ?? _displaySize.Y, _displaySize.Y);
        var padding = metrics.Padding + metrics.BorderWidth;
        var innerWidth = MathF.Max(0, width - 2 * padding);
        var innerMaximumHeight = MathF.Max(0, maximumHeight - 2 * padding);
        widget.Body.Configure(_displaySize.X <= 480, _displaySize.Y <= 359, options.HasCloseButton, options.CloseLabel, innerMaximumHeight);
        var context = new UiControlContext(this);
        using var text = TPresentation.BeginBodyText(context);
        var natural = context.MeasureParentDataChild(widget.Body, null, new(innerWidth, innerWidth, 0, float.PositiveInfinity));
        var bodyHeight = MathF.Min(innerMaximumHeight, natural.Y);
        var height = MathF.Min(maximumHeight, bodyHeight + 2 * padding);
        var bounds = new UiRect((_displaySize.X - width) / 2, (_displaySize.Y - height) / 2, width, height);
        var bodyBounds = new UiRect(bounds.X + padding, bounds.Y + padding, innerWidth, bodyHeight);
        context.MeasureParentDataChild(widget.Viewport, null, UiBoxConstraints.Tight(new(bodyBounds.Width, bodyBounds.Height)));
        if (opened && options.MotionEnabled && input.EntryFrom != new UiOverlayMotionSample(1, 1, 1))
            input.EnterStartedAt = _state.TimeSeconds;
        var motionActive = options.MotionEnabled && input.EnterStartedAt is { } started
            && _state.TimeSeconds - started < UiDialogMotion.DurationSeconds;
        var motion = motionActive ? UiDialogMotion.SampleEntry(_state.TimeSeconds - input.EnterStartedAt!.Value, input.EntryFrom) : new UiOverlayMotionSample(1, 1, 1);
        var matrix = Matrix3x2.CreateScale(motion.Scale, new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        input.Bounds = UiWidgetAffineLayout.MapBounds(bounds, matrix);
        input.NaturalBounds = bounds;
        input.LastMotion = motion;
        input.MotionNextAt = motionActive ? Math.Min(input.EnterStartedAt!.Value + UiDialogMotion.DurationSeconds, _state.TimeSeconds + 1d / 60) : null;
        if (!motionActive)
            input.ResetMotion();
        var exclusive = options.Type != UiDialogType.NonModal;
        var focusOnActivation = opened || exclusive && !input.WasExclusive && !OverlayContains(input, _state.FocusedId);
        input.AdditionalBlockingBounds = exclusive ? new(0, 0, _displaySize.X, _displaySize.Y) : null;
        if (!opened && options.Type == UiDialogType.Modal && options.LightDismiss)
        {
            var inside = OverlayPointerInside(input);
            if (_leftMousePressed)
                input.BackdropPressed = !inside;
            var dismiss = _leftMouseReleased && input.BackdropPressed && !inside;
            if (!_leftMouseDown || _leftMouseReleased)
                input.BackdropPressed = false;
            if (dismiss && RequestOverlayChange(input, false, true))
                return;
        }
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
        InteractivePaintCapture? motionCapture = null;
        WidgetAffineSpaceScope? motionCoordinates = null;
        var paintCompleted = false;
        try
        {
            input.Tier = _popupTierDepth;
            if (input.WasExclusive != exclusive)
                _requestFrame?.Invoke();
            input.WasExclusive = exclusive;
            _state.AddPopupBlockingRect(input.Bounds, input.Tier, exclusive ? UiPopupInputMode.Exclusive : UiPopupInputMode.NonModal, surfaceId);
            if (input.AdditionalBlockingBounds is { } backdrop)
                _state.AddPopupBlockingRect(backdrop, input.Tier, UiPopupInputMode.NonModal, surfaceId);
            input.RegisteredFrame = GetFrameCount();
            _blockingKeyboardTier = Math.Max(_blockingKeyboardTier, _state.CurrentPopupKeyboardTier);
            input.BackdropColor = exclusive ? TPresentation.Backdrop(context) : default;
            if (exclusive)
            {
                var color = input.BackdropColor;
                color = new((color.Rgba & 0x00ffffff) | ((uint)MathF.Round((color.Rgba >> 24) * motion.BackdropOpacity) << 24));
                AddRectFilled(new(0, 0, _displaySize.X, _displaySize.Y), color, _whiteTexture);
            }
            var shadow = input.Shadow ??= new(TPresentation.Shadow(context));
            shadow.Options = TPresentation.Shadow(context);
            if (options.MotionEnabled)
                motionCapture = BeginInteractivePaintGroup(input.SurfacePaint ??= new(), shadow.Options.PaintBounds(bounds));
            if (motionActive)
                motionCoordinates = BeginWidgetAffineSpace(matrix, motion.Scale, new(0, 0, _displaySize.X, _displaySize.Y), rasterScale: 1);
            context.BoxShadow(shadow, bounds, metrics.CornerRadius);
            context.Frame(bounds, TPresentation.Background(context), default, metrics.CornerRadius, 0);
            _accessibilityWidget = null;
            _accessibilityParent = surfaceId;
            _drawingOverlay = input;
            ExposeAccessibility(surfaceId, parent, options.Type == UiDialogType.Alert ? UiAccessibilityRole.AlertDialog : UiAccessibilityRole.Dialog,
                widget.AccessibleName ?? widget.Title, bounds, focusable: false, valuePattern: false, literalLabel: true);
            _accessibility?.SetModal(surfaceId, exclusive);
            _accessibility?.SetDialogDescription(surfaceId, options.Description);
            var previousFocus = _state.FocusedId;
            PushStyleColor(UiStyleColor.Text, TPresentation.Foreground(context));
            try
            {
                RenderParentDataChild(widget.Viewport, null, bodyBounds, bodyBounds);
            }
            finally { PopStyleColor(); }
            CompleteOverlayFocus(input, surfaceId, bounds, start, focusOnActivation, true, exclusive, previousFocus);
            paintCompleted = true;
        }
        finally
        {
            try
            {
                motionCoordinates?.Dispose();
                if (motionCapture is { } capture)
                    EndInteractivePaintGroup(capture, paintCompleted && input.Open, motion.Opacity,
                        UiWidgetAffineLayout.MapBounds(capture.Bounds, matrix));
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

    internal void RenderSurfaceBody<TPresentation>(UiSurfaceBody<TPresentation> body) where TPresentation : IUiSurfacePresentation
    {
        var context = new UiControlContext(this);
        var layout = body.Layout(context);
        var origin = AllocateLayoutBox(layout.Size);
        var clip = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var gap = TPresentation.Metrics.Gap;
        var titleWidth = MathF.Max(0, layout.Size.X - (body.HasClose ? 24 + gap : 0));
        var titleBounds = new UiRect(origin.X + (body.HasClose && body.RightToLeft ? 24 + gap : 0), origin.Y, titleWidth, layout.Title.Y);
        RenderParentDataChild(body.Title, null, titleBounds, clip);
        if (body.HasClose)
            RenderParentDataChild(body.Close, null, new(body.RightToLeft ? origin.X : origin.X + layout.Size.X - 24, origin.Y, 24, 24), clip);
        var contentY = origin.Y + layout.HeaderHeight + gap;
        RenderParentDataChild(body.Content, null, new(origin.X, contentY, layout.Size.X, layout.ContentHeight), clip);
        if (body.Actions.Children.Length > 0)
            RenderParentDataChild(body.Actions, null, new(origin.X + (body.ActionsStart != body.RightToLeft ? 0 : layout.Size.X - layout.Actions.X),
                contentY + layout.ContentHeight + gap, layout.Actions.X, layout.Actions.Y), clip);
        FinishLayoutBox(origin, layout.Size);
    }

    internal void RenderSurfaceActions(UiSurfaceActionBar actions, UiVector2[] sizes)
    {
        var size = actions.Layout(new(this));
        var origin = AllocateLayoutBox(size);
        var clip = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var offset = 0f;
        for (var i = 0; i < actions.Children.Length; i++)
        {
            var child = sizes[i];
            RenderParentDataChild(actions.Children[i], null, new(origin.X + (actions.IsStacked ? 0 : actions.RightToLeft ? size.X - offset - child.X : offset),
                origin.Y + (actions.IsStacked ? offset : 0), actions.IsStacked ? size.X : child.X, child.Y), clip);
            offset += (actions.IsStacked ? child.Y : child.X) + actions.Gap;
        }
        FinishLayoutBox(origin, size);
    }

    internal void RenderSurfaceClose<TPresentation>(string id, string label, Action close) where TPresentation : IUiSurfacePresentation
    {
        var size = new UiVector2(24, 24);
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var interaction = ProcessButton(id, bounds, UiButtonActivation.Release);
        var resolved = ResolveId(id);
        interaction = ApplyAccessibilityInvoke(resolved, bounds, interaction);
        ExposeAccessibility(resolved, _accessibilityParent, UiAccessibilityRole.Button, label, bounds, valuePattern: false, literalLabel: true);
        TPresentation.PaintClose(new(this), bounds, interaction);
        if (interaction.Activated)
            close();
        FinishLayoutBox(origin, size);
    }
}
