namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private bool OpenPopoverAt(UiPopoverInputState input, UiVector2 point)
    {
        point = WidgetScreenPoint(point);
        var previous = input.ContextPoint;
        if (!RequestOverlayChange(input, true, false))
            return false;
        if (input.Open)
        {
            input.ContextPoint = point;
            if (previous != point)
                _requestFrame?.Invoke();
        }
        return true;
    }
    private void ProcessPopoverPointerTrigger(UiPopoverInputState input, UiPopoverOptions options, UiRect trigger, bool hovered, bool available)
    {
        if (!available)
        {
            input.HoverCloseAt = null;
            input.HoverInside = input.ContextPress = false;
            return;
        }
        if (options.TriggerMode == UiPopoverTriggerMode.Context)
        {
            if (_rightMousePressed)
                input.ContextPress = hovered;
            var keyboard = _state.FocusedId == input.Trigger && _keyEvents.Any(key => key.IsDown
                && (key.Key == UiKey.ContextMenu && key.Modifiers == 0 || key.Key == UiKey.F10 && key.Modifiers == KeyModifiers.Shift));
            if (_rightMouseReleased && input.ContextPress && hovered)
                OpenPopoverAt(input, _mousePosition);
            else if (keyboard)
                OpenPopoverAt(input, new(trigger.X + trigger.Width / 2, trigger.Y + trigger.Height / 2));
            if (!_rightMouseDown || _rightMouseReleased)
                input.ContextPress = false;
            return;
        }
        if (options.TriggerMode != UiPopoverTriggerMode.Hover)
            return;
        var inside = hovered || input.Open && OverlayPointerInside(input, trigger);
        var entered = inside && !input.HoverInside;
        var left = !inside && input.HoverInside;
        input.HoverInside = inside;
        if (inside)
        {
            input.HoverCloseAt = null;
            if (entered && !input.Open)
                RequestOverlayChange(input, true, false);
        }
        else if (left && input.Open)
            input.HoverCloseAt = _state.TimeSeconds + options.MouseLeaveDelaySeconds;
        if (!inside && input.HoverCloseAt is { } deadline && _state.TimeSeconds >= deadline)
        {
            input.HoverCloseAt = null;
            RequestOverlayChange(input, false, false);
        }
    }
}
