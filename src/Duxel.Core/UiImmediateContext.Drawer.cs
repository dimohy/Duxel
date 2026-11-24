using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderDrawer<TCloseBehavior, TPresentation>(UiDrawer<TCloseBehavior, TPresentation> widget, string id, bool? controlled, bool? pending,
        UiControlActivationOrigin? origin)
        where TCloseBehavior : IUiPointerActivationBehavior
        where TPresentation : IUiDrawerPresentation
    {
        var input = widget.Input;
        var options = widget.Options;
        var metrics = TPresentation.Metrics;
        if (!float.IsFinite(metrics.Padding) || metrics.Padding < 0 || !float.IsFinite(metrics.Gap) || metrics.Gap < 0
            || !float.IsFinite(metrics.CornerRadius) || metrics.CornerRadius < 0 || !float.IsFinite(metrics.BorderWidth) || metrics.BorderWidth < 0
            || !float.IsFinite(metrics.MinimumContentHeight) || metrics.MinimumContentHeight < 0)
            throw new InvalidOperationException("Drawer presentation metrics must be finite and nonnegative.");
        var root = ResolveId(id);
        var surface = ResolveCachedIdentifier(root, "##surface", false);
        if (input.Surface is not null && input.Surface != surface) CancelOverlay(input, false, true, false);
        input.Surface = surface;
        input.SeenFrame = GetFrameCount();
        input.Parent = _drawingOverlay;
        var inline = input.Inline = options.Type == UiDrawerType.Inline;
        input.MotionEnabled = options.MotionEnabled;
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
        if (controlled == false && input.Open) CancelOverlay(input, OverlayContains(input, _state.FocusedId), false, true);
        if (pending == false && input.Open) RequestOverlayChange(input, false, true);
        if (!inline && !input.Open && controlled != true && pending != true) return;
        if (inline && !input.Open && controlled != true && pending != true
            && input.InlineMotion.Sample(false, options.MotionEnabled, _state.TimeSeconds) <= 0)
        {
            input.InlineMotion.Update(false, options.MotionEnabled, _state.TimeSeconds);
            input.SurfacePaint?.Dispose();
            input.SurfacePaint = null;
            return;
        }
        var context = new UiControlContext(this);
        var bottom = options.Position == UiDrawerPosition.Bottom;
        var rightToLeft = options.RightToLeft || context.RightToLeft;
        var left = options.Position == UiDrawerPosition.Start != rightToLeft;
        var viewport = GetOverlayViewport();
        UiRect natural;
        if (inline)
        {
            var size = widget.InlineNaturalSize(context);
            natural = new(0, 0, size.X, size.Y);
        }
        else
        {
            var extent = options.ResolveExtent(bottom ? viewport.Height : viewport.Width);
            natural = bottom ? new(viewport.X, viewport.Y + viewport.Height - extent, viewport.Width, extent)
                : new(left ? viewport.X : viewport.X + viewport.Width - extent, viewport.Y, extent, viewport.Height);
        }
        input.ClosedOffset = bottom ? new(0, natural.Height) : new(left ? -natural.Width : natural.Width, 0);
        var wasOpen = input.Open;
        if (controlled == true && !input.Open || pending == true && !input.Open)
        {
            if (origin is { } activation)
            {
                if (!ReferenceEquals(activation.ContextToken, _controlActivationToken))
                    throw new InvalidOperationException("A Drawer cannot use an activation origin from another UI context.");
                input.Trigger = activation.Id;
                input.TriggerTier = activation.Tier;
                input.TriggerSeenFrame = activation.Frame;
            }
            else { input.Trigger = _state.FocusedId; input.TriggerTier = _popupTierDepth; }
            RequestOverlayChange(input, true, false);
        }
        var inlineFrame = inline ? input.InlineMotion.Update(input.Open, options.MotionEnabled, _state.TimeSeconds) : default;
        if (inlineFrame.NextFrameTimeSeconds is { } nextInlineFrame) RequestFrameAt(nextInlineFrame);
        if (!input.Open && (!inline || inlineFrame.Value <= 0))
        {
            if (inline) { input.SurfacePaint?.Dispose(); input.SurfacePaint = null; }
            return;
        }
        var opened = !wasOpen;
        if (!inline && !opened && OverlayOwnsEscape(input, includeTrigger : false)
            && string.IsNullOrEmpty(_imeHandler?.GetCompositionText())
            && _keyEvents.Any(key => key.IsDown && key.Key == UiKey.Escape && key.Modifiers == 0)
            && RequestOverlayChange(input, false, true)) return;
        UiRect inlineAllocation = default;
        if (inline)
        {
            var size = UiDrawerInlineMotion.Allocation(new(natural.Width, natural.Height), inlineFrame.Value, bottom);
            var at = AllocateLayoutBox(size);
            inlineAllocation = new(at.X, at.Y, size.X, size.Y);
            natural = natural with { X = at.X, Y = at.Y };
            if (bottom) natural = natural with { Y = at.Y + size.Y - natural.Height };
            else if (!left) natural = natural with { X = at.X + size.X - natural.Width };
            if (!input.Open)
            {
                PushClipRect(inlineAllocation, true);
                try
                {
                    if (input.SurfacePaint is { } paint && paint.For(this) is { Content: not null } binding)
                        CompositeInteractivePaintGroup(binding, natural, 1);
                }
                finally { PopClipRect(); }
                FinishLayoutBox(at, size);
                return;
            }
        }
        using var screen = inline ? default : BeginWidgetScreenSpace();
        var padding = metrics.Padding + metrics.BorderWidth;
        var outset = widget.HeaderNavigation is null ? default : TPresentation.HeaderNavigationOutset;
        if (!float.IsFinite(outset.X) || !float.IsFinite(outset.Y) || outset.X < 0 || outset.Y < 0)
            throw new InvalidOperationException("Drawer header navigation outsets must be finite and nonnegative.");
        var sideOutset = MathF.Min(outset.X, metrics.Padding);
        var topOutset = MathF.Min(outset.Y, metrics.Padding);
        var sidePadding = padding - sideOutset;
        var bodyBounds = new UiRect(natural.X + sidePadding, natural.Y + padding - topOutset,
            MathF.Max(0, natural.Width - 2 * sidePadding), MathF.Max(0, natural.Height - 2 * padding + topOutset));
        var shortScreen = options.ScrollBody && (natural.Height <= 359 || !options.StickyHeaderFooter);
        widget.Body.Configure(false, shortScreen, options.ShowCloseButton, options.CloseLabel,
            bodyBounds.Height, !shortScreen, rightToLeft, actionsStart : true, scrollBody : options.ScrollBody, autoStackActions : true,
            navigationOutset : sideOutset);
        widget.Body.ScrollSeparator = !shortScreen && options.ScrollBody ? TPresentation.ScrollSeparator(context) : null;
        using var text = TPresentation.BeginBodyText(context);
        context.MeasureParentDataChild(widget.Viewport, null, UiBoxConstraints.Tight(new(bodyBounds.Width, bodyBounds.Height)));
        if (!inline && opened && input.MotionEnabled) input.EnterStartedAt = _state.TimeSeconds;
        var active = !inline && input.MotionEnabled && input.EnterStartedAt is { } start && _state.TimeSeconds - start < UiDrawerMotion.DurationSeconds;
        var motion = active ? UiDrawerMotion.Sample(_state.TimeSeconds - input.EnterStartedAt!.Value, input.EntryFrom) : new UiOverlayMotionSample(1, 1, 1);
        var matrix = Matrix3x2.CreateTranslation(motion.Translation.X, motion.Translation.Y);
        input.Bounds = inline ? WidgetScreenBounds(natural) : UiWidgetAffineLayout.MapBounds(natural, matrix);
        input.NaturalBounds = natural;
        input.LastMotion = motion;
        input.MotionNextAt = active ? Math.Min(input.EnterStartedAt!.Value + UiDrawerMotion.DurationSeconds, _state.TimeSeconds + 1d / 60) : null;
        if (!active) input.ResetMotion();
        var exclusive = !inline && options.Modal;
        var focusOnActivation = !inline && (opened || input.AutoFocusPending || exclusive && !input.WasExclusive && !OverlayContains(input, _state.FocusedId));
        input.AdditionalBlockingBounds = exclusive ? viewport : null;
        if (!inline && !opened && options.LightDismiss)
        {
            var inside = OverlayPointerInside(input);
            if (_leftMousePressed) input.BackdropPressed = !inside && IsHovering(viewport);
            var dismiss = _leftMouseReleased && input.BackdropPressed && !inside;
            if (!_leftMouseDown || _leftMouseReleased) input.BackdropPressed = false;
            if (dismiss && RequestOverlayChange(input, false, exclusive)) return;
        }
        var parentTier = _popupTierDepth;
        if (!inline)
        {
            _state.ReleasePreviousPopupKeyboard(surface);
            _blockingKeyboardTier = Math.Max(_state.PreviousPopupKeyboardTier, _state.CurrentPopupKeyboardTier);
            _popupTierDepth = ResolveOverlayTier(input, parentTier, opened) - 1;
            PushPopup();
            PushClipRect(viewport, false);
        }
        else PushClipRect(inlineAllocation, true);
        var parent = _accessibilityParent;
        var ownerWidget = _accessibilityWidget;
        var drawing = _drawingOverlay;
        var tabStart = _currentTabStops.Count;
        InteractivePaintCapture? capture = null;
        WidgetAffineSpaceScope? coordinates = null;
        var painted = false;
        try
        {
            input.Tier = _popupTierDepth;
            var previousFocus = _state.FocusedId;
            input.WasExclusive = exclusive;
            if (!inline)
            {
                _state.AddPopupBlockingRect(input.Bounds, input.Tier, exclusive ? UiPopupInputMode.Exclusive : UiPopupInputMode.NonModal, surface);
                if (input.AdditionalBlockingBounds is { } backdrop) _state.AddPopupBlockingRect(backdrop, input.Tier, UiPopupInputMode.NonModal, surface);
                input.RegisteredFrame = GetFrameCount();
                _blockingKeyboardTier = Math.Max(_blockingKeyboardTier, _state.CurrentPopupKeyboardTier);
            }
            input.BackdropColor = exclusive ? TPresentation.Backdrop(context) : default;
            if (exclusive)
            {
                var color = input.BackdropColor;
                color = new((color.Rgba & 0x00ffffff) | ((uint)MathF.Round((color.Rgba >> 24) * motion.BackdropOpacity) << 24));
                AddRectFilled(viewport, color, _whiteTexture);
            }
            if (inline && input.MotionEnabled && natural.Width > 0 && natural.Height > 0)
                capture = BeginInteractivePaintGroup(input.SurfacePaint ??= new(), natural);
            if (!inline)
            {
                var shadow = input.Shadow ??= new(TPresentation.Shadow(context));
                shadow.Options = TPresentation.Shadow(context);
                if (input.MotionEnabled) capture = BeginInteractivePaintGroup(input.SurfacePaint ??= new(), shadow.Options.PaintBounds(natural));
                if (active) coordinates = BeginWidgetAffineSpace(matrix, 1, viewport, rasterScale : 1);
                context.BoxShadow(shadow, natural, metrics.CornerRadius);
            }
            context.Frame(natural, TPresentation.Background(context), context.Theme.ForcedColors?.CanvasText ?? default,
                metrics.CornerRadius, context.Theme.ForcedColors is null ? 0 : metrics.BorderWidth);
            if (inline && options.Separator)
            {
                var edge = bottom ? new UiRect(natural.X, natural.Y, natural.Width, 1)
                    : new(left ? natural.X + natural.Width - 1 : natural.X, natural.Y, 1, natural.Height);
                context.Frame(edge, context.Theme.Separator, default, 0, 0);
            }
            _accessibilityWidget = null;
            _accessibilityParent = surface;
            _drawingOverlay = input;
            ExposeAccessibility(surface, parent, inline ? UiAccessibilityRole.Group : UiAccessibilityRole.Dialog,
                widget.AccessibleName ?? widget.Title, natural, focusable : false, valuePattern : false, literalLabel : true);
            if (!inline)
            {
                _accessibility?.SetModal(surface, exclusive);
                _accessibility?.SetDialogDescription(surface, options.Description);
            }
            PushStyleColor(UiStyleColor.Text, TPresentation.Foreground(context));
            try { RenderParentDataChild(widget.Viewport, null, bodyBounds, bodyBounds); }
            finally { PopStyleColor(); }
            CompleteOverlayFocus(input, surface, natural, tabStart, focusOnActivation, !inline, exclusive, previousFocus);
            input.AutoFocusPending = !inline && active && input.Members.Count == 0;
            painted = true;
        }
        finally
        {
            try
            {
                coordinates?.Dispose();
                if (capture is { } captured) EndInteractivePaintGroup(captured, painted && input.Open, motion.Opacity,
                    UiWidgetAffineLayout.MapBounds(captured.Bounds, matrix));
            }
            finally
            {
                _accessibilityParent = parent;
                _accessibilityWidget = ownerWidget;
                _drawingOverlay = drawing;
                PopClipRect();
                if (!inline) PopPopup();
                _popupTierDepth = parentTier;
            }
        }
        if (inline) FinishLayoutBox(new(inlineAllocation.X, inlineAllocation.Y), new(inlineAllocation.Width, inlineAllocation.Height));
    }
}
