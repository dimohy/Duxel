namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderWheelPicker<T>(UiWheelPicker<T> wheel, string id) where T : struct
    {
        var context = new UiControlContext(this);
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var options = wheel.Options;
        var rowHeight = MathF.Max(options.RowHeight, typography.LineHeight + 8);
        var centerIndex = options.VisibleRows / 2;
        var size = context.ConstrainToLayout(new(options.Width, rowHeight * options.VisibleRows));
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        using var clipping = context.Clip(bounds);
        var interaction = ProcessButton<SpaceControlKeys>(id, bounds, UiButtonActivation.Release);
        var root = ResolveId(id);
        if (interaction.Hovered && _mouseWheel != 0 && IsControlEnabled)
        {
            wheel.WheelRemainder += _mouseWheel;
            var steps = (int)wheel.WheelRemainder;
            wheel.WheelRemainder -= steps;
            _mouseWheel = 0;
            if (steps != 0) wheel.Move(-steps);
            _requestFrame?.Invoke();
        }
        if (interaction.Held && IsControlEnabled)
        {
            if (_leftMousePressed)
            {
                wheel.LastPointerY = _mousePosition.Y;
                wheel.Dragged = false;
                wheel.DragRemainder = wheel.DragDistance = 0;
            }
            var delta = _mousePosition.Y - wheel.LastPointerY;
            wheel.LastPointerY = _mousePosition.Y;
            wheel.DragDistance += MathF.Abs(delta);
            if (wheel.DragDistance > 6) wheel.Dragged = true;
            wheel.DragRemainder += delta;
            var steps = (int)(wheel.DragRemainder / rowHeight);
            wheel.DragRemainder -= steps * rowHeight;
            if (steps != 0) { wheel.Move(-steps); _requestFrame?.Invoke(); }
        }
        if (interaction.Focused && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
            foreach (var key in _keyEvents)
            {
                if (!key.IsDown || key.Modifiers != 0) continue;
                var step = key.Key switch { UiKey.UpArrow => -1, UiKey.DownArrow => 1, UiKey.PageUp => -5, UiKey.PageDown => 5, _ => 0 };
                if (step != 0) { wheel.Move(step); _requestFrame?.Invoke(); }
                if (key.Key == UiKey.Enter) wheel.Choose(wheel.Value.Value);
            }
        if (interaction.Activated)
        {
            if (_leftMouseReleased && !wheel.Dragged)
            {
                var row = (int)((_mousePosition.Y - bounds.Y) / rowHeight);
                if (row >= 0 && row < options.VisibleRows && wheel.Items[row].Value is { } date) wheel.Choose(date);
            }
            else if (!_leftMouseReleased) wheel.Choose(wheel.Value.Value);
        }
        ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.ListBox,
            options.Label, bounds,
            focusable : false, valuePattern : false, literalLabel : true);
        _accessibility?.SetListContainer(root, false);
        var owner = _accessibilityWidget;
        _accessibilityWidget = null;
        try
        {
            var items = wheel.Items;
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                if (item.Value is not { } date) continue;
                var row = new UiRect(bounds.X + 8, bounds.Y + index * rowHeight, MathF.Max(0, bounds.Width - 16), rowHeight);
                var cell = ResolveCachedIdentifier(root, item.Key!, false);
                var clip = IntersectRect(CurrentClipRect, row);
                if (_accessibility is { } tree)
                    foreach (var action in (ReadOnlySpan<UiAccessibilityAction>)[UiAccessibilityAction.Invoke, UiAccessibilityAction.Select, UiAccessibilityAction.Focus])
                        if (tree.Take(cell, action) is { } request)
                        {
                            request.Result = RadioAccessibilityAvailability(true, clip);
                            if (request.Result != UiAccessibilityResult.Success) continue;
                            if (action == UiAccessibilityAction.Focus)
                            {
                                wheel.MoveTo(date);
                                ApplyAccessibilityFocus(root, clip, request);
                            }
                            else wheel.Choose(date);
                            _requestFrame?.Invoke();
                        }
                ExposeAccessibility(cell, root, UiAccessibilityRole.Option, item.Label!, row,
                    focusable : true, valuePattern : false, literalLabel : true);
                _accessibility?.SetListItem(cell, index == centerIndex, interaction.Focused && index == centerIndex, true);
                if (index == centerIndex)
                {
                    context.Frame(row, context.Theme.FrameBgActive, default, 4, 0);
                    if (interaction.Focused) (_accessibilityFocusOwner, _accessibilityFocusTarget) = (root, cell);
                    if (interaction.FocusVisible) context.FocusOutline(row, 4);
                }
                context.TypographyButtonText(item.Label!, row, context.MeasureText(item.Label!),
                    ApplyAlpha(context.Theme.Text, 1 - MathF.Abs(index - centerIndex) / (centerIndex + 2f)));
            }
        }
        finally { _accessibilityWidget = owner; }
        _lastItemPos = origin;
        _lastItemSize = size;
        _lastItemId = root;
    }
}
