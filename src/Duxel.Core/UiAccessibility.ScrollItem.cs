namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    private readonly record struct ScrollViewport(UiRect Bounds, UiRect EnclosingClip, UiAccessibilityScrollInfo Range,
        bool IndependentRoot);
    private readonly record struct ScrollAdjustment(long RuntimeId, double X, double Y);

    private Dictionary<string, ScrollAdjustment>? _scrollAdjustments;
    private Request? _activeReveal;
    private int _revealAttempts;

    internal void CaptureScrollViewport(string id, UiRect bounds, UiRect enclosingClip, in UiAccessibilityScrollInfo range,
        bool independentRoot = false)
    {
        _entries[id].ScrollViewport = new(bounds, enclosingClip, range, independentRoot);
    }

    internal UiVector2 ApplyScrollIntoView(string id, UiVector2 offset, UiVector2 maximum)
    {
        return TryGetScrollAdjustment(id, out var adjustment)
            ? new((float)Math.Clamp(adjustment.X, 0, maximum.X), (float)Math.Clamp(adjustment.Y, 0, maximum.Y)) : offset;
    }

    internal double ApplyVirtualScrollIntoView(string id, double offset, double maximum, bool horizontal) =>
        TryGetScrollAdjustment(id, out var adjustment) ? Math.Clamp(horizontal ? adjustment.X : adjustment.Y, 0, maximum) : offset;

    private bool TryGetScrollAdjustment(string id, out ScrollAdjustment adjustment)
    {
        adjustment = default;
        return _activeReveal is { } request && !Volatile.Read(ref request.Canceled)
            && _scrollAdjustments is not null && _scrollAdjustments.TryGetValue(id, out adjustment)
            && _entries.TryGetValue(id, out var entry) && entry.RuntimeId == adjustment.RuntimeId;
    }

    private Entry? FindScrollParent(Entry entry)
    {
        // Floating popup geometry does not move with its logical owner's scrolling viewport.
        if (entry.ScrollViewport is { IndependentRoot: true })
            return null;
        for (var parent = entry.Staged.ParentId; parent.Length != 0;)
        {
            if (!_entries.TryGetValue(parent, out var ancestor) || ancestor.Seen != _generation)
                return null;
            if (ancestor.ScrollViewport is not null)
                return ancestor;
            parent = ancestor.Staged.ParentId;
        }
        return null;
    }

    private void ExposeScrollItem(Entry entry)
    {
        if (entry.RevealBounds is not { Width: > 0, Height: > 0 })
            return;

        for (var owner = FindScrollParent(entry); owner is not null; owner = FindScrollParent(owner))
        {
            var viewport = owner.ScrollViewport!.Value;
            if (viewport.Bounds is not { Width: > 0, Height: > 0 })
                return;
            if (viewport.Range.HorizontallyScrollable || viewport.Range.VerticallyScrollable)
            {
                entry.Staged = entry.Staged with
                {
                    CanScrollIntoView = true
                };
                return;
            }
        }
    }

    private void ProcessScrollIntoView()
    {
        if (_activeReveal is { Result: not null })
            ResetScrollIntoView();
        var request = _activeReveal;
        _scrollAdjustments?.Clear();
        if (request is null)
        {
            foreach (var pending in _pending)
            {
                if (pending.Result is null && (pending.Action == UiAccessibilityAction.ScrollIntoView || pending.VirtualRevealBounds is not null || pending.TextScrollApplied)
                    && !Volatile.Read(ref pending.Canceled))
                {
                    request = pending;
                    break;
                }
            }
        }
        if (request is null)
            return;

        if (request.RevealAfterGeneration > _generation)
            return;

        var result = ResolveScrollIntoView(request);
        if (result is null)
        {
            _activeReveal = request;
            _revealAttempts++;
            return;
        }

        if (result == UiAccessibilityResult.Success && request.VirtualRevealBounds is not null)
        {
            request.VirtualRevealBounds = null;
        }
        else
        {
            request.Consumed = true;
            request.Result = result;
        }
        ResetScrollIntoView();
    }

    private UiAccessibilityResult? ResolveScrollIntoView(Request request)
    {
        if (Volatile.Read(ref request.Canceled))
            return UiAccessibilityResult.Timeout;
        if (!_entries.TryGetValue(request.Id, out var target) || target.RuntimeId != request.RuntimeId || target.Seen != _generation)
            return UiAccessibilityResult.Unavailable;
        if (!target.Staged.Enabled)
            return UiAccessibilityResult.Disabled;
        if (request.VirtualRevealBounds is { } slice && target.ScrollViewport is { } targetViewport
            && UiImmediateContext.IntersectRect(slice, targetViewport.EnclosingClip) is { Width: > 0, Height: > 0 })
            return UiAccessibilityResult.Success;
        var owner = FindScrollParent(target);
        var textScroll = request.TextScrollApplied ? request.TextScroll : null;
        var alignment = textScroll?.Alignment ?? request.ScrollAlignment;
        UiRect bounds;
        if (textScroll is not null)
        {
            if (ResolveTextScrollBounds(request, target, out bounds) is { } failure)
                return failure;
            var pointBounds = TextScrollPointBounds(target.Staged.Id, target.TextViewport!.Value);
            if (textScroll.Point is { } desired && (desired.X < pointBounds.X || desired.X >= pointBounds.X + pointBounds.Width
                || desired.Y < pointBounds.Y || desired.Y >= pointBounds.Y + pointBounds.Height))
                return UiAccessibilityResult.InvalidArgument;
            if (owner is null)
                return target.Staged.Offscreen ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
        }
        else if ((request.VirtualRevealBounds ?? target.RevealBounds) is { Width: > 0, Height: > 0 } itemBounds && owner is not null)
            bounds = itemBounds;
        else
            return UiAccessibilityResult.InvalidOperation;

        if (owner is not null && alignment?.Point is { } requestedPoint)
        {
            var outermost = owner;
            while (FindScrollParent(outermost) is { } parent)
                outermost = parent;
            var viewport = outermost.ScrollViewport!.Value;
            var visible = UiImmediateContext.IntersectRect(viewport.Bounds, viewport.EnclosingClip);
            if (visible is not { Width: > 0, Height: > 0 })
                return UiAccessibilityResult.InvalidOperation;
            if (requestedPoint.X < visible.X || requestedPoint.X >= visible.X + visible.Width
                || requestedPoint.Y < visible.Y || requestedPoint.Y >= visible.Y + visible.Height)
                return UiAccessibilityResult.InvalidArgument;
        }

        // Plan from the innermost viewport outward. Outer owners reveal the slice that remains inside each inner viewport.
        while (owner is not null)
        {
            if (!owner.Staged.Enabled)
                return UiAccessibilityResult.Disabled;

            var scroll = owner.ScrollViewport!.Value;
            var parent = FindScrollParent(owner);
            var visible = parent is null ? UiImmediateContext.IntersectRect(scroll.Bounds, scroll.EnclosingClip) : scroll.Bounds;
            if (visible is not { Width: > 0, Height: > 0 })
                return UiAccessibilityResult.InvalidOperation;
            var range = scroll.Range;
            var horizontalOffset = range.HorizontalRevealOffset;
            var reverse = range.ReversesHorizontalReveal;
            var x = alignment is null
                ? UiScrollReveal.Axis(bounds.X, bounds.Width, visible.X, visible.Width, horizontalOffset, range.HorizontalMaximum, reverse)
                : alignment.AlignAxis(bounds.X, bounds.Width, visible.X, visible.Width, horizontalOffset, range.HorizontalMaximum,
                    horizontal : true, reverseOffset : reverse);
            if (range.VirtualHorizontal is null)
                x = (float)x;
            var y = alignment is null
                ? UiScrollReveal.Axis(bounds.Y, bounds.Height, visible.Y, visible.Height, range.VerticalOffset, range.VerticalMaximum)
                : alignment.AlignAxis(bounds.Y, bounds.Height, visible.Y, visible.Height, range.VerticalOffset, range.VerticalMaximum, false);
            if (range.VirtualVertical is null)
                y = (float)y;
            if (x != horizontalOffset || y != range.VerticalOffset)
            {
                (_scrollAdjustments ??= new(StringComparer.Ordinal)).Add(owner.Staged.Id, new(owner.RuntimeId, x, y));
                bounds = bounds with
                {
                    X = bounds.X + (float)(reverse ? x - horizontalOffset : horizontalOffset - x),
                    Y = bounds.Y + (float)(range.VerticalOffset - y)
                };
            }
            bounds = UiImmediateContext.IntersectRect(bounds, visible);
            if (bounds is not { Width: > 0, Height: > 0 })
                return UiAccessibilityResult.InvalidOperation;

            owner = parent;
        }

        if (_scrollAdjustments is { Count: > 0 })
            return _revealAttempts < 2 ? null : UiAccessibilityResult.InvalidOperation;
        return target.Staged.Offscreen || request.VirtualRevealBounds is not null
            ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
    }

    private void ResetScrollIntoView()
    {
        _activeReveal = null;
        _revealAttempts = 0;
        _scrollAdjustments?.Clear();
    }
}
