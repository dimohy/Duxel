namespace Duxel.Core;

internal sealed class UiHorizontalScrollState
{
    internal float Offset, Maximum, ContentWidth;
    internal bool Candidate, Dragging, ThumbDrag, Visible, SuppressClick;
    internal float StartX, StartOffset;
    internal string? Selected;
    internal int SeenFrame = int.MinValue;
    internal WeakReference<UiImmediateContext>? Context;
    internal UiScrollbarMotion Motion;
    internal float OpacityValue;
    internal bool SetOffset(float value)
    {
        value = Math.Clamp(value, 0, Maximum);
        if (Offset == value)
            return false;
        Offset = value;
        return true;
    }
}

public sealed partial class UiImmediateContext
{
    internal void ProcessHorizontalScroll(UiRect bounds, float edge, UiHorizontalScrollState state, UiRect thumb = default, bool thumbVisible = false)
    {
        var sameContext = state.Context is not null && state.Context.TryGetTarget(out var previous) && ReferenceEquals(previous, this);
        if (!sameContext)
            state.Motion.Reset();
        var available = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier && !IsMouseOverHigherTierPopup(_popupTierDepth);
        if (!sameContext || state.SeenFrame != GetFrameCount() - 1 || !available || state.Maximum == 0)
            state.Candidate = state.Dragging = state.ThumbDrag = false;
        state.Context ??= new(this);
        state.Context.SetTarget(this);
        state.SeenFrame = GetFrameCount();
        state.SuppressClick = false;
        var header = new UiRect(bounds.X + (state.Offset > .01f ? edge : 0), bounds.Y,
            MathF.Max(0, bounds.Width - (state.Offset > .01f ? edge : 0) - (state.Offset < state.Maximum - .01f ? edge : 0)), bounds.Height);
        if (available && state.Maximum > 0)
        {
            if (_leftMousePressed && IsHovering(IntersectRect(header, CurrentClipRect)))
            {
                state.Candidate = true;
                state.StartX = _mousePosition.X;
                state.StartOffset = state.Offset;
                state.ThumbDrag = thumbVisible && Contains(thumb, _mousePosition);
            }
            if (state.Candidate && _leftMouseDown)
            {
                var delta = _mousePosition.X - state.StartX;
                if (state.ThumbDrag || MathF.Abs(delta) >= 4)
                    state.Dragging = true;
                if (state.Dragging)
                {
                    state.SuppressClick = true;
                    var travel = MathF.Max(1, bounds.Width - thumb.Width);
                    state.SetOffset(state.StartOffset + (state.ThumbDrag ? delta * state.Maximum / travel : -delta));
                }
            }
            if (IsHovering(IntersectRect(bounds, CurrentClipRect)) && (_mouseWheelHorizontal != 0 || _mouseWheel != 0))
            {
                var wheel = _mouseWheelHorizontal != 0 ? _mouseWheelHorizontal : -_mouseWheel;
                state.SetOffset(state.Offset + wheel * 48);
                _mouseWheel = _mouseWheelHorizontal = 0;
            }
        }
        if (!_leftMouseDown)
        {
            state.SuppressClick |= state.Dragging;
            state.Candidate = state.Dragging = state.ThumbDrag = false;
        }
    }
}
