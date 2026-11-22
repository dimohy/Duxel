namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private ulong _textDragScrollRevision;
    private UiVector2 _textDragScrollRemaining;
    private int _textDragScrollTier;
    private int _textDragScrollTransform;

    // A captured selection scrolls the nearest viewport that can still move in each direction.
    // Ancestors consume the remainder in reverse render order, just like nested wheel input.
    private UiVector2 ApplyTextDragScroll(UiRect visible, UiVector2 offset, UiVector2 maximum, float deltaTime, out UiVector2 hit)
    {
        hit = ClampTextDragHit(visible, _mousePosition);
        var outside = new UiVector2(_mousePosition.X - hit.X, _mousePosition.Y - hit.Y);
        var delta = new UiVector2(SelectionScrollSpeed(outside.X) * deltaTime, SelectionScrollSpeed(outside.Y) * deltaTime);
        if (delta == default)
            return offset;
        var next = ClampTextScroll(offset, delta, maximum);
        _textDragScrollRemaining = new(delta.X - (next.X - offset.X), delta.Y - (next.Y - offset.Y));
        if (_textDragScrollRemaining != default)
        {
            unchecked { _textDragScrollRevision++; }
            _textDragScrollTier = _popupTierDepth;
            _textDragScrollTransform = _widgetTransformDepth;
        }
        if (next != offset)
            _requestFrame?.Invoke();
        return next;
    }

    private UiVector2 ConsumeTextDragScroll(ulong beforeChildren, UiVector2 offset, UiVector2 maximum)
    {
        if (beforeChildren == _textDragScrollRevision || _textDragScrollRemaining == default
            || _textDragScrollTier != _popupTierDepth || _textDragScrollTransform != _widgetTransformDepth)
            return offset;
        var next = ClampTextScroll(offset, _textDragScrollRemaining, maximum);
        _textDragScrollRemaining = new(_textDragScrollRemaining.X - (next.X - offset.X),
            _textDragScrollRemaining.Y - (next.Y - offset.Y));
        return next;
    }

    private static UiVector2 ClampTextDragHit(UiRect visible, UiVector2 point) =>
        new(Math.Clamp(point.X, visible.X, visible.X + visible.Width),
            Math.Clamp(point.Y, visible.Y, visible.Y + visible.Height));

    private static UiVector2 ClampTextScroll(UiVector2 offset, UiVector2 delta, UiVector2 maximum) =>
        new(Math.Clamp(offset.X + delta.X, 0, maximum.X), Math.Clamp(offset.Y + delta.Y, 0, maximum.Y));

    private static float SelectionScrollSpeed(float overshoot) => overshoot == 0 ? 0
        : MathF.CopySign(MathF.Min(1100, 64 + MathF.Abs(overshoot) * 9), overshoot);
}
