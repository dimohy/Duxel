namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private string? _presenceRemovedFocus;
    internal void RenderPresenceChild(UiWidget child, UiPresencePaint paint, UiRect bounds, UiRect clip, bool live, float opacity)
    {
        if (!live)
        {
            if (_state.FocusedId is { } focused && paint.Members.Contains(focused))
                _presenceRemovedFocus = focused;
            if (_state.ActiveId is { } active && paint.Members.Contains(active))
            {
                _state.ActiveId = null;
                _imeHandler?.SetCompositionOwner(null);
                CancelControlSpace();
            }
            if (opacity <= 0 || !paint.Captured)
                return;
            using var clipped = BeginControlClip(clip);
            var binding = paint.Group.For(this);
            if (binding.Content is { } content)
                CompositeInteractivePaintGroup(binding, content.Bounds with
                {
                    X = content.Bounds.X + bounds.X - paint.Root.X,
                    Y = content.Bounds.Y + bounds.Y - paint.Root.Y
                }, opacity);
            return;
        }
        var captureBounds = IntersectRect(bounds, IntersectRect(clip, CurrentClipRect));
        if (captureBounds.Width <= 0 || captureBounds.Height <= 0)
        {
            RenderParentDataChild(child, null, bounds, clip);
            return;
        }
        var capture = BeginInteractivePaintGroup(paint.Group, captureBounds);
        var first = _currentTabStops.Count;
        var completed = false;
        try
        {
            RenderParentDataChild(child, null, bounds, clip);
            completed = true;
        }
        finally
        {
            EndInteractivePaintGroup(capture, completed, opacity);
        }
        paint.Members.Clear();
        for (var i = first; i < _currentTabStops.Count; i++)
            paint.Members.Add(_currentTabStops[i].Id);
        paint.Root = bounds;
        paint.Captured = true;
    }

    private void CompletePresenceFocusRetirement()
    {
        var removed = _presenceRemovedFocus;
        _presenceRemovedFocus = null;
        if (removed is null || _state.FocusedId != removed)
            return;
        var previous = _previousTabStops.FindIndex(x => x.Id == removed);
        var candidate = -1;
        if (previous >= 0)
        {
            for (var i = previous + 1; i < _previousTabStops.Count; i++)
                if ((candidate = _currentTabStops.FindIndex(x => x.Id == _previousTabStops[i].Id)) >= 0)
                    break;
            if (candidate < 0)
                for (var i = previous - 1; i >= 0; i--)
                    if ((candidate = _currentTabStops.FindIndex(x => x.Id == _previousTabStops[i].Id)) >= 0)
                        break;
        }
        if (candidate < 0)
        {
            _state.FocusedId = _ownedControlFocus = null;
            _state.ActiveId = null;
            _imeHandler?.SetCompositionOwner(null);
            CancelControlSpace();
            return;
        }
        var stop = _currentTabStops[candidate];
        FocusGroupControl(stop.Id, keyboard: true);
        _focusedControlSeen = true;
        _focusedControlTier = stop.PopupTier;
    }
}
