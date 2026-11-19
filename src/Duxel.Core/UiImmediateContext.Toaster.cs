namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderToaster(UiToasterWidget widget, string id)
    {
        if (!widget.ContextOwner.TryGetTarget(out var prior) || !ReferenceEquals(prior, this))
        {
            foreach (var old in widget.Entries.Values) old.Dispose();
            widget.Entries.Clear(); widget.ContextOwner.SetTarget(this);
        }
        var controller = widget.Controller;
        var options = widget.Options;
        var isWindowActive = IsHostWindowActive && options.IsWindowActive;
        if (!IsControlEnabled || _displaySize.X <= 0 || _displaySize.Y <= 0 || _drawingOverlay is { Open: false }) return;
        var rootId = ResolveId(id);
        var availableHeight = MathF.Max(0, _displaySize.Y - options.Margin * 2);
        var width = MathF.Min(options.Width, MathF.Max(0, _displaySize.X - options.Margin * 2));
        var minimumHeight = UiToastContentLayout.MinimumHeight(width, _textSettings.Scale);
        var capacity = width < UiToastContentLayout.MinimumWidth ? 0
            : (int)Math.Clamp(Math.Floor(((double)availableHeight + options.Gap) / (minimumHeight + options.Gap)), 0, controller.Limit);
        var focusedToast = widget.Entries.Values.FirstOrDefault(entry => OverlayContains(entry.Input, _state.FocusedId));
        controller.SetViewportCapacity(capacity, focusedToast?.Snapshot.Id);
        foreach (var item in controller.Visible)
        {
            if (!widget.Entries.TryGetValue(item.Id, out var entry)) continue;
            controller.SetPaused(item.Id, UiToastPauseReason.Hover, entry.Input.ContainsScreenPoint(WidgetScreenPoint(_mousePosition))
                && !IsMouseOverHigherTierPopup(entry.Input.Tier));
            controller.SetPaused(item.Id, UiToastPauseReason.Focus, OverlayContains(entry.Input, _state.FocusedId));
            controller.SetPaused(item.Id, UiToastPauseReason.WindowInactive, !isWindowActive);
        }
        controller.Advance();
        var visible = controller.Visible;
        var live = visible.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var key in widget.Entries.Keys.Where(key => !live.Contains(key)).ToArray())
        {
            widget.Entries[key].Dispose(); widget.Entries.Remove(key);
        }
        var focusedEntry = widget.Entries.Values.FirstOrDefault(entry => entry.Input.Surface == _state.FocusedId
            || _state.FocusedId is { } focused && entry.Input.Members.Contains(focused));
        var shortcut = (focusedEntry is not null || _popupTierDepth >= _blockingKeyboardTier) && _keyEvents.Any(key => key.Key == UiKey.F6 && key.IsDown
            && (key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) == 0);
        if (shortcut && focusedEntry is not null)
        {
            if (focusedEntry.Input.Trigger is { } trigger)
            {
                FocusGroupControl(trigger, true);
                _focusedControlSeen = _currentTabStops.Any(stop => stop.Id == trigger);
                _focusedControlTier = focusedEntry.Input.TriggerTier;
            }
            if (focusedEntry.Input.Surface is { } priorSurface) _state.ReleasePreviousPopupKeyboard(priorSurface);
            _blockingKeyboardTier = Math.Max(_state.PreviousPopupKeyboardTier, _state.CurrentPopupKeyboardTier);
            shortcut = false;
        }
        var bottom = options.Position is UiToastPosition.Bottom or UiToastPosition.BottomStart or UiToastPosition.BottomEnd;
        var start = options.Position is UiToastPosition.TopStart or UiToastPosition.BottomStart;
        var center = options.Position is UiToastPosition.Top or UiToastPosition.Bottom;
        var left = options.RightToLeft ? !start : start;
        if (width <= 0) return;
        var x = center ? (_displaySize.X - width) / 2 : left ? options.Margin : _displaySize.X - options.Margin - width;
        var cursor = bottom ? _displaySize.Y - options.Margin : options.Margin;
        var maximumHeight = MathF.Max(0, _displaySize.Y - options.Margin * 2 - options.Gap * (visible.Count - 1))
            / Math.Max(1, visible.Count);
        using var screen = BeginWidgetScreenSpace();
        foreach (var item in visible)
        {
            if (!widget.Entries.TryGetValue(item.Id, out var entry))
            {
                entry = new(item, controller); widget.Entries.Add(item.Id, entry);
            }
            entry.Snapshot = item;
            entry.Content.Options = item.Options;
            entry.Content.ReducedMotion = options.ReducedMotion;
            entry.Content.AnimationPaused = !isWindowActive;
            entry.Content.RightToLeft = options.RightToLeft;
            var input = entry.Input;
            input.Attach(this);
            input.Surface = ResolveCachedIdentifier(rootId, "##toast/" + item.Id, false);
            input.SeenFrame = GetFrameCount(); input.Parent = _drawingOverlay;
            var context = new UiControlContext(this);
            entry.Content.Width = width;
            var size = context.MeasureParentDataChild(entry.Content, null, new(width, width, 0, maximumHeight));
            var targetY = bottom ? cursor - size.Y : cursor;
            cursor += (bottom ? -1 : 1) * (size.Y + options.Gap);
            var settings = new UiTransitionOptions(.2, UiAnimationEasing.OutCubic, options.ReducedMotion);
            var y = entry.Y.Update(targetY, _state.TimeSeconds, settings);
            var exiting = item.Status == UiToastStatus.Exiting;
            if (exiting && !entry.Exiting) { entry.Exiting = true; entry.ExitFrom = entry.LastOpacity; }
            var opacity = entry.Opacity.Update(exiting ? 0 : 1, _state.TimeSeconds, settings);
            var direction = bottom ? 1 : -1;
            var body = new UiRect(x, y.Value + (1 - opacity.Value) * 16 * direction, width, size.Y);
            if (opacity.NextFrameTimeSeconds is { } nextOpacity) RequestFrameAt(nextOpacity);
            if (y.NextFrameTimeSeconds is { } nextY) RequestFrameAt(nextY);
            if (exiting)
            {
                if (input.Open) CancelOverlay(input, OverlayContains(input, _state.FocusedId), false, false);
                if (!opacity.Running || input.SurfacePaint?.For(this).Content is null)
                {
                    controller.Remove(item.Id); entry.Dispose(); widget.Entries.Remove(item.Id);
                    continue;
                }
                var binding = input.SurfacePaint.For(this);
                var offset = (entry.ExitFrom - opacity.Value) * 16 * direction;
                var savedTier = _popupTierDepth;
                _popupTierDepth = input.Tier - 1; PushPopup();
                PushClipRect(new(0, 0, _displaySize.X, _displaySize.Y), false);
                try { CompositeInteractivePaintGroup(binding, entry.PaintBounds with { Y = entry.PaintBounds.Y + offset }, opacity.Value); }
                finally { PopClipRect(); PopPopup(); _popupTierDepth = savedTier; }
                continue;
            }
            var opened = !input.Open;
            if (opened)
            {
                input.Trigger = _state.FocusedId; input.TriggerTier = _popupTierDepth;
                RequestOverlayChange(input, true, false);
            }
            input.Bounds = body;
            var parentTier = _popupTierDepth;
            _popupTierDepth = ResolveOverlayTier(input, parentTier, opened) - 1;
            PushPopup(); PushClipRect(new(0, 0, _displaySize.X, _displaySize.Y), false);
            var parent = _accessibilityParent; var parentWidget = _accessibilityWidget; var drawing = _drawingOverlay;
            var tabStart = _currentTabStops.Count;
            var previousFocus = _state.FocusedId;
            var interactive = item.Options.CanDismiss || item.Options.Action is not null;
            var takeFocus = shortcut && interactive;
            if (takeFocus)
            {
                input.Trigger = previousFocus; input.TriggerTier = parentTier; input.TriggerSeenFrame = GetFrameCount();
                shortcut = false;
            }
            try
            {
                input.Tier = _popupTierDepth;
                input.RegisteredFrame = GetFrameCount();
                _state.AddPopupBlockingRect(body, input.Tier, UiPopupInputMode.NonModal, input.Surface);
                _drawingOverlay = input; _accessibilityParent = input.Surface; _accessibilityWidget = null;
                ExposeAccessibility(input.Surface, parent, UiAccessibilityRole.Group, item.Options.Title, body,
                    focusable : false, valuePattern : false, literalLabel : true, busy : item.Options.Progress is not null);
                if (OverlayOwnsEscape(input, false))
                    foreach (var key in _keyEvents)
                        if (key.IsDown && key.Modifiers == default)
                        {
                            if (key.Key == UiKey.Escape) controller.DismissAll();
                            else if (key.Key == UiKey.Delete) controller.Dismiss(item.Id);
                        }
                entry.Content.Shadow.Options = UiFluentShadow.Resolve(context.FluentColorScheme, UiShadowElevation.Shadow8);
                var paintBounds = entry.Content.Shadow.Options.PaintBounds(body);
                var capture = BeginInteractivePaintGroup(input.SurfacePaint ??= new(), paintBounds,
                    opacity.Running || y.Running ? UiPaintGroupTextMode.Projected : UiPaintGroupTextMode.PhysicalLcd);
                var complete = false;
                try
                {
                    RenderParentDataChild(entry.Content, null, body, new(0, 0, _displaySize.X, _displaySize.Y));
                    complete = true;
                }
                finally { EndInteractivePaintGroup(capture, complete, opacity.Value); }
                entry.PaintBounds = paintBounds;
                entry.LastOpacity = opacity.Value;
                var current = controller.Visible.FirstOrDefault(row => row.Id == item.Id);
                if (current?.Status == UiToastStatus.Exiting && input.Open)
                    CancelOverlay(input, OverlayContains(input, _state.FocusedId), false, false);
                if (interactive) CompleteOverlayFocus(input, input.Surface, body, tabStart, takeFocus, takeFocus, false, previousFocus);
                controller.SetPaused(item.Id, UiToastPauseReason.Hover, input.ContainsScreenPoint(WidgetScreenPoint(_mousePosition))
                    && !IsMouseOverHigherTierPopup(input.Tier));
                controller.SetPaused(item.Id, UiToastPauseReason.Focus, OverlayContains(input, _state.FocusedId));
                controller.SetPaused(item.Id, UiToastPauseReason.WindowInactive, !isWindowActive);
                if (!opacity.Running && current?.Status == UiToastStatus.Entering) controller.MarkVisible(item.Id);
                current = controller.Visible.FirstOrDefault(row => row.Id == item.Id);
                if (current?.Status == UiToastStatus.Visible && current.Revision == entry.Snapshot.Revision)
                    AnnounceToast(ref entry.Announcement, current.Options, input.Surface!, current.Revision);
            }
            finally
            {
                _drawingOverlay = drawing; _accessibilityParent = parent; _accessibilityWidget = parentWidget;
                PopClipRect(); PopPopup(); _popupTierDepth = parentTier;
            }
        }
        if (controller.NextTimeout is { } timeout) RequestFrameAt(_state.TimeSeconds + timeout.TotalSeconds);
    }
}
