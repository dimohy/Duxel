using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderDialog<TCloseBehavior, TPresentation>(UiDialog<TCloseBehavior, TPresentation> widget, string id, bool? controlled, bool? pending,
        UiControlActivationOrigin? origin)
        where TCloseBehavior : IUiPointerActivationBehavior
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
        if (!opened && !options.SuppressEscapeDismissal && OverlayOwnsEscape(input, includeTrigger: false)
            && string.IsNullOrEmpty(_imeHandler?.GetCompositionText())
            && _keyEvents.Any(key => key.IsDown && key.Key == UiKey.Escape && key.Modifiers == 0)
            && RequestOverlayChange(input, false, true))
            return;
        var viewport = GetOverlayViewport();
        var width = MathF.Min(options.Width, viewport.Width);
        var maximumHeight = MathF.Min(options.MaximumHeight ?? viewport.Height, viewport.Height);
        var padding = metrics.Padding + metrics.BorderWidth;
        var innerWidth = MathF.Max(0, width - 2 * padding);
        var innerMaximumHeight = MathF.Max(0, maximumHeight - 2 * padding);
        widget.Body.Configure(viewport.Width <= 480, viewport.Height <= 359, options.HasCloseButton, options.CloseLabel, innerMaximumHeight);
        var context = new UiControlContext(this);
        using var text = TPresentation.BeginBodyText(context);
        var natural = context.MeasureParentDataChild(widget.Body, null, new(innerWidth, innerWidth, 0, float.PositiveInfinity));
        var bodyHeight = MathF.Min(innerMaximumHeight, natural.Y);
        var height = MathF.Min(maximumHeight, bodyHeight + 2 * padding);
        var bounds = new UiRect(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
        var bodyBounds = new UiRect(bounds.X + padding, bounds.Y + padding, innerWidth, bodyHeight);
        context.MeasureParentDataChild(widget.Viewport, null, UiBoxConstraints.Tight(new(bodyBounds.Width, bodyBounds.Height)));
        if (opened && options.MotionEnabled && input.EntryFrom != new UiOverlayMotionSample(1, 1, 1))
            input.EnterStartedAt = _state.TimeSeconds;
        else if (InitialAnimationFrameDelayed && input.EnterStartedAt is { } entryStart && input.LastMotion == input.EntryFrom)
            input.EnterStartedAt = UiAnimationCurve.RebaseDelayedStart(entryStart, _state.TimeSeconds, UiTransitionOptions.DefaultFrameIntervalSeconds);
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
        input.AdditionalBlockingBounds = exclusive ? viewport : null;
        if (!opened && options.Type == UiDialogType.Modal && options.LightDismiss)
        {
            var inside = OverlayPointerInside(input);
            if (_leftMousePressed)
                input.BackdropPressed = !inside && IsHovering(viewport);
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
        PushClipRect(viewport, false);
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
                AddRectFilled(viewport, color, _whiteTexture);
            }
            var shadow = input.Shadow ??= new(TPresentation.Shadow(context));
            shadow.Options = TPresentation.Shadow(context);
            if (options.MotionEnabled)
                motionCapture = BeginInteractivePaintGroup(input.SurfacePaint ??= new(), shadow.Options.PaintBounds(bounds));
            if (motionActive)
                motionCoordinates = BeginWidgetAffineSpace(matrix, motion.Scale, viewport, rasterScale: 1);
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
        if (body.HeaderNavigation is { } navigation)
        {
            var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.NavigationHeight);
            var parent = _accessibilityParent;
            if (_accessibility is not null)
            {
                var id = GetID("##drawer-header-navigation");
                ExposeAccessibility(id, parent, UiAccessibilityRole.Navigation, navigation.AccessibleName ?? "Drawer navigation",
                    bounds, focusable : false, valuePattern : false, literalLabel : true);
                _accessibilityParent = id;
            }
            try { RenderParentDataChild(navigation, null, bounds, clip); }
            finally { _accessibilityParent = parent; }
        }
        var contentX = origin.X + body.NavigationOutset;
        var titleY = origin.Y + layout.NavigationHeight;
        var titleWidth = MathF.Max(0, layout.ContentWidth - (body.HasClose ? 24 + gap : 0));
        var titleBounds = new UiRect(contentX + (body.HasClose && body.RightToLeft ? 24 + gap : 0), titleY, titleWidth, layout.Title.Y);
        RenderParentDataChild(body.Title, null, titleBounds, clip);
        if (body.HasClose)
            RenderParentDataChild(body.Close, null, new(body.RightToLeft ? contentX : contentX + layout.ContentWidth - 24, titleY, 24, 24), clip);
        if (body.HeaderActions is { } headerActions)
            RenderParentDataChild(headerActions, null, new(contentX + (body.RightToLeft ? layout.ContentWidth - layout.HeaderActions.X : 0),
                origin.Y + layout.HeaderHeight - layout.HeaderActions.Y, layout.HeaderActions.X, layout.HeaderActions.Y), clip);
        var contentY = origin.Y + layout.HeaderHeight + gap;
        RenderParentDataChild(body.Content, null, new(contentX, contentY, layout.ContentWidth, layout.ContentHeight), clip);
        if (body.ScrollSeparator is { } separator && layout.ContentHeight > 0)
        {
            var scroll = body.Content.Scroll;
            if (scroll.Offset.Y > 0)
                AddRectFilled(new(contentX, contentY - 1, layout.ContentWidth, 1), separator, _whiteTexture);
            if (body.Actions.Children.Length > 0 && scroll.Offset.Y < scroll.VerticalScrollRange)
                AddRectFilled(new(contentX, contentY + layout.ContentHeight, layout.ContentWidth, 1), separator, _whiteTexture);
        }
        if (body.Actions.Children.Length > 0)
            RenderParentDataChild(body.Actions, null, new(contentX + (body.ActionsStart != body.RightToLeft ? 0 : layout.ContentWidth - layout.Actions.X),
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

    internal void RenderSurfaceClose<TPresentation>(string id, string label, Action close, UiButtonActivation activation)
        where TPresentation : IUiSurfacePresentation
    {
        var size = new UiVector2(24, 24);
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var interaction = ProcessButton(id, bounds, activation);
        var resolved = ResolveId(id);
        interaction = ApplyAccessibilityInvoke(resolved, bounds, interaction);
        ExposeAccessibility(resolved, _accessibilityParent, UiAccessibilityRole.Button, label, bounds, valuePattern: false, literalLabel: true);
        TPresentation.PaintClose(new(this), bounds, interaction);
        if (interaction.Activated)
            close();
        FinishLayoutBox(origin, size);
    }
}
